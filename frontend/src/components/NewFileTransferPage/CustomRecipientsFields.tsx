import {
  Button,
  Field,
  Fieldset,
  Label,
  Select,
  Switch,
  Textfield,
  ValidationMessage,
} from '@digdir/designsystemet-react'
import { PlusIcon, TrashIcon } from '@navikt/aksel-icons'
import { useEffect, useRef } from 'react'
import { formatOrgNumber } from '../../helpers/orgIdentifierHelper'
import { createCustomRecipientEntry, customRecipientInputId, type CustomRecipientEntry } from './formFields'
import type { CustomRecipientRowError } from './formValidation'

export type RelatedOrganizationOption = {
  organizationNumber: string
  name?: string
}

type CustomRecipientsFieldsProps = {
  id: string
  enabled: boolean
  entries: CustomRecipientEntry[]
  relatedOrganizations: RelatedOrganizationOption[]
  rowErrors: CustomRecipientRowError[]
  error?: string
  disabled?: boolean
  onEnabledChange: (enabled: boolean) => void
  onChange: (entries: CustomRecipientEntry[]) => void
}

export function CustomRecipientsFields({
  id,
  enabled,
  entries,
  relatedOrganizations,
  rowErrors,
  error,
  disabled,
  onEnabledChange,
  onChange,
}: CustomRecipientsFieldsProps) {
  const identifierInputs = useRef(new Map<string, HTMLInputElement>())
  const pendingFocus = useRef<string | null>(null)
  const enabledSwitch = useRef<HTMLInputElement>(null)

  useEffect(() => {
    const entryId = pendingFocus.current
    if (entryId) {
      identifierInputs.current.get(entryId)?.focus()
      pendingFocus.current = null
    }
  }, [entries])

  const update = (entryId: string, patch: Partial<CustomRecipientEntry>) => {
    onChange(entries.map((entry) => (entry.id === entryId ? { ...entry, ...patch } : entry)))
  }

  const add = () => {
    const entry = createCustomRecipientEntry(
      relatedOrganizations.length === 1 ? relatedOrganizations[0].organizationNumber : '',
    )
    pendingFocus.current = entry.id
    onChange([...entries, entry])
  }

  const remove = (entryId: string) => {
    identifierInputs.current.delete(entryId)
    const remaining = entries.filter((entry) => entry.id !== entryId)
    onChange(remaining)
    if (remaining.length === 0) {
      onEnabledChange(false)
      enabledSwitch.current?.focus()
    }
  }

  const organizationLabel = (option: RelatedOrganizationOption) =>
    option.name ? `${option.name} (${formatOrgNumber(option.organizationNumber)})` : formatOrgNumber(option.organizationNumber)

  return (
    <>
      <Field>
        <Switch
          ref={enabledSwitch}
          label="Varsle andre i tillegg"
          checked={enabled}
          disabled={disabled}
          onChange={(event) => {
            onEnabledChange(event.target.checked)
            if (event.target.checked && entries.length === 0) {
              add()
            }
          }}
        />
        <Field.Description>
          For eksempel en kontaktperson, som varsles på vegne av en av mottakerne.
        </Field.Description>
      </Field>

      {enabled && (
        <Fieldset id={id} tabIndex={-1}>
          <Fieldset.Legend>Varsle valgfrie mottakere</Fieldset.Legend>
          <Fieldset.Description>
            Oppgi e-postadresse eller mobilnummer, og på vegne av hvilken mottaker
            varselet gjelder.
          </Fieldset.Description>
          {error && entries.length === 0 && <ValidationMessage>{error}</ValidationMessage>}

          {entries.map((entry, index) => {
            const relatedOrganizationNumber = relatedOrganizations.some(
              (option) => option.organizationNumber === entry.relatedOrganizationNumber,
            )
              ? entry.relatedOrganizationNumber
              : ''
            const selectId = customRecipientInputId(entry.id, 'relatedOrganization')
            return (
              <div key={entry.id} className="new-transfer__metadata-row">
                <Textfield
                  ref={(element) => {
                    if (element instanceof HTMLInputElement) {
                      identifierInputs.current.set(entry.id, element)
                    }
                  }}
                  id={customRecipientInputId(entry.id, 'identifier')}
                  label="Hvem skal varsles"
                  value={entry.identifier}
                  error={rowErrors[index]?.identifier}
                  disabled={disabled}
                  onChange={(event) => update(entry.id, { identifier: event.target.value })}
                />
                <Field>
                  <Label htmlFor={selectId}>På vegne av mottaker</Label>
                  <Select
                    id={selectId}
                    value={relatedOrganizationNumber}
                    aria-invalid={rowErrors[index]?.relatedOrganization ? true : undefined}
                    disabled={disabled || relatedOrganizations.length === 0}
                    onChange={(event) => update(entry.id, { relatedOrganizationNumber: event.target.value })}
                  >
                    <Select.Option value="" disabled>
                      {relatedOrganizations.length === 0 ? 'Velg mottakere først' : 'Velg mottaker'}
                    </Select.Option>
                    {relatedOrganizations.map((option) => (
                      <Select.Option key={option.organizationNumber} value={option.organizationNumber}>
                        {organizationLabel(option)}
                      </Select.Option>
                    ))}
                  </Select>
                  {rowErrors[index]?.relatedOrganization && (
                    <ValidationMessage>{rowErrors[index].relatedOrganization}</ValidationMessage>
                  )}
                </Field>
                <Button
                  variant="tertiary"
                  data-color="danger"
                  disabled={disabled}
                  onClick={() => remove(entry.id)}
                >
                  <TrashIcon aria-hidden />
                  <span className="sr-only">Fjern {index + 1}. som skal varsles</span>
                </Button>
              </div>
            )
          })}

          <Button variant="secondary" className="new-transfer__add-metadata" disabled={disabled} onClick={add}>
            <PlusIcon aria-hidden />
            Legg til
          </Button>
        </Fieldset>
      )}
    </>
  )
}
