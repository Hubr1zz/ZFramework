# 程序集、游戏入口与内容包

## 程序集边界

| 程序集 | 路径 | 职责 |
|---|---|---|
| `Assembly-CSharp` | `Assets/GameScripts/` | GameEntry 与 Procedure |
| `GameProto` | `Assets/GameScripts/HotFix/GameProto/` | Luban 生成代码和协议 |
| `GameLogic` | `Assets/GameScripts/HotFix/GameLogic/` | 业务、UI 与 GameApp |

三个程序集均随 Player 构建。不要生成、打包或运行外部托管 DLL。`GameLogic` 可以依赖 `GameProto` 与 `TEngine.Runtime`；自定义 asmdef 不得反向依赖 `Assembly-CSharp`。

## 游戏入口

`ProcedureStartGame.OnEnter()`直接调用：

```csharp
GameApp.Entrance();
```

`GameApp.Entrance()`保持无参数，并按以下顺序执行：

1. `GameEventHelper.Init()`。
2. 注册销毁回调。
3. 启动游戏业务或首个 UI。

## 启动流程

单机/编辑器：

```text
Launch → Splash → InitPackage → InitResources
→ InitContentPackages → Preload → StartGame
```

Host/Web 在 `InitResources` 后经过 Downloader、DownloadFile、DownloadOver 和可选 ClearCache，再进入 `InitContentPackages`。

## DLC/Mod 扩展点

在 `ProcedureInitContentPackages` 中执行：

1. 扫描外部内容清单。
2. 校验游戏版本、平台、依赖和 Package 名称。
3. 调用 `IResourceModule.InitPackage(packageName, needInitMainFest)`。
4. 对 Host/Web 内容包请求版本、更新清单并下载。
5. 记录已挂载包及覆盖优先级，再进入 Preload。

可选包失败不得破坏默认包启动。资源加载时显式传 `packageName`，避免同名 location 来源不明。

## 发布边界

- C#：重新构建 Player。
- 默认资源：构建 `DefaultPackage`。
- DLC/资源型 Mod：构建独立 YooAsset Package。
- 存档与可写文件：文件系统。
- 逻辑 Mod：未来采用受限、版本化脚本接口，不接受任意 C# DLL。
