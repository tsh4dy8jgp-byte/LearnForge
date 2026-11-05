#!/usr/bin/env node
// Shared installer and supervisor. No npm dependencies are needed to run this file.
import fs from 'node:fs';
import fsp from 'node:fs/promises';
import path from 'node:path';
import os from 'node:os';
import net from 'node:net';
import { createHash, randomUUID } from 'node:crypto';
import { spawn, spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { setTimeout as delay } from 'node:timers/promises';

export class InstallError extends Error {
  constructor(message, code = 3) { super(message); this.code = code; }
}

export function parseOptions(args) {
  const options = { check: false, installOnly: false, noInstall: false, noBrowser: false, timeout: 180, apiPort: null, webPort: null, dataDir: null };
  const flags = { '--check': 'check', '--install-only': 'installOnly', '--no-install': 'noInstall', '--no-browser': 'noBrowser', '--help': 'help', '-h': 'help' };
  const values = { '--api-port': 'apiPort', '--web-port': 'webPort', '--startup-timeout': 'timeout', '--data-dir': 'dataDir' };
  for (let i = 0; i < args.length; i++) {
    const arg = args[i];
    if (flags[arg]) { options[flags[arg]] = true; continue; }
    if (!values[arg] || i + 1 >= args.length || args[i + 1].startsWith('--')) throw new InstallError('Unknown or incomplete option: ' + arg, 2);
    const key = values[arg], value = args[++i];
    if (key === 'dataDir') { options[key] = value; continue; }
    const number = Number(value);
    if (!/^\d+$/.test(value) || number < 1 || number > (key === 'timeout' ? 3600 : 65535)) throw new InstallError('Invalid value for ' + arg, 2);
    options[key] = number;
  }
  if (options.check && options.installOnly) throw new InstallError('--check and --install-only cannot be combined.', 2);
  if (options.installOnly && options.noInstall) throw new InstallError('--install-only and --no-install cannot be combined.', 2);
  if (options.apiPort && options.apiPort === options.webPort) throw new InstallError('API and web ports must differ.', 2);
  return options;
}

export function sdkMatches(version, sdk) {
  if (!/^\d+\.\d+\.\d+$/.test(version) || !/^\d+\.\d+\.\d+$/.test(sdk.version)) return false;
  const v = version.split('.').map(Number), wanted = sdk.version.split('.').map(Number);
  const compare = v[0] - wanted[0] || v[1] - wanted[1] || v[2] - wanted[2];
  if (compare < 0) return false;
  switch (sdk.rollForward ?? 'patch') {
    case 'disable': return compare === 0;
    case 'patch': case 'latestPatch': return v[0] === wanted[0] && v[1] === wanted[1] && Math.floor(v[2] / 100) === Math.floor(wanted[2] / 100);
    case 'feature': case 'latestFeature': return v[0] === wanted[0] && v[1] === wanted[1];
    case 'minor': case 'latestMinor': return v[0] === wanted[0];
    case 'major': case 'latestMajor': return true;
    default: throw new InstallError('Unsupported SDK rollForward policy: ' + sdk.rollForward, 2);
  }
}

export function selectSdk(metadata, sdk, rid) {
  const versions = metadata.releases.flatMap(release => release.sdks ?? (release.sdk ? [release.sdk] : []))
    .filter(item => sdkMatches(item.version, sdk))
    .sort((a, b) => {
      const av = a.version.split('.').map(Number), bv = b.version.split('.').map(Number);
      return bv[0] - av[0] || bv[1] - av[1] || bv[2] - av[2];
    });
  for (const item of versions) {
    const file = item.files.find(candidate => candidate.rid === rid && candidate.url.endsWith(rid.startsWith('win-') ? '.zip' : '.tar.gz'));
    if (file) return { version: item.version, ...file };
  }
  throw new InstallError('No official SDK archive satisfies global.json for ' + rid + '.');
}

// Supports the "^x.y.z" and ">=x.y.z" alternatives Angular uses in engines.node.
export function satisfiesEngines(version, range) {
  const parse = text => text.replace(/^v/, '').split('.').map(Number);
  const v = parse(version);
  const atLeast = w => v[0] - w[0] || v[1] - w[1] || v[2] - w[2];
  return range.split('||').map(part => part.trim()).some(part => {
    const match = /^(\^|>=)(\d+\.\d+\.\d+)$/.exec(part);
    if (!match) return false;
    const wanted = parse(match[2]);
    return atLeast(wanted) >= 0 && (match[1] === '>=' || v[0] === wanted[0]);
  });
}

export function toolEnvironment(dotnet, node = process.execPath, source = process.env) {
  const env = { ...source };
  const oldPath = Object.entries(source).find(([key]) => key.toUpperCase() === 'PATH')?.[1] ?? '';
  for (const key of Object.keys(env)) {
    if (key.toUpperCase() === 'PATH' || key.startsWith('DOTNET_ROOT') || key === 'MSBuildSDKsPath' || key === 'DOTNET_ROLL_FORWARD_TO_PRERELEASE') delete env[key];
  }
  const host = dotnet && fs.existsSync(dotnet) ? fs.realpathSync(dotnet) : dotnet;
  env.PATH = [path.dirname(node), ...(host ? [path.dirname(host)] : []), oldPath].join(path.delimiter);
  if (host && fs.existsSync(path.join(path.dirname(host), 'sdk'))) env.DOTNET_ROOT = path.dirname(host);
  Object.assign(env, { DOTNET_NOLOGO: '1', DOTNET_CLI_UI_LANGUAGE: 'en-US', DOTNET_CLI_TELEMETRY_OPTOUT: '1', DOTNET_CLI_USE_MSBUILD_SERVER: '0', MSBUILDDISABLENODEREUSE: '1', NG_CLI_ANALYTICS: 'false', npm_config_fund: 'false', npm_config_audit: 'false' });
  return env;
}

function capture(command, args, options = {}) {
  const result = spawnSync(command, args, { encoding: 'utf8', timeout: 20000, windowsHide: true, ...options });
  return { ok: !result.error && result.status === 0, text: (result.stdout ?? '').trim(), error: (result.stderr ?? result.error?.message ?? '').trim() };
}

function candidates(command) {
  const extension = process.platform === 'win32' ? '.exe' : '';
  const directories = Object.entries(process.env).find(([key]) => key.toUpperCase() === 'PATH')?.[1]?.split(path.delimiter) ?? [];
  return directories.map(directory => path.resolve(directory, command + extension)).filter(file => fs.existsSync(file));
}

export function npmCli(node = process.execPath) {
  const canonical = fs.realpathSync(node);
  const directory = path.dirname(canonical);
  const locations = [path.join(directory, 'node_modules/npm/bin/npm-cli.js'), path.resolve(directory, '../lib/node_modules/npm/bin/npm-cli.js')];
  for (const directory of (process.env.PATH ?? process.env.Path ?? '').split(path.delimiter)) {
    const npm = path.join(directory, process.platform === 'win32' ? 'npm.cmd' : 'npm');
    if (fs.existsSync(npm) && process.platform !== 'win32') locations.push(fs.realpathSync(npm));
  }
  return locations.find(file => fs.existsSync(file)) ?? null;
}

function probeDotnet(file, root, sdk, framework) {
  if (!fs.existsSync(file)) return null;
  const env = toolEnvironment(file);
  const version = capture(file, ['--version'], { cwd: root, env });
  if (!version.ok || !sdkMatches(version.text, sdk)) return null;
  const info = capture(file, ['--info'], { cwd: root, env });
  if (!info.ok || !new RegExp('Architecture:\\s+' + process.arch + '\\b').test(info.text)) return null;
  const runtimes = capture(file, ['--list-runtimes'], { cwd: root, env });
  if (!runtimes.ok || !runtimes.text.includes('Microsoft.AspNetCore.App ' + framework + '.') || !runtimes.text.includes('Microsoft.NETCore.App ' + framework + '.')) return null;
  return { file, version: version.text, env };
}

function findDotnet(root, sdk, framework) {
  const name = process.platform === 'win32' ? 'dotnet.exe' : 'dotnet';
  const managed = path.join(root, '.learnforge/tools/dotnet');
  const managedVersions = fs.existsSync(managed) ? fs.readdirSync(managed).filter(version => /^\d+\.\d+\.\d+$/.test(version)).sort().reverse().map(version => path.join(managed, version, name)) : [];
  const known = [
    path.join(root, '.dotnet', name), ...candidates('dotnet'),
    path.join(os.homedir(), '.dotnet', name),
    ...(process.platform === 'win32'
      ? [path.join(process.env.ProgramFiles ?? 'C:\\Program Files', 'dotnet', name)]
      : ['/usr/local/share/dotnet/dotnet', '/usr/share/dotnet/dotnet', '/usr/lib/dotnet/dotnet']),
  ];
  for (const file of [...managedVersions, ...known]) {
    const found = probeDotnet(file, root, sdk, framework);
    if (found) return found;
  }
  return null;
}

export async function hashFile(file, algorithm = 'sha256') {
  const hash = createHash(algorithm);
  for await (const chunk of fs.createReadStream(file)) hash.update(chunk);
  return hash.digest('hex');
}

function validateOfficialUrl(value) {
  const url = new URL(value);
  if (url.protocol !== 'https:' || !['builds.dotnet.microsoft.com', 'download.visualstudio.microsoft.com', 'nodejs.org'].includes(url.hostname)) {
    throw new InstallError('Release metadata contains an unsupported download location.');
  }
}

export async function downloadVerified(url, destination, expected, algorithm, downloader) {
  validateOfficialUrl(url);
  if (!new RegExp('^[a-fA-F0-9]{' + (algorithm === 'sha512' ? 128 : 64) + '}$').test(expected)) throw new InstallError('Invalid official archive checksum.');
  if (fs.existsSync(destination) && await hashFile(destination, algorithm) === expected.toLowerCase()) return destination;
  const partial = destination + '.part-' + randomUUID();
  try {
    await downloader(url, partial);
    if (await hashFile(partial, algorithm) !== expected.toLowerCase()) throw new InstallError('Archive checksum mismatch; installation aborted.');
    await fsp.rename(partial, destination);
    return destination;
  } finally { await fsp.rm(partial, { force: true }); }
}

export function runCommand(command, args, { cwd, env, logFile, signal, code = 4, interactive = false } = {}) {
  return new Promise((resolve, reject) => {
    const fd = logFile ? fs.openSync(logFile, 'a', 0o600) : null;
    const child = spawn(command, args, { cwd, env, signal, windowsHide: true, stdio: interactive ? 'inherit' : ['ignore', fd ?? 'pipe', fd ?? 'pipe'] });
    if (fd !== null) fs.closeSync(fd);
    let output = '';
    child.stdout?.on('data', chunk => { output = (output + chunk).slice(-64000); });
    child.stderr?.on('data', chunk => { output = (output + chunk).slice(-64000); });
    let spawnError;
    child.on('error', error => { spawnError = error; });
    child.on('close', status => {
      if (signal?.aborted) reject(new InstallError('Stopped.', 0));
      else if (spawnError) reject(new InstallError('Could not run ' + path.basename(command) + ': ' + spawnError.message, code));
      else if (status !== 0) reject(new InstallError(path.basename(command) + ' failed (' + status + ').' + (logFile ? ' See ' + logFile : '\n' + output), code));
      else resolve(output.trim());
    });
  });
}

export async function acquireLock(file) {
  const identity = processIdentity(process.pid);
  for (let attempt = 0; attempt < 2; attempt++) {
    try {
      const handle = await fsp.open(file, 'wx', 0o600);
      await handle.writeFile(JSON.stringify({ pid: process.pid, identity }));
      await handle.close();
      return async () => {
        const owner = await readJson(file);
        if (owner?.pid === process.pid && owner.identity === identity) await fsp.rm(file, { force: true });
      };
    } catch (error) {
      if (error.code !== 'EEXIST') throw error;
      const owner = await readJson(file);
      // A malformed/new lock may be in the brief create/write window. Never steal it.
      if (!owner || isAlive(owner.pid) && (!owner.identity || processIdentity(owner.pid) === owner.identity)) throw new InstallError('Another installer or launcher owns this checkout. Stop it first, or delete ' + file + ' if no LearnForge process is running.');
      await fsp.rm(file, { force: true });
    }
  }
  throw new InstallError('Could not acquire the installation lock.');
}

function isAlive(pid) {
  if (!Number.isInteger(pid) || pid < 1) return false;
  try { process.kill(pid, 0); return true; } catch (error) { return error.code === 'EPERM'; }
}

export function processIdentity(pid) {
  if (!isAlive(pid)) return null;
  const result = process.platform === 'win32'
    ? capture('powershell.exe', ['-NoProfile', '-Command', '$p = Get-CimInstance Win32_Process -Filter "ProcessId = ' + pid + '"; if ($p) { $p.CreationDate.ToUniversalTime().ToString("o"); $p.ExecutablePath; $p.CommandLine }'])
    : capture('ps', ['-p', String(pid), '-o', 'lstart=', '-o', 'args='], { env: { ...process.env, LC_ALL: 'C' } });
  return result.ok && result.text ? createHash('sha256').update(result.text).digest('hex') : null;
}

async function readJson(file) {
  try { return JSON.parse(await fsp.readFile(file, 'utf8')); } catch { return null; }
}

async function writeJson(file, value) {
  const temporary = file + '.tmp-' + randomUUID();
  try {
    await fsp.writeFile(temporary, JSON.stringify(value, null, 2) + '\n', { mode: 0o600 });
    await fsp.rename(temporary, file);
  } finally { await fsp.rm(temporary, { force: true }); }
}

export async function choosePort(start, { explicit = false, excluded = [], attempts = 40 } = {}) {
  for (let offset = 0; offset < (explicit ? 1 : attempts) && start + offset <= 65535; offset++) {
    const port = start + offset;
    if (excluded.includes(port)) continue;
    const available = await new Promise((resolve, reject) => {
      const server = net.createServer();
      server.once('error', error => error.code === 'EADDRINUSE' ? resolve(false) : reject(new InstallError('Cannot bind local port ' + port + ': ' + error.code, 5)));
      server.listen(port, '127.0.0.1', () => { const selected = server.address().port; server.close(() => resolve(selected)); });
    });
    if (available !== false) return available;
  }
  throw new InstallError('Port ' + start + (explicit ? ' is already occupied.' : ' and nearby ports are occupied.'), 5);
}

export async function waitForHttp(url, { signal, deadline, validate = () => true, checkChildren = () => {}, progress = () => {} }) {
  let lastProgress = 0;
  while (Date.now() < deadline) {
    if (signal?.aborted) throw new InstallError('Stopped.', 0);
    checkChildren();
    try {
      const response = await fetch(url, { signal: AbortSignal.any([AbortSignal.timeout(2000), ...(signal ? [signal] : [])]) });
      if (response.ok && await validate(response)) { checkChildren(); return; }
      await response.body?.cancel().catch(() => {});
    } catch { /* A starting server may refuse connections or not have a compiled page yet. */ }
    if (Date.now() - lastProgress > 10000) { progress('Waiting for ' + url); lastProgress = Date.now(); }
    await delay(300, undefined, { signal }).catch(() => {});
  }
  checkChildren();
  throw new InstallError('Startup timed out waiting for ' + url, 5);
}

export function launchChild(command, args, { cwd, env, logFile }) {
  const fd = fs.openSync(logFile, 'a', 0o600);
  const child = spawn(command, args, { cwd, env, detached: process.platform !== 'win32', windowsHide: true, stdio: ['ignore', fd, fd] });
  fs.closeSync(fd);
  const state = { child, command, logFile, ended: false, result: null };
  state.finished = new Promise(resolve => {
    const finish = result => {
      if (state.ended) return;
      state.ended = true;
      state.result = result;
      resolve(state);
    };
    child.once('error', error => finish(error.message));
    child.once('exit', (code, signal) => finish('exit ' + code + (signal ? ' (' + signal + ')' : '')));
  });
  return state;
}

export async function stopChildren(children) {
  for (const state of children) {
    const pid = state.child.pid;
    if (!pid) continue;
    if (process.platform === 'win32') {
      if (!state.ended) await runCommand('taskkill.exe', ['/PID', String(pid), '/T', '/F'], { code: 5 }).catch(() => {});
    } else {
      try { process.kill(-pid, 'SIGTERM'); } catch { /* Already gone. */ }
    }
  }
  await Promise.race([Promise.all(children.map(state => state.finished)), delay(3000, undefined, { ref: false })]);
  if (process.platform !== 'win32') for (const state of children) {
    // The parent can exit before its descendants; address the owned process group as well.
    try { process.kill(-state.child.pid, 'SIGKILL'); } catch { /* Group is gone. */ }
  }
}

export function browserCommand(url, platform = process.platform, env = process.env) {
  if (platform === 'darwin') return ['open', [url]];
  if (platform === 'win32') return ['rundll32.exe', ['url.dll,FileProtocolHandler', url]];
  if (platform === 'linux' && (env.DISPLAY || env.WAYLAND_DISPLAY)) {
    if (candidates('xdg-open').length) return ['xdg-open', [url]];
    if (candidates('gio').length) return ['gio', ['open', url]];
  }
  return null;
}

function openBrowser(url, log) {
  const command = browserCommand(url);
  if (!command) { log('Browser unavailable in this session. Open ' + url + ' on a desktop browser.'); return; }
  // Detached so a blocking opener (xdg-open) neither delays startup nor joins the launcher's process group.
  const child = spawn(command[0], command[1], { detached: true, stdio: 'ignore', windowsHide: true });
  child.once('error', () => log('Could not open the browser automatically. Open ' + url));
  child.unref();
}

// Stops servers recorded by a launcher that died without cleanup (for example, a closed terminal).
async function stopOrphans(record, log) {
  const owned = member => member?.identity && processIdentity(member.pid) === member.identity;
  const orphans = [record.api, record.web].filter(owned);
  if (!orphans.length) return;
  log('Stopping LearnForge servers left running by an earlier launcher.');
  for (const { pid } of orphans) {
    if (process.platform === 'win32') await runCommand('taskkill.exe', ['/PID', String(pid), '/T', '/F'], { code: 5 }).catch(() => {});
    else try { process.kill(-pid, 'SIGTERM'); } catch { /* Already gone. */ }
  }
  for (let wait = 0; wait < 30 && orphans.some(({ pid }) => isAlive(pid)); wait++) await delay(100);
  if (process.platform !== 'win32') for (const { pid } of orphans) try { process.kill(-pid, 'SIGKILL'); } catch { /* Group is gone. */ }
}

async function existingInstance(ctx) {
  const record = await readJson(path.join(ctx.state, 'instance.json'));
  if (!record || record.root !== ctx.root || !record.owner?.identity) return null;
  if (processIdentity(record.owner.pid) !== record.owner.identity) { await stopOrphans(record, ctx.log); return null; }
  if (record.dataDir !== ctx.dataDir) return null;
  if (!Number.isInteger(record.webPort) || record.url !== 'http://127.0.0.1:' + record.webPort) return null;
  if (ctx.options.apiPort && record.apiPort !== ctx.options.apiPort || ctx.options.webPort && record.webPort !== ctx.options.webPort) return null;
  for (const member of [record.owner, record.api, record.web]) if (!member?.identity || processIdentity(member.pid) !== member.identity) return null;
  try {
    const health = await fetch(record.url + '/health', { signal: AbortSignal.timeout(2000) });
    if (health.ok && (await health.json()).status === 'healthy') return record;
  } catch { /* A stale instance never authorizes killing its recorded PIDs. */ }
  return null;
}

function tail(file) {
  try { return fs.readFileSync(file, 'utf8').split('\n').slice(-25).join('\n'); } catch { return ''; }
}

async function startServers(ctx, dotnet) {
  const { root, options, env, log, signal } = ctx;
  const instanceFile = path.join(ctx.state, 'instance.json');
  for (let attempt = 0; attempt < 3; attempt++) {
    const apiPort = await choosePort(options.apiPort ?? 5080 + attempt, { explicit: !!options.apiPort, excluded: options.webPort ? [options.webPort] : [] });
    const webPort = await choosePort(options.webPort ?? 4300 + attempt, { explicit: !!options.webPort, excluded: [apiPort] });
    const apiUrl = 'http://127.0.0.1:' + apiPort, url = 'http://127.0.0.1:' + webPort;
    const proxy = path.join(ctx.state, 'run/proxy.json');
    await writeJson(proxy, { '/api': { target: apiUrl, secure: false }, '/health': { target: apiUrl, secure: false } });
    const serverEnv = {
      ...env, ASPNETCORE_ENVIRONMENT: 'Development', Database__Provider: 'Sqlite', Database__AutoMigrate: 'true',
      ConnectionStrings__Database: 'Data Source="' + path.join(ctx.dataDir, 'learnforge.db').replaceAll('"', '""') + '"',
      DataProtection__KeyDirectory: path.join(ctx.dataDir, 'keys'), AllowedHosts: 'localhost;127.0.0.1',
      Content__PackDirectories__0: path.join(root, 'packs'), Content__WatchSeconds: '5',
    };
    const children = [];
    const checkChildren = () => {
      const ended = children.find(state => state.ended);
      if (ended) throw new InstallError(path.basename(ended.command) + ' stopped: ' + ended.result + '. See ' + ended.logFile, 5);
    };
    const prefix = new Date().toISOString().replace(/[:.]/g, '-') + '-' + process.pid + '-' + attempt;
    let ready = false;
    try {
      log('Starting API on ' + apiUrl + ' and website on ' + url);
      children.push(launchChild(dotnet.file, [ctx.apiDll, '--contentRoot', path.join(root, 'apps/api'), '--urls', apiUrl], {
        cwd: path.join(root, 'apps/api'), env: serverEnv, logFile: path.join(ctx.state, 'logs/api-' + prefix + '.log'),
      }));
      children.push(launchChild(process.execPath, [ctx.angularCli, 'serve', '--proxy-config', proxy, '--host', '127.0.0.1', '--port', String(webPort)], {
        cwd: path.join(root, 'apps/web'), env: serverEnv, logFile: path.join(ctx.state, 'logs/web-' + prefix + '.log'),
      }));
      const deadline = Date.now() + options.timeout * 1000;
      const waitOptions = { signal, deadline, checkChildren, progress: log };
      const health = async response => (await response.json()).status === 'healthy';
      const catalog = async response => Array.isArray(await response.json());
      await waitForHttp(apiUrl + '/health', { ...waitOptions, validate: health });
      await waitForHttp(apiUrl + '/api/catalog', { ...waitOptions, validate: catalog });
      await waitForHttp(url, { ...waitOptions, validate: async response => (await response.text()).includes('<app-root') });
      await waitForHttp(url + '/health', { ...waitOptions, validate: health });
      await waitForHttp(url + '/api/catalog', { ...waitOptions, validate: catalog });
      const identities = children.map(state => ({ pid: state.child.pid, identity: processIdentity(state.child.pid) }));
      await writeJson(instanceFile, { root, dataDir: ctx.dataDir, url, apiPort, webPort, owner: { pid: process.pid, identity: processIdentity(process.pid) }, api: identities[0], web: identities[1] });
      ready = true;
      log('LearnForge is ready: ' + url + '\nData: ' + ctx.dataDir + '\nLogs: ' + path.join(ctx.state, 'logs') + '\nPress Ctrl+C to stop both servers.');
      if (!options.noBrowser) openBrowser(url, log);
      const aborted = new Promise(resolve => {
        if (signal.aborted) resolve(null);
        else signal.addEventListener('abort', () => resolve(null), { once: true });
      });
      const stopped = await Promise.race([...children.map(state => state.finished), aborted]);
      if (stopped) throw new InstallError(path.basename(stopped.command) + ' stopped: ' + stopped.result + '. See ' + stopped.logFile, 5);
      return;
    } catch (error) {
      const bindRace = !ready && !signal.aborted && children.some(state => /EADDRINUSE|address already in use|port .*already in use/i.test(tail(state.logFile)));
      if (bindRace && attempt < 2 && !options.apiPort && !options.webPort) { log('A port was taken during startup; retrying with new ports.'); continue; }
      for (const state of children) log(tail(state.logFile));
      throw error;
    } finally {
      await stopChildren(children);
      const record = await readJson(instanceFile);
      if (record?.owner?.pid === process.pid) await fsp.rm(instanceFile, { force: true });
    }
  }
}

async function installDotnet(ctx) {
  const channel = ctx.sdk.version.split('.').slice(0, 2).join('.');
  const rid = (process.platform === 'darwin' ? 'osx' : process.platform === 'win32' ? 'win' : 'linux') + '-' + process.arch;
  ctx.log('Installing a compatible .NET SDK for ' + rid + '…');
  const downloader = (url, destination) => runCommand(process.platform === 'win32' ? 'curl.exe' : 'curl', [
    '--fail', '--location', '--proto', '=https', '--proto-redir', '=https', '--connect-timeout', '20', '--speed-limit', '1024', '--speed-time', '60', '--retry', '2', '--output', destination, url,
  ], { cwd: ctx.root, env: ctx.env, signal: ctx.signal, logFile: ctx.logFile, code: 3 });
  const metadataFile = path.join(ctx.state, 'cache/releases-' + channel + '.json');
  await downloader('https://builds.dotnet.microsoft.com/dotnet/release-metadata/' + channel + '/releases.json', metadataFile);
  const release = selectSdk(JSON.parse(await fsp.readFile(metadataFile, 'utf8')), ctx.sdk, rid);
  const archive = await downloadVerified(release.url, path.join(ctx.state, 'cache/' + path.basename(new URL(release.url).pathname)), release.hash, 'sha512', downloader);
  const parent = path.join(ctx.state, 'tools/dotnet');
  await fsp.mkdir(parent, { recursive: true });
  const staging = await fsp.mkdtemp(path.join(parent, 'staging-'));
  const destination = path.join(parent, release.version);
  try {
    // Windows 11 includes tar.exe; the official SDK zip is supported by it.
    await runCommand(process.platform === 'win32' ? 'tar.exe' : 'tar', ['-xf', archive, '-C', staging], { cwd: ctx.root, env: ctx.env, signal: ctx.signal, logFile: ctx.logFile, code: 3 });
    const host = path.join(staging, process.platform === 'win32' ? 'dotnet.exe' : 'dotnet');
    let verified = probeDotnet(host, ctx.root, ctx.sdk, ctx.framework);
    if (!verified && process.platform === 'win32') {
      const diagnostic = capture(host, ['--info'], { env: toolEnvironment(host) });
      if (/api-ms-win-crt|vcruntime|msvcp140/i.test(diagnostic.error)) {
        await runCommand('powershell.exe', ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', path.join(ctx.root, 'scripts/install.ps1'), '--repair-native'], { interactive: true, code: 3, signal: ctx.signal });
        verified = probeDotnet(host, ctx.root, ctx.sdk, ctx.framework);
      }
    }
    if (!verified) throw new InstallError('Downloaded SDK failed its SDK/runtime/architecture check. Review native prerequisites. See ' + ctx.logFile);
    if (fs.existsSync(destination)) await fsp.rename(destination, destination + '.previous-' + randomUUID());
    await fsp.rename(staging, destination);
    return probeDotnet(path.join(destination, path.basename(host)), ctx.root, ctx.sdk, ctx.framework);
  } finally { await fsp.rm(staging, { recursive: true, force: true }); }
}

export async function dependencyFingerprint(root, nodeVersion, npmVersion) {
  const hash = createHash('sha256');
  for (const file of ['apps/web/package.json', 'apps/web/package-lock.json']) hash.update(await fsp.readFile(path.join(root, file)));
  hash.update(JSON.stringify([nodeVersion, npmVersion, process.platform, process.arch]));
  return hash.digest('hex');
}

async function packFiles(directory) {
  const entries = await fsp.readdir(directory, { withFileTypes: true });
  const files = [];
  for (const entry of entries.sort((a, b) => a.name.localeCompare(b.name))) {
    const file = path.join(directory, entry.name);
    if (entry.isDirectory()) files.push(...await packFiles(file));
    else if (entry.isFile() && entry.name.endsWith('.json')) files.push(file);
  }
  return files;
}

async function prepare(ctx, dotnet, npm) {
  const run = (command, args) => runCommand(command, args, { cwd: ctx.root, env: ctx.env, logFile: ctx.logFile, signal: ctx.signal, code: 4 });
  const stampFile = path.join(ctx.state, 'dependencies.json');
  const stamp = await readJson(stampFile);
  const fingerprint = ctx.options.noInstall ? null
    : await dependencyFingerprint(ctx.root, process.version, capture(process.execPath, [npm, '--version'], { env: ctx.env }).text);
  if (!ctx.options.noInstall && (stamp?.fingerprint !== fingerprint || !fs.existsSync(ctx.angularCli))) {
    ctx.log('Installing web dependencies from package-lock.json…');
    await fsp.rm(stampFile, { force: true });
    await run(process.execPath, [npm, 'ci', '--prefix', path.join(ctx.root, 'apps/web')]);
    if (!fs.existsSync(ctx.angularCli)) throw new InstallError('npm ci completed without the local Angular CLI.', 4);
    await writeJson(stampFile, { fingerprint });
  } else if (!fs.existsSync(ctx.angularCli)) throw new InstallError('Web dependencies are absent. Run without --no-install.', 4);
  else {
    if (ctx.options.noInstall && !stamp) ctx.log('Web dependencies were not prepared by this installer. If the build fails, run it once without --no-install.');
    ctx.log('Using installed web dependencies.');
  }
  for (const project of ['apps/api/LearnForge.Api.csproj', 'tools/cli/LearnForge.Cli.csproj']) {
    if (!ctx.options.noInstall) {
      ctx.log('Restoring ' + project + '…');
      await run(dotnet.file, ['restore', project]);
    }
    ctx.log('Building ' + project + '…');
    await run(dotnet.file, ['build', project, '--no-restore', '--disable-build-servers']);
  }
  ctx.log('Validating every course JSON…');
  for (const file of await packFiles(path.join(ctx.root, 'packs'))) {
    await run(dotnet.file, [ctx.cliDll, 'check', file]);
  }
}

export async function main(args = process.argv.slice(2), root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')) {
  const options = parseOptions(args);
  if (options.help) { console.log('Run bash scripts/install.sh --help (Windows: scripts/install.ps1 --help).'); return; }
  root = await fsp.realpath(root);
  for (const file of ['.node-version', 'global.json', 'Directory.Build.props', 'apps/web/package-lock.json', 'apps/web/package.json', 'apps/api/LearnForge.Api.csproj', 'tools/cli/LearnForge.Cli.csproj']) {
    if (!fs.existsSync(path.join(root, file))) throw new InstallError('Missing checkout file: ' + file, 2);
  }
  if (!['darwin', 'linux', 'win32'].includes(process.platform) || !['x64', 'arm64'].includes(process.arch)) throw new InstallError('Unsupported platform/architecture.', 2);
  const state = path.join(root, '.learnforge');
  const sdk = JSON.parse(await fsp.readFile(path.join(root, 'global.json'), 'utf8')).sdk;
  sdkMatches(sdk.version, sdk); // Validate policy even when there is no installed SDK.
  const framework = (await fsp.readFile(path.join(root, 'Directory.Build.props'), 'utf8')).match(/<TargetFramework>net(\d+\.\d+)<\/TargetFramework>/)?.[1];
  if (!framework) throw new InstallError('Cannot resolve the target framework from Directory.Build.props.', 2);
  const expectedNode = (await fsp.readFile(path.join(root, '.node-version'), 'utf8')).trim();
  const expectedNpm = JSON.parse(await fsp.readFile(path.join(root, 'apps/web/package.json'), 'utf8')).packageManager?.replace(/^npm@/, '');
  if (process.getuid?.() === 0 && process.env.LEARNFORGE_ALLOW_ROOT !== '1') {
    throw new InstallError('Run LearnForge as your normal user, without sudo. In a disposable container, set LEARNFORGE_ALLOW_ROOT=1.', 2);
  }
  const npm = npmCli();
  const npmVersion = npm ? capture(process.execPath, [npm, '--version']).text : null;
  const controller = new AbortController();
  const interrupted = () => controller.abort();
  // A parent using Node IPC can request the same orderly shutdown as Ctrl+C.
  const parentMessage = message => { if (message === 'learnforge:stop') interrupted(); };
  process.once('SIGINT', interrupted);
  process.once('SIGTERM', interrupted);
  // Closing the terminal sends SIGHUP; the detached servers would otherwise outlive the launcher.
  process.once('SIGHUP', interrupted);
  process.on('message', parentMessage);
  const ctx = {
    root, state, sdk, framework, options, signal: controller.signal, env: toolEnvironment(null),
    dataDir: path.resolve(root, options.dataDir ?? 'apps/api/data'),
    apiDll: path.join(root, 'apps/api/bin/Debug/net' + framework + '/LearnForge.Api.dll'),
    cliDll: path.join(root, 'tools/cli/bin/Debug/net' + framework + '/LearnForge.Cli.dll'),
    angularCli: path.join(root, 'apps/web/node_modules/@angular/cli/bin/ng.js'),
    log: message => console.log(message),
  };
  let releaseLock;
  try {
    let dotnet = findDotnet(root, sdk, framework);
    const nodeOk = process.version === 'v' + expectedNode && npmVersion === expectedNpm;
    if (options.check) {
      console.log('Node: ' + process.version + ' (required ' + expectedNode + ')');
      console.log('npm: ' + (npmVersion ?? 'missing') + ' (required ' + expectedNpm + ')');
      console.log('.NET SDK: ' + (dotnet?.version ?? 'missing/incompatible') + ' (global.json: ' + sdk.version + ', ' + (sdk.rollForward ?? 'patch') + ')');
      console.log('Web dependencies: ' + (fs.existsSync(ctx.angularCli) ? 'present' : 'not prepared'));
      console.log('Data directory: ' + ctx.dataDir);
      if (!nodeOk || !dotnet) throw new InstallError('Requirements are missing. Run the platform installer without --check.', 2);
      return;
    }
    if (!nodeOk) {
      const engines = (await readJson(path.join(root, 'apps/web/node_modules/@angular/core/package.json')))?.engines?.node;
      if (!options.noInstall || !engines || !satisfiesEngines(process.version, engines)) throw new InstallError('Use scripts/install.sh or scripts/install.ps1 to acquire the tested Node/npm pair.', 2);
      ctx.log('Using Node ' + process.version + ' (tested: ' + expectedNode + ') because --no-install was given.');
    }
    if (!options.installOnly) {
      const existing = await existingInstance(ctx);
      if (existing) {
        ctx.log('LearnForge is already running: ' + existing.url);
        if (!options.noBrowser) openBrowser(existing.url, ctx.log);
        return;
      }
    }
    const storage = fs.statfsSync(root);
    if (storage.bavail * storage.bsize < 2 * 1024 ** 3 && (!dotnet || !fs.existsSync(ctx.angularCli))) throw new InstallError('At least 2 GB of free space is required for a fresh installation.', 2);
    for (const directory of ['cache', 'logs', 'run']) await fsp.mkdir(path.join(state, directory), { recursive: true, mode: 0o700 });
    await fsp.mkdir(ctx.dataDir, { recursive: true, mode: 0o700 });
    ctx.logFile = path.join(state, 'logs/installer.log');
    ctx.log = message => { if (message) { console.log(message); fs.appendFileSync(ctx.logFile, new Date().toISOString() + ' ' + message + '\n', { mode: 0o600 }); } };
    releaseLock = await acquireLock(path.join(state, 'install.lock'));
    if (!dotnet) {
      if (options.noInstall) throw new InstallError('A compatible SDK and ASP.NET Core runtime are required. Run without --no-install.', 2);
      dotnet = await installDotnet(ctx);
      if (!dotnet) throw new InstallError('The installed SDK could not be selected by global.json.');
    }
    ctx.env = toolEnvironment(dotnet.file);
    ctx.log('Using .NET SDK ' + dotnet.version + ', Node ' + process.version + ', npm ' + npmVersion);
    await prepare(ctx, dotnet, npm);
    if (options.installOnly) { ctx.log('Installation complete. Run the same command without --install-only to start LearnForge.'); return; }
    await startServers(ctx, dotnet);
  } catch (error) {
    if (error.code !== 0 && ctx.logFile) console.error(tail(ctx.logFile));
    throw error;
  } finally {
    if (releaseLock) await releaseLock();
    process.removeListener('SIGINT', interrupted);
    process.removeListener('SIGTERM', interrupted);
    process.removeListener('SIGHUP', interrupted);
    process.removeListener('message', parentMessage);
    if (process.connected) process.disconnect();
  }
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  main().catch(error => {
    if (error.code !== 0) console.error('LearnForge: ' + error.message);
    process.exitCode = error instanceof InstallError ? error.code : 3;
  });
}
