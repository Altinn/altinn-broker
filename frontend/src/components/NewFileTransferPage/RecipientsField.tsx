import {
  EXPERIMENTAL_Suggestion as Suggestion,
  Field,
  Label,
  ValidationMessage,
} from '@digdir/designsystemet-react'
import { useMemo } from 'react'
import { formatOrgNumber } from '../../helpers/orgIdentifierHelper'
import { PartyField } from './PartyField'
import type { RecipientRules } from './newFileTransferForm'

type RecipientsFieldProps = {
  id: string
  rules: RecipientRules
  selected: string[]
  error?: string
  disabled?: boolean
  onChange: (recipients: string[]) => void
}

export function RecipientsField({
  id,
  rules,
  selected,
  error,
  disabled,
  onChange,
}: RecipientsFieldProps) {
  const selectedItems = useMemo(
    () =>
      selected.map((organizationNumber) => ({
        value: organizationNumber,
        label:
          rules.options.find((option) => option.organizationNumber === organizationNumber)?.name ??
          formatOrgNumber(organizationNumber),
      })),
    [selected, rules.options],
  )

  if (rules.requiredParty) {
    return (
      <PartyField
        label="Mottaker"
        description={`Tjenesten krever at ${rules.requiredParty.name} er part i formidlingen, så du kan ikke sende til andre.`}
        name={rules.requiredParty.name}
        organizationNumber={rules.requiredParty.organizationNumber}
      />
    )
  }

  return (
    <Field>
      <Label htmlFor={id}>Mottakere</Label>
      <Field.Description>
        Du kan bare sende til organisasjoner som står i tilgangslisten for tjenesten.
      </Field.Description>
      <Suggestion
        multiple
        selected={selectedItems}
        onSelectedChange={(items) => onChange(items.map((item) => item.value))}
      >
        <Suggestion.Input id={id} disabled={disabled} aria-invalid={error ? true : undefined} />
        <Suggestion.Clear aria-label="Fjern alle mottakere" />
        <Suggestion.List>
          <Suggestion.Empty>Ingen mottakere passer søket</Suggestion.Empty>
          {rules.options.map((option) => (
            <Suggestion.Option key={option.organizationNumber} value={option.organizationNumber}>
              {option.name} — {formatOrgNumber(option.organizationNumber)}
            </Suggestion.Option>
          ))}
        </Suggestion.List>
      </Suggestion>
      {error && <ValidationMessage>{error}</ValidationMessage>}
    </Field>
  )
}
