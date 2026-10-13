// Run after dotnet restore + npm ci. Uses real servers and a disposable database/key directory.
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import path from 'node:path';
import os from 'node:os';
import { fork } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { setTimeout as delay } from 'node:timers/promises';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const data = await fs.mkdtemp(path.join(os.tmpdir(), 'learnforge real smoke '));
const processes = [];
function launch() {
  const child = fork(path.join(root, 'scripts/install.mjs'), ['--no-install', '--no-browser', '--data-dir', data, '--startup-timeout', '180'], {
    cwd: root, stdio: ['ignore', 'pipe', 'pipe', 'ipc'],
  });
  const state = { child, output: '', ended: false, code: null };
  child.stdout.on('data', chunk => { state.output += chunk; process.stdout.write(chunk); });
  child.stderr.on('data', chunk => { state.output += chunk; process.stderr.write(chunk); });
  state.closed = new Promise(resolve => child.on('close', code => { state.ended = true; state.code = code; resolve(code); }));
  processes.push(state);
  return state;
}
async function ready(state) {
  const deadline = Date.now() + 240000;
  while (Date.now() < deadline && !state.ended) {
    const match = state.output.match(/LearnForge is ready: (http:\/\/127\.0\.0\.1:\d+)/);
    if (match) return match[1];
    await delay(200);
  }
  throw new Error('Real launcher did not become ready.\n' + state.output);
}
async function stop(state) {
  if (!state.ended && state.child.connected) state.child.send('learnforge:stop');
  const code = await Promise.race([state.closed, delay(15000, undefined, { ref: false }).then(() => { throw new Error('Shutdown timed out.'); })]);
  assert.equal(code, 0, state.output);
}
const jar = new Map();
async function request(url, endpoint, options = {}) {
  const response = await fetch(url + endpoint, { ...options, signal: AbortSignal.timeout(15000), headers: { ...options.headers, Cookie: [...jar].map(([name, value]) => name + '=' + value).join('; ') } });
  for (const cookie of response.headers.getSetCookie()) {
    const pair = cookie.split(';')[0], split = pair.indexOf('=');
    jar.set(pair.slice(0, split), pair.slice(split + 1));
  }
  assert.ok(response.ok, endpoint + ': ' + response.status + ' ' + (response.ok ? '' : await response.text()));
  return response;
}
async function mutate(url, endpoint, method, body) {
  const token = (await (await request(url, '/api/auth/csrf')).json()).token;
  return request(url, endpoint, { method, headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': token }, body: JSON.stringify(body) });
}
try {
  const first = launch(), url = await ready(first);
  const catalog = await (await request(url, '/api/catalog')).json();
  assert.ok(catalog.length > 0);
  const course = catalog.find(item => item.capabilities.lessons);
  assert.ok(course);
  const content = await (await request(url, '/api/catalog/' + course.id)).json();
  const lesson = content.lessons[0].id;
  await mutate(url, '/api/auth/register', 'POST', { email: 'installer-smoke-' + Date.now() + '@example.test', displayName: 'Installer smoke', password: 'SmokeTesting12345' });
  await mutate(url, '/api/me/courses/' + course.id + '/lessons/' + lesson, 'PUT', null);
  assert.ok((await (await request(url, '/api/me/courses/' + course.id)).json()).completedLessons.includes(lesson));
  assert.ok((await fs.readdir(path.join(data, 'keys'))).some(file => file.endsWith('.xml')));
  const second = launch();
  assert.equal(await second.closed, 0, second.output);
  assert.match(second.output, /already running/);
  await stop(first);
  const restarted = launch(), restartedUrl = await ready(restarted);
  assert.equal((await request(restartedUrl, '/api/auth/me')).status, 200);
  assert.ok((await (await request(restartedUrl, '/api/me/courses/' + course.id)).json()).completedLessons.includes(lesson));
  await stop(restarted);
  await assert.rejects(fs.access(path.join(root, '.learnforge/install.lock')));
  console.log('Real smoke passed: catalog, registration, lesson progress, instance reuse, persistent keys, restart, and shutdown.');
} finally {
  for (const state of processes) {
    if (!state.ended) {
      try { await stop(state); } catch { state.child.kill('SIGKILL'); }
    }
  }
  await fs.rm(data, { recursive: true, force: true });
}
