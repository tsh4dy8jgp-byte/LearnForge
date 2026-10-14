// Exercise the complete launcher with process/HTTP fixtures, without downloading tools.
import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import path from 'node:path';
import os from 'node:os';
import http from 'node:http';
import { spawn, spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { setTimeout as delay } from 'node:timers/promises';
import { npmCli, processIdentity } from '../install.mjs';

const moduleFile = fileURLToPath(new URL('../install.mjs', import.meta.url));
const npmVersion = spawnSync(process.execPath, [npmCli(), '--version'], { encoding: 'utf8' }).stdout.trim();

async function until(predicate, timeout = 10000) {
  const deadline = Date.now() + timeout;
  while (Date.now() < deadline) { if (await predicate()) return; await delay(100); }
  assert.fail('Fixture did not reach the expected state.');
}

async function fixture(t, behavior = {}) {
  const root = await fs.realpath(await fs.mkdtemp(path.join(os.tmpdir(), 'learnforge fixture with spaces ')));
  t.after(() => fs.rm(root, { recursive: true, force: true }));
  const write = async (file, value) => {
    await fs.mkdir(path.dirname(path.join(root, file)), { recursive: true });
    await fs.writeFile(path.join(root, file), value);
  };
  await write('.node-version', process.version.slice(1));
  await write('global.json', '{"sdk":{"version":"10.0.100","rollForward":"latestFeature"}}');
  await write('Directory.Build.props', '<TargetFramework>net10.0</TargetFramework>');
  await write('apps/web/package.json', JSON.stringify({ packageManager: 'npm@' + npmVersion }));
  await write('apps/web/package-lock.json', '{}');
  await write('apps/api/LearnForge.Api.csproj', '');
  await write('tools/cli/LearnForge.Cli.csproj', '');
  await write('apps/api/bin/Debug/net10.0/LearnForge.Api.dll', '');
  await write('tools/cli/bin/Debug/net10.0/LearnForge.Cli.dll', '');
  await write('packs/nested/course.json', '{"id":"nested-course"}');
  await write('behavior.json', JSON.stringify(behavior));
  const common = 'const fs=require("node:fs"),path=require("node:path"),http=require("node:http");const root=' + JSON.stringify(root) + ';const behavior=JSON.parse(fs.readFileSync(path.join(root,"behavior.json"),"utf8"));';
  await write('.dotnet/dotnet', '#!/usr/bin/env node\n' + common + String.raw`
const args=process.argv.slice(2);
if(args[0]==="--version"){console.log(behavior.sdk || "10.0.400");process.exit(0);}
if(args[0]==="--info"){console.log("Architecture: "+process.arch);process.exit(0);}
if(args[0]==="--list-runtimes"){console.log("Microsoft.NETCore.App 10.0.12\nMicrosoft.AspNetCore.App 10.0.12");process.exit(0);}
if(args[0]==="restore" || args[0]==="build"){process.exit(behavior.buildFailure ? 1 : 0);}
if(args[1]==="check"){try{JSON.parse(fs.readFileSync(args[2],"utf8"));fs.appendFileSync(path.join(root,"validated.txt"),args[2]+"\n");process.exit(0);}catch{console.error("Invalid course JSON: "+args[2]);process.exit(1);}}
const url=new URL(args[args.indexOf("--urls")+1]);
fs.writeFileSync(path.join(root,"api.pid"),String(process.pid));
const server=http.createServer((req,res)=>{res.setHeader("Content-Type","application/json");res.end(JSON.stringify(req.url==="/health"?{status:"healthy"}:[{id:"nested-course"}]));});
setTimeout(()=>server.listen(Number(url.port),"127.0.0.1"),behavior.apiDelay || 0);
setInterval(()=>{},1000);
`);
  await fs.chmod(path.join(root, '.dotnet/dotnet'), 0o755);
  await write('apps/web/node_modules/@angular/cli/bin/ng.js', common + String.raw`
const args=process.argv.slice(2),proxy=JSON.parse(fs.readFileSync(args[args.indexOf("--proxy-config")+1],"utf8"));
fs.writeFileSync(path.join(root,"web.pid"),String(process.pid));
fs.writeFileSync(path.join(root,"proxy-target.txt"),proxy["/api"].target);
const server=http.createServer((req,res)=>{
  if(req.url==="/"){res.setHeader("Content-Type","text/html");res.end("<html><app-root></app-root></html>");return;}
  http.get(proxy["/api"].target+req.url,upstream=>{res.writeHead(upstream.statusCode,upstream.headers);upstream.pipe(res);}).on("error",()=>{res.statusCode=503;res.end();});
});
server.listen(Number(args[args.indexOf("--port")+1]),"127.0.0.1");
if(behavior.crashAfter) setTimeout(()=>process.exit(42),behavior.crashAfter);
`);
  return root;
}

function launch(root, args = []) {
  const invocation = 'import(' + JSON.stringify(moduleFile) + ').then(m=>m.main(' + JSON.stringify(args) + ',' + JSON.stringify(root) + ')).catch(e=>{console.error(e.message);process.exitCode=e.code??3;});';
  const child = spawn(process.execPath, ['-e', invocation], { stdio: ['ignore', 'pipe', 'pipe'] });
  const result = { child, output: '', exitCode: null };
  child.stdout.on('data', chunk => { result.output += chunk; });
  child.stderr.on('data', chunk => { result.output += chunk; });
  result.closed = new Promise(resolve => child.on('close', code => { result.exitCode = code; resolve(code); }));
  return result;
}

async function stop(result) {
  if (result.exitCode === null) result.child.kill('SIGINT');
  await Promise.race([result.closed, delay(10000, undefined, { ref: false }).then(() => { if (result.exitCode === null) { result.child.kill('SIGKILL'); assert.fail('Launcher did not clean up after SIGINT.'); } })]);
}

test('whole launcher synchronizes replacement ports, validates nested packs, reuses an instance, and stops its children', { skip: process.platform === 'win32', timeout: 30000 }, async t => {
  const root = await fixture(t, { apiDelay: 400 });
  const occupied = http.createServer((_req, res) => res.end('unrelated'));
  await new Promise(resolve => occupied.listen(0, '127.0.0.1', resolve));
  t.after(() => new Promise(resolve => occupied.close(resolve)));
  const defaultPort = http.createServer((_req, res) => res.end('default port owner'));
  const ownsDefault = await new Promise(resolve => {
    defaultPort.once('error', () => resolve(false));
    defaultPort.listen(5080, '127.0.0.1', () => resolve(true));
  });
  if (ownsDefault) t.after(() => new Promise(resolve => defaultPort.close(resolve)));
  const running = launch(root, ['--no-install', '--no-browser', '--startup-timeout', '10']);
  t.after(() => stop(running));
  await until(() => running.output.includes('LearnForge is ready:') || running.exitCode !== null);
  assert.equal(running.exitCode, null, running.output);
  const record = JSON.parse(await fs.readFile(path.join(root, '.learnforge/instance.json'), 'utf8'));
  assert.equal(record.dataDir, path.join(root, 'apps/api/data'));
  if (ownsDefault) assert.notEqual(record.apiPort, 5080);
  assert.equal(await fs.readFile(path.join(root, 'proxy-target.txt'), 'utf8'), 'http://127.0.0.1:' + record.apiPort);
  assert.ok((await fs.readFile(path.join(root, 'validated.txt'), 'utf8')).includes(path.join('packs', 'nested', 'course.json')));
  assert.equal((await (await fetch(record.url + '/api/catalog')).json())[0].id, 'nested-course');
  const again = launch(root, ['--no-install', '--no-browser']);
  assert.equal(await again.closed, 0, again.output);
  assert.match(again.output, /already running/);
  await stop(running);
  assert.equal(running.exitCode, 0, running.output);
  assert.equal(processIdentity(record.api.pid), null);
  assert.equal(processIdentity(record.web.pid), null);
  await assert.rejects(fs.access(path.join(root, '.learnforge/install.lock')));
  assert.equal(await (await fetch('http://127.0.0.1:' + occupied.address().port)).text(), 'unrelated');
});

test('invalid nested course prevents any server from starting', { skip: process.platform === 'win32', timeout: 15000 }, async t => {
  const root = await fixture(t);
  await fs.writeFile(path.join(root, 'packs/nested/course.json'), '{bad');
  const running = launch(root, ['--no-install', '--no-browser']);
  assert.equal(await running.closed, 4, running.output);
  assert.match(running.output, /Invalid course JSON/);
  await assert.rejects(fs.access(path.join(root, 'api.pid')));
});

test('one server crashing makes the launcher fail and stops the companion', { skip: process.platform === 'win32', timeout: 20000 }, async t => {
  const root = await fixture(t, { crashAfter: 1200 });
  const running = launch(root, ['--no-install', '--no-browser', '--startup-timeout', '10']);
  t.after(() => stop(running));
  assert.equal(await running.closed, 5, running.output);
  assert.match(running.output, /stopped/);
  const pid = Number(await fs.readFile(path.join(root, 'api.pid'), 'utf8'));
  assert.equal(processIdentity(pid), null);
});

test('read-only checks create no state', { skip: process.platform === 'win32', timeout: 15000 }, async t => {
  const root = await fixture(t);
  const checked = launch(root, ['--check']);
  assert.equal(await checked.closed, 0, checked.output);
  await assert.rejects(fs.access(path.join(root, '.learnforge')));
});
