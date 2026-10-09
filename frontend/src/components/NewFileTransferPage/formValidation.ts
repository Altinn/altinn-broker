import { formatFileSize } from '../../helpers/fileSizeHelper'
import { formatOrgNumber } from '../../helpers/orgIdentifierHelper'
import {
  type MetadataEntry,
  type NewFileTransferErrors,
  type NewFileTransferValues,
} from './formFields'
import type { RecipientRules } from './recipientRules'

/** Limits enforced by the API */
export const MAX_METADATA_ENTRIES = 10
export const MAX_METADATA_KEY_LENGTH = 50
export const MAX_METADATA_VALUE_LENGTH = 3000
export const MAX_REFERENCE_LENGTH = 4096
const MAX_FILE_NAME_LENGTH = 255

/** Validates the entire new file transfer form and returns an object mapping field names to error messages. */
export function validate(
  values: NewFileTransferValues,
  maxFileSize: number | null,
  rules: RecipientRules,
): NewFileTransferErrors {
  return {
    reference: validateReference(values.reference),
    recipients: validateRecipients(values.recipients, rules),
    metadata: validateMetadata(values.metadata),
    file: validateFile(values.file, maxFileSize),
  }
}

export function hasErrors(errors: NewFileTransferErrors): boolean {
  return Object.values(errors).some(Boolean)
}

function validateReference(reference: string): string | undefined {
  if (!reference.trim()) {
    return 'Referanse må fylles ut.'
  }
  if (reference.length > MAX_REFERENCE_LENGTH) {
    return `Referanse kan være maks ${MAX_REFERENCE_LENGTH} tegn.`
  }
  return undefined
}

function validateRecipients(recipients: string[], rules: RecipientRules): string | undefined {
  const { requiredParty, options } = rules

  if (requiredParty) {
    return recipients.length === 1 && recipients[0] === requiredParty.organizationNumber
      ? undefined
      : `Tjenesten krever at ${requiredParty.name} er eneste mottaker.`
  }

  if (recipients.length === 0) {
    return 'Velg minst én mottaker.'
  }

  const outsideAccessList = recipients.find(
    (recipient) => !options.some((option) => option.organizationNumber === recipient),
  )
  return outsideAccessList
    ? `${formatOrgNumber(outsideAccessList)} står ikke i tilgangslisten for tjenesten.`
    : undefined
}

export type MetadataRowError = {
  key?: string
  value?: string
}

export function validateMetadataRows(metadata: MetadataEntry[]): MetadataRowError[] {
  const duplicates = duplicateKeys(metadata)
  return metadata.map((entry) => ({
    key: metadataKeyError(entry.key, duplicates),
    value: metadataValueError(entry.value),
  }))
}

function metadataKeyError(rawKey: string, duplicates: Set<string>): string | undefined {
  const key = rawKey.trim()
  if (!key) {
    return 'Nøkkel må fylles ut.'
  }
  if (key.length > MAX_METADATA_KEY_LENGTH) {
    return `Nøkkel kan være maks ${MAX_METADATA_KEY_LENGTH} tegn.`
  }
  if (duplicates.has(key)) {
    return 'To metadata kan ikke ha samme nøkkel.'
  }
  return undefined
}

function metadataValueError(value: string): string | undefined {
  if (!value.trim()) {
    return 'Verdi må fylles ut.'
  }
  if (value.length > MAX_METADATA_VALUE_LENGTH) {
    return `Verdi kan være maks ${MAX_METADATA_VALUE_LENGTH} tegn.`
  }
  return undefined
}

function duplicateKeys(metadata: MetadataEntry[]): Set<string> {
  const seen = new Set<string>()
  const duplicates = new Set<string>()
  for (const { key } of metadata) {
    const trimmed = key.trim()
    if (!trimmed) {
      continue
    }
    if (seen.has(trimmed)) {
      duplicates.add(trimmed)
    }
    seen.add(trimmed)
  }
  return duplicates
}

/** Summarises the rows for the error summary; the rows themselves carry the message the user acts on. */
function validateMetadata(metadata: MetadataEntry[]): string | undefined {
  if (metadata.length > MAX_METADATA_ENTRIES) {
    return `Du kan legge til maks ${MAX_METADATA_ENTRIES} metadata.`
  }
  const failed = validateMetadataRows(metadata).find((row) => row.key ?? row.value)
  return failed?.key ?? failed?.value
}

function validateFile(file: File | null, maxFileSize: number | null): string | undefined {
  if (!file) {
    return 'Velg en fil.'
  }
  if (file.size === 0) {
    return 'Filen er tom.'
  }
  if (file.name.length > MAX_FILE_NAME_LENGTH) {
    return `Filnavnet kan være maks ${MAX_FILE_NAME_LENGTH} tegn.`
  }
  if (maxFileSize !== null && file.size > maxFileSize) {
    return `Filen er ${formatFileSize(file.size)}. Tjenesten tillater maks ${formatFileSize(maxFileSize)}.`
  }
  return undefined
}
