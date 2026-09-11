const UNITS = ['B', 'kB', 'MB', 'GB', 'TB']

/** Decimal units, matching how the API expresses its size limits. */
export function formatFileSize(bytes: number): string {
  if (!isFileSize(bytes)) {
    return '\u2013'
  }

  const { value, unit } = toLargestUnit(bytes)
  
  return `${value.toLocaleString('nb-NO', { maximumSignificantDigits: 3 })} ${UNITS[unit]}`
}

function isFileSize(bytes: number): boolean {
  return Number.isFinite(bytes) && bytes >= 0
}

/** Scales down until the value fits the unit, or the largest unit is reached. */
function toLargestUnit(bytes: number): { value: number; unit: number } {
  let value = bytes
  let unit = 0

  while (value >= 1000 && unit < UNITS.length - 1) {
    value /= 1000
    unit += 1
  }

  return { value, unit }
}
