const NORWEGIAN_ORG_CODE = '0192'

const IDENTIFIER_PREFIXES = ['urn:altinn:organization:identifier-no:', `${NORWEGIAN_ORG_CODE}:`]

/**
 * Extracts the bare 9-digit organization number from any form the API accepts
 * Returns null for anything else.
 */
export function toOrgNumber(value: string): string | null {
  let rest = value.trim()
  const prefix = IDENTIFIER_PREFIXES.find((candidate) => rest.toLowerCase().startsWith(candidate))
  if (prefix) {
    rest = rest.slice(prefix.length)
  }

  const digits = rest.replace(/\s/g, '')
  return /^\d{9}$/.test(digits) ? digits : null
}

/**
 * Ensures that the given organization number is valid and returns it in the API-accepted identifier format.
 * Throws an `InvalidOrgNumberError` if the input is not a valid organization number.
 */
export function requireOrgIdentifier(orgNumber: string): string {
  const identifier = toOrgIdentifier(orgNumber)
  if (!identifier) {
    throw new InvalidOrgNumberError(orgNumber)
  }
  return identifier
}

/**
 * Turns an organization number into the identifier the API validates against.
 * Returns null for anything that is not a valid organization number.
 */
export function toOrgIdentifier(orgNumber: string): string | null {
  const digits = toOrgNumber(orgNumber)
  return digits ? `${NORWEGIAN_ORG_CODE}:${digits}` : null
}

export function formatOrgNumber(orgNumber: string): string {
  const digits = toOrgNumber(orgNumber)
  return digits ? digits.replace(/(\d{3})(\d{3})(\d{3})/, '$1 $2 $3') : orgNumber
}

export class InvalidOrgNumberError extends Error {
  public readonly value: string

  constructor(value: string) {
    super(`Invalid organization number: ${value}`)
    this.name = 'InvalidOrgNumberError'
    this.value = value
  }
}

