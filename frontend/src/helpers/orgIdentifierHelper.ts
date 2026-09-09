const NORWEGIAN_ORG_CODE = '0192'

const IDENTIFIER_PREFIXES = ['urn:altinn:organization:identifier-no:', `${NORWEGIAN_ORG_CODE}:`]

/**
 * Extracts the bare 9-digit organization number from any form the API accepts:
 * "0192:991825827", "urn:altinn:organization:identifier-no:991825827" or "991 825 827".
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
 * Turns an organization number into the identifier the API validates against.
 * Returns null for anything that is not a valid organization number.
 */
export function toOrgIdentifier(orgNumber: string): string | null {
  const digits = toOrgNumber(orgNumber)
  return digits ? `${NORWEGIAN_ORG_CODE}:${digits}` : null
}

/** "991825827" -> "991 825 827" */
export function formatOrgNumber(orgNumber: string): string {
  const digits = toOrgNumber(orgNumber)
  return digits ? digits.replace(/(\d{3})(\d{3})(\d{3})/, '$1 $2 $3') : orgNumber
}
