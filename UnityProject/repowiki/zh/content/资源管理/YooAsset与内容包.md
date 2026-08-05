# YooAsset 与内容包

默认游戏资源由 `DefaultPackage` 管理。`IResourceModule.InitPackage(packageName, needInitMainFest)` 支持初始化额外 Package，所有主要加载、查询、下载和缓存 API 都接受可选 `packageName`。

DLC/Mod 推荐边界：

- 每个内容包使用稳定且唯一的 Package 名称。
- 在 `ProcedureInitContentPackages` 扫描清单、校验游戏版本/平台/依赖后再初始化。
- 资源、Luban 二进制配置和只读内容可以进入 YooAsset Package。
- 存档、用户设置、日志和 `mod.json` 等可写文件使用文件系统。
- 当前架构不加载任意 C# DLL；逻辑 Mod 需另行设计受限、版本化脚本 API。

业务代码通过 `GameModule.Resource` 加载，并使用 `packageName` 明确内容来源。手动加载的 Asset 必须对应 `UnloadAsset`；实例化 GameObject 使用 `LoadGameObjectAsync`。
