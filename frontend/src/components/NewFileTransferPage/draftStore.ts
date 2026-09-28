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
  try {
    sessionStorage.removeItem(STORAGE_PREFIX + key)
  } catch {
    // Private mode and blocked storage both throw; the draft simply does not outlive the page.
  }
}

function readStored(key: string): NewFileTransferValues | undefined {
  try {
    const raw = sessionStorage.getItem(STORAGE_PREFIX + key)
    if (!raw) {
      return undefined
    }
    const stored = JSON.parse(raw) as StoredDraft
    const restored = { ...stored, file: null }
    drafts.set(key, restored)
    return restored
  } catch {
    return undefined
  }
}

function writeStored(key: string, values: NewFileTransferValues): void {
  try {
    // Listed rather than spread, so a new field has to be decided about instead of silently kept.
    const stored: StoredDraft = {
      reference: values.reference,
      recipients: values.recipients,
      metadata: values.metadata,
      virusScan: values.virusScan,
    }
    sessionStorage.setItem(STORAGE_PREFIX + key, JSON.stringify(stored))
  } catch {
    // As above: what was typed still stands on this page, it just does not survive a reload.
  }
}
