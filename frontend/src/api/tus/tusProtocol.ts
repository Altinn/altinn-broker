import { ApiError, redirectToLoginIfSessionEnded } from '../client'
import { BROKER_API_PREFIX, apiUrl } from '../config'

const TUS_PATH = `${BROKER_API_PREFIX}/filetransfer/upload/tus`
const TUS_VERSION = '1.0.0'

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
): Promise<void> {
  const parts = partPaths.map((path) => apiUrl(path)).join(' ')
  const response = await post(
    tusUploadPath(fileTransferId),
    { 'Upload-Concat': `final;${parts}` },
    signal,
  )

  await assertCreated(response)
}

/** Null when the upload is gone: expired, already assembled, never created, or no longer taken. */
export async function getUploadInfo(
  path: string,
  signal?: AbortSignal,
): Promise<{ offset: number; length: number } | null> {
  const response = await tusRequest('HEAD', path, {}, signal)

  // A HEAD meets a lock as 423, so a 409 is Broker refusing uploads to a transfer done with them.
  if (response.status === 404 || response.status === 409 || response.status === 410) {
    return null
  }
  if (response.status !== 200) {
    throw new ApiError(`${response.status} on ${response.url}`, response.status)
  }

  const offset = Number(response.headers.get('Upload-Offset') ?? Number.NaN)
  const length = Number(response.headers.get('Upload-Length') ?? Number.NaN)
  if (!Number.isFinite(offset) || !Number.isFinite(length)) {
    throw new ApiError('The upload did not report an offset', response.status)
  }

  return { offset, length }
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

async function post(
  path: string,
  headers: Record<string, string>,
  signal?: AbortSignal,
): Promise<Response> {
  for (let attempt = 0; ; attempt++) {
    const response = await tusRequest('POST', path, headers, signal)
    const passing = response.status === 423 || response.status >= 500
    if (!passing || attempt >= RETRY_DELAYS.length) {
      return response
    }
    await delay(RETRY_DELAYS[attempt], signal)
  }
}

async function tusRequest(
  method: 'POST' | 'HEAD',
  path: string,
  headers: Record<string, string>,
  signal?: AbortSignal,
): Promise<Response> {
  const response = await fetch(apiUrl(path), {
    method,
    headers: { 'Tus-Resumable': TUS_VERSION, 'X-Requested-With': 'XMLHttpRequest', ...headers },
    credentials: 'include',
    signal,
  })

  if (response.status === 401) {
    await redirectToLoginIfSessionEnded()
    throw new ApiError('Unauthorized', 401)
  }

  return response
}

async function assertCreated(response: Response): Promise<void> {
  if (response.status === 201) {
    return
  }

  throw new ApiError(`${response.status} on ${response.url}`, response.status, await readBody(response))
}

// Problem details come as JSON, so their detail can be shown.
async function readBody(response: Response): Promise<unknown> {
  const text = await response.text().catch(() => '')
  try {
    return JSON.parse(text)
  } catch {
    return text
  }
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
