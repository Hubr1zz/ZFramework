---
name: openspec-propose
description: Propose a new change with all artifacts generated in one step. Use when the user wants to quickly describe what they want to build and get a complete proposal with design, specs, and tasks ready for implementation.
allowed-tools: Bash(openspec:*)
license: MIT
metadata:
  author: openspec
  version: "1.0"
  generatedBy: "1.6.0"
---

Propose a new change - create the change and generate all artifacts in one step.

I'll create a change with artifacts:
- proposal.md (what & why)
- design.md (how)
- tasks.md (implementation steps)
- change-review.json (category, feasibility, gaps, dependencies, sources)
- dependencies.json / gaps.json / sources.json (local auditable metadata)

The generated proposal starts with `approvalStatus=draft`. Review and approve it before `/opsx:apply`.

---

**Store selection:** For a named or detected registered store, resolve its id with `openspec store list --json` and preserve the CLI-provided `--store <id>` on supported follow-ups. Otherwise use the nearest `openspec/` root.

**Input**: The user's request should include a change name (kebab-case) OR a description of what they want to build.

**Steps**

1. **If no clear input provided, ask what they want to build**

   Ask through the available user-input mechanism:
   > "What change do you want to work on? Describe what you want to build or fix."

   From their description, derive a kebab-case name (e.g., "add user authentication" → `add-user-auth`).

   **IMPORTANT**: Do NOT proceed without understanding what the user wants to build.

2. **Create or continue the change directory**
   ```bash
   openspec new change "<name>"
   ```
   Run this only when the Change does not exist. If it exists and the user chose to continue it, keep the directory and proceed from its current status. This folds the separate OPSX continue step into propose.

3. **Get the artifact build order**
   ```bash
   openspec status --change "<name>" --json
   ```
   Parse the JSON to get:
   - `applyRequires`: array of artifact IDs needed before implementation (e.g., `["tasks"]`)
   - `artifacts`: list of all artifacts with their status and dependencies
   - `planningHome`, `changeRoot`, `artifactPaths`, and `actionContext`: path and scope context. Use these instead of assuming repo-local paths.

4. **Create artifacts in sequence until apply-ready**

   Before writing the first artifact, read `openspec/localization.json`. Use `generationLanguage` as the authoritative output language: `zh-CN` or `en-US` is explicit; `source` follows the dominant language of the current design/source material and falls back to the user's request language when no design source applies. Write only canonical artifacts here; never generate into `openspec/translations/` during proposal creation.

   Track artifact progress with the current tool's plan/task mechanism when available.

   Loop through artifacts in dependency order (artifacts with no pending dependencies first):

   a. **For each artifact that is `ready` (dependencies satisfied)**:
      - Get instructions:
        ```bash
        openspec instructions <artifact-id> --change "<name>" --json
        ```
      - The instructions JSON includes:
        - `context`: Project background (constraints for you - do NOT include in output)
        - `rules`: Artifact-specific rules (constraints for you - do NOT include in output)
        - `template`: The structure to use for your output file
        - `instruction`: Schema-specific guidance for this artifact type
        - `resolvedOutputPath`: Resolved path or pattern to write the artifact
        - `dependencies`: Completed artifacts to read for context
      - Read any completed dependency files for context
      - Create the artifact file using `template` as the structure and write it to `resolvedOutputPath`
      - Apply `context` and `rules` as constraints - but do NOT copy them into the file
      - Show brief progress: "Created <artifact-id>"

   b. **Complete the unified Change audit files**
      - Follow `../openspec-derive-design-specs/references/change-schema.md` and `metadata-schema.md`.
      - Classify every delta Spec as `system`, `feature`, or `game-rule`; write the category to Spec frontmatter and `spec-review.json`. Treat legacy `architecture` metadata as `system` when reading existing artifacts.
      - Analyze current code feasibility and persist it in schema-v5 `change-review.json.verification`. Set `approvalStatus=draft`. Code evidence contains Unity GUID, display path, script SHA-256, optional entry line and major feature; reuse only when GUID/hash remain unchanged, and do not split evidence by small function.
      - Persist design conflicts, dependency misses, and implementation deltas as `reviewIssues`. Blocking issues must be resolved before approval; accepted warning/info issues require acceptance audit fields.
      - Persist the Change-local dependency subtree in `dependencies.json`.
      - A Gap is only a missing dependency node/contract; every gap must reference one open dependency edge. Ordinary code differences stay in verification differences.
      - Enforce dependency direction: system -> system; feature -> system or feature; game-rule -> feature. Legacy `architecture` is normalized to `system` before validation.
      - Generate checkbox Tasks only for implementation deltas. These files are required even when arrays are empty; do not report the Change complete without them.
      - Treat EventBus, state-machine bases, registries, and similar helpers as implementation details by default. Create a contract-named System capability and graph node only when this Change alters stable cross-module semantics (such as dispatch model or subscription lifecycle), or at least two capabilities depend on those semantics; add only real dependency edges. Otherwise keep the tool in implementation outlines/code evidence.
      - For Feature/System design, keep gameplay rules/state in cohesive pure C# objects, use `MonoBehaviour` only for Unity lifecycle, scene references, presentation adapters, or composition roots, and centralize related tunables in a small configuration asset/object. Do not scatter one component per small feature across scene objects.
      - If a capability requires manual Inspector references, tunable configuration, scene/Prefab setup, or a first-use action, write the concise actions to its optional `spec-review.json.editorGuidance`; omit it when no manual Unity work exists, and never add it to game-rule.
      - In a paired gameplay Change, the Feature owns the detailed proposal, implementation design, tasks, code evidence, gaps, external dependencies and review issues. The Game Rule stores only observable Requirement/Scenario, sources and `pairedFeatureCapability`; it follows the Feature's single Change-level approval and sync lifecycle.
      - Record every delta capability's formal target, SHA-256 baseline and existing-target snapshot in `change-review.json.syncTargets`. Prefer `openspec-sync-specs/scripts/sync_baseline.py capture`; without Python, create the identical fields, hashes and snapshots directly. This sync-only baseline belongs to the Change and is never refreshed implicitly during apply or sync.

   c. **Continue until all `applyRequires` artifacts are complete**
      - After creating each artifact, re-run `openspec status --change "<name>" --json`
      - Check if every artifact ID in `applyRequires` has `status: "done"` in the artifacts array
      - Stop when all `applyRequires` artifacts are done

   d. **If an artifact requires user input** (unclear context):
      - Ask through the available user-input mechanism
      - Then continue with creation

5. **Show final status**
   ```bash
   openspec status --change "<name>"
   ```

**Output**

After completing all artifacts, summarize:
- Change name and location
- List of artifacts created with brief descriptions
- What's ready: "All artifacts created! Ready for review."
- Prompt: ask the user to approve the Draft after all approval-blocking issues are resolved; only then set `approvalStatus=implementation-change` and offer `/opsx:apply`.

**Artifact Creation Guidelines**

- Follow the `instruction` field from `openspec instructions` for each artifact type
- The schema defines what each artifact should contain - follow it
- Read dependency artifacts for context before creating new ones
- Use `template` as the structure for your output file - fill in its sections
- **IMPORTANT**: `context` and `rules` are constraints for YOU, not content for the file
  - Do NOT copy `<context>`, `<rules>`, `<project_context>` blocks into the artifact
  - These guide what you write, but should never appear in the output

**Guardrails**
- Create ALL artifacts needed for implementation (as defined by schema's `apply.requires`)
- Always read dependency artifacts before creating a new one
- If context is critically unclear, ask the user - but prefer making reasonable decisions to keep momentum
- If a change with that name already exists, ask if user wants to continue it or create a new one
- Verify each artifact file exists after writing before proceeding to next
- Verify `change-review.json`, `dependencies.json`, `gaps.json`, `sources.json`, and per-capability `spec-review.json` exist and are coherent before reporting apply-ready
- Never describe an unapproved Draft as implementation-ready. Every category enters the approved Change path; formal Specs are written only by an explicit user-triggered sync after apply.
