import { toast } from 'react-toastify'
import { formatFileSize } from '../helpers/fileSizeHelper'

// TEMPORARY: shows how long fingerprinting a file takes, for stress-testing it. Remove after testing,
// together with the reportFingerprintTiming call in fileFingerprint.ts.

export type FingerprintTiming = {
  fileSize: number
  bytesRead: number
  readMs: number
  hashMs: number
}

export function reportFingerprintTiming({ fileSize, bytesRead, readMs, hashMs }: FingerprintTiming): void {
  const message =
    `Fingeravtrykk: ${Math.round(readMs + hashMs)} ms for ${formatFileSize(fileSize)} ` +
    `(lest ${formatFileSize(bytesRead)} på ${Math.round(readMs)} ms, hash ${Math.round(hashMs)} ms)`
  console.info(message)
  toast.info(message, { autoClose: 10_000 })
}
