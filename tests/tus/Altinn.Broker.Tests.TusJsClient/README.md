# TUS concatenation validation with tus-js-client

Small Node.js harness that mirrors `Altinn.Broker.Tests.LargeFile`, but uses the standard [tus-js-client](https://github.com/tus/tus-js-client) library with `parallelUploads` to exercise Broker concatenation end-to-end.

## Prerequisites

- Node.js 20+
- Maskinporten client credentials (`CLIENT_ID`, `CLIENT_KID`, `CLIENT_PEM_FILE`)
- A [system user for your `ORG_NO`](https://docs.altinn.studio/en/authorization/getting-started/systemuser/) linked to that client

## Setup

```bash
cd tests/tus/Altinn.Broker.Tests.TusJsClient
npm install
```

## Run

```bash
export BASE_URL="https://platform.tt02.altinn.no"
export CLIENT_ID="your-maskinporten-client-id"
export CLIENT_KID="your-key-id"
export CLIENT_PEM_FILE="/path/to/private-key.pem"
export RESOURCE_ID="your-resource-id"
export ORG_NO="your-org-number"

# Optional
export CHUNK_SIZE_MB="8"
export TUS_PARALLEL_PARTIAL_UPLOADS="4"
export GIGABYTES_TO_UPLOAD="1"
export UPLOAD_FILE_PATH="/path/to/your/file.bin"
export MASKINPORTEN_TOKEN_URL="https://test.maskinporten.no/token"

npm run upload
```

By default (when `GIGABYTES_TO_UPLOAD` is not set) the script uploads **64 MiB** so you can smoke-test quickly.

Set `UPLOAD_FILE_PATH` to upload an existing file instead. The file size is taken from disk and `GIGABYTES_TO_UPLOAD` is ignored. The file is not deleted after upload.

**Windows (PowerShell):**

```powershell
$env:BASE_URL = "https://platform.tt02.altinn.no"
$env:CLIENT_ID = "..."
$env:CLIENT_KID = "..."
$env:CLIENT_PEM_FILE = "C:\path\to\private-key.pem"
$env:RESOURCE_ID = "bruno-broker"
$env:ORG_NO = "991825827"

$env:UPLOAD_FILE_PATH = ..\generate-file.ps1 512MB
npm run upload
```

## What it does

1. Signs a Maskinporten JWT and exchanges it for an Altinn token
2. Initializes a file transfer
3. Generates a temporary binary file (or uses `UPLOAD_FILE_PATH` when set)
4. Uploads via tus-js-client (`parallelUploads` = 1 for single-stream, > 1 for concatenation)
5. Verifies the file transfer reaches `Published`

The TUS endpoint is:

`/broker/api/v1/filetransfer/upload/tus/{fileTransferId}`

No custom `Upload-Concat` header handling is required in client code — tus-js-client builds the concatenation flow automatically when `parallelUploads` is greater than 1.

See [../README.md](../README.md) for shared environment variables and authentication details.
