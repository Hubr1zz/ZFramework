---
name: reconcile-direct-implementation
description: 当用户明确允许绕过 zWorkFlow 直接实现，或实现完成后需要把代码进度纳入 zWorkFlow 时使用。核验实现是否符合已有正式 Spec；无正式 Spec 或行为超出正式 Spec 时生成 post-hoc adoption Change 供人类审查，绝不自动把偶然代码写成正式权威。
---

# Reconcile Direct Implementation

This skill makes direct coding compatible with zWorkFlow without forcing proposal-first execution. It runs after a functional cluster has compiled and received risk-proportionate verification.

## Inputs

- The user's original document or requirement reference.
- The verified code diff and targeted validation result.
- A fresh C# derived index for locating symbols and callers.
- 'openspec/specs/', active Changes, and the local discovery candidates produced by 'track-implementation-progress'.

Do not read the global project graph or every Spec. Query the valid implementation routing summary when available, then read only candidate Specs and their 'implementation.json'.

## Decision

1. Map the implemented behavior to stable capability IDs.
2. If an existing formal Spec fully describes the behavior and its normalized hash did not change:
   - compare implementation behavior with every relevant Requirement/Scenario;
   - validate current evidence and targeted tests;
   - update only that capability's 'implementation.json';
   - refresh the routing summary.
3. If no formal Spec exists, or code adds/changes behavior outside the formal Spec:
   - create 'openspec/changes/<id>/' with 'sourceKind=direct-implementation-adoption';
   - write proposal/design/tasks, Delta Specs, 'change-review.json', per-capability 'spec-review.json', dependencies, gaps, and sources from the actual verified implementation;
   - set 'approvalStatus=draft', 'specSyncStatus=pending', and record that implementation already exists;
   - list all unresolved differences between the requirement, formal authority, and code as blocking review issues.
4. Never mark an adoption Change approved or sync it automatically. The human reviews the Change, requests code or delta adjustments, then explicitly approves and syncs it.

## Constraints

- A code file, symbol match, Git commit, or passing compile is evidence, not proof of conformance.
- Do not reverse-justify accidental behavior. The user's requirement and accepted formal Spec outrank existing code.
- Reuse the normal OpenSpec Change schemas and sync path; do not invent a second direct-code ledger.
- The local discovery file stays Git-ignored and contains candidates only.
- Formal Specs never receive Review issues, differences, gaps, or approval state. Only verified, unchanged conformance may update 'implementation.json' directly.
- Keep output compact: capability mapping, conformance/adoption result, verification, and next human action.
