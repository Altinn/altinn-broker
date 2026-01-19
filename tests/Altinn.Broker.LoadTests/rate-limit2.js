import http from 'k6/http';
import { sleep, check, fail } from 'k6';

export const options = {
  vus: 1,
  duration: '1m',
  iterations: 12 // With 12x GetFileOverview per iteration, this should eventually hit 429 for typical 120/min limits
  // httpDebug: 'full',
};

// We only want to perform the "switch to new org token" check once.
let switchedToFallbackOrgToken = false;
let preFallbackErrors = [];

var TOKENS = {
  DUMMY_SENDER_TOKEN: "", // Change
  DUMMY_RECIPIENT_TOKEN: "", // Change
  FALLBACK_DUMMY_RECIPIENT_TOKEN: ""
}

const BASE_URL = __ENV.BASE_URL || "";
const RESOURCE_ID = __ENV.RESOURCE_ID || "";
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

function handleFirstRateLimit(label, res) {
  // Record the triggering 429 and log it
  preFallbackErrors.push({ label, res });
  console.error(`${label} hit rate limit (429). Captured response:`);
  console.error(res);

  if (switchedToFallbackOrgToken) {
    console.warn(`${label} rate-limited (429) after fallback check already ran.`);
    return;
  }

  switchedToFallbackOrgToken = true;
  console.warn(`${label} hit rate limit (429). Switching to fallback recipient token to verify org-based rate limiting...`);

  // Log all prior errors for context before we perform the fallback token check
  if (preFallbackErrors.length > 0) {
    console.error('Errors collected before fallback token check:');
    for (const e of preFallbackErrors) {
      console.error(`- ${e.label}:`);
      try {
        console.error(JSON.stringify(e.res, null, 2));
      } catch (err) {
        console.error(e.res);
      }
    }
  }

  newOrgToken();
}

function assertOkOrHandle429(res, ok, label) {
  if (res && res.status === 429) {
    // Save the 429 occurrence for diagnostics
        preFallbackErrors.push({ label, res });

        handleFirstRateLimit(label, res);
        return false;
    }

  if (!ok) {
    // Save non-429 failures as well so we have context
    preFallbackErrors.push({ label, res });
    console.error(label + ' failed');
    console.error(res);
    fail(res?.status);
  }
  return true;
}

export default function () {
  // If we've already switched to the fallback token and validated once, skip making further requests.
  if (switchedToFallbackOrgToken) {
    console.warn('Fallback check already performed; skipping further iterations.');
    return;
  }

  // 1) Search
  let searchHeaders = generateHeaders(TOKENS.DUMMY_RECIPIENT_TOKEN, 'application/json');
  let searchRes = http.get(`${BASE_URL}/broker/api/v1/filetransfer?resourceId=${encodeURIComponent(RESOURCE_ID)}`, { headers: searchHeaders });
  let okSearch = check(searchRes, { 'Search: status 200': (r) => r.status === 200 });
  if (!assertOkOrHandle429(searchRes, okSearch, 'Search')) {
    return;
  }

  // 2) 12x GetFileOverview for hardcoded FILE_TRANSFER_ID
  let overviewHeaders = generateHeaders(TOKENS.DUMMY_SENDER_TOKEN, 'application/json');
  for (let i = 0; i < 12; i++) {
    let res = http.get(`${BASE_URL}/broker/api/v1/filetransfer/${FILE_TRANSFER_ID}`, { headers: overviewHeaders });
    let ok = check(res, { 'Overview: status 200': (r) => r.status === 200 });
    if (!assertOkOrHandle429(res, ok, `Overview#${i + 1}`)) {
      return;
    }
  }


  sleep(1);
}

// This should be 200 after the policy is changed to make the rate limit org-based.
function newOrgToken() {
    console.log("HELLO WORLD");
  let overviewHeaders = generateHeaders(TOKENS.FALLBACK_DUMMY_RECIPIENT_TOKEN, 'application/json');
  let res = http.get(`${BASE_URL}/broker/api/v1/filetransfer/${FILE_TRANSFER_ID}`, { headers: overviewHeaders });
  console.log("GOT RESPONSE", res.status);

  if (!res || res.status !== 200) {
    console.error('Overview with fallback org token did not bypass rate limit.');
    console.error(res);
    fail(`Expected 200 with fallback org token, got ${res?.status}`);
  }
}


