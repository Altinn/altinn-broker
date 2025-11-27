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

    // Cleanup test data
    await cleanupUseCaseTestData();

    
        check(null, { 'Intentional failure (force_fail)': () => false });
    }


async function TC1_InitializeFileTransfer() {
    const token = await getSenderAltinnToken();
    check(token, { 'Sender Altinn token obtained': t => typeof t === 'string' && t.length > 0 });

    const recipient = isProduction ? __ENV.sender_org_no : __ENV.recipient_org_no;
    const payload = buildInitializeFileTransferPayload(resourceId, recipient);

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

    console.log(`TC1: Test case completed`);
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

    console.log(`TC2: Test case completed`);
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
    for (let attempt = 0; attempt <= maxRetries; attempt++) {
        sleep(1);
        const res = http.get(`${baseUrl}/broker/api/v1/filetransfer/${filetransferId}/details`, { headers });

        if (res.status === 200) {
            
            let statusVal = null;
            try {
                statusVal = res.json('fileTransferStatus');
            } catch (e) {
                console.error(`TC3: Error parsing details response: ${e.message}`);
            }

            const isPublished = (val) => {
                if (typeof val === 'number') return val === 3; // FileTransferStatus.Published
                if (typeof val === 'string') return val.toLowerCase() === 'published';
                return false;
            };

            // Keep the original string-equality check
            check(res, {
                'fileTransferStatus Published': r => r.json('fileTransferStatus') === 'Published'
            });
            // And also assert via tolerant helper (num|string)
            const publishedNow = isPublished(statusVal);
            check(publishedNow, { 'fileTransferStatus is Published (num|string)': v => v === true });

            if (publishedNow) {
                published = true;
                console.log(`TC3: File transfer is published on attempt ${attempt + 1}/${maxRetries} (status=${statusVal})`);
                break;
            } else {
                console.log(`TC3: Status not yet Published on attempt ${attempt + 1}/${maxRetries} (status=${statusVal})`);
            }
        }
        else if (res.status === 404) {
            console.log(`TC3: File transfer not found during polling attempt ${attempt + 1}/${maxRetries}`);
        }
        else {
            console.error(`TC3: Failed to get information about the file transfer. Status: ${res.status}. Body: ${res.body}`);
        }
    }
    check(published, { 'File transfer reached published status within 10s': p => p === true });
    console.log(`TC3: Test case completed`);
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
    console.log(`TC4: Test case completed`);
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

    console.log(`TC5: Test case completed`);
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
    console.log(`TC6: Test case completed`);
}

async function TC7_VerifyUpdatedStatus(filetransferId) {

    const senderToken = await getSenderAltinnToken();
    check(senderToken, { 'Sender Altinn token obtained': t => typeof t === 'string' && t.length > 0 });

    const headers = {
        Authorization: `Bearer ${senderToken}`
    };
    const res = http.get(`${baseUrl}/broker/api/v1/filetransfer/${filetransferId}`, { headers });
    check(res, { 'File transfer status after download status 200': r => r.status === 200});
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
    console.log(`TC7: Test case completed`);
}