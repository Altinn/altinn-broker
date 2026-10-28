import { useCallback, useEffect, useState } from 'react'
import { toast } from 'react-toastify'
import { ApiError } from '../../api/client'
import {
  configureResource,
  getResourceConfiguration,
  VIRUS_SCAN_MAX_SIZE_BYTES,
  type ConfigureResourceInput,
  type ResourceConfiguration,
} from '../../api/resourceConfiguration'
import { daysToIso8601, hoursToIso8601, timeSpanToDays, timeSpanToHours } from '../../helpers/durationHelper'
import { bytesToGb, gbToBytes } from '../../helpers/fileSizeHelper'
import { toOrgIdentifier, toOrgNumber } from '../../helpers/orgIdentifierHelper'
import { i18n } from '../../i18n/config'
import { localizeApiError, type LocalizedApiError } from '../../i18n/apiErrors'

const MAX_TTL_DAYS = 365
const MAX_GRACE_HOURS = 24
const VIRUS_SCAN_MAX_GB = bytesToGb(VIRUS_SCAN_MAX_SIZE_BYTES)

export function virusScanSizeHint(): string {
  return i18n.t('page.config.virus_hint', { maxGb: VIRUS_SCAN_MAX_GB })
}

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

    const body = toConfigureInput(draft, toDraft(displayed))
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
      toast.success(i18n.t('page.config.saved'))
    } catch (error) {
      setSaveError(describeSaveError(error, resourceId))
    } finally {
      setSaving(false)
    }
  }, [draft, displayed, onBehalfOf, resourceId])

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
    fileTransferTimeToLiveDays: ttlDays === null ? '' : formatDurationInput(ttlDays),
    purgeFileTransferAfterAllRecipientsConfirmed: configuration.purgeFileTransferAfterAllRecipientsConfirmed,
    purgeFileTransferGracePeriodHours: graceHours === null ? '' : formatDurationInput(graceHours),
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
      errors.maxFileTransferSizeGb = i18n.t('page.config.size_positive')
    } else if (draft.virusScanRequired && gb > VIRUS_SCAN_MAX_GB) {
      const hint = virusScanSizeHint()
      errors.maxFileTransferSizeGb = hint
      errors.virusScanRequired = hint
    }
  }

  const ttl = Number(draft.fileTransferTimeToLiveDays.replace(',', '.'))
  if (!Number.isFinite(ttl) || ttl <= 0) {
    errors.fileTransferTimeToLiveDays = i18n.t('page.config.lifetime_positive')
  } else if (ttl > MAX_TTL_DAYS) {
    errors.fileTransferTimeToLiveDays = i18n.t('page.config.lifetime_max', { max: MAX_TTL_DAYS })
  }

  const grace = Number(draft.purgeFileTransferGracePeriodHours.replace(',', '.'))
  if (!Number.isFinite(grace) || grace < 0) {
    errors.purgeFileTransferGracePeriodHours = i18n.t('page.config.grace_non_negative')
  } else if (grace > MAX_GRACE_HOURS) {
    errors.purgeFileTransferGracePeriodHours = i18n.t('page.config.grace_max', { max: MAX_GRACE_HOURS })
  }

  const party = draft.requiredParty.trim()
  if (party !== '' && !toOrgNumber(party)) {
    errors.requiredParty = i18n.t('page.config.party_invalid')
  }

  return errors
}

function toConfigureInput(
  draft: ConfigurationDraft,
  initialDraft: ConfigurationDraft,
): ConfigureResourceInput | null {
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
    approvedForDisabledVirusScan: !draft.virusScanRequired,
  }

  // Omit when unchanged so an untouched unparseable stored identifier is preserved.
  // Empty string clears; the handler skips only when the property is omitted/null.
  if (party !== initialDraft.requiredParty.trim()) {
    input.requiredParty = party === '' ? '' : (toOrgIdentifier(party) ?? '')
  }

  if (maxSize !== '') {
    input.maxFileTransferSize = gbToBytes(Number(maxSize.replace(',', '.')))
  }

  return input
}

function describeSaveError(error: unknown, resourceId: string): LocalizedApiError {
  if (error instanceof ApiError) {
    const localized = localizeApiError(error.body, { resourceId })
    if (localized.errorCode !== undefined || localized.message !== i18n.t('page.errors.generic')) {
      return localized
    }
    return { message: i18n.t('page.errors.save_failed_status', { status: error.status }) }
  }
  return { message: i18n.t('page.errors.save_failed') }
}

function formatGbInput(bytes: number): string {
  const gb = bytesToGb(bytes)
  return Number.isInteger(gb) ? String(gb) : String(Number(gb.toPrecision(6)))
}

/** Avoid long float tails when a TimeSpan maps to a fractional day/hour value. */
function formatDurationInput(value: number): string {
  return Number.isInteger(value) ? String(value) : String(Number(value.toPrecision(6)))
}
