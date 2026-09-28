import type { UploadPlan } from '../api/tus/tusUpload'

// Saves, reads and clears the session's record of an upload in progress.

const STORAGE_KEY = 'brokerbox.upload-session'

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

export function readAnyStoredUpload(): StoredUpload | null {
  return read()
}

export function readStoredUpload(resourceId: string, sender: string): StoredUpload | null {
  const stored = read()
  if (!stored) {
    return null
  }

  return stored.resourceId === resourceId && stored.sender === sender ? stored : null
}

export function saveStoredUpload(upload: StoredUpload): void {
  write(upload)
}

export function clearStoredUpload(): void {
  try {
    sessionStorage.removeItem(STORAGE_KEY)
  } catch {
    // Private mode and blocked storage both throw; losing the record only costs the resume.
  }
}

function read(): StoredUpload | null {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY)
    return raw ? (JSON.parse(raw) as StoredUpload) : null
  } catch {
    return null
  }
}

function write(upload: StoredUpload): void {
  try {
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(upload))
  } catch {
    // As above: the upload still runs, it just cannot be picked up again after a reload.
  }
}
