import http from 'k6/http';
import { sleep, check, fail } from 'k6';

export const options = {
  vus: 20,
  duration: '10m',
  iterations: 20 // can be set to 0 or removed to run indefinitely
  //httpDebug: 'full', // information about the request and response
};

var TOKENS = {
  DUMMY_SENDER_TOKEN: "",
  DUMMY_SERVICE_OWNER_TOKEN: ""
}
const BASE_URL = "http://localhost:5096"

const file = open("./data/testfile.txt", "b");
function checkResult(res, status) {
  if (!status) {
    console.error(status)
    console.error(res)
  }
}

export function setup() {
  let headers = generateHeaders(TOKENS.DUMMY_SERVICE_OWNER_TOKEN, 'application/json')

  //set fileTransfer TTL to 15 minutes. Should be longer than the test time
  var fileRes = http.put(`${BASE_URL}/broker/api/v1/resource/altinn-broker-test-resource-1`, JSON.stringify({
    fileTransferTimeToLive: "PT15M"
  }), { headers: headers });

  if (
    !check(fileRes, {
      'status code MUST be 200,204 or 409': (fileRes) => fileRes.status === 200 || fileRes.status === 409 || fileRes.status === 204
    })
  ) {
    checkResult(fileRes, false)
    fail('Could not update file transfer TTL. Exiting');
  }
}

export default async function () {
  var baseFile = {
    resourceId: 'altinn-broker-test-resource-1',
    checksum: null,
    fileName: 'testfile.txt',
    recipients: ['0192:986252932'],
    sender: '0192:991825827',
    sendersFileTransferReference: 'test-data'
  }

  let headers = generateHeaders(TOKENS.DUMMY_SENDER_TOKEN, 'application/json')
  var res = await http.asyncRequest('POST',
    `${BASE_URL}/broker/api/v1/filetransfer`,
    JSON.stringify(baseFile), { headers: headers });
  var status = check(res, { 'Initialize: status was 200': (r) => r.status == 200 });
  sleep(1);
  checkResult(res, status)

  if (status) {
    headers = generateHeaders(TOKENS.DUMMY_SENDER_TOKEN, 'application/octet-stream')
    const data = {
      field: 'this is a standard form field',
      file: http.file(file, 'testfile.txt')
    }
    var res2 = await http.asyncRequest('POST',
      `${BASE_URL}/broker/api/v1/filetransfer/${res.body}/upload`, data, { timeout: "600s", headers: headers });
    sleep(1);
    status = check(res2, { 'Upload: status was 200': (r) => r.status == 200 });
    checkResult(res, status)
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