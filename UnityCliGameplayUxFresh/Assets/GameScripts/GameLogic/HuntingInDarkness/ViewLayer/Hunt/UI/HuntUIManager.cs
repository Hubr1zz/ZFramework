using Core;
using HuntingInDarkness.ActionFlow.Hunt;
using HuntingInDarkness.Hunt;
using UnityEngine;

namespace UI.Hunt
{
    /// <summary>
    /// 狩猎阶段 3D 桌面表现协调器。
    /// 持有状态板与资源采集面板，不承担任何玩法结算。
    /// </summary>
    public class HuntUIManager : MonoBehaviour
    {
        private HuntManager _huntMgr;
        private HuntMapVisualizer huntVisualizer;
        private IHuntExplorationPort explorationPort;
        private bool _initialized;

        private HuntHarvestPanel3D harvestPanel3D;
        private HuntStatusBoard3D statusBoard3D;

        public bool IsTabletopReady => huntVisualizer != null && explorationPort != null && statusBoard3D != null;

        // ─── 初始化 ──────────────────────────────────────────────

        public void Init(HuntManager huntMgr, HuntMapVisualizer visualizer = null)
        {
            Init(huntMgr, visualizer, null);
        }

        public void Init(HuntManager huntMgr, HuntMapVisualizer visualizer, IHuntExplorationPort port)
        {
            harvestPanel3D?.DismissForSessionChange();
            ClearResourcePresentationCallbacks();
            _huntMgr = huntMgr;
            huntVisualizer = visualizer;
            explorationPort = port;
            huntMgr.OnResourcePointClicked = null;
            huntMgr.OnResourcePointPresentationRequested = visualizer != null && port != null ? ShowHarvestPresentation : null;

            if (_initialized)
            {
                if (statusBoard3D == null)
                    BuildUI();
                else
                    statusBoard3D.Initialize(_huntMgr, explorationPort);
                Refresh();
                return;
            }

            BuildUI();
            Refresh();

            // 订阅事件
            EventBus.Subscribe<GameEventTriggeredEvent>(OnGameEvent);
            EventBus.Subscribe<HuntTileInteractionCommittedEvent>(OnTileInteractionCommitted);
            EventBus.Subscribe<HarvestCommittedEvent>(OnHarvestCommitted);
            EventBus.Subscribe<HuntEventNodeCommittedEvent>(OnHuntEventNodeCommitted);
            EventBus.Subscribe<HuntActorSelectionCommittedEvent>(OnHuntActorSelectionCommitted);
            EventBus.Subscribe<HuntConsumableUsedEvent>(OnHuntConsumableUsed);
            _initialized = true;
        }

        public void InitTabletop(HuntManager huntMgr, HuntMapVisualizer visualizer, IHuntExplorationPort port)
        {
            if (visualizer == null) throw new System.ArgumentNullException(nameof(visualizer));
            if (port == null) throw new System.ArgumentNullException(nameof(port));
            Init(huntMgr, visualizer, port);
            if (!IsTabletopReady) throw new System.InvalidOperationException("狩猎 3D 状态桌初始化失败。");
        }

        public void ReleaseBindings()
        {
            EventBus.Unsubscribe<GameEventTriggeredEvent>(OnGameEvent);
            EventBus.Unsubscribe<HuntTileInteractionCommittedEvent>(OnTileInteractionCommitted);
            EventBus.Unsubscribe<HarvestCommittedEvent>(OnHarvestCommitted);
            EventBus.Unsubscribe<HuntEventNodeCommittedEvent>(OnHuntEventNodeCommitted);
            EventBus.Unsubscribe<HuntActorSelectionCommittedEvent>(OnHuntActorSelectionCommitted);
            EventBus.Unsubscribe<HuntConsumableUsedEvent>(OnHuntConsumableUsed);
            ClearResourcePresentationCallbacks();
        }

        private void BuildUI()
        {
            if (huntVisualizer == null || explorationPort == null) return;
            statusBoard3D = HuntStatusBoard3D.Create(huntVisualizer.transform);
            statusBoard3D.Initialize(_huntMgr, explorationPort);
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
        }

        private void ShowHarvestPresentation(HuntResourcePointPresentationRequest request)
        {
            if (explorationPort == null || huntVisualizer == null || !explorationPort.TryCreateSnapshot(request.Coordinate, request.PointIndex, out HuntExplorationSnapshot target)) return;
            Vector3 position = huntVisualizer.TryGetResourcePointPresentationPosition(request.Coordinate, request.PointIndex, out Vector3 markerPosition)
                ? markerPosition
                : huntVisualizer.TabletopInteractionAnchor.position + new Vector3(0f, 0.58f, -1.55f);
            harvestPanel3D ??= HuntHarvestPanel3D.Create(huntVisualizer.transform);
            harvestPanel3D.Show(target, request.ResourceName, request.DrawCount, request.PoolCardCount, explorationPort, position);
        }

        private void OnGameEvent(GameEventTriggeredEvent e)
        {
            if (e.EventId?.StartsWith("tile_reveal:") == true)
                Refresh();
        }

        private void OnTileInteractionCommitted(HuntTileInteractionCommittedEvent _) => Refresh();

        private void OnHarvestCommitted(HarvestCommittedEvent _) => Refresh();

        private void OnHuntEventNodeCommitted(HuntEventNodeCommittedEvent _) => Refresh();

        private void OnHuntActorSelectionCommitted(HuntActorSelectionCommittedEvent evt)
        {
            if (explorationPort != null && evt.SessionId == explorationPort.SessionId)
                Refresh();
        }

        private void OnHuntConsumableUsed(HuntConsumableUsedEvent evt)
        {
            if (explorationPort != null && evt.SessionId == explorationPort.SessionId)
                Refresh();
        }

        // ─── uGUI 工厂 ────────────────────────────────────────────

        private void OnDestroy()
        {
            ReleaseBindings();
            if (statusBoard3D != null)
                Destroy(statusBoard3D.gameObject);
            if (harvestPanel3D != null)
            {
                harvestPanel3D.DismissForSessionChange();
                Destroy(harvestPanel3D.gameObject);
            }
        }

        private void ClearResourcePresentationCallbacks()
        {
            if (_huntMgr == null) return;
            if (_huntMgr.OnResourcePointPresentationRequested == ShowHarvestPresentation) _huntMgr.OnResourcePointPresentationRequested = null;
        }
    }
}
