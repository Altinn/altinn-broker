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

/** Days covered by a .NET TimeSpan (may be fractional), or null when unreadable. */
export function timeSpanToDays(timeSpan: string): number | null {
  const seconds = toSeconds(timeSpan)
  return seconds === null ? null : seconds / SECONDS_PER_DAY
}

/** Hours covered by a .NET TimeSpan (may be fractional), or null when unreadable. */
export function timeSpanToHours(timeSpan: string): number | null {
  const seconds = toSeconds(timeSpan)
  return seconds === null ? null : seconds / SECONDS_PER_HOUR
}

/**
 * ISO-8601 duration for ConfigureResource (e.g. `P30D`, `P1DT12H`).
 * Fractional days are expanded to day/hour/minute/second components so values
 * like 1.5 round-trip as the same duration the API accepts (P1DT12H).
 */
export function daysToIso8601(days: number): string {
  return secondsToIso8601(Math.round(days * SECONDS_PER_DAY))
}

/**
 * ISO-8601 duration for ConfigureResource (e.g. `PT2H`, `PT90M`).
 * Fractional hours are expanded to hour/minute/second components.
 */
export function hoursToIso8601(hours: number): string {
  return secondsToIso8601(Math.round(hours * SECONDS_PER_HOUR))
}

function secondsToIso8601(totalSeconds: number): string {
  if (totalSeconds <= 0) {
    return 'PT0S'
  }

  const days = Math.floor(totalSeconds / SECONDS_PER_DAY)
  let remainder = totalSeconds % SECONDS_PER_DAY
  const hours = Math.floor(remainder / SECONDS_PER_HOUR)
  remainder %= SECONDS_PER_HOUR
  const minutes = Math.floor(remainder / 60)
  const seconds = remainder % 60

  let result = 'P'
  if (days > 0) {
    result += `${days}D`
  }
  if (hours > 0 || minutes > 0 || seconds > 0 || days === 0) {
    result += 'T'
    if (hours > 0) {
      result += `${hours}H`
    }
    if (minutes > 0) {
      result += `${minutes}M`
    }
    if (seconds > 0 || (hours === 0 && minutes === 0)) {
      result += `${seconds}S`
    }
  }
  return result
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

/**
 * A countdown for something still running, rounded to the largest unit that still tells the
 * reader how long they are waiting. Anything under a minute is not worth a number.
 */
export function formatRemainingTime(seconds: number): string {
  if (!Number.isFinite(seconds) || seconds < 60) {
    return 'under ett minutt'
  }

  const roughUnit = unitFor(seconds)
  const rounded = Math.round(seconds / roughUnit.seconds) * roughUnit.seconds
  return countOf(rounded, unitFor(rounded))
}

function unitFor(seconds: number): (typeof UNITS)[number] {
  return UNITS.find((candidate) => seconds >= candidate.seconds && candidate.seconds >= 60) ?? UNITS[2]
}
