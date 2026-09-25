#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
node - <<'JS'
const net = require('node:net');
Promise.all([5080,4300].map(port => new Promise((resolve,reject) => {
  const server = net.createServer();
  server.once('error', () => reject(new Error(`Port ${port} is busy. Stop its owner or use the manual setup guide.`)));
  server.listen(port,'127.0.0.1',()=>server.close(resolve));
}))).catch(error => { console.error(error.message); process.exitCode=1; });
JS
ASPNETCORE_ENVIRONMENT=Development dotnet run --project apps/api --no-launch-profile -- --urls http://127.0.0.1:5080 &
api_pid=$!
npm start --prefix apps/web &
web_pid=$!
cleanup() { kill "$api_pid" "$web_pid" 2>/dev/null || true; }
trap cleanup EXIT INT TERM
echo 'LearnForge: http://127.0.0.1:4300 (API: 5080). Press Ctrl+C to stop.'
wait
