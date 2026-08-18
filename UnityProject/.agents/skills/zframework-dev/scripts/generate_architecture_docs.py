#!/usr/bin/env python3
"""Generate concise ZFramework architecture docs from the current Unity project."""

from __future__ import annotations

import json
import re
import shutil
from pathlib import Path


PROJECT = Path(__file__).resolve().parents[4]
REPO = PROJECT.parent
ASSETS = PROJECT / "Assets"
OUTPUT = PROJECT / "repowiki" / "zh" / "content"
STALE_META = PROJECT / "repowiki" / "zh" / "meta"
CODE_MAP = PROJECT / ".claude" / "skills" / "zframework-dev" / "references" / "code-map.md"


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig")


def write(relative: str, content: str) -> None:
    path = OUTPUT / relative
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content.strip() + "\n", encoding="utf-8")


def rel(path: Path) -> str:
    return path.relative_to(PROJECT).as_posix()


def collect() -> dict:
    asmdefs = []
    guid_to_name = {}
    for path in ASSETS.rglob("*.asmdef"):
        meta = Path(str(path) + ".meta")
        if meta.exists():
            match = re.search(r"^guid:\s*(\w+)", read(meta), re.MULTILINE)
            if match:
                guid_to_name[match.group(1)] = json.loads(read(path))["name"]
    for path in sorted(ASSETS.rglob("*.asmdef")):
        data = json.loads(read(path))
        internal = []
        for reference in data.get("references", []):
            guid = reference.removeprefix("GUID:")
            if guid in guid_to_name:
                internal.append(guid_to_name[guid])
        asmdefs.append({"name": data["name"], "path": rel(path.parent), "refs": internal})

    setting = read(ASSETS / "ZFramework/Settings/ProcedureSetting.asset")
    procedures = re.findall(r"^  - Procedure\.(\w+)$", setting, re.MULTILINE)
    entrance = re.search(r"entranceProcedureTypeName: Procedure\.(\w+)", setting).group(1)
    transitions = {}
    for name in procedures:
        source = ASSETS / "GameScripts/Procedure" / f"{name}.cs"
        found = re.findall(r"ChangeState<(Procedure\w+)>", read(source)) if source.exists() else []
        transitions[name] = list(dict.fromkeys(found))

    game_module = read(ASSETS / "GameScripts/HotFix/GameLogic/GameModule.cs")
    modules = re.findall(r"public static ([\w<>]+) (\w+)\s*(?:\{|=>)", game_module)

    implementations = []
    for path in (ASSETS / "ZFramework/Runtime/Module").rglob("*.cs"):
        for match in re.finditer(r"(?:class|sealed class)\s+(\w+)[^{:]*:\s*([^\n{]+)", read(path)):
            bases = [item.strip() for item in match.group(2).split(",")]
            if "Module" in bases or any(item.startswith("I") and item.endswith("Module") for item in bases):
                implementations.append((match.group(1), rel(path), ", ".join(bases)))

    manifest = json.loads(read(PROJECT / "Packages/manifest.json"))["dependencies"]
    lock = json.loads(read(PROJECT / "Packages/packages-lock.json"))["dependencies"]
    packages = {
        name: f"{info['version']} ({info['source']})"
        for name, info in lock.items()
        if info.get("depth") == 0 and not name.startswith("com.unity.modules.")
    }
    unity_modules = sorted(name for name in manifest if name.startswith("com.unity.modules."))
    return {
        "asmdefs": asmdefs,
        "procedures": procedures,
        "entrance": entrance,
        "transitions": transitions,
        "modules": modules,
        "implementations": sorted(set(implementations)),
        "packages": packages,
        "unity_modules": unity_modules,
    }


def generate(data: dict) -> None:
    if OUTPUT.exists():
        resolved = OUTPUT.resolve()
        expected = (PROJECT / "repowiki" / "zh" / "content").resolve()
        if resolved != expected:
            raise RuntimeError(f"Refusing to remove unexpected path: {resolved}")
        shutil.rmtree(OUTPUT)
    if STALE_META.exists():
        resolved_meta = STALE_META.resolve()
        expected_meta = (PROJECT / "repowiki" / "zh" / "meta").resolve()
        if resolved_meta != expected_meta:
            raise RuntimeError(f"Refusing to remove unexpected path: {resolved_meta}")
        shutil.rmtree(STALE_META)
    OUTPUT.mkdir(parents=True)

    asm_rows = "\n".join(
        f"| `{a['name']}` | `{a['path']}` | {', '.join(f'`{r}`' for r in a['refs']) or '仅第三方/Unity 依赖'} |"
        for a in data["asmdefs"]
    )
    module_rows = "\n".join(f"| `GameModule.{name}` | `{kind}` |" for kind, name in data["modules"])
    package_rows = "\n".join(f"| `{name}` | `{version}` |" for name, version in sorted(data["packages"].items()))
    procedure_rows = "\n".join(
        f"| `{name}` | {', '.join(f'`{target}`' for target in data['transitions'][name]) or '终点/异步内部切换'} |"
        for name in data["procedures"]
    )

    CODE_MAP.write_text(
        (f"""# 当前代码地图

> 由 `scripts/generate_architecture_docs.py` 从当前源码生成，不要手工修改。

## 自定义程序集

| 程序集 | 路径 | 工程内依赖 |
|---|---|---|
{asm_rows}

## GameModule 入口

| 入口 | 类型 |
|---|---|
{module_rows}

## 注册流程

入口：`{data['entrance']}`

| Procedure | 直接状态切换 |
|---|---|
{procedure_rows}

## 非 Unity 内置包

| 包 | 版本/来源 |
|---|---|
{package_rows}

Unity 内置模块共 {len(data['unity_modules'])} 个，完整列表以 `Packages/manifest.json` 为准。
""").strip() + "\n",
        encoding="utf-8",
    )

    write("index.md", """
# ZFramework 架构文档

本目录由当前源码生成，描述精简后的单机游戏架构。代码是最终事实来源。

- [架构总览](项目概述/架构总览.md)
- [程序集与依赖](核心架构/程序集与依赖.md)
- [启动与生命周期](核心架构/启动与生命周期.md)
- [模块系统](模块系统/模块系统.md)
- [启动流程](流程管理/启动流程.md)
- [YooAsset 与内容包](资源管理/YooAsset与内容包.md)
- [UI 与事件](UI系统/UI与事件.md)
- [Luban 配置](配置系统/Luban配置.md)
- [构建与发布](部署发布/构建与发布.md)
- [代码导航](开发者指南/代码导航.md)
""")
    write("项目概述/架构总览.md", f"""
# 架构总览

ZFramework 当前采用 Unity Player 原生程序集、ZFramework 模块系统、YooAsset 资源管理、UniTask 异步和 Luban 配置表。不使用托管代码热更新或代码混淆插件。

```text
GameLogic / GameProto       游戏业务与配置协议
          ↓
Assembly-CSharp / Launcher  启动流程与启动 UI
          ↓
ZFramework.Runtime             模块、资源、事件、FSM、对象池
          ↓
Unity + YooAsset + UniTask + Luban
```

核心原则：业务层通过 `GameModule` 使用框架模块；资源通过 YooAsset；模块间通过 `GameEvent` 解耦；IO 优先使用 UniTask。

当前工程定义 {len(data['asmdefs'])} 个自定义 asmdef，注册 {len(data['procedures'])} 个启动 Procedure，保留 {len(data['unity_modules'])} 个 Unity 内置模块。
""")
    write("核心架构/程序集与依赖.md", f"""
# 程序集与依赖

| 程序集 | 路径 | 工程内依赖 |
|---|---|---|
{asm_rows}

`Assets/GameScripts/GameEntry.cs` 与 `Assets/GameScripts/Procedure/` 没有独立 asmdef，属于 `Assembly-CSharp`。`HotFix` 只是历史目录名；`GameLogic` 和 `GameProto` 均随 Player 编译，不加载外部 DLL。

依赖方向应保持向下：游戏业务可依赖配置与框架，框架核心不能反向引用游戏业务。自定义 asmdef 不能依赖 `Assembly-CSharp` 类型，因此启动层调用业务入口，而业务层不能回调启动层具体类。
""")
    write("核心架构/启动与生命周期.md", """
# 启动与生命周期

1. 场景中的 `RootModule.Awake()` 初始化日志、时间和 Unity 运行参数。
2. `GameEntry.Awake()`创建 UpdateDriver、Resource、Debugger、FSM 模块。
3. `ProcedureSetting.StartProcedure()`启动入口状态 `ProcedureLaunch`。
4. `RootModule.Update()`每帧调用 `ModuleSystem.Update()`。
5. `ProcedureStartGame` 调用 `GameApp.Entrance()`；入口先初始化 `GameEventHelper`，再启动业务 UI。
6. Unity 销毁回调释放 `SingletonSystem`；非编辑器退出时 `ModuleSystem.Shutdown()`。

`GameEntry`、`RootModule` 和 `GameApp` 分别承担流程引导、框架帧驱动和业务入口，职责不要混合。
""")
    write("模块系统/模块系统.md", f"""
# 模块系统

业务代码统一通过缓存入口访问模块：

| 入口 | 类型 |
|---|---|
{module_rows}

`ModuleSystem.GetModule<T>()` 负责延迟创建框架模块，适用于框架内部和启动代码。普通业务代码使用 `GameModule`，避免重复查找并保持调用面稳定。

新增框架模块时实现模块接口与 `Module` 生命周期；新增纯业务系统优先放在 `GameLogic`，不要把玩法状态塞入 ZFramework.Runtime。
""")
    write("流程管理/启动流程.md", f"""
# 启动流程

入口 Procedure：`{data['entrance']}`。

单机/编辑器流程：

```text
Launch → Splash → InitPackage → InitResources
→ InitContentPackages → Preload → StartGame
```

Host/Web 更新流程：

```text
InitResources → CreateDownloader → DownloadFile → DownloadOver
→ ClearCache（按需）→ InitContentPackages → Preload → StartGame
```

| Procedure | 直接状态切换 |
|---|---|
{procedure_rows}

`ProcedureInitContentPackages` 是 DLC/Mod 扩展点；默认实现直接放行，不能让可选内容失败阻断基础游戏。
""")
    write("资源管理/YooAsset与内容包.md", """
# YooAsset 与内容包

默认游戏资源由 `DefaultPackage` 管理。`IResourceModule.InitPackage(packageName, needInitMainFest)` 支持初始化额外 Package，所有主要加载、查询、下载和缓存 API 都接受可选 `packageName`。

DLC/Mod 推荐边界：

- 每个内容包使用稳定且唯一的 Package 名称。
- 在 `ProcedureInitContentPackages` 扫描清单、校验游戏版本/平台/依赖后再初始化。
- 资源、Luban 二进制配置和只读内容可以进入 YooAsset Package。
- 存档、用户设置、日志和 `mod.json` 等可写文件使用文件系统。
- 当前架构不加载任意 C# DLL；逻辑 Mod 需另行设计受限、版本化脚本 API。

业务代码通过 `GameModule.Resource` 加载，并使用 `packageName` 明确内容来源。手动加载的 Asset 必须对应 `UnloadAsset`；实例化 GameObject 使用 `LoadGameObjectAsync`。
""")
    write("UI系统/UI与事件.md", """
# UI 与事件

UI 业务位于 `Assets/GameScripts/HotFix/GameLogic/UI/`，框架实现位于 `Module/UIModule/`。窗口继承 `UIWindow`，复用子区域继承 `UIWidget`，通过 `GameModule.UI` 打开和关闭。

模块间广播使用静态 `GameEvent`；窗口内部需要随生命周期自动清理的监听使用 `AddUIEvent`。`GameApp.Entrance()` 必须在首次接口事件之前调用 `GameEventHelper.Init()`。

UI 中的异步资源加载必须考虑窗口关闭后的取消与释放，避免回调持有已销毁对象。
""")
    write("配置系统/Luban配置.md", """
# Luban 配置

配置工程位于仓库根目录 `Configs/GameConfig/`。生成代码输出到 `UnityProject/Assets/GameScripts/HotFix/GameProto/GameConfig/`，属于普通 `GameProto` Player 程序集；二进制数据输出到 `Assets/AssetRaw/Configs/bytes/` 并由 YooAsset 管理。

业务层通过 `ConfigSystem.Instance.Tables` 访问配置。不要手工修改 Luban 生成代码；结构和数据改动后运行仓库提供的生成脚本，并同时提交生成代码与二进制数据。
""")
    write("部署发布/构建与发布.md", """
# 构建与发布

当前发布流程只包含资源构建与 Player 构建：

1. 生成并校验 Luban 配置（有配置改动时）。
2. 使用 YooAsset Builder 或 ZFramework ReleaseTools 构建 `DefaultPackage`。
3. 按目标平台构建 Player。
4. 对 Host/Web 模式部署资源版本与清单。

C# 变化必须重新发布 Player；默认资源可通过默认 Package 更新；DLC/资源型 Mod 使用独立 Package。构建流程不存在热更 DLL 生成、AOT 元数据补充或代码混淆步骤。
""")
    write("开发者指南/代码导航.md", f"""
# 代码导航

- 框架核心：`Assets/ZFramework/Runtime/`
- 编辑器工具：`Assets/ZFramework/Editor/`
- 启动入口：`Assets/GameScripts/GameEntry.cs`
- 启动状态机：`Assets/GameScripts/Procedure/`
- 游戏业务：`Assets/GameScripts/HotFix/GameLogic/`
- 配置协议：`Assets/GameScripts/HotFix/GameProto/`
- 可打包资源：`Assets/AssetRaw/`
- YooAsset 收集配置：`Assets/ZFramework/Settings/`
- AI 开发规范：`.agents/skills/zframework-dev/`

主要非 Unity 内置包：

| 包 | 版本/来源 |
|---|---|
{package_rows}
""")


if __name__ == "__main__":
    generate(collect())
    print(f"Generated architecture docs in {OUTPUT}")
    print(f"Generated code map at {CODE_MAP}")
