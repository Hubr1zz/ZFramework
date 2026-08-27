using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;

namespace ZFramework.RTS.Editor
{
    internal sealed class RtsControlCenter : EditorWindow
    {
        private static readonly string[] TAB_NAMES = { "Agent 工作流", "手动工具", "正式化", "简介与指南" };
        private int _selectedTab;
        private Vector2 _scroll;
        private Vector2 _diagnosticScroll;

        [MenuItem("ZFramework/RTS/Control Center", priority = -100)]
        internal static void Open()
        {
            var window = GetWindow<RtsControlCenter>("ZFramework RTS");
            window.minSize = new Vector2(500f, 480f);
            window.Show();
        }

        private void OnEnable() => RtsCompilationService.StateChanged += Repaint;
        private void OnDisable() => RtsCompilationService.StateChanged -= Repaint;

        private void OnGUI()
        {
            _selectedTab = GUILayout.Toolbar(_selectedTab, TAB_NAMES);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.Space(8f);
            switch (_selectedTab)
            {
                case 0: DrawAgentWorkflow(); break;
                case 1: DrawManualTools(); break;
                case 2: DrawProduction(); break;
                default: DrawGuide(); break;
            }
            EditorGUILayout.EndScrollView();
        }

        private static void DrawAgentWorkflow()
        {
            EditorGUILayout.LabelField("Agent-First RTS", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "选择 Session → Agent 先分析正式基线与可复用实现 → 只修改当前 Session/Sources → watcher 编译并原子换代 → 从运行状态与结构化报告验证。",
                MessageType.Info);
            DrawSessionField();
            RtsSessionInfo active = RtsSessionCatalog.Active;
            if (active != null)
            {
                RtsSessionLaunchProfile profile;
                string entryScriptId;
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                {
                    profile = (RtsSessionLaunchProfile)EditorGUILayout.EnumPopup("运行配置", active.Descriptor.launchProfile);
                    entryScriptId = EditorGUILayout.DelayedTextField("入口 ScriptId", active.Descriptor.entryScriptId ?? string.Empty);
                }
                bool entryChanged = !string.Equals(entryScriptId, active.Descriptor.entryScriptId, StringComparison.Ordinal);
                if (entryChanged && !RtsSessionCatalog.TryValidateScriptId(entryScriptId, out string scriptIdError))
                    EditorGUILayout.HelpBox(scriptIdError, MessageType.Error);
                else if (profile != active.Descriptor.launchProfile || entryChanged)
                {
                    active.Descriptor.launchProfile = profile;
                    active.Descriptor.entryScriptId = entryScriptId.Trim();
                    RtsSessionCatalog.Save(active);
                    RtsWorkspaceManifest.Write();
                }
                EditorGUILayout.HelpBox("入口 ID 不是任意标签：必须与入口 IScript 的 [ScriptId(\"...\")] 完全一致，并且在当前 Session 依赖闭包中唯一。", MessageType.None);
                EditorGUILayout.LabelField("Session 源码", Path.GetRelativePath(RtsProjectSettings.instance.ProjectRoot, active.SourceRoot));
                EditorGUILayout.LabelField("正式基线", string.IsNullOrWhiteSpace(active.Descriptor.baseRevision) ? "未记录" : active.Descriptor.baseRevision);
                if (active.Descriptor.launchProfile == RtsSessionLaunchProfile.InContext)
                    EditorGUILayout.LabelField("激活点", $"{active.Descriptor.activationProcedure} / {active.Descriptor.activationScene}");
                DescribeButton("启动当前 Session", "Sandbox 进入固定 RTSTest；InContext 走正式主场景与 Procedure 后挂载增量。",
                    RtsTestFlow.StartActiveSession, EditorApplication.isPlayingOrWillChangePlaymode || RtsCompilationService.IsCompiling);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("打开源码目录")) EditorUtility.RevealInFinder(active.SourceRoot);
                    if (GUILayout.Button("查看复用分析")) InternalEditorUtility.OpenFileAtLineExternal(active.ReuseAnalysisPath, 1);
                }
            }
            bool autoReload = EditorGUILayout.ToggleLeft("保存外部源码后自动编译并热替换", RtsScriptWatcher.IsEnabled);
            if (autoReload != RtsScriptWatcher.IsEnabled) RtsScriptWatcher.SetEnabled(autoReload);
            EditorGUILayout.LabelField("Workspace", RtsWorkspaceManifest.PathName);
            EditorGUILayout.LabelField("运行状态", RtsRuntimeStatus.RelativePath);
            EditorGUILayout.LabelField("有效源码根", string.Join(", ", RtsProjectSettings.instance.ResolveSourceRoots()));
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("刷新 Agent Manifest")) RtsWorkspaceManifest.Write();
                if (GUILayout.Button("创建示例验证队列")) RtsAgentTaskRunner.CreateExample();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(RtsAgentTaskRunner.IsRunning))
                    if (GUILayout.Button("运行验证队列")) RtsAgentTaskRunner.Run();
                using (new EditorGUI.DisabledScope(!RtsAgentTaskRunner.IsRunning))
                    if (GUILayout.Button("停止队列")) RtsAgentTaskRunner.Cancel();
            }
            DrawRuntimeSummary();
            int unresolved = RtsDummySandbox.FindUnresolved().Count;
            if (unresolved > 0)
                EditorGUILayout.HelpBox($"{unresolved} 个稳定资产键尚未映射正式资产；热验证可继续，但正式化会被阻止。", MessageType.Warning);
        }

        private void DrawManualTools()
        {
            EditorGUILayout.LabelField("启动与恢复", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("这些是显式操作，不属于 Agent 默认热更新循环。RTSTest 是工具维护的固定开发场景。", MessageType.None);
            RtsProjectSettings settings = RtsProjectSettings.instance;
            string[] units = settings.CompileUnits.Select(unit => string.IsNullOrWhiteSpace(unit.name) ? "Unnamed" : unit.name).ToArray();
            int unit = EditorGUILayout.Popup("编译单元", settings.ActiveCompileUnit, units);
            if (unit != settings.ActiveCompileUnit)
            {
                settings.ActiveCompileUnit = unit;
                RtsScriptWatcher.RefreshWatchers();
            }
            RtsLaunchTarget target = (RtsLaunchTarget)EditorGUILayout.EnumPopup("启动目标", settings.LaunchTarget);
            if (target != settings.LaunchTarget) settings.LaunchTarget = target;
            DescribeButton("启动所选目标", "保存当前场景，编译外部 RTS 源码，并进入所选的正常流程或 RTSTest。", StartSelectedTarget,
                EditorApplication.isPlayingOrWillChangePlaymode || RtsCompilationService.IsCompiling);
            DescribeButton("重建 RTSTest", "只在测试场景损坏时重建固定 Bootstrap；会覆盖 RTSTest，不用于日常玩法迭代。", RtsTestFlow.RecreateTestScene,
                EditorApplication.isPlayingOrWillChangePlaymode);
            EditorGUILayout.LabelField("固定路径", RtsTestFlow.TestScenePath);
            EditorGUILayout.LabelField("入口 ScriptId", settings.RtsTestEntryScriptId);

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("源码与诊断", EditorStyles.boldLabel);
            DescribeButton("创建 Session", "在 RTSWorkspace/Sessions 下创建隔离的源码、资产映射、任务和运行状态。", RtsSessionWizard.Open);
            DescribeButton("创建 Data / Adaptor / View 骨架", "在当前 Session/Sources 下生成纯 Data、双端 Adaptor 与 Unity View。", RtsGameplayWizard.Open);
            DescribeButton("打开稳定资产映射", "维护逻辑资产键到 RTS dummy/正式 Prefab 的环境映射。", RtsDummySandbox.OpenMapping);
            DescribeButton("项目设置", "编辑编译单元、源码根、主场景、session 和 Agent 边界。", () => SettingsService.OpenProjectSettings("Project/ZFramework RTS"));
            DrawCompilationSection();

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("运行时显式操作", EditorStyles.boldLabel);
            DescribeButton("装载已编译 DLL", "选择一个外部程序集并尝试原子换代；失败时保留健康代。", ScriptAssemblyLoader.LoadCompiledAssembly,
                !EditorApplication.isPlaying);
            DescribeButton("恢复最后健康代", "重新应用内存中的最后健康 Provider。", () => ScriptAssemblyLoader.RestoreHealthyGeneration(),
                !EditorApplication.isPlaying || string.IsNullOrEmpty(ScriptAssemblyLoader.LastHealthyGeneration));
            DescribeButton("重启当前玩法场景", "显式恢复动作，会重置场景；不应放入默认 Agent 队列。", ScriptAssemblyLoader.RequestRestartCurrentScene,
                !EditorApplication.isPlaying);
            DescribeButton("场景重启压力测试 ×10", "检查 scope 清理、重复对象和重启稳定性。", ScriptAssemblyLoader.StressRestartCurrentScene,
                !EditorApplication.isPlaying);
        }

        private static void DrawProduction()
        {
            EditorGUILayout.LabelField("增量正式化", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "输出只是可接入已有项目的增量模块。不会生成 Bootstrap Prefab、不会挂载组件、不会修改现有场景；现有启动流程负责实例化和释放。",
                MessageType.Info);
            DrawSessionField();
            EditorGUILayout.LabelField("下一导出", RtsSourcePromotion.GetNextTargetAssetPath());
            EditorGUILayout.HelpBox("只归档当前 Session 的旧 Export；其他 Session 的最新 Export 会继续参与正式编译。", MessageType.None);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || RtsCompilationService.IsCompiling))
            {
                DescribeButton("Dry Run", "预览转换、阻塞项、稳定资产映射和目标版本，不改动 Assets。", RtsSourcePromotion.ShowPreview);
                DescribeButton("确认并导出", "备份后写入新的 session/export；不改场景和启动流程。", RtsSourcePromotion.ExportWithConfirmation);
                DescribeButton("恢复上次导出", "恢复最近一次正式化前的 Generated 状态。", RtsSourcePromotion.RestoreLatestBackup);
                DescribeButton("验证 Zero-RTS Player", "验证 Player 程序集、启用的 Build Scene 依赖与活动正式源码不含 RTS。", RtsZeroBuildGuard.ValidateFromControlCenter);
            }
        }

        private static void DrawGuide()
        {
            EditorGUILayout.LabelField("RTS 是什么", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "RTS 是 Agent 驱动的玩法实验层：Unity 只编译稳定宿主，规则与状态放在 AssetDatabase 外由 Roslyn 热换。确认里程碑后，再把同一纯 C# Data 与薄正式 Adaptor/View 作为增量源码导出。",
                MessageType.Info);
            EditorGUILayout.LabelField("推荐结构", "Session → Data（规则/状态） → 双端 Adaptor → View（Unity 表现/资产）");
            EditorGUILayout.LabelField("稳定资产键", "与路径/GUID 无关的语义 ID，例如 enemy.healer；两端分别映射表现资产。");
            EditorGUILayout.LabelField("RTSTest", RtsTestFlow.TestScenePath);
            EditorGUILayout.HelpBox("RTSTest 不在 Assets/Scenes，而在 YooAsset 原始资源目录 Assets/AssetRaw/Scenes。它是固定开发场景，不进入正式 Build Scene 即可。", MessageType.None);
            DescribeButton("打开简介", "查看包定位、边界和主要入口。", () => OpenDocumentation("README.md"));
            DescribeButton("打开上手指南", "查看 Agent 循环、三层设计、session 正式化和接入方式。", () => OpenDocumentation("Documentation~/RTS-QUICKSTART.md"));
        }

        private void DrawCompilationSection()
        {
            string state = RtsCompilationService.IsCompiling
                ? (RtsCompilationService.HasPendingCompile ? "编译中（已有更新排队）" : "编译中")
                : "空闲";
            EditorGUILayout.LabelField("编译状态", state);
            RtsCompileResult result = RtsCompilationService.LastResult;
            if (result.ElapsedMilliseconds > 0d) EditorGUILayout.LabelField("最近耗时", $"{result.ElapsedMilliseconds:F0} ms");
            if (RtsCompilationService.P95Milliseconds > 0d) EditorGUILayout.LabelField("最近 P95", $"{RtsCompilationService.P95Milliseconds:F0} ms");
            DescribeButton("立即编译/热替换", "手动触发一次编译；Play Mode 中成功后应用新代。", ScriptAssemblyLoader.RequestCompileAndReload);
            if (!string.IsNullOrWhiteSpace(result.Diagnostics))
            {
                _diagnosticScroll = EditorGUILayout.BeginScrollView(_diagnosticScroll, GUILayout.MinHeight(90f));
                EditorGUILayout.TextArea(result.Diagnostics, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
                foreach (Match match in Regex.Matches(result.Diagnostics, @"(?m)^(.*?\.cs)\((\d+),(\d+)\)"))
                {
                    string file = match.Groups[1].Value.Trim();
                    if (GUILayout.Button($"打开 {Path.GetFileName(file)}:{match.Groups[2].Value}", EditorStyles.linkLabel))
                        InternalEditorUtility.OpenFileAtLineExternal(file, int.Parse(match.Groups[2].Value));
                }
            }
        }

        private static void DrawRuntimeSummary()
        {
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("运行观察", EditorStyles.boldLabel);
            string generation = "未运行";
            string instances = "-";
            if (EditorApplication.isPlaying)
            {
                try
                {
                    IScriptRuntimeModule runtime = ModuleSystem.GetModule<IScriptRuntimeModule>();
                    generation = string.IsNullOrEmpty(runtime.ActiveGeneration) ? "等待 Provider" : runtime.ActiveGeneration;
                    instances = runtime.ActiveInstanceCount.ToString();
                }
                catch (Exception) { generation = "等待 ZFramework 初始化"; }
            }
            EditorGUILayout.LabelField("健康代", generation);
            EditorGUILayout.LabelField("活动实例", instances);
            EditorGUILayout.LabelField("已加载动态代", ScriptAssemblyLoader.LoadedGenerationCount.ToString());
            EditorGUILayout.LabelField("估算动态内存", EditorUtility.FormatBytes(ScriptAssemblyLoader.LoadedAssemblyBytes));
            if (ScriptAssemblyLoader.LoadedGenerationCount >= 20 || ScriptAssemblyLoader.LoadedAssemblyBytes >= 64L * 1024L * 1024L)
                EditorGUILayout.HelpBox("Mono 无法卸载旧动态程序集；安排一次低频维护性 Play/Domain Reload 回收内存。", MessageType.Warning);
        }

        private static void DrawSessionField()
        {
            RtsProjectSettings settings = RtsProjectSettings.instance;
            IReadOnlyList<RtsSessionInfo> sessions = RtsSessionCatalog.ReadAll();
            if (sessions.Count == 0)
            {
                EditorGUILayout.HelpBox("尚未创建 RTS Session。", MessageType.Warning);
                if (GUILayout.Button("创建 Session")) RtsSessionWizard.Open();
                return;
            }
            int current = Math.Max(0, sessions.ToList().FindIndex(session =>
                session.Id.Equals(settings.ActiveSessionId, StringComparison.OrdinalIgnoreCase)));
            string[] names = sessions.Select(session => $"{session.DisplayName} ({session.Id})").ToArray();
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || RtsCompilationService.IsCompiling))
            {
                int selected = EditorGUILayout.Popup("当前 Session", current, names);
                if (selected != current) RtsSessionCatalog.Select(sessions[selected].Id);
                if (GUILayout.Button("新建 Session", GUILayout.Width(120f))) RtsSessionWizard.Open();
            }
        }

        private static void DescribeButton(string label, string description, Action action, bool disabled = false)
        {
            using (new EditorGUI.DisabledScope(disabled))
                if (GUILayout.Button(label, GUILayout.Height(25f))) action();
            EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(3f);
        }

        private static void OpenDocumentation(string relativePath)
        {
            string absolute = Path.Combine(RtsProjectSettings.instance.ProjectRoot, "Packages/com.zframework.rts", relativePath);
            InternalEditorUtility.OpenFileAtLineExternal(absolute, 1);
        }

        internal static void StartSelectedTarget()
        {
            if (RtsProjectSettings.instance.LaunchTarget == RtsLaunchTarget.RtsTest)
            {
                RtsTestFlow.StartTestFlow();
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string mainScene = RtsProjectSettings.instance.MainScene;
            if (string.IsNullOrWhiteSpace(mainScene))
            {
                Log.Error("[RTS] No main scene is configured for the normal launch target.");
                return;
            }

            EditorSceneManager.OpenScene(mainScene, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }
    }

    [InitializeOnLoad]
    internal static class RtsResourceInspectorExtension
    {
        static RtsResourceInspectorExtension() => UnityEditor.Editor.finishedDefaultHeaderGUI += DrawHeader;

        private static void DrawHeader(UnityEditor.Editor editor)
        {
            if (!(editor.target is ResourceModuleDriver)) return;
            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("ZFramework RTS", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Agent 工作流、手动恢复与正式化入口已统一到 Control Center。", EditorStyles.wordWrappedMiniLabel);
                if (GUILayout.Button("打开 RTS Control Center")) RtsControlCenter.Open();
            }
        }
    }
}
