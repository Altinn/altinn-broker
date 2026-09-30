import { List, SettingsItem, SettingsSection } from '@altinn/altinn-components'
import { Alert, Button, Field, Label, Select, Switch, Textfield, ValidationMessage } from '@digdir/designsystemet-react'
import { useMemo } from 'react'
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
import {
  useResourceConfigurationEditor,
  type ConfigurationDraft,
  type ConfigurationDraftErrors,
} from './useResourceConfigurationEditor'
import './fileTransferServiceDetailPage.css'

const NOT_SET = '–'
const NO_LIMIT = 'Ingen grense satt'
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
  recipients: AllowedRecipient[]
}

export function ResourceConfigurationList({
  resourceId,
  configuration,
  onBehalfOf,
  canPublish,
  isServiceOwner,
  sender,
  recipients,
}: ResourceConfigurationListProps) {
  const editor = useResourceConfigurationEditor({ resourceId, configuration, onBehalfOf })
  const publishNotice = canPublish ? null : configureAccessNotice(isServiceOwner)
  const partyOptions = useMemo(
    () => buildRequiredPartyOptions(sender, recipients, editor.configuration.requiredParty),
    [sender, recipients, editor.configuration.requiredParty],
  )

  return (
    <div className="resource-configuration">
      <Field className="resource-configuration__edit-toggle">
        <Switch
          label="Rediger oppsett"
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
  return (
    <form
      className="resource-configuration__form"
      onSubmit={(event) => {
        event.preventDefault()
        onSave()
      }}
    >
      <Textfield
        label="Maks filstørrelse (GB)"
        description="La feltet stå tomt når ingen grense skal endres."
        value={draft.maxFileTransferSizeGb}
        onChange={(event) => onChange('maxFileTransferSizeGb', event.target.value)}
        error={errors.maxFileTransferSizeGb}
        inputMode="decimal"
        disabled={saving}
      />
      <Textfield
        label="Levetid for formidling (dager)"
        value={draft.fileTransferTimeToLiveDays}
        onChange={(event) => onChange('fileTransferTimeToLiveDays', event.target.value)}
        error={errors.fileTransferTimeToLiveDays}
        inputMode="numeric"
        disabled={saving}
      />
      <Field>
        <Switch
          label="Slettes når alle mottakere har bekreftet"
          checked={draft.purgeFileTransferAfterAllRecipientsConfirmed}
          onChange={(event) => onChange('purgeFileTransferAfterAllRecipientsConfirmed', event.target.checked)}
          disabled={saving}
        />
      </Field>
      <Textfield
        label="Venteperiode før sletting (timer)"
        value={draft.purgeFileTransferGracePeriodHours}
        onChange={(event) => onChange('purgeFileTransferGracePeriodHours', event.target.value)}
        error={errors.purgeFileTransferGracePeriodHours}
        inputMode="numeric"
        disabled={saving}
      />
      <Field>
        <Label htmlFor="required-party">Påkrevd part</Label>
        <Field.Description>
          Velg avsender eller en mottaker. La stå uten valg for ingen påkrevd part.
        </Field.Description>
        <Select
          id="required-party"
          value={draft.requiredParty}
          onChange={(event) => onChange('requiredParty', event.target.value)}
          disabled={saving}
          aria-invalid={errors.requiredParty ? true : undefined}
        >
          <Select.Option value={NO_REQUIRED_PARTY}>Ingen påkrevd part</Select.Option>
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
          label="Virusskanning påkrevd"
          checked={draft.virusScanRequired}
          onChange={(event) => onChange('virusScanRequired', event.target.checked)}
          disabled={saving}
        />
      </Field>
      <div className="resource-configuration__actions">
        <Button type="submit" disabled={saving}>
          {saving ? 'Lagrer…' : 'Lagre'}
        </Button>
        <Button type="button" variant="secondary" onClick={onCancel} disabled={saving}>
          Avbryt
        </Button>
      </div>
    </form>
  )
}

/** Sender first, then recipients; keeps a configured party that is not in either list. */
function buildRequiredPartyOptions(
  sender: AllowedRecipient,
  recipients: AllowedRecipient[],
  requiredParty: string | null,
): AllowedRecipient[] {
  const options: AllowedRecipient[] = [
    { name: sender.name, organizationNumber: sender.organizationNumber },
  ]
  const seen = new Set([sender.organizationNumber])

  for (const recipient of recipients) {
    if (seen.has(recipient.organizationNumber)) {
      continue
    }
    seen.add(recipient.organizationNumber)
    options.push(recipient)
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
      label: 'Maks filstørrelse',
      value:
        configuration.maxFileTransferSize === null
          ? NO_LIMIT
          : formatFileSize(configuration.maxFileTransferSize),
    },
    {
      label: 'Levetid for formidling',
      value: formatDuration(configuration.fileTransferTimeToLive),
    },
    {
      label: 'Slettes når alle mottakere har bekreftet',
      value: yesNo(configuration.purgeFileTransferAfterAllRecipientsConfirmed),
    },
    {
      label: 'Venteperiode før sletting',
      value: formatDuration(configuration.purgeFileTransferGracePeriod),
    },
    {
      label: 'Påkrevd part',
      value: requiredPartyDisplay(configuration.requiredParty, partyOptions),
    },
    {
      label: 'Virusskanning påkrevd',
      value: yesNo(!configuration.approvedForDisabledVirusScan),
    },
  ]
}

function yesNo(value: boolean): string {
  return value ? 'Ja' : 'Nei'
}

function requiredPartyDisplay(identifier: string | null, partyOptions: AllowedRecipient[]): string {
  if (!identifier) {
    return NOT_SET
  }

  const digits = toOrgNumber(identifier)
  const match = digits
    ? partyOptions.find((party) => party.organizationNumber === digits)
    : undefined
  return match
    ? formatOrganizationDisplay(match.name, match.organizationNumber)
    : formatOrgNumber(identifier)
}
