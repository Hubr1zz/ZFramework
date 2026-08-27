using System.Collections.Generic;
using System.Linq;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Hunt;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.ViewLayer.Hunt;
using HuntingInDarkness.ViewLayer.Tabletop;
using UnityEngine;

namespace UI.Hunt
{
    /// <summary>地图边缘的狩猎状态桌；实体猎人卡只提交行动猎人选择命令。</summary>
    public sealed class HuntStatusBoard3D : MonoBehaviour
    {
        private readonly List<TabletopEventChoiceCard3D> hunterCards = new();
        private HuntManager manager;
        private TabletopEventPrimaryCard3D summaryCard;
        private HuntCollectibleTray3D collectibleTray;

        public int ActiveHunterCardCount => hunterCards.Count;
        public int CollectibleCardCount => collectibleTray?.CardCount ?? 0;
        public string CollectibleOwnerName => collectibleTray?.OwnerName ?? string.Empty;
        public string SelectedHunterName => manager?.SelectedHunter?.Name ?? string.Empty;

        public bool TryGetHunterAnchor(int hunterId, out Vector3 anchor)
        {
            anchor = default;
            if (hunterId <= 0 || manager?.ActiveHunters == null) return false;
            int count = Mathf.Min(hunterCards.Count, manager.ActiveHunters.Count);
            for (int index = 0; index < count; index++)
            {
                HunterInstance hunter = manager.ActiveHunters[index];
                TabletopEventChoiceCard3D card = hunterCards[index];
                if (hunter == null || hunter.InstanceId != hunterId || card == null || !card.gameObject.activeInHierarchy) continue;
                anchor = card.transform.position;
                return true;
            }
            return false;
        }

        public static HuntStatusBoard3D Create(Transform parent)
        {
            var boardObject = new GameObject("HuntStatusBoard3D");
            boardObject.transform.SetParent(parent, false);
            return boardObject.AddComponent<HuntStatusBoard3D>();
        }

        public void Initialize(HuntManager huntManager)
        {
            manager = huntManager;
            Rebuild();
        }

        public void Refresh()
        {
            if (manager == null)
                return;
            if (hunterCards.Count != Mathf.Min(manager.ActiveHunters?.Count ?? 0, HuntStatusBoardLayout.MaximumHunterCards))
            {
                Rebuild();
                return;
            }

            PresentSummary();
            for (int index = 0; index < hunterCards.Count; index++)
                PresentHunterCard(hunterCards[index], manager.ActiveHunters[index]);
            collectibleTray?.Present(manager.SelectedHunter);
        }

        private void Rebuild()
        {
            ClearCards();
            if (manager == null)
                return;

            transform.localPosition = GetBoardLocalPosition();
            summaryCard = TabletopEventPrimaryCard3D.Create(transform);
            summaryCard.MoveTo(HuntStatusBoardLayout.SummaryCardLocalPosition);
            int hunterCount = Mathf.Min(manager.ActiveHunters?.Count ?? 0, HuntStatusBoardLayout.MaximumHunterCards);
            for (int index = 0; index < hunterCount; index++)
            {
                HunterInstance hunter = manager.ActiveHunters[index];
                TabletopEventChoiceCard3D card = TabletopEventChoiceCard3D.Create(transform, HuntStatusBoardLayout.GetHunterCardLocalPosition(index));
                int hunterId = hunter.InstanceId;
                card.Clicked = () => SelectHunter(hunterId);
                hunterCards.Add(card);
            }
            collectibleTray = HuntCollectibleTray3D.Create(transform);
            Refresh();
        }

        private void PresentSummary()
        {
            if (summaryCard == null)
                return;
            if (!manager.HasLivingHunter)
            {
                summaryCard.Present(
                    "远征队失去行动能力",
                    "当前没有可继续探索或采集的猎人。\n\n本次狩猎只能结束，无法再翻开或移动地块。",
                    "使用地图左侧的实体回营卡结算本次远征",
                    TabletopEventPrimaryTone.Failure);
                return;
            }
            int revealedCount = manager.Map?.Values.Count(tile => tile.State == TileState.Revealed) ?? 0;
            int tileCount = manager.Map?.Count ?? 0;
            string noiseSummary = string.Empty;
            if (manager.NoiseProfile != null && manager.NoiseProfile.TryCreatePlan(manager.ActiveHunters, out NoiseCheckPlan noisePlan))
                noiseSummary = $"\n当前基础风险 · {noisePlan.DangerCardCount}/{noisePlan.DeckSize} 张危险牌";
            PlayableHuntNoiseResolution lastNoise = manager.LastNoiseResolution;
            if (lastNoise.IsResolved)
                noiseSummary += lastNoise.IsDanger ? $"\n上次抽牌 · 危险（{lastNoise.EventDisplayName}）" : "\n上次抽牌 · 安静";
            summaryCard.Present(
                PlayableHuntDestinationRuntime.ActiveDisplayName,
                $"小队位置 · {manager.SquadPosition.x}, {manager.SquadPosition.y}\n已探索 · {revealedCount}/{tileCount}{noiseSummary}\n\n点击蓝色地块翻开地图。\n点击猎人卡指定事件与采集的行动者。",
                "地图左侧的实体回营卡可结束探索",
                TabletopEventPrimaryTone.Check);
        }

        private void PresentHunterCard(TabletopEventChoiceCard3D card, HunterInstance hunter)
        {
            if (card == null || hunter == null)
                return;
            bool selected = ReferenceEquals(manager.SelectedHunter, hunter);
            bool available = hunter.IsAlive;
            HuntCollectiblePresentation collectibles = HuntCollectiblePresentation.Create(hunter.Collectibles);
            string body = $"头 {hunter.HP.head}/{hunter.MaxHP.head}  躯 {hunter.HP.body}/{hunter.MaxHP.body}\n臂 {hunter.HP.arms}/{hunter.MaxHP.arms}  腿 {hunter.HP.legs}/{hunter.MaxHP.legs}\n意志 {hunter.Willpower}/{hunter.WillpowerMax}\n携带 {collectibles.TotalCount} · {collectibles.Summary}";
            string status = !available ? "已失去行动能力" : selected ? "当前行动猎人" : "点击选为行动猎人";
            card.Present(hunter.Name, body, available, status, card.Clicked);
        }

        private void SelectHunter(int hunterId)
        {
            if (manager == null || PlayableHuntInputGuard.IsBlocked)
                return;
            manager.SelectHunter(hunterId);
            Refresh();
        }

        private Vector3 GetBoardLocalPosition()
        {
            if (manager.Map == null || manager.Map.Count == 0)
                return new Vector3(HuntStatusBoardLayout.MapEdgeOffset, 0.36f, 0f);
            float maximumX = float.MinValue;
            float minimumZ = float.MaxValue;
            float maximumZ = float.MinValue;
            foreach (Vector2Int coordinate in manager.Map.Keys)
            {
                Vector3 position = manager.TileToWorld(coordinate);
                maximumX = Mathf.Max(maximumX, position.x);
                minimumZ = Mathf.Min(minimumZ, position.z);
                maximumZ = Mathf.Max(maximumZ, position.z);
            }
            Vector3 worldPosition = new(maximumX + HuntStatusBoardLayout.MapEdgeOffset, 0.36f, (minimumZ + maximumZ) * 0.5f);
            return transform.parent != null ? transform.parent.InverseTransformPoint(worldPosition) : worldPosition;
        }

        private void ClearCards()
        {
            summaryCard = null;
            collectibleTray = null;
            hunterCards.Clear();
            foreach (Transform child in transform)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
        }

        private void OnDestroy() => ClearCards();
    }
}
