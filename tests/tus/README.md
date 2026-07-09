# TUS client validation harnesses

End-to-end TUS upload clients for manually testing Altinn Broker, including the [concatenation extension](https://tus.io/protocols/resumable-upload.html#concatenation) for parallel partial uploads.

| Client | Path | Library |
| --- | --- | --- |
| .NET | [Altinn.Broker.Tests.LargeFile](Altinn.Broker.Tests.LargeFile/) | Custom reference implementation |
| JavaScript | [Altinn.Broker.Tests.TusJsClient](Altinn.Broker.Tests.TusJsClient/) | [tus-js-client](https://github.com/tus/tus-js-client) |
| Python | [Altinn.Broker.Tests.TusPythonClient](Altinn.Broker.Tests.TusPythonClient/) | [tus-py-client](https://github.com/tus/tus-py-client) |

See also [tus-concatenation.md](../../tus-concatenation.md) for protocol and server behaviour.

## Quick start

All three clients share the same environment variables and defaults:

| Variable | Required | Default |
| --- | --- | --- |
| `CLIENT_ID` | yes | — |
| `CLIENT_KID` | yes | — |
| `CLIENT_PEM_FILE` | yes* | — | Path to Maskinporten private key `.pem` file |
| `RESOURCE_ID` | yes | — |
| `ORG_NO` | yes | — |
| `BASE_URL` | no | `https://platform.tt02.altinn.no` |
| `CHUNK_SIZE_MB` | no | `8` |
| `TUS_PARALLEL_PARTIAL_UPLOADS` | no | `4` |
| `GIGABYTES_TO_UPLOAD` | no | **64 MiB** smoke-test size when unset |
| `UPLOAD_FILE_PATH` | no | — (use with `generate-file.ps1`; ignores `GIGABYTES_TO_UPLOAD`) |

\* Provide `CLIENT_PEM_FILE` for local runs, or `CLIENT_PEM` with the PEM file **base64-encoded** (`.NET` LargeFile only — for Azure Container Apps and similar).

### Authentication

Clients authenticate like the Bruno requests in `.bruno/Authentication/`:

1. Sign a short-lived JWT with `CLIENT_ID`, `CLIENT_KID`, your private key PEM, and `ORG_NO`
2. Exchange it for a Maskinporten access token at `test.maskinporten.no/token` (or prod)
3. Exchange that token for a 1-hour Altinn token at `{BASE_URL}/authentication/api/v1/exchange/maskinporten`

`ORG_NO` is the organisation number for the system user tied to your `CLIENT_ID` (used in the Maskinporten JWT and as the file-transfer sender). You must [set up a system user for that organisation](https://docs.altinn.studio/en/authorization/getting-started/systemuser/) before running the clients.

`CLIENT_PEM_FILE` is the path to your RSA private key PEM file (same key as Bruno's `client_pem`). For the `.NET` LargeFile client in Azure Container Apps, set `CLIENT_PEM` to the **base64-encoded** PEM file content (secret/env injection). Encode with e.g. `base64 -w0 private-key.pem` or PowerShell: `[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes((Get-Content -Raw key.pem)))`.

Optional: `MASKINPORTEN_TOKEN_URL` (override token endpoint).

See each client's `README.md` for language-specific setup and run instructions.

### Generate a test file (Windows)

```powershell
cd tests/tus
$env:UPLOAD_FILE_PATH = .\generate-file.ps1 512MB
```

`generate-file.ps1` writes random binary data to `%TEMP%` and returns the full path. Use it with `UPLOAD_FILE_PATH`:

```powershell
cd tests/tus
$env:UPLOAD_FILE_PATH = .\generate-file.ps1 512MB
cd Altinn.Broker.Tests.LargeFile      # dotnet run
# or Altinn.Broker.Tests.TusJsClient   # npm run upload
# or Altinn.Broker.Tests.TusPythonClient  # python src/main.py
```
