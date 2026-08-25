using System.Collections.Generic;
using Core;
using HuntingInDarkness.Data;
using HuntingInDarkness.Settlement;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Settlement
{
    /// <summary>
    /// 营地阶段 2D HUD（精简版）。
    /// 只保留：顶部年份标签、底部「年鉴 / 出发狩猎」按钮、以及叠加详情面板。
    /// 主内容区（猎人/资源/工坊/发明）已迁移至 SettlementTable3D（世界空间 3D 卡牌）。
    ///
    /// 骨架在场景中预先搭好，所有引用通过 Inspector 连线；缺任何一项会在 Init 明确报错，
    /// 便于一眼看出本场景必须配置哪些组件。
    /// </summary>
    public class SettlementUIManager : MonoBehaviour
    {
        // ─── 场景引用（预先搭好并连线）────────────────────────────
        [Header("HUD 骨架")]
        [SerializeField] private TextMeshProUGUI _yearLabel;      // 顶部年份标签
        [SerializeField] private GameObject      _panelOverlay;   // 叠加面板的遮罩根（初始隐藏）
        [SerializeField] private Button          _annalsButton;   // 年鉴按钮

        [Header("子面板（预放在 overlay 下）")]
        [SerializeField] private EventPopup             _eventPopup;
        [SerializeField] private HunterDetailPanel      _detailPanel;
        [SerializeField] private DepartureConfirmWindow _departureConfirmWindow;

        // ─── 注入（由 GameManager 调用 Init）────────────────────
        private SettlementManager _settlementMgr;

        // ─── 初始化 ──────────────────────────────────────────────

        public void Init(SettlementManager mgr)
        {
            _settlementMgr = mgr;
            EventBus.Subscribe<YearAdvancedEvent>(OnYearAdvanced);

            if (!ValidateSceneRefs()) return;

            _eventPopup.OnResolved = CloseOverlay;
            _detailPanel.OnClose   = CloseOverlay;
            _annalsButton.onClick.AddListener(() => Debug.Log("[UI] 年鉴面板尚未实现"));

            _panelOverlay.SetActive(false);
            RefreshYearLabel();
        }

        /// <summary>校验所有场景引用是否连线；缺失逐项报错并指出字段名。</summary>
        private bool ValidateSceneRefs()
        {
            bool ok = true;
            void Req(Object o, string field)
            {
                if (o == null)
                {
                    Debug.LogError($"[SettlementUIManager] 场景引用未配置：{field}（请在 Inspector 连线）", this);
                    ok = false;
                }
            }
            Req(_yearLabel,              nameof(_yearLabel));
            Req(_panelOverlay,           nameof(_panelOverlay));
            Req(_annalsButton,           nameof(_annalsButton));
            Req(_eventPopup,             nameof(_eventPopup));
            Req(_detailPanel,            nameof(_detailPanel));
            Req(_departureConfirmWindow, nameof(_departureConfirmWindow));
            return ok;
        }

        // ─── 面板显示 ────────────────────────────────────────────

        public void ShowEvent(EventData evt, HunterInstance hunter)
        {
            _panelOverlay.SetActive(true);
            HideAllPanels();
            _eventPopup.gameObject.SetActive(true);
            _eventPopup.Show(evt, hunter, _settlementMgr.Events);
        }

        public void ShowHunterDetail(HunterInstance hunter)
        {
            _panelOverlay.SetActive(true);
            HideAllPanels();
            _detailPanel.gameObject.SetActive(true);
            _detailPanel.Show(hunter);
        }

        /// <summary>由 SettlementTable3D 的出发卡点击触发：展示队伍并询问确认。</summary>
        public void ShowDepartureConfirm(List<HunterInstance> squad)
        {
            _panelOverlay.SetActive(true);
            HideAllPanels();
            _departureConfirmWindow.gameObject.SetActive(true);
            _departureConfirmWindow.Show(squad,
                onConfirm: () =>
                {
                    var ids = new List<int>(squad.Count);
                    foreach (var h in squad) ids.Add(h.InstanceId);
                    GameManager.Instance?.TryDepartForHunt(ids);
                    CloseOverlay();
                },
                onCancel: CloseOverlay);
        }

        private void CloseOverlay()
        {
            HideAllPanels();
            _panelOverlay.SetActive(false);
        }

        private void HideAllPanels()
        {
            _eventPopup.gameObject.SetActive(false);
            _detailPanel.gameObject.SetActive(false);
            _departureConfirmWindow.gameObject.SetActive(false);
        }

        // ─── 刷新 ────────────────────────────────────────────────

        public void Refresh() => RefreshYearLabel();

        private void RefreshYearLabel()
        {
            if (_yearLabel != null && _settlementMgr != null)
                _yearLabel.text = $"  第 {_settlementMgr.Data.CurrentYear} 年  —  营地建设阶段";
        }

        private void OnYearAdvanced(YearAdvancedEvent _) => RefreshYearLabel();

        // ─── 清理 ────────────────────────────────────────────────

        private void OnDestroy()
        {
            EventBus.Unsubscribe<YearAdvancedEvent>(OnYearAdvanced);
        }
    }
}
