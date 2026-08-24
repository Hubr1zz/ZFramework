import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { spawnSync } from "node:child_process";
import test from "node:test";
import { fileURLToPath } from "node:url";

const script = fileURLToPath(new URL("../.agents/skills/track-implementation-progress/scripts/run.ps1", import.meta.url));

function write(path, content) {
  mkdirSync(dirname(path), { recursive: true });
  writeFileSync(path, content, "utf8");
}

function run(command, args, cwd, allowFailure = false) {
  const result = spawnSync(command, args, { cwd, encoding: "utf8" });
  if (!allowFailure && result.status !== 0) {
    throw new Error(`${command} ${args.join(" ")} failed:\n${result.stdout}\n${result.stderr}`);
  }
  return result;
}

function hash(content) {
  return createHash("sha256").update(content.replaceAll("\r\n", "\n").replaceAll("\r", "\n"), "utf8").digest("hex");
}

test("implementation summary verifies evidence and discovery refuses unmapped code", () => {
  const repositoryRoot = mkdtempSync(join(tmpdir(), "zworkflow-implementation-"));
  const root = join(repositoryRoot, "UnityProject");
  try {
    const source = "public sealed class DemoFeature { }\n";
    write(join(root, "Assets/Scripts/DemoFeature.cs"), source);
    write(join(root, "openspec/specs/demo-feature/spec.md"), "---\ntitle: Demo Feature\n---\n\n# Demo\n");
    write(join(root, "openspec/specs/tree-feature/spec.md"), "---\ntitle: Tree Feature\n---\n\n# Tree\n");
    write(join(root, "openspec/specs/unknown-feature/spec.md"), "---\ntitle: Unknown Feature\n---\n\n# Unknown\n");
    write(join(root, "openspec/spec-metadata/dependencies.json"), JSON.stringify({
      schemaVersion: 2,
      nodes: [{ id: "tree-feature", label: "Tree Feature", readiness: "implemented", specPath: "openspec/specs/tree-feature/spec.md" }],
      edges: [],
    }, null, 2));
    write(join(root, "openspec/specs/demo-feature/spec-review.json"), JSON.stringify({
      schemaVersion: 5,
      capability: "demo-feature",
      readiness: "implemented",
      codeReadiness: "implemented",
      verification: {
        status: "verified",
        summary: "verified demo",
        codeEvidence: [{ displayPath: "Assets/Scripts/DemoFeature.cs", fileHash: hash(source), feature: "demo" }],
      },
    }, null, 2));
    write(join(root, ".agents/codebase-query/code-query-index.json"), JSON.stringify({
      schemaVersion: 6,
      files: [{ path: "Assets/Scripts/DemoFeature.cs", sourceHash: hash(source), types: [{ qualifiedName: "DemoFeature" }], methodDefinitions: [] }],
    }));

    run("git", ["init", "-q"], repositoryRoot);
    run("git", ["config", "user.email", "test@example.com"], repositoryRoot);
    run("git", ["config", "user.name", "test"], repositoryRoot);
    run("git", ["add", "."], repositoryRoot);
    run("git", ["commit", "-qm", "baseline"], repositoryRoot);
    const revision = run("git", ["rev-parse", "HEAD"], repositoryRoot).stdout.trim();
    write(join(root, "openspec/implementation-ledger.json"), JSON.stringify({ schemaVersion: 3, updatedAt: "", discoveryRevision: revision, entries: [] }, null, 2));

    const refresh = run("pwsh", ["-NoProfile", "-File", script, "refresh", "-ProjectRoot", root], root);
    const summary = JSON.parse(refresh.stdout);
    const requirement = summary.requirements.find((item) => item.id === "demo-feature");
    assert.equal(requirement.effectiveStatus, "verified");
    assert.equal(summary.requirements.find((item) => item.id === "tree-feature").effectiveStatus, "implemented");
    assert.equal(summary.requirements.find((item) => item.id === "unknown-feature").effectiveStatus, "unknown");
    assert.deepEqual(summary.attentionRequired, ["unknown-feature"]);
    assert.deepEqual(summary.verificationRequired, ["tree-feature"]);

    write(join(root, "Assets/Scripts/DemoFeature.cs"), "public sealed class DemoFeature { public int Value => 1; }\n");
    write(join(root, "Assets/Scripts/ManualFeature.cs"), "public sealed class ManualFeature { }\n");
    const discoveryResult = run("pwsh", ["-NoProfile", "-File", script, "discover", "-ProjectRoot", root], root);
    const discovery = JSON.parse(discoveryResult.stdout);
    assert.equal(discovery.baselineMissing, false);
    assert.ok(discovery.staleEvidence.some((item) => item.capability === "demo-feature"));
    assert.ok(discovery.unmappedCSharpChanges.some((item) => item.path === "Assets/Scripts/ManualFeature.cs"));
    assert.equal(discovery.changedCSharpFiles.find((item) => item.path === "Assets/Scripts/DemoFeature.cs").mappedByExistingEvidence, true);

    write(join(root, "openspec/implementation-ledger.json"), JSON.stringify({
      schemaVersion: 3,
      updatedAt: "",
      discoveryRevision: "",
      discoveryExclusions: [{ path: "Assets/Scripts/DemoFeature.cs", reason: "fixture exclusion" }],
      entries: [],
    }, null, 2));
    const bootstrapResult = run("pwsh", ["-NoProfile", "-File", script, "discover", "-ProjectRoot", root], root);
    const bootstrap = JSON.parse(bootstrapResult.stdout);
    assert.equal(bootstrap.baselineMissing, true);
    assert.ok(bootstrap.excludedCSharpChanges.some((item) => item.path === "Assets/Scripts/DemoFeature.cs"));
    assert.ok(bootstrap.unmappedCSharpChanges.some((item) => item.path === "Assets/Scripts/ManualFeature.cs"));

    const checkpoint = run("pwsh", ["-NoProfile", "-File", script, "checkpoint", "-ProjectRoot", root], root, true);
    assert.notEqual(checkpoint.status, 0);
    assert.match(checkpoint.stderr, /未映射 C# 变化或过期证据/);
    assert.equal(JSON.parse(readFileSync(join(root, "openspec/implementation-summary.json"), "utf8")).role, "derived-index");
  } finally {
    rmSync(repositoryRoot, { recursive: true, force: true });
  }
});
