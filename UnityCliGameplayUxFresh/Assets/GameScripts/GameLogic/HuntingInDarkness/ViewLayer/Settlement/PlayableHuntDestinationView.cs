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
        private readonly List<PlayableHuntDestinationAvailability> destinationAvailability = new();
        private GameManager manager;
        private PlayableHuntDestinationCatalog catalog;
        private TabletopHuntDeparturePanel3D panel;
        private UI.SettlementTable3D settlementTable;
        private bool requestInFlight;
        private bool isDestroying;
        private bool hidesSettlementTable;
        private bool settlementTableWasActive;
        private int selectedDestinationIndex;
        private string attemptedDestinationId = string.Empty;

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
            if (requestInFlight || manager == null || manager.SettlementData == null || manager.CurrentGamePhase != GamePhase.Settlement)
                return;
            pendingHunterIds.Clear();
            if (hunterIds != null)
                foreach (int hunterId in hunterIds)
                    if (manager.SettlementData.GetHunter(hunterId)?.IsAvailable == true && !pendingHunterIds.Contains(hunterId))
                        pendingHunterIds.Add(hunterId);
            EnsurePanel();
            if (!HuntingInDarkness.GameCore.Settlement.DepartureRules.CanDepart(pendingHunterIds, out _)) return;
            OpenDestinations(new List<int>(pendingHunterIds));
        }

        private void OpenDestinations(IReadOnlyList<int> hunterIds)
        {
            pendingHunterIds.Clear();
            if (hunterIds != null)
                pendingHunterIds.AddRange(hunterIds);
            destinationAvailability.Clear();
            if (catalog != null)
                destinationAvailability.AddRange(catalog.GetAvailability(manager.SettlementData.CurrentYear));
            if (destinationAvailability.Count == 0)
            {
                ConfirmDepartureAsync(null).Forget();
                return;
            }

            attemptedDestinationId = string.Empty;
            selectedDestinationIndex = -1;
            panel.PresentDestinationProjections(GetPanelAnchor(), destinationAvailability, ResolvePendingHunters(), selectedDestinationIndex, string.Empty, ConfirmDeparture, null, null);
        }

        private void ConfirmDeparture(PlayableHuntDestination destination)
        {
            if (requestInFlight || destination == null)
                return;
            int resolvedIndex = PlayableHuntDestinationCatalog.ResolveAvailableIndex(destinationAvailability, destination.DestinationId);
            if (resolvedIndex < 0 || !ReferenceEquals(destinationAvailability[resolvedIndex].Destination, destination) || !destinationAvailability[resolvedIndex].IsAvailable)
                return;
            selectedDestinationIndex = resolvedIndex;
            attemptedDestinationId = destination.DestinationId;
            ConfirmDepartureAsync(destination).Forget();
        }

        private async UniTaskVoid ConfirmDepartureAsync(PlayableHuntDestination destination)
        {
            requestInFlight = true;
            try
            {
                SettlementDepartureCommandResult result = await manager.DepartForHuntAsync(new List<int>(pendingHunterIds), destination);
                if (this == null || isDestroying) return;
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
            destinationAvailability.Clear();
            if (catalog != null)
                destinationAvailability.AddRange(catalog.GetAvailability(manager.SettlementData.CurrentYear));
            if (destinationAvailability.Count == 0)
            {
                Close();
                return;
            }
            selectedDestinationIndex = PlayableHuntDestinationCatalog.ResolveAvailableIndex(destinationAvailability, attemptedDestinationId);
            panel.PresentDestinationProjections(GetPanelAnchor(), destinationAvailability, ResolvePendingHunters(), selectedDestinationIndex, reason, ConfirmDeparture, null, null);
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
            return origin + new Vector3(0f, 0.66f, -2.8f);
        }

        private void Close()
        {
            if (panel != null)
                panel.Close();
            RestoreSettlementTable();
            pendingHunterIds.Clear();
            destinationAvailability.Clear();
            selectedDestinationIndex = 0;
            attemptedDestinationId = string.Empty;
        }

        private void HideSettlementTable()
        {
            if (hidesSettlementTable)
                return;
            Transform presentationRoot = manager != null ? manager.TabletopPresentationRoot : null;
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
            isDestroying = true;
            requestInFlight = false;
            if (manager != null)
                manager.ClearPlayableHuntDepartureInput(this);
            if (panel != null)
                panel.Close();
            RestoreSettlementTable();
            if (panel != null)
                Destroy(panel.gameObject);
            panel = null;
        }
    }
}
