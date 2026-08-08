import { FormEvent, useEffect, useMemo, useState } from 'react'
import { searchCrashGroups } from './api'
import type { CrashGroup, CrashGroupResult, Filters, RegressionStatus } from './types'

const emptyFilters: Filters = { query: '', project: '', build: '', platform: '', status: '' }
const emptyResult: CrashGroupResult = { totalCrashes: 0, returnedGroups: 0, items: [] }

function App() {
  const [draft, setDraft] = useState<Filters>(emptyFilters)
  const [filters, setFilters] = useState<Filters>(emptyFilters)
  const [result, setResult] = useState<CrashGroupResult>(emptyResult)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [refresh, setRefresh] = useState(0)

  useEffect(() => {
    const controller = new AbortController()
    setLoading(true)
    setError(null)
    searchCrashGroups(filters, controller.signal)
      .then(setResult)
      .catch((reason: unknown) => {
        if (!controller.signal.aborted) {
          setError(reason instanceof Error ? reason.message : 'Crash search failed')
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false)
      })
    return () => controller.abort()
  }, [filters, refresh])

  const stats = useMemo(() => ({
    regressions: result.items.filter(group => group.regressionStatus === 'regression').length,
    affectedBuilds: new Set(result.items.flatMap(group => group.affectedBuilds)).size,
    latest: result.items.reduce<string | null>((current, group) =>
      !current || group.lastSeenUtc > current ? group.lastSeenUtc : current, null)
  }), [result])

  const maxOccurrences = Math.max(...result.items.map(group => group.occurrences), 1)

  function submit(event: FormEvent) {
    event.preventDefault()
    setFilters(draft)
  }

  function clearFilters() {
    setDraft(emptyFilters)
    setFilters(emptyFilters)
  }

  return (
    <div className="shell">
      <header className="topbar">
        <div className="brand-mark" aria-hidden="true"><span /></div>
        <div>
          <p className="eyebrow">PipelineGuard</p>
          <h1>Crash Intelligence</h1>
        </div>
        <div className="service-state">
          <span className={error ? 'state-dot offline' : 'state-dot'} />
          {error ? 'Search unavailable' : loading ? 'Syncing index' : 'Index online'}
        </div>
      </header>

      <main>
        <section className="hero">
          <div>
            <p className="eyebrow">Operations overview</p>
            <h2>Find the crashes that matter.</h2>
            <p className="lede">Normalized Unreal call stacks, grouped by deterministic fingerprint and ranked by current impact.</p>
          </div>
          <button className="refresh" onClick={() => setRefresh(value => value + 1)} disabled={loading}>
            <span aria-hidden="true">↻</span> Refresh
          </button>
        </section>

        <section className="metrics" aria-label="Crash summary">
          <Metric label="Indexed crashes" value={formatNumber(result.totalCrashes)} tone="blue" />
          <Metric label="Visible groups" value={formatNumber(result.returnedGroups)} tone="violet" />
          <Metric label="Regressions" value={formatNumber(stats.regressions)} tone="red" />
          <Metric label="Affected builds" value={formatNumber(stats.affectedBuilds)} tone="amber" />
        </section>

        <form className="filters" onSubmit={submit}>
          <label className="search-field">
            <span>Search</span>
            <input
              value={draft.query}
              onChange={event => setDraft({ ...draft, query: event.target.value })}
              placeholder="Error, symbol, or message"
            />
          </label>
          <FilterInput label="Project" value={draft.project} placeholder="PipelineGuardHost" onChange={project => setDraft({ ...draft, project })} />
          <FilterInput label="Build" value={draft.build} placeholder="1.4.27" onChange={build => setDraft({ ...draft, build })} />
          <FilterInput label="Platform" value={draft.platform} placeholder="Linux" onChange={platform => setDraft({ ...draft, platform })} />
          <label>
            <span>Status</span>
            <select value={draft.status} onChange={event => setDraft({ ...draft, status: event.target.value })}>
              <option value="">All statuses</option>
              <option value="regression">Regression</option>
              <option value="new">New</option>
              <option value="ongoing">Ongoing</option>
              <option value="resolved">Resolved</option>
            </select>
          </label>
          <button className="apply" type="submit">Apply</button>
          <button className="clear" type="button" onClick={clearFilters}>Clear</button>
        </form>

        {error && <div className="error-banner" role="alert"><strong>Search failed.</strong> {error}</div>}

        <section className="groups-panel">
          <div className="panel-heading">
            <div>
              <p className="eyebrow">Fingerprint groups</p>
              <h3>Crash frequency</h3>
            </div>
            <p>{stats.latest ? `Latest occurrence ${relativeTime(stats.latest)}` : 'No indexed occurrences'}</p>
          </div>

          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Crash group</th>
                  <th>Status</th>
                  <th>Frequency</th>
                  <th>Builds</th>
                  <th>Last seen</th>
                </tr>
              </thead>
              <tbody>
                {loading && result.items.length === 0
                  ? Array.from({ length: 5 }, (_, index) => <SkeletonRow key={index} />)
                  : result.items.map(group => (
                    <CrashGroupRow key={group.fingerprint} group={group} maxOccurrences={maxOccurrences} />
                  ))}
              </tbody>
            </table>
          </div>

          {!loading && !error && result.items.length === 0 && (
            <div className="empty-state">
              <div className="empty-icon">⌁</div>
              <h4>No crash groups found</h4>
              <p>Ingest a report or broaden the current filters.</p>
            </div>
          )}
        </section>
      </main>
      <footer>Deterministic grouping · Idempotent indexing · OpenTelemetry monitored</footer>
    </div>
  )
}

function Metric({ label, value, tone }: { label: string; value: string; tone: string }) {
  return <article className={`metric ${tone}`}><span>{label}</span><strong>{value}</strong><i /></article>
}

function FilterInput({ label, value, placeholder, onChange }: {
  label: string
  value: string
  placeholder: string
  onChange: (value: string) => void
}) {
  return <label><span>{label}</span><input value={value} placeholder={placeholder} onChange={event => onChange(event.target.value)} /></label>
}

function CrashGroupRow({ group, maxOccurrences }: { group: CrashGroup; maxOccurrences: number }) {
  const width = `${Math.max((group.occurrences / maxOccurrences) * 100, 5)}%`
  return (
    <tr>
      <td>
        <div className="group-title"><span className="error-glyph">!</span><strong>{group.errorType}</strong></div>
        <p className="message" title={group.errorMessage}>{group.errorMessage}</p>
        <code title={group.representativeFrame}>{group.representativeFrame || group.fingerprint}</code>
      </td>
      <td><StatusBadge status={group.regressionStatus} /></td>
      <td>
        <strong className="frequency-number">{formatNumber(group.occurrences)}</strong>
        <div className="frequency-bar"><i style={{ width }} /></div>
      </td>
      <td>
        <strong>{group.affectedBuildCount}</strong>
        <p className="builds" title={group.affectedBuilds.join(', ')}>{group.affectedBuilds.slice(0, 2).join(', ') || '—'}</p>
      </td>
      <td><strong>{relativeTime(group.lastSeenUtc)}</strong><p>{formatDate(group.lastSeenUtc)}</p></td>
    </tr>
  )
}

function StatusBadge({ status }: { status: RegressionStatus }) {
  return <span className={`status ${status}`}><i />{status}</span>
}

function SkeletonRow() {
  return <tr className="skeleton"><td><i /><i /></td><td><i /></td><td><i /></td><td><i /></td><td><i /></td></tr>
}

function formatNumber(value: number) {
  return new Intl.NumberFormat('en-US', { notation: value >= 10_000 ? 'compact' : 'standard', maximumFractionDigits: 1 }).format(value)
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat('en-US', { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' }).format(new Date(value))
}

function relativeTime(value: string) {
  const elapsed = Date.now() - new Date(value).getTime()
  const minutes = Math.max(Math.floor(elapsed / 60_000), 0)
  if (minutes < 1) return 'just now'
  if (minutes < 60) return `${minutes}m ago`
  const hours = Math.floor(minutes / 60)
  if (hours < 24) return `${hours}h ago`
  return `${Math.floor(hours / 24)}d ago`
}

export default App
