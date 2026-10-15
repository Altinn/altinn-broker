import type { CustomRecipientInput, NotificationChannel, NotificationInput } from '../../api/initializeFileTransfer'

/**
 * Defines which fields exist, what an empty form looks like, and the shape of the values a field holds.
 */
export type MetadataEntry = {
  id: string
  key: string
  value: string
}

/**
 * Someone to notify beyond the file transfer's own recipients: an email address or mobile number, notified on behalf
 * of one of the file transfer's recipients.
 */
export type CustomRecipientEntry = {
  id: string
  identifier: string
  relatedOrganizationNumber: string
}

export type { NotificationChannel }

export type NewFileTransferValues = {
  reference: string
  recipients: string[]
  metadata: MetadataEntry[]
  file: File | null
  virusScan: boolean
  notifications: boolean
  sendReminder: boolean
  customRecipientsEnabled: boolean
  customRecipients: CustomRecipientEntry[]
}

export type NewFileTransferField =
  | 'reference'
  | 'recipients'
  | 'metadata'
  | 'file'
  | 'notifications'
  | 'customRecipients'

export type NewFileTransferErrors = Partial<Record<NewFileTransferField, string>>

export const fieldLabels: Record<NewFileTransferField, string> = {
  reference: 'Referanse',
  recipients: 'Mottakere',
  metadata: 'Metadata',
  file: 'Fil',
  notifications: 'Varslinger',
  customRecipients: 'Andre som skal varsles',
}

/** Namespaced so the error summary can link to the control that failed. */
export function fieldId(field: NewFileTransferField): string {
  return `new-transfer-${field}`
}

/** Metadata rows come and go, so the summary links to one of their inputs rather than the fieldset. */
export function metadataInputId(entryId: string, part: 'key' | 'value'): string {
  return `${fieldId('metadata')}-${part}-${entryId}`
}

export function createMetadataEntry(): MetadataEntry {
  return { id: crypto.randomUUID(), key: '', value: '' }
}

export function customRecipientInputId(entryId: string, part: 'identifier' | 'relatedOrganization'): string {
  return `${fieldId('customRecipients')}-${part}-${entryId}`
}

export function createCustomRecipientEntry(relatedOrganizationNumber = ''): CustomRecipientEntry {
  return { id: crypto.randomUUID(), identifier: '', relatedOrganizationNumber }
}

export function emptyValues(): NewFileTransferValues {
  return {
    reference: '',
    recipients: [],
    metadata: [],
    file: null,
    virusScan: true,
    notifications: false,
    sendReminder: false,
    customRecipientsEnabled: false,
    customRecipients: [],
  }
}

export type CustomRecipientIdentifier =
  | { emailAddress: string }
  | { mobileNumber: string }
  // Custom recipient organization number - disabled for now.
  // | { organizationNumber: string }

/**
 * The API's email check (EmailRegex in the backend's NotificationValidationHelper), so an address the API would
 * reject is caught in the frontend.
 */
const EMAIL_PATTERN =
  /^(("[^"]+")|(([a-zA-Z0-9æøåÆØÅ!#$%&'*+\-=?^_`{|}~])+(\.([a-zA-Z0-9æøåÆØÅ!#$%&'*+\-=?^_`{|}~])+)*))@(((([a-zA-Z0-9æøåÆØÅ]([a-zA-Z0-9\-æøåÆØÅ]{0,61})[a-zA-Z0-9æøåÆØÅ]\.)|[a-zA-Z0-9æøåÆØÅ]\.){1,9})([a-zA-Z]{2,14})|((\d{1,3})\.(\d{1,3})\.(\d{1,3})\.(\d{1,3})))$/

/**
 * Tells from its format which kind of identifier was typed, since the field takes either. Returns null for anything
 * that is neither. Only the format is checked - the API validates the rest.
 */
export function toCustomRecipientIdentifier(input: string): CustomRecipientIdentifier | null {
  const value = input.trim()
  if (EMAIL_PATTERN.test(value) && !value.includes(';')) {
    return { emailAddress: value }
  }

  const compact = value.replace(/\s/g, '')
  // E.164: a country code never starts with 0, and the number is at most 15 digits.
  if (/^(\+|00)[1-9]\d{1,14}$/.test(compact)) {
    // The API takes both, but storing one form means the same number is always the same recipient.
    return { mobileNumber: compact.startsWith('00') ? `+${compact.slice(2)}` : compact }
  }
  if (/^[49]\d{7}$/.test(compact)) {
    // A Norwegian mobile number typed without country code - the API requires one.
    return { mobileNumber: `+47${compact}` }
  }

  // Custom recipient organization number - disabled for now (needs toOrgNumber from orgIdentifierHelper).
  // const organizationNumber = toOrgNumber(compact)
  // if (organizationNumber) {
  //   return { organizationNumber }
  // }

  return null
}

/** The notification as the API takes it, or undefined when none is wanted. */
export function toNotificationInput(values: NewFileTransferValues): NotificationInput | undefined {
  if (!values.notifications) {
    return undefined
  }

  const customRecipients = values.customRecipientsEnabled
    ? values.customRecipients.flatMap((entry): CustomRecipientInput[] => {
        const identifier = toCustomRecipientIdentifier(entry.identifier)
        return identifier ? [{ ...identifier, relatedOrganizationNumber: entry.relatedOrganizationNumber }] : []
      })
    : []

  return {
    sendReminder: values.sendReminder,
    customRecipients: customRecipients.length > 0 ? customRecipients : undefined,
  }
}

/** The metadata field as the API takes it: a dictionary of the entered pairs. */
export function toPropertyList(metadata: MetadataEntry[]): Record<string, string> {
  return Object.fromEntries(metadata.map(({ key, value }) => [key.trim(), value]))
}
