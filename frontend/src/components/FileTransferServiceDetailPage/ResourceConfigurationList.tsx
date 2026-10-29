import { List, SettingsItem, SettingsSection } from '@altinn/altinn-components'
import { Alert, Button, Field, Label, Select, Switch, Textfield, ValidationMessage } from '@digdir/designsystemet-react'
import { useMemo } from 'react'
import { useTranslation } from 'react-i18next'
import type { AllowedRecipient } from '../../api/allowedRecipients'
import type { ResourceConfiguration } from '../../api/resourceConfiguration'
import { formatDuration } from '../../helpers/durationHelper'
import { formatFileSize } from '../../helpers/fileSizeHelper'
import {
  formatOrganizationDisplay,
  formatOrgNumber,
  toOrgNumber,
} from '../../helpers/orgIdentifierHelper'
import {
  configureAccessNotice,
  messageWithOptionalInlineLink,
  type LocalizedApiError,
} from '../../i18n/apiErrors'
import { i18n } from '../../i18n/config'
import {
  useResourceConfigurationEditor,
  virusScanSizeHint,
  type ConfigurationDraft,
  type ConfigurationDraftErrors,
} from './useResourceConfigurationEditor'
import './fileTransferServiceDetailPage.css'

const NO_REQUIRED_PARTY = ''

function LocalizedErrorText({ error }: { error: LocalizedApiError }) {
  const parts = messageWithOptionalInlineLink(error)
  if (!parts.link) {
    return <span>{parts.before}</span>
  }

  return (
    <span>
      {parts.before}
      <a
        href={parts.link.href}
        target="_blank"
        rel="noopener noreferrer"
        className="resource-configuration__error-link"
      >
        {parts.link.label}
      </a>
      {parts.after}
    </span>
  )
}

type ResourceConfigurationListProps = {
  resourceId: string
  configuration: ResourceConfiguration
  onBehalfOf: string
  /** When false, the edit toggle is disabled and an access notice is shown. */
  canPublish: boolean
  /** Used to choose between service-owner vs gatekeeper access notices. */
  isServiceOwner: boolean
  sender: AllowedRecipient
  /**
   * Access-list parties (unfiltered by required party). Transfer-time allowed-recipients is too
   * narrow when a required party is already set.
   */
  accessListCandidates: AllowedRecipient[]
}

export function ResourceConfigurationList({
  resourceId,
  configuration,
  onBehalfOf,
  canPublish,
  isServiceOwner,
  sender,
  accessListCandidates,
}: ResourceConfigurationListProps) {
  const { t } = useTranslation()
  const editor = useResourceConfigurationEditor({ resourceId, configuration, onBehalfOf })
  const publishNotice = canPublish ? null : configureAccessNotice(isServiceOwner)
  const partyOptions = useMemo(
    () =>
      buildRequiredPartyOptions(sender, accessListCandidates, editor.configuration.requiredParty),
    [sender, accessListCandidates, editor.configuration.requiredParty],
  )

  return (
    <div className="resource-configuration">
      <Field className="resource-configuration__edit-toggle">
        <Switch
          label={t('page.config.edit')}
          checked={editor.editing}
          onChange={(event) => {
            if (event.target.checked) {
              editor.setEditingEnabled(true)
            } else {
              editor.cancel()
            }
          }}
          disabled={!canPublish || editor.saving}
        />
        {publishNotice && (
          <Field.Description>
            <LocalizedErrorText error={publishNotice} />
          </Field.Description>
        )}
      </Field>

      {editor.saveError && (
        <Alert data-color="danger" className="resource-configuration__alert">
          <LocalizedErrorText error={editor.saveError} />
        </Alert>
      )}

      {editor.editing ? (
        <ConfigurationForm
          draft={editor.draft}
          errors={editor.errors}
          partyOptions={partyOptions}
          saving={editor.saving}
          onChange={editor.updateDraft}
          onSave={editor.save}
          onCancel={editor.cancel}
        />
      ) : (
        <SettingsSection>
          <List size="sm">
            {configurationFields(editor.configuration, partyOptions).map((field) => (
              <SettingsItem key={field.label} id={field.label} title={field.label} value={field.value} />
            ))}
          </List>
        </SettingsSection>
      )}
    </div>
  )
}

type ConfigurationFormProps = {
  draft: ConfigurationDraft
  errors: ConfigurationDraftErrors
  partyOptions: AllowedRecipient[]
  saving: boolean
  onChange: <K extends keyof ConfigurationDraft>(key: K, value: ConfigurationDraft[K]) => void
  onSave: () => void
  onCancel: () => void
}

function ConfigurationForm({
  draft,
  errors,
  partyOptions,
  saving,
  onChange,
  onSave,
  onCancel,
}: ConfigurationFormProps) {
  const { t } = useTranslation()
  const hint = virusScanSizeHint()
  return (
    <form
      className="resource-configuration__form"
      onSubmit={(event) => {
        event.preventDefault()
        onSave()
      }}
    >
      <Textfield
        label={t('page.config.max_size_gb')}
        description={t('page.config.max_size_description', { hint })}
        value={draft.maxFileTransferSizeGb}
        onChange={(event) => onChange('maxFileTransferSizeGb', event.target.value)}
        error={errors.maxFileTransferSizeGb}
        inputMode="decimal"
        disabled={saving}
      />
      <Textfield
        label={t('page.config.lifetime_days')}
        value={draft.fileTransferTimeToLiveDays}
        onChange={(event) => onChange('fileTransferTimeToLiveDays', event.target.value)}
        error={errors.fileTransferTimeToLiveDays}
        inputMode="decimal"
        disabled={saving}
      />
      <Field>
        <Switch
          label={t('page.config.purge_when_confirmed')}
          checked={draft.purgeFileTransferAfterAllRecipientsConfirmed}
          onChange={(event) => onChange('purgeFileTransferAfterAllRecipientsConfirmed', event.target.checked)}
          disabled={saving}
        />
      </Field>
      <Textfield
        label={t('page.config.grace_hours')}
        value={draft.purgeFileTransferGracePeriodHours}
        onChange={(event) => onChange('purgeFileTransferGracePeriodHours', event.target.value)}
        error={errors.purgeFileTransferGracePeriodHours}
        inputMode="decimal"
        disabled={saving}
      />
      <Field>
        <Label htmlFor="required-party">{t('page.config.required_party')}</Label>
        <Field.Description>{t('page.config.required_party_description')}</Field.Description>
        <Select
          id="required-party"
          value={draft.requiredParty}
          onChange={(event) => onChange('requiredParty', event.target.value)}
          disabled={saving}
          aria-invalid={errors.requiredParty ? true : undefined}
        >
          <Select.Option value={NO_REQUIRED_PARTY}>{t('page.config.no_required_party')}</Select.Option>
          {partyOptions.map((party) => (
            <Select.Option key={party.organizationNumber} value={party.organizationNumber}>
              {formatOrganizationDisplay(party.name, party.organizationNumber)}
            </Select.Option>
          ))}
        </Select>
        {errors.requiredParty && <ValidationMessage>{errors.requiredParty}</ValidationMessage>}
      </Field>
      <Field>
        <Switch
          label={t('page.config.virus_required')}
          checked={draft.virusScanRequired}
          onChange={(event) => onChange('virusScanRequired', event.target.checked)}
          disabled={saving}
          aria-invalid={errors.virusScanRequired ? true : undefined}
        />
        <Field.Description>{hint}</Field.Description>
        {errors.virusScanRequired && <ValidationMessage>{errors.virusScanRequired}</ValidationMessage>}
      </Field>
      <div className="resource-configuration__actions">
        <Button type="submit" disabled={saving}>
          {saving ? t('page.common.saving') : t('page.common.save')}
        </Button>
        <Button type="button" variant="secondary" onClick={onCancel} disabled={saving}>
          {t('page.common.cancel')}
        </Button>
      </div>
    </form>
  )
}

/** Sender first, then access-list candidates; keeps a configured party that is not in either list. */
function buildRequiredPartyOptions(
  sender: AllowedRecipient,
  accessListCandidates: AllowedRecipient[],
  requiredParty: string | null,
): AllowedRecipient[] {
  const options: AllowedRecipient[] = [
    { name: sender.name, organizationNumber: sender.organizationNumber },
  ]
  const seen = new Set([sender.organizationNumber])

  for (const candidate of accessListCandidates) {
    if (seen.has(candidate.organizationNumber)) {
      continue
    }
    seen.add(candidate.organizationNumber)
    options.push(candidate)
  }

  const configured = requiredParty ? toOrgNumber(requiredParty) : null
  if (configured && !seen.has(configured)) {
    options.push({ name: formatOrgNumber(configured), organizationNumber: configured })
  }

  return options
}

function configurationFields(configuration: ResourceConfiguration, partyOptions: AllowedRecipient[]) {
  return [
    {
      label: i18n.t('page.config.max_size'),
      value:
        configuration.maxFileTransferSize === null
          ? i18n.t('page.config.no_limit')
          : formatFileSize(configuration.maxFileTransferSize),
    },
    {
      label: i18n.t('page.config.lifetime'),
      value: formatDuration(configuration.fileTransferTimeToLive),
    },
    {
      label: i18n.t('page.config.purge_when_confirmed'),
      value: yesNo(configuration.purgeFileTransferAfterAllRecipientsConfirmed),
    },
    {
      label: i18n.t('page.config.grace'),
      value: formatDuration(configuration.purgeFileTransferGracePeriod),
    },
    {
      label: i18n.t('page.config.required_party'),
      value: requiredPartyDisplay(configuration.requiredParty, partyOptions),
    },
    {
      label: i18n.t('page.config.virus_required'),
      value: yesNo(!configuration.approvedForDisabledVirusScan),
    },
  ]
}

function yesNo(value: boolean): string {
  return value ? i18n.t('page.common.yes') : i18n.t('page.common.no')
}

function requiredPartyDisplay(identifier: string | null, partyOptions: AllowedRecipient[]): string {
  if (!identifier) {
    return i18n.t('page.common.not_set')
  }

  const digits = toOrgNumber(identifier)
  const match = digits
    ? partyOptions.find((party) => party.organizationNumber === digits)
    : undefined
  return match
    ? formatOrganizationDisplay(match.name, match.organizationNumber)
    : formatOrgNumber(identifier)
}
