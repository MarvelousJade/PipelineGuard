// Real loopback HTTP/SQLite smoke. Uses an isolated database and only its own child process.
import assert from 'node:assert/strict'
import { spawn } from 'node:child_process'
import { mkdtemp, readFile, rm } from 'node:fs/promises'
import { createServer } from 'node:net'
import { tmpdir } from 'node:os'
import { join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { setTimeout as delay } from 'node:timers/promises'

const root = fileURLToPath(new URL('../', import.meta.url))
const directory = await mkdtemp(join(tmpdir(), 'pipelineguard-smoke-'))
const socket = createServer()
await new Promise((done, reject) => { socket.once('error', reject); socket.listen(0, '127.0.0.1', done) })
const port = socket.address().port
await new Promise(done => socket.close(done))
const base = `http://127.0.0.1:${port}`
let child
let logs = ''

async function start() {
  logs = ''
  child = spawn('dotnet', [resolve(root, 'src/PipelineGuard.Api/bin/Release/net8.0/PipelineGuard.Api.dll')], {
    cwd: root,
    env: { ...process.env, ASPNETCORE_URLS: base, PipelineGuard__DatabasePath: join(directory, 'runs.db') },
    stdio: ['ignore', 'pipe', 'pipe']
  })
  let spawnError
  child.once('error', error => { spawnError = error })
  child.stdout.on('data', chunk => { logs = (logs + chunk).slice(-16000) })
  child.stderr.on('data', chunk => { logs = (logs + chunk).slice(-16000) })
  for (let attempt = 0; attempt < 100; attempt++) {
    if (spawnError) throw spawnError
    if (child.exitCode !== null) throw new Error(`API exited before readiness: ${logs}`)
    try {
      const response = await fetch(`${base}/health`, { signal: AbortSignal.timeout(1000) })
      if (response.ok) return
    } catch { /* A refused connection is expected while the child starts. */ }
    await delay(100)
  }
  throw new Error(`API did not become ready: ${logs}`)
}

async function stop() {
  if (!child || child.exitCode !== null) return
  const stopped = new Promise(done => child.once('exit', done))
  child.kill('SIGTERM')
  const timer = setTimeout(() => child.kill('SIGKILL'), 5000)
  await stopped
  clearTimeout(timer)
}

async function request(path, expected, report) {
  const response = await fetch(`${base}${path}`, {
    method: report === undefined ? 'GET' : 'POST',
    headers: report === undefined ? {} : { 'Content-Type': 'application/json' },
    body: report === undefined ? undefined : JSON.stringify(report),
    signal: AbortSignal.timeout(5000)
  })
  assert.equal(response.status, expected, `${path}: ${await response.clone().text()}`)
  return response.json()
}

try {
  await start()
  const fixture = JSON.parse(await readFile(resolve(root, 'samples/clean-report.json'), 'utf8'))
  const older = { ...fixture, runId: '00000000-0000-0000-0000-000000000001',
    startedUtc: '2026-07-16T12:00:00+03:00', completedUtc: '2026-07-16T12:00:00+03:00' }
  const newer = { ...fixture, runId: '00000000-0000-0000-0000-000000000002',
    startedUtc: '2026-07-16T10:00:00Z', completedUtc: '2026-07-16T10:00:00Z' }
  await request('/api/v1/runs', 201, older)
  await request('/api/v1/runs', 201, newer)
  await request('/api/v1/runs', 409, { ...older, project: 'Replacement' })
  await request('/api/v1/runs', 400, { ...fixture, schemaVersion: '9.0' })
  await request('/api/v1/runs/00000000-0000-0000-0000-000000000099', 404)
  await stop()
  await start()
  assert.deepEqual(await request(`/api/v1/runs/${older.runId}`, 200), older)
  const page = await request('/api/v1/runs?limit=1&offset=0', 200)
  assert.equal(page.items[0].runId, newer.runId)
  const next = await request('/api/v1/runs?limit=1&offset=1', 200)
  assert.equal(next.items[0].runId, older.runId)
  assert.equal((await request('/api/v1/runs', 200)).items.length, 2)
  console.log('PASS: HTTP ingest, duplicate preservation, rejection, missing run, restart and chronological pagination')
} finally {
  await stop()
  await rm(directory, { recursive: true, force: true })
}
