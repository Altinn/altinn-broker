import { Button } from '@digdir/designsystemet-react'
import { PauseIcon, PlayIcon } from '@navikt/aksel-icons'
import { useTranslation } from 'react-i18next'
import type { UploadProgress as Progress } from '../../api/tus/tusUpload'
import { formatRemainingTime } from '../../helpers/durationHelper'
import { formatFileSize } from '../../helpers/fileSizeHelper'
import { i18n } from '../../i18n/config'
import { useUploadProgress, type UploadStatus } from '../../upload/uploadsContext'

type UploadProgressProps = {
  status: UploadStatus
  onPause: () => void
  onResume: () => void
}

export function UploadProgress({ status, onPause, onResume }: UploadProgressProps) {
  const { t } = useTranslation()
  const progress = useUploadProgress()

  return (
    <div className="new-transfer__progress">
      <progress value={progress?.percent ?? 0} max={100} aria-label={t('page.upload.aria')} />
      <div className="new-transfer__progress-status">
        <p>{statusText(status, progress)}</p>
        {(status === 'paused' || status === 'pausing') && (
          <Button type="button" variant="tertiary" data-size="sm" onClick={onResume}>
            <PlayIcon aria-hidden />
            {t('page.upload.continue')}
          </Button>
        )}
        {status === 'uploading' && (
          <Button type="button" variant="tertiary" data-size="sm" onClick={onPause}>
            <PauseIcon aria-hidden />
            {t('page.upload.pause')}
          </Button>
        )}
      </div>
      {/* The status line changes several times a second, which is unusable read aloud, so the
          same state is announced in tenths instead. */}
      <span className="sr-only" aria-live="polite">
        {screenReaderStatusText(status, progress)}
      </span>
    </div>
  )
}

function statusText(status: UploadStatus, progress: Progress | null): string {
  if (status === 'initializing' || !progress) {
    return i18n.t('page.upload.creating')
  }

  const amount = i18n.t('page.upload.of', {
    loaded: formatFileSize(progress.loaded),
    total: formatFileSize(progress.total),
  })
  const sent = `${progress.percent} % (${amount})`

  switch (status) {
    case 'failed':
      return i18n.t('page.upload.stopped_at', { progress: sent })
    case 'paused':
      return i18n.t('page.upload.paused_at', { progress: sent })
    case 'pausing':
      return i18n.t('page.upload.pausing_at', { progress: sent })
    case 'finishing':
      return i18n.t('page.upload.assembling')
    default:
      return [i18n.t('page.upload.uploading', { progress: sent }), rate(progress), remaining(progress)]
        .filter(Boolean)
        .join(' · ')
  }
}

// Readable status text for screen readers
function screenReaderStatusText(status: UploadStatus, progress: Progress | null): string {
  if (status === 'initializing' || !progress) {
    return i18n.t('page.upload.creating_plain')
  }

  const tenths = i18n.t('page.upload.percent_label', { percent: Math.floor(progress.percent / 10) * 10 })

  switch (status) {
    case 'failed':
      return i18n.t('page.upload.sr_stopped', { percent: tenths })
    case 'paused':
      return i18n.t('page.upload.sr_paused', { percent: tenths })
    case 'pausing':
      return i18n.t('page.upload.sr_pausing')
    case 'finishing':
      return i18n.t('page.upload.assembling_plain')
    default:
      return i18n.t('page.upload.sr_uploading', { percent: tenths })
  }
}

function rate(progress: Progress): string {
  return progress.bytesPerSecond === null
    ? ''
    : i18n.t('page.upload.rate', { size: formatFileSize(progress.bytesPerSecond) })
}

function remaining(progress: Progress): string {
  return progress.secondsRemaining === null
    ? ''
    : i18n.t('page.duration.remaining', { time: formatRemainingTime(progress.secondsRemaining) })
}
