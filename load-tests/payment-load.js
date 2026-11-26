// Chapter 2 load test: many people pay at the same time while others just browse.
//
// Run (API and fake payment gateway running):
//   docker compose run --rm k6 run /scripts/payment-load.js
//
// Two groups of users run at the same time:
//   buyers   - 200 users, each buys 10 tickets one after another (reserve a seat, then pay)
//   browsers - 10 requests per second to the event list, which has nothing to do with payments
//
// Each run uses 2,000 seats of the "Load Test Arena" event.

import http from 'k6/http';
import { check } from 'k6';
import exec from 'k6/execution';

const BASE_URL = __ENV.BASE_URL || 'http://host.docker.internal:5287';
const BUYERS = 200;
const TICKETS_PER_BUYER = 10;

export const options = {
  scenarios: {
    buyers: {
      executor: 'per-vu-iterations',
      exec: 'buyTicket',
      vus: BUYERS,
      iterations: TICKETS_PER_BUYER,
      maxDuration: '3m',
    },
    browsers: {
      executor: 'constant-arrival-rate',
      exec: 'browseEvents',
      rate: 10,
      timeUnit: '1s',
      duration: '30s',
      preAllocatedVUs: 20,
      maxVUs: 300,
    },
  },
  // What "healthy" means for this test. k6 marks each line with a check mark or a cross.
  thresholds: {
    'http_req_duration{name:browse}': ['p(95)<500'],   // listing events should stay fast
    'http_req_failed{name:browse}': ['rate<0.01'],     // and should not fail
    'http_req_duration{name:confirm}': ['p(95)<3000'], // a payment takes ~2 s, so 3 s is fair
    'http_req_failed{name:confirm}': ['rate<0.01'],
  },
  summaryTrendStats: ['avg', 'p(95)', 'max'],
};

// Runs once before the test: find enough free seats in the load test event.
export function setup() {
  const events = http.get(`${BASE_URL}/api/events`).json();
  const arena = events.find((e) => e.name === 'Load Test Arena');
  const seats = http.get(`${BASE_URL}/api/events/${arena.id}/seats`).json();
  const freeSeatIds = seats.filter((s) => s.status === 'Available').map((s) => s.id);

  const needed = BUYERS * TICKETS_PER_BUYER;
  if (freeSeatIds.length < needed) {
    throw new Error(`Need ${needed} free seats, found ${freeSeatIds.length}. Reset the database first.`);
  }

  return { seatIds: freeSeatIds };
}

// Buyer: reserve a seat, then pay for it.
export function buyTicket(data) {
  const seatId = data.seatIds[exec.scenario.iterationInTest]; // every iteration gets its own seat
  const headers = { 'Content-Type': 'application/json' };

  const reserve = http.post(
    `${BASE_URL}/api/reservations`,
    JSON.stringify({ seatId, userId: newUuid() }),
    { headers, tags: { name: 'reserve' } },
  );
  if (!check(reserve, { 'reserve: 200': (r) => r.status === 200 })) {
    return;
  }

  const confirm = http.post(
    `${BASE_URL}/api/reservations/${reserve.json('id')}/confirm`,
    null,
    { tags: { name: 'confirm' } },
  );
  check(confirm, { 'confirm: 200': (r) => r.status === 200 });
}

// Browser: only looks at the event list.
export function browseEvents() {
  const res = http.get(`${BASE_URL}/api/events`, { tags: { name: 'browse' } });
  check(res, { 'browse: 200': (r) => r.status === 200 });
}

function newUuid() {
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
    const r = (Math.random() * 16) | 0;
    return (c === 'x' ? r : (r & 0x3) | 0x8).toString(16);
  });
}
