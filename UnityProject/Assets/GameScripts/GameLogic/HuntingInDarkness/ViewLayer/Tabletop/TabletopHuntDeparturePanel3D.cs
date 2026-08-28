using System.Collections.Generic;
using Cards3D;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Hunt;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Hunt;
using UnityEngine;

namespace HuntingInDarkness.ViewLayer.Tabletop
{
    /// <summary>远征编队与地区选择共用的世界空间桌面。</summary>
    public sealed class TabletopHuntDeparturePanel3D : MonoBehaviour
    {
        private const string HunterDropScope = "hunt-departure-squad";
        private readonly List<HuntDepartureHunterCard3D> hunterCards = new();
        private readonly List<PlayableHuntDestinationAvailability> destinationAvailability = new();
        private readonly List<HunterInstance> squadHunters = new();
        private SlotGrid rosterGrid;
        private SlotGrid squadGrid;
        private TabletopEventPrimaryCard3D squadPrimaryCard;
        private TabletopEventChoiceCard3D continueCard;
        private System.Action<IReadOnlyList<int>> squadConfirmed;
        private System.Action cancelled;
        private System.Action<PlayableHuntDestination> destinationConfirmed;
        private System.Action destinationBack;
        private int selectedDestinationIndex;
        private string destinationStatus = string.Empty;

        public bool IsOpen => gameObject.activeSelf;
        public int SquadCount => GetSquadHunterIds().Count;
        public int DestinationCount => destinationAvailability.Count;
        public int AvailableDestinationCount => CountAvailableDestinations();
        public int SelectedDestinationIndex => selectedDestinationIndex;
        public int InspectedHunterId { get; private set; }

        public static TabletopHuntDeparturePanel3D Create(Transform parent)
        {
            var gameObject = new GameObject("TabletopHuntDeparturePanel3D");
            gameObject.transform.SetParent(parent, false);
            var panel = gameObject.AddComponent<TabletopHuntDeparturePanel3D>();
            gameObject.SetActive(false);
            return panel;
        }

        public void PresentSquad(Vector3 worldPosition, IReadOnlyList<HunterInstance> availableHunters, IReadOnlyList<int> initialHunterIds, System.Action<IReadOnlyList<int>> onConfirmed, System.Action onCancelled)
        {
            ClearContent();
            transform.position = worldPosition;
            transform.rotation = Quaternion.identity;
            gameObject.SetActive(true);
            squadConfirmed = onConfirmed;
            cancelled = onCancelled;

            squadPrimaryCard = TabletopEventPrimaryCard3D.Create(transform);
            squadPrimaryCard.MoveTo(new Vector3(0f, 0f, 2.30f));
            ShowSquadInstructions();

            int hunterCount = availableHunters?.Count ?? 0;
            int rosterRows = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(1, hunterCount) / 4f));
            squadGrid = CreateHunterGrid(new Vector3(0f, 0f, 0.20f), 4, 1, "远 征 小 队");
            rosterGrid = CreateHunterGrid(new Vector3(0f, 0f, -1.15f - (rosterRows - 1) * 0.38f), 4, rosterRows, "可 用 猎 人");

            var cardsByHunterId = new Dictionary<int, HuntDepartureHunterCard3D>();
            if (availableHunters != null)
                foreach (HunterInstance hunter in availableHunters)
                {
                    if (hunter == null || !hunter.IsAvailable || cardsByHunterId.ContainsKey(hunter.InstanceId))
                        continue;
                    HuntDepartureHunterCard3D card = HuntDepartureHunterCard3D.Create(hunter, transform);
                    card.ConfigureDropScope(HunterDropScope);
                    card.PlacementChanged = RefreshSquadActionCard;
                    card.InspectionRequested = inspectedCard => ShowHunterDetails(inspectedCard.Hunter);
                    hunterCards.Add(card);
                    cardsByHunterId.Add(hunter.InstanceId, card);
                }

            if (initialHunterIds != null)
                foreach (int hunterId in initialHunterIds)
                {
                    if (squadGrid.GetFirstEmptySlot() == null || !cardsByHunterId.Remove(hunterId, out HuntDepartureHunterCard3D card)) continue;
                    squadGrid.TryPlaceCard(card);
                }
            if (availableHunters != null)
                foreach (HunterInstance hunter in availableHunters)
                {
                    if (hunter == null || !cardsByHunterId.Remove(hunter.InstanceId, out HuntDepartureHunterCard3D card)) continue;
                    rosterGrid.TryPlaceCard(card);
                }

            float actionZ = -2.55f - (rosterRows - 1) * (CardView3D.CH + 0.12f);
            continueCard = TabletopEventChoiceCard3D.Create(transform, new Vector3(-0.82f, 0f, actionZ));
            TabletopEventChoiceCard3D cancelCard = TabletopEventChoiceCard3D.Create(transform, new Vector3(0.82f, 0f, actionZ));
            cancelCard.Present("返回营地", "保留当前营地状态", true, "点击取消整备", () => cancelled?.Invoke());
            RefreshSquadActionCard();
        }

        public void PresentDestinations(Vector3 worldPosition, IReadOnlyList<PlayableHuntDestination> availableDestinations, IReadOnlyList<HunterInstance> selectedHunters, int selectedIndex, string status, System.Action<PlayableHuntDestination> onConfirmed, System.Action onBack, System.Action onCancelled)
        {
            var projections = new List<PlayableHuntDestinationAvailability>();
            if (availableDestinations != null)
                foreach (PlayableHuntDestination destination in availableDestinations)
                    if (destination != null)
                        projections.Add(new PlayableHuntDestinationAvailability(destination, true, string.Empty));
            PresentDestinationProjections(worldPosition, projections, selectedHunters, selectedIndex, status, onConfirmed, onBack, onCancelled);
        }

        public void PresentDestinationProjections(Vector3 worldPosition, IReadOnlyList<PlayableHuntDestinationAvailability> projections, IReadOnlyList<HunterInstance> selectedHunters, int selectedIndex, string status, System.Action<PlayableHuntDestination> onConfirmed, System.Action onBack, System.Action onCancelled)
        {
            ClearContent();
            transform.position = worldPosition;
            transform.rotation = Quaternion.identity;
            gameObject.SetActive(true);
            destinationConfirmed = onConfirmed;
            destinationBack = onBack;
            cancelled = onCancelled;
            destinationStatus = status ?? string.Empty;
            if (projections != null)
                foreach (PlayableHuntDestinationAvailability projection in projections)
                    if (projection.Destination != null)
                        destinationAvailability.Add(projection);
            if (selectedHunters != null)
                foreach (HunterInstance hunter in selectedHunters)
                    if (hunter != null)
                        squadHunters.Add(hunter);
            selectedDestinationIndex = ResolveSelectedDestination(selectedIndex);
            BuildDestinationCards();
        }

        public void Close()
        {
            ClearContent();
            gameObject.SetActive(false);
        }

        private SlotGrid CreateHunterGrid(Vector3 localPosition, int columns, int rows, string label)
        {
            SlotGrid grid = SlotGrid.Create(transform, localPosition, columns, rows, CardView3D.CW + 0.08f, CardView3D.CH + 0.08f, 0.12f, false, CardCategory.HunterProfile);
            grid.DropScope = HunterDropScope;
            foreach (CardSlot slot in grid.Slots)
                slot.DropScope = HunterDropScope;
            grid.AddLabel(label);
            return grid;
        }

        private void RefreshSquadActionCard()
        {
            if (continueCard == null)
                return;
            List<int> hunterIds = GetSquadHunterIds();
            bool canDepart = DepartureRules.CanDepart(hunterIds, out string reason);
            string status = canDepart ? $"{hunterIds.Count}/4 · 点击选择目的地" : reason;
            continueCard.Present("选择路线", "确认远征名册并查看可用地区", canDepart, status, () => squadConfirmed?.Invoke(GetSquadHunterIds()));
        }

        private void ShowSquadInstructions()
        {
            InspectedHunterId = 0;
            squadPrimaryCard?.Present("组建狩猎小队", "把猎人卡拖入四个远征槽。\n点击猎人卡可检查伤势、装备和噪音。\n卡槽顺序会成为小队展示顺序。", "1–4 名可用猎人", TabletopEventPrimaryTone.Narrative);
        }

        private void ShowHunterDetails(HunterInstance hunter)
        {
            if (squadPrimaryCard == null || hunter == null) return;
            HuntDepartureHunterPresentation presentation = HuntDepartureHunterPresentation.Create(hunter);
            InspectedHunterId = hunter.InstanceId;
            squadPrimaryCard.Present(presentation.Title, presentation.Body, presentation.Footer, TabletopEventPrimaryTone.Check);
        }

        private List<int> GetSquadHunterIds()
        {
            var hunterIds = new List<int>();
            if (squadGrid == null)
                return hunterIds;
            foreach (CardSlot slot in squadGrid.Slots)
                if (slot.OccupantCard is HuntDepartureHunterCard3D card && card.Hunter != null)
                    hunterIds.Add(card.Hunter.InstanceId);
            return hunterIds;
        }

        private void BuildDestinationCards()
        {
            foreach (Transform child in transform)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }

            TabletopEventPrimaryCard3D primary = TabletopEventPrimaryCard3D.Create(transform);
            string primaryFooter = string.IsNullOrWhiteSpace(destinationStatus) ? (CountAvailableDestinations() == 0 && destinationAvailability.Count > 0 ? "当前年份没有可出发地区" : "路线情报") : destinationStatus;
            primary.Present("选择狩猎地区", "地区决定这次生成的地块、事件与常见资源。\n确认前可以返回重新编队。", primaryFooter, TabletopEventPrimaryTone.Check);

            var cards = new List<TabletopEventChoicePresentation>();
            for (int index = 0; index < destinationAvailability.Count; index++)
            {
                int destinationIndex = index;
                PlayableHuntDestinationAvailability projection = destinationAvailability[index];
                PlayableHuntDestination destination = projection.Destination;
                bool available = projection.IsAvailable;
                bool selected = index == selectedDestinationIndex;
                string body = $"{destination.Description}\n\n常见收获 · {destination.ResourceHint}\n风险 · {destination.DangerHint}";
                PlayableHuntNoiseProfile noiseProfile = destination.HuntContent != null ? destination.HuntContent.NoiseProfile : null;
                if (noiseProfile != null && noiseProfile.TryCreatePlan(squadHunters, out NoiseCheckPlan plan))
                    body += $"\n预计基础风险 · {plan.DangerCardCount}/{plan.DeckSize} 张危险牌（噪音 {plan.NoiseScore}；效果在抽牌前结算）";
                string status = available ? (selected ? "◆ 已选择" : "◇ 点击选择") : $"🔒 {projection.Reason}";
                if (!available)
                    body += $"\n\n解锁条件 · {projection.Reason}";
                cards.Add(new TabletopEventChoicePresentation(destination.DisplayName, body, available, status, () => SelectDestination(destinationIndex)));
            }

            bool hasSelection = selectedDestinationIndex >= 0 && selectedDestinationIndex < destinationAvailability.Count && destinationAvailability[selectedDestinationIndex].IsAvailable;
            cards.Add(new TabletopEventChoicePresentation("确认出发", "提交名册并进入所选地区", hasSelection, hasSelection ? "点击启程" : "没有可用地区", ConfirmDestination));
            cards.Add(new TabletopEventChoicePresentation("重新编队", "返回猎人卡槽", true, string.Empty, () => destinationBack?.Invoke()));
            cards.Add(new TabletopEventChoicePresentation("取消远征", "返回营地桌面", true, string.Empty, () => cancelled?.Invoke()));

            int count = cards.Count;
            for (int index = 0; index < count; index++)
            {
                TabletopEventChoicePresentation presentation = cards[index];
                TabletopEventChoiceCard3D card = TabletopEventChoiceCard3D.Create(transform, TabletopEventLayout.GetChoiceLocalPosition(index, count));
                card.Present(presentation.Title, presentation.Body, presentation.Interactable, presentation.Status, presentation.Selected);
            }
        }

        private void SelectDestination(int index)
        {
            if (index < 0 || index >= destinationAvailability.Count || !destinationAvailability[index].IsAvailable || index == selectedDestinationIndex)
                return;
            selectedDestinationIndex = index;
            destinationStatus = string.Empty;
            BuildDestinationCards();
        }

        private void ConfirmDestination()
        {
            if (selectedDestinationIndex < 0 || selectedDestinationIndex >= destinationAvailability.Count || !destinationAvailability[selectedDestinationIndex].IsAvailable)
                return;
            destinationConfirmed?.Invoke(destinationAvailability[selectedDestinationIndex].Destination);
        }

        private int ResolveSelectedDestination(int selectedIndex)
        {
            if (selectedIndex >= 0 && selectedIndex < destinationAvailability.Count && destinationAvailability[selectedIndex].IsAvailable)
                return selectedIndex;
            return FindFirstAvailableDestination();
        }

        private int FindFirstAvailableDestination()
        {
            for (int index = 0; index < destinationAvailability.Count; index++)
                if (destinationAvailability[index].IsAvailable)
                    return index;
            return -1;
        }

        private int CountAvailableDestinations()
        {
            int count = 0;
            foreach (PlayableHuntDestinationAvailability projection in destinationAvailability)
                if (projection.IsAvailable)
                    count++;
            return count;
        }

        private void ClearContent()
        {
            foreach (Transform child in transform)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            rosterGrid = null;
            squadGrid = null;
            squadPrimaryCard = null;
            continueCard = null;
            InspectedHunterId = 0;
            hunterCards.Clear();
            destinationAvailability.Clear();
            squadHunters.Clear();
            squadConfirmed = null;
            destinationConfirmed = null;
            destinationBack = null;
            cancelled = null;
        }

        private void OnDestroy()
        {
            hunterCards.Clear();
            destinationAvailability.Clear();
            squadHunters.Clear();
        }
    }
}
