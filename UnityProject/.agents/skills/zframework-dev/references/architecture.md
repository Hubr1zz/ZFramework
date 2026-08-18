# ZFramework 架构与项目结构

> 需要核对当前程序集、模块入口、Procedure 和包版本时，同时读取 [code-map.md](code-map.md)。

## 分层

```text
GameLogic / GameProto       业务逻辑与配置协议
          ↓
Assembly-CSharp / Launcher  GameEntry、Procedure、启动 UI
          ↓
ZFramework.Runtime             模块系统、资源、事件、FSM、池
          ↓
Unity / YooAsset / UniTask / Luban
```

依赖保持向下。`Assets/GameScripts/GameEntry.cs` 与 `Procedure/` 属于 `Assembly-CSharp`；自定义 asmdef 不能引用其中的类型。`GameLogic` 和 `GameProto` 随 Player 编译，`HotFix` 仅是历史目录名。

## 关键目录

| 路径 | 职责 |
|---|---|
| `Assets/ZFramework/Runtime/` | 框架核心与模块实现 |
| `Assets/ZFramework/Editor/` | 构建和编辑器工具 |
| `Assets/Launcher/` | 启动阶段 UI |
| `Assets/GameScripts/Procedure/` | 启动状态机 |
| `Assets/GameScripts/HotFix/GameLogic/` | 游戏业务普通程序集 |
| `Assets/GameScripts/HotFix/GameProto/` | Luban 配置普通程序集 |
| `Assets/AssetRaw/` | YooAsset 收集资源 |
| `Configs/GameConfig/` | Luban 配置工程（仓库根目录） |

## 生命周期

1. `RootModule.Awake()` 初始化框架环境。
2. `GameEntry.Awake()`创建核心模块并启动 Procedure。
3. `RootModule.Update()`驱动 `ModuleSystem.Update()`。
4. `ProcedureStartGame` 调用无参数的 `GameApp.Entrance()`。
5. `GameApp.Entrance()`先调用 `GameEventHelper.Init()`，再启动业务。
6. 销毁时释放业务单例，Player 退出时关闭 ModuleSystem。

## 资源与内容边界

- 默认资源使用 `DefaultPackage`。
- DLC/资源型 Mod 使用独立 YooAsset Package，在 `ProcedureInitContentPackages` 发现、校验和初始化。
- 存档、设置、日志和可写 Mod 清单使用文件系统。
- 不加载外部 C# DLL；逻辑 Mod 需要另行设计受限脚本 API。

## 事实来源

- 当前代码地图：[code-map.md](code-map.md)
- 程序集与内容包：[assembly-content-workflow.md](assembly-content-workflow.md)
- 模块 API：[modules.md](modules.md)
- 资源 API：[resource-api.md](resource-api.md)
