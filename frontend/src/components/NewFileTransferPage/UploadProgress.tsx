import { Button } from '@digdir/designsystemet-react'
import { PauseIcon, PlayIcon } from '@navikt/aksel-icons'
import type { UploadProgress as Progress } from '../../api/tus/tusUpload'
import { formatRemainingTime } from '../../helpers/durationHelper'
import { formatFileSize } from '../../helpers/fileSizeHelper'

type UploadState = {
  progress: Progress | null
  initializing: boolean
  pausing: boolean
  paused: boolean
  finishing: boolean
  stopped: boolean
}

type UploadProgressProps = UploadState & {
  onPause: () => void
  onResume: () => void
}

export function UploadProgress({ onPause, onResume, ...state }: UploadProgressProps) {
  const { progress, pausing, paused, finishing, stopped } = state
  const running = progress !== null && !pausing && !paused && !finishing && !stopped

  return (
    <div className="new-transfer__progress">
      <progress value={progress?.percent ?? 0} max={100} aria-label="Opplasting" />
      <div className="new-transfer__progress-status">
        <p>{statusText(state)}</p>
        {(paused || pausing) && (
          <Button type="button" variant="tertiary" data-size="sm" onClick={onResume}>
            <PlayIcon aria-hidden />
            Fortsett
          </Button>
        )}
        {running && (
          <Button type="button" variant="tertiary" data-size="sm" onClick={onPause}>
            <PauseIcon aria-hidden />
            Pause
          </Button>
        )}
      </div>
      {/* The status line changes several times a second, which is unusable read aloud, so the
          same state is announced in tenths instead. */}
      <span className="sr-only" aria-live="polite">
        {screenReaderStatusText(state)}
      </span>
    </div>
  )
}

function statusText({
  progress,
  initializing,
  pausing,
  paused,
  finishing,
  stopped,
}: UploadState): string {
  if (!progress) {
    // Resuming asks each upload how far it got before it can say anything about progress.
    return initializing ? 'Oppretter formidlingen…' : 'Finner ut hvor opplastingen slapp…'
  }

  const amount = `${formatFileSize(progress.loaded)} av ${formatFileSize(progress.total)}`
  const sent = `${progress.percent} % (${amount})`

  if (stopped) {
    return `Stoppet på ${sent}`
  }
  if (paused) {
    return `Pauset på ${sent}`
  }
  if (pausing) {
    return `Pauser på ${sent} — fullfører delene som er i gang`
  }
  if (finishing) {
    return 'Setter sammen filen…'
  }

  return [`Laster opp — ${sent}`, rate(progress), remaining(progress)]
    .filter(Boolean)
    .join(' · ')
}

// Readable status text for screen readers
function screenReaderStatusText({
  progress,
  initializing,
  pausing,
  paused,
  finishing,
  stopped,
}: UploadState): string {
  if (!progress) {
    return initializing ? 'Oppretter formidlingen' : 'Finner ut hvor opplastingen slapp'
  }

  const tenths = `${Math.floor(progress.percent / 10) * 10} prosent`

  if (stopped) {
    return `Opplastingen stoppet på ${tenths}`
  }
  if (paused) {
    return `Opplastingen er pauset på ${tenths}`
  }
  if (pausing) {
    return 'Pauser opplastingen'
  }
  if (finishing) {
    return 'Setter sammen filen'
  }

  return `Laster opp, ${tenths}`
}

function rate(progress: Progress): string {
  return progress.bytesPerSecond === null ? '' : `${formatFileSize(progress.bytesPerSecond)}/s`
}

function remaining(progress: Progress): string {
  return progress.secondsRemaining === null
    ? ''
    : `${formatRemainingTime(progress.secondsRemaining)} igjen`
}
