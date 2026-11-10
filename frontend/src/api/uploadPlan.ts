import { ApiError, apiFetch } from './client'
import { BROKER_API_PREFIX } from './config'
import { tusUploadPath } from './tus/tusProtocol'
import type { UploadPlan } from './tus/tusUpload'

// The server's copy of how a transfer's file is split into tus uploads, so that the upload can be
// continued from another browser. The file's name is the transfer's; its size is where the last part ends.

export type FileFingerprint = {
  fingerprint: string
  fingerprintVersion: number
}

export type StoredUploadPart = {
  /** The transfer's own id for a file sent whole, else the partial upload's id. */
  uploadId: string
  start: number
  length: number
  /** For showing progress only; tus is asked again when the upload continues. */
  uploadedBytes: number
}

export type StoredUploadPlan = FileFingerprint & {
  uploadedBytes: number
  /** In the order the file is joined from them. */
  parts: StoredUploadPart[]
}

function uploadPlanPath(fileTransferId: string): string {
  return `${BROKER_API_PREFIX}/frontend/file-transfers/${fileTransferId}/upload-plan`
}

export async function saveUploadPlan(plan: UploadPlan, fingerprint: FileFingerprint): Promise<void> {
  await apiFetch(uploadPlanPath(plan.fileTransferId), {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      ...fingerprint,
      parts: plan.parts.map((part) => ({
        uploadId: uploadIdOf(part.path),
        start: part.start,
        length: part.length,
      })),
    }),
  })
}

/** The plan, or null when the transfer has no upload that can be continued. */
export async function getUploadPlan(fileTransferId: string): Promise<StoredUploadPlan | null> {
  try {
    return await apiFetch<StoredUploadPlan>(uploadPlanPath(fileTransferId), {
      redirectOnUnauthorized: false,
    })
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) {
      return null
    }
    throw error
  }
}

/** The size of the file the plan uploads. */
export function planFileSize(plan: StoredUploadPlan): number {
  const last = plan.parts[plan.parts.length - 1]
  return last ? last.start + last.length : 0
}

export function toUploadPlan(fileTransferId: string, stored: StoredUploadPlan): UploadPlan {
  return {
    fileTransferId,
    parts: stored.parts.map(({ uploadId, start, length }) => ({
      path: uploadPath(fileTransferId, uploadId),
      start,
      length,
    })),
  }
}

// The same paths the server hands out when the uploads are created.
function uploadPath(fileTransferId: string, uploadId: string): string {
  return uploadId.toLowerCase() === fileTransferId.toLowerCase()
    ? tusUploadPath(fileTransferId)
    : `${tusUploadPath(fileTransferId)}/partial/${uploadId}`
}

// The upload's id is the last segment of its path, for a single upload and a partial alike.
function uploadIdOf(path: string): string {
  return path.replace(/\/+$/, '').split('/').pop() ?? path
}
