import http from 'k6/http';
import { check, fail } from 'k6';
import { Counter } from 'k6/metrics';
import { getEligiblePartiesAltinnToken } from './altinnTokenService.js';

export const baseUrl = __ENV.base_url || 'https://platform.yt01.altinn.cloud';

// Mix of eligible and non-eligible org numbers. Override with -e org_numbers=a,b,c
const orgNumbers = ('311695657,310630829,314184904,000000000,313798534,311170376').split(',');

const rateLimited = new Counter('rate_limited_429');
const serverErrors = new Counter('server_errors_5xx');

export async function setupEligibleParties() {
  // -e token=... skips Maskinporten, e.g. when running against localhost with a token from Bruno
  const token = await getEligiblePartiesAltinnToken();
  if (!check(token, { 'Eligible parties token obtained': (t) => typeof t === 'string' && t.length > 0 })) {
    fail('Could not obtain eligible parties token');
  }
  return {
    headers: {
      Authorization: `Bearer ${token}`,
      Accept: 'application/json',
    },
  };
}

export function checkEligibility(data) {
  const orgNumber = orgNumbers[Math.floor(Math.random() * orgNumbers.length)];
  const res = http.get(`${baseUrl}/broker/api/v1/eligible-party/${orgNumber}`, {
    headers: data.headers,
    tags: { name: 'GetPartyEligibility' },
    timeout: '5s',
  });

  if (res.status === 429) rateLimited.add(1);
  if (res.status >= 500) serverErrors.add(1);

  const ok = check(res, {
    'status is 200': (r) => r.status === 200,
    'body has isEligible': (r) => r.status === 200 && typeof r.json('isEligible') === 'boolean',
  });

  if (!ok && __ENV.debug) {
    console.error(`${res.status} ${orgNumber}: ${res.body}`);
  }
}
