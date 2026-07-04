import { createReadStream } from 'node:fs';
import { stat, unlink } from 'node:fs/promises';
import { Upload } from 'tus-js-client';

import {
  configureResource,
  getAccessToken,
  getFileTransferOverview,
  initializeFileTransfer,
} from './broker.mjs';
import { createUploadFile } from './generate-file.mjs';

const DEFAULT_CHUNK_SIZE_MB = 8;
const DEFAULT_PARALLEL_UPLOADS = 4;
const DEFAULT_UPLOAD_MIB = 64;

function readEnv(name, fallback) {
  const value = process.env[name];
  return value === undefined || value === '' ? fallback : value;
}

function requireEnv(name) {
  const value = process.env[name];
  if (!value) {
    throw new Error(`Missing required environment variable: ${name}`);
  }

  return value;
}

function formatGiB(bytes) {
  return (bytes / (1024 * 1024 * 1024)).toFixed(2);
}

function formatMiB(bytes) {
  return (bytes / (1024 * 1024)).toFixed(2);
}

async function uploadWithTusJsClient({
  baseUrl,
  token,
  fileTransferId,
  filePath,
  chunkSize,
  parallelUploads,
}) {
  const { size } = await stat(filePath);
  const endpoint = `${baseUrl.replace(/\/$/, '')}/broker/api/v1/filetransfer/upload/tus/${fileTransferId}`;
  const startedAt = Date.now();

  await new Promise((resolve, reject) => {
    const upload = new Upload(
      createReadStream(filePath),
      {
        endpoint,
        chunkSize,
        parallelUploads,
        retryDelays: [1000, 2000, 4000, 8000, 16000],
        headers: {
          Authorization: `Bearer ${token}`,
        },
        metadata: {
          filename: 'input.txt',
          filetype: 'application/octet-stream',
        },
        onError(error) {
          reject(error);
        },
        onProgress(bytesUploaded, bytesTotal) {
          const elapsedSeconds = Math.max((Date.now() - startedAt) / 1000, 0.001);
          const mibPerSecond = bytesUploaded / elapsedSeconds / (1024 * 1024);
          const percentage = ((bytesUploaded / bytesTotal) * 100).toFixed(1);
          process.stdout.write(
            `\rProgress: ${percentage}% (${formatGiB(bytesUploaded)} / ${formatGiB(bytesTotal)} GiB, ${mibPerSecond.toFixed(2)} MiB/s)`,
          );
        },
        onSuccess() {
          console.log('');
          console.log(`TUS upload finished: ${upload.url}`);
          resolve();
        },
      },
    );

    upload.start();
  });

  const elapsedSeconds = Math.max((Date.now() - startedAt) / 1000, 0.001);
  const averageSpeedMbps = size / elapsedSeconds / (1024 * 1024);
  console.log(
    `TUS concatenation upload completed in ${elapsedSeconds.toFixed(1)}s (avg: ${averageSpeedMbps.toFixed(2)} MiB/s)`,
  );
}

async function main() {
  const baseUrl = readEnv('BASE_URL', 'https://altinn-dev-api.azure-api.net');
  const username = requireEnv('TEST_TOOLS_USERNAME');
  const password = requireEnv('TEST_TOOLS_PASSWORD');
  const orgNumber = readEnv('ORG_NO', '991825827');
  const chunkSizeMb = Number(readEnv('CHUNK_SIZE_MB', String(DEFAULT_CHUNK_SIZE_MB)));
  const parallelUploads = Number(readEnv('TUS_PARALLEL_PARTIAL_UPLOADS', String(DEFAULT_PARALLEL_UPLOADS)));
  const uploadBytes = process.env.GIGABYTES_TO_UPLOAD
    ? Number(process.env.GIGABYTES_TO_UPLOAD) * 1024 * 1024 * 1024
    : DEFAULT_UPLOAD_MIB * 1024 * 1024;
  const chunkSize = chunkSizeMb * 1024 * 1024;

  if (parallelUploads < 2) {
    throw new Error('Set TUS_PARALLEL_PARTIAL_UPLOADS to 2 or more to exercise concatenation.');
  }

  console.log(`BASE_URL: ${baseUrl}`);
  console.log(`Upload size: ${formatGiB(uploadBytes)} GiB (${formatMiB(uploadBytes)} MiB)`);
  console.log(`CHUNK_SIZE_MB: ${chunkSizeMb}`);
  console.log(`TUS_PARALLEL_PARTIAL_UPLOADS: ${parallelUploads}`);

  const token = await getAccessToken(username, password, orgNumber);
  await configureResource(baseUrl, token, uploadBytes);
  const fileTransferId = await initializeFileTransfer(baseUrl, token);
  console.log(`File transfer id: ${fileTransferId}`);

  const filePath = await createUploadFile(uploadBytes);
  console.log(`Temporary upload file: ${filePath}`);

  try {
    await uploadWithTusJsClient({
      baseUrl,
      token,
      fileTransferId,
      filePath,
      chunkSize,
      parallelUploads,
    });

    const overview = await getFileTransferOverview(baseUrl, token, fileTransferId);
    if (overview.fileTransferStatus !== 'Published') {
      throw new Error(`Expected Published status, got ${overview.fileTransferStatus}`);
    }

    console.log(`Verified file transfer ${fileTransferId} is Published.`);
  } finally {
    await unlink(filePath).catch(() => {});
  }
}

main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});


