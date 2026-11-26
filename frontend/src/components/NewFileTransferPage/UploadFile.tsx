import {
  Button,
  EXPERIMENTAL_FileUpload as FileUpload,
  Field,
  Label,
  ValidationMessage,
} from '@digdir/designsystemet-react'
import { FileIcon, TrashIcon } from '@navikt/aksel-icons'
import { useRef } from 'react'
import { useTranslation } from 'react-i18next'
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
  const { t } = useTranslation()
 
  return (
    <Field>
      <Label htmlFor={file ? undefined : id}>{t('page.upload.file')}</Label>

      {file ? (
        <div className="new-transfer__file">
          <FileIcon aria-hidden fontSize="1.5rem" />
          <span className="new-transfer__file-name">{file.name}</span>
          <span className="new-transfer__file-size">{formatFileSize(file.size)}</span>
          <Button variant="tertiary" data-color="danger" onClick={() => onChange(null)} disabled={disabled}>
            <TrashIcon aria-hidden />
            {t('page.upload.remove')}
          </Button>
        </div>
      ) : (
        <FileUpload>
          <Field.Description>{t('page.upload.drop')}</Field.Description>
          {maxFileSize !== null && (
            <Field.Description>{t('page.upload.max', { size: formatFileSize(maxFileSize) })}</Field.Description>
          )}
          <Button asChild variant="secondary">
            <span>{t('page.upload.choose')}</span>
          </Button>
          <input
            id={id}
            type="file"
            disabled={disabled}
            aria-invalid={error ? true : undefined}
            onChange={(event) => onChange(event.target.files?.[0] ?? null)}
          />
        </FileUpload>
      )}

      {error && <ValidationMessage>{error}</ValidationMessage>}
    </Field>
  )
}
