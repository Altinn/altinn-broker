import { useCallback, useEffect, useState } from 'react'
import { ApiError } from '../../api/client'
import { hasReceivedFile } from '../../api/fileTransferStatus'
import { discardUpload, readUploadedBytes } from '../../api/tus/tusUpload'
import { InvalidOrgNumberError } from '../../helpers/orgIdentifierHelper'
import { UploadPlanError, useActiveUpload, useUploadActions } from '../../upload/uploadsContext'
import {
  clearStoredUpload,
  isSameFile,
  readStoredUpload,
  type StoredUpload,
} from '../../upload/uploadSession'
import { toPropertyList, type NewFileTransferErrors, type NewFileTransferValues } from './formFields'
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

  const cancel = useCallback(() => {
    if (hasActive) {
      uploads.cancel()
    }
  }, [hasActive, uploads])

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
    active,
    blockedBy,
    submitError: submitError || active?.error || '',
    interrupted: stored,
    interruptedUploaded: interrupted.uploaded,
    wrongFile,
    resumeReady,
    resumeInterrupted,
    discardInterrupted: interrupted.discard,
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
        } else {
          clearStoredUpload(resourceId, senderOrgNumber)
        }
        setInterruptedUpload(null)
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
  }, [interruptedUpload, markReceived, resourceId, senderOrgNumber])

  const discard = useCallback(() => {
    if (interruptedUpload) {
      discardUpload(interruptedUpload.plan)
    }
    clearStoredUpload(resourceId, senderOrgNumber)
    setInterruptedUpload(null)
  }, [interruptedUpload, resourceId, senderOrgNumber])

  return { upload: interruptedUpload, uploaded, discard }
}

function describeInitializeError(error: unknown): string {
  if (error instanceof UploadPlanError) {
    return `${describeInitializeError(error.cause)} Formidlingen ble opprettet med id ${error.fileTransferId}, men filen ble ikke lastet opp.`
  }
  if (error instanceof InvalidOrgNumberError) {
    return error.message
  }
  if (error instanceof ApiError) {
    const detail = (error.body as { detail?: string } | null)?.detail
    return detail ?? `Formidlingen feilet (HTTP ${error.status}).`
  }
  return 'Formidlingen feilet. Prøv igjen.'
}
