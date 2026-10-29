// Session storage throws in private mode and when the browser blocks it. What is kept there is a
// convenience on top of the page, so losing it is never an error.

export function readSessionItem<T>(key: string): T | null {
  try {
    const raw = sessionStorage.getItem(key)
    return raw ? (JSON.parse(raw) as T) : null
  } catch {
    return null
  }
}

export function writeSessionItem(key: string, value: unknown): void {
  try {
    sessionStorage.setItem(key, JSON.stringify(value))
  } catch {
    // Not kept, which only costs the convenience.
  }
}

export function removeSessionItem(key: string): void {
  try {
    sessionStorage.removeItem(key)
  } catch {
    // Left behind, which only costs the convenience.
  }
}

export function clearSessionItems(prefix: string): void {
  try {
    for (const key of Object.keys(sessionStorage)) {
      if (key.startsWith(prefix)) {
        sessionStorage.removeItem(key)
      }
    }
  } catch {
    // Left behind, which only costs the convenience.
  }
}
