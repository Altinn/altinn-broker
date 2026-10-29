import type { UploadPlan } from '../api/tus/tusUpload'
import { readSessionItem, removeSessionItem, writeSessionItem } from '../helpers/sessionStorageHelper'
import type { PlannedUpload } from './uploadsContext'

// Saves, reads and clears the session's record of an upload in progress.

const STORAGE_PREFIX = 'brokerbox.upload-session:'

export type FileFingerprint = {
  name: string
  size: number
  lastModified: number
}

export type StoredUpload = {
  resourceId: string
  sender: string
  file: FileFingerprint
  plan: UploadPlan
}

export function isSameFile(fingerprint: FileFingerprint, file: File): boolean {
  return (
    fingerprint.name === file.name &&
    fingerprint.size === file.size &&
    fingerprint.lastModified === file.lastModified
  )
}

export function readStoredUpload(resourceId: string, sender: string): StoredUpload | null {
  return readSessionItem<StoredUpload>(storageKey(resourceId, sender))
}

export function saveStoredUpload({ resourceId, sender, file, plan }: PlannedUpload): void {
  const stored: StoredUpload = {
    resourceId,
    sender,
    file: { name: file.name, size: file.size, lastModified: file.lastModified },
    plan,
  }
  writeSessionItem(storageKey(resourceId, sender), stored)
}

export function clearStoredUpload(resourceId: string, sender: string): void {
  removeSessionItem(storageKey(resourceId, sender))
}

// One upload record per service and sender.
function storageKey(resourceId: string, sender: string): string {
  return `${STORAGE_PREFIX}${sender}:${resourceId}`
}
