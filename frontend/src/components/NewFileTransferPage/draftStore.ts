import { readSessionItem, removeSessionItem, writeSessionItem } from '../../helpers/sessionStorageHelper'
import type { NewFileTransferValues } from './formFields'

// Saves, reads and clears the session's copy of what has been typed into the form. The chosen
// file is not part of it: a File cannot be serialised.

const drafts = new Map<string, NewFileTransferValues>()

const STORAGE_PREFIX = 'brokerbox.draft:'

type StoredDraft = Omit<NewFileTransferValues, 'file'>

export function draftKey(resourceId: string, senderOrgNumber: string): string {
  return `${senderOrgNumber}:${resourceId}`
}

export function readDraft(key: string): NewFileTransferValues | undefined {
  return drafts.get(key) ?? readStored(key)
}

export function writeDraft(key: string, values: NewFileTransferValues): void {
  drafts.set(key, values)
  writeStored(key, values)
}

export function clearDraft(key: string): void {
  drafts.delete(key)
  removeSessionItem(STORAGE_PREFIX + key)
}

function readStored(key: string): NewFileTransferValues | undefined {
  const stored = readSessionItem<StoredDraft>(STORAGE_PREFIX + key)
  if (!stored) {
    return undefined
  }
  const restored = { ...stored, file: null }
  drafts.set(key, restored)
  return restored
}

function writeStored(key: string, values: NewFileTransferValues): void {
  // Listed rather than spread, so a new field has to be decided about instead of silently kept.
  const stored: StoredDraft = {
    reference: values.reference,
    recipients: values.recipients,
    metadata: values.metadata,
    virusScan: values.virusScan,
  }
  writeSessionItem(STORAGE_PREFIX + key, stored)
}
