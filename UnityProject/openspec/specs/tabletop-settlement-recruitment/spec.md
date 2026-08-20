# 营地桌面猎人招募

## Purpose

把猎人损失后的补员流程完整放回 3D 营地桌面，同时保留玩家亲自命名角色的情感投入，并让招募继续受 Settlement ActionQueue、Reactor 与持久化事务约束。

## Requirements

### Requirement: 招募从营火实体卡进入

营地桌面 SHALL 提供营火招募入口卡，并持续展示本次接纳成本及当前不可招募原因。

#### Scenario: 当前年度允许招募

- **WHEN** 营地存在候选模板、未达到人口上限且满足年度与资源约束
- **THEN** 营火卡 SHALL 可点击并打开世界空间招募板

#### Scenario: 当前年度不可招募

- **WHEN** 人口已满、年度名额已使用、资源不足或没有候选模板
- **THEN** 营火卡 SHALL 禁止提交并展示原因

### Requirement: 候选猎人使用 3D 卡牌展示

招募板 SHALL 把候选模板显示为可选择的 3D 猎人卡；候选数量超过单页容量时 SHALL 分页，而不是让内容超出面板。

#### Scenario: 候选数量超过五人

- **WHEN** 内容表提供超过五个候选模板
- **THEN** 玩家 SHALL 可通过世界空间翻页按钮访问全部候选

### Requirement: 玩家必须为新猎人命名

招募板 SHALL 提供世界空间命名牌，支持键盘、中文输入法组合文本、退格和粘贴，并使用 `RecruitmentRules` 校验长度、控制字符与重名。

#### Scenario: 名字有效

- **WHEN** 玩家选择候选卡并输入有效且未重复的名字
- **THEN** 确认按钮或回车 SHALL 提交招募命令

#### Scenario: 名字无效

- **WHEN** 名字为空、过长、包含控制字符或与营地历史名字重复
- **THEN** View SHALL 展示规则原因且不得提交状态变更

### Requirement: 招募事务由营地 Runner 掌权

招募 View SHALL 只提交模板与名字，不得直接扣除资源、创建猎人或写入年鉴。Settlement Runner SHALL 串行重验模板、年度、人口、资源与名字，并在提交后发布事实。

#### Scenario: 重复或并发确认

- **WHEN** 玩家连续点击确认，或其他效果同时改变招募条件
- **THEN** View SHALL 抑制本地重复请求，Runner SHALL 只提交仍满足条件的事务

### Requirement: 正常流程不创建旧屏幕空间招募窗

正式 `PlayableGameBootstrap` SHALL 不再实例化 `PlayableRecruitmentView`；旧类只作为尚未清理序列化引用时的兼容实现保留。

#### Scenario: 正式营地进入招募流程

- **WHEN** `PlayableGameBootstrap` 建立可游玩营地桌面
- **THEN** 招募 SHALL 只通过世界空间营火卡与招募板呈现
- **AND** 不得创建旧 `PlayableRecruitmentView`
