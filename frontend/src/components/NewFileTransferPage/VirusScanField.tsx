import { Field, Switch } from '@digdir/designsystemet-react'

type VirusScanFieldProps = {
  checked: boolean
  /** The resource is not approved for transfers without virus scanning. */
  locked: boolean
  disabled?: boolean
  onChange: (checked: boolean) => void
}

export function VirusScanField({ checked, locked, disabled, onChange }: VirusScanFieldProps) {
  return (
    <Field>
      <Switch
        label="Virusskann filen"
        checked={checked}
        disabled={disabled || locked}
        onChange={(event) => onChange(event.target.checked)}
      />
      <Field.Description>
        {locked
          ? 'Tjenesten er ikke godkjent for formidlinger uten virusskanning, så skanning er påkrevd.'
          : 'Mottakeren kan først laste ned filen etter at den er skannet.'}
      </Field.Description>
    </Field>
  )
}
