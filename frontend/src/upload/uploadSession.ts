import type { UploadPlan } from '../api/tus/tusUpload'

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
  fileTransferId: string
  file: FileFingerprint
  plan: UploadPlan
}

export function fingerprintOf(file: File): FileFingerprint {
  return { name: file.name, size: file.size, lastModified: file.lastModified }
}

export function isSameFile(fingerprint: FileFingerprint, file: File): boolean {
  return (
    fingerprint.name === file.name &&
    fingerprint.size === file.size &&
    fingerprint.lastModified === file.lastModified
  )
}

export function readStoredUpload(resourceId: string, sender: string): StoredUpload | null {
  try {
    const raw = sessionStorage.getItem(storageKey(resourceId, sender))
    return raw ? (JSON.parse(raw) as StoredUpload) : null
  } catch {
    return null
  }
}

export function saveStoredUpload(upload: StoredUpload): void {
  try {
    sessionStorage.setItem(storageKey(upload.resourceId, upload.sender), JSON.stringify(upload))
  } catch {
    // Private mode and blocked storage both throw; the upload still runs.
  }
}

export function clearStoredUpload(resourceId: string, sender: string): void {
  try {
    sessionStorage.removeItem(storageKey(resourceId, sender))
  } catch {
    // As above: losing the record only costs the resume.
  }
}

// One upload record per service and sender.
function storageKey(resourceId: string, sender: string): string {
  return `${STORAGE_PREFIX}${sender}:${resourceId}`
}
