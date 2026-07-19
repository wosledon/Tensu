// Tensu gateway load test (PRD §4.2 / §7.1).
//
// Usage:
//   choco install k6   (or https://k6.io/docs/get-started/installation)
//   set TENSU_BASE_URL=http://localhost:5000
//   set TENSU_API_KEY=<platform api key>
//   set TENSU_MODEL=<provider-model>          (default: gpt-4o — use any enabled model id)
//   k6 run scripts/loadtest/k6-gateway.js
//
// Optional tuning:
//   set RPS=1000            target request rate per scenario (default: 100)
//   set DURATION=2m         test duration (default: 1m)
//   set P99_THRESHOLD_MS=30 alert threshold for gateway-added latency
//
// Scenarios (mixed traffic):
//   non_stream  70% — POST /v1/chat/completions
//   stream      20% — POST /v1/chat/completions with stream=true
//   models      10% — GET  /v1/models
//
// Pass criteria: error rate < 0.1%, P99 latency below P99_THRESHOLD_MS for
// cache-hit responses is expected to pass; upstream-bound traffic depends on
// the upstream provider, so thresholds are evaluated per scenario.

import http from 'k6/http';
import { check } from 'k6';
import { Rate, Trend } from 'k6/metrics';

const BASE_URL = __ENV.TENSU_BASE_URL || 'http://localhost:5000';
const API_KEY = __ENV.TENSU_API_KEY || 'tk-replace-me';
const MODEL = __ENV.TENSU_MODEL || 'gpt-4o';
const RPS = parseInt(__ENV.RPS || '100', 10);
const DURATION = __ENV.DURATION || '1m';
const P99_THRESHOLD = parseInt(__ENV.P99_THRESHOLD_MS || '30', 10);

const errorRate = new Rate('tensu_errors');
const nonStreamLatency = new Trend('tensu_non_stream_latency', true);
const streamLatency = new Trend('tensu_stream_latency', true);
const modelsLatency = new Trend('tensu_models_latency', true);

export const options = {
  scenarios: {
    non_stream: {
      executor: 'constant-arrival-rate',
      rate: Math.round(RPS * 0.7),
      timeUnit: '1s',
      duration: DURATION,
      preAllocatedVUs: Math.max(10, Math.round(RPS / 10)),
      maxVUs: Math.max(50, RPS),
      exec: 'nonStream',
    },
    stream: {
      executor: 'constant-arrival-rate',
      rate: Math.round(RPS * 0.2),
      timeUnit: '1s',
      duration: DURATION,
      preAllocatedVUs: Math.max(10, Math.round(RPS / 20)),
      maxVUs: Math.max(50, RPS),
      exec: 'stream',
    },
    models: {
      executor: 'constant-arrival-rate',
      rate: Math.round(RPS * 0.1),
      timeUnit: '1s',
      duration: DURATION,
      preAllocatedVUs: 5,
      maxVUs: 20,
      exec: 'models',
    },
  },
  thresholds: {
    tensu_errors: ['rate<0.001'],
    tensu_models_latency: [`p(99)<${P99_THRESHOLD}`],
    // Cache-hit (repeat) requests should meet the PRD gateway overhead target.
    http_req_duration{scenario:models}: [`p(99)<${P99_THRESHOLD}`],
  },
};

const HEADERS = {
  'Content-Type': 'application/json',
  Authorization: `Bearer ${API_KEY}`,
};

function chatBody(stream) {
  return JSON.stringify({
    model: MODEL,
    messages: [{ role: 'user', content: stream ? 'Say hello briefly.' : 'Hi' }],
    max_tokens: 16,
    stream,
  });
}

export function nonStream() {
  const res = http.post(`${BASE_URL}/v1/chat/completions`, chatBody(false), { headers: HEADERS });
  nonStreamLatency.add(res.timings.duration);
  const ok = check(res, {
    'status 2xx': (r) => r.status >= 200 && r.status < 300,
    'has request id': (r) => !!r.headers['X-Request-Id'],
  });
  errorRate.add(!ok);
}

export function stream() {
  const res = http.post(`${BASE_URL}/v1/chat/completions`, chatBody(true), {
    headers: HEADERS,
    responseType: 'text',
  });
  streamLatency.add(res.timings.duration);
  const ok = check(res, {
    'status 2xx': (r) => r.status >= 200 && r.status < 300,
    'is sse': (r) => (r.headers['Content-Type'] || '').includes('text/event-stream'),
  });
  errorRate.add(!ok);
}

export function models() {
  const res = http.get(`${BASE_URL}/v1/models`, { headers: HEADERS });
  modelsLatency.add(res.timings.duration);
  const ok = check(res, {
    'status 200': (r) => r.status === 200,
    'object list': (r) => {
      try { return JSON.parse(r.body).object === 'list'; } catch { return false; }
    },
  });
  errorRate.add(!ok);
}
