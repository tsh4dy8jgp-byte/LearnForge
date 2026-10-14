import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import http from 'node:http';
import { createHash } from 'node:crypto';
import { setTimeout as delay } from 'node:timers/promises';
import {
  parseOptions, sdkMatches, selectSdk, toolEnvironment, hashFile, downloadVerified,
  acquireLock, choosePort, waitForHttp, launchChild, stopChildren, processIdentity,
  browserCommand, dependencyFingerprint, runCommand, satisfiesEngines,
} from '../install.mjs';

async function temporary(t) {
  const directory = await fs.mkdtemp(path.join(os.tmpdir(), 'learnforge installer '));
  t.after(() => fs.rm(directory, { recursive: true, force: true }));
  return directory;
}

test('invalid arguments are rejected before installation', () => {
  for (const args of [
    ['--unknown'], ['--api-port'], ['--web-port', '0'], ['--api-port', '65536'],
    ['--startup-timeout', 'NaN'], ['--check', '--install-only'],
    ['--install-only', '--no-install'], ['--api-port', '5000', '--web-port', '5000'],
  ]) assert.throws(() => parseOptions(args), error => error.code === 2);
  assert.equal(parseOptions(['--data-dir', 'a directory with spaces', '--no-browser']).dataDir, 'a directory with spaces');
});

test('SDK resolution rejects previews, older SDKs and incompatible feature bands', () => {
  const sdk = { version: '10.0.100', rollForward: 'latestFeature' };
  assert.equal(sdkMatches('10.0.400', sdk), true);
  for (const version of ['9.0.999', '10.0.99', '11.0.100', '10.0.400-preview.1']) assert.equal(sdkMatches(version, sdk), false);
  assert.equal(sdkMatches('10.0.199', { ...sdk, rollForward: 'latestPatch' }), true);
  assert.equal(sdkMatches('10.0.200', { ...sdk, rollForward: 'latestPatch' }), false);
  const file = rid => ({ rid, url: 'https://builds.dotnet.microsoft.com/sdk/' + rid + '.tar.gz', hash: 'a'.repeat(128) });
  const metadata = { releases: [
    { sdks: [{ version: '10.0.100', files: [file('osx-arm64')] }] },
    { sdks: [{ version: '10.0.400-preview.1', files: [file('osx-arm64')] }, { version: '10.0.300', files: [file('osx-arm64')] }] },
  ] };
  assert.equal(selectSdk(metadata, sdk, 'osx-arm64').version, '10.0.300');
  assert.throws(() => selectSdk(metadata, sdk, 'linux-x64'), /No official SDK/);
});

test('selected tool environment avoids mismatched roots and Windows Path duplicates', () => {
  const env = toolEnvironment('/some/dotnet', '/some/node', { Path: '/old', DOTNET_ROOT_X64: '/wrong', DOTNET_ROOT: '/wrong', MSBuildSDKsPath: '/wrong', HTTPS_PROXY: 'https://proxy.invalid' });
  assert.equal(env.DOTNET_ROOT_X64, undefined);
  assert.equal(env.Path, undefined);
  assert.equal(env.MSBuildSDKsPath, undefined);
  assert.equal(env.HTTPS_PROXY, 'https://proxy.invalid');
  // Only a directory that contains SDKs is a DOTNET_ROOT; a shim such as snap's is not.
  assert.equal(env.DOTNET_ROOT, undefined);
});

test('a tool environment names the SDK root of the selected host', async t => {
  const root = await temporary(t);
  await fs.mkdir(path.join(root, 'sdk'));
  await fs.writeFile(path.join(root, 'dotnet'), '');
  assert.equal(toolEnvironment(path.join(root, 'dotnet'), '/some/node', {}).DOTNET_ROOT, await fs.realpath(root));
});

test('--no-install accepts only Node versions Angular supports', () => {
  const engines = '^22.22.3 || ^24.15.0 || >=26.0.0';
  for (const version of ['v22.22.3', 'v22.30.0', 'v24.15.0', 'v26.0.0', 'v27.1.0']) assert.equal(satisfiesEngines(version, engines), true, version);
  for (const version of ['v22.22.2', 'v23.0.0', 'v24.14.9', 'v25.9.0', 'v20.19.0']) assert.equal(satisfiesEngines(version, engines), false, version);
});

test('verified downloads reuse good caches and reject tampered archives atomically', async t => {
  const directory = await temporary(t), destination = path.join(directory, 'SDK archive.tar.gz');
  const good = Buffer.from('official bytes'), expected = createHash('sha512').update(good).digest('hex');
  let downloads = 0;
  const downloader = async (_url, file) => { downloads++; await fs.writeFile(file, good); };
  const url = 'https://builds.dotnet.microsoft.com/sdk/archive.tar.gz';
  await downloadVerified(url, destination, expected, 'sha512', downloader);
  await downloadVerified(url, destination, expected, 'sha512', downloader);
  assert.equal(downloads, 1);
  assert.equal(await hashFile(destination, 'sha512'), expected);
  await assert.rejects(downloadVerified(url, destination, 'f'.repeat(128), 'sha512', downloader), /checksum mismatch/);
  assert.deepEqual(await fs.readFile(destination), good);
  assert.deepEqual((await fs.readdir(directory)).filter(file => file.includes('.part')), []);
  await assert.rejects(downloadVerified('http://builds.dotnet.microsoft.com/sdk', destination, expected, 'sha512', downloader), /unsupported download/);
});

test('checkout lock rejects a live owner and recovers a dead owner', async t => {
  const directory = await temporary(t), file = path.join(directory, 'install.lock');
  const release = await acquireLock(file);
  await assert.rejects(acquireLock(file), /Another installer/);
  await release();
  await fs.writeFile(file, JSON.stringify({ pid: 2147483647, identity: 'dead' }));
  const releaseRecovered = await acquireLock(file);
  assert.equal(JSON.parse(await fs.readFile(file, 'utf8')).pid, process.pid);
  await releaseRecovered();
});

test('port selection preserves unrelated listeners and explicit conflicts fail', async t => {
  const server = http.createServer((_req, res) => res.end('unrelated application'));
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  t.after(() => new Promise(resolve => server.close(resolve)));
  const occupied = server.address().port;
  await assert.rejects(choosePort(occupied, { explicit: true }), error => error.code === 5);
  assert.notEqual(await choosePort(occupied), occupied);
  assert.equal(await (await fetch('http://127.0.0.1:' + occupied)).text(), 'unrelated application');
});

test('readiness waits for an application response and observes timeout/cancellation', async t => {
  let ready = false;
  const server = http.createServer((_req, res) => { res.statusCode = ready ? 200 : 503; res.end(JSON.stringify({ status: ready ? 'healthy' : 'starting' })); });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  t.after(() => new Promise(resolve => server.close(resolve)));
  const url = 'http://127.0.0.1:' + server.address().port;
  const waiting = waitForHttp(url, { deadline: Date.now() + 3000, validate: async res => (await res.json()).status === 'healthy' });
  await delay(150); ready = true; await waiting;
  ready = false;
  await assert.rejects(waitForHttp(url, { deadline: Date.now() + 100 }), error => error.code === 5);
  const controller = new AbortController(); controller.abort();
  await assert.rejects(waitForHttp(url, { deadline: Date.now() + 1000, signal: controller.signal }), error => error.code === 0);
  await assert.rejects(waitForHttp(url, { deadline: Date.now() + 1000, checkChildren: () => { throw new Error('child crashed'); } }), /child crashed/);
});

test('aborting a preparation command waits for its owned child to close', async () => {
  const controller = new AbortController();
  const running = runCommand(process.execPath, ['-e', 'setInterval(() => {}, 1000)'], { signal: controller.signal });
  setTimeout(() => controller.abort(), 100);
  await assert.rejects(running, error => error.code === 0);
});

test('server shutdown removes the owned process group, including descendants', { skip: process.platform === 'win32' }, async t => {
  const directory = await temporary(t);
  const pidFile = path.join(directory, 'worker.pid');
  const code = 'const c=require("node:child_process").spawn(process.execPath,["-e","setInterval(()=>{},1000)"],{stdio:"ignore"});require("node:fs").writeFileSync(process.argv[1],String(c.pid));setInterval(()=>{},1000);';
  const state = launchChild(process.execPath, ['-e', code, pidFile], { logFile: path.join(directory, 'child.log') });
  t.after(() => stopChildren([state]));
  for (let i = 0; i < 30; i++) { try { await fs.access(pidFile); break; } catch { await delay(50); } }
  const worker = Number(await fs.readFile(pidFile, 'utf8'));
  assert.ok(processIdentity(worker));
  await stopChildren([state]);
  await delay(100);
  assert.equal(processIdentity(worker), null);
});

test('browser dispatch supports desktop platforms and headless sessions', () => {
  const url = 'http://127.0.0.1:4300';
  assert.deepEqual(browserCommand(url, 'darwin'), ['open', [url]]);
  assert.deepEqual(browserCommand(url, 'win32'), ['rundll32.exe', ['url.dll,FileProtocolHandler', url]]);
  assert.equal(browserCommand(url, 'linux', {}), null);
});

test('dependency fingerprints change when the lockfile or toolchain changes', async t => {
  const directory = await temporary(t);
  await fs.mkdir(path.join(directory, 'apps/web'), { recursive: true });
  await fs.writeFile(path.join(directory, 'apps/web/package.json'), '{}');
  const lockfile = path.join(directory, 'apps/web/package-lock.json');
  await fs.writeFile(lockfile, '{}');
  const first = await dependencyFingerprint(directory, '22.22.3', '10.9.8');
  assert.notEqual(first, await dependencyFingerprint(directory, '22.22.4', '10.9.8'));
  await fs.writeFile(lockfile, '{"changed":true}');
  assert.notEqual(first, await dependencyFingerprint(directory, '22.22.3', '10.9.8'));
});
