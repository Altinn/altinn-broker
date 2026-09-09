import type { AccessListMember } from '../../api/accessListMembers'
import { formatFileSize } from '../../helpers/fileSizeHelper'
import { formatOrgNumber, toOrgNumber } from '../../helpers/orgIdentifierHelper'

/** Limits enforced by the API */
export const MAX_METADATA_ENTRIES = 10
export const MAX_METADATA_KEY_LENGTH = 50
export const MAX_METADATA_VALUE_LENGTH = 3000
export const MAX_REFERENCE_LENGTH = 4096
const MAX_FILE_NAME_LENGTH = 255

export type MetadataEntry = {
  id: string
  key: string
  value: string
}

export type NewFileTransferValues = {
  reference: string
  recipients: string[]
  metadata: MetadataEntry[]
  file: File | null
  virusScan: boolean
}

export type NewFileTransferField = 'reference' | 'recipients' | 'metadata' | 'file'
export type NewFileTransferErrors = Partial<Record<NewFileTransferField, string>>

export const fieldLabels: Record<NewFileTransferField, string> = {
  reference: 'Referanse',
  recipients: 'Mottakere',
  metadata: 'Metadata',
  file: 'Fil',
}

/** Namespaced so the error summary can link to the control that failed. */
export function fieldId(field: NewFileTransferField): string {
  return `new-transfer-${field}`
}

export type RecipientRules = {
  /** The organizations the sender is allowed to pick, sender itself excluded. */
  options: AccessListMember[]
  /** Set when the resource forces this organization to be the one and only recipient. */
  requiredParty: AccessListMember | null
}

export function createMetadataEntry(): MetadataEntry {
  return { id: crypto.randomUUID(), key: '', value: '' }
}

export function emptyValues(): NewFileTransferValues {
  return {
    reference: '',
    recipients: [],
    metadata: [createMetadataEntry()],
    file: null,
    virusScan: true,
  }
}

/**
 * Narrows the access list to what the API will accept from this sender.
 * A resource with a required party only allows transfers that party is in, so a sender that is
 * not the required party can send to it and nobody else.
 */
export function resolveRecipientRules(
  members: AccessListMember[],
  requiredPartyIdentifier: string | null,
  senderOrgNumber: string,
): RecipientRules {
  const options = members.filter((member) => member.organizationNumber !== senderOrgNumber)
  const requiredPartyNumber = requiredPartyIdentifier ? toOrgNumber(requiredPartyIdentifier) : null

  if (!requiredPartyNumber || requiredPartyNumber === senderOrgNumber) {
    return { options, requiredParty: null }
  }

  const requiredParty = options.find(
    (member) => member.organizationNumber === requiredPartyNumber,
  ) ?? { organizationNumber: requiredPartyNumber, name: formatOrgNumber(requiredPartyNumber) }

  return { options: [requiredParty], requiredParty }
}

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

/** Filled entries only — a blank row is a row the user has not started on. */
export function toPropertyList(metadata: MetadataEntry[]): Record<string, string> {
  return Object.fromEntries(filledMetadata(metadata).map(({ key, value }) => [key.trim(), value]))
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

function validateMetadata(metadata: MetadataEntry[]): string | undefined {
  const filled = filledMetadata(metadata)

  if (filled.length > MAX_METADATA_ENTRIES) {
    return `Du kan legge til maks ${MAX_METADATA_ENTRIES} metadata.`
  }
  if (metadata.some((entry) => !entry.key.trim() && entry.value.trim())) {
    return 'Metadata med verdi må også ha en nøkkel.'
  }
  if (metadata.some((entry) => entry.key.trim() && !entry.value.trim())) {
    return 'Metadata med nøkkel må også ha en verdi.'
  }
  if (filled.some((entry) => entry.key.trim().length > MAX_METADATA_KEY_LENGTH)) {
    return `Nøkkel kan være maks ${MAX_METADATA_KEY_LENGTH} tegn.`
  }
  if (filled.some((entry) => entry.value.length > MAX_METADATA_VALUE_LENGTH)) {
    return `Verdi kan være maks ${MAX_METADATA_VALUE_LENGTH} tegn.`
  }

  const keys = filled.map((entry) => entry.key.trim())
  return new Set(keys).size === keys.length ? undefined : 'To metadata kan ikke ha samme nøkkel.'
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

function filledMetadata(metadata: MetadataEntry[]): MetadataEntry[] {
  return metadata.filter((entry) => entry.key.trim() || entry.value.trim())
}
