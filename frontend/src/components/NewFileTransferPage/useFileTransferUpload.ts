import { useCallback, useEffect, useRef, useState } from 'react'
import { ApiError } from '../../api/client'
import { discardUpload, readUploadedBytes } from '../../api/tus/tusUpload'
import { InvalidOrgNumberError } from '../../helpers/orgIdentifierHelper'
import { useUploads } from '../../upload/uploadsContext'
import {
  clearStoredUpload,
  isSameFile,
  readStoredUpload,
  type StoredUpload,
} from '../../upload/uploadSession'
import type { ActiveUpload, UploadStatus } from '../../upload/uploadsContext'
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

  const blockedBy = blockingUpload(active === null ? uploads.active : null, uploads.cancel)

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
      { resourceId, sender: senderOrgNumber, plan: stored.plan, file },
      // Only the bar's starting point; the library asks each upload its own offset regardless.
      interrupted.uploaded ?? 0,
    )
  }, [interrupted.uploaded, resourceId, senderOrgNumber, stored, uploads, values.file])

  return {
    submitAttempts,
    blockedBy,
    sending: active !== null,
    initializing: active?.status === 'initializing',
    pausing: active?.status === 'pausing',
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
  status: UploadStatus
  cancel: () => void
}

// Only an active upload blocks this form: records are kept per service, so an upload left
// unfinished elsewhere is waiting there rather than holding this one up.
function blockingUpload(live: ActiveUpload | null, cancel: () => void): BlockingUpload | null {
  if (live === null) {
    return null
  }

  return {
    resourceId: live.resourceId,
    fileName: live.fileName,
    fileSize: live.fileSize,
    percent: live.progress?.percent ?? null,
    status: live.status,
    cancel,
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

    // Aborted on the way out, so leaving the page also stops the retries that wait out a lock or other error.
    const controller = new AbortController()

    readUploadedBytes(upload.plan, controller.signal)
      .then((bytes) => {
        if (controller.signal.aborted) {
          return
        }
        if (bytes === null) {
          clearStoredUpload(resourceId, senderOrgNumber)
          setFound((current) => ({ ...current, upload: null }))
          return
        }
        setUploaded(bytes)
      })
      .catch((error) => {
        if (controller.signal.aborted) {
          return
        }
        console.error('Could not read how far the interrupted upload got', error)
      })

    return () => {
      controller.abort()
    }
  }, [resourceId, senderOrgNumber, upload])

  const discard = useCallback(() => {
    if (upload) {
      discardUpload(upload.plan)
    }
    clearStoredUpload(resourceId, senderOrgNumber)
    setFound((current) => ({ ...current, upload: null }))
  }, [resourceId, senderOrgNumber, upload])

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
