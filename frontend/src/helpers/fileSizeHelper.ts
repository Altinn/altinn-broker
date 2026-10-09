const UNITS = ['B', 'kB', 'MB', 'GB', 'TB']
const BYTES_PER_GB = 1000 ** 3

/** Decimal units, matching how the API expresses its size limits. */
export function formatFileSize(bytes: number | undefined): string {
  if (!isFileSize(bytes)) {
    return '\u2013'
  }

  const { value, unit } = toLargestUnit(bytes)

  return `${value.toLocaleString('nb-NO', { maximumSignificantDigits: 3 })} ${UNITS[unit]}`
}

/** Decimal gigabytes, matching how the API expresses its size limits. */
export function bytesToGb(bytes: number): number {
  return bytes / BYTES_PER_GB
}

/** Converts decimal gigabytes to whole bytes for ConfigureResource. */
export function gbToBytes(gb: number): number {
  return Math.round(gb * BYTES_PER_GB)
}

function isFileSize(bytes: number | undefined): bytes is number {
  return bytes !== undefined && Number.isFinite(bytes) && bytes >= 0
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
