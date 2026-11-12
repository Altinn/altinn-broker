import { setupEligibleParties, checkEligibility } from './helpers/eligiblePartyRequests.js';

// Pass/fail check: can we sustain the target rate?
// Usage: k6 run -e rate=1000 -e duration=10m -e p95_ms=100 -e p99_ms=300 test-eligible-parties-load.js
const rate = Number(1000);
const duration = '10m';
const p95Ms = Number(100);
const p99Ms = Number(300);

// VUs in use = rate × latency. Size the pool for the p99 limit with 2x margin;
// beyond that, iterations are dropped and the run fails.
const maxVUs = Math.max(20, Math.ceil(rate * (p99Ms / 1000) * 2));

export const options = {
  scenarios: {
    warmup: {
      executor: 'ramping-arrival-rate',
      startRate: 10,
      timeUnit: '1s',
      preAllocatedVUs: Math.ceil(maxVUs / 4),
      maxVUs,
      // 2 minutes so autoscaling has time to react before the measured phase
      stages: [{ duration: '2m', target: rate }],
    },
    steady: {
      executor: 'constant-arrival-rate',
      startTime: '2m',
      rate,
      timeUnit: '1s',
      duration,
      preAllocatedVUs: Math.ceil(maxVUs / 2),
      maxVUs,
    },
  },
  thresholds: {
    // Only the steady phase counts toward pass/fail
    'http_req_failed{scenario:steady}': ['rate<0.001'],
    'http_req_duration{scenario:steady}': [`p(95)<${p95Ms}`, `p(99)<${p99Ms}`],
    'checks{scenario:steady}': ['rate>0.999'],
    'dropped_iterations{scenario:steady}': ['count==0'],
    'rate_limited_429{scenario:steady}': ['count==0'],
  },
};

export const setup = setupEligibleParties;

export default function (data) {
  checkEligibility(data);
}
