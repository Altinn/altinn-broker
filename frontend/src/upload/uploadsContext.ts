import { createContext, useContext } from 'react'
import type { InitializeFileTransferInput } from '../api/initializeFileTransfer'
import type { UploadPlan, UploadProgress, UploadRunStatus } from '../api/tus/tusUpload'

// Three contexts, so that what changes several times a second re-renders only what shows it:
// the actions never change, the active upload changes with its status, and the progress all the time.

export type UploadStatus = 'initializing' | UploadRunStatus | 'failed'

export type ActiveUpload = {
  resourceId: string
  sender: string
  fileName: string
  fileSize: number
  status: UploadStatus
  error: string
}

export type StartUploadInput = InitializeFileTransferInput

/** A file and the plan for uploading it to a transfer the sender has already initialized. */
export type PlannedUpload = {
  resourceId: string
  sender: string
  plan: UploadPlan
  file: File
}

export type UploadSuccessListener = (upload: {
  fileTransferId: string
  resourceId: string
  sender: string
}) => void

export type UploadActions = {
  start: (input: StartUploadInput) => Promise<void>
  resumeStored: (upload: PlannedUpload, alreadySent: number) => void
  pause: () => void
  resume: () => void
  cancel: () => void
  /** Returns the function that removes the listener again. */
  addUploadSuccessListener: (listener: UploadSuccessListener) => () => void
}

/** The transfer was created, but the upload of its file could not be prepared. */
export class UploadPlanError extends Error {
  readonly fileTransferId: string

  constructor(fileTransferId: string, cause: unknown) {
    super('The upload could not be prepared', { cause })
    this.name = 'UploadPlanError'
    this.fileTransferId = fileTransferId
  }
}

export const UploadActionsContext = createContext<UploadActions | null>(null)
export const ActiveUploadContext = createContext<ActiveUpload | null>(null)
export const UploadProgressContext = createContext<UploadProgress | null>(null)

export function useUploadActions(): UploadActions {
  const actions = useContext(UploadActionsContext)
  if (!actions) {
    throw new Error('useUploadActions must be used within an UploadsProvider')
  }
  return actions
}

export function useActiveUpload(): ActiveUpload | null {
  return useContext(ActiveUploadContext)
}

export function useUploadProgress(): UploadProgress | null {
  return useContext(UploadProgressContext)
}
