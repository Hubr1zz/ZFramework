# Luban 配置

配置工程位于仓库根目录 `Configs/GameConfig/`。生成代码输出到 `UnityProject/Assets/GameScripts/HotFix/GameProto/GameConfig/`，属于普通 `GameProto` Player 程序集；二进制数据输出到 `Assets/AssetRaw/Configs/bytes/` 并由 YooAsset 管理。

业务层通过 `ConfigSystem.Instance.Tables` 访问配置。不要手工修改 Luban 生成代码；结构和数据改动后运行仓库提供的生成脚本，并同时提交生成代码与二进制数据。
