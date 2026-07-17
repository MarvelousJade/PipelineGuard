# PG-002 — Advertised OpenAPI 3.1 operations omit required response definitions

| Field | Value |
| --- | --- |
| Status | Open |
| Severity / priority | Medium / P2 |
| Component / version | ASP.NET API contract / revision `b90675b` |
| Source case / run | PG-API-006 / source review; runtime validator pending |
| Environment | Source-confirmed; local API execution blocked by missing ASP.NET Core 8 runtime |
| Reproducibility | Static inspection: 1/1 |

## Preconditions

Treat `/openapi/v1.json` as an OpenAPI 3.1 document because it declares `openapi: 3.1.0` and is advertised by the API root.

## Steps to reproduce

1. Inspect the object returned by `MapGet("/openapi/v1.json", ...)` in `src/PipelineGuard.Api/Program.cs`.
2. Compare each Path Item operation with the OpenAPI 3.1 Operation Object requirements.
3. When a compatible runtime is available, GET the document and submit it to an OpenAPI 3.1 validator.

## Expected result

Every advertised operation contains a required `responses` object; the run-ID path declares its path parameter, and the document validates without structural errors.

## Actual result

The generated operations contain only `summary`. They omit `responses`; `/api/v1/runs/{runId}` also omits a path-parameter definition. An OpenAPI 3.1 Operation Object requires `responses`.

## Impact, evidence, and workaround

- Impact: validators and client generators may reject the advertised document, so it is not a reliable machine-readable API contract.
- Evidence: `src/PipelineGuard.Api/Program.cs` lines 200–214 at revision `b90675b`; OpenAPI 3.1 Operation Object specification.
- Workaround: use the endpoint behavior documented in `README.md` and do not generate clients from the current document.

## Resolution and QA retest

- Root cause: the endpoint returns a hand-built discovery summary rather than a complete OpenAPI document.
- Fix revision: none.
- Focused retest: PG-API-006 with a named OpenAPI 3.1 validator.
- Regression impact: PG-API-001 through PG-API-005 plus an automated schema/document validation test.
