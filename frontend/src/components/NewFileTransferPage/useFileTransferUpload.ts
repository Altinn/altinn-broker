import { useCallback, useEffect, useState } from 'react'
import { ApiError } from '../../api/client'
import { i18n } from '../../i18n/config'
import { hasReceivedFile } from '../../api/fileTransferStatus'
import { FileChangedError, discardUpload, readUploadedBytes } from '../../api/tus/tusUpload'
import { InvalidOrgNumberError } from '../../helpers/orgIdentifierHelper'
import { UploadPlanError, useActiveUpload, useUploadActions } from '../../upload/uploadsContext'
import {
  clearStoredUpload,
  isSameFile,
  readStoredUpload,
  type StoredUpload,
} from '../../upload/uploadSession'
import {
  toChecksumInput,
  toNotificationInput,
  toPropertyList,
  type NewFileTransferErrors,
  type NewFileTransferValues,
} from './formFields'
import { hasErrors } from './formValidation'

// Adapts the uploads context to this form: scopes the active upload to this resource and sender, and
// exposes submit, pause, resume, cancel and the interrupted-upload actions.

type Options = {
  resourceId: string
  senderOrgNumber: string
  values: NewFileTransferValues
  errors: NewFileTransferErrors
}

export function useFileTransferUpload({ resourceId, senderOrgNumber, values, errors }: Options) {
  const uploads = useUploadActions()
  const live = useActiveUpload()
  const [submitAttempts, setSubmitAttempts] = useState(0)
  const [submitError, setSubmitError] = useState('')

  const isOwn = live?.resourceId === resourceId && live.sender === senderOrgNumber
  const active = isOwn ? live : null
  const blockedBy = isOwn ? null : live

  const interrupted = useInterruptedUpload(resourceId, senderOrgNumber, active !== null)

  // Leaving the page pauses rather than aborts, so nothing is left locked on the server.
  const { pause } = uploads
  const hasActive = active !== null
  useEffect(() => (hasActive ? pause : undefined), [hasActive, pause])

  const discardInterrupted = interrupted.discard
  const cancel = useCallback(() => {
    if (hasActive) {
      uploads.cancel()
    } else {
      discardInterrupted()
    }
  }, [discardInterrupted, hasActive, uploads])

  const submit = useCallback(async () => {
    setSubmitAttempts((attempts) => attempts + 1)
    setSubmitError('')

    if (active) {
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
        ...toChecksumInput(values),
        disableVirusScan: !values.virusScan,
        notification: toNotificationInput(values),
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
    active,
    blockedBy,
    submitError: submitError || active?.error || '',
    interrupted: stored,
    interruptedUploaded: interrupted.uploaded,
    wrongFile,
    resumeReady,
    resumeInterrupted,
    submit,
    pause,
    resume,
    cancel,
    cancelBlocking: uploads.cancel,
  }
}

function useInterruptedUpload(resourceId: string, senderOrgNumber: string, hasActive: boolean) {
  const { markReceived } = useUploadActions()
  const [interruptedUpload, setInterruptedUpload] = useState<StoredUpload | null>(() =>
    readStoredUpload(resourceId, senderOrgNumber),
  )
  if (hasActive && interruptedUpload) {
    setInterruptedUpload(null)
  }
  const [uploaded, setUploaded] = useState<number | null>(null)

  const discard = useCallback(() => {
    if (interruptedUpload) {
      discardUpload(interruptedUpload.plan)
    }
    clearStoredUpload(resourceId, senderOrgNumber)
    setInterruptedUpload(null)
  }, [interruptedUpload, resourceId, senderOrgNumber])

  useEffect(() => {
    if (!interruptedUpload) {
      return
    }

    // Aborted on the way out, so leaving the page also stops the retries that wait out a lock or other error.
    const controller = new AbortController()

    readUploadedBytes(interruptedUpload.plan, controller.signal)
      .then(async (bytes) => {
        if (controller.signal.aborted) {
          return
        }
        if (bytes !== null) {
          setUploaded(bytes)
          return
        }
        const received = await hasReceivedFile(interruptedUpload.plan.fileTransferId)
        if (controller.signal.aborted) {
          return
        }
        if (received) {
          markReceived({ resourceId, sender: senderOrgNumber, plan: interruptedUpload.plan })
          setInterruptedUpload(null)
        } else {
          discard()
        }
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
  }, [discard, interruptedUpload, markReceived, resourceId, senderOrgNumber])

  return { upload: interruptedUpload, uploaded, discard }
}

function describeInitializeError(error: unknown): string {
  if (error instanceof UploadPlanError) {
    return `${describeInitializeError(error.cause)} ${i18n.t('page.upload_errors.created_not_uploaded', { id: error.fileTransferId })}`
  }
  if (error instanceof FileChangedError) {
    return i18n.t('page.upload_errors.changed')
  }
  if (error instanceof InvalidOrgNumberError) {
    return i18n.t('page.upload_errors.invalid_org', { value: error.value })
  }
  if (error instanceof ApiError) {
    const body = error.body as { detail?: string; errorCode?: number } | null
    const notificationMessage = notificationErrorMessage(body?.errorCode)
    return notificationMessage ?? body?.detail ?? i18n.t('page.upload_errors.failed_status', { status: error.status })
  }
  return i18n.t('page.upload_errors.failed')
}

/** The API's notification errors (NotificationErrors in the backend), worded for the user. */
function notificationErrorMessage(errorCode: number | undefined): string | undefined {
  switch (errorCode) {
    case 8001:
    case 8002:
    case 8003:
    case 8004:
    case 8005:
    case 8006:
    case 8007:
      return i18n.t(`page.upload_errors.c${errorCode}`)
    default:
      return undefined
  }
}
