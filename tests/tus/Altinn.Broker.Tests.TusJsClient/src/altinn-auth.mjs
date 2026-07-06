import { createSign } from 'node:crypto';
import { existsSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';

const BROKER_WRITE_SCOPE = 'altinn:broker.write';

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

function readPrivateKeyPem() {
  const filePath = requireEnv('CLIENT_PEM_FILE');
  const resolvedPath = resolve(filePath);
  if (!existsSync(resolvedPath)) {
    throw new Error(`CLIENT_PEM_FILE does not exist or is not a file: ${filePath}`);
  }

  return readFileSync(resolvedPath, 'utf8');
}

function isProductionPlatform(baseUrl) {
  return (
    baseUrl.toLowerCase().includes('platform.altinn.no') &&
    !baseUrl.toLowerCase().includes('tt02')
  );
}

function resolveMaskinportenTokenUrl(baseUrl) {
  const override = process.env.MASKINPORTEN_TOKEN_URL;
  if (override) {
    return override;
  }

  return isProductionPlatform(baseUrl)
    ? 'https://maskinporten.no/token'
    : 'https://test.maskinporten.no/token';
}

function getMaskinportenAudience(tokenUrl) {
  const uri = new URL(tokenUrl);
  return `${uri.protocol}//${uri.host}/`;
}

function base64url(value) {
  return Buffer.from(value).toString('base64url');
}

function createMaskinportenClientAssertion({ clientId, clientKid, privateKeyPem, orgNumber, tokenUrl }) {
  const now = Math.floor(Date.now() / 1000);
  const header = { alg: 'RS256', kid: clientKid };
  const payload = {
    aud: getMaskinportenAudience(tokenUrl),
    scope: BROKER_WRITE_SCOPE,
    iss: clientId,
    iat: now,
    exp: now + 120,
    authorization_details: [
      {
        type: 'urn:altinn:systemuser',
        systemuser_org: {
          authority: 'iso6523-actorid-upis',
          ID: orgNumber,
        },
      },
    ],
  };

  const encodedHeader = base64url(JSON.stringify(header));
  const encodedPayload = base64url(JSON.stringify(payload));
  const signatureInput = `${encodedHeader}.${encodedPayload}`;
  const signature = createSign('RSA-SHA256').update(signatureInput).sign(privateKeyPem);
  return `${signatureInput}.${base64url(signature)}`;
}

function parseAltinnExchangeToken(body) {
  const trimmed = body.trim();
  if (trimmed.startsWith('eyJ')) {
    return trimmed;
  }

  if (trimmed.startsWith('"')) {
    return JSON.parse(trimmed);
  }

  const parsed = JSON.parse(trimmed);
  if (typeof parsed === 'string') {
    return parsed;
  }

  if (parsed?.access_token) {
    return parsed.access_token;
  }

  throw new Error(`Unexpected Altinn token exchange response: ${body}`);
}

export function readAuthOptionsFromEnvironment() {
  return {
    baseUrl: readEnv('BASE_URL', 'https://platform.tt02.altinn.no'),
    clientId: requireEnv('CLIENT_ID'),
    clientKid: requireEnv('CLIENT_KID'),
    clientPrivateKeyPem: readPrivateKeyPem(),
    orgNumber: requireEnv('ORG_NO'),
    maskinportenTokenUrl: process.env.MASKINPORTEN_TOKEN_URL ?? null,
  };
}

async function requestMaskinportenToken(options) {
  const tokenUrl = options.maskinportenTokenUrl ?? resolveMaskinportenTokenUrl(options.baseUrl);
  const assertion = createMaskinportenClientAssertion({
    clientId: options.clientId,
    clientKid: options.clientKid,
    privateKeyPem: options.clientPrivateKeyPem,
    orgNumber: options.orgNumber,
    tokenUrl,
  });

  const body = new URLSearchParams({
    grant_type: 'urn:ietf:params:oauth:grant-type:jwt-bearer',
    assertion,
  });

  for (let attempt = 0; attempt < 2; attempt += 1) {
    const response = await fetch(tokenUrl, {
      method: 'POST',
      headers: {
        Accept: 'application/json',
        'Content-Type': 'application/x-www-form-urlencoded',
      },
      body,
    });

    const payload = await response.json().catch(() => ({}));
    if (response.status !== 503 || attempt === 1) {
      if (!response.ok || !payload.access_token) {
        throw new Error(
          `Maskinporten token request failed. Status=${response.status}. ` +
            `Error=${payload.error}. Description=${payload.error_description}`,
        );
      }

      return payload.access_token;
    }

    await new Promise((resolve) => setTimeout(resolve, 1000));
  }

  throw new Error('Maskinporten token request failed after retry.');
}

async function exchangeMaskinportenToken(baseUrl, maskinportenToken) {
  const exchangeUrl = `${baseUrl.replace(/\/$/, '')}/authentication/api/v1/exchange/maskinporten`;
  const response = await fetch(exchangeUrl, {
    method: 'GET',
    headers: {
      Authorization: `Bearer ${maskinportenToken}`,
      Accept: 'application/json',
    },
  });

  const body = await response.text();
  if (!response.ok || !body.trim()) {
    throw new Error(`Altinn token exchange failed. Status=${response.status}. Body=${body}`);
  }

  return parseAltinnExchangeToken(body);
}

export async function exchangeAltinnToken(options) {
  const maskinportenToken = await requestMaskinportenToken(options);
  return exchangeMaskinportenToken(options.baseUrl, maskinportenToken);
}
