import { tagWithCurrentStageIndex } from 'https://jslib.k6.io/k6-utils/1.4.0/index.js';
import { setupEligibleParties, checkEligibility } from './helpers/eligiblePartyRequests.js';

// Finds the breaking point and checks recovery. Read the results; it only fails on the safety abort.
// Usage: k6 run test-eligible-parties-stress.js
// Agree with the environment owner before running against a shared environment.
export const options = {
  scenarios: {
    stress: {
      executor: 'ramping-arrival-rate',
      startRate: 10,
      timeUnit: '1s',
      preAllocatedVUs: 300,
      maxVUs: 1000, // 3000 rps × ~330 ms; kept below 1500 so a GitHub runner can cope
      stages: [
        { duration: '1m',  target: 250 },  // 0  warm-up
        { duration: '3m',  target: 250 },  // 1  baseline: compare with stage 11
        { duration: '1m',  target: 500 },  // 2
        { duration: '3m',  target: 500 },  // 3  hold 0.5x target
        { duration: '2m',  target: 1000 }, // 4  ramp slower, let autoscaling follow
        { duration: '5m',  target: 1000 }, // 5  hold target
        { duration: '2m',  target: 2000 }, // 6
        { duration: '3m',  target: 2000 }, // 7  hold 2x
        { duration: '2m',  target: 3000 }, // 8
        { duration: '3m',  target: 3000 }, // 9  hold 3x
        { duration: '10s', target: 250 },  // 10 sudden drop
        { duration: '3m',  target: 250 },  // 11 recovery: compare with stage 1
      ],
    },
  },
  thresholds: {
    // Safety net only: stop if the environment is clearly falling over
    http_req_failed: [{ threshold: 'rate<0.05', abortOnFail: true, delayAbortEval: '1m' }],

    // Always-true thresholds, only there so the summary shows each hold stage separately
    'http_req_duration{stage:1}':  ['p(95)>=0'],
    'http_req_duration{stage:3}':  ['p(95)>=0'],
    'http_req_duration{stage:5}':  ['p(95)>=0'],
    'http_req_duration{stage:7}':  ['p(95)>=0'],
    'http_req_duration{stage:9}':  ['p(95)>=0'],
    'http_req_duration{stage:11}': ['p(95)>=0'],
    'http_req_failed{stage:5}':    ['rate>=0'],
    'http_req_failed{stage:7}':    ['rate>=0'],
    'http_req_failed{stage:9}':    ['rate>=0'],
    'http_req_failed{stage:11}':   ['rate>=0'],
    'dropped_iterations':          ['count>=0'],
    'rate_limited_429':            ['count>=0'],
  },
  summaryTrendStats: ['avg', 'p(50)', 'p(95)', 'p(99)', 'max'],
};

export const setup = setupEligibleParties;

export default function (data) {
  tagWithCurrentStageIndex();
  checkEligibility(data);
}
