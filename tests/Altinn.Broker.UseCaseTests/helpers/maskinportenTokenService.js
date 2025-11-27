import http from 'k6/http';
import encoding from 'k6/encoding';
import { buildMaskinportenJwt } from './maskinportenJwtBuilder.js';

const maskinportenUrl = (__ENV.base_url.toLowerCase().includes('platform.altinn.no'))
    ? 'https://maskinporten.no/token'
    : 'https://test.maskinporten.no/token';

export async function retrieveMaskinportenToken({ clientId, kid, pem, scope, isSender }) {
    const jwt = await buildMaskinportenJwt({ clientId, kid, pem, scope, tokenUrl: maskinportenUrl , isSender});
    const url = maskinportenUrl;
    const headers = { 'Content-Type': 'application/x-www-form-urlencoded', 'Accept': 'application/json' };
    const body = Object.entries({ grant_type: 'urn:ietf:params:oauth:grant-type:jwt-bearer', assertion: jwt })
        .map(([k, v]) => encodeURIComponent(k) + '=' + encodeURIComponent(v)).join('&');
    const res = http.post(url, body, { headers });
    if (res.status !== 200) {
        throw new Error(`Maskinporten token failed: status=${res.status} body=${res.body}`);
    }
    return res.json('access_token');
}

export async function getMaintenanceMaskinportenToken() {
    const pem = __ENV.mp_client_pem_b64
        ? encoding.b64decode(__ENV.mp_client_pem_b64, 'std', 's').toString()
        : __ENV.mp_client_pem;
    return await retrieveMaskinportenToken({
        clientId: __ENV.mp_client_id,
        kid: __ENV.mp_kid,
        pem,
        scope: 'altinn:broker.maintenance',
    });
}