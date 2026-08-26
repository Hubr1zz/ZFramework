## 1. 数据与事件事务

- [x] 1.1 增加持久风险租约与旧存档兼容字段
- [x] 1.2 增加表驱动选项效果、作用域校验与批次预检
- [x] 1.3 配置一个含安全分支的生产事件案例

## 2. Campaign 投影与消费边界

- [x] 2.1 通过 installer registry 把租约投影到 Hunt ActionEnvironment
- [x] 2.2 支持当前/未来 session、authority swap、Reset 与 Dispose 生命周期
- [x] 2.3 在成功回营权威 Action 内幂等消费租约

## 3. 验证与收养

- [x] 3.1 编译、78 项相关 EditMode 与 2 项 PlayMode 生命周期测试通过
- [x] 3.2 对抗复核失败关闭、重放、回滚、跨 session 与回营边界
- [ ] 3.3 人类审查本 Delta Spec 后批准同步到正式 Spec
