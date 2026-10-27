import { Alert, Button, Heading, Paragraph } from '@digdir/designsystemet-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { formatFileSize } from '../../helpers/fileSizeHelper'
import { i18n } from '../../i18n/config'
import { useUploadProgress, type ActiveUpload } from '../../upload/uploadsContext'
import { CancelUploadConfirmation } from './CancelUploadConfirmation'

type BlockingUploadNoticeProps = {
  upload: ActiveUpload
  onContinue: () => void
  onCancel: () => void
}

export function BlockingUploadNotice({ upload, onContinue, onCancel }: BlockingUploadNoticeProps) {
  const { t } = useTranslation()
  const [confirming, setConfirming] = useState(false)
  const percent = useUploadProgress()?.percent ?? null

  // While finishing, the server may already have accepted the join, which a cancel can't undo.
  const cancellable = upload.status !== 'finishing'
  if (confirming && !cancellable) {
    setConfirming(false)
  }

  return (
    <Alert data-color="info" className="new-transfer__notice">
      <Heading level={3} data-size="2xs">
        {heading(upload)}
      </Heading>
      <Paragraph data-size="sm">
        {t('page.blocking.body', {
          name: upload.fileName,
          size: formatFileSize(upload.fileSize),
          progress: progressText(upload, percent),
        })}
      </Paragraph>
      {confirming ? (
        <CancelUploadConfirmation onConfirm={onCancel} onDismiss={() => setConfirming(false)} />
      ) : (
        <div className="new-transfer__resume-actions">
          <Button type="button" variant="secondary" data-size="sm" onClick={onContinue}>
            {t('page.blocking.go')}
          </Button>
          {cancellable && (
            <Button
              type="button"
              variant="tertiary"
              data-size="sm"
              onClick={() => setConfirming(true)}
            >
              {t('page.blocking.cancel')}
            </Button>
          )}
        </div>
      )}
    </Alert>
  )
}

function progressText(upload: ActiveUpload, percent: number | null): string {
  if (percent !== null) {
    return i18n.t('page.blocking.percent', { percent })
  }
  return upload.status === 'initializing'
    ? i18n.t('page.blocking.not_started')
    : i18n.t('page.blocking.incomplete')
}

function heading(upload: ActiveUpload): string {
  switch (upload.status) {
    case 'failed':
      return i18n.t('page.blocking.failed')
    case 'pausing':
    case 'paused':
      return i18n.t('page.blocking.paused')
    default:
      return i18n.t('page.blocking.active')
  }
}
