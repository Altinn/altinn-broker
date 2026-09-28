import { useCallback, useEffect, useRef, useState } from 'react'
import { ApiError } from '../../api/client'
import { discardUpload, readUploadedBytes } from '../../api/tus/tusUpload'
import { InvalidOrgNumberError } from '../../helpers/orgIdentifierHelper'
import { useUploads } from '../../upload/uploadsContext'
import {
  clearStoredUpload,
  isSameFile,
  readAnyStoredUpload,
  readStoredUpload,
  type StoredUpload,
} from '../../upload/uploadSession'
import type { UploadStatus } from '../../upload/uploadsContext'
import { toPropertyList, type NewFileTransferErrors, type NewFileTransferValues } from './formFields'
import { hasErrors } from './formValidation'

// Adapts UploadsContext to this form: scopes the active upload to this resource and sender, and
// exposes submit, pause, resume, cancel and the interrupted-upload actions.

type Options = {
  resourceId: string
  senderOrgNumber: string
  values: NewFileTransferValues
  errors: NewFileTransferErrors
}

export function useFileTransferUpload({ resourceId, senderOrgNumber, values, errors }: Options) {
  const uploads = useUploads()
  const [submitAttempts, setSubmitAttempts] = useState(0)
  const [submitError, setSubmitError] = useState('')

  // An upload that belongs to another service is none of this page's business.
  const active =
    uploads.active?.resourceId === resourceId && uploads.active.sender === senderOrgNumber
      ? uploads.active
      : null

  const blockedBy = useBlockingUpload(resourceId, senderOrgNumber, active !== null)

  const interrupted = useInterruptedUpload(resourceId, senderOrgNumber, active !== null)

  // Leaving the page pauses rather than aborts, so nothing is left locked on the server.
  const { pause } = uploads
  const hasActiveRef = useRef(false)
  useEffect(() => {
    hasActiveRef.current = active !== null
  })
  useEffect(
    () => () => {
      if (hasActiveRef.current) {
        pause()
      }
    },
    [pause],
  )

  const submit = useCallback(async () => {
    setSubmitAttempts((attempts) => attempts + 1)
    setSubmitError('')

    if (active) {
      return
    }

    if (uploads.active) {
      setSubmitError('En annen opplasting holder plassen. Fortsett eller avbryt den først.')
      return
    }

    const file = values.file
    if (!file || hasErrors(errors)) {
      return
    }

    try {
      await uploads.start({
        resourceId,
        sender: senderOrgNumber,
        file,
        recipients: values.recipients,
        reference: values.reference,
        propertyList: toPropertyList(values.metadata),
        disableVirusScan: !values.virusScan,
      })
    } catch (error) {
      setSubmitError(describeInitializeError(error))
    }
  }, [active, errors, resourceId, senderOrgNumber, uploads, values])

  const resume = useCallback(() => {
    setSubmitError('')
    uploads.resume()
  }, [uploads])

  const stored = interrupted.upload
  const resumeReady =
    stored !== null && values.file !== null && isSameFile(stored.file, values.file)

  // With an interrupted upload pending, any other file is a mistake rather than a new transfer.
  const wrongFile =
    stored !== null && values.file !== null && !resumeReady
      ? `Dette er ikke filen opplastingen leser. Velg «${stored.file.name}», eller forkast den avbrutte opplastingen for å sende noe annet.`
      : undefined

  const resumeInterrupted = useCallback(() => {
    const file = values.file
    if (!stored || !file || !isSameFile(stored.file, file)) {
      return
    }
    setSubmitError('')
    uploads.resumeStored(
      {
        resourceId,
        sender: senderOrgNumber,
        fileTransferId: stored.fileTransferId,
        plan: stored.plan,
        file,
      },
      // Only the bar's starting point; the library asks each upload its own offset regardless.
      interrupted.uploaded ?? 0,
    )
  }, [interrupted.uploaded, resourceId, senderOrgNumber, stored, uploads, values.file])

  return {
    submitAttempts,
    blockedBy,
    sending: active !== null,
    initializing: active?.status === 'initializing',
    paused: active?.status === 'paused',
    failed: active?.status === 'failed',
    finishing: active?.status === 'finishing',
    progress: active?.progress ?? null,
    activeFile: active && { name: active.fileName, size: active.fileSize },
    submitError: submitError || active?.error || '',
    interrupted: stored,
    interruptedUploaded: interrupted.uploaded,
    wrongFile,
    resumeReady,
    resumeInterrupted,
    discardInterrupted: interrupted.discard,
    submit,
    pause: uploads.pause,
    resume,
    cancel: uploads.cancel,
  }
}

export type BlockingUpload = {
  resourceId: string
  fileName: string
  fileSize: number
  percent: number | null
  status: UploadStatus | 'interrupted'
  cancel: () => void
}

function useBlockingUpload(
  resourceId: string,
  senderOrgNumber: string,
  mine: boolean,
): BlockingUpload | null {
  const uploads = useUploads()
  const live = mine ? null : uploads.active

  const [stored, setStored] = useState<StoredUpload | null>(() => readAnyStoredUpload())
  const [uploaded, setUploaded] = useState<number | null>(null)

  const elsewhere =
    stored !== null && (stored.resourceId !== resourceId || stored.sender !== senderOrgNumber)
      ? stored
      : null
  const pending = live === null ? elsewhere : null

  useEffect(() => {
    if (!pending) {
      return
    }

    let cancelled = false

    readUploadedBytes(pending.plan)
      .then((bytes) => {
        if (cancelled) {
          return
        }
        // Nothing left to carry on to, so the record blocks nobody.
        if (bytes === null) {
          clearStoredUpload()
          setStored(null)
          return
        }
        setUploaded(bytes)
      })
      .catch((error) => {
        console.error('Could not read how far the blocking upload got', error)
      })

    return () => {
      cancelled = true
    }
  }, [pending])

  if (live) {
    return {
      resourceId: live.resourceId,
      fileName: live.fileName,
      fileSize: live.fileSize,
      percent: live.progress?.percent ?? null,
      status: live.status,
      cancel: uploads.cancel,
    }
  }

  if (!pending) {
    return null
  }

  return {
    resourceId: pending.resourceId,
    fileName: pending.file.name,
    fileSize: pending.file.size,
    percent: uploaded === null ? null : Math.floor((uploaded / pending.file.size) * 100),
    status: 'interrupted',
    cancel: () => {
      discardUpload(pending.plan)
      clearStoredUpload()
      setStored(null)
    },
  }
}

function useInterruptedUpload(resourceId: string, senderOrgNumber: string, hasActive: boolean) {
  const key = `${senderOrgNumber}:${resourceId}`
  const [found, setFound] = useState<{ key: string; upload: StoredUpload | null }>(() => ({
    key,
    upload: readStoredUpload(resourceId, senderOrgNumber),
  }))
  if (found.key !== key) {
    setFound({ key, upload: readStoredUpload(resourceId, senderOrgNumber) })
  }

  const upload = hasActive || found.key !== key ? null : found.upload
  const [uploaded, setUploaded] = useState<number | null>(null)

  useEffect(() => {
    if (!upload) {
      return
    }

    // Left to finish rather than aborted: aborting leaves the upload locked against the next run.
    let cancelled = false

    readUploadedBytes(upload.plan)
      .then((bytes) => {
        if (cancelled) {
          return
        }
        if (bytes === null) {
          clearStoredUpload()
          setFound((current) => ({ ...current, upload: null }))
          return
        }
        setUploaded(bytes)
      })
      .catch((error) => {
        console.error('Could not read how far the interrupted upload got', error)
      })

    return () => {
      cancelled = true
    }
  }, [upload])

  const discard = useCallback(() => {
    if (upload) {
      discardUpload(upload.plan)
    }
    clearStoredUpload()
    setFound((current) => ({ ...current, upload: null }))
  }, [upload])

  return { upload, uploaded, discard }
}

function describeInitializeError(error: unknown): string {
  if (error instanceof InvalidOrgNumberError) {
    return error.message
  }
  if (error instanceof ApiError) {
    const detail = (error.body as { detail?: string } | null)?.detail
    return detail ?? `Formidlingen feilet (HTTP ${error.status}).`
  }
  return 'Formidlingen feilet. Prøv igjen.'
}
