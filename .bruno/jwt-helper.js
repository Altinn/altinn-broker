const crypto = require('crypto');

function generateJWT(clientId, clientKid, clientPem, scope, authDetails = null) {
    const header = {
        "alg": "RS256",
        "kid": clientKid
    };

    const payload = {
        "aud": "https://test.maskinporten.no/",
        "scope": scope,
        "iss": clientId,
        "iat": Math.floor(Date.now() / 1000),
        "exp": Math.floor(Date.now() / 1000) + 120
    };

    if (authDetails) {
        payload.authorization_details = authDetails;
    }

    function base64url(input) {
        return Buffer.from(input).toString('base64')
            .replace(/\+/g, '-')
            .replace(/\//g, '_')
            .replace(/=/g, '');
    }

    const encodedHeader = base64url(JSON.stringify(header));
    const encodedPayload = base64url(JSON.stringify(payload));
    const signatureInput = `${encodedHeader}.${encodedPayload}`;

    const signature = crypto.sign("RSA-SHA256", Buffer.from(signatureInput), clientPem);
    const encodedSignature = base64url(signature);

    return `${signatureInput}.${encodedSignature}`;
}

module.exports = { generateJWT };
