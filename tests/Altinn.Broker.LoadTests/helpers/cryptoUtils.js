import encoding from 'k6/encoding';

export function pemToBinary(pem) {
    const base64 = (pem || '')
        .replace(/-----BEGIN[\s\S]*?-----/g, '')
        .replace(/-----END[\s\S]*?-----/g, '')
        .replace(/\s+/g, '');
    return encoding.b64decode(base64, 'std');
}

// Converts a string to bytes (Uint8Array)
// Note: This assumes ASCII-only input (works for base64url JWT strings)
export function stringToBytes(str) {
    const arr = new Uint8Array(str.length);
    for (let i = 0; i < str.length; i++) {
        arr[i] = str.charCodeAt(i);
    }
    return arr;
}