import { List, SettingsItem, SettingsSection } from '@altinn/altinn-components'
import { Alert, Button, Field, Switch, Textfield } from '@digdir/designsystemet-react'
import type { ResourceConfiguration } from '../../api/resourceConfiguration'
import { formatDuration } from '../../helpers/durationHelper'
import { formatFileSize } from '../../helpers/fileSizeHelper'
import { formatOrgNumber } from '../../helpers/orgIdentifierHelper'
import { publishAccessNotice } from '../../i18n/apiErrors'
import {
  useResourceConfigurationEditor,
  type ConfigurationDraft,
  type ConfigurationDraftErrors,
} from './useResourceConfigurationEditor'
import './fileTransferServiceDetailPage.css'

const NOT_SET = '–'
const NO_LIMIT = 'Ingen grense satt'

type ResourceConfigurationListProps = {
  resourceId: string
  configuration: ResourceConfiguration
  onBehalfOf: string
  /** When false, the edit toggle is disabled and an access notice is shown. */
  canPublish: boolean
}

export function ResourceConfigurationList({
  resourceId,
  configuration,
  onBehalfOf,
  canPublish,
}: ResourceConfigurationListProps) {
  const editor = useResourceConfigurationEditor({ resourceId, configuration, onBehalfOf })
  const publishNotice = canPublish ? null : publishAccessNotice(resourceId)

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
            {publishNotice.message}{' '}
            <a
              href={publishNotice.linkHref}
              target="_blank"
              rel="noopener noreferrer"
              className="resource-configuration__error-link"
            >
              {publishNotice.linkLabel}
            </a>
          </Field.Description>
        )}
      </Field>

      {editor.saveError && (
        <Alert data-color="danger" className="resource-configuration__alert">
          <span>{editor.saveError.message}</span>
          {editor.saveError.linkHref && (
            <>
              {' '}
              <a
                href={editor.saveError.linkHref}
                target="_blank"
                rel="noopener noreferrer"
                className="resource-configuration__error-link"
              >
                {editor.saveError.linkLabel ?? editor.saveError.linkHref}
              </a>
            </>
          )}
        </Alert>
      )}

      {editor.editing ? (
        <ConfigurationForm
          draft={editor.draft}
          errors={editor.errors}
          virusScanRequired={!editor.configuration.approvedForDisabledVirusScan}
          saving={editor.saving}
          onChange={editor.updateDraft}
          onSave={editor.save}
          onCancel={editor.cancel}
        />
      ) : (
        <SettingsSection>
          <List size="sm">
            {configurationFields(editor.configuration).map((field) => (
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
  virusScanRequired: boolean
  saving: boolean
  onChange: <K extends keyof ConfigurationDraft>(key: K, value: ConfigurationDraft[K]) => void
  onSave: () => void
  onCancel: () => void
}

function ConfigurationForm({
  draft,
  errors,
  virusScanRequired,
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
      <Textfield
        label="Påkrevd part"
        description="Organisasjonsnummer. La stå tomt for ingen påkrevd part."
        value={draft.requiredParty}
        onChange={(event) => onChange('requiredParty', event.target.value)}
        error={errors.requiredParty}
        disabled={saving}
      />
      <Textfield
        label="Virusskanning påkrevd"
        value={yesNo(virusScanRequired)}
        readOnly
        disabled
      />
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

function configurationFields(configuration: ResourceConfiguration) {
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
      value: requiredParty(configuration.requiredParty),
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

function requiredParty(identifier: string | null): string {
  return identifier ? formatOrgNumber(identifier) : NOT_SET
}
