import { Button, Fieldset, Textfield, ValidationMessage } from '@digdir/designsystemet-react'
import { PlusIcon, TrashIcon } from '@navikt/aksel-icons'
import { createMetadataEntry, type MetadataEntry } from './formFields'
import {
  MAX_METADATA_ENTRIES,
  MAX_METADATA_KEY_LENGTH,
  MAX_METADATA_VALUE_LENGTH,
} from './formValidation'

type MetadataFieldsProps = {
  id: string
  entries: MetadataEntry[]
  error?: string
  disabled?: boolean
  onChange: (entries: MetadataEntry[]) => void
}

export function MetadataFields({ id, entries, error, disabled, onChange }: MetadataFieldsProps) {
  const update = (entryId: string, patch: Partial<MetadataEntry>) => {
    onChange(entries.map((entry) => (entry.id === entryId ? { ...entry, ...patch } : entry)))
  }

  const remove = (entryId: string) => {
    const remaining = entries.filter((entry) => entry.id !== entryId)
    onChange(remaining.length > 0 ? remaining : [createMetadataEntry()])
  }

  return (
    <Fieldset id={id} tabIndex={-1}>
      <Fieldset.Legend>Andre metadata</Fieldset.Legend>
      <Fieldset.Description>
        Valgfrie nøkkel/verdi-par som følger formidlingen. Maks {MAX_METADATA_ENTRIES} par.
      </Fieldset.Description>

      {entries.map((entry, index) => (
        <div key={entry.id} className="new-transfer__metadata-row">
          <Textfield
            label="Nøkkel"
            value={entry.key}
            disabled={disabled}
            maxLength={MAX_METADATA_KEY_LENGTH}
            onChange={(event) => update(entry.id, { key: event.target.value })}
          />
          <Textfield
            label="Verdi"
            value={entry.value}
            disabled={disabled}
            maxLength={MAX_METADATA_VALUE_LENGTH}
            onChange={(event) => update(entry.id, { value: event.target.value })}
          />
          <Button
            variant="tertiary"
            data-color="danger"
            disabled={disabled}
            onClick={() => remove(entry.id)}
          >
            <TrashIcon aria-hidden />
            <span className="sr-only">Fjern metadata {index + 1}</span>
          </Button>
        </div>
      ))}

      <Button
        variant="secondary"
        className="new-transfer__add-metadata"
        disabled={disabled || entries.length >= MAX_METADATA_ENTRIES}
        onClick={() => onChange([...entries, createMetadataEntry()])}
      >
        <PlusIcon aria-hidden />
        Legg til metadata
      </Button>

      {error && <ValidationMessage>{error}</ValidationMessage>}
    </Fieldset>
  )
}
