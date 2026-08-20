using Core;
using HuntingInDarkness.ActionFlow.Hunt;
using HuntingInDarkness.Data;
using HuntingInDarkness.Hunt;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Hunt
{
    /// <summary>
    /// 狩猎阶段 UI 总协调器（MonoBehaviour）。
    /// 显示猎人状态面板、资源采集弹窗、开发者模式按钮。
    /// </summary>
    public class HuntUIManager : MonoBehaviour
    {
        private HuntManager _huntMgr;
        private HuntMapVisualizer huntVisualizer;
        private bool _initialized;

        // 子面板
        private HunterStatusOverlay  _statusOverlay;
        private ResourceHarvestPopup _harvestPopup;
        private EventPopupHunt       _eventPopupHunt;
        private HuntHarvestPanel3D harvestPanel3D;
        private HuntStatusBoard3D statusBoard3D;

        // 顶部信息栏
        private Text _infoLabel;

        // ─── 初始化 ──────────────────────────────────────────────

        public void Init(HuntManager huntMgr, HuntMapVisualizer visualizer = null)
        {
            harvestPanel3D?.DismissForSessionChange();
            _huntMgr = huntMgr;
            huntVisualizer = visualizer;
            huntMgr.OnResourcePointClicked = ShowHarvestPopup;

            if (_initialized)
            {
                if (statusBoard3D != null)
                    statusBoard3D.Initialize(_huntMgr);
                Refresh();
                return;
            }

            BuildUI();
            Refresh();

            // 订阅事件
            EventBus.Subscribe<GameEventTriggeredEvent>(OnGameEvent);
            EventBus.Subscribe<HuntTileInteractionCommittedEvent>(OnTileInteractionCommitted);
            EventBus.Subscribe<HarvestCommittedEvent>(OnHarvestCommitted);
            _initialized = true;
        }

        private void BuildUI()
        {
            if (huntVisualizer != null)
            {
                statusBoard3D = HuntStatusBoard3D.Create(huntVisualizer.transform);
                statusBoard3D.Initialize(_huntMgr);
                return;
            }

            FullStretch(gameObject);

            // 顶部信息栏
            var topGo = NewPanel("TopBar", new Vector2(0, 0.92f), new Vector2(1, 1));
            topGo.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.14f, 0.9f);
            _infoLabel = MakeText(topGo, "Info", "狩猎阶段", 15, TextAnchor.MiddleCenter);

            // 猎人状态叠加（左下角）
            var statusGo = NewPanel("HunterStatus", new Vector2(0, 0), new Vector2(0.28f, 0.35f));
            statusGo.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.14f, 0.85f);
            _statusOverlay = statusGo.AddComponent<HunterStatusOverlay>();

            // 资源采集弹窗（居中，初始隐藏）
            var harvestGo = NewPanel("HarvestPopup", new Vector2(0.25f, 0.2f), new Vector2(0.75f, 0.8f));
            harvestGo.SetActive(false);
            _harvestPopup = harvestGo.AddComponent<ResourceHarvestPopup>();
            _harvestPopup.OnClose = () => harvestGo.SetActive(false);

            // 狩猎事件弹窗（复用简化版，居中）
            var evtGo = NewPanel("EventPopup", new Vector2(0.15f, 0.1f), new Vector2(0.85f, 0.9f));
            evtGo.SetActive(false);
            _eventPopupHunt = evtGo.AddComponent<EventPopupHunt>();
            _eventPopupHunt.OnClose = () => evtGo.SetActive(false);

            topGo.transform.SetAsLastSibling();
        }

        // ─── 刷新 ────────────────────────────────────────────────

        public void Refresh()
        {
            if (_huntMgr == null) return;
            if (statusBoard3D != null)
            {
                statusBoard3D.Refresh();
                return;
            }
            _infoLabel.text = $"{PlayableHuntDestinationRuntime.ActiveDisplayName}  — 小队位置 {_huntMgr.SquadPosition}";
            _statusOverlay?.Init(_huntMgr.ActiveHunters);
        }

        private void ShowHarvestPopup(ResourcePointInstance point, HunterInstance hunter)
        {
            if (huntVisualizer != null)
            {
                if (_harvestPopup != null)
                    _harvestPopup.gameObject.SetActive(false);
                Vector3 position = huntVisualizer.TryGetResourcePointPresentationPosition(point, out Vector3 markerPosition)
                    ? markerPosition
                    : huntVisualizer.TabletopInteractionAnchor.position + new Vector3(0f, 0.58f, -1.55f);
                harvestPanel3D ??= HuntHarvestPanel3D.Create(huntVisualizer.transform);
                harvestPanel3D.Show(point, _huntMgr, position);
                return;
            }
            if (_harvestPopup == null)
                return;
            _harvestPopup.gameObject.SetActive(true);
            _harvestPopup.Show(point, hunter, _huntMgr);
        }

        private void OnGameEvent(GameEventTriggeredEvent e)
        {
            if (e.EventId?.StartsWith("tile_reveal:") == true)
                Refresh();
        }

        private void OnTileInteractionCommitted(HuntTileInteractionCommittedEvent _) => Refresh();

        private void OnHarvestCommitted(HarvestCommittedEvent _) => Refresh();

        // ─── uGUI 工厂 ────────────────────────────────────────────

        private GameObject NewPanel(string name, Vector2 aMin, Vector2 aMax)
            => NewPanel(name, transform, aMin, aMax);

        private static GameObject NewPanel(string name, Transform parent,
            Vector2 aMin, Vector2 aMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            go.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.15f, 0.8f);
            return go;
        }

        internal static Text MakeText(GameObject parent, string name, string text,
            int fontSize, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            FullStretch(go);
            var t = go.AddComponent<Text>();
            t.text = text; t.fontSize = fontSize; t.alignment = anchor;
            t.color = Color.white;
            t.font  = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            return t;
        }

        internal static void FullStretch(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<GameEventTriggeredEvent>(OnGameEvent);
            EventBus.Unsubscribe<HuntTileInteractionCommittedEvent>(OnTileInteractionCommitted);
            EventBus.Unsubscribe<HarvestCommittedEvent>(OnHarvestCommitted);
            if (statusBoard3D != null)
                Destroy(statusBoard3D.gameObject);
            if (harvestPanel3D != null)
            {
                harvestPanel3D.DismissForSessionChange();
                Destroy(harvestPanel3D.gameObject);
            }
        }
    }
}
