import { useCallback, useEffect, useState } from 'react'
import { ApiError } from '../../api/client'
import { FileChangedError, discardUpload } from '../../api/tus/tusUpload'
import { toUploadPlan } from '../../api/uploadPlan'
import { formatFileSize } from '../../helpers/fileSizeHelper'
import { InvalidOrgNumberError } from '../../helpers/orgIdentifierHelper'
import { matchesFingerprint } from '../../upload/fileFingerprint'
import { UploadPlanError, useActiveUpload, useUploadActions } from '../../upload/uploadsContext'
import type { ResumableUpload } from './useResumableUpload'
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
  /** A stopped upload the form continues instead of sending a new transfer. */
  resume: ResumableUpload | null
}

export function useFileTransferUpload({ resourceId, senderOrgNumber, values, errors, resume }: Options) {
  const uploads = useUploadActions()
  const live = useActiveUpload()
  const [submitAttempts, setSubmitAttempts] = useState(0)
  const [submitError, setSubmitError] = useState('')

  const isOwn = live?.resourceId === resourceId && live.sender === senderOrgNumber
  const active = isOwn ? live : null
  const blockedBy = isOwn ? null : live

  // Leaving the page pauses rather than aborts, so nothing is left locked on the server.
  const { pause } = uploads
  const hasActive = active !== null
  useEffect(() => (hasActive ? pause : undefined), [hasActive, pause])

  const submit = useCallback(async () => {
    setSubmitAttempts((attempts) => attempts + 1)
    setSubmitError('')

    // A stopped upload is continued, never sent again as a new transfer.
    if (active || resume) {
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
  }, [active, errors, resourceId, resume, senderOrgNumber, uploads, values])

  const resumeActive = useCallback(() => {
    setSubmitError('')
    uploads.resume()
  }, [uploads])

  // Once the stopped upload runs again, it is the active one.
  const interrupted = active === null ? resume : null
  const fileCheck = useSameFileCheck(interrupted, values.file)
  const resumeReady = fileCheck?.error === null

  const resumeInterrupted = useCallback(() => {
    const file = values.file
    if (!interrupted || !file || !resumeReady) {
      return
    }
    setSubmitError('')
    uploads.resumeStored(
      {
        resourceId,
        sender: senderOrgNumber,
        plan: toUploadPlan(interrupted.fileTransferId, interrupted.plan),
        file,
      },
      // Only the bar's starting point; the library asks each upload its own offset regardless.
      interrupted.plan.uploadedBytes,
    )
  }, [interrupted, resourceId, resumeReady, senderOrgNumber, uploads, values.file])

  const discardInterrupted = useCallback(() => {
    if (interrupted) {
      discardUpload(toUploadPlan(interrupted.fileTransferId, interrupted.plan))
    }
  }, [interrupted])

  return {
    submitAttempts,
    active,
    blockedBy,
    submitError: submitError || active?.error || '',
    interrupted,
    checkingFile: interrupted !== null && values.file !== null && fileCheck === null,
    wrongFile: fileCheck?.error ?? undefined,
    resumeReady,
    resumeInterrupted,
    discardInterrupted,
    submit,
    pause,
    resume: resumeActive,
    cancel: uploads.cancel,
    cancelBlocking: uploads.cancel,
  }
}

/**
 * Whether the picked file is the one the stopped upload started with. Name and size are known at once;
 * the content takes a moment to read, and until then there is no answer.
 */
function useSameFileCheck(upload: ResumableUpload | null, file: File | null) {
  const [checked, setChecked] = useState<{ file: File; error: string | null } | null>(null)

  useEffect(() => {
    if (!upload || !file || describeMismatch(upload, file)) {
      return
    }
    let current = true
    const differs = `Innholdet i «${file.name}» er ikke det samme som i filen opplastingen startet med. Velg den samme filen, eller avbryt opplastingen for å sende noe annet.`
    matchesFingerprint(file, upload.plan)
      .then((matches) => current && setChecked({ file, error: matches ? null : differs }))
      .catch(() => current && setChecked({ file, error: 'Klarte ikke å lese filen. Velg den på nytt.' }))
    return () => {
      current = false
    }
  }, [file, upload])

  if (!upload || !file) {
    return null
  }
  const mismatch = describeMismatch(upload, file)
  if (mismatch) {
    return { file, error: mismatch }
  }
  return checked?.file === file ? checked : null
}

function describeMismatch(upload: ResumableUpload, file: File): string | null {
  if (file.name !== upload.fileName || file.size !== upload.fileSize) {
    return `Dette er ikke filen opplastingen leser. Velg «${upload.fileName}» (${formatFileSize(upload.fileSize)}), eller avbryt opplastingen for å sende noe annet.`
  }
  return null
}

function describeInitializeError(error: unknown): string {
  if (error instanceof UploadPlanError) {
    return `${describeInitializeError(error.cause)} Formidlingen ble opprettet med id ${error.fileTransferId}, men filen ble ikke lastet opp.`
  }
  if (error instanceof FileChangedError) {
    return 'Filen er endret eller flyttet etter at du valgte den. Velg filen på nytt.'
  }
  if (error instanceof InvalidOrgNumberError) {
    return error.message
  }
  if (error instanceof ApiError) {
    const body = error.body as { detail?: string; errorCode?: number } | null
    const notificationMessage = body?.errorCode ? notificationErrorMessages[body.errorCode] : undefined
    return notificationMessage ?? body?.detail ?? `Formidlingen feilet (HTTP ${error.status}).`
  }
  return 'Formidlingen feilet. Prøv igjen.'
}

/** The API's notification errors (NotificationErrors in the backend), worded for the user. */
const notificationErrorMessages: Record<number, string> = {
  8001: 'En av de andre som skal varsles mangler e-postadresse eller mobilnummer.',
  8002: 'En av de andre som skal varsles har både e-postadresse og mobilnummer.',
  8003: 'En av e-postadressene til de andre som skal varsles er ugyldig.',
  8004: 'Et av mobilnumrene til de andre som skal varsles er ugyldig.',
  8005: 'En av de andre som skal varsles mangler hvilken mottaker varselet gjelder.',
  8006: 'En av de andre som skal varsles er knyttet til en organisasjon som ikke er mottaker av formidlingen.',
  8007: 'En av de andre som skal varsles er knyttet til et ugyldig organisasjonsnummer.',
}
