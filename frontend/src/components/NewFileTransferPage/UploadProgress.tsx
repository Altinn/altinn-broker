import { Button } from '@digdir/designsystemet-react'
import { PauseIcon, PlayIcon } from '@navikt/aksel-icons'
import type { UploadProgress as Progress } from '../../api/tus/tusUpload'
import { formatRemainingTime } from '../../helpers/durationHelper'
import { formatFileSize } from '../../helpers/fileSizeHelper'
import { useUploadProgress, type UploadStatus } from '../../upload/uploadsContext'

type UploadProgressProps = {
  status: UploadStatus
  onPause: () => void
  onResume: () => void
}

export function UploadProgress({ status, onPause, onResume }: UploadProgressProps) {
  const progress = useUploadProgress()

  return (
    <div className="new-transfer__progress">
      <progress value={progress?.percent ?? 0} max={100} aria-label="Opplasting" />
      <div className="new-transfer__progress-status">
        <p>{statusText(status, progress)}</p>
        {(status === 'paused' || status === 'pausing') && (
          <Button type="button" variant="tertiary" data-size="sm" onClick={onResume}>
            <PlayIcon aria-hidden />
            Fortsett
          </Button>
        )}
        {status === 'uploading' && progress !== null && (
          <Button type="button" variant="tertiary" data-size="sm" onClick={onPause}>
            <PauseIcon aria-hidden />
            Pause
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
  if (!progress) {
    // Resuming asks each upload how far it got before it can say anything about progress.
    return status === 'initializing' ? 'Oppretter formidlingen…' : 'Finner ut hvor opplastingen slapp…'
  }

  const amount = `${formatFileSize(progress.loaded)} av ${formatFileSize(progress.total)}`
  const sent = `${progress.percent} % (${amount})`

  switch (status) {
    case 'failed':
      return `Stoppet på ${sent}`
    case 'paused':
      return `Pauset på ${sent}`
    case 'pausing':
      return `Pauser på ${sent} — fullfører delene som er i gang`
    case 'finishing':
      return 'Setter sammen filen…'
    default:
      return [`Laster opp — ${sent}`, rate(progress), remaining(progress)]
        .filter(Boolean)
        .join(' · ')
  }
}

// Readable status text for screen readers
function screenReaderStatusText(status: UploadStatus, progress: Progress | null): string {
  if (!progress) {
    return status === 'initializing' ? 'Oppretter formidlingen' : 'Finner ut hvor opplastingen slapp'
  }

  const tenths = `${Math.floor(progress.percent / 10) * 10} prosent`

  switch (status) {
    case 'failed':
      return `Opplastingen stoppet på ${tenths}`
    case 'paused':
      return `Opplastingen er pauset på ${tenths}`
    case 'pausing':
      return 'Pauser opplastingen'
    case 'finishing':
      return 'Setter sammen filen'
    default:
      return `Laster opp, ${tenths}`
  }
}

function rate(progress: Progress): string {
  return progress.bytesPerSecond === null ? '' : `${formatFileSize(progress.bytesPerSecond)}/s`
}

function remaining(progress: Progress): string {
  return progress.secondsRemaining === null
    ? ''
    : `${formatRemainingTime(progress.secondsRemaining)} igjen`
}
