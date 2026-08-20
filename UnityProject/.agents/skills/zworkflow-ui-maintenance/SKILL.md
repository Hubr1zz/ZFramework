---
name: zworkflow-ui-maintenance
description: 优化、扩展或重构本项目 zWorkFlow Unity Workbench UI 时使用；约束所有页面在最小窗口和嵌套分栏中不产生非预期横向溢出，并执行布局预算验证。不要用于普通游戏 UI。
---

# zWorkFlow UI Maintenance

本 skill 只约束当前项目中的 zWorkFlow Workbench 改动。开始修改前先读取目标页面及其共享渲染方法，完成后运行覆盖全部 Workbench 页面与可移植模板的布局审计，再做 Unity MCP 编译验证。

## 布局规则

1. 嵌套面板的内容宽度必须来自该面板的本地布局预算，不得直接把 `position.width` 或 `EditorGUIUtility.currentViewWidth` 当作内容宽度。
2. 分栏先扣除固定栏、间距、样式边距和滚动条预算，再把剩余宽度交给右栏；结果最小只能钳制到可布局的正数，不能用会反向撑大父容器的视觉最小宽度。
3. 固定宽度控件必须有明确预算或窄窗口降级方案。正文、标签和帮助文本启用换行；普通页面只使用纵向滚动。横向滚动仅允许用于明确需要保持原始行宽的代码或原始数据视图。
4. Markdown 表格、图片、编辑器和代码块的最大宽度不得超过调用方提供的本地内容宽度。表格列数增加时压缩单元格并换行，不得以单元格最小宽度撑破父容器。
5. 新增或修改任一页面后，至少验证 Workbench 的 `900x600` 最小窗口和一个常用较大尺寸。布局审计必须扫描全部 `AgentWorkbenchWindow*.cs` 与对应模板，禁止新增全局视图宽度和“窗口宽度 + 视觉最小值”式嵌套计算。优先使用数据化布局审计与 Unity MCP，不以截图作为主要验证手段。

## 验证

在 `UnityProject` 根目录运行：

```powershell
pwsh -NoProfile -File .agents/skills/zworkflow-ui-maintenance/scripts/test-layout.ps1
```

随后通过 Unity MCP 刷新资源、等待编译完成并检查 Error 日志。若修改了 `zWorkFlow/setup/assets` 中的可移植模板，还要同步验证模板与实际 Editor 文件；本项目专用规则本身禁止进入模板或移植包。

## 分发边界

- 保持本 skill 仅位于 `.agents/skills/zworkflow-ui-maintenance`。
- 不要在 `zWorkFlow/.agents/skills` 创建镜像。
- 不要把该目录或名称加入 `zWorkFlow/setup/PACKAGE_MANIFEST.json`。
