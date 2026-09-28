import { useCallback, useEffect, useMemo, useState } from 'react'
import { getAllowedRecipients, type AllowedRecipient } from '../../api/allowedRecipients'
import { getResourceConfiguration, type ResourceConfiguration } from '../../api/resourceConfiguration'
import {
  emptyValues,
  metadataInputId,
  type MetadataEntry,
  type NewFileTransferValues,
} from './formFields'
import { draftKey, readDraft, writeDraft } from './draftStore'
import { validate, validateMetadataRows, type MetadataRowError } from './formValidation'
import { resolveRecipientRules } from './recipientRules'
import { useFileTransferUpload } from './useFileTransferUpload'

type Options = {
  resourceId: string
  senderOrgNumber: string
}

type LoadedResource = {
  /** The sender and resource the content belongs to. */
  key: string
  configuration: ResourceConfiguration | null
  recipients: AllowedRecipient[]
  error: string
}

/**
 * Owns everything the form does: what the resource allows, who may receive, what the user typed,
 * and the send. The page itself only lays the fields out.
 */
export function useNewFileTransferForm({ resourceId, senderOrgNumber }: Options) {
  const { configuration, recipients, loading, loadError } = useResourceContext(
    resourceId,
    senderOrgNumber,
  )
  const form = useFormValues(
    configuration,
    recipients,
    senderOrgNumber,
    draftKey(resourceId, senderOrgNumber),
  )
  const upload = useFileTransferUpload({
    resourceId,
    senderOrgNumber,
    values: form.values,
    errors: form.errors,
  })

  return {
    ...form,
    ...upload,
    loading,
    loadError,
    errors: upload.submitAttempts > 0 ? form.errors : {},
    metadataRowErrors: upload.submitAttempts > 0 ? form.metadataRowErrors : [],
  }
}

/** What the resource allows, and who may receive on it. */
function useResourceContext(resourceId: string, senderOrgNumber: string) {
  const [loaded, setLoaded] = useState<LoadedResource | null>(null)
  // Allowed recipients depend on the sender, so both identify what was loaded.
  const key = `${senderOrgNumber}:${resourceId}`

  useEffect(() => {
    if (!senderOrgNumber) {
      return
    }

    let cancelled = false

    Promise.all([
      getResourceConfiguration(resourceId),
      getAllowedRecipients(resourceId, senderOrgNumber),
    ])
      .then(([configuration, recipients]) => ({ key, configuration, recipients, error: '' }))
      .catch(() => ({
        key,
        configuration: null,
        recipients: [],
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
  }, [key, resourceId, senderOrgNumber])

  // Anything loaded for another resource or party belongs to a previous route, so it counts as not loaded.
  const resource = loaded?.key === key ? loaded : null
  const recipients = useMemo(() => resource?.recipients ?? [], [resource])

  return {
    configuration: resource?.configuration ?? null,
    recipients,
    loading: resource === null,
    loadError: resource?.error ?? '',
  }
}

/** The draft the user is editing, narrowed to what the API accepts, and its validation errors. */
function useFormValues(
  configuration: ResourceConfiguration | null,
  recipients: AllowedRecipient[],
  senderOrgNumber: string,
  key: string,
) {
  const [stored, setStored] = useState(() => ({ key, draft: readDraft(key) ?? emptyValues() }))
  // A route to another service is a different draft, so what was typed into this one is left alone.
  if (stored.key !== key) {
    setStored({ key, draft: readDraft(key) ?? emptyValues() })
  }
  const draft = stored.draft

  const setDraft = useCallback(
    (change: (current: NewFileTransferValues) => NewFileTransferValues) => {
      setStored((current) => {
        const next = change(current.draft)
        writeDraft(current.key, next)
        return { ...current, draft: next }
      })
    },
    [],
  )

  const rules = useMemo(
    () => resolveRecipientRules(recipients, configuration?.requiredParty ?? null, senderOrgNumber),
    [recipients, configuration, senderOrgNumber],
  )
  const maxFileSize = configuration?.maxFileTransferSize ?? null
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
    [setDraft],
  )

  const errors = useMemo(() => validate(values, maxFileSize, rules), [values, maxFileSize, rules])
  const metadataRowErrors = useMemo(() => validateMetadataRows(values.metadata), [values.metadata])
  const metadataErrorInputId = useMemo(
    () => firstMetadataErrorInputId(values.metadata, metadataRowErrors),
    [values.metadata, metadataRowErrors],
  )

  return {
    rules,
    maxFileSize,
    virusScanLocked,
    values,
    setValue,
    errors,
    metadataRowErrors,
    metadataErrorInputId,
  }
}

/** The summary has to land the user on an input; this selects the first metadata error input's ID */
function firstMetadataErrorInputId(
  metadata: MetadataEntry[],
  rowErrors: MetadataRowError[],
): string | undefined {
  const index = rowErrors.findIndex((row) => row.key ?? row.value)
  if (index < 0) {
    return undefined
  }
  return metadataInputId(metadata[index].id, rowErrors[index].key ? 'key' : 'value')
}
