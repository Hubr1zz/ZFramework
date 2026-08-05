import assert from "node:assert/strict";
import { existsSync, mkdirSync, mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";
import {
  compareVersion,
  copyPortablePackage,
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
