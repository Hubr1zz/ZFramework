# Hunting in Darkness → TEngine 脚本迁移清单

迁移日期：2026-08-06  
源项目：`D:\UnityProjects\My project`（只读，迁移后 Git 状态为空）  
目标项目：`D:\UnityProjects\ZFramework\UnityProject`

## 迁移结果

- 复制 164 个游戏运行时 C# 脚本，覆盖源项目的 `3DCardTest`、`Adapters`、
  `Cards3D`、`Core`、`GameCore`、`GameplayBase`、`SO`、`UI`、
  `ViewLayer`、`InteractionSystem/Runtime`，以及实际作为 Combat 场景组件的
  `UnitTests/CombatTestBootstrap`。
- 新增 1 个 Odin Inspector 编译兼容脚本。
- 复制 3 个游戏内容作者工具脚本；没有复制 zWorkFlow Workbench Editor 脚本。
- 13 个源脚本为适配目标依赖或 TEngine 运行契约而改写，其余游戏脚本保持源码内容不变。

## 分层与 TEngine 的职责映射

| 现有层 | 是否与 TEngine 重复 | 迁移后的边界 |
| --- | --- | --- |
| GameCore / 数据规则层 | 不重复 | 保持纯 C#、无 Unity 引用；继续作为唯一玩法规则与权威状态层。 |
| Adapters | 部分重复 | 坐标、SO 映射、持久化和组合适配保留；全局事件、启动、异步生命周期交给 TEngine。 |
| ViewLayer / UI | 部分重复 | 世界空间 3D、场景 HUD、相机与实体视图仍用 MonoBehaviour；将来新增的完整窗口使用 TEngine `UIWindow/UIModule`。 |
| GameManager | 不属于 TEngine 的替代品 | 保留为玩法阶段与子系统组合根；由 TEngine Procedure → GameApp 启动，不再自行成为框架入口。 |

关键原则是“TEngine 管基础设施和生命周期，Hunting in Darkness 管玩法语义”，不把领域规则重写成 TEngine Module。

## 已调整的游戏脚本（逐文件）

| 脚本 | 具体调整 |
| --- | --- |
| `Core/EventBus.cs` | 保留原泛型 API 以避免改写约 130 个调用点；内部改为 TEngine `GameEvent` 的稳定类型路由，移除独立监听表，只保留可清理的订阅句柄。 |
| `Core/GameManager.cs` | 存档、读档、删除调用改为 UniTask，并绑定 `GetCancellationTokenOnDestroy()`；玩法组合职责不变。 |
| `Adapters/Unity/Persistence/SaveLoadSystem.cs` | 同步文件 IO 改为 UniTask 线程池 IO；支持 CancellationToken；反序列化回主线程；销毁取消不再记录为失败。 |
| `UI/DevModePanel.cs` | 存档存在性检查与删除改为异步调用，绑定面板销毁生命周期。 |
| `3DCardTest/CombatTestSetup.cs` | 测试启动 Coroutine 改为 UniTask，并绑定组件销毁取消。 |
| `Cards3D/Base/CardSlot.cs` | 卡牌动画 Coroutine 改为 UniTask；动画中断与对象销毁可取消。 |
| `Cards3D/Views/ResourceCard3D.cs` | 延迟刷新 Coroutine 改为 UniTask，并绑定组件生命周期。 |
| `InteractionSystem/InputTrigger.cs` | 目标项目未安装 Input System；输入引用改为可编译、可运行的 `KeyCode` 轮询。 |
| `InteractionSystem/InteractableThreeDBehaviour.cs` | 移除 Input System 依赖，改用 `KeyCode`；其余交互分发语义保留。 |
| `InteractionSystem/InteractableObject.cs` | 移除 Odin 基类，改为 `MonoBehaviour`；多态交互数据使用 `[SerializeReference]`。 |
| `InteractionSystem/InteractableUIElement.cs` | 移除 Odin 基类，改为 `MonoBehaviour`；多态交互数据使用 `[SerializeReference]`。 |
| `SO/Combat/CombatFieldRulesSO.cs` | 移除 `SerializedScriptableObject`，改为标准 `ScriptableObject`，避免引入目标项目不存在的 Odin 运行时。 |
| `Editor/HuntingInDarkness/ActionCardEditorWindow.cs` | 从 Odin EditorWindow 重写为原生 Unity EditorWindow，保留行动卡创建/编辑用途。 |

## 新增与目标框架调整

| 脚本/配置 | 具体调整 |
| --- | --- |
| `Compatibility/OdinInspectorCompatibility.cs` | 新增：在没有 `ODIN_INSPECTOR` 定义时提供无行为 Inspector 特性，仅保证编译；Unity 序列化仍由标准字段与 `SerializeReference` 决定。 |
| `Assets/GameScripts/GameLogic/GameLogic.asmdef` | 恢复目标 GameLogic 程序集并引用 `TEngine.Runtime`、`UniTask`、TMP、UGUI 和纯 C# `HuntingInDarkness.GameCore`。 |
| `Assets/GameScripts/GameLogic/GameApp.cs` | 作为 TEngine 游戏入口初始化事件设施、查找已配置的 GameManager；缺少配置时明确中止，不创建会在战斗装配阶段崩溃的空根对象；退出时清理 EventBus/Singleton/GameModule。 |
| `Assets/GameScripts/Procedure/ProcedureStartGame.cs` | TEngine 启动 Procedure 在 Launcher UI 隐藏后调用 `GameApp.Entrance()`。 |
| `GameModule.cs`、`Module/UIModule/**`、`SingletonSystem/**` | 从目标项目自身的迁移隔离区恢复 TEngine GameLogic 支撑脚本；未用源项目实现覆盖。目标已存在的 `FrameworkUI/UIBindComponent` 保留，重复副本未恢复。 |

## 不需要逐个改写的脚本

其余 151 个游戏运行时脚本和 2 个内容 Editor 脚本保持源代码内容不变。它们符合以下任一情况：

- 纯 C# 规则/数据代码，不依赖 TEngine 基础设施；
- Unity Adapter、世界空间 View 或场景 HUD，属于 TEngine UI 窗口体系之外的合法表现层；
- 通过保留的 `Core.EventBus` API 间接使用 TEngine GameEvent，不需要机械改写调用点。

## 仍需在 Unity 中手工完成的内容工作

这次范围按要求只迁移/创建脚本，没有复制 Scene、Prefab、材质、字体、ScriptableObject 实例或其他内容资源。因此脚本已进入 TEngine
结构并可编译，但目标游戏还不能仅凭这些脚本获得完整可玩内容：

1. 在目标启动场景放置 `GameManager`，连接阶段根节点、Canvas、相机、Settlement UI 和内容 ScriptableObject。
2. 重建或迁移 Boss、角色、场地、卡牌、武器等 SO 实例，并重新连接缺失 GUID。
3. 若要保留原 Input System 的 InputAction 资产与绑定，需先在目标安装 Input System，再把两个 KeyCode 适配脚本恢复为 InputActionReference 版本并重绑资产。
4. 若迁移旧 Odin 序列化资产，需要安装 Odin 并恢复原类型，或编写一次性数据转换；当前无行为兼容特性不会还原 Odin 私有序列化数据。
5. 把真正的全屏 UI Prefab 注册到 TEngine UIModule；现有 3D 卡桌、场景 HUD 和相机脚本不应强行改成 UIWindow。

## 验证

- Unity 6 batch compile 曾在迁移中间态完整通过；最终批处理因目标项目正被用户的 Unity Editor 打开而无法再次启动，未强制关闭编辑器。
- 最终 `GameLogic.csproj`：0 error。
- 最终 `Assembly-CSharp.csproj`：0 error。
- 最终 `Assembly-CSharp-Editor.csproj`：0 error。
- 静态扫描未发现迁移代码中的 `StartCoroutine`、`IEnumerator`、`WaitForSeconds`、
  `SceneManager`、内容 `Resources.Load` 或直接业务层 `ModuleSystem.GetModule` 调用。

已知非阻塞警告包括 Unity 6 的 `FindObjectOfType`/TMP 过时 API、InteractionSystem 的
`GetHashCode`/Unity Object 比较警告、CardSlot 的非穷举枚举 switch，以及目标项目 MCP
程序集带来的 .NET 程序集版本警告。它们不阻止编译，但建议进入后续技术债清理。
