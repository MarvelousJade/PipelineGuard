# Interview guide — understand, don't memorize

These are project explanations, **not claims about the learner's personal experience**. Adapt them after actually tracing the code. Earlier Unreal/Compose work belongs to the repository's recorded history, not automatically to this rework or to you.

## 60-second introduction

“PipelineGuard has two separate workflows: Unreal content validation and crash grouping. I would demonstrate validation first because it needs only one .NET API and SQLite. A strict JSON contract catches invalid reports; accepted reports are stored once by run ID and can be retrieved after restart. The focused rework corrected timestamp ordering and tested duplicate preservation through real loopback HTTP. The optional crash system uses RabbitMQ and immutable Elasticsearch documents. Its HTTP boundary now has deterministic tests for replay, dependency failures and cancellation. The portable suites have 83 passing tests; real engine and broker benchmarks are historical evidence, not something this rework reran. The main tradeoff is keeping a simple local architecture without pretending it is a production multi-tenant service.”

## Three-minute technical walkthrough

**0:00–0:45 — boundary and contract.** Open `src/PipelineGuard.Api/Program.cs`: one bounded upload handler calls `JsonReportSerializer.Deserialize`. Trace into the contracts validator: required fields, version, findings and summary counts. Explain why shape validation alone can't check cross-field totals. CLI uses the same serializer without needing a server.

**0:45–1:30 — persistence.** Open `SqliteReportStore.TryAddAsync`: parameterized SQL, run ID primary key, no replacement on duplicate. Distinguish original report JSON from sortable metadata. Explain UTC instant versus local clock; show why 12:00+03:00 is earlier than 10:00Z. Canonical seven-digit UTC strings retain tick precision and support lexical ordering. A deterministic run-ID tie-breaker stabilizes pages for a fixed dataset. Startup repairs old metadata atomically, but scans are a local-store tradeoff.

**1:30–2:15 — optional crash workflow.** Trace ingestion → confirmed durable publish → consumer → fingerprint → `ElasticsearchCrashIndex.TryCreateAsync`. Acknowledgment follows indexing. A replay must not overwrite the first document: a unique ID alone is insufficient if the write is an upsert. Only 404 means “create index”; 503 means unavailable, not absent. Caller cancellation should not be mislabeled as an HTTP timeout. Retry scheduling stays in one place, the consumer.

**2:15–3:00 — evidence and limits.** Run `node scripts/verify-validation.mjs` after the Release build; describe exactly what it checks. Contrast actual SQLite/HTTP with synthetic crash HTTP tests and historical Compose/Unreal evidence. Discuss startup migration cost, offset pagination during inserts, incomplete OpenAPI/editor controls and lack of production auth. Finish with what evidence would justify a cursor, versioned migration or real-cluster tests.

## Questions to test understanding

1. Why doesn't storing ISO-8601 strings automatically guarantee chronological sorting? What does the tie-breaker guarantee, and what doesn't it?
2. Why preserve uploaded JSON while normalizing metadata? What happens if a legacy date is corrupted halfway through migration?
3. When would integer ticks or a versioned migration be better? What experiment would compare startup cost at realistic volume?
4. Why doesn't “same crash ID” alone prove idempotency? What happens if the worker dies after indexing but before acknowledging?
5. How would you separate caller cancellation, server overload, auth failure and mapping failure? Which layer should retry?
6. What can the synthetic handler prove, and what requires a real RabbitMQ/Elasticsearch test?
7. How could bounded status aggregation miss a group? What changes would true multi-tenant isolation require?

## Completed agent investigations — explanations, not personal stories

Both intentional exercises were completed autonomously by the agent with access to the separate solutions. These summaries describe actual local agent checks, not blind discoveries or the user's experience. See [investigation and integration evidence](../learning-investigations/README.md).

### History chronology (`d8a0a8f`)

**Problem →** older report listed first after restart. **Candidate explanation →** offset-preserving strings were sorted as local clocks (solution-assisted). **Evidence →** 3/6 SQLite tests failed; real HTTP smoke returned run `001` instead of `002`; independent conversion showed 12:00+03:00 = 09:00Z. **Decision →** normalize metadata rather than change UI order or rewrite uploaded JSON. **Fix →** restore UTC conversion before round-trip formatting. **Verification →** all 55 validation tests, zero-warning build and restart smoke passed.

Follow-ups: Why does invariant formatting not imply UTC? Why is a tie-breaker still needed? What would a cursor guarantee under concurrent inserts that offset pages do not? When would startup normalization justify a versioned migration?

### Immutable replay (`1d5a25c`)

**Problem →** same-ID replay changed stored processing time. **Candidate explanation →** ordinary PUT replaced the document despite stable identity (solution-assisted). **Evidence →** adapter regression failed with 12:05Z becoming 12:06Z while the other 27 crash tests, including in-memory duplicate processing, passed. **Decision →** let storage atomically enforce create-if-absent, avoiding GET-before-PUT races. **Fix →** restore `op_type=create` and retain conflict-as-duplicate handling. **Verification →** focused regression and all 28 crash tests passed; both builds and HTTP smoke passed.

Follow-ups: Why can document count stay correct while audit evidence changes? What happens after index success but before acknowledgment? Why is an in-memory set insufficient across worker replicas? What does the synthetic handler fail to prove, and how would a real replay test strengthen the evidence?

## For the user's own investigation

Keep notes using this template; don't fill it with invented results:

- **Problem:** observed symptom and exact checkpoint.
- **Hypothesis:** competing explanations before inspecting the cause.
- **Evidence:** commands, inputs, outputs and what each ruled out.
- **Decision:** chosen fix versus alternatives and limitations.
- **Fix:** your actual diff/commit.
- **Verification:** failing-before/passing-after regression, wider checks and untested cases.
- **Learning:** what changed in your mental model; next question to investigate.

Share those notes for concise interview stories and follow-up questions based on what you personally investigated, changed and verified. Until then, no learner debugging story or experience claim is prepared.
