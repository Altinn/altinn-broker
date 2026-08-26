
# Broker frontend/"BrokerBox" (WORK-IN-PROGRESS)

This subfolder contains the code for the Broker frontend application that is intended to be linked to from Arbeidsflate, and permits organizations to easily and securely transfer large files to other organizations. It uses Broker TUS as its backend.

## Technology

It is a React app that directly connects to the Broker API from the browser. End-user login is handled by the Broker API (BFF pattern): the browser never holds ID-Porten or Altinn tokens in JavaScript.

## To start

1. cd frontend
2. npm install
3. Start the Broker API locally (HTTPS, default `https://localhost:7241`)
4. npm run dev
5. Open **https://localhost:5173** (not `http://`) and accept the Vite self-signed certificate

Vite serves the SPA over HTTPS and proxies `/broker` to the API so the session cookie stays first-party.

Optional: set `VITE_API_PROXY_TARGET` if the API runs on a different host/port.

## Authentication

The API supports two end-user authentication modes. Both expose the same SPA-facing endpoints under `/broker/api/v1/authentication/`.

| Mode | API module | When to use |
|------|------------|-------------|
| **ID-Porten direct** | `IdPortenDirectAuth` | Local dev, Front Door + APIM (today), any host without shared Altinn portal session |
| **Altinn platform SSO** | `AltinnPlatformAuth` | Future hosting on `*.altinn.no` (e.g. `bb.ui.altinn.no`) next to Arbeidsflate (`af.altinn.no`) |

### ID-Porten direct (current default)

Broker acts as an ID-Porten confidential client: OIDC code flow, server-side Altinn token exchange, httpOnly session cookie.

**SPA flow**

1. App loads → `GET /broker/api/v1/authentication/me`
2. Not authenticated → redirect to `GET /broker/api/v1/authentication/login?returnUrl=…`
3. ID-Porten callback → `POST /broker/api/v1/authentication/callback` (form_post)
4. Session cookie set; SPA loads protected routes
5. Logout → `GET /broker/api/v1/authentication/logout`

**SPA behaviour**

- Unauthenticated users are redirected to `/broker/api/v1/authentication/login`
- Session is an httpOnly cookie; API calls use `credentials: "include"`
- Logout uses the header **Logg ut** button → `/broker/api/v1/authentication/logout`
- Mutations must send `X-Requested-With` (handled by `apiFetch`)

**ID-Porten client registration**

Register redirect URI (must match deployed host + path):

`https://<broker-host>/broker/api/v1/authentication/callback`

Register back-channel logout (`backchannel_logout_uri`):

- Local API: `https://localhost:7241/broker/api/v1/authentication/backchannel-logout`
- Deployed: `https://<broker-host>/broker/api/v1/authentication/backchannel-logout`

The callback must be published as **POST** in APIM (ID-Porten uses `response_mode=form_post`).

### Altinn platform SSO (future `*.altinn.no`)

When the SPA runs on the same cookie domain as Arbeidsflate (e.g. `.altinn.no`), users who are already logged into Altinn do not need a separate ID-Porten redirect.

**SPA flow**

1. App loads → `GET /broker/api/v1/authentication/me`
2. Not authenticated → `GET /broker/api/v1/authentication/refresh` (forwards existing Altinn portal cookies to platform)
3. API sets the Altinn runtime JWT cookie; `/me` returns authenticated
4. If no platform session exists, fall back to ID-Porten direct login (or redirect to Altinn login — TBD in SPA)

This mirrors how other `*.ui.altinn.no` apps reuse the shared Altinn session.

### Shared API endpoints

| Endpoint | Purpose |
|----------|---------|
| `GET /broker/api/v1/authentication/me` | Session probe (always 200; `authenticated: true/false`) |
| `GET /broker/api/v1/authentication/refresh` | Refresh Altinn JWT from platform session cookie |
| `GET /broker/api/v1/authentication/login` | Start ID-Porten login (IdPortenDirectAuth) |
| `POST /broker/api/v1/authentication/callback` | OIDC callback (IdPortenDirectAuth) |
| `GET /broker/api/v1/authentication/logout` | Logout (IdPortenDirectAuth) |

## Configuration

Settings live in the Broker API (`appsettings.json`, `appsettings.Development.json`, or environment variables).

### `IdPortenSettings` (IdPortenDirectAuth)

Used for direct ID-Porten login. Section name is unchanged for existing deployments.

| Setting | Required | Description |
|---------|----------|-------------|
| `Authority` | Yes | ID-Porten issuer (e.g. `https://test.idporten.no`) |
| `ClientId` | Yes | ID-Porten client id (Key Vault secret in deploy) |
| `ClientSecret` | Yes | ID-Porten client secret (Key Vault secret in deploy) |
| `Scopes` | Yes | Must include at least one `altinn:*` scope (e.g. `altinn:portal/enduser`) |
| `SpaBaseUrl` | Dev / split-origin | Public SPA origin (e.g. `https://localhost:5173`). OIDC callback and post-login redirect use this host. Leave empty when SPA and API share the same origin (Front Door + APIM). |
| `CookieName` | No | Broker session cookie name (default `AltinnBrokerSession`) |

Fixed in code (not configurable): callback `/broker/api/v1/authentication/callback`, front-channel logout `/broker/api/v1/authentication/frontchannel-logout`, back-channel logout `/broker/api/v1/authentication/backchannel-logout`, post-logout redirect `/`, required ACR `idporten-loa-substantial`, session lifetime 60 minutes.

**Deploy environment variables** (Container App):

- `IdPortenSettings__Authority` ← `IDPORTEN_AUTHORITY`
- `IdPortenSettings__ClientId` ← Key Vault
- `IdPortenSettings__ClientSecret` ← Key Vault
- `IdPortenSettings__SpaBaseUrl` ← `FRONTEND_BASE_URL`

**Local Development example** (`appsettings.local.json`):

```json
"IdPortenSettings": {
  "Authority": "https://test.idporten.no",
  "ClientId": "<from Samarbeidsportalen>",
  "ClientSecret": "<from Samarbeidsportalen>",
  "Scopes": ["openid", "profile", "altinn:portal/enduser"],
  "SpaBaseUrl": "https://localhost:5173"
}
```

### `AltinnPlatformAuth`

Used for shared Altinn portal session on `*.altinn.no`.

| Setting | Required | Description |
|---------|----------|-------------|
| `JwtCookieName` | Yes | Name of the httpOnly JWT cookie set after refresh (default `AltinnStudioRuntime`) |
| `CookieDomain` | Prod on `*.altinn.no` | Cookie `Domain` for shared hosts (e.g. `.altinn.no`). Leave empty for host-only cookies (local dev). |

**Deploy environment variables**:

- `AltinnPlatformAuth__JwtCookieName`
- `AltinnPlatformAuth__CookieDomain` (e.g. `.altinn.no`)

**Example for `bb.ui.altinn.no`:**

```json
"AltinnPlatformAuth": {
  "JwtCookieName": "AltinnStudioRuntime",
  "CookieDomain": ".altinn.no"
}
```

### `AltinnOptions` (required for both modes)

Token exchange (ID-Porten) and platform refresh (SSO) call Altinn Authentication.

| Setting | Required | Description |
|---------|----------|-------------|
| `PlatformGatewayUrl` | Yes | Altinn platform base URL (e.g. `https://platform.tt02.altinn.no/`) |
| `OpenIdWellKnown` | Yes | Altinn JWT validation metadata URL |
| `PlatformSubscriptionKey` | APIM envs | APIM subscription key for platform calls |

## Azure / Front Door deployment

In Azure, Front Door can proxy API and SPA on one origin:

1. Set GitHub secret `API_ORIGIN_HOST_NAME` to the APIM host (e.g. `altinn-dev-api.azure-api.net`)
2. Leave `VITE_API_BASE_URL` empty so the SPA uses same-origin `/broker/...` URLs
3. Set `FRONTEND_BASE_URL` → `IdPortenSettings__SpaBaseUrl` (Front Door URL from the deploy log)
4. Register ID-Porten redirect URI: `https://<front-door-host>/broker/api/v1/authentication/callback`

Front Door endpoint names are globally unique. The name is derived from `AZURE_NAME_PREFIX`. After deploy, use the hostname printed in the workflow log for `FRONTEND_BASE_URL` and ID-Porten.

Verify routing after deploy:

```bash
curl -sI "https://<front-door-host>/broker/api/v1/authentication/me"
# Expect Content-Type: application/json (not text/html from static storage)
```

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
