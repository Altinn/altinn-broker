import type { FileFingerprint } from '../api/uploadPlan'

// Tells whether a file picked again is the one an upload started with, also in another browser or on
// another machine, where the modified time can't be trusted. Hashing the whole of a large file would
// take minutes, so only a few pieces of it are read: a different file of the same name and exact size
// would have to match in all of them.

export const FINGERPRINT_VERSION = 1

const SAMPLE_SIZE = 256 * 1024
const SAMPLE_COUNT = 4

export async function fingerprintFile(file: Blob): Promise<FileFingerprint> {
  const size = new Uint8Array(8)
  new DataView(size.buffer).setBigUint64(0, BigInt(file.size))

  const pieces = [size, ...(await Promise.all(sampleRanges(file.size).map(([start, end]) => readBytes(file, start, end))))]
  const digest = await crypto.subtle.digest('SHA-256', concat(pieces))

  return { fingerprint: toBase64(new Uint8Array(digest)), fingerprintVersion: FINGERPRINT_VERSION }
}

/** Whether the file is the one the fingerprint was taken of. */
export async function matchesFingerprint(file: Blob, expected: FileFingerprint): Promise<boolean> {
  if (expected.fingerprintVersion !== FINGERPRINT_VERSION) {
    return false
  }
  const actual = await fingerprintFile(file)
  return actual.fingerprint === expected.fingerprint
}

// The start, two points between and the end. A small file is read whole.
function sampleRanges(size: number): [number, number][] {
  if (size <= SAMPLE_SIZE * SAMPLE_COUNT) {
    return [[0, size]]
  }
  const lastStart = size - SAMPLE_SIZE
  return Array.from({ length: SAMPLE_COUNT }, (_, index) => {
    const start = Math.floor((lastStart * index) / (SAMPLE_COUNT - 1))
    return [start, start + SAMPLE_SIZE]
  })
}

async function readBytes(file: Blob, start: number, end: number): Promise<Uint8Array> {
  return new Uint8Array(await file.slice(start, end).arrayBuffer())
}

function concat(pieces: Uint8Array[]): Uint8Array<ArrayBuffer> {
  const joined = new Uint8Array(pieces.reduce((total, piece) => total + piece.length, 0))
  let offset = 0
  for (const piece of pieces) {
    joined.set(piece, offset)
    offset += piece.length
  }
  return joined
}

function toBase64(bytes: Uint8Array): string {
  let binary = ''
  for (const byte of bytes) {
    binary += String.fromCharCode(byte)
  }
  return btoa(binary)
}
