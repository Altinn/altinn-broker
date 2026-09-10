import { ApiError, redirectToLogin } from './client'
import { apiUrl } from './config'

export type UploadProgress = {
  loaded: number
  total: number
  percent: number
}

export type UploadOptions = {
  onProgress?: (progress: UploadProgress) => void
  signal?: AbortSignal
}

/**
 * POSTs a binary body and reports how much of it has been sent.
 *
 * XMLHttpRequest is the only transport that can report upload progress. Credentials, the CSRF marker and the 401 handling mirror
 * `apiFetch`, so both transports call the API the same way.
 *
 * Resolves with the parsed JSON body, or null when the response has no JSON body.
 */
export function uploadBinary<T>(
  path: string,
  body: Blob,
  options: UploadOptions = {},
): Promise<T | null> {
  const { onProgress, signal } = options

  return new Promise<T | null>((resolve, reject) => {
    if (signal?.aborted) {
      reject(abortError())
      return
    }

    const xhr = new XMLHttpRequest()
    const abort = () => xhr.abort()

    xhr.open('POST', apiUrl(path))
    xhr.withCredentials = true
    xhr.setRequestHeader('Content-Type', 'application/octet-stream')
    xhr.setRequestHeader('X-Requested-With', 'XMLHttpRequest')
    xhr.setRequestHeader('Accept', 'application/json')

    xhr.upload.onprogress = (event) => {
      if (event.lengthComputable) {
        onProgress?.(toProgress(event))
      }
    }

    // onload fires for every completed response, 4xx and 5xx included, so the status is checked
    // here. onerror only covers a request that never got a response at all.
    xhr.onload = () => {
      const parsed = parseJsonBody(xhr.responseText)
      const failure = toFailure(xhr.status, parsed)
      if (failure) {
        reject(failure)
        return
      }
      resolve(parsed as T | null)
    }

    xhr.onerror = () => reject(new ApiError('Upload failed: network error', 0))
    xhr.onabort = () => reject(abortError())
    xhr.onloadend = () => signal?.removeEventListener('abort', abort)

    signal?.addEventListener('abort', abort, { once: true })
    xhr.send(body)
  })
}

function toProgress(event: ProgressEvent): UploadProgress {
  return {
    loaded: event.loaded,
    total: event.total,
    percent: Math.round((event.loaded / event.total) * 100),
  }
}

function parseJsonBody(raw: string): unknown {
  try {
    return JSON.parse(raw)
  } catch {
    return null
  }
}

function toFailure(status: number, body: unknown): Error | null {
  if (status === 401) {
    redirectToLogin()
    return new ApiError('Unauthorized', 401)
  }
  if (status < 200 || status >= 300) {
    return new ApiError(`Upload failed: ${status}`, status, body)
  }
  return null
}

function abortError(): DOMException {
  return new DOMException('Upload aborted', 'AbortError')
}
