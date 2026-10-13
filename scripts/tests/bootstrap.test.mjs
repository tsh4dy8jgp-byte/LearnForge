// POSIX bootstrap acquisition uses local archive/downloader fixtures, with no network requests.
import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import path from 'node:path';
import os from 'node:os';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { createHash } from 'node:crypto';
import { npmCli } from '../install.mjs';

const source = fileURLToPath(new URL('../install.sh', import.meta.url));
const quote = value => "'" + value.replaceAll("'", "'\\''") + "'";

async function fixture(t, tampered = false) {
  const root = await fs.realpath(await fs.mkdtemp(path.join(os.tmpdir(), 'learnforge bootstrap ')));
  t.after(() => fs.rm(root, { recursive: true, force: true }));
  const write = async (file, value, mode) => {
    await fs.mkdir(path.dirname(path.join(root, file)), { recursive: true });
    await fs.writeFile(path.join(root, file), value, { mode });
  };
  await fs.mkdir(path.join(root, 'scripts'), { recursive: true });
  await fs.copyFile(source, path.join(root, 'scripts/install.sh'));
  await write('.node-version', process.version.slice(1));
  const npm = spawnSync(process.execPath, [npmCli(), '--version'], { encoding: 'utf8' }).stdout.trim();
  await write('apps/web/package.json', JSON.stringify({ packageManager: 'npm@' + npm }));
  await write('scripts/install.mjs', 'import fs from "node:fs"; fs.writeFileSync("bootstrapped.txt", "ready"); console.log("Shared installer reached");');
  await write('payload/bin/node', '#!/bin/sh\nexec ' + quote(process.execPath) + ' "$@"\n', 0o755);
  await write('payload/lib/node_modules/npm/bin/npm-cli.js', 'require(' + JSON.stringify(npmCli()) + ');');
  const osName = process.platform === 'darwin' ? 'darwin' : 'linux';
  let arch = process.arch;
  if (process.platform === 'darwin' && spawnSync('sysctl', ['-in', 'hw.optional.arm64'], { encoding: 'utf8' }).stdout.trim() === '1') arch = 'arm64';
  const archiveName = 'node-' + process.version + '-' + osName + '-' + arch + (osName === 'linux' ? '.tar.xz' : '.tar.gz');
  const archive = path.join(root, archiveName);
  const packed = spawnSync('tar', [osName === 'linux' ? '-cJf' : '-czf', archive, '-C', root, 'payload'], { encoding: 'utf8' });
  assert.equal(packed.status, 0, packed.stderr);
  const hash = createHash('sha256').update(await fs.readFile(archive)).digest('hex');
  await write('SHASUMS256.txt', (tampered ? '0'.repeat(64) : hash) + '  ' + archiveName + '\n');
  // Hide a matching global Node host so the entry point has to bootstrap it.
  await write('fixture-bin/node', '#!/bin/sh\necho v0.0.0\n', 0o755);
  await write('fixture-bin/curl', '#!/bin/sh\nexec ' + quote(process.execPath) + ' ' + quote(path.join(root, 'download.cjs')) + ' "$@"\n', 0o755);
  await write('download.cjs', 'const fs=require("node:fs");const args=process.argv.slice(2);const dest=args[args.indexOf("-o")+1];const url=args.find(a=>a.startsWith("https://"));fs.copyFileSync(url.endsWith("SHASUMS256.txt")?' + JSON.stringify(path.join(root, 'SHASUMS256.txt')) + ':' + JSON.stringify(archive) + ',dest);');
  return { root, env: { ...process.env, PATH: path.join(root, 'fixture-bin') + path.delimiter + process.env.PATH } };
}

test('bootstrap acquires verified Node privately and reuses it on the second invocation', { skip: !['darwin', 'linux'].includes(process.platform) }, async t => {
  const { root, env } = await fixture(t);
  const first = spawnSync('bash', ['scripts/install.sh', '--install-only'], { cwd: root, env, encoding: 'utf8', timeout: 15000 });
  assert.equal(first.status, 0, first.stdout + first.stderr);
  assert.equal(await fs.readFile(path.join(root, 'bootstrapped.txt'), 'utf8'), 'ready');
  const managed = path.join(root, '.learnforge/tools/node', process.version, 'bin/node');
  assert.equal(spawnSync(managed, ['--version'], { encoding: 'utf8' }).stdout.trim(), process.version);
  const second = spawnSync('bash', ['scripts/install.sh', '--install-only'], { cwd: root, env, encoding: 'utf8', timeout: 15000 });
  assert.equal(second.status, 0, second.stdout + second.stderr);
  assert.doesNotMatch(second.stdout, /Installing Node/);
});

test('bootstrap checksum failure never publishes a partial Node installation', { skip: !['darwin', 'linux'].includes(process.platform) }, async t => {
  const { root, env } = await fixture(t, true);
  const result = spawnSync('bash', ['scripts/install.sh', '--install-only'], { cwd: root, env, encoding: 'utf8', timeout: 15000 });
  assert.equal(result.status, 3, result.stdout + result.stderr);
  assert.match(result.stderr, /checksum mismatch/);
  await assert.rejects(fs.access(path.join(root, '.learnforge/tools/node', process.version)));
  await assert.rejects(fs.access(path.join(root, '.learnforge/node-bootstrap.lock')));
  await assert.rejects(fs.access(path.join(root, 'bootstrapped.txt')));
});

test('bootstrap rejects invalid flags before creating state or invoking the downloader', { skip: !['darwin', 'linux'].includes(process.platform) }, async t => {
  const { root, env } = await fixture(t);
  const result = spawnSync('bash', ['scripts/install.sh', '--unknown'], { cwd: root, env, encoding: 'utf8', timeout: 5000 });
  assert.equal(result.status, 2, result.stdout + result.stderr);
  await assert.rejects(fs.access(path.join(root, '.learnforge')));
});
