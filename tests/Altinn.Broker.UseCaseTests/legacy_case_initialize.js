import http from 'k6/http';
import { check, sleep } from 'k6';
import crypto from 'k6/crypto';
import { buildLegacyInitializeFileTransferPayload, TEST_TAG_LEGACY } from './helpers/brokerPayloadBuilder.js';
import { cleanupUseCaseTestData } from './helpers/cleanupUseCaseTestsData.js';
import { getLegacyMaskinportenToken } from './helpers/maskinportenTokenService.js';

const baseUrl = __ENV.base_url;
const recipient = __ENV.recipient;
const sender = __ENV.sender;
// Legacy controller expects onBehalfOfConsumer as a string (org number)
const onBehalfOfConsumerSender = sender;
const onBehalfOfConsumerRecipient = recipient;
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
    let filetransferId = null;
    let filetransferId2 = null;

    try {
    ({ filetransferId } = await TC1_InitializeLegacyFileTransfer());
    await TC2_UploadLegacyFileTransfer(filetransferId);
    await TC3_LegacyPollAndVerifyUpload(filetransferId);
    await TC4_SearchLegacyFileAsRecipient(filetransferId);
    await TC5_LegacyDownloadAndVerifyBytes(filetransferId);
    await TC6_LegacyConfirmDownload(filetransferId);
    await TC7_LegacyVerifyUpdatedStatus(filetransferId);
    ({ filetransferId2 } = await TC8_LegacyGetFileOverviews(filetransferId));
    await TC9_LegacyGetFiles(filetransferId, filetransferId2);
    } catch (e) {
        check(false, { 'No exceptions in test execution': () => false });
        throw e;
    } 
    await cleanupUseCaseTestData(TEST_TAG_LEGACY);
}

async function TC1_InitializeLegacyFileTransfer() {
    const token = await getLegacyMaskinportenToken();
    check(token, { 'Legacy token obtained': t => typeof t === 'string' && t.length > 0 });

    const payload = buildLegacyInitializeFileTransferPayload(recipient);

    const headers = {
        'Authorization': `Bearer ${token}`,
        'Content-Type': 'application/json',
        'Accept': 'application/json'
    };

    const response = http.post(`${baseUrl}/broker/api/v1/legacy/file`, JSON.stringify(payload), { headers });
    check(response, { 'File transfer initialized status 200': r => r.status === 200 });
    if (response.status !== 200) {
        console.error(`File transfer initialization failed. Status: ${response.status}. Body: ${response.body}`);
        return { filetransferId: null };
    }

    let filetransferId = null;
    try {
        const jsonResponse = response.json();
        if (jsonResponse) {
            filetransferId = jsonResponse;
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
    const response = http.post(
        `${baseUrl}/broker/api/v1/legacy/file/${filetransferId}/upload?onBehalfOfConsumer=${encodeURIComponent(onBehalfOfConsumerSender)}`,
        fixtureBytes,
        { headers }
    );
    check(response, { 'File upload status 200': r => r.status === 200 });

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
    let statusValue = null;
    let lastResponse = null;

    const isPublished = (value) => {
        if (typeof value === 'number') return value === 3; // FileStatus.Published
        if (typeof value === 'string') return value.toLowerCase() === 'published';
        return false;
    };

    for (let attempt = 0; attempt < maxRetries; attempt++) {
        sleep(10);
        lastResponse = http.get(`${baseUrl}/broker/api/v1/legacy/file/${filetransferId}?onBehalfOfConsumer=${encodeURIComponent(onBehalfOfConsumerSender)}`, { headers });
        if (lastResponse.status === 200) {
            try {
                statusValue = lastResponse.json('fileStatus');
            } catch (e) {
                console.error(`TC3: Error parsing details response: ${e.message}`);
            }

            if (isPublished(statusValue)) {
                published = true;
                console.log(`TC3: File transfer is published on attempt ${attempt + 1}/${maxRetries} (status=${statusValue})`);
                break;
            } else {
                console.error(`TC3: Status not yet Published on attempt ${attempt + 1}/${maxRetries} (status=${statusValue})`);
            }
        }
        else if (lastResponse.status === 404) {
            console.error(`TC3: File transfer not found during polling attempt ${attempt + 1}/${maxRetries}`);
        }
        else {
            console.error(`TC3: Failed to get information about the file transfer. Status: ${lastResponse.status}. Body: ${lastResponse.body}`);
        }
    }

    // Checks outside the loop
    if (lastResponse && lastResponse.status === 200) {
        check(lastResponse, {
            'fileStatus Published': r => isPublished(r.json('fileStatus'))
        });
    }
    check(published, { 'File transfer reached published status within 30s': p => p === true });
    console.log(`TC3: Poll and verify upload completed`);
}

async function TC4_SearchLegacyFileAsRecipient(filetransferId) {

    const legacyToken = await getLegacyMaskinportenToken();
    check(legacyToken, { 'Legacy token obtained': t => typeof t === 'string' && t.length > 0 });

    const headers = {
        Authorization: `Bearer ${legacyToken}`
    };

    const response = http.get(`${baseUrl}/broker/api/v1/legacy/file/${filetransferId}?onBehalfOfConsumer=${encodeURIComponent(onBehalfOfConsumerRecipient)}`, { headers });
    check(response, { 'File transfer found by recipient status 200': r => r.status === 200 });
    if (response.status !== 200) {
        console.error(`File transfer search by recipient failed. Status: ${response.status}. Body: ${response.body}`);
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
    const response = http.get(`${baseUrl}/broker/api/v1/legacy/file/${filetransferId}/download?onBehalfOfConsumer=${encodeURIComponent(onBehalfOfConsumerRecipient)}`, { headers, responseType: 'binary' });
    check(response, { 'File download status 200': r => r.status === 200 });
    if (response.status !== 200) {
        console.error(`File download failed. Status: ${response.status}. Body: ${response.body}`);
        return null;
    }

    // Verify downloaded content matches fixture via SHA-256
    const downloadedHash = crypto.sha256(response.body, 'hex');
    const lengthMatches = (response.body.byteLength === fixtureBytes.byteLength);

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

    const response = http.post(`${baseUrl}/broker/api/v1/legacy/file/${filetransferId}/confirmdownload?onBehalfOfConsumer=${encodeURIComponent(onBehalfOfConsumerRecipient)}`, null, { headers });
    check(response, { 'Confirm download status 200': r => r.status === 200 });
    if (response.status !== 200) {
        console.error(`Confirm download failed. Status: ${response.status}. Body: ${response.body}`);
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
    const response = http.get(`${baseUrl}/broker/api/v1/legacy/file/${filetransferId}?onBehalfOfConsumer=${encodeURIComponent(onBehalfOfConsumerRecipient)}`, { headers });
    check(response, { 'File transfer status after download status 200': r => r.status === 200 });
    if (response.status !== 200) {
        console.error(`Failed to get file transfer status after download. Status: ${response.status}. Body: ${response.body}`);
        return;
    }

    let statusAfterDownload = null;
    try {
        statusAfterDownload = response.json('fileStatus');
    } catch (e) {
        console.error(`Error parsing details response: ${e.message}`);
    }

    const isDownloaded = (value) => {
        if (typeof value === 'number') return value === 5;
        if (typeof value === 'string') return value.toLowerCase() === 'allconfirmeddownloaded';
        return false;
    };

    const downloaded = isDownloaded(statusAfterDownload);
    check(downloaded, { 'File transfer reached downloaded status': d => d === true });
    console.log(`TC7: Verify updated status completed`);
}

async function TC8_LegacyGetFileOverviews(filetransferId1) {
    const token = await getLegacyMaskinportenToken();
    const headersJson = { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json', Accept: 'application/json' };
    const payload = buildLegacyInitializeFileTransferPayload(recipient);

    const responseInitialize2 = http.post(`${baseUrl}/broker/api/v1/legacy/file`, JSON.stringify(payload), { headers: headersJson });
    check(responseInitialize2, { 'TC8 initialize2 200': r => r.status === 200 });
    const filetransferId2 = responseInitialize2.json();

    const requestedIds = [filetransferId1, filetransferId2, filetransferId1];
    const url = `${baseUrl}/broker/api/v1/legacy/file/overviews?onBehalfOfConsumer=${encodeURIComponent(onBehalfOfConsumerSender)}`;
    const response = http.post(url, JSON.stringify(requestedIds), { headers: headersJson });
    check(response, { 'Overviews 200': r => r.status === 200 });

    const models = response.json();
    const returnedIds = models.map(m => m.fileId || m.fileTransferId);

    const uniqueRequested = Array.from(new Set([filetransferId1, filetransferId2]));
    const uniqueReturned = Array.from(new Set(returnedIds));

    check(uniqueReturned, {
        'Contains id1': ids => ids.includes(filetransferId1),
        'Contains id2': ids => ids.includes(filetransferId2),
        'No duplicates in response': ids => ids.length === returnedIds.length,
        'Exact set match': ids => ids.length === uniqueRequested.length && ids.every(id => uniqueRequested.includes(id)),
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
    const response = http.get(url, { headers: headersJson });
    check(response, { 'GetFiles (GET) 200': r => r.status === 200 });

    const models = response.json();
    const returnedIds = Array.isArray(models) ? models : [];

    check(returnedIds, {
        'TC9 contains id1': ids => ids.includes(filetransferId1),
        'TC9 contains id2': ids => ids.includes(filetransferId2)
    });

    // Status filter: AllConfirmedDownloaded. Expect downloaded file (filetransferId1) to be present and the new one (filetransferId2) to be absent.
    const urlDownloaded = `${baseUrl}/broker/api/v1/legacy/file?recipients=${encodeURIComponent(onBehalfOfConsumerRecipient)}&status=AllConfirmedDownloaded`;
    const responseDownloaded = http.get(urlDownloaded, { headers: headersJson });
    check(responseDownloaded, { 'GetFiles (GET) AllConfirmedDownloaded 200': r => r.status === 200 });
    const modelsDownloaded = responseDownloaded.json();
    const returnedDownloadedIds = Array.isArray(modelsDownloaded) ? modelsDownloaded : [];
    check(returnedDownloadedIds, {
        'TC9 AllConfirmedDownloaded: contains id1': ids => ids.includes(filetransferId1),
        'TC9 AllConfirmedDownloaded: does not contain id2': ids => !ids.includes(filetransferId2)
    });
    console.log('TC9: Get files completed');
}