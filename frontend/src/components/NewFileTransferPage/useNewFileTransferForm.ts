import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import {
  getAccessListMembers,
  mockAccessList,
  type AccessListMember,
} from '../../api/accessListMembers'
import { ApiError } from '../../api/client'
import { sendFileTransfer } from '../../api/sendFileTransfer'
import {
  getResourceConfiguration,
  resolveMaxFileTransferSize,
  type ResourceConfiguration,
} from '../../api/resourceConfiguration'
import type { UploadProgress } from '../../api/xhrClient'
import { InvalidOrgNumberError } from '../../helpers/orgIdentifierHelper'
import {
  emptyValues,
  toPropertyList,
  type NewFileTransferErrors,
  type NewFileTransferValues,
} from './formFields'
import { hasErrors, validate, validateMetadataRows } from './formValidation'
import { resolveRecipientRules } from './recipientRules'

type Options = {
  resourceId: string
  senderOrgNumber: string
  onSent: () => void
}

type LoadedResource = {
  resourceId: string
  configuration: ResourceConfiguration | null
  members: AccessListMember[]
  error: string
}

/**
 * Owns everything the form does: what the resource allows, who may receive, what the user typed,
 * and the two-step send. The page itself only lays the fields out.
 */
export function useNewFileTransferForm({ resourceId, senderOrgNumber, onSent }: Options) {
  const { configuration, members, loading, loadError } = useResourceContext(resourceId)
  const form = useFormValues(configuration, members, senderOrgNumber)
  const submission = useSubmission({
    resourceId,
    senderOrgNumber,
    values: form.values,
    errors: form.errors,
    onSent,
  })

  return {
    ...form,
    ...submission,
    loading,
    loadError,
    errors: submission.submitAttempts > 0 ? form.errors : {},
    metadataRowErrors: submission.submitAttempts > 0 ? form.metadataRowErrors : [],
  }
}

/** What the resource allows, and who may receive on it. */
function useResourceContext(resourceId: string) {
  const [loaded, setLoaded] = useState<LoadedResource | null>(null)

  useEffect(() => {
    let cancelled = false

    Promise.all([getResourceConfiguration(resourceId), getAccessListMembers(mockAccessList)])
      .then(([configuration, members]) => ({ resourceId, configuration, members, error: '' }))
      .catch(() => ({
        resourceId,
        configuration: null,
        members: [],
        error: 'Kunne ikke hente oppsettet for tjenesten. Prøv å laste siden på nytt.',
      }))
      .then((result) => {
        if (!cancelled) {
          setLoaded(result)
        }
      })

    return () => {
      cancelled = true
    }
  }, [resourceId])

  // Anything loaded for another resource belongs to a previous route, so it counts as not loaded.
  const resource = loaded?.resourceId === resourceId ? loaded : null
  const members = useMemo(() => resource?.members ?? [], [resource])

  return {
    configuration: resource?.configuration ?? null,
    members,
    loading: resource === null,
    loadError: resource?.error ?? '',
  }
}

/** The draft the user is editing, narrowed to what the API accepts, and its validation errors. */
function useFormValues(
  configuration: ResourceConfiguration | null,
  members: AccessListMember[],
  senderOrgNumber: string,
) {
  const [draft, setDraft] = useState<NewFileTransferValues>(emptyValues)

  const rules = useMemo(
    () => resolveRecipientRules(members, configuration?.requiredParty ?? null, senderOrgNumber),
    [members, configuration, senderOrgNumber],
  )
  const maxFileSize = configuration ? resolveMaxFileTransferSize(configuration) : null
  const requiredParty = rules.requiredParty
  const virusScanLocked = !configuration?.approvedForDisabledVirusScan
  const values = useMemo<NewFileTransferValues>(
    () => ({
      ...draft,
      recipients: requiredParty ? [requiredParty.organizationNumber] : draft.recipients,
      virusScan: virusScanLocked || draft.virusScan,
    }),
    [draft, requiredParty, virusScanLocked],
  )

  const setValue = useCallback(
    <K extends keyof NewFileTransferValues>(key: K, value: NewFileTransferValues[K]) => {
      setDraft((current) => ({ ...current, [key]: value }))
    },
    [],
  )

  const errors = useMemo(() => validate(values, maxFileSize, rules), [values, maxFileSize, rules])
  const metadataRowErrors = useMemo(() => validateMetadataRows(values.metadata), [values.metadata])

  return { rules, maxFileSize, virusScanLocked, values, setValue, errors, metadataRowErrors }
}

type SubmissionOptions = Options & {
  values: NewFileTransferValues
  errors: NewFileTransferErrors
}

/** The two-step send, and the progress and failure it reports back. */
function useSubmission({
  resourceId,
  senderOrgNumber,
  values,
  errors,
  onSent,
}: SubmissionOptions) {
  const [submitAttempts, setSubmitAttempts] = useState(0)
  const [sending, setSending] = useState(false)
  const [progress, setProgress] = useState<UploadProgress | null>(null)
  const [submitError, setSubmitError] = useState('')

  const abortRef = useRef<AbortController | null>(null)
  useEffect(() => () => abortRef.current?.abort(), [])

  const send = useCallback(
    async (file: File, signal: AbortSignal) => {
      // Only needed if the upload half fails: the transfer exists by then and can be followed up.
      let fileTransferId = ''
      try {
        await sendFileTransfer(
          {
            resourceId,
            sender: senderOrgNumber,
            recipients: values.recipients,
            file,
            reference: values.reference,
            propertyList: toPropertyList(values.metadata),
            disableVirusScan: !values.virusScan,
          },
          {
            onInitialized: (id) => {
              fileTransferId = id
            },
            onProgress: setProgress,
            signal,
          },
        )
        onSent()
      } catch (error) {
        if (!isAbortError(error)) {
          setSubmitError(describeError(error, fileTransferId))
        }
      }
    },
    [onSent, resourceId, senderOrgNumber, values],
  )

  const submit = useCallback(async () => {
    setSubmitAttempts((attempts) => attempts + 1)
    setSubmitError('')

    if (sending || !values.file || hasErrors(errors)) {
      return
    }

    const controller = new AbortController()
    abortRef.current = controller
    setSending(true)
    setProgress(null)

    await send(values.file, controller.signal)

    if (abortRef.current === controller) {
      abortRef.current = null
    }
    setSending(false)
  }, [errors, send, sending, values])

  const abort = useCallback(() => abortRef.current?.abort(), [])

  return { submitAttempts, sending, progress, submitError, submit, abort }
}

function isAbortError(error: unknown): boolean {
  return error instanceof DOMException && error.name === 'AbortError'
}

/** `fileTransferId` is set once initialization succeeded, so a failed upload can be followed up. */
function describeError(error: unknown, fileTransferId: string): string {
  if (error instanceof InvalidOrgNumberError) {
    return error.message
  }
  if (error instanceof ApiError) {
    return describeApiError(error, fileTransferId)
  }
  return 'Formidlingen feilet. Prøv igjen.'
}

function describeApiError(error: ApiError, fileTransferId: string): string {
  const detail = (error.body as { detail?: string } | null)?.detail
  const message = detail ?? `Formidlingen feilet (HTTP ${error.status}).`
  if (!fileTransferId) {
    return message
  }
  return `${message} Formidlingen ble opprettet med id ${fileTransferId}, men filen ble ikke lastet opp.`
}
