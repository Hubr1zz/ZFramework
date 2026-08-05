#!/usr/bin/env node

import { spawnSync } from "node:child_process";
import { cpSync, existsSync, mkdirSync, readFileSync, readdirSync, renameSync, rmSync } from "node:fs";
import { basename, dirname, resolve, sep } from "node:path";
import { fileURLToPath } from "node:url";

const PACKAGE_ROOT = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const manifest = JSON.parse(readFileSync(resolve(PACKAGE_ROOT, "setup/PACKAGE_MANIFEST.json"), "utf8"));
const requirement = manifest.runtimeRequirements.openSpecCli;
const MIN_NODE = requirement.nodeMinimumVersion.split(".").map(Number);
const MIN_OPENSPEC = requirement.minimumVersion.split(".").map(Number);
const MAX_OPENSPEC = requirement.maximumExclusiveVersion.split(".").map(Number);
const INSTALL_SPEC = requirement.installSpec;
const EXCLUDED_TOP_LEVEL = new Set([
  ".git",
  ".github",
  "node_modules",
  "zWorkFlow Pack",
  "zWorkFlow Pack.zip",
]);

export function parseVersion(text) {
  const match = String(text ?? "").match(/v?(\d+)\.(\d+)\.(\d+)/);
  return match ? match.slice(1, 4).map(Number) : null;
}

export function compareVersion(left, right) {
  for (let index = 0; index < 3; index += 1) {
    if (left[index] !== right[index]) return left[index] - right[index];
  }
  return 0;
}

export function isCompatibleOpenSpec(version) {
  return Boolean(version)
    && compareVersion(version, MIN_OPENSPEC) >= 0
    && compareVersion(version, MAX_OPENSPEC) < 0;
}

function run(command, args, options = {}) {
  if (process.platform === "win32") {
    return spawnSync(process.env.ComSpec ?? "cmd.exe", ["/d", "/s", "/c", command, ...args], {
      encoding: "utf8",
      stdio: options.inherit ? "inherit" : "pipe",
      shell: false,
    });
  }
  return spawnSync(command, args, {
    encoding: "utf8",
    stdio: options.inherit ? "inherit" : "pipe",
    shell: false,
  });
}

export function detectOpenSpec() {
  const result = run("openspec", ["--version"]);
  const output = `${result.stdout ?? ""}\n${result.stderr ?? ""}`.trim();
  return {
    available: result.status === 0,
    output,
    version: result.status === 0 ? parseVersion(output) : null,
  };
}

export function copyPortablePackage(sourceRoot, destinationRoot) {
  if (existsSync(destinationRoot)) {
    throw new Error(`目标已存在，未覆盖：${destinationRoot}`);
  }

  const stagingRoot = resolve(
    dirname(destinationRoot),
    `.${basename(destinationRoot)}.install-${process.pid}-${Date.now()}`,
  );
  mkdirSync(stagingRoot, { recursive: false });
  try {
    for (const entry of readdirSync(sourceRoot, { withFileTypes: true })) {
      if (EXCLUDED_TOP_LEVEL.has(entry.name)) continue;
      cpSync(resolve(sourceRoot, entry.name), resolve(stagingRoot, entry.name), {
        recursive: true,
        errorOnExist: true,
        force: false,
      });
    }
    renameSync(stagingRoot, destinationRoot);
  } catch (error) {
    rmSync(stagingRoot, { recursive: true, force: true });
    throw error;
  }
}

function ensureRuntime() {
  const nodeVersion = parseVersion(process.version);
  if (!nodeVersion || compareVersion(nodeVersion, MIN_NODE) < 0) {
    throw new Error(`需要 Node.js >= 20.19.0，当前为 ${process.version}`);
  }

  let openspec = detectOpenSpec();
  if (isCompatibleOpenSpec(openspec.version)) {
    return { node: process.version, openspec: openspec.output, installed: false };
  }

  if (openspec.version && compareVersion(openspec.version, MAX_OPENSPEC) >= 0) {
    throw new Error(`检测到不兼容的 OpenSpec ${openspec.output}；不会自动降级 2.x`);
  }

  process.stdout.write(`正在安装兼容的 OpenSpec CLI：${INSTALL_SPEC}\n`);
  const install = run("npm", ["install", "-g", INSTALL_SPEC], { inherit: true });
  if (install.status !== 0) throw new Error("OpenSpec CLI 安装失败");

  openspec = detectOpenSpec();
  if (!isCompatibleOpenSpec(openspec.version)) {
    throw new Error(`安装后验证失败：${openspec.output || "openspec 命令不可用"}`);
  }
  return { node: process.version, openspec: openspec.output, installed: true };
}

function help() {
  process.stdout.write(`zWorkFlow bootstrap CLI

用法：
  zworkflow setup [项目目录]
  zworkflow doctor

直接从 GitHub 使用：
  npx --yes github:Hubr1zz/zWorkFlow setup

setup 会安装到 <项目目录>/zWorkFlow，不覆盖已有目录。随后仍需让 Agent
读取 zWorkFlow/setup/SETUP_NEW_PROJECT.md，完成项目事实分析与安全适配。
`);
}

function main(argv) {
  const command = argv[0] ?? "help";
  if (["help", "--help", "-h"].includes(command)) return help();

  if (command === "doctor") {
    const nodeVersion = parseVersion(process.version);
    const openspec = detectOpenSpec();
    process.stdout.write(`Node.js: ${process.version}\n`);
    process.stdout.write(`OpenSpec: ${openspec.output || "not found"}\n`);
    if (!nodeVersion || compareVersion(nodeVersion, MIN_NODE) < 0) {
      throw new Error("Node.js 版本不兼容；doctor 不会自动安装运行时");
    }
    if (!isCompatibleOpenSpec(openspec.version)) {
      throw new Error("OpenSpec CLI 缺失或版本不兼容；运行 setup 可自动安装兼容 1.x");
    }
    return;
  }

  if (command !== "setup") throw new Error(`未知命令：${command}`);

  const projectRoot = resolve(argv[1] ?? process.cwd());
  if (!existsSync(projectRoot)) throw new Error(`项目目录不存在：${projectRoot}`);
  const destination = resolve(projectRoot, "zWorkFlow");
  const packageRoot = PACKAGE_ROOT;
  if (destination === packageRoot || destination.startsWith(`${packageRoot}${sep}`)) {
    throw new Error("目标目录不能位于当前 zWorkFlow 安装源内部");
  }

  const runtime = ensureRuntime();
  copyPortablePackage(packageRoot, destination);
  process.stdout.write(`\nzWorkFlow 已下载到：${destination}\n`);
  process.stdout.write(`Node.js：${runtime.node}\nOpenSpec：${runtime.openspec}\n`);
  process.stdout.write("下一步：让当前项目中的 Agent 读取 zWorkFlow/setup/SETUP_NEW_PROJECT.md 并执行完整 setup。\n");
}

const invokedPath = process.argv[1] ? resolve(process.argv[1]) : "";
if (invokedPath === fileURLToPath(import.meta.url)) {
  try {
    main(process.argv.slice(2));
  } catch (error) {
    process.stderr.write(`zWorkFlow setup 失败：${error.message}\n`);
    process.exitCode = 1;
  }
}
