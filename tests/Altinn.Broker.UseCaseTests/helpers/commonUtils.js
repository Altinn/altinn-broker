export function toUrn(id) {
    if (!id) return '';
    if (id.includes(':')) return id;
    if (/^\d{9}$/.test(id)) return `urn:altinn:organization:identifier-no:${id}`;
    return id;
}