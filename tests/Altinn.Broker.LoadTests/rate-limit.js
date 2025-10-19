import http from 'k6/http';
import { sleep, check } from 'k6';

export const options = {
  vus: 1,
  duration: '1m',
  iterations: 12 // Should fail at 12, each iteration is 12x GetFileOverview and the typical rate limit is 120/minute
  // httpDebug: 'full',
};

var TOKENS = {
  DUMMY_SENDER_TOKEN: "", // Change
  DUMMY_RECIPIENT_TOKEN: "" // Change
}

const BASE_URL = __ENV.BASE_URL || "https://platform.tt02.altinn.no";
const RESOURCE_ID = __ENV.RESOURCE_ID || "altinn-broker-system-user-1";
const FILE_TRANSFER_ID = __ENV.FILE_TRANSFER_ID || ""; // Change

function generateHeaders(token, contentType) {
  return {
    'Authorization': 'Bearer ' + token,
    'Content-Type': contentType,
    'Accept': '*/*, text/plain',
    'Accept-Encoding': 'gzip, deflate, br',
    'Connection': 'keep-alive'
  }
}

function checkResult(res, ok, label) {
  if (!ok) {
    console.error(label + ' failed');
    console.error(res);
    fail(res.status);
  }
}

export default function () {
  // 1) Search
  let searchHeaders = generateHeaders(TOKENS.DUMMY_RECIPIENT_TOKEN, 'application/json');
  let searchRes = http.get(`${BASE_URL}/broker/api/v1/filetransfer?resourceId=${encodeURIComponent(RESOURCE_ID)}`, { headers: searchHeaders });
  let okSearch = check(searchRes, { 'Search: status 200': (r) => r.status === 200 });
  checkResult(searchRes, okSearch, 'Search');

  // 2) 12x GetFileOverview for hardcoded FILE_TRANSFER_ID
  let overviewHeaders = generateHeaders(TOKENS.DUMMY_SENDER_TOKEN, 'application/json');
  for (let i = 0; i < 12; i++) {
    let res = http.get(`${BASE_URL}/broker/api/v1/filetransfer/${FILE_TRANSFER_ID}`, { headers: overviewHeaders });
    let ok = check(res, { 'Overview: status 200': (r) => r.status === 200 });
    checkResult(res, ok, `Overview#${i+1}`);
  }

  sleep(1);
}


