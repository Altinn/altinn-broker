# TUS concatenation validation with tus-js-client

Small Node.js harness that mirrors `Altinn.Broker.Tests.LargeFile`, but uses the standard [tus-js-client](https://github.com/tus/tus-js-client) library with `parallelUploads` to exercise Broker concatenation end-to-end.

## Prerequisites

- Node.js 20+
- Test tools credentials for the Altinn token generator (same as the LargeFile test)

## Setup

```bash
cd tests/Altinn.Broker.Tests.TusJsClient
npm install
```

## Run

```bash
export BASE_URL="https://altinn-dev-api.azure-api.net"
export TEST_TOOLS_USERNAME="your-test-tools-user"
export TEST_TOOLS_PASSWORD="your-test-tools-password"

# Optional
export ORG_NO="991825827"
export CHUNK_SIZE_MB="8"
export TUS_PARALLEL_PARTIAL_UPLOADS="4"
export GIGABYTES_TO_UPLOAD="1"

npm run upload
```

By default (when `GIGABYTES_TO_UPLOAD` is not set) the script uploads **64 MiB** so you can smoke-test quickly.

## What it does

1. Obtains a Maskinporten/Altinn test token
2. Configures the test resource max file size
3. Initializes a file transfer
4. Generates a temporary binary file
5. Uploads via tus-js-client with `parallelUploads` (TUS concatenation)
6. Verifies the file transfer reaches `Published`

The TUS endpoint is:

`/broker/api/v1/filetransfer/upload/tus/{fileTransferId}`

No custom `Upload-Concat` header handling is required in client code — tus-js-client builds the concatenation flow automatically when `parallelUploads` is greater than 1.
