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
        private readonly List<PlayableHuntDestination> destinations = new();
        private readonly List<HunterInstance> squadHunters = new();
        private SlotGrid rosterGrid;
        private SlotGrid squadGrid;
        private TabletopEventChoiceCard3D continueCard;
        private System.Action<IReadOnlyList<int>> squadConfirmed;
        private System.Action cancelled;
        private System.Action<PlayableHuntDestination> destinationConfirmed;
        private System.Action destinationBack;
        private int selectedDestinationIndex;
        private string destinationStatus = string.Empty;

        public bool IsOpen => gameObject.activeSelf;
        public int SquadCount => GetSquadHunterIds().Count;
        public int DestinationCount => destinations.Count;
        public int SelectedDestinationIndex => selectedDestinationIndex;

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

            TabletopEventPrimaryCard3D primary = TabletopEventPrimaryCard3D.Create(transform);
            primary.MoveTo(new Vector3(0f, 0f, 2.30f));
            primary.Present("组建狩猎小队", "把猎人卡拖入四个远征槽。\n卡槽顺序会成为小队展示顺序。", "1–4 名可用猎人", TabletopEventPrimaryTone.Narrative);

            int hunterCount = availableHunters?.Count ?? 0;
            int rosterRows = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(1, hunterCount) / 4f));
            squadGrid = CreateHunterGrid(new Vector3(0f, 0f, 0.20f), 4, 1, "远 征 小 队");
            rosterGrid = CreateHunterGrid(new Vector3(0f, 0f, -1.15f - (rosterRows - 1) * 0.38f), 4, rosterRows, "可 用 猎 人");

            var selectedIds = new HashSet<int>(initialHunterIds ?? System.Array.Empty<int>());
            if (availableHunters != null)
                foreach (HunterInstance hunter in availableHunters)
                {
                    if (hunter == null || !hunter.IsAvailable)
                        continue;
                    HuntDepartureHunterCard3D card = HuntDepartureHunterCard3D.Create(hunter, transform);
                    card.ConfigureDropScope(HunterDropScope);
                    card.PlacementChanged = RefreshSquadActionCard;
                    bool wantsSquad = selectedIds.Contains(hunter.InstanceId) && squadGrid.GetFirstEmptySlot() != null;
                    (wantsSquad ? squadGrid : rosterGrid).TryPlaceCard(card);
                    hunterCards.Add(card);
                }

            float actionZ = -2.55f - (rosterRows - 1) * (CardView3D.CH + 0.12f);
            continueCard = TabletopEventChoiceCard3D.Create(transform, new Vector3(-0.82f, 0f, actionZ));
            TabletopEventChoiceCard3D cancelCard = TabletopEventChoiceCard3D.Create(transform, new Vector3(0.82f, 0f, actionZ));
            cancelCard.Present("返回营地", "保留当前营地状态", true, "点击取消整备", () => cancelled?.Invoke());
            RefreshSquadActionCard();
        }

        public void PresentDestinations(Vector3 worldPosition, IReadOnlyList<PlayableHuntDestination> availableDestinations, IReadOnlyList<HunterInstance> selectedHunters, int selectedIndex, string status, System.Action<PlayableHuntDestination> onConfirmed, System.Action onBack, System.Action onCancelled)
        {
            ClearContent();
            transform.position = worldPosition;
            transform.rotation = Quaternion.identity;
            gameObject.SetActive(true);
            destinationConfirmed = onConfirmed;
            destinationBack = onBack;
            cancelled = onCancelled;
            destinationStatus = status ?? string.Empty;
            if (availableDestinations != null)
                foreach (PlayableHuntDestination destination in availableDestinations)
                    if (destination != null)
                        destinations.Add(destination);
            if (selectedHunters != null)
                foreach (HunterInstance hunter in selectedHunters)
                    if (hunter != null)
                        squadHunters.Add(hunter);
            selectedDestinationIndex = destinations.Count == 0 ? -1 : Mathf.Clamp(selectedIndex, 0, destinations.Count - 1);
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
            primary.Present("选择狩猎地区", "地区决定这次生成的地块、事件与常见资源。\n确认前可以返回重新编队。", string.IsNullOrWhiteSpace(destinationStatus) ? "路线情报" : destinationStatus, TabletopEventPrimaryTone.Check);

            var cards = new List<TabletopEventChoicePresentation>();
            for (int index = 0; index < destinations.Count; index++)
            {
                int destinationIndex = index;
                PlayableHuntDestination destination = destinations[index];
                bool selected = index == selectedDestinationIndex;
                string body = $"{destination.Description}\n\n常见收获 · {destination.ResourceHint}\n风险 · {destination.DangerHint}";
                PlayableHuntNoiseProfile noiseProfile = destination.HuntContent != null ? destination.HuntContent.NoiseProfile : null;
                if (noiseProfile != null && noiseProfile.TryCreatePlan(squadHunters, out NoiseCheckPlan plan))
                    body += $"\n预计基础风险 · {plan.DangerCardCount}/{plan.DeckSize} 张危险牌（噪音 {plan.NoiseScore}；效果在抽牌前结算）";
                cards.Add(new TabletopEventChoicePresentation(destination.DisplayName, body, true, selected ? "◆ 已选择" : "◇ 点击选择", () => SelectDestination(destinationIndex)));
            }

            bool hasSelection = selectedDestinationIndex >= 0 && selectedDestinationIndex < destinations.Count;
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
            if (index < 0 || index >= destinations.Count || index == selectedDestinationIndex)
                return;
            selectedDestinationIndex = index;
            destinationStatus = string.Empty;
            BuildDestinationCards();
        }

        private void ConfirmDestination()
        {
            if (selectedDestinationIndex < 0 || selectedDestinationIndex >= destinations.Count)
                return;
            destinationConfirmed?.Invoke(destinations[selectedDestinationIndex]);
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
            continueCard = null;
            hunterCards.Clear();
            destinations.Clear();
            squadHunters.Clear();
            squadConfirmed = null;
            destinationConfirmed = null;
            destinationBack = null;
            cancelled = null;
        }

        private void OnDestroy()
        {
            hunterCards.Clear();
            destinations.Clear();
            squadHunters.Clear();
        }
    }
}
