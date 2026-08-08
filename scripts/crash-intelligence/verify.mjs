#!/usr/bin/env node
import { randomUUID } from 'node:crypto'
import { execFileSync } from 'node:child_process'
import { mkdir, writeFile } from 'node:fs/promises'
import { dirname } from 'node:path'
import process from 'node:process'

const options = parseArguments(process.argv.slice(2))
const project = `CrashBenchmark-${new Date().toISOString().replace(/[^0-9]/g, '').slice(0, 14)}`
const groupCount = Math.min(options.groups, options.count)
const reports = Array.from({ length: options.count }, (_, index) => createReport(index, groupCount, project))
const evidence = {
  schemaVersion: '1.0',
  startedAtUtc: new Date().toISOString(),
  api: options.api,
  project,
  requestedReports: options.count,
  semanticGroups: groupCount,
  concurrency: options.concurrency,
  results: {}
}

console.log(`Benchmark project: ${project}`)
console.log(`Ingesting ${reports.length.toLocaleString()} synthetic reports...`)
const ingestionLatencies = await postReports(reports)
evidence.results.ingestion = {
  accepted: ingestionLatencies.length,
  p50Milliseconds: percentile(ingestionLatencies, 50),
  p95Milliseconds: percentile(ingestionLatencies, 95),
  p99Milliseconds: percentile(ingestionLatencies, 99)
}
await waitForQueueDrain()
await waitForIndexedCount(options.count)

const grouped = await searchGroups({ project, limit: String(groupCount) })
const expectedCounts = Array.from({ length: groupCount }, (_, group) =>
  Math.floor((options.count + groupCount - group - 1) / groupCount)).sort((a, b) => a - b)
const actualCounts = grouped.items.map(item => item.occurrences).sort((a, b) => a - b)
const correctlyGrouped = expectedCounts.length === actualCounts.length
  ? expectedCounts.reduce((sum, expected, index) =>
      sum + Math.min(expected, actualCounts[index] ?? 0), 0)
  : 0
evidence.results.grouping = {
  expectedGroups: groupCount,
  actualGroups: grouped.returnedGroups,
  correctlyGroupedReports: correctlyGrouped,
  equivalentStackGroupingPercent: round((correctlyGrouped / options.count) * 100)
}

console.log('Replaying every message to verify idempotency...')
await postReports(reports)
await waitForQueueDrain()
const afterReplay = await searchGroups({ project, limit: '1' })
evidence.results.replay = {
  messagesReplayed: reports.length,
  indexedDocuments: afterReplay.totalCrashes,
  duplicateEffects: Math.max(afterReplay.totalCrashes - options.count, 0),
  zeroDuplicateRecords: afterReplay.totalCrashes === options.count
}

console.log(`Measuring ${options.searchSamples} search requests...`)
const searchLatencies = []
for (let index = 0; index < options.searchSamples; index++) {
  const started = performance.now()
  await searchGroups({ project, query: `Scenario${index % groupCount}`, limit: '20' })
  searchLatencies.push(performance.now() - started)
}
evidence.results.search = {
  samples: searchLatencies.length,
  p50Milliseconds: percentile(searchLatencies, 50),
  p95Milliseconds: percentile(searchLatencies, 95),
  p99Milliseconds: percentile(searchLatencies, 99)
}

if (!options.skipRecovery) {
  console.log('Stopping the worker and queuing reports for the recovery test...')
  compose('stop', options.workerService)
  let workerRestarted = false
  try {
    const recoveryReports = Array.from({ length: options.recoveryCount }, (_, index) =>
      createReport(options.count + index, groupCount, project))
    await postReports(recoveryReports)
    await waitForQueueReady(options.recoveryCount)

    const recoveryStarted = performance.now()
    compose('start', options.workerService)
    workerRestarted = true
    await waitForIndexedCount(options.count + options.recoveryCount)
    const recoveryMilliseconds = performance.now() - recoveryStarted
    evidence.results.workerRecovery = {
      reportsQueuedWhileStopped: options.recoveryCount,
      recoveryTimeMilliseconds: round(recoveryMilliseconds),
      recoveryTimeSeconds: round(recoveryMilliseconds / 1000),
      indexedDocumentsAfterRecovery: options.count + options.recoveryCount
    }
  } finally {
    if (!workerRestarted) compose('start', options.workerService)
  }
}

evidence.completedAtUtc = new Date().toISOString()
const timestamp = evidence.completedAtUtc.replace(/[:.]/g, '-').toLowerCase()
const output = options.output ?? `docs/crash-intelligence/evidence/benchmark-${timestamp}.json`
await mkdir(dirname(output), { recursive: true })
await writeFile(output, `${JSON.stringify(evidence, null, 2)}\n`)
console.log(`Evidence written to ${output}`)
console.log(JSON.stringify(evidence.results, null, 2))

if (!evidence.results.replay.zeroDuplicateRecords ||
    evidence.results.grouping.equivalentStackGroupingPercent !== 100) {
  process.exitCode = 1
}

async function postReports(items) {
  const latencies = new Array(items.length)
  let cursor = 0
  const workers = Array.from({ length: Math.min(options.concurrency, items.length) }, async () => {
    while (true) {
      const index = cursor++
      if (index >= items.length) return
      const started = performance.now()
      const response = await fetch(`${options.api}/api/v1/crashes`, {
        method: 'POST',
        headers: { 'content-type': 'application/json' },
        body: JSON.stringify(items[index]),
        signal: AbortSignal.timeout(30_000)
      })
      latencies[index] = performance.now() - started
      if (response.status !== 202) {
        throw new Error(`Ingestion ${index} returned HTTP ${response.status}: ${await response.text()}`)
      }
    }
  })
  await Promise.all(workers)
  return latencies
}

async function searchGroups(parameters) {
  const query = new URLSearchParams(parameters)
  const response = await fetch(`${options.api}/api/v1/crash-groups?${query}`, {
    signal: AbortSignal.timeout(30_000)
  })
  if (!response.ok) throw new Error(`Search returned HTTP ${response.status}: ${await response.text()}`)
  return response.json()
}

async function waitForIndexedCount(expected) {
  await waitUntil(async () => {
    const result = await searchGroups({ project, limit: '1' })
    return result.totalCrashes === expected
  }, `Elasticsearch count ${expected}`, options.timeoutSeconds)
}

async function waitForQueueDrain() {
  if (options.skipRabbitCheck) {
    await delay(10_000)
    return
  }

  await waitUntil(async () => {
    const queue = await rabbitQueue('pipelineguard.crash.processing')
    return queue.messages_ready === 0 && queue.messages_unacknowledged === 0
  }, 'RabbitMQ queue drain', options.timeoutSeconds)
}

async function waitForQueueReady(minimum) {
  if (options.skipRabbitCheck) {
    await delay(2_000)
    return
  }

  await waitUntil(async () => (await rabbitQueue('pipelineguard.crash.processing')).messages_ready >= minimum,
    `${minimum} ready RabbitMQ messages`, 30)
}

async function rabbitQueue(name) {
  const credentials = Buffer.from(`${options.rabbitUser}:${options.rabbitPassword}`).toString('base64')
  const response = await fetch(`${options.rabbitManagement}/api/queues/%2F/${encodeURIComponent(name)}`, {
    headers: { authorization: `Basic ${credentials}` },
    signal: AbortSignal.timeout(10_000)
  })
  if (!response.ok) throw new Error(`RabbitMQ management returned HTTP ${response.status}`)
  return response.json()
}

async function waitUntil(predicate, description, timeoutSeconds) {
  const deadline = Date.now() + timeoutSeconds * 1000
  while (Date.now() < deadline) {
    if (await predicate()) return
    await delay(1000)
  }
  throw new Error(`Timed out waiting for ${description}`)
}

function createReport(index, groups, benchmarkProject) {
  const group = index % groups
  const variant = Math.floor(index / groups)
  return {
    schemaVersion: '1.0',
    crashId: randomUUID(),
    occurredAtUtc: new Date(Date.now() - (index % 3_600) * 1000).toISOString(),
    project: benchmarkProject,
    build: {
      version: `benchmark-1.${variant % 5}`,
      branch: 'crash-intelligence-benchmark',
      changelist: 200_000 + variant
    },
    platform: variant % 2 === 0 ? 'Linux' : 'Win64',
    errorType: 'AccessViolation',
    errorMessage: `Synthetic access violation in scenario ${group}; entity ${variant}`,
    stackTrace: [
      `${address(index)} UnrealEditor-Game.${variant % 2 === 0 ? 'so' : 'dll'}!UCrashScenario${group}::Execute(${variant % 2 === 0 ? 'int32' : 'long'}) [/agent/run-${variant}/Scenario.cpp:${100 + variant}]`,
      `${address(index + 1)} UnrealEditor-Game.${variant % 2 === 0 ? 'so' : 'dll'}!UGameInstance::Tick(${variant % 2 === 0 ? 'float' : 'double'}) + 0x${(variant + 15).toString(16)} [/agent/run-${variant}/Game.cpp:${200 + variant}]`
    ],
    metadata: { syntheticGroup: String(group), generator: 'verify.mjs' }
  }
}

function address(value) {
  return `0x${(0x7ff000000000n + BigInt(value)).toString(16).padStart(16, '0')}`
}

function compose(action, service) {
  execFileSync(options.dockerCommand, ['compose', action, service], { stdio: 'inherit' })
}

function percentile(values, percentileValue) {
  const sorted = [...values].sort((a, b) => a - b)
  const index = Math.max(Math.ceil((percentileValue / 100) * sorted.length) - 1, 0)
  return round(sorted[index] ?? 0)
}

function round(value) {
  return Math.round(value * 100) / 100
}

function delay(milliseconds) {
  return new Promise(resolve => setTimeout(resolve, milliseconds))
}

function parseArguments(argumentsList) {
  const values = new Map()
  const flags = new Set()
  for (let index = 0; index < argumentsList.length; index++) {
    const argument = argumentsList[index]
    if (!argument.startsWith('--')) throw new Error(`Unknown argument: ${argument}`)
    if (['--skip-recovery', '--skip-rabbit-check'].includes(argument)) {
      flags.add(argument)
    } else {
      values.set(argument, argumentsList[++index])
    }
  }

  const integer = (name, fallback) => {
    const parsed = Number.parseInt(values.get(name) ?? String(fallback), 10)
    if (!Number.isSafeInteger(parsed) || parsed < 1) throw new Error(`${name} must be a positive integer`)
    return parsed
  }

  return {
    api: (values.get('--api') ?? 'http://127.0.0.1:5080').replace(/\/$/, ''),
    count: integer('--count', 10_000),
    groups: integer('--groups', 100),
    concurrency: integer('--concurrency', 32),
    searchSamples: integer('--search-samples', 100),
    recoveryCount: integer('--recovery-count', 100),
    timeoutSeconds: integer('--timeout-seconds', 300),
    rabbitManagement: (values.get('--rabbit-management') ?? 'http://127.0.0.1:15672').replace(/\/$/, ''),
    rabbitUser: values.get('--rabbit-user') ?? 'pipelineguard',
    rabbitPassword: values.get('--rabbit-password') ?? 'pipelineguard-local',
    workerService: values.get('--worker-service') ?? 'crash-worker',
    dockerCommand: values.get('--docker-command') ?? 'docker',
    output: values.get('--output'),
    skipRecovery: flags.has('--skip-recovery'),
    skipRabbitCheck: flags.has('--skip-rabbit-check')
  }
}
