import { createContext, useContext } from 'react'
import type { UploadPlan, UploadProgress } from '../api/tus/tusUpload'

export type UploadStatus = 'initializing' | 'uploading' | 'finishing' | 'paused' | 'failed'

export type ActiveUpload = {
  resourceId: string
  sender: string
  fileTransferId: string
  fileName: string
  fileSize: number
  status: UploadStatus
  progress: UploadProgress | null
  error: string
}

export type StartUploadInput = {
  resourceId: string
  sender: string
  file: File
  recipients: string[]
  reference: string
  propertyList: Record<string, string>
  disableVirusScan: boolean
}

export type ResumeStoredInput = {
  resourceId: string
  sender: string
  fileTransferId: string
  plan: UploadPlan
  file: File
}

export type UploadSuccessListener = (upload: { fileTransferId: string; resourceId: string }) => void

export type UploadsContextValue = {
  active: ActiveUpload | null
  start: (input: StartUploadInput) => Promise<void>
  resumeStored: (upload: ResumeStoredInput, alreadySent: number) => void
  pause: () => void
  resume: () => void
  cancel: () => void
  addUploadSuccessListener: (listener: UploadSuccessListener) => void
  removeUploadSuccessListener: (listener: UploadSuccessListener) => void
}

export const UploadsContext = createContext<UploadsContextValue | null>(null)

export function useUploads(): UploadsContextValue {
  const context = useContext(UploadsContext)
  if (!context) {
    throw new Error('useUploads must be used within an UploadsProvider')
  }
  return context
}
