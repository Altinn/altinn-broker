# Testing large file

In order to test upload of large files you can use the `tests/tus/Altinn.Broker.Tests.LargeFile` console application. Configure Maskinporten credentials (`CLIENT_ID`, `CLIENT_KID`, and either `CLIENT_PEM_FILE` or `CLIENT_PEM`), `ORG_NO`, and `RESOURCE_ID` as described in `tests/tus/README.md`. Ensure you have [set up a system user for your organisation](https://docs.altinn.studio/en/authorization/getting-started/systemuser/) linked to your Maskinporten client, then run the project.

If you want to test it from another environment, build and run the container from the repository root:

```bash
docker build -f tests/tus/Altinn.Broker.Tests.LargeFile/Dockerfile -t altinn-broker-largefile .
```

**Local / file mount** — mount the PEM and set `CLIENT_PEM_FILE`:

```bash
docker run --rm \
  -v /path/to/key.pem:/run/secrets/client.pem:ro \
  -e CLIENT_ID=... \
  -e CLIENT_KID=... \
  -e CLIENT_PEM_FILE=/run/secrets/client.pem \
  -e ORG_NO=... \
  -e RESOURCE_ID=... \
  -e BASE_URL=... \
  altinn-broker-largefile
```

**Azure Container App** — inject the base64-encoded PEM as a secret/env var (`CLIENT_PEM`); no file mount required:

```bash
# Prepare once: base64 -w0 private-key.pem
docker run --rm \
  -e CLIENT_ID=... \
  -e CLIENT_KID=... \
  -e CLIENT_PEM=LS0tLS1CRUdJTi... \
  -e ORG_NO=... \
  -e RESOURCE_ID=... \
  -e BASE_URL=... \
  altinn-broker-largefile
```

Set the remaining environment variables listed in `tests/tus/README.md`.
