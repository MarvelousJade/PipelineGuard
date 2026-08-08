export type RegressionStatus = 'new' | 'ongoing' | 'resolved' | 'regression'

export interface CrashGroup {
  fingerprint: string
  occurrences: number
  firstSeenUtc: string
  lastSeenUtc: string
  affectedBuildCount: number
  affectedBuilds: string[]
  errorType: string
  errorMessage: string
  representativeFrame: string
  regressionStatus: RegressionStatus
  resolvedInBuild?: string
}

export interface CrashGroupResult {
  totalCrashes: number
  returnedGroups: number
  items: CrashGroup[]
}

export interface Filters {
  query: string
  project: string
  build: string
  platform: string
  status: string
}
