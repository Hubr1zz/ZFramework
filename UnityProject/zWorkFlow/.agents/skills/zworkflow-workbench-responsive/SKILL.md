---
name: zworkflow-workbench-responsive
description: 维护或优化 zWorkFlow Unity Workbench 的页面、控件、布局或视觉表现时，审查并防止内容横向溢出、固定宽度挤压和窄窗口退化。仅用于 zWorkFlow 自身开发，不用于安装后的项目工作流。
---

# zWorkFlow Workbench Responsive

修改 `AgentWorkbenchWindow*.cs` 或对应 `setup/assets/*.template` 前后，保持每个页面在 Workbench 最小窗口和 Unity 停靠窄窗口中可完整访问。

## 不变量

- 页面只允许纵向滚动；普通正文、Markdown、表格、编辑器和列表不得制造水平滚动条。
- 横向组的子项不能依赖文字计算出的隐式最小宽度。可变数量或可变文案的等宽控件使用父级已分配 `Rect` 切分；不要用多个 `GUILayout.ExpandWidth(true)` 假设布局系统会压缩内容。
- 固定宽度总和、间距和容器 padding 必须小于所在区域的最小可用宽度。不能证明时，在阈值下换行、分行或切换为纵向主从布局。
- 长标题、路径、ID、翻译文本和无空格 token 都视为正常输入。正文使用可换行样式；单行状态可裁剪并通过 tooltip 或可选择详情保留完整值。
- 修改共享布局原语时检查其全部调用页面；修改单页时至少检查该页的列表空态、最长内容、编辑态和按钮最多态。
- 运行源码与 `setup/assets` 模板必须字节一致，避免当前项目修复而新安装再次回归。

## 验证

1. 搜索本次涉及页面内的 `HorizontalScope`、`GUILayout.Width`、`MinWidth`、`CalcSize` 与 ScrollView，逐组核算最小宽度。
2. 对可变标签使用中英文最长文案和三位数计数推演；对正文使用长路径、GUID 和连续英文 token 推演。
3. 编译 Unity Editor 程序集，并运行 zWorkFlow 包测试。
4. 比较运行源码与对应模板；若无法启动 Unity，明确标记实际窄窗口交互为未验证，不以静态检查冒充视觉验证。

## 分发边界

本 Skill 只记录 zWorkFlow 制作规范。它必须保留在维护仓库，并列入 `PACKAGE_MANIFEST.json.portablePackageExcludes`；CLI 复制测试必须证明移植包中不存在该目录。
