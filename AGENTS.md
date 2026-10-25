# Development and learning workflow

Build in a small, understandable, interview-ready style. Use the smallest architecture that meets the requirements, clear boundaries and names, and dependencies only for concrete problems. Finish one end-to-end workflow before expanding; explain important decisions in plain language.

Before work, read repository instructions, inspect code/docs/tests/history and Git status, preserve user changes, and make a short checklist. Rework on `shaoyu/rework`; preserve useful behavior and existing history. Rebuild only for a concrete reason.

For each coherent increment define acceptance criteria, then:
**implement → test → reproduce/investigate failures → fix → verify → inspect diff → commit**.
Include relevant code, tests and docs in focused commits. Add regression tests for meaningful bugs. Fix uncommitted mistakes in the same increment; fix previously committed behavior in a separate fix commit. Use descriptive messages and explain why substantial changes were needed and what passed. Report behavior, actual verification and commit hash after each increment.

Never claim checks passed without running them. Report blocked checks. Never commit secrets, generated artifacts or unrelated changes; never push or rewrite history without authorization.

After the baseline passes, create a separate learning branch with two or three plausible stack-relevant defects. Keep the working branch passing. Preserve reproducible faulty checkpoints; provide symptoms and reproduction steps without revealing causes. Label simulated reports, hypothetical consequences and example reasoning as practice. Prepare meaningful regression tests, keep diagnosis/solutions separate, let the learner investigate, and offer progressive hints only when asked. Verify eventual fixes and explain why they work.

Record two or three genuine engineering decisions: problem, alternatives, evidence, choice/tradeoffs and limitations. Distinguish local/synthetic measurements from production evidence.

Keep documentation concise: purpose/run commands, architecture/main workflow, checks/actual results, decisions, learner exercises/separate solutions and limitations. Prepare a 60-second introduction and three-minute walkthrough. Only after the learner completes exercises, derive interview stories from their actual notes: problem → hypothesis → evidence → decision → fix → verification. Include questions testing alternatives and stack understanding; never invent personal experience.

Finish the agreed scope, run required checks, inspect the final diff and commit verified agent-owned changes. Explain anything unfinished or unverified.

## Measure results and record resume evidence

Before implementing changes, read [docs/impact-and-resume-evidence.md](docs/impact-and-resume-evidence.md). Plan meaningful metrics and baseline/correctness checks before changing behavior; retain exact revisions, environment, workload, procedure and all raw results. Keep engineering, workflow and actual business impact separate. Update that evidence document with supported outcomes, contribution scope, limitations and verified/preliminary/unverified draft claims. Never invent numbers or convert synthetic benchmarks into real-user savings. Update an actual resume only when requested under its current instructions.
