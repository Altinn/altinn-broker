import {
  Button,
  EXPERIMENTAL_FileUpload as FileUpload,
  Field,
  Label,
  ValidationMessage,
} from '@digdir/designsystemet-react'
import { FileIcon, TrashIcon } from '@navikt/aksel-icons'
import { useRef } from 'react'
import { formatFileSize } from '../../helpers/fileSizeHelper'

type UploadFileProps = {
  id: string
  file: File | null
  /** Null when the resource configuration could not be read, so no limit can be shown. */
  maxFileSize: number | null
  error?: string
  disabled?: boolean
  onChange: (file: File | null) => void
}

export function UploadFile({ id, file, maxFileSize, error, disabled, onChange }: UploadFileProps) {
  const inputRef = useRef<HTMLInputElement>(null)

  const clear = () => {
    // The input keeps its own value, so picking the same file again is ignored unless it is reset.
    if (inputRef.current) {
      inputRef.current.value = ''
    }
    onChange(null)
  }

  return (
    <Field>
      <Label htmlFor={id}>Fil</Label>
      <FileUpload>
        <Field.Description>Dra filen hit, eller velg den fra maskinen</Field.Description>
        {maxFileSize !== null && (
          <Field.Description>Maks {formatFileSize(maxFileSize)}</Field.Description>
        )}
        <Button asChild variant="secondary">
          <span>Velg fil</span>
        </Button>
        <input
          ref={inputRef}
          id={id}
          type="file"
          disabled={disabled}
          aria-invalid={error ? true : undefined}
          onChange={(event) => onChange(event.target.files?.[0] ?? null)}
        />
      </FileUpload>

      {file && (
        <div className="new-transfer__file">
          <FileIcon aria-hidden fontSize="1.5rem" />
          <span className="new-transfer__file-name">{file.name}</span>
          <span className="new-transfer__file-size">{formatFileSize(file.size)}</span>
          <Button variant="tertiary" data-color="danger" onClick={clear} disabled={disabled}>
            <TrashIcon aria-hidden />
            Fjern
          </Button>
        </div>
      )}

      {error && <ValidationMessage>{error}</ValidationMessage>}
    </Field>
  )
}
