import { createReadStream } from 'node:fs';
import { stat, unlink } from 'node:fs/promises';
import { resolve } from 'node:path';
import { Upload } from 'tus-js-client';

import { exchangeAltinnToken, readAuthOptionsFromEnvironment } from './altinn-auth.mjs';
import { initializeFileTransfer, verifyPublished } from './broker.mjs';
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

function logUploadSize(uploadBytes) {
  console.log(`Upload size: ${formatGiB(uploadBytes)} GiB (${formatMiB(uploadBytes)} MiB)`);
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
  const uploadLabel = parallelUploads > 1 ? 'TUS concatenation upload' : 'TUS upload';

  await new Promise((resolvePromise, reject) => {
    const upload = new Upload(createReadStream(filePath), {
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
        resolvePromise();
      },
    });

    upload.start();
  });

  const elapsedSeconds = Math.max((Date.now() - startedAt) / 1000, 0.001);
  const averageSpeedMbps = size / elapsedSeconds / (1024 * 1024);
  console.log(
    `${uploadLabel} completed in ${elapsedSeconds.toFixed(1)}s (avg: ${averageSpeedMbps.toFixed(2)} MiB/s)`,
  );
}

async function resolveUploadFile(uploadFilePath) {
  const path = resolve(uploadFilePath);
  try {
    const fileStat = await stat(path);
    if (!fileStat.isFile()) {
      throw new Error(`UPLOAD_FILE_PATH does not exist or is not a file: ${uploadFilePath}`);
    }

    return { filePath: path, uploadBytes: fileStat.size, cleanupFile: false };
  } catch (error) {
    if (error && typeof error === 'object' && 'code' in error && error.code === 'ENOENT') {
      throw new Error(`UPLOAD_FILE_PATH does not exist or is not a file: ${uploadFilePath}`);
    }

    throw error;
  }
}

async function createGeneratedUploadFile(uploadBytes) {
  const filePath = await createUploadFile(uploadBytes);
  return { filePath, uploadBytes, cleanupFile: true };
}

async function main() {
  const authOptions = readAuthOptionsFromEnvironment();
  const baseUrl = authOptions.baseUrl;
  const resourceId = requireEnv('RESOURCE_ID');
  const chunkSizeMb = Number(readEnv('CHUNK_SIZE_MB', String(DEFAULT_CHUNK_SIZE_MB)));
  const parallelUploads = Number(
    readEnv('TUS_PARALLEL_PARTIAL_UPLOADS', String(DEFAULT_PARALLEL_UPLOADS)),
  );
  const uploadFilePath = readEnv('UPLOAD_FILE_PATH', null);
  const chunkSize = chunkSizeMb * 1024 * 1024;

  console.log(`BASE_URL: ${baseUrl}`);
  console.log(`RESOURCE_ID: ${resourceId}`);
  console.log(`ORG_NO: ${authOptions.orgNumber}`);
  console.log(`CHUNK_SIZE_MB: ${chunkSizeMb}`);
  console.log(`TUS_PARALLEL_PARTIAL_UPLOADS: ${parallelUploads}`);

  const token = await exchangeAltinnToken(authOptions);
  const fileTransferId = await initializeFileTransfer(
    baseUrl,
    token,
    authOptions.orgNumber,
    resourceId,
  );
  console.log(`File transfer id: ${fileTransferId}`);

  const { filePath, uploadBytes, cleanupFile } = uploadFilePath
    ? await resolveUploadFile(uploadFilePath)
    : await createGeneratedUploadFile(
        process.env.GIGABYTES_TO_UPLOAD
          ? Number(process.env.GIGABYTES_TO_UPLOAD) * 1024 * 1024 * 1024
          : DEFAULT_UPLOAD_MIB * 1024 * 1024,
      );

  logUploadSize(uploadBytes);
  if (uploadFilePath) {
    console.log(`UPLOAD_FILE_PATH: ${filePath}`);
  } else {
    console.log(`Temporary upload file: ${filePath}`);
  }

  try {
    await uploadWithTusJsClient({
      baseUrl,
      token,
      fileTransferId,
      filePath,
      chunkSize,
      parallelUploads,
    });

    await verifyPublished(baseUrl, token, fileTransferId);
  } finally {
    if (cleanupFile) {
      await unlink(filePath).catch(() => {});
    }
  }
}

main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
