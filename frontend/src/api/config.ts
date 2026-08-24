/** Broker API prefix (APIM and local routes share this). */
export const BROKER_API_PREFIX = '/broker/api/v1'

export const AUTH_BASE_PATH = `${BROKER_API_PREFIX}/authentication`

/** Base URL for Broker API. Empty = same origin (prod / Vite proxy). */
export const API_BASE_URL = ((import.meta.env.VITE_API_BASE_URL as string | undefined) ?? '').replace(
  /\/$/,
  '',
)

export function apiUrl(path: string): string {
  const normalized = path.startsWith('/') ? path : `/${path}`
  return `${API_BASE_URL}${normalized}`
}
