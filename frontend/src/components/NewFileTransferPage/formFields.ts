/**
 * Defines which fields exist, what an empty form looks like, and the shape of the values a field holds.
 */
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

export function createMetadataEntry(): MetadataEntry {
  return { id: crypto.randomUUID(), key: '', value: '' }
}

export function emptyValues(): NewFileTransferValues {
  return {
    reference: '',
    recipients: [],
    metadata: [],
    file: null,
    virusScan: true,
  }
}

/** The metadata field as the API takes it: a dictionary of the entered pairs. */
export function toPropertyList(metadata: MetadataEntry[]): Record<string, string> {
  return Object.fromEntries(metadata.map(({ key, value }) => [key.trim(), value]))
}
