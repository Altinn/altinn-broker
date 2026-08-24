
# Broker frontend/"BrokerBox" (WORK-IN-PROGRESS)

This subfolder contains the code for the Broker frontend application that is intended to be linked to from Arbeidsflate, and permits organizations to easily and securely transfer large files to other organizations. It uses Broker TUS as its backend.

## Technology

It is a React app that directly connects to the Broker API from the browser, and uses ID-Porten for login.

## To start

1. cd frontend
2. npm install
3. Start the Broker API locally (HTTPS, default `https://localhost:7241`)
4. npm run dev
5. Open **https://localhost:5173** (not `http://`) and accept the Vite self-signed certificate

Vite serves the SPA over HTTPS and proxies `/broker` to the API so the session cookie stays first-party.

In deployed environments, set GitHub secret `VITE_API_BASE_URL` to the Broker API origin (e.g. `https://altinn-dev-api.azure-api.net`) so the SPA build calls APIM. Leave it empty only when the SPA and API are same-origin (Front Door routes `/broker` to the API).

Register this redirect URI on the ID-Porten client: `https://localhost:5173/broker/api/v1/authentication/callback`

Also register back-channel logout (Samarbeidsportalen → client → `backchannel_logout_uri`):

- Local API: `https://localhost:7241/broker/api/v1/authentication/backchannel-logout`
- Deployed (same site as a redirect URI): `https://<broker-host>/broker/api/v1/authentication/backchannel-logout`

ID-Porten POSTs a signed `logout_token` there when the user logs out of another public-sector service. The API revokes the session by `sid`; the next SPA request then sees the user as logged out.

`IdPortenSettings:SpaBaseUrl` is set to `https://localhost:5173` in Development so post-login redirects return to the SPA (not `https://localhost:7241`).

### Auth

- Unauthenticated users are redirected to `GET /broker/api/v1/authentication/login`
- Session is an httpOnly cookie; API calls use `credentials: "include"`
- Logout uses the header **Logg ut** button → `GET /broker/api/v1/authentication/logout`
- Mutations must send `X-Requested-With` (handled by `apiFetch`)

Optional: set `VITE_API_PROXY_TARGET` if the API runs on a different host/port.

## Goals

- [x] The user should be able to login with ID-Porten
- [ ] The user should be able to create and upload a file transfer on behalf of their organization
- [ ] The recipient should get a notification about a new file transfer
- [ ] The recipient should be able to download the file
- [ ] There should be a progress bar displaying the progress
- [ ] The design should be consistent with the Arbeidsflate such that the user does not experience it as a distinct application
- [ ] The user should be able to see information and metadata about current and historical file transfers
- [ ] The UI should give information about Broker resources the user has access to
- [ ] It must be universally accessible for everyone regardless of disability
- [ ] The design should be responsive so that it can be used on all common devices
