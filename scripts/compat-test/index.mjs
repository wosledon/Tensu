// Tensu SDK compatibility test (PRD §7.1).
// Connects the official OpenAI and Anthropic SDKs directly to the Tensu gateway
// and validates that standard SDK calls work unchanged.
//
// Usage:
//   cd scripts/compat-test
//   npm install
//   set TENSU_BASE_URL=http://localhost:5000   (no trailing slash, no /v1)
//   set TENSU_API_KEY=<platform api key>
//   set TENSU_OPENAI_MODEL=<provider-model>     (an OpenAI-protocol model id)
//   set TENSU_ANTHROPIC_MODEL=<provider-model>  (an Anthropic-protocol model id)
//   npm test
//
// Exit code 0 = all checks passed, 1 = at least one failure.

import OpenAI from 'openai';
import Anthropic from '@anthropic-ai/sdk';

const BASE_URL = (process.env.TENSU_BASE_URL || 'http://localhost:5000').replace(/\/+$/, '');
const API_KEY = process.env.TENSU_API_KEY || 'tk-replace-me';
const OPENAI_MODEL = process.env.TENSU_OPENAI_MODEL || 'gpt-4o';
const ANTHROPIC_MODEL = process.env.TENSU_ANTHROPIC_MODEL || '';

let passed = 0;
let failed = 0;

function report(name, ok, detail = '') {
  if (ok) {
    passed++;
    console.log(`  PASS ${name}`);
  } else {
    failed++;
    console.error(`  FAIL ${name}${detail ? ` — ${detail}` : ''}`);
  }
}

async function testOpenAI() {
  console.log('\n[OpenAI SDK]');
  const client = new OpenAI({ baseURL: `${BASE_URL}/v1`, apiKey: API_KEY });

  // 1. Model list
  try {
    const list = await client.models.list();
    report('models.list returns data array', Array.isArray(list.data) && list.data.length > 0);
  } catch (e) {
    report('models.list returns data array', false, e.message);
  }

  // 2. Non-stream chat completion
  try {
    const res = await client.chat.completions.create({
      model: OPENAI_MODEL,
      messages: [{ role: 'user', content: 'Say hello in one word.' }],
      max_tokens: 16,
    });
    report('chat.completions.create shape',
      typeof res.id === 'string' &&
      Array.isArray(res.choices) && res.choices.length > 0 &&
      typeof res.choices[0].message?.content === 'string');
  } catch (e) {
    report('chat.completions.create shape', false, e.message);
  }

  // 3. Streaming chat completion
  try {
    const stream = await client.chat.completions.create({
      model: OPENAI_MODEL,
      messages: [{ role: 'user', content: 'Say hello in one word.' }],
      max_tokens: 16,
      stream: true,
    });
    let chunks = 0;
    let content = '';
    for await (const chunk of stream) {
      chunks++;
      const delta = chunk.choices?.[0]?.delta?.content;
      if (delta) content += delta;
    }
    report('streaming yields chunks with content', chunks > 0 && content.length > 0);
  } catch (e) {
    report('streaming yields chunks with content', false, e.message);
  }
}

async function testAnthropic() {
  if (!ANTHROPIC_MODEL) {
    console.log('\n[Anthropic SDK] skipped (TENSU_ANTHROPIC_MODEL not set)');
    return;
  }
  console.log('\n[Anthropic SDK]');
  const client = new Anthropic({ baseURL: BASE_URL, apiKey: API_KEY });

  // 1. Non-stream message
  try {
    const res = await client.messages.create({
      model: ANTHROPIC_MODEL,
      max_tokens: 16,
      messages: [{ role: 'user', content: 'Say hello in one word.' }],
    });
    const text = res.content?.find((b) => b.type === 'text')?.text;
    report('messages.create shape',
      res.type === 'message' && Array.isArray(res.content) && typeof text === 'string');
  } catch (e) {
    report('messages.create shape', false, e.message);
  }

  // 2. Streaming message
  try {
    const stream = client.messages.stream({
      model: ANTHROPIC_MODEL,
      max_tokens: 16,
      messages: [{ role: 'user', content: 'Say hello in one word.' }],
    });
    let text = '';
    for await (const event of stream) {
      if (event.type === 'content_block_delta' && event.delta?.type === 'text_delta') {
        text += event.delta.text;
      }
    }
    report('messages.stream yields text deltas', text.length > 0);
  } catch (e) {
    report('messages.stream yields text deltas', false, e.message);
  }
}

console.log(`Tensu SDK compat test against ${BASE_URL}`);
await testOpenAI();
await testAnthropic();

console.log(`\n${passed} passed, ${failed} failed`);
process.exit(failed > 0 ? 1 : 0);
