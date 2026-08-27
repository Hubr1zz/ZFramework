# Local Codebase Query Contract

## 目的

用一次本地脚本调用替代多轮 `rg → Read → rg → Read`，优先解决当前 Unity 项目中
最常见的结构定位问题。实现为 PowerShell 7+，支持 Windows 与 macOS，无需安装 MCP、数据库、Python 包或
常驻进程。

## 产物角色

- 权威内容：默认扫描目标 Unity 项目的 `Assets/**/*.cs`；可用 `-SourceRoots` 指定项目内其他源码根。
- 派生索引：Git 管理的 `.agents/codebase-query/code-query-index.json`。
- 明确消费者：本 skill、人工 CLI 查询及支持执行 PowerShell 的 Agent。
- 生命周期：源码签名变化时自动更新；正式指纹只包含相对路径与规范化为 UTF-8/LF 后的源码内容哈希，因此不同机器和 worktree 对同一提交生成字节一致的产物。Git 忽略的本地 sidecar 用原始文件长度和写入时间复用已计算哈希；未变化文件不重读源码，变化文件只提取一次，随后对紧凑事实重新绑定。文件重命名或移动会移除旧路径并加入新路径。产物可随时删除并重建。
- 路径格式：索引和 JSON 输出统一保存 `/` 分隔的项目相对路径，访问文件时转换为当前操作系统的目录分隔符；源码、索引和控制台输入输出显式使用 UTF-8。

索引只保存文件路径、命名空间、完整类型名、类型/方法行范围、继承关系、方法签名、调用标识符、
已解析调用目标和类型引用，不复制源码正文。

## 命令

| 命令 | 参数 | 返回内容 |
| --- | --- | --- |
| `build` | 无 | 重建索引及统计 |
| `status` | 无 | 索引版本、文件数、是否新鲜 |
| `architecture` | `-Limit` | 顶层目录、命名空间、类型与方法统计 |
| `search` | `-Query`、`-Limit` | 匹配的文件、类型、方法 |
| `callers` | `-Query`、`-Limit` | 已解析调用者、定义位置及剩余同名词法候选；支持 `Type.Method` |
| `impact` | `-Path` 或 `-Query`、`-Limit` | 按已解析调用、已解析类型引用、词法命中分级的影响候选 |
| `changed` | `-Limit` | Git 改动文件及其候选影响文件 |
| `context` | `-Query`、`-Limit` | 单个类型/方法的定义范围、直接调用者/类型引用、词法缺口、相关测试和建议读取范围 |

可用 `-Root` 指定项目根目录，用 `-IndexPath` 改变缓存位置，用 `-SourceRoots`
覆盖默认 `Assets` 源码根。默认覆盖其中全部 C# 文件；需要缩小查询范围时，可通过
`-ExcludeRoots` 显式排除第三方或生成目录。默认 `-Limit 8`，`callers`/`impact` 折叠词法兜底，只返回总数和最多三个样本；`-IncludeLexical` 用于有意展开，`search -IncludeMethods` 用于展开类型成员。`-MaxOutputBytes` 默认 12288，超限时返回截断说明而不是把大段 JSON 注入上下文。输出默认是紧凑 JSON；传入 `-Pretty` 可得到缩进 JSON。
每次输出包含 `engine=codebase-query-regex-binding-v7` 与 `schemaVersion=7`，供 Agent 和人工核验查询来源。构建进度输出到 stderr 和 Git 忽略的本地进度快照，stdout 始终只包含最终 JSON。正式索引通过同目录临时文件原子替换，构建中断不会留下半份产物。

`scripts/run.ps1` 是唯一稳定公共入口。内部实现和绑定库按 capability marker 动态发现，
在 skill 目录内部重命名或移动不会破坏调用；公共入口本身必须保持稳定，否则任何外部
调用方都无法知道新的启动地址。

## 准确性

索引 v7 使用内容哈希、单文件一次提取、文件级增量复用、全量覆盖校验、声明行范围和轻量 C# 类型绑定，但仍不是 Roslyn 语义模型。

当前可以解析：

- 命名空间、普通 `using` 和 using alias。
- 类型完整名以及项目内基类、接口关系。
- 显式字段、参数、局部变量的声明类型。
- `var value = new SomeType()` 的直接类型推断。
- `receiver.Method()`、`Type.Method()`、`this.Method()` 和 `base.Method()`。
- 泛型方法声明及 `Type.Method<T>()` 调用。
- 当方法声明在项目内基类时，沿继承关系查找方法所有者。

输出分层：

- `resolvedCallers` / `resolved-call`：receiver 类型唯一，且目标类型或项目内基类声明了目标方法。
- `resolved-type`：完整类型引用已消歧，但没有形成方法绑定。
- `lexicalFallbackFiles` / `lexical`：只确认同名标识符，作为覆盖率回退。

文件级影响查询默认会从“词法兜底符号”中排除 `Start`、`Update`、`Dispose` 等高频生命周期/通用方法，但已解析到具体类型的方法调用仍保留；显式 `-IncludeLexical` 可恢复完整词法候选。原因是同名只证明拼写相同，无法证明语义相关；高频名称会把大量互不相干的文件带进候选集。

准确性边界：

- `resolved` 表示本地轻量规则得到唯一目标，不等同于编译器证明。
- 当前不按参数数量或参数类型区分重载。
- 不追踪属性返回值、任意方法返回值、链式调用、委托、lambda 或跨语句数据流。
- 变量映射是文件级近似；同名变量的复杂嵌套作用域仍可能需要源码核验。
- 扩展方法、动态分派、反射和外部程序集方法不会完整解析。
- 为避免漏掉同文件内部调用，声明文件可能保留在词法回退中。
- Unity 隐式生命周期和资源序列化关系需要额外核验。
- `callers` 与 `impact` 的空结果不能作为不存在关系的证明。
- Git `changed` 查询使用 `core.quotepath=false` 获取 UTF-8 路径；中文文件名能够正常进入索引和变更结果，但团队仍可采用英文文件名降低跨工具、终端和外部插件的兼容风险。

## 方法来源

工作方式参考 MIT 许可项目
[`DeusData/codebase-memory-mcp`](https://github.com/DeusData/codebase-memory-mcp)：提前建立
可复用结构索引，查询后再回到准确源码核验。本实现是面向本项目需求的独立轻量
实现，没有复制其源代码或打包其二进制。
