using System.Collections.Generic;
using Core;
using Cysharp.Threading.Tasks;
using GameplayBase;
using HuntingInDarkness.ActionFlow.Settlement;
using HuntingInDarkness.Data;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.ViewLayer.Tabletop;
using UnityEngine;

namespace HuntingInDarkness.ViewLayer.Settlement
{
    /// <summary>营地与狩猎阶段之间的世界空间小队编成和目的地选择端口。</summary>
    public sealed class PlayableHuntDestinationView : MonoBehaviour, IPlayableHuntDepartureInput
    {
        private readonly List<int> pendingHunterIds = new();
        private readonly List<PlayableHuntDestination> availableDestinations = new();
        private GameManager manager;
        private PlayableHuntDestinationCatalog catalog;
        private TabletopHuntDeparturePanel3D panel;
        private UI.SettlementTable3D settlementTable;
        private bool requestInFlight;
        private bool hidesSettlementTable;
        private bool settlementTableWasActive;
        private int selectedDestinationIndex;

        public bool IsPresenting => panel != null && panel.IsOpen;
        public TabletopHuntDeparturePanel3D ActivePanel => panel;

        public void Initialize(GameManager gameManager, PlayableHuntDestinationCatalog destinationCatalog)
        {
            manager = gameManager;
            catalog = destinationCatalog;
            manager?.SetPlayableHuntDepartureInput(this);
        }

        public void RequestDeparture(IReadOnlyList<int> hunterIds)
        {
            if (requestInFlight || manager?.SettlementData == null || manager.CurrentGamePhase != GamePhase.Settlement || manager.IsSettlementActionSessionRunning)
                return;
            List<HunterInstance> hunters = manager.SettlementData.GetAvailableHunters();
            if (hunters.Count == 0)
                return;

            pendingHunterIds.Clear();
            if (hunterIds != null)
                foreach (int hunterId in hunterIds)
                    if (manager.SettlementData.GetHunter(hunterId)?.IsAvailable == true && !pendingHunterIds.Contains(hunterId))
                        pendingHunterIds.Add(hunterId);
            EnsurePanel();
            panel.PresentSquad(GetPanelAnchor(), hunters, pendingHunterIds, OpenDestinations, Close);
            HideSettlementTable();
        }

        private void OpenDestinations(IReadOnlyList<int> hunterIds)
        {
            pendingHunterIds.Clear();
            if (hunterIds != null)
                pendingHunterIds.AddRange(hunterIds);
            availableDestinations.Clear();
            if (catalog != null)
                availableDestinations.AddRange(catalog.GetAvailable(manager.SettlementData.CurrentYear));
            if (availableDestinations.Count == 0)
            {
                ConfirmDepartureAsync(null).Forget();
                return;
            }

            PlayableHuntDestination active = PlayableHuntDestinationRuntime.ActiveDestination;
            int activeIndex = availableDestinations.IndexOf(active);
            selectedDestinationIndex = activeIndex >= 0 ? activeIndex : 0;
            panel.PresentDestinations(GetPanelAnchor(), availableDestinations, ResolvePendingHunters(), selectedDestinationIndex, string.Empty, ConfirmDeparture, ReturnToSquad, Close);
        }

        private void ConfirmDeparture(PlayableHuntDestination destination)
        {
            if (requestInFlight)
                return;
            selectedDestinationIndex = availableDestinations.IndexOf(destination);
            ConfirmDepartureAsync(destination).Forget();
        }

        private async UniTaskVoid ConfirmDepartureAsync(PlayableHuntDestination destination)
        {
            requestInFlight = true;
            try
            {
                SettlementDepartureCommandResult result = await manager.DepartForHuntAsync(new List<int>(pendingHunterIds), destination);
                if (!result.Succeeded)
                {
                    PresentDestinationFailure(result.Reason);
                    return;
                }
                Close();
            }
            finally
            {
                requestInFlight = false;
            }
        }

        private void PresentDestinationFailure(string reason)
        {
            if (manager == null || manager.CurrentGamePhase != GamePhase.Settlement)
                return;
            if (availableDestinations.Count == 0)
            {
                ReturnToSquad();
                return;
            }
            panel.PresentDestinations(GetPanelAnchor(), availableDestinations, ResolvePendingHunters(), selectedDestinationIndex, reason, ConfirmDeparture, ReturnToSquad, Close);
        }

        private void ReturnToSquad()
        {
            if (manager?.SettlementData == null || manager.CurrentGamePhase != GamePhase.Settlement)
                return;
            panel.PresentSquad(GetPanelAnchor(), manager.SettlementData.GetAvailableHunters(), pendingHunterIds, OpenDestinations, Close);
        }

        private List<HunterInstance> ResolvePendingHunters()
        {
            var hunters = new List<HunterInstance>();
            if (manager?.SettlementData == null) return hunters;
            foreach (int hunterId in pendingHunterIds)
            {
                HunterInstance hunter = manager.SettlementData.GetHunter(hunterId);
                if (hunter?.IsAvailable == true)
                    hunters.Add(hunter);
            }
            return hunters;
        }

        private void EnsurePanel()
        {
            Transform parent = manager.TabletopPresentationRoot != null ? manager.TabletopPresentationRoot : transform;
            if (panel == null)
                panel = TabletopHuntDeparturePanel3D.Create(parent);
            else if (panel.transform.parent != parent)
                panel.transform.SetParent(parent, true);
        }

        private Vector3 GetPanelAnchor()
        {
            Transform presentationRoot = manager.TabletopPresentationRoot;
            Vector3 origin = presentationRoot != null ? presentationRoot.position : transform.position;
            return origin + new Vector3(0f, 0.66f, -1.8f);
        }

        private void Close()
        {
            panel?.Close();
            RestoreSettlementTable();
            pendingHunterIds.Clear();
            availableDestinations.Clear();
            selectedDestinationIndex = 0;
        }

        private void HideSettlementTable()
        {
            if (hidesSettlementTable)
                return;
            Transform presentationRoot = manager?.TabletopPresentationRoot;
            settlementTable = presentationRoot != null ? presentationRoot.GetComponentInChildren<UI.SettlementTable3D>(true) : null;
            if (settlementTable == null)
                return;
            settlementTableWasActive = settlementTable.gameObject.activeSelf;
            settlementTable.gameObject.SetActive(false);
            hidesSettlementTable = true;
        }

        private void RestoreSettlementTable()
        {
            if (!hidesSettlementTable)
                return;
            if (settlementTable != null)
                settlementTable.gameObject.SetActive(settlementTableWasActive);
            settlementTable = null;
            hidesSettlementTable = false;
        }

        private void OnDestroy()
        {
            requestInFlight = false;
            manager?.ClearPlayableHuntDepartureInput(this);
            panel?.Close();
            RestoreSettlementTable();
            if (panel != null)
                Destroy(panel.gameObject);
            panel = null;
        }
    }
}
