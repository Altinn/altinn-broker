# TUS concatenation validation with tus-py-client

Small Python harness that mirrors `Altinn.Broker.Tests.LargeFile`, but uses [tus-py-client](https://github.com/tus/tus-py-client) with `parallel_uploads` to exercise Broker concatenation end-to-end.

## Prerequisites

- Python 3.10+
- Git (required to install tus-py-client from GitHub; see note below)
- Maskinporten client credentials (`CLIENT_ID`, `CLIENT_KID`, `CLIENT_SECRET`)
- A [system user for your `ORG_NO`](https://docs.altinn.studio/en/authorization/getting-started/systemuser/) linked to that client

## Setup

```bash
cd tests/tus/Altinn.Broker.Tests.TusPythonClient
python -m venv .venv
source .venv/bin/activate   # Windows: .venv\Scripts\activate
pip install -r requirements.txt
```

**Note:** Parallel upload concatenation is not in the current PyPI release of `tuspy` (1.1.0). This harness installs tus-py-client from the Git commit that added `parallel_uploads` support. When a newer PyPI release ships with that feature, you can switch `requirements.txt` to a normal `tuspy>=…` pin.

## Run

```bash
export BASE_URL="https://platform.tt02.altinn.no"
export CLIENT_ID="your-maskinporten-client-id"
export CLIENT_KID="your-key-id"
export CLIENT_SECRET="$(cat /path/to/private-key.pem)"
export RESOURCE_ID="your-resource-id"
export ORG_NO="your-org-number"

# Optional
export CHUNK_SIZE_MB="8"
export TUS_PARALLEL_PARTIAL_UPLOADS="4"
export GIGABYTES_TO_UPLOAD="1"
export UPLOAD_FILE_PATH="/path/to/your/file.bin"
export MASKINPORTEN_TOKEN_URL="https://test.maskinporten.no/token"

python src/main.py
```

By default (when `GIGABYTES_TO_UPLOAD` is not set) the script uploads **64 MiB** of generated data so you can smoke-test quickly.

Set `UPLOAD_FILE_PATH` to upload an existing file instead. The file size is taken from disk and `GIGABYTES_TO_UPLOAD` is ignored. The file is not deleted after upload.

**Windows (PowerShell):**

```powershell
$env:BASE_URL = "https://platform.tt02.altinn.no"
$env:CLIENT_ID = "..."
$env:CLIENT_KID = "..."
$env:CLIENT_SECRET = Get-Content -Raw -Path "C:\path\to\private-key.pem"
$env:RESOURCE_ID = "bruno-broker"
$env:ORG_NO = "991825827"

# Generate + upload in one go (script lives in tests/tus/)
$env:UPLOAD_FILE_PATH = ..\generate-file.ps1 512MB
python src/main.py
```

If your Windows user folder contains `$` (e.g. `C:\Users\$HH8000-...`), do **not** paste that path inside double quotes — PowerShell treats `$HH8000` as a variable. Use single quotes, `$env:TEMP`, or capture the path from `..\generate-file.ps1` as shown above.

```powershell
# MD5 of the generated file
Get-FileHash -LiteralPath $env:UPLOAD_FILE_PATH -Algorithm MD5
```

## What it does

1. Signs a Maskinporten JWT and exchanges it for an Altinn token
2. Initializes a file transfer
3. Generates a temporary binary file (or uses `UPLOAD_FILE_PATH` when set)
4. Uploads via tus-py-client (`parallel_uploads` = 1 for single-stream, > 1 for concatenation)
5. Verifies the file transfer reaches `Published`

The TUS endpoint is:

`/broker/api/v1/filetransfer/upload/tus/{fileTransferId}`

No custom `Upload-Concat` header handling is required in client code — tus-py-client builds the concatenation flow automatically when `parallel_uploads` is greater than 1.

See [../README.md](../README.md) for shared environment variables and authentication details.
