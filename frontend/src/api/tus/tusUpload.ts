import {
  DefaultHttpStack,
  DetailedError,
  Upload,
  type HttpRequest,
  type HttpResponse,
  type UploadOptions,
} from 'tus-js-client'
import { ApiError, redirectToLoginIfSessionEnded } from '../client'
import { apiUrl } from '../config'
import {
  RETRY_DELAYS,
  concatenateUploads,
  createPartialUpload,
  createUpload,
  delay,
  getUploadInfo,
  isGone,
  isTemporary,
} from './tusProtocol'

const MAX_PARALLEL_PARTS = 6
const MIN_PART_SIZE = 64 * 1024 * 1024
const MIN_CHUNK_SIZE = 8 * 1024 * 1024
// Each chunk becomes one Azure block, and a part takes at most 50,000 of them.
const CHUNKS_PER_PART_BUDGET = 40_000

// How long an upload may go without progress or a successful response before it fails.
const STALL_TIMEOUT_MS = 120_000
// How often progress is reported at most.
const PROGRESS_INTERVAL_MS = 200
// How much time the measured upload speed is averaged over.
const SPEED_WINDOW_MS = 3000

/** One upload partial, representing a contiguous segment of the file. */
export type UploadPart = {
  path: string
  start: number
  length: number
}

/** The plan for the upload: a single contiguous upload, or several parts concatenated at the end. */
export type UploadPlan = {
  fileTransferId: string
  parts: UploadPart[]
}

export type UploadProgress = {
  loaded: number
  total: number
  percent: number
  bytesPerSecond: number | null
  secondsRemaining: number | null
}

export type UploadRunStatus = 'uploading' | 'pausing' | 'paused' | 'finishing'

export type StartUploadOptions = {
  onProgress?: (progress: UploadProgress) => void
  onStatus?: (status: UploadRunStatus) => void
  alreadySent?: number
  /** For a pause that came while the transfer was still being created */
  paused?: boolean
}

/** An upload under way. `finished` settles once it has succeeded, failed or been aborted. */
export type UploadRun = {
  finished: Promise<void>
  pause: () => void
  resume: () => Promise<void>
  abort: () => void
}

/** Resuming cannot help: the plan's uploads are gone, or the transfer no longer takes them. */
export class UploadGoneError extends ApiError {
  constructor(message: string, status: number, body?: unknown) {
    super(message, status, body)
    this.name = 'UploadGoneError'
  }
}

export async function createUploadPlan(
  fileTransferId: string,
  file: File,
  signal?: AbortSignal,
): Promise<UploadPlan> {
  const ranges = splitIntoParts(file.size)

  // Single-part upload plan
  if (ranges.length === 1) {
    const path = await createUpload(fileTransferId, file.size, signal)
    return { fileTransferId, parts: [{ path, ...ranges[0] }] }
  }

  // Concatenated upload plan
  const parts: UploadPart[] = []
  try {
    for (const range of ranges) {
      const path = await createPartialUpload(fileTransferId, range.length, signal)
      parts.push({ path, ...range })
    }
  } catch (error) {
    // Without the whole plan, nothing will ever upload to the partials created so far.
    discardUpload({ fileTransferId, parts })
    throw error
  }

  return { fileTransferId, parts }
}

export function startUpload(
  plan: UploadPlan,
  file: File,
  { onProgress, onStatus, alreadySent = 0, paused: startPaused = false }: StartUploadOptions = {},
): UploadRun {
  const concatenated = plan.parts.length > 1
  const sentPerPart: (number | null)[] = plan.parts.map(() => null)
  let partsDone = 0
  let paused = false
  let allSent = false
  let settled = false
  let failure: Error | undefined
  let reported: UploadRunStatus | null = null

  const progress = trackProgress(file.size, alreadySent, onProgress)
  const transport = pausableHttpStack(report)
  const concatenation = new AbortController()
  const watchdog = stallWatchdog(STALL_TIMEOUT_MS, () => {
    // A paused upload is quiet on purpose, once the requests already sent have finished.
    if (paused && !transport.isSending()) {
      watchdog.keepAlive()
      return
    }
    abortParts()
    finish(failure ?? new ApiError('The upload stopped responding', 0))
  })

  let settle: (error?: Error) => void = () => {}
  const finished = new Promise<void>((resolve, reject) => {
    settle = (error) => (error ? reject(error) : resolve())
  })

  function status(): UploadRunStatus {
    if (paused) {
      return transport.isSending() ? 'pausing' : 'paused'
    }
    return allSent ? 'finishing' : 'uploading'
  }

  function report() {
    const current = status()
    if (!settled && current !== reported) {
      reported = current
      progress.show()
      onStatus?.(current)
    }
  }

  function finish(error?: Error) {
    if (settled) {
      return
    }
    settled = true
    void transport.stop()
    concatenation.abort()
    watchdog.clear()
    settle(error)
  }

  // Each part is an upload of its own. Without an endpoint, tus-js-client fails a part that is gone
  // instead of quietly creating a new one in its place.
  const uploads = plan.parts.map(
    (part, index) =>
      new Upload(file.slice(part.start, part.start + part.length), {
        ...transportOptions(),
        uploadUrl: apiUrl(part.path),
        chunkSize: chunkSizeFor(plan),
        retryDelays: RETRY_DELAYS,
        httpStack: transport,
        storeFingerprintForResuming: false,
        onAfterResponse: (_request, response) => {
          if (response.getStatus() < 400) {
            watchdog.keepAlive()
          }
        },
        onProgress: (sent) => {
          if (settled) {
            return
          }
          // A part's first report jumps to wherever it had got to, which can be an absurd value and says nothing about upload speed.
          if (sentPerPart[index] === null) {
            progress.restartRate()
          }
          sentPerPart[index] = sent
          const total = sentPerPart.reduce<number>((sum, bytes) => sum + (bytes ?? 0), 0)
          watchdog.keepAlive()
          progress.report(total)
          // A part that re-sends its last chunk takes this back.
          allSent = concatenated && total >= file.size
          report()
        },
        onSuccess: () => {
          partsDone++
          if (partsDone === plan.parts.length) {
            complete()
          }
        },
        // The other parts are still sending when one fails. Settling before they finish would let
        // the next run collide with them on the server's upload locks.
        onError: (error) => {
          failure ??= asApiError(error)
          void transport.stop().then(() => finish(failure))
        },
      }),
  )

  function complete() {
    if (!concatenated) {
      finish()
      return
    }
    const paths = plan.parts.map((part) => part.path)
    concatenateUploads(plan.fileTransferId, paths, concatenation.signal, watchdog.keepAlive).then(
      () => finish(),
      (error: Error) =>
        finish(
          error instanceof ApiError && isGone(error.status)
            ? new UploadGoneError(error.message, error.status, error.body)
            : error,
        ),
    )
  }

  function abortParts() {
    for (const upload of uploads) {
      void upload.abort()
    }
  }

  function pause() {
    // Only the concatenation is left once every part is in, and holding it gains nothing.
    if (partsDone === plan.parts.length) {
      return
    }
    paused = true
    transport.hold()
    report()
  }

  if (startPaused) {
    pause()
  } else {
    report()
  }
  watchdog.keepAlive()
  for (const upload of uploads) {
    upload.start()
  }

  return {
    finished,
    pause,
    async resume() {
      if (settled || !paused) {
        return
      }
      paused = false
      watchdog.keepAlive()
      progress.restartRate()
      report()
      // A pause can outlive the session.
      if (await redirectToLoginIfSessionEnded()) {
        return
      }
      // In case it was paused again while the session was checked.
      if (!paused) {
        transport.release()
      }
    },
    abort() {
      abortParts()
      finish(abortError())
    },
  }
}

/**
 * How many of the file's bytes the server already holds, or null when the plan is no longer 
 * usable — its uploads expired, or the transfer was finished by someone else.
 */
export async function readUploadedBytes(
  plan: UploadPlan,
  signal?: AbortSignal,
): Promise<number | null> {
  for (let attempt = 0; ; attempt++) {
    try {
      return await readOffsets(plan, signal)
    } catch (error) {
      // A lock left behind by an abandoned request expires on its own, and a server error may pass.
      const temporary = error instanceof ApiError && isTemporary(error.status)
      if (attempt >= RETRY_DELAYS.length || !temporary) {
        throw error
      }
      await delay(RETRY_DELAYS[attempt], signal)
    }
  }
}

/** Gives up every upload in a plan, best effort. */
export function discardUpload(plan: UploadPlan): void {
  for (const part of plan.parts) {
    const options = { ...transportOptions(), retryDelays: RETRY_DELAYS }
    Upload.terminate(apiUrl(part.path), options).catch(() => undefined)
  }
}

export function isUploadGone(error: unknown): boolean {
  return error instanceof UploadGoneError
}

function splitIntoParts(size: number): { start: number; length: number }[] {
  const count = Math.min(MAX_PARALLEL_PARTS, Math.floor(size / MIN_PART_SIZE))
  if (count < 2) {
    return [{ start: 0, length: size }]
  }

  const partSize = Math.ceil(size / count)
  return Array.from({ length: count }, (_, index) => {
    const start = index * partSize
    return { start, length: Math.min(partSize, size - start) }
  })
}

function trackProgress(
  total: number,
  alreadySent: number,
  onProgress?: (progress: UploadProgress) => void,
) {
  let reportedAt = 0
  let rateWindow: { at: number; loaded: number } | null = null
  let bytesPerSecond: number | null = null
  let loaded = alreadySent

  function emit() {
    onProgress?.({
      loaded,
      total,
      percent: total > 0 ? Math.min(100, Math.floor((loaded / total) * 100)) : 100,
      bytesPerSecond,
      secondsRemaining:
        bytesPerSecond !== null && bytesPerSecond > 0 ? (total - loaded) / bytesPerSecond : null,
    })
  }

  return {
    report(sent: number) {
      // Kept even when not shown, so the next report, or the one on resume, is not behind.
      loaded = Math.max(loaded, sent)
      const now = performance.now()
      if (now - reportedAt < PROGRESS_INTERVAL_MS) {
        return
      }
      reportedAt = now

      rateWindow ??= { at: now, loaded }
      const elapsed = now - rateWindow.at
      if (elapsed >= SPEED_WINDOW_MS) {
        bytesPerSecond = ((loaded - rateWindow.loaded) * 1000) / elapsed
        rateWindow = { at: now, loaded }
      }
      emit()
    },
    // The rate before a pause says nothing about the rate after it.
    restartRate() {
      rateWindow = null
      bytesPerSecond = null
      emit()
    },
    /** Shows the latest figure now, whether or not the interval has passed. */
    show: emit,
  }
}

// tus-js-client has no pause of its own, but it lets the transport be supplied. Holding requests
// pauses the upload without aborting any, which would leave the server holding locks.
function pausableHttpStack(onRequestDone: () => void) {
  const stack = new DefaultHttpStack({})
  const inFlight = new Set<Promise<HttpResponse>>()
  let held: Promise<void> | null = null
  let letThrough = () => {}
  let stopped = false

  const hold = () => {
    held ??= new Promise((resolve) => (letThrough = resolve))
  }

  return {
    getName: () => 'PausableHttpStack',
    createRequest(method: string, url: string): HttpRequest {
      const request = stack.createRequest(method, url)
      const send = request.send.bind(request)
      request.send = async (body: unknown) => {
        while (held) {
          await held
        }
        const response = send(body)
        const forget = () => {
          inFlight.delete(response)
          onRequestDone()
        }
        inFlight.add(response)
        response.then(forget, forget)
        return response
      }
      return request
    },
    isSending: () => inFlight.size > 0,
    hold,
    release() {
      if (!stopped) {
        letThrough()
        held = null
      }
    },
    /** Holds every request for good, and settles once those already sent have finished. */
    async stop() {
      stopped = true
      hold()
      await Promise.allSettled(inFlight)
    },
  }
}

function stallWatchdog(timeoutMs: number, onStall: () => void) {
  let timer: ReturnType<typeof setTimeout>

  return {
    keepAlive() {
      clearTimeout(timer)
      timer = setTimeout(onStall, timeoutMs)
    },
    clear() {
      clearTimeout(timer)
    },
  }
}

// tus-js-client sends no cookies of its own accord, and Broker refuses a cookie-authenticated
// mutation without the CSRF marker.
function transportOptions(): UploadOptions {
  return {
    headers: { 'X-Requested-With': 'XMLHttpRequest' },
    onBeforeRequest: (request) => {
      const xhr = request.getUnderlyingObject() as XMLHttpRequest
      xhr.withCredentials = true
    },
    onShouldRetry: shouldRetry,
  }
}

function shouldRetry(error: DetailedError): boolean {
  const status = error.originalResponse?.getStatus() ?? 0

  if (status === 401) {
    void redirectToLoginIfSessionEnded()
    return false
  }

  // On a PATCH, a 409 is an offset mismatch, which the HEAD before the next try sorts out. Anywhere
  // else it is Broker refusing uploads to a transfer that is done with them.
  if (status === 409) {
    return error.originalRequest?.getMethod() === 'PATCH'
  }

  return status === 0 || isTemporary(status)
}

function chunkSizeFor(plan: UploadPlan): number {
  const longest = Math.max(...plan.parts.map((part) => part.length))
  return Math.max(MIN_CHUNK_SIZE, Math.ceil(longest / CHUNKS_PER_PART_BUDGET))
}

function asApiError(error: Error): Error {
  if (!(error instanceof DetailedError)) {
    return error
  }

  const response = error.originalResponse
  const status = response?.getStatus() ?? 0
  const body = response?.getBody()
  return isGone(status) && !shouldRetry(error)
    ? new UploadGoneError(error.message, status, body)
    : new ApiError(error.message, status, body)
}

async function readOffsets(plan: UploadPlan, signal?: AbortSignal): Promise<number | null> {
  const uploads = await Promise.all(plan.parts.map((part) => getUploadInfo(part.path, signal)))

  return uploads.reduce<number | null>((total, upload, index) => {
    if (total === null || upload === null) {
      return null
    }
    return total + Math.min(upload.offset, plan.parts[index].length)
  }, 0)
}

function abortError(): DOMException {
  return new DOMException('Upload aborted', 'AbortError')
}
