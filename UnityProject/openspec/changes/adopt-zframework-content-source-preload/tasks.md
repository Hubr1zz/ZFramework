## 1. 内容源与生命周期

- [x] 1.1 新增独立 Manifest、显式 Bundle 与结构化校验
- [x] 1.2 新增 ZFramework Singleton 内容源系统并覆盖并发、取消和释放
- [x] 1.3 调整 Procedure/GameApp/Bootstrap，使游戏只在预载成功后进入

## 2. 显式内容装配

- [x] 2.1 Campaign Candidate、事件世代与 Settlement Plan 改为消费 Bundle
- [x] 2.2 移除生产代码中的 `Resources.Load/LoadAll` 内容发现
- [x] 2.3 迁移内容资产到 AssetRaw，保留 GUID 并补齐特性表显式引用

## 3. 验证与对账

- [x] 3.1 Unity 6000.5.9f1 编译与 C# 构建通过
- [x] 3.2 内容源 4/4、装配事务 18/18、相关内容表 45/45
- [x] 3.3 核验 Manifest 引用可解析且生产代码无 Resources 内容加载
- [x] 3.4 发布前执行 Windows Player 构建与启动日志烟雾验证
