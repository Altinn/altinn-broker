import http from 'k6/http';
import { retrieveMaskinportenToken } from './maskinportenTokenService.js';

const authorizationBaseUrl = (__ENV.base_url.toLowerCase().includes('platform.altinn.no'))
    ? 'https://platform.altinn.no'
    : 'https://platform.tt02.altinn.no';

async function retrieveAltinnToken({ baseUrl, clientId, kid, pem, scope, isSender }) {
    const mpToken = await retrieveMaskinportenToken({ clientId, kid, pem, scope, isSender });
    const headers = { 'Authorization': `Bearer ${mpToken}`, 'Accept': 'application/json' };
    const url = `${(baseUrl || '').replace(/\/$/, '')}/authentication/api/v1/exchange/maskinporten`;
    const res = http.get(url, { headers });
    if (res.status !== 200) {
        throw new Error(`Altinn exchange failed: status=${res.status} body=${res.body}`);
    }
    const parsed = res.json();
    if (typeof parsed === 'string') return parsed.replace(/^"|"$/g, '');
    if (parsed && typeof parsed === 'object' && parsed.access_token) return parsed.access_token;
    throw new Error(`Altinn exchange failed: status=${res.status} body=${res.body}`);
}

export async function getSenderAltinnToken() {
    return await retrieveAltinnToken({
        baseUrl: authorizationBaseUrl,
        clientId: __ENV.mp_client_id,
        kid: __ENV.mp_kid,
        pem: __ENV.mp_client_pem,
        scope: 'altinn:broker.write',
        isSender: true
    });
}

export async function getRecipientAltinnToken() {
    return await retrieveAltinnToken({
        baseUrl: authorizationBaseUrl,
        clientId: __ENV.mp_client_id,
        kid: __ENV.mp_kid,
        pem: __ENV.mp_client_pem,
        scope: 'altinn:broker.read altinn:serviceowner',
        isSender: false
    });
}