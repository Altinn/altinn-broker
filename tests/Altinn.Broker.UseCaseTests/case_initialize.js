import http from 'k6/http';
import { check, sleep } from 'k6';
import crypto from 'k6/crypto';
import { buildInitializeFileTransferPayload } from './helpers/brokerPayloadBuilder.js';
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

/**
 * TC1: Initialize a file transfer
 * TC2: Upload the initialized file transfer
 * TC3: Poll and verify successful upload
 * TC4: Search for the file as a recipient and find it
 * TC5: Download and verify correct bytes
 * TC6: Confirm download
 * TC7: Verify updated status
 * Cleanup: Remove test data created during the test
 */

export default async function () {
    const { filetransferId } = await TC1_InitializeFileTransfer();
    await TC2_UploadFileTransfer(filetransferId);
    await TC3_PollAndVerifyUpload(filetransferId);
    await TC4_SearchFileAsRecipient(filetransferId);
    await TC5_DownloadAndVerifyBytes(filetransferId);
    await TC6_ConfirmDownload(filetransferId);
    await TC7_VerifyUpdatedStatus(filetransferId);
    const { iauFileTransferId } = await TC8_InitializeAndUpload();
    await TC9_GetFileTransfers(filetransferId, iauFileTransferId);

    // Cleanup test data
    await cleanupUseCaseTestData();
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

    const res = http.post(`${baseUrl}/broker/api/v1/filetransfer`, JSON.stringify(payload), { headers });
    check(res, { 'File transfer initialized status 200': r => r.status === 200 });
    if (res.status !== 200) {
        console.error(`File transfer initialization failed. Status: ${res.status}. Body: ${res.body}`);
        return { filetransferId: null };
    }

    let filetransferId = null;
    try {
        const response = res.json();
        if (response && response.fileTransferId) {
            filetransferId = response.fileTransferId;
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
    const res = http.post(`${baseUrl}/broker/api/v1/filetransfer/${filetransferId}/upload`, fixtureBytes, { headers });
    check(res, { 'File upload status 200': r => r.status === 200 });

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
    let statusVal = null;
    let lastRes = null;

    const isPublished = (val) => {
        if (typeof val === 'number') return val === 3; // FileTransferStatus.Published
        if (typeof val === 'string') return val.toLowerCase() === 'published';
        return false;
    };

    for (let attempt = 0; attempt <= maxRetries; attempt++) {
        sleep(1);
        lastRes = http.get(`${baseUrl}/broker/api/v1/filetransfer/${filetransferId}/details`, { headers });

        if (lastRes.status === 200) {
            try {
                statusVal = lastRes.json('fileTransferStatus');
            } catch (e) {
                console.error(`TC3: Error parsing details response: ${e.message}`);
            }

            if (isPublished(statusVal)) {
                published = true;
                console.log(`TC3: File transfer is published on attempt ${attempt + 1}/${maxRetries} (status=${statusVal})`);
                break;
            } else {
                console.log(`TC3: Status not yet Published on attempt ${attempt + 1}/${maxRetries} (status=${statusVal})`);
            }
        }
        else if (lastRes.status === 404) {
            console.log(`TC3: File transfer not found during polling attempt ${attempt + 1}/${maxRetries}`);
        }
        else {
            console.error(`TC3: Failed to get information about the file transfer. Status: ${lastRes.status}. Body: ${lastRes.body}`);
        }
    }

    if (lastRes && lastRes.status === 200) {
        check(lastRes, {
            'fileTransferStatus Published': r => r.json('fileTransferStatus') === 'Published'
        });
    }
    check(isPublished(statusVal), { 'fileTransferStatus is Published (num|string)': v => v === true });
    check(published, { 'File transfer reached published status within 10s': p => p === true });
    console.log(`TC3: Poll and verify upload completed`);
}

async function TC4_SearchFileAsRecipient(filetransferId) {

    const recipientToken = await getRecipientAltinnToken();
    check(recipientToken, { 'Recipient Altinn token obtained': t => typeof t === 'string' && t.length > 0 });

    const headers = {
        Authorization: `Bearer ${recipientToken}`
    };

    const res = http.get(`${baseUrl}/broker/api/v1/filetransfer/${filetransferId}`, { headers });
    check(res, { 'File transfer found by recipient status 200': r => r.status === 200 });
    if (res.status !== 200) {
        console.error(`File transfer search by recipient failed. Status: ${res.status}. Body: ${res.body}`);
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
    const res = http.get(`${baseUrl}/broker/api/v1/filetransfer/${filetransferId}/download`, { headers, responseType: 'binary' });
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

async function TC6_ConfirmDownload(filetransferId) {

    const recipientToken = await getRecipientAltinnToken();
    check(recipientToken, { 'Recipient Altinn token obtained': t => typeof t === 'string' && t.length > 0 });

    const headers = {
        Authorization: `Bearer ${recipientToken}`
    };

    const res = http.post(`${baseUrl}/broker/api/v1/filetransfer/${filetransferId}/confirmdownload`, null, { headers });
    check(res, { 'Confirm download status 204': r => r.status === 204 });
    if (res.status !== 204) {
        console.error(`Confirm download failed. Status: ${res.status}. Body: ${res.body}`);
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
    const res = http.get(`${baseUrl}/broker/api/v1/filetransfer/${filetransferId}`, { headers });
    check(res, { 'File transfer status after download status 200': r => r.status === 200 });
    if (res.status !== 200) {
        console.error(`Failed to get file transfer status after download. Status: ${res.status}. Body: ${res.body}`);
        return;
    }

    let statusAfterDownload = null;
    try {
        statusAfterDownload = res.json('fileTransferStatus');
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
        'Metadata.FileName': meta.fileName,
        'Metadata.ResourceId': meta.resourceId,
        'Metadata.SendersFileTransferReference': meta.sendersFileTransferReference,
        'Metadata.Sender': meta.sender,
        'Metadata.Recipients[0]': meta.recipients[0],
        'Metadata.DisableVirusScan': String(!!meta.disableVirusScan),
        'FileTransfer': http.file(fixtureBytes, meta.fileName, 'text/plain')
    };

    const params = { headers: { Authorization: `Bearer ${senderToken}` } };
    const res = http.post(`${baseUrl}/broker/api/v1/filetransfer/upload`, formBody, params);
    check(res, { 'InitializeAndUpload 200': r => r.status === 200 });
    if (res.status !== 200) {
        console.error(`InitializeAndUpload failed. Status: ${res.status}. Body: ${res.body}`);
        return { iauFileTransferId: null };
    }

    let iauFileTransferId = null;
    try {
        iauFileTransferId = res.json('fileTransferId');
        check(iauFileTransferId, { 'TC8 fileTransferId obtained': id => typeof id === 'string' && id.length > 0 });
    } catch (e) {
        console.error(`Error parsing InitializeAndUpload response: ${e.message}`);
    }

    const ovParams = { headers: { Authorization: `Bearer ${senderToken}` } };
    const ovRes = http.get(`${baseUrl}/broker/api/v1/filetransfer/${iauFileTransferId}`, ovParams);
    check(ovRes, { 'TC8 overview 200': r => r.status === 200 });
    check(ovRes.json('fileTransferStatus'), { 'TC8 status Published': s => s === 'Published' });

    console.log('TC8: InitializeAndUpload completed');
    return { iauFileTransferId };
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
    const res = http.get(url, { headers });
    check(res, { 'GetFileTransfers 200': r => r.status === 200 });
    if (res.status !== 200) {
        console.error(`GetFileTransfers failed. Status: ${res.status}. Body: ${res.body}`);
        return;
    }

    const ids = Array.isArray(res.json()) ? res.json() : [];
    check(ids, {
        'GetFileTransfers contains TC1 id': list => list.includes(fileTransferId1),
        'GetFileTransfers contains TC8 id': list => list.includes(fileTransferId2)
    });
    console.log('TC9: GetFileTransfers check completed');
}