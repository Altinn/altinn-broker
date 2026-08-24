/** Base URL for Broker API. Empty = same origin (prod / Vite proxy). */
export const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL as string | undefined) ?? ''

export function apiUrl(path: string): string {
  const normalized = path.startsWith('/') ? path : `/${path}`
  return `${API_BASE_URL}${normalized}`
}
