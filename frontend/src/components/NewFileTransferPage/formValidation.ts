import { formatFileSize } from '../../helpers/fileSizeHelper'
import { i18n } from '../../i18n/config'
import { formatOrgNumber } from '../../helpers/orgIdentifierHelper'
import {
  toCustomRecipientIdentifier,
  type CustomRecipientEntry,
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
    checksum: values.checksumEnabled
      ? validateChecksum(values.checksum, values.checksumAlgorithm)
      : undefined,
    customRecipients: validateCustomRecipients(values),
  }
}

export function hasErrors(errors: NewFileTransferErrors): boolean {
  return Object.values(errors).some(Boolean)
}

function validateReference(reference: string): string | undefined {
  if (!reference.trim()) {
    return i18n.t('page.validation.reference_required')
  }
  if (reference.length > MAX_REFERENCE_LENGTH) {
    return i18n.t('page.validation.reference_max', { max: MAX_REFERENCE_LENGTH })
  }
  return undefined
}

function validateRecipients(recipients: string[], rules: RecipientRules): string | undefined {
  const { requiredParty, options } = rules

  if (requiredParty) {
    return recipients.length === 1 && recipients[0] === requiredParty.organizationNumber
      ? undefined
      : i18n.t('page.validation.recipient_only', { name: requiredParty.name })
  }

  if (recipients.length === 0) {
    return i18n.t('page.validation.recipient_min')
  }

  const outsideAccessList = recipients.find(
    (recipient) => !options.some((option) => option.organizationNumber === recipient),
  )
  return outsideAccessList
    ? i18n.t('page.validation.recipient_not_allowed', { org: formatOrgNumber(outsideAccessList) })
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
    return i18n.t('page.validation.metadata_key_required')
  }
  if (key.length > MAX_METADATA_KEY_LENGTH) {
    return i18n.t('page.validation.metadata_key_max', { max: MAX_METADATA_KEY_LENGTH })
  }
  if (duplicates.has(key)) {
    return i18n.t('page.validation.metadata_key_duplicate')
  }
  return undefined
}

function metadataValueError(value: string): string | undefined {
  if (!value.trim()) {
    return i18n.t('page.validation.metadata_value_required')
  }
  if (value.length > MAX_METADATA_VALUE_LENGTH) {
    return i18n.t('page.validation.metadata_value_max', { max: MAX_METADATA_VALUE_LENGTH })
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
    return i18n.t('page.validation.metadata_max', { max: MAX_METADATA_ENTRIES })
  }
  const failed = validateMetadataRows(metadata).find((row) => row.key ?? row.value)
  return failed?.key ?? failed?.value
}

export type CustomRecipientRowError = {
  identifier?: string
  relatedOrganization?: string
}

/** Rows only count while they are switched on and notifications are wanted at all. */
export function customRecipientsInUse(values: NewFileTransferValues): boolean {
  return values.notifications && values.customRecipientsEnabled
}

export function validateCustomRecipientRows(
  entries: CustomRecipientEntry[],
  recipients: string[],
): CustomRecipientRowError[] {
  return entries.map((entry) => ({
    identifier: customRecipientIdentifierError(entry.identifier),
    relatedOrganization: recipients.includes(entry.relatedOrganizationNumber)
      ? undefined
      : i18n.t('page.validation.custom_related'),
  }))
}

function customRecipientIdentifierError(identifier: string): string | undefined {
  if (!identifier.trim()) {
    return i18n.t('page.validation.custom_who')
  }
  if (!toCustomRecipientIdentifier(identifier)) {
    return i18n.t('page.validation.custom_contact')
  }
  return undefined
}

/** Summarises the rows for the error summary; the rows themselves carry the message the user acts on. */
function validateCustomRecipients(values: NewFileTransferValues): string | undefined {
  if (!customRecipientsInUse(values)) {
    return undefined
  }
  if (values.customRecipients.length === 0) {
    return i18n.t('page.validation.custom_min')
  }
  const failed = validateCustomRecipientRows(values.customRecipients, values.recipients).find(
    (row) => row.identifier ?? row.relatedOrganization,
  )
  return failed?.identifier ?? failed?.relatedOrganization
}

const CHECKSUM_LENGTH = { MD5: 32, SHA256: 64 } as const

function validateChecksum(checksum: string, algorithm: keyof typeof CHECKSUM_LENGTH): string | undefined {
  const value = checksum.trim()
  if (!value) {
    return undefined
  }

  const expectedLength = CHECKSUM_LENGTH[algorithm]
  if (value.length !== expectedLength) {
    return i18n.t('page.validation.checksum_length', { algorithm, length: expectedLength })
  }
  if (value.toLowerCase() !== value) {
    return i18n.t('page.validation.checksum_lowercase')
  }
  return undefined
}

function validateFile(file: File | null, maxFileSize: number | null): string | undefined {
  if (!file) {
    return i18n.t('page.validation.file_required')
  }
  if (file.size === 0) {
    return i18n.t('page.validation.file_empty')
  }
  if (file.name.length > MAX_FILE_NAME_LENGTH) {
    return i18n.t('page.validation.file_name_max', { max: MAX_FILE_NAME_LENGTH })
  }
  if (maxFileSize !== null && file.size > maxFileSize) {
    return i18n.t('page.validation.file_too_big', {
      size: formatFileSize(file.size),
      max: formatFileSize(maxFileSize),
    })
  }
  return undefined
}
