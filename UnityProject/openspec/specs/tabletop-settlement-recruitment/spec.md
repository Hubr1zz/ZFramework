# 营地桌面猎人招募

## Purpose

把设施值守产出的人口转化为猎人损失后的补员供给，并将完整流程放回 3D 营地桌面；玩家仍亲自命名角色，招募继续受 Settlement ActionQueue、Reactor 与持久化事务约束。

## Requirements

### Requirement: 招募从营火实体卡进入

营地桌面 SHALL 提供营火招募入口卡，并持续展示本次接纳成本及当前不可招募原因。

#### Scenario: 当前年度允许招募

- **WHEN** 营地存在候选模板、未达到名册上限且满足年度、资源与人口供给约束
- **THEN** 营火卡 SHALL 可点击并打开世界空间招募板

#### Scenario: 当前年度不可招募

- **WHEN** 名册已满、年度名额已使用、资源或人口供给不足，或没有候选模板
- **THEN** 营火卡 SHALL 禁止提交并展示原因

### Requirement: 人口是常规招募的配置化供给

招募内容 SHALL 配置人口成本。存活猎人不少于两名时，成功招募 SHALL 同时消耗有效资源成本与有效人口成本；存活猎人为零或一名时，有效人口成本 SHALL 为零，以避免伤亡后的补员软锁。零存活猎人的既有免费援助规则 SHALL 保持不变。

#### Scenario: 设施值守人口支持常规补员

- **WHEN** 至少两名猎人存活且营地拥有足够资源和人口供给
- **THEN** 成功招募 SHALL 各扣除一次权威有效成本并创建一名猎人

#### Scenario: 人口供给不足

- **WHEN** 至少两名猎人存活但人口低于有效人口成本
- **THEN** 招募 SHALL 失败、展示人口不足原因，且不得扣资源或改变名册

#### Scenario: 营地接近覆灭

- **WHEN** 存活猎人为零或一名
- **THEN** 有效人口成本 SHALL 为零且人口不得变为负数

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

招募 View SHALL 只提交模板与名字，不得直接扣除资源、人口、创建猎人或写入年鉴。Settlement Runner SHALL 在 Before Reactor 完成后计算有效成本，串行重验模板、年度、名册、资源、人口与名字，并在提交后发布事实。

#### Scenario: 重复或并发确认

- **WHEN** 玩家连续点击确认，或其他效果同时改变招募条件
- **THEN** View SHALL 抑制本地重复请求，Runner SHALL 只提交仍满足条件的事务

#### Scenario: Reactor 修改招募条款

- **WHEN** Before Reactor 修改资源成本、人口成本或名册上限
- **THEN** Runner SHALL 以修改后的配置重新计算有效成本，并让校验与实际扣除使用同一组数值

### Requirement: 正常流程不创建旧屏幕空间招募窗

正式 `PlayableGameBootstrap` SHALL 不再实例化 `PlayableRecruitmentView`；旧类只作为尚未清理序列化引用时的兼容实现保留。

#### Scenario: 正式营地进入招募流程

- **WHEN** `PlayableGameBootstrap` 建立可游玩营地桌面
- **THEN** 招募 SHALL 只通过世界空间营火卡与招募板呈现
- **AND** 不得创建旧 `PlayableRecruitmentView`
