## Why

按当前库存判断素材配方是否解锁，会让玩家消耗最后一份素材后重新遗忘配方；工坊目录也未与当前营地内容世代交叉校验，错误配置只能在玩家操作时表现为永久不可达。

## What Changes

- 将首次成功带回或由营地事件获得的素材记录为战役级持久知识，素材耗尽不再撤销配方可见性。
- 旧存档以当前正库存素材幂等补种知识，并拒绝未来版本的发现状态 schema。
- Campaign 安装时交叉校验工坊身份、成本、前置发明和配方所需工坊均属于同一内容世代。
- 不改变 View 命令、Settlement ActionQueue 制造事务或 Showdown。

## Capabilities

### Modified Capabilities

- `expedition-build-progression-loop`: 回营正式提交素材时同时提交永久发现事实。
- `settlement-workshop-crafting`: 素材门禁改为已发现知识，库存只决定当前能否制造。
- `settlement-content-plan-lifecycle`: 增加工坊目录跨内容世代预检及发现状态迁移。

## Impact

- Settlement 权威状态、回营与事件资源提交、工坊纯规则。
- 战役内容安装预检和旧存档迁移。
- 既有 3D 工坊只读取更新后的可用配方投影。
