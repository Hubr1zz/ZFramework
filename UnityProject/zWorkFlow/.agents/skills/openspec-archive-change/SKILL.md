---
name: openspec-archive-change
description: Archive a completed change in the experimental workflow. Use when the user wants to finalize and archive a change after implementation is complete.
allowed-tools: Bash(openspec:*)
license: MIT
metadata:
  author: openspec
  version: "1.0"
  generatedBy: "1.6.0"
---

Archive a completed change in the experimental workflow.

**Store selection:** For a named or detected registered store, resolve its id with `openspec store list --json` and preserve the CLI-provided `--store <id>` on supported follow-ups. Otherwise use the nearest `openspec/` root.

**Input:** Use a named or context-unambiguous Change; otherwise list the available Changes and ask the user.

**Steps**

1. **If no change name provided, prompt for selection**

   Run `openspec list --json` and ask the user to select through the available input mechanism.

   Show only active changes (not already archived).
   Include the schema used for each change if available.

2. **Check artifact completion status**

   Run `openspec status --change "<name>" --json` to check artifact completion.

   Parse the JSON to understand:
   - `schemaName`: The workflow being used
   - `planningHome`, `changeRoot`, `artifactPaths`, and `actionContext`: path and scope context
   - `artifacts`: List of artifacts with their status (`done` or other)

   If any artifact is not `done`, list it and stop. Archive never bypasses incomplete planning artifacts.

3. **Check task completion status**

   Read the tasks file (typically `tasks.md`) to check for incomplete tasks.

   Count tasks marked with `- [ ]` (incomplete) vs `- [x]` (complete).

   If tasks are incomplete, show the count and stop.

   **If no tasks file exists:** Proceed without task-related warning.

4. **Assess delta spec sync state**

   Use `artifactPaths.specs.existingOutputPaths` from status JSON to check for delta specs. If none exist, proceed without sync prompt.

   **If delta specs exist:**
   - Compare each delta spec with its corresponding main spec at `openspec/specs/<capability>/spec.md`
   - Determine what changes would be applied (adds, modifications, removals, renames)
   - Show a combined summary before prompting

   Require `change-review.json.specSyncStatus=synced` and verify the main Specs contain all deltas. If changes remain, stop and ask the user to invoke sync explicitly. Archive MUST NOT invoke sync or edit formal Specs on the user's behalf.

5. **Perform the archive**

   The Workbench may perform this step directly with its Archive button when every Task is complete and `specSyncStatus=synced`; the button only executes the checked directory move below and never runs sync or implementation.

   Create `archive` under `planningHome.changesDir` when missing, using the current platform's safe filesystem operation.

   Generate target name using current date: `YYYY-MM-DD-<change-name>`

   **Check if target already exists:**
   - If yes: Fail with error, suggest renaming existing archive or using different date
   - If no: move the exact `changeRoot` to that target using the current platform's safe filesystem operation

6. **Display summary**

   Show archive completion summary including:
   - Change name
   - Schema that was used
   - Archive location
   - Whether specs were synced (if applicable)

**Output On Success**

```
## Archive Complete

**Change:** <change-name>
**Schema:** <schema-name>
**Archived to:** the archive path derived from `planningHome.changesDir`/YYYY-MM-DD-<name>/
**Specs:** ✓ Synced to main specs (or "No delta specs")

All artifacts complete. All tasks complete.
```

**Guardrails**
- Always prompt for change selection if not provided
- Use artifact graph (openspec status --json) for completion checking
- Incomplete planning artifacts, tasks, or unsynced delta Specs block archive
- Preserve .openspec.yaml when moving to archive (it moves with the directory)
- Show clear summary of what happened
- Archive never triggers sync; the user must request sync as a separate action
- Workbench Archive stays disabled until all Tasks are complete and the Change is synced; it is a recoverable organization move, while Delete is permanent removal
- If delta specs exist, always run the sync assessment; sync them before archive
