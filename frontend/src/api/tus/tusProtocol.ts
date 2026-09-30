import { ApiError, redirectToLoginIfSessionEnded } from '../client'
import { BROKER_API_PREFIX, apiUrl } from '../config'

const TUS_PATH = `${BROKER_API_PREFIX}/filetransfer/upload/tus`
const TUS_VERSION = '1.0.0'

// The TUS creation requests.

export function tusUploadPath(fileTransferId: string): string {
  return `${TUS_PATH}/${fileTransferId}`
}

export async function createUpload(
  fileTransferId: string,
  length: number,
  signal?: AbortSignal,
): Promise<string> {
  const path = tusUploadPath(fileTransferId)
  const response = await tusRequest(path, { 'Upload-Length': String(length) }, signal)

  await assertCreated(response)
  return locationPath(response, path)
}

/** Must be called front to back: creation order decides each partial's place in the file. */
export async function createPartialUpload(
  fileTransferId: string,
  length: number,
  signal?: AbortSignal,
): Promise<string> {
  const response = await tusRequest(
    tusUploadPath(fileTransferId),
    { 'Upload-Length': String(length), 'Upload-Concat': 'partial' },
    signal,
  )

  await assertCreated(response)
  return locationPath(response)
}

/** Null when the upload is gone: expired, already assembled, or never created. */
export async function getUploadInfo(
  path: string,
  signal?: AbortSignal,
): Promise<{ offset: number; length: number } | null> {
  const response = await tusRequest(path, {}, signal, 'HEAD')

  if (response.status === 404 || response.status === 410) {
    return null
  }
  if (response.status !== 200) {
    throw new ApiError(`${response.status} on ${response.url}`, response.status)
  }

  const offset = Number(response.headers.get('Upload-Offset'))
  const length = Number(response.headers.get('Upload-Length'))
  if (!Number.isFinite(offset) || !Number.isFinite(length)) {
    throw new ApiError('The upload did not report an offset', response.status)
  }

  return { offset, length }
}

async function tusRequest(
  path: string,
  headers: Record<string, string>,
  signal?: AbortSignal,
  method: 'POST' | 'HEAD' = 'POST',
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

  const body = await response.text().catch(() => undefined)
  throw new ApiError(`${response.status} on ${response.url}`, response.status, body)
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
