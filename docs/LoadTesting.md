## Load testing with k6
Before running tests you should mock external dependencies like:
- AltinnAuthorization by setting the function CheckUserAccess to return true
- AltinnRegisterService to return a string 
- AltinnResourceRegister to return a ResourceEntity
- Use the ConsoleLogEventBus instead of AltinnEventBus

Environment variables (same as use case tests):
- `base_url` – environment to test (e.g. `https://platform.tt02.altinn.no`)
- `mp_client_id`, `mp_kid`, `mp_client_pem` – Maskinporten client credentials
- `sender`, `recipient` – organization numbers used in token authorization details

Tokens are generated at runtime via Maskinporten and Altinn token exchange. The token helpers live in `tests/Altinn.Broker.LoadTests/helpers/` (copied from the use case tests helpers — keep them in sync if those change).

k6 option variables: 
- VUs: How many virtual users running tests at the same time. 
- iterations: how many tests TOTAL should be completed. vus/iterations=test per vus. 0 means infinite iterations for as long as the test will run. 
- httpDebug: full/summary. Outputs infomration about http requests and responses
- duration: How long the test should be running. The test also adds a 30 seconds graceful stop period on top of this. 

We run load tests using k6. To run without installing k6 you can use docker compose (set `base_url` to `http://host.docker.internal:5096` for local API):

```bash
cd tests/Altinn.Broker.LoadTests
base_url=https://platform.tt02.altinn.no \
mp_client_id=... mp_kid=... mp_client_pem=... \
sender=... recipient=... \
docker compose -f docker-compose-loadtest.yml up k6-test
```

If you have k6 installed locally, run from `tests/Altinn.Broker.LoadTests`:

```bash
k6 run -e base_url=https://platform.tt02.altinn.no \
  -e mp_client_id=... -e mp_kid=... -e mp_client_pem=... \
  -e sender=... -e recipient=... \
  test.js
```
