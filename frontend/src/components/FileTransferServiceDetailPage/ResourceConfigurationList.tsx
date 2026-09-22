import { List, SettingsItem, SettingsSection } from '@altinn/altinn-components'
import type { ResourceConfiguration } from '../../api/resourceConfiguration'
import { formatDuration } from '../../helpers/durationHelper'
import { formatFileSize } from '../../helpers/fileSizeHelper'
import { formatOrgNumber } from '../../helpers/orgIdentifierHelper'
import './fileTransferServiceDetailPage.css'

const NOT_SET = '–'
const NO_LIMIT = 'Ingen grense satt'

type ResourceConfigurationListProps = {
  configuration: ResourceConfiguration
}

export function ResourceConfigurationList({ configuration }: ResourceConfigurationListProps) {
  return (
    <SettingsSection>
      <List size="sm">
        {configurationFields(configuration).map((field) => (
          <SettingsItem key={field.label} id={field.label} title={field.label} value={field.value} />
        ))}
      </List>
    </SettingsSection>
  )
}

function configurationFields(configuration: ResourceConfiguration) {
  return [
    {
      label: 'Maks filstørrelse',
      value: configuration.maxFileTransferSize === null
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
