import { Alert, Heading, Paragraph } from '@digdir/designsystemet-react'
import { useTranslation } from 'react-i18next'
import { formatFileSize } from '../../helpers/fileSizeHelper'

type InterruptedUploadNoticeProps = {
  fileName: string
  fileSize: number
  /** Bytes the server already holds. */
  uploaded: number
  ready: boolean
}

export function InterruptedUploadNotice({ fileName, fileSize, uploaded, ready }: InterruptedUploadNoticeProps) {
  const { t } = useTranslation()
  const percent = uploaded === null ? null : Math.floor((uploaded / fileSize) * 100)

  return (
    <Alert data-color="info" className="new-transfer__resume">
      <Heading level={3} data-size="2xs">
        {t('page.interrupted.title')}
      </Heading>
      <Paragraph data-size="sm">
        {ready

          ? t('page.interrupted.ready', { name: fileName })
          : t('page.interrupted.choose', { name: fileName, size: formatFileSize(fileSize) })}
      </Paragraph>
      {percent !== null && uploaded !== null && (
        <>
          <progress value={percent} max={100} aria-label={t('page.interrupted.aria')} />
          <Paragraph data-size="sm">
            {t('page.interrupted.already', {
              percent,
              uploaded: formatFileSize(uploaded),
              total: formatFileSize(fileSize),
            })}
          </Paragraph>
        </>
      )}
    </Alert>
  )
}
