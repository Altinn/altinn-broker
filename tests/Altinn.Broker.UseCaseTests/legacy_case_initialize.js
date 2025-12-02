import http from 'k6/http';
import { check, sleep } from 'k6';
import crypto from 'k6/crypto';
import { buildLegacyInitializeFileTransferPayload, TEST_TAG_LEGACY } from './helpers/brokerPayloadBuilder.js';
import { cleanupUseCaseTestData } from './helpers/cleanupUseCaseTestsData.js';
import { getLegacyMaskinportenToken } from './helpers/maskinportenTokenService.js';

const baseUrl = __ENV.base_url;
const resourceId = 'bruksmonster-broker';
const isProduction = (baseUrl.toLowerCase().includes('platform.altinn.no') ? true : false);
// Legacy controller expects onBehalfOfConsumer as a string (org number)
const onBehalfOfConsumerSender = isProduction ? "orgnummerforprod" : "313896013";
const onBehalfOfConsumerRecipient = isProduction ? "orgnummerforprod" : "311167898"
export const options = {
    thresholds: {
        checks: ["rate==1"],
    },
    vus: 1,
    iterations: 1,
}

// Load fixture bytes at init context and precompute hash for verification
const fixtureBytes = open('./fixtures/usecase-broker-test-file.txt', 'b');
const fixtureHash = crypto.sha256(fixtureBytes, 'hex');

/**
 * TC1: Initialize a file transfer
 * TC2: Upload the initialized file transfer
 * TC3: Poll and verify successful upload
 * TC4: Search for the file as a recipient and find it
 * TC5: Download and verify correct bytes
 * TC6: Confirm download
 * TC7: Verify updated status
 * TC8: Get file overviews
 * TC9: Get files
 * Cleanup: Remove test data created during the test
 */

export default async function () {
    const { filetransferId } = await TC1_InitializeLegacyFileTransfer();
    await TC2_UploadLegacyFileTransfer(filetransferId);
    await TC3_LegacyPollAndVerifyUpload(filetransferId);
    await TC4_SearchLegacyFileAsRecipient(filetransferId);
    await TC5_LegacyDownloadAndVerifyBytes(filetransferId);
    await TC6_LegacyConfirmDownload(filetransferId);
    await TC7_LegacyVerifyUpdatedStatus(filetransferId);
    const { filetransferId2 } = await TC8_LegacyGetFileOverviews(filetransferId);
    await TC9_LegacyGetFiles(filetransferId, filetransferId2);

    // Cleanup test data
    await cleanupUseCaseTestData(TEST_TAG_LEGACY);
}

async function TC1_InitializeLegacyFileTransfer() {
    const token = await getLegacyMaskinportenToken();
    check(token, { 'Legacy token obtained': t => typeof t === 'string' && t.length > 0 });

    const recipient = isProduction ? "orgnummerforprod" : "311167898"
    const payload = buildLegacyInitializeFileTransferPayload(recipient);

    const headers = {
        'Authorization': `Bearer ${token}`,
        'Content-Type': 'application/json',
        'Accept': 'application/json'
    };

    const res = http.post(`${baseUrl}/broker/api/v1/legacy/file`, JSON.stringify(payload), { headers });
    check(res, { 'File transfer initialized status 200': r => r.status === 200 });
    if (res.status !== 200) {
        console.error(`File transfer initialization failed. Status: ${res.status}. Body: ${res.body}`);
        return { filetransferId: null };
    }

    let filetransferId = null;
    try {
        const response = res.json();
        if (response) {
            filetransferId = response;
        }
        check(filetransferId, { 'File transfer ID obtained': id => typeof id === 'string' && id.length > 0 });
    } catch (e) {
        console.error(`Error parsing initialization response: ${e.message}`);
    }

    console.log(`TC1: Initialize legacy file transfer completed`);
    return { filetransferId };
}

async function TC2_UploadLegacyFileTransfer(filetransferId) {

    if (!filetransferId) {
        console.error(`TC2 aborted: No filetransferId from TC1`);
        return;
    }

    const legacyToken = await getLegacyMaskinportenToken();
    check(legacyToken, { 'Legacy token obtained (upload)': t => typeof t === 'string' && t.length > 0 });
    const headers = {
        Authorization: `Bearer ${legacyToken}`,
        'Content-Type': 'application/octet-stream'
    };

    // Upload the exact fixture bytes so we can verify later
    const res = http.post(
        `${baseUrl}/broker/api/v1/legacy/file/${filetransferId}/upload?onBehalfOfConsumer=${encodeURIComponent(onBehalfOfConsumerSender)}`,
        fixtureBytes,
        { headers }
    );
    check(res, { 'File upload status 200': r => r.status === 200 });

    console.log(`TC2: Upload legacy file transfer completed`);
}

async function TC3_LegacyPollAndVerifyUpload(filetransferId) {
    if (!filetransferId) {
        console.error(`TC3 aborted: No filetransferId from TC1`);
        return;
    }

    const legacyToken = await getLegacyMaskinportenToken();
    check(legacyToken, { 'Legacy token obtained': t => typeof t === 'string' && t.length > 0 });

    const headers = {
        Authorization: `Bearer ${legacyToken}`
    }

    const maxRetries = 10;
    let published = false;
    let statusVal = null;
    let lastRes = null;

    const isPublished = (val) => {
        if (typeof val === 'number') return val === 3; // FileStatus.Published
        if (typeof val === 'string') return val.toLowerCase() === 'published';
        return false;
    };

    for (let attempt = 0; attempt <= maxRetries; attempt++) {
        sleep(1);
        lastRes = http.get(`${baseUrl}/broker/api/v1/legacy/file/${filetransferId}?onBehalfOfConsumer=${encodeURIComponent(onBehalfOfConsumerSender)}`, { headers });
        if (lastRes.status === 200) {
            try {
                statusVal = lastRes.json('fileStatus');
            } catch (e) {
                console.error(`TC3: Error parsing details response: ${e.message}`);
            }

            if (isPublished(statusVal)) {
                published = true;
                console.log(`TC3: File transfer is published on attempt ${attempt + 1}/${maxRetries} (status=${statusVal})`);
                break;
            } else {
                console.error(`TC3: Status not yet Published on attempt ${attempt + 1}/${maxRetries} (status=${statusVal})`);
            }
        }
        else if (lastRes.status === 404) {
            console.error(`TC3: File transfer not found during polling attempt ${attempt + 1}/${maxRetries}`);
        }
        else {
            console.error(`TC3: Failed to get information about the file transfer. Status: ${lastRes.status}. Body: ${lastRes.body}`);
        }
    }

    // Checks outside the loop
    if (lastRes && lastRes.status === 200) {
        check(lastRes, {
            'fileStatus Published': r => r.json('fileStatus') === 'Published'
        });
    }
    check(isPublished(statusVal), { 'fileStatus is Published (num|string)': v => v === true });
    check(published, { 'File transfer reached published status within 10s': p => p === true });
    console.log(`TC3: Poll and verify upload completed`);
}

async function TC4_SearchLegacyFileAsRecipient(filetransferId) {

    const legacyToken = await getLegacyMaskinportenToken();
    check(legacyToken, { 'Legacy token obtained': t => typeof t === 'string' && t.length > 0 });

    const headers = {
        Authorization: `Bearer ${legacyToken}`
    };

    const res = http.get(`${baseUrl}/broker/api/v1/legacy/file/${filetransferId}?onBehalfOfConsumer=${encodeURIComponent(onBehalfOfConsumerRecipient)}`, { headers });
    check(res, { 'File transfer found by recipient status 200': r => r.status === 200 });
    if (res.status !== 200) {
        console.error(`File transfer search by recipient failed. Status: ${res.status}. Body: ${res.body}`);
    }
    console.log(`TC4: Test case completed`);
}

async function TC5_LegacyDownloadAndVerifyBytes(filetransferId) {

    if (!filetransferId) {
        console.error(`TC5 aborted: No filetransferId from earlier steps`);
        return null;
    }

    const legacyToken = await getLegacyMaskinportenToken();
    check(legacyToken, { 'Legacy token obtained': t => typeof t === 'string' && t.length > 0 });

    const headers = {
        Authorization: `Bearer ${legacyToken}`
    };

    // Request binary to receive an ArrayBuffer for exact byte comparison
    const res = http.get(`${baseUrl}/broker/api/v1/legacy/file/${filetransferId}/download?onBehalfOfConsumer=${encodeURIComponent(onBehalfOfConsumerRecipient)}`, { headers, responseType: 'binary' });
    check(res, { 'File download status 200': r => r.status === 200 });
    if (res.status !== 200) {
        console.error(`File download failed. Status: ${res.status}. Body: ${res.body}`);
        return null;
    }

    // Verify downloaded content matches fixture via SHA-256
    const downloadedHash = crypto.sha256(res.body, 'hex');
    const lengthMatches = (res.body.byteLength === fixtureBytes.byteLength);

    check(downloadedHash, { 'Downloaded hash equals fixture': h => h === fixtureHash });
    check(lengthMatches, { 'Downloaded length equals fixture length': m => m === true });

    console.log(`TC5: Download and verify bytes completed`);
}

async function TC6_LegacyConfirmDownload(filetransferId) {

    const legacyToken = await getLegacyMaskinportenToken();
    check(legacyToken, { 'Legacy token obtained': t => typeof t === 'string' && t.length > 0 });

    const headers = {
        Authorization: `Bearer ${legacyToken}`
    };

    const res = http.post(`${baseUrl}/broker/api/v1/legacy/file/${filetransferId}/confirmdownload?onBehalfOfConsumer=${encodeURIComponent(onBehalfOfConsumerRecipient)}`, null, { headers });
    check(res, { 'Confirm download status 200': r => r.status === 200 });
    if (res.status !== 200) {
        console.error(`Confirm download failed. Status: ${res.status}. Body: ${res.body}`);
        return;
    }
    console.log(`TC6: Confirm download completed`);
}

async function TC7_LegacyVerifyUpdatedStatus(filetransferId) {

    const legacyToken = await getLegacyMaskinportenToken();
    check(legacyToken, { 'Legacy token obtained': t => typeof t === 'string' && t.length > 0 });

    const headers = {
        Authorization: `Bearer ${legacyToken}`
    };
    const res = http.get(`${baseUrl}/broker/api/v1/legacy/file/${filetransferId}?onBehalfOfConsumer=${encodeURIComponent(onBehalfOfConsumerRecipient)}`, { headers });
    check(res, { 'File transfer status after download status 200': r => r.status === 200 });
    if (res.status !== 200) {
        console.error(`Failed to get file transfer status after download. Status: ${res.status}. Body: ${res.body}`);
        return;
    }

    let statusAfterDownload = null;
    try {
        statusAfterDownload = res.json('fileStatus');
    } catch (e) {
        console.error(`Error parsing details response: ${e.message}`);
    }

    const isDownloaded = (val) => {
        if (typeof val === 'number') return val === 5;
        if (typeof val === 'string') return val.toLowerCase() === 'allconfirmeddownloaded';
        return false;
    };

    const downloaded = isDownloaded(statusAfterDownload);
    check(downloaded, { 'File transfer reached downloaded status': d => d === true });
    console.log(`TC7: Verify updated status completed`);
}

async function TC8_LegacyGetFileOverviews(filetransferId1) {
    const token = await getLegacyMaskinportenToken();
    const headersJson = { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json', Accept: 'application/json' };
    const recipient = isProduction ? 'orgnummerforprod' : '311167898';
    const payload = buildLegacyInitializeFileTransferPayload(recipient);

    const resInit2 = http.post(`${baseUrl}/broker/api/v1/legacy/file`, JSON.stringify(payload), { headers: headersJson });
    check(resInit2, { 'TC8 init2 200': r => r.status === 200 });
    const filetransferId2 = resInit2.json();

    const requestedIds = [filetransferId1, filetransferId2, filetransferId1];
    const url = `${baseUrl}/broker/api/v1/legacy/file/overviews?onBehalfOfConsumer=${encodeURIComponent(onBehalfOfConsumerSender)}`;
    const res = http.post(url, JSON.stringify(requestedIds), { headers: headersJson });
    check(res, { 'Overviews 200': r => r.status === 200 });

    const models = res.json();
    const returnedIds = models.map(m => m.fileId || m.fileTransferId);

    const uniqRequested = Array.from(new Set([filetransferId1, filetransferId2]));
    const uniqReturned = Array.from(new Set(returnedIds));

    check(uniqReturned, {
        'Contains id1': ids => ids.includes(filetransferId1),
        'Contains id2': ids => ids.includes(filetransferId2),
        'No duplicates in response': ids => ids.length === returnedIds.length,
        'Exact set match': ids => ids.length === uniqRequested.length && ids.every(id => uniqRequested.includes(id)),
    });
    console.log(`TC8: Get file overviews completed`);
    return { filetransferId2 };
}

async function TC9_LegacyGetFiles(filetransferId1, filetransferId2) {
    const token = await getLegacyMaskinportenToken();
    check(token, { 'TC9 token obtained': t => typeof t === 'string' && t.length > 0 });

    const headersJson = { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json', Accept: 'application/json' };
    // Scope to sender perspective with a narrow time window to avoid unrelated results in shared environments
    const url = `${baseUrl}/broker/api/v1/legacy/file?recipients=${encodeURIComponent(onBehalfOfConsumerRecipient)}`;
    const res = http.get(url, { headers: headersJson });
    check(res, { 'GetFiles (GET) 200': r => r.status === 200 });

    const models = res.json();
    const returnedIds = Array.isArray(models) ? models : [];

    check(returnedIds, {
        'TC9 contains id1': ids => ids.includes(filetransferId1),
        'TC9 contains id2': ids => ids.includes(filetransferId2)
    });

    // Status filter: AllConfirmedDownloaded. Expect downloaded file (filetransferId1) to be present and the new one (filetransferId2) to be absent.
    const urlDownloaded = `${baseUrl}/broker/api/v1/legacy/file?recipients=${encodeURIComponent(onBehalfOfConsumerRecipient)}&status=AllConfirmedDownloaded`;
    const resDownloaded = http.get(urlDownloaded, { headers: headersJson });
    check(resDownloaded, { 'GetFiles (GET) AllConfirmedDownloaded 200': r => r.status === 200 });
    const modelsDownloaded = resDownloaded.json();
    const returnedDownloadedIds = Array.isArray(modelsDownloaded) ? modelsDownloaded : [];
    check(returnedDownloadedIds, {
        'TC9 AllConfirmedDownloaded: contains id1': ids => ids.includes(filetransferId1),
        'TC9 AllConfirmedDownloaded: does not contain id2': ids => !ids.includes(filetransferId2)
    });
    console.log('TC9: Get files completed');
}