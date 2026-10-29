import { useCallback, useEffect, useMemo, useState } from 'react'
import { i18n } from '../../i18n/config'
import { getAllowedRecipients, type AllowedRecipient } from '../../api/allowedRecipients'
import {
  effectiveMaxFileTransferSize,
  getResourceConfiguration,
  type ResourceConfiguration,
} from '../../api/resourceConfiguration'
import {
  customRecipientInputId,
  emptyValues,
  metadataInputId,
  type CustomRecipientEntry,
  type MetadataEntry,
  type NewFileTransferValues,
} from './formFields'
import { draftKey, readDraft, writeDraft } from './draftStore'
import {
  customRecipientsInUse,
  validate,
  validateCustomRecipientRows,
  validateMetadataRows,
  type CustomRecipientRowError,
  type MetadataRowError,
} from './formValidation'
import { resolveRecipientRules } from './recipientRules'
import { useFileTransferUpload } from './useFileTransferUpload'

type Options = {
  resourceId: string
  senderOrgNumber: string
}

type LoadedResource = {
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
    customRecipientRowErrors: upload.submitAttempts > 0 ? form.customRecipientRowErrors : [],
  }
}

/** What the resource allows, and who may receive on it. */
function useResourceContext(resourceId: string, senderOrgNumber: string) {
  const [loaded, setLoaded] = useState<LoadedResource | null>(null)

  useEffect(() => {
    if (!senderOrgNumber) {
      return
    }

    let cancelled = false

    Promise.all([
      getResourceConfiguration(resourceId),
      getAllowedRecipients(resourceId, senderOrgNumber),
    ])
      .then(([configuration, recipients]) => ({ configuration, recipients, error: '' }))
      .catch(() => ({
        configuration: null,
        recipients: [],
        error: i18n.t('page.service.settings_error'),
      }))
      .then((result) => {
        if (!cancelled) {
          setLoaded(result)
        }
      })

    return () => {
      cancelled = true
    }
  }, [resourceId, senderOrgNumber])

  const recipients = useMemo(() => loaded?.recipients ?? [], [loaded])

  return {
    configuration: loaded?.configuration ?? null,
    recipients,
    loading: loaded === null,
    loadError: loaded?.error ?? '',
  }
}

/** The draft the user is editing, narrowed to what the API accepts, and its validation errors. */
function useFormValues(
  configuration: ResourceConfiguration | null,
  recipients: AllowedRecipient[],
  senderOrgNumber: string,
  key: string,
) {
  const [draft, setDraft] = useState(() => readDraft(key) ?? emptyValues())

  useEffect(() => {
    writeDraft(key, draft)
  }, [key, draft])

  const rules = useMemo(
    () => resolveRecipientRules(recipients, configuration?.requiredParty ?? null, senderOrgNumber),
    [recipients, configuration, senderOrgNumber],
  )
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
  const maxFileSize = effectiveMaxFileTransferSize(
    configuration?.maxFileTransferSize ?? null,
    values.virusScan,
  )

  const setValue = useCallback(
    <K extends keyof NewFileTransferValues>(key: K, value: NewFileTransferValues[K]) => {
      setDraft((current) => ({ ...current, [key]: value }))
    },
    [],
  )

  const language = i18n.language
  const errors = useMemo(() => {
    void language
    return validate(values, maxFileSize, rules)
  }, [values, maxFileSize, rules, language])
  const metadataRowErrors = useMemo(() => {
    void language
    return validateMetadataRows(values.metadata)
  }, [values.metadata, language])
  const metadataErrorInputId = useMemo(
    () => firstMetadataErrorInputId(values.metadata, metadataRowErrors),
    [values.metadata, metadataRowErrors],
  )
  const customRecipientRowErrors = useMemo(() => {
    void language
    return customRecipientsInUse(values)
      ? validateCustomRecipientRows(values.customRecipients, values.recipients)
      : []
  }, [values, language])
  const customRecipientErrorInputId = useMemo(
    () => firstCustomRecipientErrorInputId(values.customRecipients, customRecipientRowErrors),
    [values.customRecipients, customRecipientRowErrors],
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
    customRecipientRowErrors,
    customRecipientErrorInputId,
  }
}

/** The summary has to land the user on an input; this selects the first custom recipient error input's ID */
function firstCustomRecipientErrorInputId(
  entries: CustomRecipientEntry[],
  rowErrors: CustomRecipientRowError[],
): string | undefined {
  const index = rowErrors.findIndex((row) => row.identifier ?? row.relatedOrganization)
  if (index < 0) {
    return undefined
  }
  return customRecipientInputId(entries[index].id, rowErrors[index].identifier ? 'identifier' : 'relatedOrganization')
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
