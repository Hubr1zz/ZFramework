## 1. 阶段端口

- [x] 1.1 定义 Settlement、Hunt、Showdown 窄操作端口并由 CampaignRuntime 暴露
- [x] 1.2 将 manager/session factory 与 Hunt current provider 收回阶段管理器内部
- [x] 1.3 删除 CampaignRuntime 公共 factory 配置逃逸口

## 2. GameManager 收口

- [x] 2.1 移除 GameManager 对三个具体 manager/coordinator 的字段与引用
- [x] 2.2 保留顶层 FSM、跨阶段事务、启动保存关闭与既有兼容 API

## 3. 验证

- [x] 3.1 增加端口权限与 GameManager 字段的反射架构约束
- [x] 3.2 运行 phase runtime 与 campaign-loop 定向 PlayMode、编译和 diff 验证
