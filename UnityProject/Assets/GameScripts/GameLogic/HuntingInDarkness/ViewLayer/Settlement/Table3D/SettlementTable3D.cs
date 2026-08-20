using System.Collections.Generic;
using Cards3D;
using Core;
using HuntingInDarkness.Data;
using HuntingInDarkness.Settlement;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// 营地阶段 3D 桌面（场景驱动版本）。
    ///
    /// 布局由场景预放置对象决定：
    ///   - 4 个分区 presenter（HunterZone / ResourceZone / WorkshopZone / InventionZone），
    ///     各自持有 SlotGrid，在 Inspector 连线（卡牌经静态 EntityCreator 工厂创建）
    ///   - 桌面背景、区域背景板、标签等均为独立场景对象，可在编辑器直接调整
    ///
    /// 此脚本只负责：订阅事件、把数据与点击回调分发给各分区 presenter。
    /// 未连线时（GameManager 用 AddComponent 运行时创建）程序化回退搭建四区与 presenter。
    /// </summary>
    public class SettlementTable3D : MonoBehaviour
    {
        // ─── 分区 presenter（Inspector 连线）──────────────────────────────
        [Header("分区 presenter（场景预放置）")]
        [SerializeField] private HunterZone    _hunterZone;
        [SerializeField] private ResourceZone  _resourceZone;
        [SerializeField] private WorkshopZone  _workshopZone;
        [SerializeField] private InventionZone _inventionZone;

        [Header("出发小队区（场景预放，无程序化回退）")]
        [SerializeField] private SquadZone _squadZone;

        [Header("猎人 3D 装备板")]
        [SerializeField] private HunterEquipmentPanel3D hunterEquipmentPanel;
        [SerializeField] private Transform hunterEquipmentPanelAnchor;

        // ─── 点击回调（上层先设置，Init 时下发给对应分区）──────────────────
        public System.Action<HunterInstance>   OnHunterClicked;
        /// <summary>点击发明卡（有主动效果时触发），由外部展示效果选择面板。</summary>
        public System.Action<InventionCard3D>  OnInventionEffectRequested;
        /// <summary>点击工坊卡，由外部展示可制造物品面板。</summary>
        public System.Action<WorkshopCard3D>   OnWorkshopClicked;
        /// <summary>点击出发卡，上报当前小队，由外部弹出出发确认窗。</summary>
        public System.Action<List<HunterInstance>> OnDepartureRequested;

        // ─── 注入数据 ─────────────────────────────────────────────────────
        private SettlementManager _mgr;

        // ─── 初始化 ───────────────────────────────────────────────────────

        public void Init(SettlementManager mgr)
        {
            _mgr = mgr;
            EnsureSceneRefs();    // 未连线时程序化搭建四区 + presenter
            EnsureHunterEquipmentPanel();
            WireZoneCallbacks();  // 把上层设的回调下发给分区
            FillAllZones();

            EventBus.Subscribe<ResourceChangedEvent>(OnResourceChanged);
            EventBus.Subscribe<HunterRosterChangedEvent>(OnRosterChanged);
            EventBus.Subscribe<YearAdvancedEvent>(OnYearAdvanced);
        }

        private void WireZoneCallbacks()
        {
            _hunterZone.OnHunterClicked              = ShowHunterEquipment;
            _inventionZone.OnInventionEffectRequested = OnInventionEffectRequested;
            _workshopZone.OnWorkshopClicked          = OnWorkshopClicked;
            if (_squadZone != null) _squadZone.OnDepartureRequested = OnDepartureRequested;
        }

        private void EnsureHunterEquipmentPanel()
        {
            if (hunterEquipmentPanel == null)
                hunterEquipmentPanel = HunterEquipmentPanel3D.Create(transform);
        }

        private void ShowHunterEquipment(HunterInstance hunter)
        {
            if (hunterEquipmentPanel == null)
            {
                OnHunterClicked?.Invoke(hunter);
                return;
            }

            Vector3 position = hunterEquipmentPanelAnchor != null ? hunterEquipmentPanelAnchor.position : transform.TransformPoint(new Vector3(0f, 0.08f, -3.2f));
            hunterEquipmentPanel.Show(hunter, position);
        }

        private void FillAllZones()
        {
            _hunterZone.Fill(_mgr.Data.GetAvailableHunters());
            _resourceZone.Fill(_mgr.Data.Resources);
            _workshopZone.Fill();
            _inventionZone.Fill(_mgr.Inventions.AllInventions);
        }

        // ─── 程序化回退布局 ─────────────────────────────────────────────────
        // 设计：与卡牌「prefab 优先、程序化回退」一致。
        //   - 场景中已连线分区 presenter → 直接使用（精细可控）
        //   - 未连线（GameManager 用 AddComponent 运行时创建）→ 自动搭建四区 + presenter
        // 这样营地 3D 桌面开箱即用，无需手动布置场景。

        private void EnsureSceneRefs()
        {
            // 任一分区已连线即视为场景已布置，跳过自动搭建
            if (_hunterZone || _resourceZone || _workshopZone || _inventionZone) return;

            BuildFallbackLayout();
        }

        private void BuildFallbackLayout()
        {
            const float slotW = CardView3D.CW + 0.06f;
            const float slotH = CardView3D.CH + 0.06f;
            const float gap   = 0.10f;
            const float interGap = 0.80f;

            int hunCount = _mgr.Data.GetAvailableHunters().Count;
            int resCount = _mgr.Data.Resources.Count;
            int invCount = _mgr.Inventions.AllInventions.Count;
            int wsCount  = 0; // 暂无建筑/工坊数据，预留空区

            var hun = Dim(hunCount, 3, 2);
            var res = Dim(resCount, 4, 2);
            var ws  = Dim(wsCount,  3, 1);
            var inv = Dim(invCount, 5, 2);

            float Wz(int c) => c * (slotW + gap);
            float Hz(int r) => r * (slotH + gap);

            float leftW  = Mathf.Max(Wz(hun.cols), Wz(ws.cols));
            float rightW = Mathf.Max(Wz(res.cols), Wz(inv.cols));
            float topH   = Mathf.Max(Hz(hun.rows), Hz(res.rows));
            float botH   = Mathf.Max(Hz(ws.rows),  Hz(inv.rows));

            float leftCX  = -(leftW  * 0.5f + interGap * 0.5f);
            float rightCX =  (rightW * 0.5f + interGap * 0.5f);
            float topCZ   =  (topH * 0.5f + interGap * 0.5f);
            float botCZ   = -(botH * 0.5f + interGap * 0.5f);

            BuildTableSurface(leftW + rightW + interGap + 1.5f, topH + botH + interGap + 1.5f);

            var hunterGrid    = BuildZone("猎 人", leftCX,  topCZ, hun.cols, hun.rows,
                                          slotW, slotH, gap, CardCategory.HunterProfile);
            var resourceGrid  = BuildZone("资 源", rightCX, topCZ, res.cols, res.rows,
                                          slotW, slotH, gap, CardCategory.Resource);
            var workshopGrid  = BuildZone("建 筑", leftCX,  botCZ, ws.cols, ws.rows,
                                          slotW, slotH, gap, CardCategory.Workshop);
            var inventionGrid = BuildZone("发 明", rightCX, botCZ, inv.cols, inv.rows,
                                          slotW, slotH, gap, CardCategory.Invention);

            // 程序化创建分区 presenter 并注入区域与工厂
            _hunterZone    = gameObject.AddComponent<HunterZone>();
            _resourceZone  = gameObject.AddComponent<ResourceZone>();
            _workshopZone  = gameObject.AddComponent<WorkshopZone>();
            _inventionZone = gameObject.AddComponent<InventionZone>();

            _hunterZone.SetRefs(hunterGrid);
            _resourceZone.SetRefs(resourceGrid);
            _workshopZone.SetRefs(workshopGrid);
            _inventionZone.SetRefs(inventionGrid);
        }

        private SlotGrid BuildZone(string title, float cx, float cz, int cols, int rows,
            float slotW, float slotH, float gap, CardCategory cat)
        {
            var grid = SlotGrid.Create(transform, new Vector3(cx, 0.006f, cz),
                cols, rows, slotW, slotH, gap, autoExpand: true, cat);
            grid.AddLabel(title);

            // 区域背景板
            float w = cols * (slotW + gap) + 0.40f;
            float h = rows * (slotH + gap) + 0.40f;
            var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bg.name = $"ZoneBG_{title}";
            bg.transform.SetParent(transform, false);
            bg.transform.localPosition = new Vector3(cx, 0.003f, cz);
            bg.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            bg.transform.localScale    = new Vector3(w, h, 1f);
            Destroy(bg.GetComponent<Collider>());
            var mr = bg.GetComponent<MeshRenderer>();
            mr.material.color = new Color(0.11f, 0.11f, 0.15f, 0.92f);
            return grid;
        }

        private void BuildTableSurface(float w, float h)
        {
            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "SettlementTableSurface";
            plane.transform.SetParent(transform, false);
            plane.transform.localPosition = new Vector3(0f, 0f, 0f);
            // Plane 默认 10x10，缩放到目标尺寸
            plane.transform.localScale = new Vector3(w / 10f, 1f, h / 10f);
            plane.GetComponent<MeshRenderer>().material.color = new Color(0.13f, 0.12f, 0.10f);
            Destroy(plane.GetComponent<Collider>());
        }

        private static (int cols, int rows) Dim(int count, int maxCols, int minRows)
        {
            int cols = Mathf.Clamp(count, 1, maxCols);
            int rows = Mathf.Max(minRows, Mathf.CeilToInt((float)count / cols));
            return (cols, rows);
        }

        // ─── 公共刷新 ─────────────────────────────────────────────────────

        /// <summary>全量刷新所有卡牌（年份推进后调用）。</summary>
        public void Refresh()
        {
            if (_mgr == null) return;
            FillAllZones();
        }

        /// <summary>刷新发明与工坊卡牌的视觉状态。</summary>
        public void RefreshCards()
        {
            _inventionZone.RefreshCards();
            _workshopZone.RefreshCards();
        }

        // ─── EventBus ─────────────────────────────────────────────────────

        private void OnResourceChanged(ResourceChangedEvent e)
        {
            // 命中已有卡就地更新；否则整区重填
            if (!_resourceZone.TryUpdateCount(e.ResourceName, e.NewAmount))
                _resourceZone.Fill(_mgr.Data.Resources);
        }

        private void OnRosterChanged(HunterRosterChangedEvent _)
            => _hunterZone.Fill(_mgr.Data.GetAvailableHunters());

        private void OnYearAdvanced(YearAdvancedEvent _)
        {
            _inventionZone.RefreshCards();
            _workshopZone.RefreshCards();
        }

        // ─── 清理 ─────────────────────────────────────────────────────────

        private void OnDestroy()
        {
            EventBus.Unsubscribe<ResourceChangedEvent>(OnResourceChanged);
            EventBus.Unsubscribe<HunterRosterChangedEvent>(OnRosterChanged);
            EventBus.Unsubscribe<YearAdvancedEvent>(OnYearAdvanced);
        }
    }
}
