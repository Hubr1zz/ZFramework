import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync, existsSync } from "node:fs";
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

function setupRepository() {
  const repositoryRoot = mkdtempSync(join(tmpdir(), "zworkflow-implementation-"));
  const root = join(repositoryRoot, "UnityProject");
  const source = "public sealed class DemoFeature { }\n";
  const spec = "---\ntitle: Demo Feature\n---\n\n# Demo\n";
  write(join(root, "Assets/Scripts/DemoFeature.cs"), source);
  write(join(root, "Design/DemoFeature.md"), "# Demo Feature\n");
  write(join(root, "openspec/specs/demo-feature/spec.md"), spec);
  write(join(root, "openspec/spec-metadata/dependencies.json"), JSON.stringify({ schemaVersion: 2, nodes: [], edges: [] }, null, 2));
  write(join(root, "openspec/specs/demo-feature/implementation.json"), JSON.stringify({
    schemaVersion: 1,
    artifactRole: "formal-implementation-assertion",
    capability: "demo-feature",
    specHash: hash(spec),
    title: "Demo Feature",
    codeReadiness: "implemented",
    progress: 100,
    summary: "verified demo",
    sourceReferences: [{ sourceId: "design-doc", path: "Design/DemoFeature.md" }, { sourceId: "code", path: "Assets/Scripts/DemoFeature.cs:1" }, { sourceId: "external", path: "C:/outside/design.md" }],
    verification: {
      status: "verified",
      validatedAgainstSpecHash: hash(spec),
      evidence: [{ displayPath: "Assets/Scripts/DemoFeature.cs", fileHash: hash(source), feature: "demo" }],
    },
  }, null, 2));
  write(join(root, ".agents/codebase-query/code-query-index.json"), JSON.stringify({
    schemaVersion: 6,
    files: [{ path: "Assets/Scripts/DemoFeature.cs", sourceHash: hash(source), types: [{ qualifiedName: "DemoFeature" }], methodDefinitions: [] }],
  }));
  write(join(root, "openspec/implementation-audit.json"), JSON.stringify({ schemaVersion: 1, discoveryRevision: "", discoveryExclusions: [] }, null, 2));
  run("git", ["init", "-q"], repositoryRoot);
  run("git", ["config", "user.email", "test@example.com"], repositoryRoot);
  run("git", ["config", "user.name", "test"], repositoryRoot);
  run("git", ["add", "."], repositoryRoot);
  run("git", ["commit", "-qm", "baseline"], repositoryRoot);
  return { repositoryRoot, root, source };
}

test("summary reads implementation facts, exposes verifiable digests, and ignores the legacy ledger", () => {
  const fixture = setupRepository();
  try {
    write(join(fixture.root, "openspec/implementation-ledger.json"), "not-json");
    const refresh = run("pwsh", ["-NoProfile", "-File", script, "refresh", "-ProjectRoot", fixture.root], fixture.root);
    const summary = JSON.parse(refresh.stdout);
    assert.equal(summary.schemaVersion, 2);
    assert.equal(summary.role, "derived-routing-index");
    assert.match(summary.inputDigest, /^[0-9a-f]{64}$/);
    assert.match(summary.inputManifestDigest, /^[0-9a-f]{64}$/);
    assert.ok(summary.inputManifest.some((item) => item.path.endsWith("implementation.json")));
    assert.ok(summary.inputManifest.some((item) => item.path === "Design/DemoFeature.md"));
    assert.ok(!summary.inputManifest.some((item) => item.path.includes("DemoFeature.cs:1")));
    const requirement = summary.requirements.find((item) => item.id === "demo-feature");
    assert.equal(requirement.effectiveStatus, "verified");
    assert.deepEqual(requirement.designSources, [{ sourceId: "design-doc", path: "Design/DemoFeature.md" }]);
    assert.equal(run("pwsh", ["-NoProfile", "-File", script, "validate", "-ProjectRoot", fixture.root], fixture.root).status, 0);
  } finally {
    rmSync(fixture.repositoryRoot, { recursive: true, force: true });
  }
});

test("validate fails closed when implementation evidence or implementation fact changes", () => {
  const fixture = setupRepository();
  try {
    run("pwsh", ["-NoProfile", "-File", script, "refresh", "-ProjectRoot", fixture.root], fixture.root);
    const summaryPath = join(fixture.root, "openspec/implementation-summary.json");
    const tampered = JSON.parse(readFileSync(summaryPath, "utf8"));
    tampered.requirements[0].effectiveStatus = "implemented";
    write(summaryPath, JSON.stringify(tampered, null, 2));
    const tamperedSummary = run("pwsh", ["-NoProfile", "-File", script, "validate", "-ProjectRoot", fixture.root], fixture.root, true);
    assert.notEqual(tamperedSummary.status, 0);
    assert.match(tamperedSummary.stderr, /派生内容已被修改/);
    run("pwsh", ["-NoProfile", "-File", script, "refresh", "-ProjectRoot", fixture.root], fixture.root);
    write(join(fixture.root, "Assets/Scripts/DemoFeature.cs"), "public sealed class DemoFeature { public int Value => 1; }\n");
    const staleEvidence = run("pwsh", ["-NoProfile", "-File", script, "validate", "-ProjectRoot", fixture.root], fixture.root, true);
    assert.notEqual(staleEvidence.status, 0);
    assert.match(staleEvidence.stderr, /已过期/);
    run("pwsh", ["-NoProfile", "-File", script, "refresh", "-ProjectRoot", fixture.root], fixture.root);
    const implementationPath = join(fixture.root, "openspec/specs/demo-feature/implementation.json");
    const implementation = JSON.parse(readFileSync(implementationPath, "utf8"));
    implementation.summary = "changed fact";
    write(implementationPath, JSON.stringify(implementation, null, 2));
    const staleFact = run("pwsh", ["-NoProfile", "-File", script, "validate", "-ProjectRoot", fixture.root], fixture.root, true);
    assert.notEqual(staleFact.status, 0);
    assert.match(staleFact.stderr, /已过期/);
  } finally {
    rmSync(fixture.repositoryRoot, { recursive: true, force: true });
  }
});

test("refresh keeps an implementation assertion stale after its formal spec changes", () => {
  const fixture = setupRepository();
  try {
    write(join(fixture.root, "openspec/specs/demo-feature/spec.md"), "---\ntitle: Demo Feature\n---\n\n# Changed behavior\n");
    const summary = JSON.parse(run("pwsh", ["-NoProfile", "-File", script, "refresh", "-ProjectRoot", fixture.root], fixture.root).stdout);
    const requirement = summary.requirements.find((item) => item.id === "demo-feature");
    assert.equal(requirement.effectiveStatus, "stale");
    assert.ok(summary.staleEvidence.some((item) => item.capability === "demo-feature" && item.state === "spec-binding-invalid"));
  } finally {
    rmSync(fixture.repositoryRoot, { recursive: true, force: true });
  }
});

test("discover writes only local candidates and query returns attention, capability, and path slices", () => {
  const fixture = setupRepository();
  try {
    write(join(fixture.root, "openspec/specs/partial-feature/spec.md"), "---\ntitle: Partial Feature\n---\n");
    write(join(fixture.root, "openspec/specs/partial-feature/implementation.json"), JSON.stringify({ schemaVersion: 1, capability: "partial-feature", implementationStatus: "partial", progress: 40 }, null, 2));
    run("pwsh", ["-NoProfile", "-File", script, "refresh", "-ProjectRoot", fixture.root], fixture.root);
    const summaryPath = join(fixture.root, "openspec/implementation-summary.json");
    const summaryBeforeDiscover = readFileSync(summaryPath, "utf8");
    const discovery = JSON.parse(run("pwsh", ["-NoProfile", "-File", script, "discover", "-ProjectRoot", fixture.root], fixture.root).stdout);
    assert.ok(existsSync(join(fixture.root, ".agent-memory/zworkflow/local/implementation-discovery.json")));
    assert.equal(readFileSync(summaryPath, "utf8"), summaryBeforeDiscover);
    assert.equal(discovery.role, "local-audit");
    const attention = JSON.parse(run("pwsh", ["-NoProfile", "-File", script, "query", "-Attention", "-ProjectRoot", fixture.root], fixture.root).stdout);
    assert.deepEqual(attention.requirements.map((item) => item.id), ["partial-feature"]);
    const capability = JSON.parse(run("pwsh", ["-NoProfile", "-File", script, "query", "-Slice", "capability", "-Capability", "demo-feature", "-ProjectRoot", fixture.root], fixture.root).stdout);
    assert.deepEqual(capability.requirements.map((item) => item.id), ["demo-feature"]);
    const path = JSON.parse(run("pwsh", ["-NoProfile", "-File", script, "query", "-Slice", "path", "-Path", "Assets/Scripts", "-ProjectRoot", fixture.root], fixture.root).stdout);
    assert.deepEqual(path.requirements.map((item) => item.id), ["demo-feature"]);
  } finally {
    rmSync(fixture.repositoryRoot, { recursive: true, force: true });
  }
});

test("checkpoint updates implementation-audit and never creates or writes the legacy ledger", () => {
  const fixture = setupRepository();
  try {
    run("pwsh", ["-NoProfile", "-File", script, "refresh", "-ProjectRoot", fixture.root], fixture.root);
    run("pwsh", ["-NoProfile", "-File", script, "checkpoint", "-ProjectRoot", fixture.root], fixture.root);
    const audit = JSON.parse(readFileSync(join(fixture.root, "openspec/implementation-audit.json"), "utf8"));
    assert.equal(audit.schemaVersion, 1);
    assert.match(audit.discoveryRevision, /^[0-9a-f]{40}$/);
    assert.equal(existsSync(join(fixture.root, "openspec/implementation-ledger.json")), false);
  } finally {
    rmSync(fixture.repositoryRoot, { recursive: true, force: true });
  }
});
