import assert from "node:assert/strict";
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";
import {
  compareVersion,
  comparePackageVersions,
  copyPortablePackage,
  installOrUpgradePortablePackage,
  isCompatibleOpenSpec,
  parseVersion,
} from "../bin/zworkflow.mjs";

test("parses and checks supported semantic versions", () => {
  assert.deepEqual(parseVersion("OpenSpec 1.6.0"), [1, 6, 0]);
  assert.equal(isCompatibleOpenSpec([1, 6, 0]), true);
  assert.equal(isCompatibleOpenSpec([1, 9, 2]), true);
  assert.equal(isCompatibleOpenSpec([2, 0, 0]), false);
  assert.ok(compareVersion([1, 5, 9], [1, 6, 0]) < 0);
});

test("compares package versions", () => {
  assert.ok(comparePackageVersions("2026.08.04", "2026.07.29") > 0);
  assert.ok(comparePackageVersions("2026.08.04.1", "2026.08.04") > 0);
  assert.equal(comparePackageVersions("2026.08.04.0", "2026.08.04"), 0);
  assert.equal(comparePackageVersions("2026.08.04", "2026.08.04"), 0);
  assert.throws(() => comparePackageVersions("latest", "2026.08.04"), /无法比较/);
});

test("copies a package without machine or repository state", () => {
  const root = mkdtempSync(join(tmpdir(), "zworkflow-cli-"));
  try {
    const source = join(root, "source");
    const target = join(root, "target");
    mkdirSync(join(source, ".git"), { recursive: true });
    mkdirSync(join(source, ".github"), { recursive: true });
    mkdirSync(join(source, "node_modules"), { recursive: true });
    mkdirSync(join(source, "zWorkFlow Pack"), { recursive: true });
    mkdirSync(join(source, "setup"), { recursive: true });
    writeFileSync(join(source, "setup", "SETUP_NEW_PROJECT.md"), "setup");
    writeFileSync(join(source, ".git", "config"), "private");
    writeFileSync(join(source, ".github", "README.md"), "site");

    copyPortablePackage(source, target);
    assert.equal(existsSync(join(target, "setup", "SETUP_NEW_PROJECT.md")), true);
    assert.equal(existsSync(join(target, ".git")), false);
    assert.equal(existsSync(join(target, ".github")), false);
    assert.equal(existsSync(join(target, "node_modules")), false);
    assert.equal(existsSync(join(target, "zWorkFlow Pack")), false);
    assert.throws(() => copyPortablePackage(source, target), /目标已存在/);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});

test("upgrades managed files while preserving project data", () => {
  const root = mkdtempSync(join(tmpdir(), "zworkflow-upgrade-"));
  try {
    const source = join(root, "source");
    const target = join(root, "zWorkFlow");
    mkdirSync(join(source, "setup"), { recursive: true });
    mkdirSync(join(source, "packages", "new-bridge"), { recursive: true });
    mkdirSync(join(source, ".agent-memory", "team"), { recursive: true });
    writeFileSync(join(source, "setup", "PACKAGE_MANIFEST.json"), JSON.stringify({ packageVersion: "2026.08.04" }));
    writeFileSync(join(source, "packages", "new-bridge", "SKILL.md"), "new");
    writeFileSync(join(source, ".agent-memory", "team", "member.md"), "template");

    mkdirSync(join(target, "setup"), { recursive: true });
    mkdirSync(join(target, "packages", "old-bridge"), { recursive: true });
    mkdirSync(join(target, ".agent-memory", "team"), { recursive: true });
    mkdirSync(join(target, "openspec"), { recursive: true });
    mkdirSync(join(target, "custom-data"), { recursive: true });
    writeFileSync(join(target, "setup", "PACKAGE_MANIFEST.json"), JSON.stringify({ packageVersion: "2026.07.29" }));
    writeFileSync(join(target, "packages", "old-bridge", "SKILL.md"), "old");
    writeFileSync(join(target, ".agent-memory", "team", "member.md"), "personal");
    writeFileSync(join(target, "openspec", "spec.md"), "project spec");
    writeFileSync(join(target, "custom-data", "state.json"), "state");

    const result = installOrUpgradePortablePackage(source, target);
    assert.equal(result.action, "upgraded");
    assert.deepEqual(result.cleanupPending, []);
    assert.equal(existsSync(join(target, "packages", "new-bridge", "SKILL.md")), true);
    assert.equal(existsSync(join(target, "packages", "old-bridge", "SKILL.md")), false);
    assert.equal(readFileSync(join(target, ".agent-memory", "team", "member.md"), "utf8"), "personal");
    assert.equal(readFileSync(join(target, "openspec", "spec.md"), "utf8"), "project spec");
    assert.equal(readFileSync(join(target, "custom-data", "state.json"), "utf8"), "state");
    assert.equal(installOrUpgradePortablePackage(source, target).action, "up-to-date");
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});
+test("upgrades from a newer pack nested inside the target", () => {
  const root = mkdtempSync(join(tmpdir(), "zworkflow-nested-upgrade-"));
  try {
    const target = join(root, "zWorkFlow");
    const source = join(target, "zWorkFlow Pack");
    mkdirSync(join(target, "setup"), { recursive: true });
    mkdirSync(join(target, "openspec"), { recursive: true });
    mkdirSync(join(source, "setup"), { recursive: true });
    mkdirSync(join(source, "packages", "bridge"), { recursive: true });
    writeFileSync(join(target, "setup", "PACKAGE_MANIFEST.json"), JSON.stringify({ packageVersion: "2026.07.29" }));
    writeFileSync(join(target, "openspec", "ledger.json"), "keep");
    writeFileSync(join(source, "setup", "PACKAGE_MANIFEST.json"), JSON.stringify({ packageVersion: "2026.08.04" }));
    writeFileSync(join(source, "packages", "bridge", "SKILL.md"), "latest");

    const result = installOrUpgradePortablePackage(source, target);
    assert.equal(result.action, "upgraded");
    assert.deepEqual(result.cleanupPending, []);
    assert.equal(readFileSync(join(target, "openspec", "ledger.json"), "utf8"), "keep");
    assert.equal(readFileSync(join(target, "packages", "bridge", "SKILL.md"), "utf8"), "latest");
    assert.equal(existsSync(join(target, "zWorkFlow Pack", "setup", "PACKAGE_MANIFEST.json")), true);
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
});
