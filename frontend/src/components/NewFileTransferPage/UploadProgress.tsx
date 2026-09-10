import type { UploadProgress as Progress } from '../../api/xhrClient'
import { formatFileSize } from '../../helpers/fileSizeHelper'

type UploadProgressProps = {
  progress: Progress | null
}

export function UploadProgress({ progress }: UploadProgressProps) {
  return (
    <div className="new-transfer__progress" aria-live="polite">
      <progress value={progress?.percent ?? 0} max={100} aria-label="Opplasting" />
      <p>
        {progress
          ? `Laster opp — ${progress.percent} % (${formatFileSize(progress.loaded)} av ${formatFileSize(progress.total)})`
          : 'Oppretter formidlingen…'}
      </p>
    </div>
  )
}
