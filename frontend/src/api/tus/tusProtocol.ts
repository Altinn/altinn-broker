import { ApiError, apiRequest, readBody } from '../client'
import { BROKER_API_PREFIX, apiUrl } from '../config'

const TUS_PATH = `${BROKER_API_PREFIX}/filetransfer/upload/tus`
const TUS_HEADERS = { 'Tus-Resumable': '1.0.0' }

// Retry to wait out a lock, or a server error that passes.
export const RETRY_DELAYS = [1000, 2000, 4000, 8000, 15000, 15000, 15000]

// The TUS requests made outside tus-js-client: creating the uploads, joining the parts, and asking
// how far an upload got.

export function tusUploadPath(fileTransferId: string): string {
  return `${TUS_PATH}/${fileTransferId}`
}

export async function createUpload(
  fileTransferId: string,
  length: number,
  signal?: AbortSignal,
): Promise<string> {
  const path = tusUploadPath(fileTransferId)
  const response = await post(path, { 'Upload-Length': String(length) }, signal)

  await assertCreated(response)
  return locationPath(response, path)
}

/** Must be called front to back: creation order decides each partial's place in the file. */
export async function createPartialUpload(
  fileTransferId: string,
  length: number,
  signal?: AbortSignal,
): Promise<string> {
  const response = await post(
    tusUploadPath(fileTransferId),
    { 'Upload-Length': String(length), 'Upload-Concat': 'partial' },
    signal,
  )

  await assertCreated(response)
  return locationPath(response)
}

/** Joins the uploaded partials, in the order given, into the transfer's file. */
export async function concatenateUploads(
  fileTransferId: string,
  partPaths: string[],
  signal?: AbortSignal,
  onAttempt?: () => void,
): Promise<void> {
  const parts = partPaths.map((path) => apiUrl(path)).join(' ')
  const response = await post(
    tusUploadPath(fileTransferId),
    { 'Upload-Concat': `final;${parts}` },
    signal,
    onAttempt,
  )

  await assertCreated(response)
}

/**
 * How many bytes the server holds of the upload, or null when it is gone: expired, already
 * assembled, never created, or no longer taken.
 */
export async function getUploadOffset(path: string, signal?: AbortSignal): Promise<number | null> {
  const response = await apiRequest(path, { method: 'HEAD', headers: TUS_HEADERS, signal })

  // A HEAD meets a lock as 423, so a 409 is Broker refusing uploads to a transfer done with them.
  if (isGone(response.status)) {
    return null
  }
  if (response.status !== 200) {
    throw new ApiError(`${response.status} on ${response.url}`, response.status)
  }

  const offset = Number(response.headers.get('Upload-Offset') ?? Number.NaN)
  if (!Number.isFinite(offset)) {
    throw new ApiError('The upload did not report an offset', response.status)
  }

  return offset
}

export function delay(ms: number, signal?: AbortSignal): Promise<void> {
  return new Promise((resolve, reject) => {
    const timer = setTimeout(resolve, ms)
    signal?.addEventListener(
      'abort',
      () => {
        clearTimeout(timer)
        reject(signal.reason)
      },
      { once: true },
    )
  })
}

/** The upload is gone, or its transfer no longer takes uploads. */
export function isGone(status: number): boolean {
  return status === 404 || status === 409 || status === 410
}

/** A lock, or a server error that passes, either of which is worth another try. */
export function isTemporary(status: number): boolean {
  return status === 423 || status >= 500
}

async function post(
  path: string,
  headers: Record<string, string>,
  signal?: AbortSignal,
  onAttempt?: () => void,
): Promise<Response> {
  for (let attempt = 0; ; attempt++) {
    onAttempt?.()
    const response = await apiRequest(path, {
      method: 'POST',
      headers: { ...TUS_HEADERS, ...headers },
      signal,
    })
    if (!isTemporary(response.status) || attempt >= RETRY_DELAYS.length) {
      return response
    }
    await delay(RETRY_DELAYS[attempt], signal)
  }
}

async function assertCreated(response: Response): Promise<void> {
  if (response.status === 201) {
    return
  }

  throw new ApiError(`${response.status} on ${response.url}`, response.status, await readBody(response))
}

function locationPath(response: Response, fallback?: string): string {
  const location = response.headers.get('Location')
  if (!location) {
    if (fallback) {
      return fallback
    }
    throw new ApiError('The created upload has no location', response.status)
  }

  return location.startsWith('http') ? new URL(location).pathname : location
}
