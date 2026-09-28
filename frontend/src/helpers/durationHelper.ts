const UNITS = [
  { seconds: 86400, singular: 'dag', plural: 'dager' },
  { seconds: 3600, singular: 'time', plural: 'timer' },
  { seconds: 60, singular: 'minutt', plural: 'minutter' },
  { seconds: 1, singular: 'sekund', plural: 'sekunder' },
]

const SECONDS_PER_DAY = 86400
const SECONDS_PER_HOUR = 3600

/** .NET TimeSpan format: "30.00:00:00" is 30 days, "02:00:00" is 2 hours. */
const TIME_SPAN = /^(?:(\d+)\.)?(\d{1,2}):(\d{2}):(\d{2})(?:\.\d+)?$/

/**
 * Formats a duration in the format the API returns as Norwegian text, using the largest
 * unit it divides into evenly. Returns a dash for anything it cannot read.
 */
export function formatDuration(timeSpan: string): string {
  const seconds = toSeconds(timeSpan)
  if (seconds === null) {
    return '–'
  }

  const unit = UNITS.find((candidate) => seconds >= candidate.seconds && seconds % candidate.seconds === 0)
  return unit ? countOf(seconds, unit) : '0 sekunder'
}

/** Whole days covered by a .NET TimeSpan, or null when the value cannot be read. */
export function timeSpanToDays(timeSpan: string): number | null {
  const seconds = toSeconds(timeSpan)
  return seconds === null ? null : seconds / SECONDS_PER_DAY
}

/** Whole hours covered by a .NET TimeSpan, or null when the value cannot be read. */
export function timeSpanToHours(timeSpan: string): number | null {
  const seconds = toSeconds(timeSpan)
  return seconds === null ? null : seconds / SECONDS_PER_HOUR
}

/** ISO-8601 duration for ConfigureResource (e.g. `P30D`). */
export function daysToIso8601(days: number): string {
  return `P${days}D`
}

/** ISO-8601 duration for ConfigureResource (e.g. `PT2H`). */
export function hoursToIso8601(hours: number): string {
  return `PT${hours}H`
}

function toSeconds(timeSpan: string): number | null {
  const parts = TIME_SPAN.exec(timeSpan)
  if (!parts) {
    return null
  }

  const [, days = '0', hours, minutes, seconds] = parts
  return Number(days) * SECONDS_PER_DAY + Number(hours) * SECONDS_PER_HOUR + Number(minutes) * 60 + Number(seconds)
}

function countOf(seconds: number, unit: (typeof UNITS)[number]): string {
  const count = seconds / unit.seconds
  return `${count} ${count === 1 ? unit.singular : unit.plural}`
}
