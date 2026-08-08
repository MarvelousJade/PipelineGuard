import type { CrashGroupResult, Filters } from './types'

export async function searchCrashGroups(filters: Filters, signal: AbortSignal): Promise<CrashGroupResult> {
  const parameters = new URLSearchParams({ limit: '100' })
  Object.entries(filters).forEach(([key, value]) => {
    if (value.trim()) parameters.set(key, value.trim())
  })

  const response = await fetch(`/api/v1/crash-groups?${parameters.toString()}`, { signal })
  if (!response.ok) {
    const problem = await response.json().catch(() => null) as { title?: string } | null
    throw new Error(problem?.title ?? `Search failed with HTTP ${response.status}`)
  }

  return response.json() as Promise<CrashGroupResult>
}
