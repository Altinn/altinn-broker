import { createWriteStream } from 'node:fs';
import { finished } from 'node:stream/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { randomFillSync } from 'node:crypto';

const WRITE_CHUNK_SIZE = 8 * 1024 * 1024;

export async function createUploadFile(byteLength) {
  const filePath = join(tmpdir(), `altinn-broker-tus-upload-${Date.now()}.bin`);
  const stream = createWriteStream(filePath);

  let remaining = byteLength;
  const buffer = Buffer.alloc(Math.min(WRITE_CHUNK_SIZE, byteLength));

  while (remaining > 0) {
    const chunkSize = Math.min(buffer.length, remaining);
    randomFillSync(buffer.subarray(0, chunkSize));
    const canContinue = stream.write(buffer.subarray(0, chunkSize));
    if (!canContinue) {
      await new Promise((resolve) => stream.once('drain', resolve));
    }

    remaining -= chunkSize;
  }

  stream.end();
  await finished(stream);

  return filePath;
}
