import http from 'k6/http';
import { check, sleep } from 'k6';
import crypto from 'k6/crypto';
import { buildInitializeFileTransferPayload, TEST_TAG_A3 } from './helpers/brokerPayloadBuilder.js';
import { cleanupUseCaseTestData } from './helpers/cleanupUseCaseTestsData.js';
import { getSenderAltinnToken, getRecipientAltinnToken } from './helpers/altinnTokenService.js';

const baseUrl = __ENV.base_url;
const resourceId = 'bruksmonster-broker';
const isProduction = (baseUrl.toLowerCase().includes('platform.altinn.no') ? true : false);

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

// Second fixture for TC8 to test with different file content
const fixture2Bytes = open('./fixtures/usecase-broker-test-file2.txt', 'b');

/**
 * TC1: Initialize a file transfer
 * TC2: Upload the initialized file transfer
 * TC3: Poll and verify successful upload
 * TC4: Search for the file as a recipient and find it
 * TC5: Download and verify correct bytes
 * TC6: Confirm download
 * TC7: Verify updated status
 * TC8: Initialize and upload
 * TC9: Get file transfers 
 * Cleanup: Remove test data created during the test
 */

export default async function () {
    let filetransferId = null;
    let initializeAndUploadFileTransferId = null;

    try {
    ({ filetransferId } = await TC1_InitializeFileTransfer());
    await TC2_UploadFileTransfer(filetransferId);
    await TC3_PollAndVerifyUpload(filetransferId);
    await TC4_SearchFileAsRecipient(filetransferId);
    await TC5_DownloadAndVerifyBytes(filetransferId);
    await TC6_ConfirmDownload(filetransferId);
    await TC7_VerifyUpdatedStatus(filetransferId);
    ({ initializeAndUploadFileTransferId } = await TC8_InitializeAndUpload());
    await TC9_GetFileTransfers(filetransferId, initializeAndUploadFileTransferId);
    } finally {
    // Cleanup test data
    await cleanupUseCaseTestData(TEST_TAG_A3);
    }
}



async function TC1_InitializeFileTransfer() {
    const token = await getSenderAltinnToken();
    check(token, { 'Sender Altinn token obtained': t => typeof t === 'string' && t.length > 0 });

    const recipient = isProduction ? "orgnummerforprod" : "311167898"
    const payload = buildInitializeFileTransferPayload(recipient);

    const headers = {
        'Authorization': `Bearer ${token}`,
        'Content-Type': 'application/json',
        'Accept': 'application/json'
    };

    const response = http.post(`${baseUrl}/broker/api/v1/filetransfer`, JSON.stringify(payload), { headers });
    check(response, { 'File transfer initialized status 200': r => r.status === 200 });
    if (response.status !== 200) {
        console.error(`File transfer initialization failed. Status: ${response.status}. Body: ${response.body}`);
        return { filetransferId: null };
    }

    let filetransferId = null;
    try {
        const jsonResponse = response.json();
        if (jsonResponse && jsonResponse.fileTransferId) {
            filetransferId = jsonResponse.fileTransferId;
        }
        check(filetransferId, { 'File transfer ID obtained': id => typeof id === 'string' && id.length > 0 });
    } catch (e) {
        console.error(`Error parsing initialization response: ${e.message}`);
    }

    console.log(`TC1: Initialize file transfer completed`);
    return { filetransferId };
}


async function TC2_UploadFileTransfer(filetransferId) {

    if (!filetransferId) {
        console.error(`TC2 aborted: No filetransferId from TC1`);
        return;
    }

    const senderToken = await getSenderAltinnToken();
    check(senderToken, { 'Sender Altinn token obtained (upload)': t => typeof t === 'string' && t.length > 0 });

    const headers = {
        Authorization: `Bearer ${senderToken}`,
        'Content-Type': 'application/octet-stream'
    };

    // Upload the exact fixture bytes so we can verify later
    const response = http.post(`${baseUrl}/broker/api/v1/filetransfer/${filetransferId}/upload`, fixtureBytes, { headers });
    check(response, { 'File upload status 200': r => r.status === 200 });

    console.log(`TC2: Upload file transfer completed`);
}

async function TC3_PollAndVerifyUpload(filetransferId) {
    if (!filetransferId) {
        console.error(`TC3 aborted: No filetransferId from TC1`);
        return;
    }

    const senderToken = await getSenderAltinnToken();
    check(senderToken, { 'Sender Altinn token obtained': t => typeof t === 'string' && t.length > 0 });

    const headers = {
        Authorization: `Bearer ${senderToken}`
    }

    const maxRetries = 10;
    let published = false;
    let statusValue = null;
    let lastResponse = null;

    const isPublished = (val) => {
        if (typeof val === 'number') return val === 3; // FileTransferStatus.Published
        if (typeof val === 'string') return val.toLowerCase() === 'published';
        return false;
    };

    for (let attempt = 0; attempt < maxRetries; attempt++) {
        sleep(3);
        lastResponse = http.get(`${baseUrl}/broker/api/v1/filetransfer/${filetransferId}/details`, { headers });

        if (lastResponse.status === 200) {
            try {
                statusValue = lastResponse.json('fileTransferStatus');
            } catch (e) {
                console.error(`TC3: Error parsing details response: ${e.message}`);
            }

            if (isPublished(statusValue)) {
                published = true;
                console.log(`TC3: File transfer is published on attempt ${attempt + 1}/${maxRetries} (status=${statusValue})`);
                break;
            } else {
                console.log(`TC3: Status not yet Published on attempt ${attempt + 1}/${maxRetries} (status=${statusValue})`);
            }
        }
        else if (lastResponse.status === 404) {
            console.log(`TC3: File transfer not found during polling attempt ${attempt + 1}/${maxRetries}`);
        }
        else {
            console.error(`TC3: Failed to get information about the file transfer. Status: ${lastResponse.status}. Body: ${lastResponse.body}`);
        }
    }

    if (lastResponse && lastResponse.status === 200) {
        check(lastResponse, {
            'fileTransferStatus Published': r => isPublished(r.json('fileTransferStatus'))
        });
    }
    check(isPublished(statusValue), { 'fileTransferStatus is Published (num|string)': v => v === true });
    check(published, { 'File transfer reached published status within 30s': p => p === true });
    console.log(`TC3: Poll and verify upload completed`);
}

async function TC4_SearchFileAsRecipient(filetransferId) {

    const recipientToken = await getRecipientAltinnToken();
    check(recipientToken, { 'Recipient Altinn token obtained': t => typeof t === 'string' && t.length > 0 });

    const headers = {
        Authorization: `Bearer ${recipientToken}`
    };

    const response = http.get(`${baseUrl}/broker/api/v1/filetransfer/${filetransferId}`, { headers });
    check(response, { 'File transfer found by recipient status 200': r => r.status === 200 });
    if (response.status !== 200) {
        console.error(`File transfer search by recipient failed. Status: ${response.status}. Body: ${response.body}`);
    }
    console.log(`TC4: Search file as recipient completed`);
}

async function TC5_DownloadAndVerifyBytes(filetransferId) {

    if (!filetransferId) {
        console.error(`TC5 aborted: No filetransferId from earlier steps`);
        return null;
    }

    const recipientToken = await getRecipientAltinnToken();
    check(recipientToken, { 'Recipient Altinn token obtained': t => typeof t === 'string' && t.length > 0 });

    const headers = {
        Authorization: `Bearer ${recipientToken}`
    };

    // Request binary to receive an ArrayBuffer for exact byte comparison
    const response = http.get(`${baseUrl}/broker/api/v1/filetransfer/${filetransferId}/download`, { headers, responseType: 'binary' });
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

async function TC6_ConfirmDownload(filetransferId) {

    const recipientToken = await getRecipientAltinnToken();
    check(recipientToken, { 'Recipient Altinn token obtained': t => typeof t === 'string' && t.length > 0 });

    const headers = {
        Authorization: `Bearer ${recipientToken}`
    };

    const response = http.post(`${baseUrl}/broker/api/v1/filetransfer/${filetransferId}/confirmdownload`, null, { headers });
    check(response, { 'Confirm download status 204': r => r.status === 204 });
    if (response.status !== 204) {
        console.error(`Confirm download failed. Status: ${response.status}. Body: ${response.body}`);
        return;
    }
    console.log(`TC6: Confirm download completed`);
}

async function TC7_VerifyUpdatedStatus(filetransferId) {

    const senderToken = await getSenderAltinnToken();
    check(senderToken, { 'Sender Altinn token obtained': t => typeof t === 'string' && t.length > 0 });

    const headers = {
        Authorization: `Bearer ${senderToken}`
    };
    const response = http.get(`${baseUrl}/broker/api/v1/filetransfer/${filetransferId}`, { headers });
    check(response, { 'File transfer status after download status 200': r => r.status === 200 });
    if (response.status !== 200) {
        console.error(`Failed to get file transfer status after download. Status: ${response.status}. Body: ${response.body}`);
        return;
    }

    let statusAfterDownload = null;
    try {
        statusAfterDownload = response.json('fileTransferStatus');
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

async function TC8_InitializeAndUpload() {
    const senderToken = await getSenderAltinnToken();
    check(senderToken, { 'Sender token for TC8 obtained': t => typeof t === 'string' && t.length > 0 });

    const recipientOrg = isProduction ? 'orgnummerforprod' : '311167898';
    const meta = buildInitializeFileTransferPayload(recipientOrg);

    // Build multipart/form-data with nested form keys for Metadata and a file part for FileTransfer
    const formBody = {
        'Metadata.FileName': 'usecase-broker-test-file2.txt',
        'Metadata.ResourceId': meta.resourceId,
        'Metadata.SendersFileTransferReference': meta.sendersFileTransferReference,
        'Metadata.Sender': meta.sender,
        'Metadata.Recipients[0]': meta.recipients[0],
        'Metadata.DisableVirusScan': String(!!meta.disableVirusScan),
        'Metadata.PropertyList.testTag': meta.propertyList.testTag,
        'Metadata.PropertyList.useCase': meta.propertyList.useCase,
        'Metadata.PropertyList.description': meta.propertyList.description,
        'FileTransfer': http.file(fixture2Bytes, 'usecase-broker-test-file2.txt', 'text/plain')
    };

    const params = { headers: { Authorization: `Bearer ${senderToken}` } };
    const response = http.post(`${baseUrl}/broker/api/v1/filetransfer/upload`, formBody, params);
    check(response, { 'InitializeAndUpload 200': r => r.status === 200 });
    if (response.status !== 200) {
        console.error(`InitializeAndUpload failed. Status: ${response.status}. Body: ${response.body}`);
        return { initializeAndUploadFileTransferId: null };
    }

    let initializeAndUploadFileTransferId = null;
    try {
        initializeAndUploadFileTransferId = response.json('fileTransferId');
        check(initializeAndUploadFileTransferId, { 'TC8 fileTransferId obtained': id => typeof id === 'string' && id.length > 0 });
    } catch (e) {
        console.error(`Error parsing InitializeAndUpload response: ${e.message}`);
    }

    const overviewHeaders = { Authorization: `Bearer ${senderToken}`, Accept: 'application/json' };
    
    const maxRetries = 10;
    let published = false;
    let statusValue = null;
    let overviewResponse = null;

    const isPublished = (val) => {
        if (typeof val === 'number') return val === 3; // FileTransferStatus.Published
        if (typeof val === 'string') return val.toLowerCase() === 'published';
        return false;
    };

    for (let attempt = 0; attempt < maxRetries; attempt++) {
        sleep(3);
        overviewResponse = http.get(`${baseUrl}/broker/api/v1/filetransfer/${initializeAndUploadFileTransferId}`, { headers: overviewHeaders });
        if (overviewResponse.status === 200) {
            try {
                statusValue = overviewResponse.json('fileTransferStatus');
            } catch (e) {
                console.error(`TC8: Error parsing overview response: ${e.message}`);
            }
            if (isPublished(statusValue)) {
                published = true;
                console.log(`TC8: File transfer is published on attempt ${attempt + 1}/${maxRetries} (status=${statusValue})`);
                break;
            } else {
                console.log(`TC8: Status not yet Published on attempt ${attempt + 1}/${maxRetries} (status=${statusValue})`);
            }
        } else if (overviewResponse.status === 404) {
            console.log(`TC8: File transfer not found during polling attempt ${attempt + 1}/${maxRetries}`);
        } else {
            console.error(`TC8: Failed to get file transfer overview. Status: ${overviewResponse.status}. Body: ${overviewResponse.body}`);
        }
    }

    if (overviewResponse) {
        check(overviewResponse, { 'TC8 overview 200': r => r.status === 200 });
    }
    check(isPublished(statusValue), { 'TC8 status Published (num|string)': v => v === true });
    check(published, { 'TC8 reached Published within 30s': p => p === true });

    console.log('TC8: InitializeAndUpload completed');
    return { initializeAndUploadFileTransferId };
}

async function TC9_GetFileTransfers(fileTransferId1, fileTransferId2) {
    if (!fileTransferId1 || !fileTransferId2) {
        console.error('TC9 aborted: missing fileTransferId(s) from earlier steps');
        return;
    }

    const senderToken = await getSenderAltinnToken();
    check(senderToken, { 'Sender token for list obtained': t => typeof t === 'string' && t.length > 0 });

    const headers = { Authorization: `Bearer ${senderToken}` };
    const url = `${baseUrl}/broker/api/v1/filetransfer?resourceId=${encodeURIComponent(resourceId)}&role=Sender`;
    const response = http.get(url, { headers });
    check(response, { 'GetFileTransfers 200': r => r.status === 200 });
    if (response.status !== 200) {
        console.error(`GetFileTransfers failed. Status: ${response.status}. Body: ${response.body}`);
        return;
    }

    const ids = Array.isArray(response.json()) ? response.json() : [];
    check(ids, {
        'GetFileTransfers contains TC1 id': list => list.includes(fileTransferId1),
        'GetFileTransfers contains TC8 id': list => list.includes(fileTransferId2)
    });
    console.log('TC9: GetFileTransfers check completed');
}