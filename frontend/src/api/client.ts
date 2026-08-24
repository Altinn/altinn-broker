import { AUTH_BASE_PATH, apiUrl } from './config'

export class ApiError extends Error {
  public readonly status: number
  public readonly body?: unknown

  constructor(message: string, status: number, body?: unknown) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.body = body
  }
}

export type ApiFetchOptions = RequestInit & {
  /** When true (default), 401 triggers redirect to ID-Porten login. */
  redirectOnUnauthorized?: boolean
}

/**
 * Authenticated fetch against the Broker API.
 * Always sends cookies (`credentials: "include"`).
 * State-changing requests get `X-Requested-With` for CSRF protection.
 */
export async function apiFetch<T = unknown>(
  path: string,
  options: ApiFetchOptions = {},
): Promise<T> {
  const { redirectOnUnauthorized = true, headers: initHeaders, ...rest } = options
  const method = (rest.method ?? 'GET').toUpperCase()
  const headers = new Headers(initHeaders)

  if (!['GET', 'HEAD', 'OPTIONS'].includes(method) && !headers.has('X-Requested-With')) {
    headers.set('X-Requested-With', 'XMLHttpRequest')
  }

  if (!headers.has('Accept')) {
    headers.set('Accept', 'application/json')
  }

  const response = await fetch(apiUrl(path), {
    ...rest,
    method,
    headers,
    credentials: 'include',
  })

  if (response.status === 401 && redirectOnUnauthorized) {
    redirectToLogin()
    throw new ApiError('Unauthorized', 401)
  }

  if (!response.ok) {
    let body: unknown
    try {
      body = await response.json()
    } catch {
      body = await response.text().catch(() => undefined)
    }
    throw new ApiError(`Request failed: ${response.status}`, response.status, body)
  }

  if (response.status === 204) {
    return undefined as T
  }

  const contentType = response.headers.get('Content-Type') ?? ''
  if (contentType.includes('application/json')) {
    return (await response.json()) as T
  }

  return (await response.text()) as T
}

export function redirectToLogin(returnUrl: string = window.location.pathname + window.location.search) {
  const params = new URLSearchParams({ returnUrl })
  window.location.assign(apiUrl(`${AUTH_BASE_PATH}/login?${params}`))
}

export function redirectToLogout(returnUrl: string = '/') {
  const params = new URLSearchParams({ returnUrl })
  window.location.assign(apiUrl(`${AUTH_BASE_PATH}/logout?${params}`))
}
