---
name: openspec-apply-change
description: Implement tasks from an OpenSpec change. Use when the user wants to start implementing, continue implementation, or work through tasks.
allowed-tools: Bash(openspec:*)
license: MIT
metadata:
  author: openspec
  version: "1.0"
  generatedBy: "1.6.0"
---

Implement tasks from an OpenSpec change.

**Store selection:** For a named or detected registered store, resolve its id with `openspec store list --json` and preserve the CLI-provided `--store <id>` on supported follow-ups. Otherwise use the nearest `openspec/` root.

**Input:** Use a named or context-unambiguous Change; otherwise list the available Changes and ask the user.

**Steps**

1. **Select the change**

   If a name is provided, use it. Otherwise:
   - Infer from conversation context if the user mentioned a change
   - Auto-select if only one active change exists
   - If ambiguous, run `openspec list --json` and ask the user to select through the available input mechanism

   Always announce: "Using change: <name>" and how to override (e.g., `/opsx:apply <other>`).

2. **Check status to understand the schema**
   ```bash
   openspec status --change "<name>" --json
   ```
   Parse the JSON to understand:
   - `schemaName`: The workflow being used (e.g., "spec-driven")
   - `planningHome`, `changeRoot`, and `actionContext`: planning scope and edit constraints
   - Which artifact contains the tasks (typically "tasks" for spec-driven, check status for others)

3. **Get apply instructions**

   ```bash
   openspec instructions apply --change "<name>" --json
   ```

   This returns:
   - `contextFiles`: artifact ID -> array of concrete file paths (varies by schema - could be proposal/specs/design/tasks or spec/tests/implementation/docs)
   - Progress (total, complete, remaining)
   - Task list with status
   - Dynamic instruction based on current state

   **Handle states:**
   - If `state: "blocked"` (missing artifacts): show them and suggest `/opsx:propose <change>` to complete the existing proposal
   - If `state: "all_done"`: skip code edits and report that explicit Spec sync is available
   - Otherwise: proceed to implementation

4. **Read context files**

   Read every file path listed under `contextFiles` from the apply instructions output.
   The files depend on the schema being used:
   - **spec-driven**: proposal, specs, design, tasks
   - Other schemas: follow the contextFiles from CLI output

5. **Check capability readiness**

   Read `<changeRoot>/change-review.json`, `<changeRoot>/dependencies.json` and `<changeRoot>/gaps.json` first; these are the Change's persisted feasibility and local dependency subtree. Then use `openspec/spec-metadata/dependencies.json` only to resolve dependencies outside the Change:
   - If `change-review.json.implementationNotes` is non-empty, read it before selecting the first task and treat it as user-authored implementation constraints for this apply. It may narrow or defer task implementation, but must not silently contradict approved Specs or acceptance criteria; pause and ask when it does.
   - Before any code edit, inspect every `verification.codeEvidence` in the Change and its capability reviews. Resolve the current script by GUID and recompute its full-file SHA-256. Unchanged evidence is reused. For every changed hash, reread the complete script and verify the recorded `feature` semantically: if it still holds, update path/line/hash plus `status=verified` and `checkedAt`, then continue apply automatically; never refresh a hash without rereading the script.
   - If changed evidence no longer supports its recorded feature, stop before implementation tasks and reconcile the Change's Verification, Review Issues, implementation differences, Tasks and affected planning text. Re-run readiness and strict validation. Continue only when the approved behavior and acceptance criteria remain unchanged; if the correction changes approved scope or behavior, pause for user review before applying code.
   - Require schema-v5 `change-review.json` with `approvalStatus=implementation-change`. A Draft (`approvalStatus=draft`) must not be applied.
   - Reject any unresolved Review Issue with `blocksApproval=true`. A blocking issue can never be bypassed by `accepted`; warning/info are valid only when explicitly accepted with audit fields.
   - `codeReadiness` must be `unimplemented`, `partial`, or `not-applicable`. A paired Change may contain game-rule, feature and system deltas; code tasks belong to implementation deltas only. Normalize legacy `architecture` to `system` while reading.
   - Map the change's delta spec capability folders and categories to dependency nodes.
   - Reject invalid category edges: system -> non-system, game-rule -> non-feature, or feature -> game-rule. Legacy `architecture` is valid only through normalization to `system`.
   - Every Gap must be a missing dependency node/contract referenced by one open edge; non-dependency gaps are schema errors and must be repaired before code edits.
   - `ready` and `ready-with-deferred-gaps` may proceed.
   - `blocked-by-design` and `blocked-by-integration` must stop before code edits.
   - An accepted hard prerequisite remains blocking until its gap/edge is `resolved`.
   - An external dependency is implementation-ready when provided by a formal Spec, or by an active `implementation-change` whose Tasks are complete, `verification.status=verified|implemented`, `codeReadiness=implemented`, `readiness=ready|implemented`, and no blocker remains. Draft approval may depend on a merely approved Change, but apply must enforce this stricter check. Applied-but-unsynced dependencies remain pending deltas and cannot be archived before sync.
   - Show the minimal blocking subtree (`requires` / `integrates-with`) and recommend updating the proposal to cover that subtree or completing the prerequisite change first.
   - Do not invent a temporary interface to bypass the dependency.
   - Verify every checkbox in `tasks.md` represents an implementation delta from Verification. If it is a design question or dependency waiting item, stop and return it to Draft review instead of implementing it.

6. **Show current progress**

   Display:
   - Schema being used
   - Progress: "N/M tasks complete"
   - Remaining tasks overview
   - Dynamic instruction from CLI

7. **Implement tasks (loop until done or blocked)**

   For each pending task:
   - Show which task is being worked on
   - Make the code changes required
   - Keep changes minimal and focused
   - Mark task complete in the tasks file: `- [ ]` → `- [x]`
   - Continue to next task
   - Preserve the design's ownership boundary: pure C# gameplay logic first, Unity adapters/components only where lifecycle, scene identity, serialized references, or presentation require them; prefer existing composition roots and centralized configuration over scattered per-feature MonoBehaviours.
   - If the capability has `spec-review.json.editorGuidance`, implement the exposed fields/assets/entry points consistently with it. Treat any manual scene/Inspector action as a handoff to report and verify, not as permission to create extra components.
   - For every implemented Feature/System capability, update its `spec-review.json.implementationOutline` to match the actual core data types, decisions and call flow. Use a few pseudo-code-like sentences; do not copy Requirements or list every method. Game Rule reviews do not contain this field.
   - For a paired Change, treat the Feature as the implementation owner and complete its shared tasks/verification; the paired Game Rule has no independent apply action. A later explicit sync merges both deltas atomically after implementation and Change-wide dependency checks pass.

   **Pause if:**
   - Task is unclear → ask for clarification
   - Implementation reveals a design issue → suggest updating artifacts
   - Error or blocker encountered → report and wait for guidance
   - User interrupts

8. **Stop at the Change sandbox boundary**

   Apply only changes implementation code, tests, task checkboxes, verification and `implementationOutline` inside the active Change. It MUST NOT create, edit or merge anything under `openspec/specs/` or `openspec/spec-metadata/`, and MUST NOT invoke `openspec-sync-specs`. When implementation finishes, keep `change-review.json.specSyncStatus=pending` so the human can review the implemented Change and request adjustments before explicit sync. Each Change remains an independent sandbox until that manual sync.

   Do not copy ordinary implementation output into the incremental refactor queue: source files, Git and the active Change already provide those records. If apply reveals deferred technical debt, a non-functional refactor or an architecture risk that is intentionally left outside the approved Change, add or update one actionable queue item through `project-refactor-queue`; do not use the queue for completed work or as a duplicate change log.

9. **On completion or pause, show status**

   Display:
   - Tasks completed this session
   - Overall progress: "N/M tasks complete"
   - If all done: suggest explicit sync; archive remains blocked until sync succeeds
   - If paused: explain why and wait for guidance

**Output:** During implementation, show the current task and progress. On completion, list tasks completed this session, total progress, and the explicit sync next step. On pause, report the blocker and available options without guessing.

**Guardrails**
- Continue until done or blocked; read all reported context and enforce readiness before editing.
- Keep each task minimal, update its checkbox immediately, and pause on ambiguity or design issues.
- Never create or modify a formal Spec during Draft approval or apply; only an explicit user-triggered sync may affect formal Specs
- Use CLI-reported `contextFiles`; do not assume file names.

**Fluid Workflow Integration**

This skill supports the "actions on a change" model:

- **Can be invoked anytime**: Before all artifacts are done (if tasks exist), after partial implementation, interleaved with other actions
- **Allows artifact updates**: If implementation reveals design issues, suggest updating artifacts - not phase-locked, work fluidly
