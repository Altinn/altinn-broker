import { Button, Fieldset, Textfield } from '@digdir/designsystemet-react'
import { PlusIcon, TrashIcon } from '@navikt/aksel-icons'
import { useEffect, useRef } from 'react'
import { useTranslation } from 'react-i18next'
import { createMetadataEntry, metadataInputId, type MetadataEntry } from './formFields'
import {
  MAX_METADATA_ENTRIES,
  MAX_METADATA_KEY_LENGTH,
  MAX_METADATA_VALUE_LENGTH,
  type MetadataRowError,
} from './formValidation'

type MetadataFieldsProps = {
  id: string
  entries: MetadataEntry[]
  rowErrors: MetadataRowError[]
  disabled?: boolean
  onChange: (entries: MetadataEntry[]) => void
}

export function MetadataFields({
  id,
  entries,
  rowErrors,
  disabled,
  onChange,
}: MetadataFieldsProps) {
  const { t } = useTranslation()
  const keyInputs = useRef(new Map<string, HTMLInputElement>())
  const pendingFocus = useRef<string | null>(null)

  // A row appears on demand, so focus has to follow it for anyone not using a mouse.
  useEffect(() => {
    const entryId = pendingFocus.current
    if (entryId) {
      keyInputs.current.get(entryId)?.focus()
      pendingFocus.current = null
    }
  }, [entries])

  const update = (entryId: string, patch: Partial<MetadataEntry>) => {
    onChange(entries.map((entry) => (entry.id === entryId ? { ...entry, ...patch } : entry)))
  }

  const add = () => {
    const entry = createMetadataEntry()
    pendingFocus.current = entry.id
    onChange([...entries, entry])
  }

  const remove = (entryId: string) => {
    keyInputs.current.delete(entryId)
    onChange(entries.filter((entry) => entry.id !== entryId))
  }

  return (
    <Fieldset id={id} tabIndex={-1}>
      <Fieldset.Legend>{t('page.metadata.legend')}</Fieldset.Legend>
      <Fieldset.Description>
        {t('page.metadata.description', { max: MAX_METADATA_ENTRIES })}
      </Fieldset.Description>

      {entries.map((entry, index) => (
        <div key={entry.id} className="new-transfer__metadata-row">
          <Textfield
            ref={(element) => {
              if (element instanceof HTMLInputElement) {
                keyInputs.current.set(entry.id, element)
              }
            }}
            id={metadataInputId(entry.id, 'key')}
            label={t('page.metadata.key')}
            value={entry.key}
            error={rowErrors[index]?.key}
            disabled={disabled}
            maxLength={MAX_METADATA_KEY_LENGTH}
            onChange={(event) => update(entry.id, { key: event.target.value })}
          />
          <Textfield
            id={metadataInputId(entry.id, 'value')}
            label={t('page.metadata.value')}
            value={entry.value}
            error={rowErrors[index]?.value}
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
            <span className="sr-only">{t('page.metadata.remove', { index: index + 1 })}</span>
          </Button>
        </div>
      ))}

      <Button
        variant="secondary"
        className="new-transfer__add-metadata"
        disabled={disabled || entries.length >= MAX_METADATA_ENTRIES}
        onClick={add}
      >
        <PlusIcon aria-hidden />
        {t('page.metadata.add')}
      </Button>
    </Fieldset>
  )
}
