import { Field, Switch } from '@digdir/designsystemet-react'

type VirusScanFieldProps = {
  checked: boolean
  /** The resource is not approved for transfers without virus scanning. */
  locked: boolean
  onChange: (checked: boolean) => void
}

/**
 * Renders nothing on a resource that is not approved for disabled virus scanning.
 */
export function VirusScanField({ checked, locked, onChange }: VirusScanFieldProps) {
  if (locked) {
    return null
  }

  return (
    <Field>
      <Switch
        label="Virusskann filen"
        checked={checked}
        onChange={(event) => onChange(event.target.checked)}
      />
      <Field.Description>
        Mottakeren kan først laste ned filen etter at den er skannet.
      </Field.Description>
    </Field>
  )
}
