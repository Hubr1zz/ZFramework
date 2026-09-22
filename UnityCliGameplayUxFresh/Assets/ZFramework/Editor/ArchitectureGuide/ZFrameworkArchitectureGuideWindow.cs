using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ZFramework.Editor
{
    public sealed class ZFrameworkArchitectureGuideWindow : EditorWindow
    {
        private const string StyleSheetPath = "Assets/ZFramework/Editor/ArchitectureGuide/ZFrameworkArchitectureGuideWindow.uss";

        private readonly Dictionary<GuidePage, Button> pageButtons = new Dictionary<GuidePage, Button>();
        private readonly Dictionary<int, Button> moduleTabButtons = new Dictionary<int, Button>();
        private VisualElement pageContent;
        private VisualElement moduleDetailContent;
        private Label pageTitle;
        private Label pageSubtitle;
        private GuidePage currentPage;
        private int currentModuleIndex;

        [MenuItem("ZFramework/Architecture Guide", false, -50)]
        public static void Open()
        {
            GetWindow<ZFrameworkArchitectureGuideWindow>("ZFramework Architecture");
        }

        public void CreateGUI()
        {
            minSize = new Vector2(920f, 560f);
            rootVisualElement.Clear();
            pageButtons.Clear();
            moduleTabButtons.Clear();

            StyleSheet styleSheet = LoadStyleSheet();
            if (styleSheet != null)
                rootVisualElement.styleSheets.Add(styleSheet);

            rootVisualElement.AddToClassList("zag-root");
            var shell = new TwoPaneSplitView(0, 238f, TwoPaneSplitViewOrientation.Horizontal);
            shell.AddToClassList("zag-shell");
            shell.Add(BuildNavigation());
            shell.Add(BuildWorkspace());
            rootVisualElement.Add(shell);

            ShowPage(currentPage);
        }

        private static StyleSheet LoadStyleSheet()
        {
            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StyleSheetPath);
            if (styleSheet != null)
                return styleSheet;

            string[] candidates = AssetDatabase.FindAssets("ZFrameworkArchitectureGuideWindow t:StyleSheet");
            if (candidates.Length == 0)
                return null;

            return AssetDatabase.LoadAssetAtPath<StyleSheet>(AssetDatabase.GUIDToAssetPath(candidates[0]));
        }

        private VisualElement BuildNavigation()
        {
            var navigation = new VisualElement();
            navigation.AddToClassList("zag-navigation");

            var brand = new VisualElement();
            brand.AddToClassList("zag-brand");
            var icon = new Label("ZF");
            icon.AddToClassList("zag-brand-icon");
            brand.Add(icon);
            var copy = new VisualElement();
            var title = new Label("ZFRAMEWORK");
            title.AddToClassList("zag-brand-title");
            var subtitle = new Label("Architecture Guide");
            subtitle.AddToClassList("zag-brand-subtitle");
            copy.Add(title);
            copy.Add(subtitle);
            brand.Add(copy);
            navigation.Add(brand);

            var menu = new ScrollView();
            menu.AddToClassList("zag-menu");
            AddNavigationSection(menu, "理解架构");
            AddNavigationItem(menu, GuidePage.Overview, "▦", "架构总览", "三类能力与运行关系");
            AddNavigationItem(menu, GuidePage.ModuleMap, "⌘", "初始自带模块", "点选模块查看用法");
            AddNavigationSection(menu, "做出决策");
            AddNavigationItem(menu, GuidePage.DecisionGuide, "?", "功能归类", "用五问选择放置位置");
            navigation.Add(menu);

            var footer = new VisualElement();
            footer.AddToClassList("zag-navigation-footer");
            var badge = new Label("EDITOR ONLY");
            badge.AddToClassList("zag-status-pill");
            footer.Add(badge);
            var detail = new Label("本窗口只解释架构，不参与运行时生命周期。");
            detail.AddToClassList("zag-footer-detail");
            footer.Add(detail);
            navigation.Add(footer);
            return navigation;
        }

        private VisualElement BuildWorkspace()
        {
            var workspace = new VisualElement();
            workspace.AddToClassList("zag-workspace");

            var header = new VisualElement();
            header.AddToClassList("zag-page-header");
            var titles = new VisualElement();
            pageTitle = new Label();
            pageTitle.AddToClassList("zag-page-title");
            pageSubtitle = new Label();
            pageSubtitle.AddToClassList("zag-page-subtitle");
            titles.Add(pageTitle);
            titles.Add(pageSubtitle);
            header.Add(titles);
            var tag = new Label("ZFramework · based on TEngine");
            tag.AddToClassList("zag-header-badge");
            header.Add(tag);
            workspace.Add(header);

            pageContent = new VisualElement();
            pageContent.AddToClassList("zag-page-content");
            workspace.Add(pageContent);
            return workspace;
        }

        private void AddNavigationSection(VisualElement parent, string title)
        {
            var label = new Label(title);
            label.AddToClassList("zag-menu-section");
            parent.Add(label);
        }

        private void AddNavigationItem(VisualElement parent, GuidePage page, string icon, string title, string subtitle)
        {
            var button = new Button(() => ShowPage(page));
            button.AddToClassList("zag-menu-item");
            var iconLabel = new Label(icon);
            iconLabel.AddToClassList("zag-menu-icon");
            button.Add(iconLabel);
            var copy = new VisualElement();
            copy.AddToClassList("zag-menu-copy");
            var titleLabel = new Label(title);
            titleLabel.AddToClassList("zag-menu-title");
            var subtitleLabel = new Label(subtitle);
            subtitleLabel.AddToClassList("zag-menu-subtitle");
            copy.Add(titleLabel);
            copy.Add(subtitleLabel);
            button.Add(copy);
            parent.Add(button);
            pageButtons[page] = button;
        }

        private void ShowPage(GuidePage page)
        {
            currentPage = page;
            foreach (KeyValuePair<GuidePage, Button> pair in pageButtons)
                pair.Value.EnableInClassList("is-selected", pair.Key == page);

            pageContent.Clear();
            switch (page)
            {
                case GuidePage.Overview:
                    SetPageHeading("架构总览", "先看三类能力分别解决什么问题，再看它们怎样一起运行");
                    pageContent.Add(BuildOverviewPage());
                    break;
                case GuidePage.ModuleMap:
                    SetPageHeading("初始自带模块", "点选名称，查看它解决什么问题、怎样使用以及会用到谁");
                    pageContent.Add(BuildModuleMapPage());
                    break;
                case GuidePage.DecisionGuide:
                    SetPageHeading("功能归类", "回答五个简单问题，判断新功能应该放进 Core、IModule 还是游戏业务单例");
                    pageContent.Add(BuildDecisionGuidePage());
                    break;
            }
        }

        private void SetPageHeading(string title, string subtitle)
        {
            pageTitle.text = title;
            pageSubtitle.text = subtitle;
        }

        private VisualElement BuildOverviewPage()
        {
            var scroll = CreatePageScroll();
            scroll.Add(CreateCallout("项目来源", "ZFramework 是基于开源 Unity 框架 TEngine 扩展的个人架构。本工具使用 ZFramework 的当前代码事实解释分层，同时保留 TEngine 的来源、版权与 MIT 许可证。", "info"));
            scroll.Add(CreateHero("一句话理解", "Core 基元提供最基础的能力；IModule 是框架统一管理的公共服务；游戏业务单例保存本次游戏中的业务状态。GameEntry 和 RootModule 负责启动并连接它们。"));

            scroll.Add(CreateSectionTitle("框架如何运行"));
            var flow = new VisualElement();
            flow.AddToClassList("zag-layer-flow");
            flow.Add(CreateLayer("UNITY 启动入口", "RootModule · GameEntry · Settings", "从 Prefab 读取配置，创建需要的能力，并把 Unity 的每帧更新和退出通知传给框架", "host"));
            flow.Add(CreateFlowArrow("创建并驱动"));
            flow.Add(CreateLayer("IModule 模块", "Resource · FSM · Procedure · Audio · Scene · Timer · Localization", "一份服务供全项目使用，由 ModuleSystem 按顺序启动、更新并在退出时清理", "module"));
            flow.Add(CreateFlowArrow("使用"));
            flow.Add(CreateLayer("Core 基元", "ModuleSystem · MemoryPool · GameEvent · GameTime · Log · Utility", "低层、稳定、通常无项目玩法内容", "core"));
            scroll.Add(flow);

            scroll.Add(CreateSectionTitle("三类能力分别放什么"));
            var grid = new VisualElement();
            grid.AddToClassList("zag-three-column");
            grid.Add(CreateCategoryCard("Core", "Core 基元", "不需要框架替它管理启动和关闭，通常是稳定的小工具、基础规则或底层机制。", new[]
            {
                "Module / ModuleSystem",
                "MemoryPool / GameEvent",
                "GameTime / RuntimeId",
                "Log / Utility"
            }, "例：MemoryPool 只在借出和归还时工作，不需要每帧更新，所以适合留在 Core。", "core"));
            grid.Add(CreateCategoryCard("IModule", "IModule 模块", "需要一份共享服务，并且要按固定顺序启动、更新或关闭，由 ModuleSystem 负责管理。", new[]
            {
                "IResourceModule",
                "IFsmModule / IProcedureModule",
                "IAudioModule / ISceneModule",
                "ITimerModule / ILocalizationModule"
            }, "例：资源模块要保存加载记录并在退出时释放资源，因此应该作为 IModule。", "module"));
            grid.Add(CreateCategoryCard("SingletonSystem", "游戏业务单例", "保存当前玩家、当前战斗或当前界面等业务状态，跟随一次游戏会话创建和释放。", new[]
            {
                "UI 根节点 / 输入管理器",
                "玩家会话 / 战斗管理器",
                "跨场景业务协调对象",
                "需要跨场景保留的 Unity 对象"
            }, "例：PlayerSession 知道玩家账号和存档，这些内容只属于当前游戏，不应放进框架 Runtime。", "singleton"));
            scroll.Add(grid);

            scroll.Add(CreateSectionTitle("配置和访问入口放在哪里"));
            var adjacent = new VisualElement();
            adjacent.AddToClassList("zag-card-row");
            adjacent.Add(CreateInfoCard("Settings", "它保存 Unity Inspector 中可编辑的启动配置，并把 AudioSetting、ProcedureSetting、UpdateSetting 交给启动流程。脚本留在 Runtime/Module/Settings，配置资产和 GameEntry Prefab 放在 Assets/ZFramework/Settings。它本身不是 IModule。", "启动配置"));
            adjacent.Add(CreateInfoCard("ConfigSystem", "它认识本项目的 Luban 表结构，并借助 Resource 模块读取数据，因此属于项目接入代码。当前实现应放在 GameScripts/GameLogic/Config，而不是 ZFramework 的通用模块目录。", "项目代码"));
            adjacent.Add(CreateInfoCard("GameModule 或项目统一入口", "业务代码可以通过一个统一入口取得 Resource、Audio 等模块，减少到处查询。这个入口只负责访问，不负责创建或销毁模块。", "访问入口"));
            scroll.Add(adjacent);

            scroll.Add(CreateCallout("不要只看名字和文件夹", "RootModule 虽然名字里有 Module，但它是启动入口；GameEvent 虽然不继承 Module，却是 Core 基元；Settings 放在 Module 目录中，也仍然只是启动配置。应该看它做什么、由谁启动和清理。", "warning"));
            scroll.Add(CreateSourceActions());
            return scroll;
        }

        private VisualElement BuildModuleMapPage()
        {
            var scroll = CreatePageScroll();
            scroll.Add(CreateCallout("模块可以互相使用", "模块不需要彼此完全隔离。箭头表示左边会使用右边提供的能力；只要方向清楚、不互相绕成一圈，就容易理解和维护。", "info"));

            scroll.Add(CreateSectionTitle("模块之间怎么配合"));
            var dependencyGraph = new VisualElement();
            dependencyGraph.AddToClassList("zag-dependency-graph");
            dependencyGraph.Add(CreateDependencyNode("Procedure", "使用 FSM 表示当前流程"));
            dependencyGraph.Add(CreateDependencyArrow());
            dependencyGraph.Add(CreateDependencyNode("FSM", "管理状态和状态切换"));
            dependencyGraph.Add(CreateDependencySpacer());
            dependencyGraph.Add(CreateDependencyNode("Audio / Scene / Localization", "需要读取声音、场景或语言资源"));
            dependencyGraph.Add(CreateDependencyArrow());
            dependencyGraph.Add(CreateDependencyNode("Resource", "统一加载和释放资源"));
            dependencyGraph.Add(CreateDependencyArrow());
            dependencyGraph.Add(CreateDependencyNode("ObjectPool", "复用较完整的运行对象，并使用 MemoryPool"));
            scroll.Add(dependencyGraph);

            scroll.Add(CreateSectionTitle("初始自带模块"));
            scroll.Add(CreateCallout("这个目录也列出两项 Core 和一项配置", "MemoryPool、GameEvent 会被多个模块使用，因此一并放在这里讲解，但它们是 Core 基元；Settings 是启动配置。每个页签的类型标签会明确说明。", "success"));
            scroll.Add(BuildModuleTabs());
            moduleDetailContent = new VisualElement();
            moduleDetailContent.AddToClassList("zag-module-detail-host");
            scroll.Add(moduleDetailContent);
            ShowModuleDetail(currentModuleIndex);
            scroll.Add(CreateCallout("当前没有列入 UI 和 ConfigSystem", "当前源码中没有可作为初始 IModule 展示的 UI 模块；ConfigSystem 认识具体游戏的 Luban 表结构，应该放在 GameScripts。以后加入真正通用、需要统一启动和关闭的 UI 服务时，再把它注册为 IModule。", "warning"));
            return scroll;
        }

        private VisualElement BuildDecisionGuidePage()
        {
            var scroll = CreatePageScroll();
            scroll.Add(CreateHero("五问判断法", "不要先问它叫不叫 System 或 Manager。先看它保存什么、谁会使用，以及要不要跟随框架一起启动和关闭。"));

            var questions = new[]
            {
                new DecisionQuestion("01", "大家是否必须使用同一份状态？", "例如所有界面和流程都应看到同一批已加载资源。"),
                new DecisionQuestion("02", "它是否会长期保存内容，并且退出时需要清理？", "例如资源句柄、缓存、计时任务、声音实例或跨帧队列。"),
                new DecisionQuestion("03", "它是否必须跟随框架一起启动、每帧运行或关闭？", "如果少一次更新或清理就会出错，通常需要由 ModuleSystem 管理。"),
                new DecisionQuestion("04", "不同玩法都会使用它吗？", "去掉当前玩家、关卡和玩法后，它是否仍能在另一个项目中复用？"),
                new DecisionQuestion("05", "内部做法是否足够复杂，调用者只需要一个简单接口？", "如果接口只是原样转发一次调用，就没有必要为了形式增加一层。")
            };
            foreach (DecisionQuestion question in questions)
                scroll.Add(CreateQuestionRow(question));

            scroll.Add(CreateSectionTitle("根据答案落位"));
            var outcomes = new VisualElement();
            outcomes.AddToClassList("zag-outcome-row");
            outcomes.Add(CreateOutcome("多数为否，而且不认识玩法", "Core 基元", "做成简单、稳定的基础能力；不必为了统一外观强行增加模块接口。", "core"));
            outcomes.Add(CreateOutcome("前四项多数为是", "IModule 模块", "注册进 ModuleSystem，写清楚它会使用哪些模块，并在关闭时释放自己保存的内容。", "module"));
            outcomes.Add(CreateOutcome("认识当前玩家、战斗或界面", "游戏业务单例", "放进 GameScripts，由 SingletonSystem 或清晰的业务入口负责创建和释放。", "singleton"));
            scroll.Add(outcomes);
            scroll.Add(CreateCallout("快速检查", "试着把这层删掉：如果每个调用者都被迫重复处理加载、缓存、更新或清理，它值得成为模块；如果删掉后代码反而更直接，就先保持简单。", "info"));
            return scroll;
        }

        private static ScrollView CreatePageScroll()
        {
            var scroll = new ScrollView();
            scroll.AddToClassList("zag-scroll");
            return scroll;
        }

        private static VisualElement CreateHero(string title, string body)
        {
            var hero = new VisualElement();
            hero.AddToClassList("zag-hero");
            var eyebrow = new Label("快速理解");
            eyebrow.AddToClassList("zag-eyebrow");
            var titleLabel = new Label(title);
            titleLabel.AddToClassList("zag-hero-title");
            var bodyLabel = new Label(body);
            bodyLabel.AddToClassList("zag-hero-body");
            hero.Add(eyebrow);
            hero.Add(titleLabel);
            hero.Add(bodyLabel);
            return hero;
        }

        private static VisualElement CreateLayer(string title, string examples, string body, string kind)
        {
            var layer = new VisualElement();
            layer.AddToClassList("zag-layer");
            layer.AddToClassList("is-" + kind);
            var titleLabel = new Label(title);
            titleLabel.AddToClassList("zag-layer-title");
            var examplesLabel = new Label(examples);
            examplesLabel.AddToClassList("zag-layer-examples");
            var bodyLabel = new Label(body);
            bodyLabel.AddToClassList("zag-layer-body");
            layer.Add(titleLabel);
            layer.Add(examplesLabel);
            layer.Add(bodyLabel);
            return layer;
        }

        private static VisualElement CreateFlowArrow(string caption)
        {
            var arrow = new VisualElement();
            arrow.AddToClassList("zag-flow-arrow");
            arrow.Add(new Label("▼"));
            arrow.Add(new Label(caption));
            return arrow;
        }

        private static VisualElement CreateCategoryCard(string code, string title, string body, string[] examples, string note, string kind)
        {
            var card = new VisualElement();
            card.AddToClassList("zag-category-card");
            card.AddToClassList("is-" + kind);
            var codeLabel = new Label(code);
            codeLabel.AddToClassList("zag-category-code");
            var titleLabel = new Label(title);
            titleLabel.AddToClassList("zag-category-title");
            var bodyLabel = new Label(body);
            bodyLabel.AddToClassList("zag-category-body");
            card.Add(codeLabel);
            card.Add(titleLabel);
            card.Add(bodyLabel);
            foreach (string example in examples)
            {
                var exampleLabel = new Label("• " + example);
                exampleLabel.AddToClassList("zag-example");
                card.Add(exampleLabel);
            }
            var noteLabel = new Label(note);
            noteLabel.AddToClassList("zag-category-note");
            card.Add(noteLabel);
            return card;
        }

        private static VisualElement CreateInfoCard(string title, string body, string badge)
        {
            var card = new VisualElement();
            card.AddToClassList("zag-info-card");
            var badgeLabel = new Label(badge);
            badgeLabel.AddToClassList("zag-small-badge");
            var titleLabel = new Label(title);
            titleLabel.AddToClassList("zag-card-title");
            var bodyLabel = new Label(body);
            bodyLabel.AddToClassList("zag-card-body");
            card.Add(badgeLabel);
            card.Add(titleLabel);
            card.Add(bodyLabel);
            return card;
        }

        private static VisualElement CreateCallout(string title, string body, string kind)
        {
            var callout = new VisualElement();
            callout.AddToClassList("zag-callout");
            callout.AddToClassList("is-" + kind);
            var titleLabel = new Label(title);
            titleLabel.AddToClassList("zag-callout-title");
            var bodyLabel = new Label(body);
            bodyLabel.AddToClassList("zag-callout-body");
            callout.Add(titleLabel);
            callout.Add(bodyLabel);
            return callout;
        }

        private static Label CreateSectionTitle(string title)
        {
            var label = new Label(title);
            label.AddToClassList("zag-section-title");
            return label;
        }

        private static VisualElement CreateDependencyNode(string title, string subtitle)
        {
            var node = new VisualElement();
            node.AddToClassList("zag-dependency-node");
            var titleLabel = new Label(title);
            titleLabel.AddToClassList("zag-dependency-title");
            var subtitleLabel = new Label(subtitle);
            subtitleLabel.AddToClassList("zag-dependency-subtitle");
            node.Add(titleLabel);
            node.Add(subtitleLabel);
            return node;
        }

        private static Label CreateDependencyArrow()
        {
            var label = new Label("→");
            label.AddToClassList("zag-dependency-arrow");
            return label;
        }

        private static VisualElement CreateDependencySpacer()
        {
            var spacer = new VisualElement();
            spacer.AddToClassList("zag-dependency-spacer");
            return spacer;
        }

        private VisualElement BuildModuleTabs()
        {
            moduleTabButtons.Clear();
            var tabs = new VisualElement();
            tabs.AddToClassList("zag-module-tabs");
            ModuleGuideEntry[] entries = GetModuleGuideEntries();
            for (int i = 0; i < entries.Length; i++)
            {
                int index = i;
                var button = new Button(() => ShowModuleDetail(index)) { text = entries[i].Name };
                button.AddToClassList("zag-module-tab");
                tabs.Add(button);
                moduleTabButtons[index] = button;
            }
            return tabs;
        }

        private void ShowModuleDetail(int index)
        {
            ModuleGuideEntry[] entries = GetModuleGuideEntries();
            currentModuleIndex = Mathf.Clamp(index, 0, entries.Length - 1);
            foreach (KeyValuePair<int, Button> pair in moduleTabButtons)
                pair.Value.EnableInClassList("is-selected", pair.Key == currentModuleIndex);

            if (moduleDetailContent == null)
                return;

            ModuleGuideEntry entry = entries[currentModuleIndex];
            moduleDetailContent.Clear();

            var header = new VisualElement();
            header.AddToClassList("zag-module-detail-header");
            var heading = new VisualElement();
            heading.AddToClassList("zag-module-detail-heading");
            var kindLabel = new Label(entry.Kind);
            kindLabel.AddToClassList("zag-module-kind");
            var titleLabel = new Label(entry.Name);
            titleLabel.AddToClassList("zag-module-detail-title");
            var summaryLabel = new Label(entry.Summary);
            summaryLabel.AddToClassList("zag-module-detail-summary");
            heading.Add(kindLabel);
            heading.Add(titleLabel);
            heading.Add(summaryLabel);
            header.Add(heading);
            header.Add(CreatePingButton("查看源码", entry.SourcePath));
            moduleDetailContent.Add(header);

            moduleDetailContent.Add(CreateModuleDetailSection("什么时候用", entry.WhenToUse));

            var steps = new VisualElement();
            steps.AddToClassList("zag-module-detail-section");
            var stepsTitle = new Label("怎么用");
            stepsTitle.AddToClassList("zag-module-detail-section-title");
            steps.Add(stepsTitle);
            for (int i = 0; i < entry.Steps.Length; i++)
            {
                var row = new VisualElement();
                row.AddToClassList("zag-module-step");
                var number = new Label((i + 1).ToString());
                number.AddToClassList("zag-module-step-number");
                var body = new Label(entry.Steps[i]);
                body.AddToClassList("zag-module-step-body");
                row.Add(number);
                row.Add(body);
                steps.Add(row);
            }
            moduleDetailContent.Add(steps);

            moduleDetailContent.Add(CreateModuleDetailSection("简单例子", entry.Example, "is-example"));
            moduleDetailContent.Add(CreateModuleDetailSection("它会用到谁", entry.Relationship));
        }

        private static VisualElement CreateModuleDetailSection(string title, string body, string extraClass = null)
        {
            var section = new VisualElement();
            section.AddToClassList("zag-module-detail-section");
            if (!string.IsNullOrEmpty(extraClass))
                section.AddToClassList(extraClass);
            var titleLabel = new Label(title);
            titleLabel.AddToClassList("zag-module-detail-section-title");
            var bodyLabel = new Label(body);
            bodyLabel.AddToClassList("zag-module-detail-section-body");
            section.Add(titleLabel);
            section.Add(bodyLabel);
            return section;
        }

        private static ModuleGuideEntry[] GetModuleGuideEntries()
        {
            return new[]
            {
                new ModuleGuideEntry("Resource", "IModule 模块", "统一加载、缓存和释放资源，底层使用 YooAsset。", "需要读取 Prefab、贴图、音频、场景或配置文件时使用。", new[]
                {
                    "先通过项目统一入口取得 IResourceModule，不要在业务代码里自己创建 ResourceModule。",
                    "用资源地址发起同步或异步加载，并保存返回结果或已创建的实例。",
                    "不再使用时调用对应的释放方法；不要只销毁 GameObject 而忘记资源引用。"
                }, "打开角色界面时异步加载角色头像；界面关闭后释放头像资源，让资源模块统一处理引用次数。", "会使用 ObjectPool 来复用内部记录，并连接 YooAsset 完成实际加载。", "Assets/ZFramework/Runtime/Module/ResourceModule/IResourceModule.cs"),
                new ModuleGuideEntry("ObjectPool", "IModule 模块", "复用带名称、生成和回收状态的对象，适合比一条普通数据更完整的运行对象。", "子弹、特效、资源包装对象等会频繁创建和销毁，并且需要容量或过期清理时使用。", new[]
                {
                    "为对象定义 ObjectBase 包装类型，再通过 IObjectPoolModule 创建对应对象池。",
                    "把可复用对象注册进池；需要时 Spawn，结束使用时 Unspawn。",
                    "设置容量和自动释放时间，让模块逐步清掉长期闲置对象。"
                }, "爆炸特效播放结束后不直接销毁，而是归还特效池；下次爆炸再次取出并重置。", "内部会使用 MemoryPool 复用池的管理记录；Resource 也会使用它。", "Assets/ZFramework/Runtime/Module/ObjectPoolModule/IObjectPoolModule.cs"),
                new ModuleGuideEntry("MemoryPool", "Core 基元", "复用实现 IMemory 的短小纯 C# 对象，减少频繁 new 带来的垃圾回收。", "事件参数、临时命令、加载记录等对象创建很频繁，而且能在归还时完整重置时使用。", new[]
                {
                    "让可复用类型实现 IMemory，并在 Clear 中清空所有旧状态。",
                    "需要时调用 MemoryPool.Acquire<T>() 取得对象。",
                    "用完立即调用 MemoryPool.Release；归还后不要继续保存或读取这个对象。"
                }, "一次事件派发前借出 EventArgs，填入伤害值并发送；派发结束立刻归还。它不需要每帧检查，所以不是 IModule。", "不依赖其他模块；GameEvent 和 ObjectPool 会使用它。", "Assets/ZFramework/Runtime/Core/MemoryPool/MemoryPool.cs"),
                new ModuleGuideEntry("FSM", "IModule 模块", "保存当前状态，并负责从一个状态切换到另一个状态。", "一个对象同一时间只能处于少数明确状态，例如待机、移动、攻击时使用。", new[]
                {
                    "为每个状态编写 FsmState，并实现进入、更新和离开时要做的事。",
                    "通过 IFsmModule 创建状态机，传入状态拥有者和可用状态。",
                    "需要切换时调用 ChangeState；对象销毁时同时销毁对应状态机。"
                }, "敌人出生后进入 Idle，看到玩家切到 Chase，进入攻击距离后切到 Attack。", "本身是基础服务；Procedure 在它之上组织游戏的大流程。", "Assets/ZFramework/Runtime/Module/FsmModule/IFsmModule.cs"),
                new ModuleGuideEntry("Procedure", "IModule 模块", "管理游戏当前处于哪个大流程，例如启动、登录、大厅或战斗。", "多个大流程互斥，并且进入和离开时各有一组固定工作时使用。", new[]
                {
                    "为每个大流程编写 ProcedureBase 子类。",
                    "在 ProcedureSetting 中登记入口流程和可用流程。",
                    "启动后由 IProcedureModule 切换流程；把流程细节交给各自的 Procedure 类。"
                }, "游戏启动进入 Preload，资源准备完切到 Login，登录成功再切到 Lobby。", "使用 FSM 保存当前流程状态。", "Assets/ZFramework/Runtime/Module/ProcedureModule/IProcedureModule.cs"),
                new ModuleGuideEntry("Audio", "IModule 模块", "统一播放、暂停和停止音乐、音效与语音，并管理音量分类。", "任何需要播放声音并希望统一控制音量、循环和回收时使用。", new[]
                {
                    "在 AudioSetting 中配置声音分类和默认参数。",
                    "通过项目统一入口取得 IAudioModule，再用 Play 指定分类、资源地址和是否循环。",
                    "保存返回的 AudioAgent；需要暂停、停止或调整音量时操作它。"
                }, "进入大厅时循环播放背景音乐；切入战斗时停止大厅音乐并播放战斗音乐。", "使用 Resource 加载音频资源，并借助对象池复用播放代理。", "Assets/ZFramework/Runtime/Module/AudioModule/IAudioModule.cs"),
                new ModuleGuideEntry("Scene", "IModule 模块", "统一处理场景加载、卸载和场景切换后的资源整理。", "需要从登录场景切到大厅或战斗场景，并希望等待加载完成时使用。", new[]
                {
                    "通过项目统一入口取得 ISceneModule。",
                    "用场景地址发起异步加载，并等待完成后再创建该场景的业务对象。",
                    "离开场景时走模块的卸载流程，让资源清理发生在正确时机。"
                }, "点击开始战斗后显示加载界面，等待 Battle 场景完成，再关闭加载界面并创建战斗会话。", "使用 Resource 加载场景，并在卸载后请求清理不再使用的资源。", "Assets/ZFramework/Runtime/Module/SceneModule/ISceneModule.cs"),
                new ModuleGuideEntry("Localization", "IModule 模块", "读取语言表，根据当前语言替换文字，并在运行时切换语言。", "界面文字、图片或其他内容需要跟随玩家选择的语言变化时使用。", new[]
                {
                    "确认场景使用 Assets/ZFramework/Settings/Prefab/GameEntry.prefab；其中已经带有 LocalizationManager。把语言 CSV 配置为运行时可加载资源。",
                    "选中带 TextMeshProUGUI 的文字对象，点击 Add Component，添加 I2/Localization/I2 Localize。组件会自动识别 TextMeshPro UGUI。",
                    "在 I2 Localize 的 Terms 区域打开 Main 页签，选择或填写语言表里的键，例如 UI/Login/Start；确保该键在每种语言下都有文字。",
                    "运行游戏后通过 ILocalizationModule.SetLanguage 切换语言。所有启用的 I2 Localize 组件会自动刷新；新增语言时也要更新并重新导出 CSV。"
                }, "按钮原文是“开始游戏”，Main 词条填 UI/Common/Start。切换到 English 后，TextMeshProUGUI 自动显示“Start Game”。", "使用 Resource 读取语言 CSV；TextMeshPro 支持由 LocalizeTarget_TextMeshPro_UGUI 提供。", "Assets/ZFramework/Runtime/Module/LocalizationModule/ILocalizationModule.cs"),
                new ModuleGuideEntry("Timer", "IModule 模块", "集中管理延时、循环计时、暂停、恢复和移除。", "几秒后执行一次、固定间隔重复执行，或需要查询剩余时间时使用。", new[]
                {
                    "通过项目统一入口取得 ITimerModule。",
                    "调用 AddTimer，传入回调、间隔、是否循环以及是否忽略时间缩放。",
                    "保存返回的 timerId；对象提前销毁时用 RemoveTimer 取消，避免回调访问失效对象。"
                }, "技能进入冷却后添加 5 秒计时器；到时清除冷却标记。界面关闭时先移除该计时器。", "由 ModuleSystem 每帧更新，不需要 Resource。", "Assets/ZFramework/Runtime/Module/TimerModule/ITimerModule.cs"),
                new ModuleGuideEntry("Debugger", "IModule 模块", "在运行时显示日志和框架状态，帮助开发阶段快速排查问题。", "需要在真机或 Game 视图中查看日志、内存池和对象池状态时使用。", new[]
                {
                    "使用带 Debugger 子对象的 GameEntry Prefab，并在开发版本中启用它。",
                    "运行后打开调试界面，选择日志或状态页查看当前数据。",
                    "发布正式版本前按项目设置关闭入口，避免把开发信息暴露给玩家。"
                }, "真机出现资源未释放时，打开 ObjectPool 页面查看对象数量，再结合日志定位是谁没有归还。", "会读取 ObjectPool 等模块的状态，但不应改变它们的业务行为。", "Assets/ZFramework/Runtime/Module/DebugerModule/IDebuggerModule.cs"),
                new ModuleGuideEntry("UpdateDriver", "IModule 模块", "把 Unity 的 Update、FixedUpdate、LateUpdate、协程和退出通知提供给普通 C# 代码。", "非 MonoBehaviour 对象确实需要接收 Unity 帧事件或启动协程时使用。", new[]
                {
                    "通过项目统一入口取得 IUpdateDriver。",
                    "注册需要的 Update、FixedUpdate 或 LateUpdate 回调。",
                    "对象结束使用时移除同一个回调，避免重复执行和对象无法释放。"
                }, "一个纯 C# 相机控制器注册 LateUpdate，在所有移动完成后刷新相机；销毁控制器时解除注册。", "由 Unity 宿主转发帧事件，是其他普通 C# 对象接入 Unity 生命周期的桥梁。", "Assets/ZFramework/Runtime/Module/UpdataDriverModule/IUpdateDriver.cs"),
                new ModuleGuideEntry("GameEvent", "Core 基元", "让发送者通知多个接收者，而不需要知道它们是谁。", "一个结果需要通知多个彼此无直接关系的系统，例如金币变化同时刷新 UI 和提示时使用。", new[]
                {
                    "为事件准备稳定的 int 或 string 标识，并约定参数类型。",
                    "接收者启用时调用 GameEvent.AddEventListener 注册回调。",
                    "发送者调用 GameEvent.Send；接收者停用或销毁时必须 RemoveEventListener。"
                }, "背包增加金币后发送 CoinChanged；顶部栏刷新数字，任务系统同时检查收集目标。", "内部使用 MemoryPool 复用部分事件数据；它是静态 Core 能力，不由 ModuleSystem 管理。", "Assets/ZFramework/Runtime/Core/GameEvent/GameEvent.cs"),
                new ModuleGuideEntry("Settings", "启动配置（不是 IModule）", "把 Inspector 中选择的 Audio、Procedure 和 Update 配置交给启动流程。", "希望策划或开发者直接在 Unity 中选择启动参数和 ScriptableObject 配置时使用。", new[]
                {
                    "在 Assets/ZFramework/Settings 中创建或修改对应的配置资产。",
                    "在 GameEntry Prefab 的 Settings 组件上绑定 AudioSetting、ProcedureSetting 和 UpdateSetting。",
                    "启动时由 Settings 读取这些引用并初始化相关功能；不要把它当成 ModuleSystem 中的服务查询。"
                }, "修改 ProcedureSetting 的入口流程后，GameEntry 下次运行会从新的流程开始，不需要改 RootModule。", "它把 Unity 序列化资产交给 Audio、Procedure 和 UpdateDriver，但自己不继承 Module。", "Assets/ZFramework/Runtime/Module/Settings/Settings.cs")
            };
        }

        private static VisualElement CreateQuestionRow(DecisionQuestion question)
        {
            var row = new VisualElement();
            row.AddToClassList("zag-question-row");
            var number = new Label(question.Number);
            number.AddToClassList("zag-question-number");
            var copy = new VisualElement();
            var title = new Label(question.Title);
            title.AddToClassList("zag-question-title");
            var hint = new Label(question.Hint);
            hint.AddToClassList("zag-question-hint");
            copy.Add(title);
            copy.Add(hint);
            row.Add(number);
            row.Add(copy);
            return row;
        }

        private static VisualElement CreateOutcome(string condition, string title, string body, string kind)
        {
            var outcome = new VisualElement();
            outcome.AddToClassList("zag-outcome");
            outcome.AddToClassList("is-" + kind);
            var conditionLabel = new Label(condition);
            conditionLabel.AddToClassList("zag-outcome-condition");
            var titleLabel = new Label(title);
            titleLabel.AddToClassList("zag-outcome-title");
            var bodyLabel = new Label(body);
            bodyLabel.AddToClassList("zag-card-body");
            outcome.Add(conditionLabel);
            outcome.Add(titleLabel);
            outcome.Add(bodyLabel);
            return outcome;
        }

        private static VisualElement CreateSourceActions()
        {
            var actions = new VisualElement();
            actions.AddToClassList("zag-source-actions");
            actions.Add(new Label("从源码继续阅读"));
            actions.Add(CreatePingButton("ModuleSystem", "Assets/ZFramework/Runtime/Core/ModuleSystem.cs"));
            actions.Add(CreatePingButton("MemoryPool", "Assets/ZFramework/Runtime/Core/MemoryPool/MemoryPool.cs"));
            actions.Add(CreatePingButton("Settings", "Assets/ZFramework/Runtime/Module/Settings/Settings.cs"));
            return actions;
        }

        private static Button CreatePingButton(string label, string path)
        {
            var button = new Button(() =>
            {
                UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset == null)
                    return;
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }) { text = label };
            button.AddToClassList("zag-source-button");
            return button;
        }

        private enum GuidePage
        {
            Overview,
            ModuleMap,
            DecisionGuide
        }

        private readonly struct ModuleGuideEntry
        {
            public ModuleGuideEntry(string name, string kind, string summary, string whenToUse, string[] steps, string example, string relationship, string sourcePath)
            {
                Name = name;
                Kind = kind;
                Summary = summary;
                WhenToUse = whenToUse;
                Steps = steps;
                Example = example;
                Relationship = relationship;
                SourcePath = sourcePath;
            }

            public string Name { get; }
            public string Kind { get; }
            public string Summary { get; }
            public string WhenToUse { get; }
            public string[] Steps { get; }
            public string Example { get; }
            public string Relationship { get; }
            public string SourcePath { get; }
        }

        private readonly struct DecisionQuestion
        {
            public DecisionQuestion(string number, string title, string hint)
            {
                Number = number;
                Title = title;
                Hint = hint;
            }

            public string Number { get; }
            public string Title { get; }
            public string Hint { get; }
        }
    }
}
