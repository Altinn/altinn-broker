import http from 'k6/http';
import { sleep, check, fail } from 'k6';
import { getSenderAltinnToken } from './helpers/altinnTokenService.js';

export const options = {
  vus: 20,
  duration: '10m'
  //httpDebug: 'full', // information about the request and response
};

const baseUrl = __ENV.base_url || 'https://platform.yt01.altinn.cloud';
const sender = __ENV.sender;
const recipient = __ENV.recipient;

const file = open('./data/testfile.txt', 'b');

function checkResult(res, status) {
  if (!status) {
    console.error(status)
    console.error(res)
  }
}

export async function setup() {
  const senderToken = await getSenderAltinnToken();
  if (!check(senderToken, { 'Sender Altinn token obtained': (t) => typeof t === 'string' && t.length > 0 })) {
    fail('Could not obtain sender Altinn token');
  }
  return { senderToken };
}

export default async function (data) {
  var baseFile = {
    resourceId: 'ttd-broker-performance-test',
    checksum: null,
    fileName: 'testfile.txt',
    recipients: [`urn:altinn:organization:identifier-no:${recipient}`],
    sender: `urn:altinn:organization:identifier-no:${sender}`,
    sendersFileTransferReference: 'test-data'
  }

  let headers = generateHeaders(data.senderToken, 'application/json')
  var res = await http.asyncRequest('POST',
    `${baseUrl}/broker/api/v1/filetransfer`,
    JSON.stringify(baseFile), { headers: headers });
  var status = check(res, { 'Initialize: status was 200': (r) => r.status == 200 });
  sleep(1);
  checkResult(res, status)

  if (status) {
    headers = generateHeaders(data.senderToken, 'application/octet-stream')
    const body = res.json();
    var res2 = await http.asyncRequest(
      'POST',
      `${baseUrl}/broker/api/v1/filetransfer/${body.fileTransferId}/upload`,
      file,
      { timeout: '600s', headers: headers }
    );
    status = check(res2, { 'Upload: status was 200': (r) => r.status == 200 });
    checkResult(res2, status)

    if (status) {
      await pollUntilPublished(body.fileTransferId, data.senderToken);
    }
  }
}

function isPublishedStatus(val) {
  if (typeof val === 'number') return val === 3;
  if (typeof val === 'string') return val.toLowerCase() === 'published';
  return false;
}

async function pollUntilPublished(fileTransferId, token) {
  const headers = generateHeaders(token, 'application/json');
  const maxAttempts = Number(__ENV.poll_max_seconds) || 30;
  let published = false;
  let lastResponse = null;

  for (let attempt = 0; attempt < maxAttempts; attempt++) {
    lastResponse = http.get(
      `${baseUrl}/broker/api/v1/filetransfer/${fileTransferId}`, { headers });
    if (lastResponse.status === 200) {
      const overview = lastResponse.json();
      if (isPublishedStatus(overview.fileTransferStatus)) {
        published = true;
        check(overview, {
          'Published: fileTransferStatus is Published': (o) => isPublishedStatus(o.fileTransferStatus),
          'Published: published field is set': (o) => o.published != null && o.published !== '',
          'Published: fileTransferSize > 0': (o) => o.fileTransferSize > 0,
          'Published: checksum is set': (o) => o.checksum != null && o.checksum !== '',
        });
        break;
      }
    }
    if (attempt == maxAttempts - 1) {
      const overview = lastResponse.json();
      console.log(`Polling attempt ${attempt} of ${maxAttempts} for file transfer ${fileTransferId}`);
      console.log(`Checksum: ${JSON.stringify(overview.checksum)}`)
    }
    sleep(1);
  }

  check(published, { 'Published: reached within poll timeout': (p) => p === true });
  if (!published) {
    checkResult(lastResponse, false);
  }
}

function generateHeaders(token, contentType) {
  return {
    'Authorization': 'Bearer ' + token,
    'Content-Type': contentType,
    'Accept': '*/*, text/plain',
    'Accept-Encoding': 'gzip, deflate, br',
    'Connection': 'keep-alive'
  }
}
