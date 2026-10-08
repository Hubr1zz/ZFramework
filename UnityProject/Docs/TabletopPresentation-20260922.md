# HuntingInDarkness 表现层改造记录

日期：2026-09-22

## 本轮结果

1. 营地入口按功能分排，主场景相机使用约 75° 的俯视取景，实体入口保持独立点击区域。现有三类卡牌 Prefab 统一了字号、排版和中文字体引用；运行时动态卡沿用同一套尺寸与基线常量，避免静态卡和动态卡出现基线漂移。
   狩猎资源标记、携带物以及采集/消耗品面板统一引用狩猎中文字体并使用一致的字号层级。
2. 事件与年鉴改为屏幕 2D UI。事件正文、选项和年鉴记录都保留完整内容并支持滚动；选项的可用条件、禁用原因、判定式、骰点范围、成功/失败边界、实际结果和后续事件均展示已有数据。事件和年鉴的遮罩、按钮及关闭帧输入由屏幕模态输入门统一管理。
3. 被动发明、主动发明及发明候选预览均保留。候选预览只读，不提交解锁或生产；四个实体扩区入口分别对应工坊、资源、已掌握发明和发明牌堆预览，各自独立展开并使用互斥的内容区域。
4. 没有修改玩法数据、结算流程、Core、Bootstrap 或存档结构。事件仍沿用既有流程与随机交互端口，表现层只读取并呈现权威结果。

## 资产位置与手动调整

- 营地布局和四个扩区入口：`Assets/Prefabs/HuntingInDarkness/Settlement/SettlementTableVisuals.prefab`。
- 75° 俯视取景由主场景 `Assets/Scenes/main.unity` 中的 `SettlementCameraController` 持有；布局 Prefab 不保存相机控制权。
- 已有卡牌 Prefab：`Assets/Prefabs/HuntingInDarkness/Cards/ResourceCard3D.prefab`、`WorkshopCard3D.prefab`、`WorkshopRecipeCard3D.prefab`。
- 动态卡排版基线：`Assets/GameScripts/GameLogic/HuntingInDarkness/Cards3D/Base/CardPresentationConsts.cs`。
- 事件面板、选项模板、年鉴面板和条目模板：`Assets/AssetRaw/UI/Prefabs/TabletopEventPanelScreen.prefab`、`ScreenEventChoice.prefab`、`CampLedgerPanelScreen.prefab`、`ScreenLedgerEntry.prefab`。
- 屏幕 UI 注册宿主和场景入口：`Assets/AssetRaw/UI/Prefabs/TabletopPresentationAssets.prefab`、`Assets/Scenes/main.unity`。
- 中文字体：`Assets/AssetRaw/Fonts/HuntingInDarkness/TabletopUsabilityFont.asset`。

Canvas 排序层、RectTransform、字号、颜色、滚动视口、滚动条和营地入口位置都保存在 Prefab 或场景中，可以直接在 Inspector 手动调整。一次性 installer 已移除；安装后的 Prefab 与场景才是正式表现数据，不需要重新运行 installer。

## 规则方案边界

当前 `EventOption` 只有 success/fail 两个结果分支。本轮没有新增多区间骰点玩法；若以后要让不同点数区间进入不同子事件，应另案增加有序 `orderedRanges` 数据、区间不重叠与覆盖合法性测试，以及结算阶段的区间分派逻辑。

无限资源增长只需要表现层分页或虚拟化时，可以作为后续 UI 优化，不新增玩法规则或改变存档模型。

## 验证状态

Unity 编译通过；EditMode 23/23，加资源标记 1/1，共 24 项通过；PlayMode 59 项前轮通过 56 项，修复后末次定点 5/5（覆盖剩余 3 项及 2 项扩区测试）；狩猎携带物/消耗品追加 2/2 通过。事件、年鉴和营地截图使用真实数据与 Prefab 做离屏验证，未人工遍历全部剧情。
