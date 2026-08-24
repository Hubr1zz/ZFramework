using Core;
using Cysharp.Threading.Tasks;
using GameplayBase;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// 开发者模式面板（Shared UI，挂在 Canvas/Shared 下）。
    /// 快捷键 F1 切换显隐。
    ///
    /// 功能：
    ///   - 跳转到任意阶段（Settlement / Hunt / BossFight）
    ///   - 快速添加资源
    ///   - 快速招募猎人
    ///   - 修改年份 / 跳过当前事件
    ///   - 直接触发 Boss 胜利（结算）
    ///   - 保存 / 读档
    /// </summary>
    public class DevModePanel : MonoBehaviour
    {
        [Header("切换快捷键")]
        public KeyCode toggleKey = KeyCode.F1;

        private GameManager _gm;
        private GameObject  _panel;
        private bool        _visible = false;

        // ─── 初始化 ──────────────────────────────────────────────

        public void Init(GameManager gm)
        {
            _gm = gm;
            BuildPanel();
            _panel.SetActive(false);
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
                Toggle();
        }

        public void Toggle()
        {
            _visible = !_visible;
            _panel?.SetActive(_visible);
        }

        // ─── UI 构建 ─────────────────────────────────────────────

        private void BuildPanel()
        {
            _panel = new GameObject("DevPanel");
            _panel.transform.SetParent(transform, false);
            var rt = _panel.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.65f, 0.3f);
            rt.anchorMax = new Vector2(0.99f, 0.98f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            _panel.AddComponent<Image>().color = new Color(0.05f, 0.08f, 0.06f, 0.96f);

            // 标题
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(_panel.transform, false);
            var trt = titleGo.AddComponent<RectTransform>();
            trt.anchorMin = new Vector2(0, 0.92f); trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            titleGo.AddComponent<Image>().color = new Color(0.1f, 0.2f, 0.1f, 1f);
            MakeText(titleGo, "T", $"★ 开发者模式  [{toggleKey}]", 13, TextAnchor.MiddleCenter)
                .color = new Color(0.6f, 1f, 0.6f);

            // 内容容器（ScrollRect）
            var svGo = new GameObject("SV");
            svGo.transform.SetParent(_panel.transform, false);
            var svRt = svGo.AddComponent<RectTransform>();
            svRt.anchorMin = new Vector2(0, 0); svRt.anchorMax = new Vector2(1, 0.92f);
            svRt.offsetMin = svRt.offsetMax = Vector2.zero;
            svGo.AddComponent<Image>().color = Color.clear;
            var sr = svGo.AddComponent<ScrollRect>(); sr.horizontal = false;

            var vpGo = new GameObject("VP"); vpGo.transform.SetParent(svGo.transform, false);
            FullStretch(vpGo);
            vpGo.AddComponent<Image>().color = Color.clear;
            vpGo.AddComponent<Mask>().showMaskGraphic = false;
            sr.viewport = vpGo.GetComponent<RectTransform>();

            var cGo = new GameObject("Content"); cGo.transform.SetParent(vpGo.transform, false);
            var crt = cGo.AddComponent<RectTransform>();
            crt.anchorMin = new Vector2(0, 1); crt.anchorMax = new Vector2(1, 1);
            crt.pivot     = new Vector2(0.5f, 1);
            var vlg = cGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4; vlg.padding = new RectOffset(6, 6, 6, 6);
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            cGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.content = crt;

            BuildButtons(cGo.transform);
        }

        private void BuildButtons(Transform parent)
        {
            // ── 阶段跳转 ──
            AddSection(parent, "── 阶段跳转 ──");
            AddBtn(parent, "▶ 营地阶段",   () => _gm?.TransitionToPhase(GamePhase.Settlement));
            AddBtn(parent, "▶ 狩猎阶段",   () => _gm?.TransitionToPhase(GamePhase.Hunt));
            AddBtn(parent, "▶ Boss决战",   () => _gm?.TransitionToPhase(GamePhase.BossFight));

            // ── Boss决战控制 ──
            AddSection(parent, "── Boss决战 ──");
            AddBtn(parent, "✓ Boss已击败（结算）",  OnDevBossDefeated);

            // ── 资源快速添加 ──
            AddSection(parent, "── 快速资源 ──");
            AddBtn(parent, "+ Bone ×5",  () => DevResource("Bone",  5));
            AddBtn(parent, "+ Hide ×5",  () => DevResource("Hide",  5));
            AddBtn(parent, "+ Stone ×5", () => DevResource("Stone", 5));
            AddBtn(parent, "+ Organ ×3", () => DevResource("Organ", 3));

            // ── 猎人 ──
            AddSection(parent, "── 猎人 ──");
            AddBtn(parent, "+ 招募猎人", () => _gm?.DevAddHunter("测试猎人"));

            // ── 年份 ──
            AddSection(parent, "── 时间线 ──");
            AddBtn(parent, "+ 推进1年",  () => _gm?.DevAdvanceYear());

            // ── 存档 ──
            AddSection(parent, "── 存档 ──");
            AddBtn(parent, "💾 保存",   () => _gm?.DevSave());
            AddBtn(parent, "📂 读档",   () => _gm?.DevLoad());
            AddBtn(parent, "🗑 删除存档", () => _gm?.DeleteCampaignSaveAsync().Forget());
        }

        // ─── 按钮逻辑 ─────────────────────────────────────────────

        private void OnDevBossDefeated()
        {
            EventBus.Publish(new BossDefeatedEvent());
        }

        private void DevResource(string name, int amount)
        {
            _gm?.DevAddResource(name, amount);
        }

        // ─── uGUI 工厂 ────────────────────────────────────────────

        private void AddSection(Transform parent, string label)
        {
            var go = new GameObject($"Sec_{label}");
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>(); le.preferredHeight = 22;
            go.AddComponent<Image>().color = new Color(0.1f, 0.15f, 0.1f, 1f);
            MakeText(go, "T", label, 10, TextAnchor.MiddleLeft).GetComponent<RectTransform>()
                .offsetMin = new Vector2(6, 0);
        }

        private void AddBtn(Transform parent, string label, System.Action action)
        {
            var go = new GameObject($"Btn_{label}");
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>(); le.preferredHeight = 30;
            go.AddComponent<Image>().color = new Color(0.15f, 0.22f, 0.15f, 1f);
            var btn = go.AddComponent<Button>();
            var a = action;
            btn.onClick.AddListener(() => a());
            MakeText(go, "T", label, 12, TextAnchor.MiddleCenter);
        }

        private static Text MakeText(GameObject parent, string name, string text,
            int fontSize, TextAnchor anchor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            FullStretch(go);
            var t = go.AddComponent<Text>();
            t.text = text; t.fontSize = fontSize; t.alignment = anchor;
            t.color = Color.white;
            t.font  = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return t;
        }

        private static void FullStretch(GameObject go)
        {
            if (!go.TryGetComponent<RectTransform>(out var rt))
            {
                rt = go.AddComponent<RectTransform>();
            }
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
