import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import {
  getAccessListMembers,
  mockAccessList,
  type AccessListMember,
} from '../../api/accessListMembers'
import { ApiError } from '../../api/client'
import {
  FileTransferValidationError,
  sendFileTransfer,
  type UploadProgress,
} from '../../api/fileTransfers'
import {
  getResourceConfiguration,
  resolveMaxFileTransferSize,
  type ResourceConfiguration,
} from '../../api/resources'
import {
  emptyValues,
  hasErrors,
  resolveRecipientRules,
  toPropertyList,
  validate,
  type NewFileTransferValues,
} from './newFileTransferForm'

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
  const [loaded, setLoaded] = useState<LoadedResource | null>(null)
  const [draft, setDraft] = useState<NewFileTransferValues>(emptyValues)
  const [submitAttempts, setSubmitAttempts] = useState(0)
  const [sending, setSending] = useState(false)
  const [progress, setProgress] = useState<UploadProgress | null>(null)
  const [submitError, setSubmitError] = useState('')

  const abortRef = useRef<AbortController | null>(null)
  useEffect(() => () => abortRef.current?.abort(), [])

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
  const configuration = resource?.configuration ?? null
  const members = useMemo(() => resource?.members ?? [], [resource])

  const rules = useMemo(
    () => resolveRecipientRules(members, configuration?.requiredParty ?? null, senderOrgNumber),
    [members, configuration, senderOrgNumber],
  )

  const maxFileSize = configuration ? resolveMaxFileTransferSize(configuration) : null

  // What the API will actually accept, which is not always what the user is free to choose:
  // a required party is the only possible recipient, and virus scanning stays on unless approved.
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

    // Only needed if the upload half fails: the transfer exists by then and can be followed up.
    let fileTransferId = ''
    try {
      await sendFileTransfer(
        {
          resourceId,
          sender: senderOrgNumber,
          recipients: values.recipients,
          file: values.file,
          reference: values.reference,
          propertyList: toPropertyList(values.metadata),
          disableVirusScan: !values.virusScan,
        },
        {
          onInitialized: (id) => {
            fileTransferId = id
          },
          onProgress: setProgress,
          signal: controller.signal,
        },
      )
      onSent()
    } catch (error) {
      if (error instanceof DOMException && error.name === 'AbortError') {
        return
      }
      setSubmitError(describeError(error, fileTransferId))
    } finally {
      if (abortRef.current === controller) {
        abortRef.current = null
      }
      setSending(false)
    }
  }, [errors, onSent, resourceId, sending, senderOrgNumber, values])

  const abort = useCallback(() => abortRef.current?.abort(), [])

  return {
    rules,
    maxFileSize,
    virusScanLocked,
    loading: resource === null,
    loadError: resource?.error ?? '',
    values,
    setValue,
    errors: submitAttempts > 0 ? errors : {},
    submitAttempts,
    sending,
    progress,
    submitError,
    submit,
    abort,
  }
}

/** `fileTransferId` is set once initialization succeeded, so a failed upload can be followed up. */
function describeError(error: unknown, fileTransferId: string): string {
  if (error instanceof FileTransferValidationError) {
    return error.message
  }

  if (error instanceof ApiError) {
    const detail = (error.body as { detail?: string } | null)?.detail
    const message = detail ?? `Formidlingen feilet (HTTP ${error.status}).`
    return fileTransferId
      ? `${message} Formidlingen ble opprettet med id ${fileTransferId}, men filen ble ikke lastet opp.`
      : message
  }

  return 'Formidlingen feilet. Prøv igjen.'
}
