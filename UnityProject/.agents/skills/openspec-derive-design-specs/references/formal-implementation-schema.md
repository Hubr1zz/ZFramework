# Formal implementation assertion

`openspec/specs/<capability>/spec.md` is the accepted behavioral authority. Its adjacent `implementation.json` records only the last verified implementation assertion for that authority; it is not a Review and never contains open issues, differences, approval state, gaps, or draft readiness.

Minimum schema:

```json
{
  "schemaVersion": 1,
  "artifactRole": "formal-implementation-assertion",
  "capability": "stable-capability-id",
  "specHash": "normalized-sha256",
  "codeReadiness": "implemented",
  "verification": {
    "status": "verified",
    "summary": "behavior-oriented result",
    "validatedAgainstSpecHash": "normalized-sha256",
    "evidence": [],
    "tests": [],
    "verifiedAt": "ISO-8601"
  },
  "implementationOutline": [],
  "publishedBy": {
    "mode": "sync",
    "changeId": "change-id"
  }
}
```

The effective state is derived. If the current normalized Spec hash differs from `specHash` or evidence no longer matches current code, consumers must show `stale` and must not rewrite the assertion automatically.

An accepted Change is transformed into this schema during explicit sync. The Change and its archived copy retain the complete review history. A direct implementation may update this file only when it conforms to an unchanged formal Spec and has been verified; otherwise it must create a post-hoc adoption Change.
