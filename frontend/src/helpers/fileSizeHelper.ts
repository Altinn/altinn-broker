const UNITS = ['B', 'kB', 'MB', 'GB', 'TB']

/** Decimal units, matching how the API expresses its size limits. */
export function formatFileSize(bytes: number): string {
  if (!Number.isFinite(bytes) || bytes < 0) {
    return '–'
  }

  let value = bytes
  let unit = 0
  while (value >= 1000 && unit < UNITS.length - 1) {
    value /= 1000
    unit += 1
  }

  const decimals = unit === 0 ? 0 : value >= 100 ? 0 : value >= 10 ? 1 : 2
  return `${value.toLocaleString('nb-NO', { maximumFractionDigits: decimals })} ${UNITS[unit]}`
}
