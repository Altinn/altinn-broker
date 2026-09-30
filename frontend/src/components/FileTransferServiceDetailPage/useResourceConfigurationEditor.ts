import { useCallback, useEffect, useState } from 'react'
import { toast } from 'react-toastify'
import { ApiError } from '../../api/client'
import {
  configureResource,
  getResourceConfiguration,
  type ConfigureResourceInput,
  type ResourceConfiguration,
} from '../../api/resourceConfiguration'
import { daysToIso8601, hoursToIso8601, timeSpanToDays, timeSpanToHours } from '../../helpers/durationHelper'
import { bytesToGb, gbToBytes } from '../../helpers/fileSizeHelper'
import { toOrgIdentifier, toOrgNumber } from '../../helpers/orgIdentifierHelper'
import { localizeApiError, type LocalizedApiError } from '../../i18n/apiErrors'

const MAX_TTL_DAYS = 365
const MAX_GRACE_HOURS = 24

export type ConfigurationDraft = {
  maxFileTransferSizeGb: string
  fileTransferTimeToLiveDays: string
  purgeFileTransferAfterAllRecipientsConfirmed: boolean
  purgeFileTransferGracePeriodHours: string
  requiredParty: string
  /** When true, virus scan is required (approvedForDisabledVirusScan is false). */
  virusScanRequired: boolean
}

export type ConfigurationDraftErrors = Partial<Record<keyof ConfigurationDraft, string>>

type Options = {
  resourceId: string
  configuration: ResourceConfiguration
  /** Organization number the end user acts on behalf of (required for ID-porten PDP). */
  onBehalfOf: string
}

/**
 * Edit toggle, draft values, validation and save against ConfigureResource.
 */
export function useResourceConfigurationEditor({ resourceId, configuration, onBehalfOf }: Options) {
  const [displayed, setDisplayed] = useState(configuration)
  const [editing, setEditing] = useState(false)
  const [draft, setDraft] = useState<ConfigurationDraft>(() => toDraft(configuration))
  const [errors, setErrors] = useState<ConfigurationDraftErrors>({})
  const [saving, setSaving] = useState(false)
  const [saveError, setSaveError] = useState<LocalizedApiError | null>(null)

  useEffect(() => {
    setDisplayed(configuration)
    setDraft(toDraft(configuration))
    setEditing(false)
    setErrors({})
    setSaveError(null)
  }, [configuration])

  const setEditingEnabled = useCallback(
    (enabled: boolean) => {
      setEditing(enabled)
      setSaveError(null)
      setErrors({})
      if (enabled) {
        setDraft(toDraft(displayed))
      }
    },
    [displayed],
  )

  const updateDraft = useCallback(<K extends keyof ConfigurationDraft>(key: K, value: ConfigurationDraft[K]) => {
    setDraft((current) => ({ ...current, [key]: value }))
  }, [])

  const cancel = useCallback(() => {
    setEditing(false)
    setDraft(toDraft(displayed))
    setErrors({})
    setSaveError(null)
  }, [displayed])

  const save = useCallback(async () => {
    const nextErrors = validateDraft(draft)
    setErrors(nextErrors)
    if (Object.keys(nextErrors).length > 0) {
      return
    }

    const body = toConfigureInput(draft)
    if (!body) {
      return
    }

    setSaving(true)
    setSaveError(null)
    try {
      await configureResource(resourceId, body, onBehalfOf)
      const refreshed = await getResourceConfiguration(resourceId)
      setDisplayed(refreshed)
      setDraft(toDraft(refreshed))
      setEditing(false)
      toast.success('Oppsettet er lagret.')
    } catch (error) {
      setSaveError(describeSaveError(error, resourceId))
    } finally {
      setSaving(false)
    }
  }, [draft, onBehalfOf, resourceId])

  return {
    configuration: displayed,
    editing,
    setEditingEnabled,
    draft,
    updateDraft,
    errors,
    saving,
    saveError,
    save,
    cancel,
  }
}

function toDraft(configuration: ResourceConfiguration): ConfigurationDraft {
  const ttlDays = timeSpanToDays(configuration.fileTransferTimeToLive)
  const graceHours = timeSpanToHours(configuration.purgeFileTransferGracePeriod)

  return {
    maxFileTransferSizeGb:
      configuration.maxFileTransferSize === null ? '' : formatGbInput(configuration.maxFileTransferSize),
    fileTransferTimeToLiveDays: ttlDays === null ? '' : String(ttlDays),
    purgeFileTransferAfterAllRecipientsConfirmed: configuration.purgeFileTransferAfterAllRecipientsConfirmed,
    purgeFileTransferGracePeriodHours: graceHours === null ? '' : String(graceHours),
    requiredParty: configuration.requiredParty
      ? (toOrgNumber(configuration.requiredParty) ?? '')
      : '',
    virusScanRequired: !configuration.approvedForDisabledVirusScan,
  }
}

function validateDraft(draft: ConfigurationDraft): ConfigurationDraftErrors {
  const errors: ConfigurationDraftErrors = {}

  const maxSize = draft.maxFileTransferSizeGb.trim()
  if (maxSize !== '') {
    const gb = Number(maxSize.replace(',', '.'))
    if (!Number.isFinite(gb) || gb <= 0) {
      errors.maxFileTransferSizeGb = 'Oppgi en filstørrelse større enn 0 GB.'
    }
  }

  const ttl = Number(draft.fileTransferTimeToLiveDays.replace(',', '.'))
  if (!Number.isInteger(ttl) || ttl <= 0) {
    errors.fileTransferTimeToLiveDays = 'Oppgi levetid som et heltall antall dager.'
  } else if (ttl > MAX_TTL_DAYS) {
    errors.fileTransferTimeToLiveDays = `Levetiden kan ikke overstige ${MAX_TTL_DAYS} dager.`
  }

  const grace = Number(draft.purgeFileTransferGracePeriodHours.replace(',', '.'))
  if (!Number.isInteger(grace) || grace < 0) {
    errors.purgeFileTransferGracePeriodHours = 'Oppgi venteperioden som et heltall antall timer.'
  } else if (grace > MAX_GRACE_HOURS) {
    errors.purgeFileTransferGracePeriodHours = `Venteperioden kan ikke overstige ${MAX_GRACE_HOURS} timer.`
  }

  const party = draft.requiredParty.trim()
  if (party !== '' && !toOrgNumber(party)) {
    errors.requiredParty = 'Velg en gyldig part fra listen.'
  }

  return errors
}

function toConfigureInput(draft: ConfigurationDraft): ConfigureResourceInput | null {
  const errors = validateDraft(draft)
  if (Object.keys(errors).length > 0) {
    return null
  }

  const ttl = Number(draft.fileTransferTimeToLiveDays.replace(',', '.'))
  const grace = Number(draft.purgeFileTransferGracePeriodHours.replace(',', '.'))
  const maxSize = draft.maxFileTransferSizeGb.trim()
  const party = draft.requiredParty.trim()

  const input: ConfigureResourceInput = {
    fileTransferTimeToLive: daysToIso8601(ttl),
    purgeFileTransferAfterAllRecipientsConfirmed: draft.purgeFileTransferAfterAllRecipientsConfirmed,
    purgeFileTransferGracePeriod: hoursToIso8601(grace),
    // Empty string clears; the handler skips only when the property is omitted/null.
    requiredParty: party === '' ? '' : (toOrgIdentifier(party) ?? ''),
    approvedForDisabledVirusScan: !draft.virusScanRequired,
  }

  if (maxSize !== '') {
    input.maxFileTransferSize = gbToBytes(Number(maxSize.replace(',', '.')))
  }

  return input
}

function describeSaveError(error: unknown, resourceId: string): LocalizedApiError {
  if (error instanceof ApiError) {
    const localized = localizeApiError(error.body, { resourceId })
    if (localized.errorCode !== undefined || localized.message !== 'Noe gikk galt. Prøv igjen.') {
      return localized
    }
    return { message: `Lagring feilet (HTTP ${error.status}).` }
  }
  return { message: 'Lagring feilet. Prøv igjen.' }
}

function formatGbInput(bytes: number): string {
  const gb = bytesToGb(bytes)
  return Number.isInteger(gb) ? String(gb) : String(Number(gb.toPrecision(6)))
}
