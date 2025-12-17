import encoding from 'k6/encoding';
import { pemToBinary, stringToBytes } from './cryptoUtils.js';

const sender = __ENV.sender;
const recipient = __ENV.recipient;

export async function buildMaskinportenJwt({ clientId, kid, pem, scope, tokenUrl, isSender }) {
    const isProduction = !tokenUrl.includes('test');
    const now = Math.floor(Date.now() / 1000);
    const header = { alg: 'RS256', typ: 'JWT', kid };
    const payload = {
        aud: tokenUrl,
        scope: scope,
        iss: clientId,
        sub: clientId,
        authorization_details: [
            {
                type: "urn:altinn:systemuser",
                systemuser_org:
                    {
                        authority : "iso6523-actorid-upis",
                        ID: isProduction ? (isSender ? sender : recipient) : (isSender ? "313896013" : "311167898")
                    }
            }
        ],
        iat: now,
        nbf: now - 5,
        exp: now + 120,
        jti: `${now}-${Math.random().toString(36).slice(2)}`
    };

    const encodedHeader = encoding.b64encode(JSON.stringify(header), 'url');
    const encodedPayload = encoding.b64encode(JSON.stringify(payload), 'url');
    const signingInput = `${encodedHeader}.${encodedPayload}`;

    const keyData = pemToBinary(pem);
    const key = await crypto.subtle.importKey(
        'pkcs8',
        keyData,
        { name: 'RSASSA-PKCS1-v1_5', hash: 'SHA-256' },
        false,
        ['sign']
    );

    const data = stringToBytes(signingInput);
    const signature = await crypto.subtle.sign({ name: 'RSASSA-PKCS1-v1_5' }, key, data);
    const encodedSignature = encoding.b64encode(new Uint8Array(signature), 'url');
    return `${signingInput}.${encodedSignature}`;
}