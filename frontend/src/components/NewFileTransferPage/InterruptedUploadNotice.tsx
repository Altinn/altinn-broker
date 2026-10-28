import { Alert, Heading, Paragraph } from '@digdir/designsystemet-react'
import { useTranslation } from 'react-i18next'
import type { FileFingerprint } from '../../upload/uploadSession'
import { formatFileSize } from '../../helpers/fileSizeHelper'

type InterruptedUploadNoticeProps = {
  file: FileFingerprint
  /** Bytes the server already holds, or null while that is still being read back. */
  uploaded: number | null
  ready: boolean
}

export function InterruptedUploadNotice({ file, uploaded, ready }: InterruptedUploadNoticeProps) {
  const { t } = useTranslation()
  const percent = uploaded === null ? null : Math.floor((uploaded / file.size) * 100)

  return (
    <Alert data-color="info" className="new-transfer__resume">
      <Heading level={3} data-size="2xs">
        {t('page.interrupted.title')}
      </Heading>
      <Paragraph data-size="sm">
        {ready
          ? t('page.interrupted.ready', { name: file.name })
          : t('page.interrupted.choose', { name: file.name, size: formatFileSize(file.size) })}
      </Paragraph>
      {percent !== null && uploaded !== null && (
        <>
          <progress value={percent} max={100} aria-label={t('page.interrupted.aria')} />
          <Paragraph data-size="sm">
            {t('page.interrupted.already', {
              percent,
              uploaded: formatFileSize(uploaded),
              total: formatFileSize(file.size),
            })}
          </Paragraph>
        </>
      )}
    </Alert>
  )
}
