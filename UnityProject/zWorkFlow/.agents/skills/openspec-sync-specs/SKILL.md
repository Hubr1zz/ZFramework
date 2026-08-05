---
name: openspec-sync-specs
description: Sync delta specs from a change to main specs. Use when the user wants to update main specs with changes from a delta spec, without archiving the change.
allowed-tools: Bash(openspec:*)
license: MIT
metadata:
  author: openspec
  version: "1.0"
  generatedBy: "1.6.0"
---

Sync delta specs from a change to main specs.

This is an **agent-driven** operation - you will read delta specs and directly edit main specs to apply the changes. This allows intelligent merging (e.g., adding a scenario without copying the entire requirement).

**Store selection:** For a named or detected registered store, resolve its id with `openspec store list --json` and preserve the CLI-provided `--store <id>` on supported follow-ups. Otherwise use the nearest `openspec/` root.

**Input:** Use a named or context-unambiguous Change; otherwise list the available Changes and ask the user.

**Steps**

1. **If no change name provided, prompt for selection**

   Run `openspec list --json` and ask the user to select through the available input mechanism.

   Show changes that have delta specs (under `specs/` directory).

2. **Resolve change context**

   Run:
   ```bash
   openspec status --change "<name>" --json
   ```

3. **Find delta specs**

   Use `artifactPaths.specs.existingOutputPaths` from the status JSON as the list of delta spec files.

   Each delta spec file contains sections like:
   - `## ADDED Requirements` - New requirements to add
   - `## MODIFIED Requirements` - Changes to existing requirements
   - `## REMOVED Requirements` - Requirements to remove
   - `## RENAMED Requirements` - Requirements to rename (FROM:/TO: format)

   If no delta specs found, inform user and stop.

3.5. **Validate the formal-Spec baseline before reading or merging targets**

   Every Change persists its targets and optimistic-concurrency baseline in `change-review.json.syncTargets`. Validate it with `sync_baseline.py validate` when Python is available; otherwise compute the same hashes and Requirement overlap directly.
   - `clean|merge-safe`: continue and preserve both sides; `merge-safe` means changed Requirements do not overlap the Delta.
   - `review-required`: read `baseSnapshotPath`, current formal Spec and Delta. Continue only when the semantic merge is safe; record `safe|conflict` and a reason in `syncValidation` (or through `resolve-review`).
   - Only confirmed replacement, incompatible edits or mixed semantics writes `specSyncStatus=blocked-by-conflict`. Target removal, overlaps and legacy baselines without snapshots require review; a changed hash alone never blocks.
   - Missing `syncTargets` remains review-blocking. Reconcile/rebase explicitly; never manufacture a baseline during sync.

4. **For each delta spec, apply changes to main specs**

   Only sync an approved active Change (`change-review.json` schema v5, `approvalStatus=implementation-change`) whose implementation tasks and required verification are complete. Draft Changes must never be synced.

   For each repo-local capability delta spec path returned by the CLI:

   a. **Read the delta spec** to understand the intended changes

   b. **Read the main spec** at `openspec/specs/<capability>/spec.md` (may not exist yet)

   c. **Apply changes intelligently**:

      **ADDED Requirements:**
      - If requirement doesn't exist in main spec → add it
      - If requirement already exists → update it to match (treat as implicit MODIFIED)

      **MODIFIED Requirements:**
      - Find the requirement in main spec
      - Apply the changes - this can be:
        - Adding new scenarios (don't need to copy existing ones)
        - Modifying existing scenarios
        - Changing the requirement description
      - Preserve scenarios/content not mentioned in the delta

      **REMOVED Requirements:**
      - Remove the entire requirement block from main spec

      **RENAMED Requirements:**
      - Find the FROM requirement, rename to TO

   d. **Create new main spec** if capability doesn't exist yet:
      - Create `openspec/specs/<capability>/spec.md`
      - Preserve the delta Spec's `schemaVersion` and `category` frontmatter
      - Add Purpose section (can be brief, mark as TBD)
      - Add Requirements section with the ADDED requirements

   e. **Sync audit metadata**:
      - Copy or intelligently merge the Change capability's `spec-review.json` to the formal Spec directory.
      - Merge Change-local nodes and edges from `dependencies.json` into `openspec/spec-metadata/dependencies.json` by ID.
      - Merge Change-local missing-dependency gaps from `gaps.json` into `openspec/spec-metadata/gaps.json` by ID.
      - Preserve unrelated formal nodes, edges, gaps, schema-v3 GUID-based `verification.codeEvidence`, and source history.
      - Validate category direction after normalizing legacy `architecture` to `system`: system -> system; feature -> system or feature; game-rule -> feature.

5. **Record sync completion**

   After every capability succeeds atomically, use `sync_baseline.py record-synced` or update the identical sync fields directly when Python is unavailable. A paired Change is atomic: never record partial sync.

6. **Show summary**

   After applying all changes, summarize:
   - Which capabilities were updated
   - What changes were made (requirements added/modified/removed/renamed)

**Delta Spec Format Reference**

```markdown
## ADDED Requirements

### Requirement: New Feature
The system SHALL do something new.

#### Scenario: Basic case
- **WHEN** user does X
- **THEN** system does Y

## MODIFIED Requirements

### Requirement: Existing Feature
#### Scenario: New scenario to add
- **WHEN** user does A
- **THEN** system does B

## REMOVED Requirements

### Requirement: Deprecated Feature

## RENAMED Requirements

- FROM: `### Requirement: Old Name`
- TO: `### Requirement: New Name`
```

**Key Principle: Intelligent Merging**

Unlike programmatic merging, you can apply **partial updates**:
- To add a scenario, just include that scenario under MODIFIED - don't copy existing scenarios
- The delta represents *intent*, not a wholesale replacement
- Use your judgment to merge changes sensibly

**Output On Success**

```
## Specs Synced: <change-name>

Updated main specs:

**<capability-1>**:
- Added requirement: "New Feature"
- Modified requirement: "Existing Feature" (added 1 scenario)

**<capability-2>**:
- Created new spec file
- Added requirement: "Another Feature"

Main specs are now updated. The change remains active - archive when implementation is complete.
```

**Guardrails**
- Read both delta and main specs before making changes
- Preserve existing content not mentioned in delta
- If something is unclear, ask for clarification
- Show what you're changing as you go
- The operation should be idempotent - running twice should give same result
- A synced formal Spec is incomplete if its category, `spec-review.json`, verification, local dependency subtree or missing-dependency gaps were dropped
- Draft approval and apply never call this workflow. Only an explicit user sync request may merge delta Specs into formal Specs.
- A changed hash is evidence, not a conflict. Block only after base/current/Delta comparison confirms functional replacement, incompatible edits or semantic mixing.
- Never resolve a confirmed conflict by overwriting the formal Spec. Reconcile/rebase the Change first, then capture a new baseline with explicit user approval.
