## 1. 资源点生成

- [x] 1.1 仅从尚未达到同类上限的有效候选中按权重生成资源点
- [x] 1.2 验证同类型合法重复、候选耗尽与无效 ID 的终止行为

## 2. 内容与恢复门禁

- [x] 2.1 在 Hunt Bundle 准备阶段拒绝单地块重复资源点配置 ID
- [x] 2.2 恢复时校验资源点归属、总量、同类上限、翻牌数和素材池多重集合
- [x] 2.3 为 schema v2/v3 保留受限单素材迁移，并让 schema v4 保持严格

## 3. 验证

- [x] 3.1 使用 Unity 6000.5.9f1 完成编译
- [x] 3.2 完成 HuntResourceRules 6/6、PlayableHuntContentBundle 15/15、ActiveHuntPersistence 14/14 定向测试
- [x] 3.3 完成旧档兼容与恢复原子性的对抗审查
