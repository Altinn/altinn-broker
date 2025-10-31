import http from 'k6/http';
import { check, fail } from 'k6';

export const options = {
  vus: 1,
  iterations: 1,
};

// Usage: k6 run -e ENDPOINT_TO_TEST=Search test-single-endpoint.js
// Wait 60+ seconds between runs to let rate limits reset
const TEST_ENDPOINT = __ENV.ENDPOINT_TO_TEST || 'Search';

var TOKENS = {
  DUMMY_SENDER_TOKEN: "",
  DUMMY_RECIPIENT_TOKEN: ""
}

const BASE_URL = __ENV.BASE_URL || "https://platform.tt02.altinn.no";
const RESOURCE_ID = __ENV.RESOURCE_ID || "bruno-broker";
const FILE_TRANSFER_ID = __ENV.FILE_TRANSFER_ID || "";

function generateHeaders(token, contentType) {
  return {
    'Authorization': 'Bearer ' + token,
    'Content-Type': contentType,
    'Accept': '*/*, text/plain',
    'Accept-Encoding': 'gzip, deflate, br',
    'Connection': 'keep-alive'
  }
}

function isRateLimited(res) {
  return res && res.status === 429;
}

function sendRequest(method, url, headers, body) {
  switch (method) {
    case 'GET':
      return http.get(url, { headers });
    case 'POST':
      return http.post(url, body, { headers });
    default:
      fail('Unsupported method: ' + method);
  }
}

const ENDPOINTS = [
  { name: 'Initialize', method: 'POST', path: '/broker/api/v1/filetransfer', limitPerMinute: 120, actor: 'sender', contentType: 'application/json', body: JSON.stringify({ dummy: true }) },
  { name: 'UploadBinary', method: 'POST', path: `/broker/api/v1/filetransfer/${FILE_TRANSFER_ID}/upload`, limitPerMinute: 120, actor: 'sender', contentType: 'application/octet-stream', body: '0' },
  { name: 'UploadFormData', method: 'POST', path: '/broker/api/v1/filetransfer/upload', limitPerMinute: 120, actor: 'sender', contentType: 'multipart/form-data; boundary=--k6', body: '--k6--' },
  { name: 'Overview', method: 'GET', path: `/broker/api/v1/filetransfer/${FILE_TRANSFER_ID}`, limitPerMinute: 120, actor: 'sender', contentType: 'application/json' },
  { name: 'Details', method: 'GET', path: `/broker/api/v1/filetransfer/${FILE_TRANSFER_ID}/details`, limitPerMinute: 15, actor: 'sender', contentType: 'application/json' },
  { name: 'Search', method: 'GET', path: `/broker/api/v1/filetransfer?resourceId=${encodeURIComponent(RESOURCE_ID)}`, limitPerMinute: 10, actor: 'recipient', contentType: 'application/json' },
  { name: 'Download', method: 'GET', path: `/broker/api/v1/filetransfer/${FILE_TRANSFER_ID}/download`, limitPerMinute: 120, actor: 'recipient', contentType: 'application/json' },
  { name: 'ConfirmDownload', method: 'POST', path: `/broker/api/v1/filetransfer/${FILE_TRANSFER_ID}/confirmdownload`, limitPerMinute: 120, actor: 'recipient', contentType: 'application/json', body: JSON.stringify({ dummy: true }) }
];

function headersFor(actor, contentType) {
  if (actor === 'sender') {
    return generateHeaders(TOKENS.DUMMY_SENDER_TOKEN, contentType);
  }
  return generateHeaders(TOKENS.DUMMY_RECIPIENT_TOKEN, contentType);
}

function hit(ep, count) {
  const headers = headersFor(ep.actor, ep.contentType);
  let rateLimited = 0;
  for (let i = 0; i < count; i++) {
    const res = sendRequest(ep.method, `${BASE_URL}${ep.path}`, headers, ep.body);
    if (isRateLimited(res)) {
      rateLimited++;
    }
  }
  return rateLimited;
}

export default function () {
  const epToTest = ENDPOINTS.find((e) => e.name === TEST_ENDPOINT);
  if (!epToTest) {
    fail(`Unknown endpoint: ${TEST_ENDPOINT}`);
  }

  console.log(`Testing ${epToTest.name} (limit: ${epToTest.limitPerMinute}/min)`);

  // Step 1: Saturate this endpoint
  const overCalls = epToTest.limitPerMinute + 5;
  const rlCount = hit(epToTest, overCalls);
  if (rlCount === 0) {
    fail(`${epToTest.name}: No rate limit hits observed (should see 429s when exceeding ${epToTest.limitPerMinute} requests)`);
  }
  console.log(`${epToTest.name}: Got ${rlCount} rate limit responses`);

  // Step 2: Verify this endpoint is rate-limited
  const checkRes = sendRequest(epToTest.method, `${BASE_URL}${epToTest.path}`, headersFor(epToTest.actor, epToTest.contentType), epToTest.body);
  const rlOk = check(checkRes, { [`${epToTest.name} is rate-limited`]: (r) => isRateLimited(r) });
  if (!rlOk) {
    fail(`${epToTest.name}: Should be rate-limited but got ${checkRes.status}`);
  }

  // Step 3: Check ALL other endpoints are NOT rate-limited
  const otherEndpoints = ENDPOINTS.filter((e) => e.name !== TEST_ENDPOINT);
  otherEndpoints.forEach((ep) => {
    const res = sendRequest(ep.method, `${BASE_URL}${ep.path}`, headersFor(ep.actor, ep.contentType), ep.body);
    const ok = check(res, { [`${ep.name} NOT rate-limited after ${TEST_ENDPOINT} saturation`]: (r) => !isRateLimited(r) });
    if (!ok) {
      fail(`${ep.name} IS rate-limited after saturating ${TEST_ENDPOINT} - they share a counter!`);
    }
  });

  console.log(`✓ ${TEST_ENDPOINT} has isolated rate limit`);
}

