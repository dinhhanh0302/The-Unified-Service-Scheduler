// Load test for SPEC §10 performance targets (step 10).
//
//   Booking      POST /api/v1/appointments   p95 < 500 ms
//   Availability GET  /api/v1/availability   p95 < 300 ms
//
// Run against the Docker Compose stack (seed data). It creates real appointments, so reset afterwards
// with `docker compose down -v`.
//
//   k6 installed:  k6 run perf/booking.k6.js
//   via Docker:    docker run --rm -i -e BASE_URL=http://host.docker.internal:8080 grafana/k6 run - < perf/booking.k6.js
//
// 409 / 422 answers are expected outcomes of a booking system under load (slot taken, vehicle already
// booked) and are not counted as failures; 5xx and timeouts are.

import http from 'k6/http';
import { check } from 'k6';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:8080';
const DURATION = __ENV.DURATION || '30s';

// Seed data: src/Scheduler.Infrastructure/Persistence/Migrations/0002_seed_data.sql
const D1 = 'd1000000-0000-0000-0000-000000000001';
const OIL_CHANGE = '5e000000-0000-0000-0000-000000000001';
const DIAGNOSTICS = '5e000000-0000-0000-0000-000000000003';
const OWNERS = [
  { customerId: 'c0000000-0000-0000-0000-000000000001', vehicleId: 'ae000000-0000-0000-0000-000000000001' },
  { customerId: 'c0000000-0000-0000-0000-000000000001', vehicleId: 'ae000000-0000-0000-0000-000000000002' },
  { customerId: 'c0000000-0000-0000-0000-000000000002', vehicleId: 'ae000000-0000-0000-0000-000000000003' },
  { customerId: 'c0000000-0000-0000-0000-000000000003', vehicleId: 'ae000000-0000-0000-0000-000000000004' },
];

export const options = {
  scenarios: {
    availability: {
      executor: 'constant-arrival-rate',
      exec: 'availability',
      rate: 20,
      timeUnit: '1s',
      duration: DURATION,
      preAllocatedVUs: 20,
    },
    booking: {
      executor: 'constant-arrival-rate',
      exec: 'booking',
      rate: 10,
      timeUnit: '1s',
      duration: DURATION,
      preAllocatedVUs: 20,
    },
  },
  thresholds: {
    'http_req_duration{endpoint:availability}': ['p(95)<300'],
    'http_req_duration{endpoint:booking}': ['p(95)<500'],
    'http_req_failed': ['rate<0.01'],
    checks: ['rate>0.99'],
  },
};

// Business rejections are valid answers, not transport failures.
http.setResponseCallback(http.expectedStatuses(200, 201, 409, 422));

const VIETNAM_OFFSET_MS = 7 * 60 * 60 * 1000;
const randomInt = (min, max) => Math.floor(Math.random() * (max - min + 1)) + min;
const pad = (n) => String(n).padStart(2, '0');

// A local date (yyyy-MM-dd) at the dealership, 2–40 days ahead, Monday–Saturday.
function futureOpenDate() {
  for (;;) {
    const local = new Date(Date.now() + VIETNAM_OFFSET_MS + randomInt(2, 40) * 24 * 60 * 60 * 1000);
    if (local.getUTCDay() !== 0) {
      return `${local.getUTCFullYear()}-${pad(local.getUTCMonth() + 1)}-${pad(local.getUTCDate())}`;
    }
  }
}

export function availability() {
  const serviceTypeId = Math.random() < 0.7 ? OIL_CHANGE : DIAGNOSTICS;
  const response = http.get(
    `${BASE_URL}/api/v1/availability?dealershipId=${D1}&serviceTypeId=${serviceTypeId}&date=${futureOpenDate()}`,
    { tags: { endpoint: 'availability' } });

  check(response, { 'availability 200': (r) => r.status === 200 });
}

export function booking() {
  const owner = OWNERS[randomInt(0, OWNERS.length - 1)];
  const startTime = `${futureOpenDate()}T${pad(randomInt(8, 15))}:${pad(15 * randomInt(0, 3))}:00+07:00`;

  const response = http.post(
    `${BASE_URL}/api/v1/appointments`,
    JSON.stringify({
      customerId: owner.customerId,
      vehicleId: owner.vehicleId,
      dealershipId: D1,
      serviceTypeId: OIL_CHANGE,
      startTime,
    }),
    { headers: { 'Content-Type': 'application/json' }, tags: { endpoint: 'booking' } });

  check(response, { 'booking answered (201/409/422)': (r) => [201, 409, 422].includes(r.status) });
}
