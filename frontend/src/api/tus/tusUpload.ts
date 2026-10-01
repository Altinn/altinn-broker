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
import { createPartialUpload, createUpload, getUploadInfo, tusUploadPath } from './tusProtocol'

const MAX_PARALLEL_PARTS = 6
const MIN_PART_SIZE = 64 * 1024 * 1024
const MIN_CHUNK_SIZE = 8 * 1024 * 1024
const CHUNKS_PER_PART_BUDGET = 40_000

// Retry to wait out a lock or offset conflict left by a request this client abandoned.
const RETRY_DELAYS = [1000, 2000, 4000, 8000, 15000, 15000, 15000]
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

/** The plan for the upload; whether it is concatenated from multiple parts or a single contiguous upload. */
export type UploadPlan = {
  fileTransferId: string
  parts: UploadPart[]
  concatenated: boolean
}

export type UploadProgress = {
  loaded: number
  total: number
  percent: number
  bytesPerSecond: number | null
  secondsRemaining: number | null
}

export type UploadPhase = 'finishing' | 'paused'

export type StartUploadOptions = {
  onProgress?: (progress: UploadProgress) => void
  onPhase?: (phase: UploadPhase) => void
  alreadySent?: number
}

/** An upload under way. `finished` settles once it has succeeded, failed or been aborted. */
export type UploadRun = {
  finished: Promise<void>
  pause: () => void
  resume: () => Promise<void>
  abort: () => void
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
    return { fileTransferId, parts: [{ path, ...ranges[0] }], concatenated: false }
  }

  // Concatenated upload plan
  const parts: UploadPart[] = []
  for (const range of ranges) {
    const path = await createPartialUpload(fileTransferId, range.length, signal)
    parts.push({ path, ...range })
  }

  return { fileTransferId, parts, concatenated: true }
}

export function startUpload(
  plan: UploadPlan,
  file: File,
  { onProgress, onPhase, alreadySent = 0 }: StartUploadOptions = {},
): UploadRun {
  const progress = trackProgress(file.size, alreadySent, onProgress)
  const transport = pausableHttpStack(() => onPhase?.('paused'))
  const watchdog = stallWatchdog(STALL_TIMEOUT_MS, () => {
    // A paused upload is quiet on purpose.
    if (transport.isPaused()) {
      watchdog.keepAlive()
      return
    }
    finish(new ApiError('The upload stopped responding', 0))
  })

  let settled = false
  let settle: (error?: Error) => void = () => {}
  const finished = new Promise<void>((resolve, reject) => {
    settle = (error) => (error ? reject(error) : resolve())
  })

  function finish(error?: Error) {
    if (settled) {
      return
    }
    settled = true
    void transport.stop()
    watchdog.clear()
    settle(error)
  }

  const upload = new Upload(file, {
    ...transportOptions(),
    ...targetOptions(plan),
    endpoint: apiUrl(tusUploadPath(plan.fileTransferId)),
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
      watchdog.keepAlive()
      progress.report(sent)
      if (plan.concatenated && sent >= file.size) {
        onPhase?.('finishing')
      }
    },
    onSuccess: () => finish(),
    // The other parallel parts are still sending when the first one fails. Settling before they
    // finish would let the next run collide with them on the server's upload locks.
    onError: (error) => {
      void transport.stop().then(() => finish(asApiError(error)))
    },
  })

  // tus-js-client only takes existing parallel uploads in the form of a previous upload to resume.
  if (plan.concatenated) {
    upload.resumeFromPreviousUpload({
      uploadUrl: null,
      parallelUploadUrls: plan.parts.map((part) => apiUrl(part.path)),
      urlStorageKey: '',
      size: file.size,
      metadata: {},
      creationTime: new Date().toISOString(),
    })
  }

  watchdog.keepAlive()
  upload.start()

  let resumeAttempt = 0
  return {
    finished,
    pause() {
      resumeAttempt++
      transport.pause()
    },
    async resume() {
      const attempt = ++resumeAttempt
      if (await redirectToLoginIfSessionEnded()) {
        return
      }
      // Paused again while the session was checked.
      if (attempt !== resumeAttempt) {
        return
      }
      progress.restartRate()
      transport.resume()
    },
    abort() {
      void upload.abort()
      finish(abortError())
    },
  }
}

/**
 * How many of the file's bytes the server already holds, or null when the plan is no longer good
 * for anything — its uploads expired, or the transfer was finished by someone else.
 */
export async function readUploadedBytes(
  plan: UploadPlan,
  signal?: AbortSignal,
): Promise<number | null> {
  for (let attempt = 0; ; attempt++) {
    try {
      return await readOffsets(plan, signal)
    } catch (error) {
      // A lock left behind by an abandoned request should eventually expire on its own
      if (attempt >= RETRY_DELAYS.length || !isLocked(error)) {
        throw error
      }
      await delay(RETRY_DELAYS[attempt], signal)
    }
  }
}

/** Gives up every upload in a plan, best effort. */
export function discardUpload(plan: UploadPlan): void {
  for (const part of plan.parts) {
    Upload.terminate(apiUrl(part.path), transportOptions()).catch(() => undefined)
  }
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
  let sent = alreadySent

  return {
    report(reported: number) {
      const now = performance.now()
      if (now - reportedAt < PROGRESS_INTERVAL_MS) {
        return
      }
      reportedAt = now
      const loaded = Math.max(sent, reported)
      sent = loaded

      rateWindow ??= { at: now, loaded }
      const elapsed = now - rateWindow.at
      if (elapsed >= SPEED_WINDOW_MS) {
        bytesPerSecond = ((loaded - rateWindow.loaded) * 1000) / elapsed
        rateWindow = { at: now, loaded }
      }

      onProgress?.({
        loaded,
        total,
        percent: total > 0 ? Math.min(100, Math.floor((loaded / total) * 100)) : 100,
        bytesPerSecond,
        secondsRemaining:
          bytesPerSecond !== null && bytesPerSecond > 0 ? (total - loaded) / bytesPerSecond : null,
      })
    },
    // The rate before a pause says nothing about the rate after it.
    restartRate() {
      rateWindow = null
      bytesPerSecond = null
    },
  }
}

// tus-js-client has no pause of its own, but it lets the transport be supplied. Holding requests
// pauses the upload without aborting any, which would leave the server holding locks.
function pausableHttpStack(onPaused: () => void) {
  const stack = new DefaultHttpStack({})
  const inFlight = new Set<Promise<HttpResponse>>()
  let held: Promise<void> | null = null
  let release = () => {}
  let stopped = false

  const isPaused = () => held !== null && !stopped
  const hold = () => {
    held ??= new Promise((resolve) => (release = resolve))
  }
  // Paused only once the requests already sent have finished.
  const reportIfPaused = () => {
    if (isPaused() && inFlight.size === 0) {
      onPaused()
    }
  }

  return {
    getName: () => 'PausableHttpStack',
    createRequest(method: string, url: string): HttpRequest {
      const request = stack.createRequest(method, url)
      const send = request.send.bind(request)
      request.send = async (body: unknown) => {
        while (held) {
          reportIfPaused()
          await held
        }
        const response = send(body)
        const forget = () => {
          inFlight.delete(response)
          reportIfPaused()
        }
        inFlight.add(response)
        response.then(forget, forget)
        return response
      }
      return request
    },
    isPaused,
    pause() {
      hold()
      reportIfPaused()
    },
    resume() {
      if (!stopped) {
        release()
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

  return status === 0 || status === 409 || status === 423 || status >= 500
}

function targetOptions(plan: UploadPlan): Partial<UploadOptions> {
  if (!plan.concatenated) {
    return { uploadUrl: apiUrl(plan.parts[0].path) }
  }

  return {
    parallelUploads: plan.parts.length,
    parallelUploadBoundaries: plan.parts.map((part) => ({
      start: part.start,
      end: part.start + part.length,
    })),
  }
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
  return new ApiError(error.message, response?.getStatus() ?? 0, response?.getBody())
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

function isLocked(error: unknown): boolean {
  return error instanceof ApiError && (error.status === 423 || error.status === 409)
}

function delay(ms: number, signal?: AbortSignal): Promise<void> {
  return new Promise((resolve, reject) => {
    const timer = setTimeout(resolve, ms)
    signal?.addEventListener(
      'abort',
      () => {
        clearTimeout(timer)
        reject(abortError())
      },
      { once: true },
    )
  })
}

function abortError(): DOMException {
  return new DOMException('Upload aborted', 'AbortError')
}
