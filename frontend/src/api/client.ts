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
    await redirectToLoginIfSessionEnded()
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

/**
 * A 401 from a Broker endpoint does not by itself mean the session is over: a failing downstream
 * dependency (Altinn Authorization, token exchange) surfaces the same status. Only /me can tell the
 * difference, so ask it before throwing the user out to ID-Porten for no reason.
 */
export async function redirectToLoginIfSessionEnded(returnUrl?: string): Promise<void> {
  if (await sessionIsGone()) {
    redirectToLogin(returnUrl)
  }
}

async function sessionIsGone(): Promise<boolean> {
  try {
    const response = await fetch(apiUrl(`${AUTH_BASE_PATH}/me`), {
      credentials: 'include',
      headers: { Accept: 'application/json' },
    })

    if (response.status === 401) {
      return true
    }

    if (!response.ok || !(response.headers.get('Content-Type') ?? '').includes('application/json')) {
      return false
    }

    const me = (await response.json()) as { authenticated?: boolean }
    return me.authenticated === false
  } catch {
    // A network failure says nothing about the session — leave the user where they are.
    return false
  }
}

/** Parallel 401s must not each start their own navigation. */
let loginRedirectStarted = false

export function redirectToLogin(returnUrl: string = window.location.pathname + window.location.search) {
  const loginPath = `${AUTH_BASE_PATH}/login`
  // Avoid nested returnUrl when /broker/... is wrongly served as the SPA (Front Door → storage).
  if (window.location.pathname === loginPath || loginRedirectStarted) {
    return
  }

  loginRedirectStarted = true

  const safeReturnUrl =
    !returnUrl || returnUrl.startsWith(AUTH_BASE_PATH) ? '/' : returnUrl
  const params = new URLSearchParams({ returnUrl: safeReturnUrl })
  window.location.assign(apiUrl(`${loginPath}?${params}`))
}

export function redirectToLogout(returnUrl: string = '/') {
  const params = new URLSearchParams({ returnUrl })
  window.location.assign(apiUrl(`${AUTH_BASE_PATH}/logout?${params}`))
}
