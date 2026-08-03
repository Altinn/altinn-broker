import http from 'k6/http';
import { check, fail, sleep } from 'k6';
import { Trend } from 'k6/metrics';
import { getSenderAltinnToken, getRecipientAltinnToken } from './helpers/altinnTokenService.js';

/**
 * Measures whether the first wave of parallel Range downloads is slower than
 * subsequent waves (as reported by a customer with multi-threaded downloads).
 *
 * Flow:
 *  1. Initialize a file transfer and upload random bytes via TUS (chunked)
 *  2. Wait until Published
 *  3. Download in waves of N parallel 50 MiB Range requests
 *  4. Compare first-wave chunk timings vs later waves
 *
 * Usage (from tests/Altinn.Broker.LoadTests):
 *   k6 run -e base_url=... -e mp_client_id=... -e mp_kid=... -e mp_client_pem=... \
 *     -e sender=... -e recipient=... test-range-download.js
 *
 * Optional env:
 *   file_size_mb       Total upload size in MiB (default 1024)
 *   download_chunk_mb  Range size per request in MiB (default 50)
 *   parallel           Concurrent Range requests per wave (default 3)
 *   upload_chunk_mb    TUS PATCH chunk size in MiB (default 8)
 *   resource_id        Broker resource id (default ttd-broker-performance-test)
 *   file_transfer_id   Skip upload; reuse an already-published transfer
 *   poll_max_seconds   Max wait for Published after upload (default 600)
 */

export const options = {
  vus: 1,
  iterations: 1,
  thresholds: {
    checks: ['rate==1'],
  },
  setupTimeout: '300s',
};

const baseUrl = (__ENV.base_url || 'https://platform.tt02.altinn.no').replace(/\/$/, '');
const sender = __ENV.sender;
const recipient = __ENV.recipient;
const resourceId = __ENV.resource_id || 'ttd-broker-performance-test';

const FILE_SIZE_MB = Number(__ENV.file_size_mb) || 1024;
const DOWNLOAD_CHUNK_MB = Number(__ENV.download_chunk_mb) || 50;
const PARALLEL = Number(__ENV.parallel) || 3;
const UPLOAD_CHUNK_MB = Number(__ENV.upload_chunk_mb) || 8;
const POLL_MAX_SECONDS = Number(__ENV.poll_max_seconds) || 600;

const MiB = 1024 * 1024;
const FILE_SIZE = FILE_SIZE_MB * MiB;
const DOWNLOAD_CHUNK = DOWNLOAD_CHUNK_MB * MiB;
const UPLOAD_CHUNK = UPLOAD_CHUNK_MB * MiB;

const firstWaveChunkMs = new Trend('range_download_first_wave_chunk_ms', true);
const laterWaveChunkMs = new Trend('range_download_later_wave_chunk_ms', true);
const firstWaveWallMs = new Trend('range_download_first_wave_wall_ms', true);
const laterWaveWallMs = new Trend('range_download_later_wave_wall_ms', true);

function generateHeaders(token, contentType) {
  const headers = {
    Authorization: 'Bearer ' + token,
    Accept: '*/*',
    Connection: 'keep-alive',
  };
  if (contentType) {
    headers['Content-Type'] = contentType;
  }
  return headers;
}

function isPublishedStatus(val) {
  if (typeof val === 'number') return val === 3;
  if (typeof val === 'string') return val.toLowerCase() === 'published';
  return false;
}

function createRandomChunk(size) {
  // Patterned bytes are enough for throughput testing and much faster than crypto.randomBytes at multi-MiB sizes.
  const buffer = new ArrayBuffer(size);
  const view = new Uint8Array(buffer);
  for (let i = 0; i < size; i++) {
    view[i] = (i * 31) & 0xff;
  }
  return buffer;
}

function pollUntilPublished(fileTransferId, token, expectedSize) {
  const headers = generateHeaders(token, 'application/json');
  let lastResponse = null;

  for (let attempt = 0; attempt < POLL_MAX_SECONDS; attempt++) {
    lastResponse = http.get(`${baseUrl}/broker/api/v1/filetransfer/${fileTransferId}`, {
      headers,
      timeout: '60s',
    });
    if (lastResponse.status === 200) {
      const overview = lastResponse.json();
      if (isPublishedStatus(overview.fileTransferStatus)) {
        const checks = {
          'Published: fileTransferStatus is Published': (o) => isPublishedStatus(o.fileTransferStatus),
          'Published: fileTransferSize > 0': (o) => o.fileTransferSize > 0,
        };
        if (expectedSize != null) {
          checks['Published: fileTransferSize matches upload'] = (o) =>
            o.fileTransferSize === expectedSize;
        }
        check(overview, checks);
        return overview.fileTransferSize || expectedSize || FILE_SIZE;
      }
    }
    if (attempt > 0 && attempt % 30 === 0) {
      console.log(`Still waiting for Published… attempt ${attempt}/${POLL_MAX_SECONDS}`);
    }
    sleep(1);
  }

  fail(
    `File transfer ${fileTransferId} did not reach Published within ${POLL_MAX_SECONDS}s. ` +
      `Last status=${lastResponse && lastResponse.status} body=${lastResponse && lastResponse.body}`,
  );
}

function initializeFileTransfer(senderToken) {
  const headers = generateHeaders(senderToken, 'application/json');
  const body = {
    resourceId,
    checksum: null,
    fileName: `range-download-${FILE_SIZE_MB}mb.bin`,
    recipients: [`urn:altinn:organization:identifier-no:${recipient}`],
    sender: `urn:altinn:organization:identifier-no:${sender}`,
    sendersFileTransferReference: `k6-range-download-${Date.now()}`,
  };

  const res = http.post(`${baseUrl}/broker/api/v1/filetransfer`, JSON.stringify(body), {
    headers,
    timeout: '60s',
  });
  if (
    !check(res, {
      'Initialize: status was 200': (r) => r.status === 200,
    })
  ) {
    fail(`Initialize failed: status=${res.status} body=${res.body}`);
  }
  return res.json().fileTransferId;
}

function uploadViaTus(fileTransferId, senderToken) {
  const authHeaders = generateHeaders(senderToken);
  const tusEndpoint = `${baseUrl}/broker/api/v1/filetransfer/upload/tus/${fileTransferId}`;

  const createRes = http.post(tusEndpoint, null, {
    headers: Object.assign({}, authHeaders, {
      'Tus-Resumable': '1.0.0',
      'Upload-Length': String(FILE_SIZE),
    }),
    timeout: '60s',
  });
  if (
    !check(createRes, {
      'TUS create: status was 201': (r) => r.status === 201,
    })
  ) {
    fail(`TUS create failed: status=${createRes.status} body=${createRes.body}`);
  }

  let uploadUrl = createRes.headers.Location || createRes.headers.location;
  if (!uploadUrl) {
    uploadUrl = tusEndpoint;
  } else if (!uploadUrl.startsWith('http')) {
    uploadUrl = uploadUrl.startsWith('/') ? `${baseUrl}${uploadUrl}` : `${baseUrl}/${uploadUrl}`;
  }

  const fullChunk = createRandomChunk(UPLOAD_CHUNK);
  let offset = 0;
  const started = Date.now();

  while (offset < FILE_SIZE) {
    const remaining = FILE_SIZE - offset;
    const chunkSize = remaining < UPLOAD_CHUNK ? remaining : UPLOAD_CHUNK;
    const body = chunkSize === UPLOAD_CHUNK ? fullChunk : createRandomChunk(chunkSize);

    const patchRes = http.patch(uploadUrl, body, {
      headers: Object.assign({}, authHeaders, {
        'Tus-Resumable': '1.0.0',
        'Upload-Offset': String(offset),
        'Content-Type': 'application/offset+octet-stream',
        'Content-Length': String(chunkSize),
      }),
      timeout: '600s',
    });

    if (patchRes.status !== 204 && patchRes.status !== 200) {
      fail(
        `TUS PATCH failed at offset ${offset}: status=${patchRes.status} body=${patchRes.body}`,
      );
    }

    const newOffsetHeader = patchRes.headers['Upload-Offset'] || patchRes.headers['upload-offset'];
    const newOffset = newOffsetHeader ? Number(newOffsetHeader) : offset + chunkSize;
    if (!(newOffset > offset)) {
      fail(`TUS Upload-Offset did not advance (was ${offset}, got ${newOffsetHeader})`);
    }
    offset = newOffset;

    const pct = ((offset / FILE_SIZE) * 100).toFixed(1);
    const elapsed = Math.max((Date.now() - started) / 1000, 0.001);
    const mibPerSec = offset / elapsed / MiB;
    console.log(
      `Upload progress: ${pct}% (${(offset / MiB).toFixed(0)} / ${FILE_SIZE_MB} MiB, ${mibPerSec.toFixed(2)} MiB/s)`,
    );
  }

  console.log(`TUS upload completed in ${((Date.now() - started) / 1000).toFixed(1)}s`);
}

function buildRanges(fileSize) {
  const ranges = [];
  for (let start = 0; start < fileSize; start += DOWNLOAD_CHUNK) {
    const end = Math.min(start + DOWNLOAD_CHUNK, fileSize) - 1;
    ranges.push({ start, end, length: end - start + 1 });
  }
  return ranges;
}

function downloadWave(fileTransferId, recipientToken, ranges, waveIndex) {
  const headersBase = generateHeaders(recipientToken);
  const requests = ranges.map((range) => ({
    method: 'GET',
    url: `${baseUrl}/broker/api/v1/filetransfer/${fileTransferId}/download`,
    params: {
      // Discard body so we measure server/network time without buffering 50 MiB × N in the VU.
      responseType: 'none',
      timeout: '600s',
      headers: Object.assign({}, headersBase, {
        Range: `bytes=${range.start}-${range.end}`,
      }),
      tags: {
        name: 'RangeDownload',
        wave: String(waveIndex),
      },
    },
  }));

  const wallStart = Date.now();
  const responses = http.batch(requests);
  const wallMs = Date.now() - wallStart;

  const chunkDurations = [];
  for (let i = 0; i < responses.length; i++) {
    const res = responses[i];
    const range = ranges[i];
    const ok = check(res, {
      'Range download: status 206': (r) => r.status === 206,
      'Range download: Content-Range present': (r) => {
        const cr = r.headers['Content-Range'] || r.headers['content-range'];
        return typeof cr === 'string' && cr.indexOf(`bytes ${range.start}-${range.end}/`) === 0;
      },
    });
    if (!ok) {
      fail(
        `Wave ${waveIndex} range ${range.start}-${range.end} failed: ` +
          `status=${res.status} content-range=${res.headers['Content-Range'] || res.headers['content-range']}`,
      );
    }
    chunkDurations.push(res.timings.duration);
  }

  return { wallMs, chunkDurations };
}

export async function setup() {
  if (!sender || !recipient) {
    fail('Required env: sender, recipient (plus Maskinporten credentials)');
  }

  const senderToken = await getSenderAltinnToken();
  const recipientToken = await getRecipientAltinnToken();
  if (
    !check(senderToken, {
      'Sender Altinn token obtained': (t) => typeof t === 'string' && t.length > 0,
    })
  ) {
    fail('Could not obtain sender Altinn token');
  }
  if (
    !check(recipientToken, {
      'Recipient Altinn token obtained': (t) => typeof t === 'string' && t.length > 0,
    })
  ) {
    fail('Could not obtain recipient Altinn token');
  }

  let fileTransferId = __ENV.file_transfer_id;
  let fileSize = FILE_SIZE;

  if (fileTransferId) {
    console.log(`Reusing existing file transfer ${fileTransferId}`);
    fileSize = pollUntilPublished(fileTransferId, senderToken, null);
  } else {
    console.log(
      `Initializing ${FILE_SIZE_MB} MiB upload (TUS chunks of ${UPLOAD_CHUNK_MB} MiB), ` +
        `then ${PARALLEL}-way parallel ${DOWNLOAD_CHUNK_MB} MiB Range downloads`,
    );
    fileTransferId = initializeFileTransfer(senderToken);
    console.log(`Initialized file transfer ${fileTransferId}`);
    uploadViaTus(fileTransferId, senderToken);
    fileSize = pollUntilPublished(fileTransferId, senderToken, FILE_SIZE);
  }

  return { senderToken, recipientToken, fileTransferId, fileSize };
}

export default function (data) {
  const ranges = buildRanges(data.fileSize);
  const waves = [];
  for (let i = 0; i < ranges.length; i += PARALLEL) {
    waves.push(ranges.slice(i, i + PARALLEL));
  }

  console.log(
    `Downloading fileTransferId=${data.fileTransferId} (${(data.fileSize / MiB).toFixed(0)} MiB) ` +
      `in ${waves.length} wave(s) of up to ${PARALLEL} × ${DOWNLOAD_CHUNK_MB} MiB`,
  );

  const firstChunkSamples = [];
  const laterChunkSamples = [];
  let firstWall = null;
  const laterWalls = [];

  for (let w = 0; w < waves.length; w++) {
    const result = downloadWave(data.fileTransferId, data.recipientToken, waves[w], w);
    const avgChunk =
      result.chunkDurations.reduce((a, b) => a + b, 0) / result.chunkDurations.length;

    console.log(
      `Wave ${w}: wall=${result.wallMs.toFixed(0)}ms, ` +
        `chunks=[${result.chunkDurations.map((d) => d.toFixed(0)).join(', ')}]ms, ` +
        `avgChunk=${avgChunk.toFixed(0)}ms`,
    );

    if (w === 0) {
      firstWall = result.wallMs;
      firstWaveWallMs.add(result.wallMs);
      for (const d of result.chunkDurations) {
        firstWaveChunkMs.add(d);
        firstChunkSamples.push(d);
      }
    } else {
      laterWalls.push(result.wallMs);
      laterWaveWallMs.add(result.wallMs);
      for (const d of result.chunkDurations) {
        laterWaveChunkMs.add(d);
        laterChunkSamples.push(d);
      }
    }
  }

  if (laterChunkSamples.length === 0) {
    fail(
      'Need at least two waves to compare first vs later downloads. ' +
        'Increase file_size_mb or lower download_chunk_mb / parallel.',
    );
  }

  const avg = (arr) => arr.reduce((a, b) => a + b, 0) / arr.length;
  const firstAvg = avg(firstChunkSamples);
  const laterAvg = avg(laterChunkSamples);
  const ratio = firstAvg / laterAvg;
  const laterWallAvg = avg(laterWalls);

  console.log('--- Range download timing comparison ---');
  console.log(`First wave  wall: ${firstWall.toFixed(0)} ms | avg chunk: ${firstAvg.toFixed(0)} ms`);
  console.log(
    `Later waves wall avg: ${laterWallAvg.toFixed(0)} ms | avg chunk: ${laterAvg.toFixed(0)} ms`,
  );
  console.log(`First/later chunk ratio: ${ratio.toFixed(2)}x`);
  if (ratio >= 1.5) {
    console.log(
      `RESULT: First wave looks substantially slower (~${ratio.toFixed(2)}x). Customer report reproduced.`,
    );
  } else if (ratio >= 1.2) {
    console.log(
      `RESULT: First wave is moderately slower (~${ratio.toFixed(2)}x). Worth investigating.`,
    );
  } else {
    console.log(
      `RESULT: No large first-wave penalty observed (ratio ${ratio.toFixed(2)}x).`,
    );
  }
}
