import {
  EXPERIMENTAL_Suggestion as Suggestion,
  Field,
  Label,
  ValidationMessage,
} from '@digdir/designsystemet-react'
import { useMemo } from 'react'
import { useTranslation } from 'react-i18next'
import { formatOrgNumber } from '../../helpers/orgIdentifierHelper'
import { PartyField } from './PartyField'
import type { RecipientRules } from './recipientRules'

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
  const { t } = useTranslation()
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
        label={t('page.common.recipient')}
        description={t('page.recipients.required', { name: rules.requiredParty.name })}
        name={rules.requiredParty.name}
        organizationNumber={rules.requiredParty.organizationNumber}
      />
    )
  }

  return (
    <Field>
      <Label htmlFor={id}>{t('page.common.recipients')}</Label>
      <Field.Description>{t('page.recipients.description')}</Field.Description>
      <Suggestion
        multiple
        selected={selectedItems}
        onSelectedChange={(items) => onChange(items.map((item) => item.value))}
      >
        <Suggestion.Input id={id} disabled={disabled} aria-invalid={error ? true : undefined} />
        <Suggestion.Clear aria-label={t('page.recipients.clear')} />
        <Suggestion.List>
          <Suggestion.Empty>{t('page.recipients.empty')}</Suggestion.Empty>
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
