using System.Collections.Generic;
using System.Threading;
using Cards3D;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.Data;
using HuntingInDarkness.Settlement;
using HuntingInDarkness.ViewLayer.Presentation;
using UnityEngine;

namespace UI
{
    /// <summary>营地发明区：资格牌进入牌堆，点击牌堆随机抽取至多两张供玩家选择。</summary>
    public class InventionZone : MonoBehaviour
    {
        [SerializeField] private SlotGrid _grid;
        [SerializeField] private SlotGrid masteredGrid;
        [SerializeField] private SlotGrid candidateGrid;
        [SerializeField] private Transform previewRoot;
        [SerializeField] private SlotGrid previewGrid;
        [SerializeField] private InventionDrawCandidatesCard3D drawEntry;
        [SerializeField, Min(1)] private int masteredGridColumns = 4;
        [SerializeField, Min(1)] private int candidateGridColumns = 3;
        [SerializeField, Min(1)] private int previewGridColumns = 4;
        [SerializeField] private SettlementExpansionAreaController candidateExpansion;

        public System.Action<InventionCard3D> OnInventionEffectRequested;
        public System.Action<InventionCard3D> OnInventionUnlockRequested;

        private readonly List<InventionCard3D> drawnCards = new();
        private readonly List<InventionCard3D> masteredCards = new();
        private readonly List<InventionCard3D> previewCards = new();
        private readonly List<InventionData> eligibleInventions = new();
        private readonly List<InventionData> pendingInventions = new();
        private readonly HashSet<int> pendingSelectedSlots = new();
        private InventionSystem inventionSystem;
        private InventionDeckCard3D deckCard;
        private CancellationTokenSource drawCancellationSource;
        private bool isDrawing;
        private int pendingRequiredCount;
        public int EligibleCount => eligibleInventions.Count;
        public int DrawnCount => drawnCards.Count;
        public bool HasPendingDraw => pendingInventions.Count > 0;
        public int RemainingDrawCount => Mathf.Max(0, pendingRequiredCount - pendingSelectedSlots.Count);
        public bool CanDrawCandidates => !isDrawing && CandidateGrid != null && (HasPendingDraw || drawnCards.Count == 0 && eligibleInventions.Count > 0);
        public IReadOnlyList<InventionData> DrawnInventions => drawnCards.FindAll(card => card != null).ConvertAll(card => card.Data);
        public IReadOnlyList<InventionData> AvailableInventionPreview => eligibleInventions;
        public Transform PreviewRoot => previewRoot;
        public SlotGrid PreviewGrid => previewGrid;
        public InventionDrawCandidatesCard3D DrawEntry => drawEntry;

        public void SetRefs(SlotGrid grid)
        {
            _grid = grid;
            candidateGrid ??= grid;
        }

        public void SetProductionRefs(SlotGrid mastered, SlotGrid candidates, SettlementExpansionAreaController expansion)
        {
            masteredGrid = mastered;
            candidateGrid = candidates;
            candidateExpansion = expansion;
        }

        public void SetPreviewRefs(Transform root, SlotGrid grid, InventionDrawCandidatesCard3D entry)
        {
            previewRoot = root;
            previewGrid = grid;
            drawEntry = entry;
        }

        public void Fill(InventionSystem system)
        {
            Clear();
            inventionSystem = system;
            if (inventionSystem == null) return;
            if (CandidateGrid == null) throw new System.InvalidOperationException("InventionZone 缺少 candidateGrid。");
            if (MasteredGrid == null) throw new System.InvalidOperationException("InventionZone 缺少 masteredGrid。");
            ValidatePreviewRefs();
            deckCard = InventionDeckCard3D.Create(CandidateParent);
            deckCard.DrawRequested = DrawCandidates;
            deckCard.PreviewRequested = null;
            if (drawEntry != null) drawEntry.DrawRequested = DrawCandidates;
            CandidateGrid.TryPlaceCard(deckCard);
            RefreshCards();
        }

        public void RefreshCards()
        {
            if (inventionSystem == null) return;
            bool unlockedDrawnCard = drawnCards.Exists(card => card != null && inventionSystem.IsUnlocked(card.Data));
            if (unlockedDrawnCard && pendingInventions.Count == 0) ClearDrawnCards();
            SynchronizeMasteredCards();

            foreach (InventionCard3D card in drawnCards)
            {
                if (card == null) continue;
                bool canUnlock = inventionSystem.CanUnlock(card.Data, out string reason);
                card.ConfigureState(false, canUnlock, reason);
                card.ConfigureInspectionContent(GetInspectionContent(card.Data));
            }
            RebuildEligiblePool();
            deckCard?.Present(eligibleInventions.Count, drawnCards.Count > 0 && pendingInventions.Count == 0, pendingInventions.Count > 0);
            drawEntry?.Present(eligibleInventions.Count, drawnCards.Count > 0 && pendingInventions.Count == 0, pendingInventions.Count > 0);
            if (candidateExpansion != null && candidateExpansion.IsOpen) RebuildReadOnlyPreview();
        }

        public void OpenCandidatePreview()
        {
            candidateExpansion?.Open();
            RebuildReadOnlyPreview();
            deckCard?.Present(eligibleInventions.Count, drawnCards.Count > 0 && pendingInventions.Count == 0, pendingInventions.Count > 0);
            drawEntry?.Present(eligibleInventions.Count, drawnCards.Count > 0 && pendingInventions.Count == 0, pendingInventions.Count > 0);
        }

        public void CloseCandidatePreview() => candidateExpansion?.Close();

        public void DrawCandidates()
        {
            DrawCandidatesAsync().Forget();
        }

        public async UniTask DrawCandidatesAsync(CancellationToken cancellationToken = default)
        {
            if (isDrawing || drawnCards.Count > 0 && pendingInventions.Count == 0 || eligibleInventions.Count == 0 && pendingInventions.Count == 0 || CandidateGrid == null) return;
            if (pendingInventions.Count == 0)
            {
                pendingInventions.AddRange(eligibleInventions);
                for (int index = pendingInventions.Count - 1; index > 0; index--)
                {
                    int swapIndex = Random.Range(0, index + 1);
                    (pendingInventions[index], pendingInventions[swapIndex]) = (pendingInventions[swapIndex], pendingInventions[index]);
                }
                pendingRequiredCount = Mathf.Min(2, pendingInventions.Count);
            }
            CandidateGrid.EnsureCapacityPreservingCards(1 + pendingRequiredCount, candidateGridColumns);
            CancellationTokenSource source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, this.GetCancellationTokenOnDestroy());
            drawCancellationSource = source;
            isDrawing = true;
            deckCard?.Present(eligibleInventions.Count, false, true);
            drawEntry?.Present(eligibleInventions.Count, false, true);
            PhysicalInteractionScreenView screen = null;
            try
            {
                screen = await PhysicalInteractionScreenView.OpenAsync(source.Token);
                int count = pendingInventions.Count;
                await screen.BeginCardSelectionAsync("营地发明", $"从 {count} 张背牌中选择 {pendingRequiredCount} 项发明", count, screen.OperationToken);
                foreach (int index in pendingSelectedSlots)
                {
                    InventionCard3D card = FindDrawnCardForSlot(index);
                    if (card != null) await screen.RevealSelectionAsync(index, GetInventionRevealText(card.Data), screen.OperationToken, card.Data.inventionName);
                }
                while (pendingSelectedSlots.Count < pendingRequiredCount)
                {
                    var available = new List<int>();
                    for (int index = 0; index < count; index++)
                        if (!pendingSelectedSlots.Contains(index)) available.Add(index);
                    int selectedIndex = await screen.WaitForCardSelectionAsync($"已选 {pendingSelectedSlots.Count}/{pendingRequiredCount} 项", available, screen.OperationToken);
                    InventionData invention = pendingInventions[selectedIndex];
                    InventionCard3D card = CreateCandidateCard(invention);
                    if (!CandidateGrid.TryPlaceCard(card))
                    {
                        Destroy(card.gameObject);
                        throw new System.InvalidOperationException("发明候选网格无法放置已选卡牌。");
                    }
                    card.ConfigureState(false, true, string.Empty);
                    drawnCards.Add(card);
                    pendingSelectedSlots.Add(selectedIndex);
                    await screen.RevealSelectionAsync(selectedIndex, GetInventionRevealText(invention), screen.OperationToken, invention.inventionName);
                }
                pendingInventions.Clear();
                pendingSelectedSlots.Clear();
                pendingRequiredCount = 0;
                RebuildEligiblePool();
                if (candidateExpansion != null && candidateExpansion.IsOpen) RebuildReadOnlyPreview();
            }
            catch (System.OperationCanceledException)
            {
                // Keep the selected slots and their candidates so the next entry resumes this draw.
            }
            finally
            {
                if (screen != null)
                {
                    screen.EndCardSelection();
                    screen.Close();
                }
                isDrawing = false;
                if (drawCancellationSource == source) drawCancellationSource = null;
                source.Dispose();
                RebuildEligiblePool();
                deckCard?.Present(eligibleInventions.Count, drawnCards.Count > 0 && pendingInventions.Count == 0, pendingInventions.Count > 0);
                drawEntry?.Present(eligibleInventions.Count, drawnCards.Count > 0 && pendingInventions.Count == 0, pendingInventions.Count > 0);
            }
        }

        public void Clear()
        {
            drawCancellationSource?.Cancel();
            drawCancellationSource = null;
            pendingInventions.Clear();
            pendingSelectedSlots.Clear();
            pendingRequiredCount = 0;
            ClearDrawnCards();
            ClearMasteredCards();
            ClearReadOnlyPreview();
            if (deckCard != null) DestroyObject(deckCard.gameObject);
            deckCard = null;
            eligibleInventions.Clear();
            if (CandidateGrid != null)
                foreach (CardSlot slot in CandidateGrid.Slots) slot.ClearCard();
            if (MasteredGrid != null && MasteredGrid != CandidateGrid)
                foreach (CardSlot slot in MasteredGrid.Slots) slot.ClearCard();
            inventionSystem = null;
        }

        private void RebuildEligiblePool()
        {
            eligibleInventions.Clear();
            if (inventionSystem == null) return;
            foreach (InventionData invention in inventionSystem.AllInventions)
            {
                if (invention == null || inventionSystem.IsUnlocked(invention) || drawnCards.Exists(card => card != null && card.Data == invention)) continue;
                if (inventionSystem.CanUnlock(invention, out _)) eligibleInventions.Add(invention);
            }
        }

        private InventionCard3D CreateCandidateCard(InventionData invention)
        {
            InventionCard3D card = EntityCreator.CreateInventionCard(invention, CandidateParent);
            card.OnEffectMenuRequested = selected => OnInventionEffectRequested?.Invoke(selected);
            card.OnUnlockRequested = selected => OnInventionUnlockRequested?.Invoke(selected);
            card.ConfigureInspectionContent(GetInspectionContent(invention));
            return card;
        }

        private InventionCard3D FindDrawnCardForSlot(int slot)
        {
            if (slot < 0 || slot >= pendingInventions.Count) return null;
            InventionData data = pendingInventions[slot];
            return drawnCards.Find(card => card != null && card.Data == data);
        }

        private string GetInventionRevealText(InventionData invention) => $"{invention.inventionName}\n{invention.effectDescription}";

        public CardInspectionContent GetInspectionContent(InventionData invention)
        {
            var sections = new List<string>();
            if (!string.IsNullOrWhiteSpace(invention.description)) sections.Add(invention.description);
            if (!string.IsNullOrWhiteSpace(invention.effectDescription)) sections.Add(invention.effectDescription);
            sections.Add($"费用：{FormatCosts(invention)}");
            bool unlocked = inventionSystem.IsUnlocked(invention);
            string reason = string.Empty;
            bool canUnlock = !unlocked && inventionSystem.CanUnlock(invention, out reason);
            string status = reason;
            if (unlocked)
                status = "已掌握";
            else if (canUnlock)
                status = "当前候选";
            sections.Add($"状态：{status}");
            List<InventionData> children = inventionSystem.GetChildren(invention);
            if (children.Count > 0)
            {
                var followUps = new List<string>();
                foreach (InventionData child in children)
                {
                    if (child == null) continue;
                    var remainingPrerequisites = new List<string>();
                    foreach (InventionData prerequisite in child.prerequisites)
                        if (prerequisite != null && prerequisite != invention && !inventionSystem.IsUnlocked(prerequisite)) remainingPrerequisites.Add(prerequisite.inventionName);
                    string childStatus;
                    if (inventionSystem.IsUnlocked(child))
                        childStatus = "已掌握";
                    else if (remainingPrerequisites.Count > 0)
                        childStatus = $"仍需前置：{string.Join("、", remainingPrerequisites)}";
                    else
                        childStatus = "仍需单独解锁";
                    followUps.Add($"{child.inventionName}：{childStatus}；费用 {FormatCosts(child)}");
                }
                sections.Add($"后续发明（仍需满足前置与费用）：\n{string.Join("\n", followUps)}");
            }
            return new CardInspectionContent(invention.inventionName, string.Join("\n\n", sections), inventionSystem.IsUnlocked(invention) ? "已掌握" : "费用与前置条件会在解锁时再次检查");
        }

        private static string FormatCosts(InventionData invention)
        {
            if (invention.costs == null || invention.costs.Count == 0) return "无";
            var costs = new List<string>();
            foreach (InventionCost cost in invention.costs)
                if (cost?.resource != null) costs.Add($"{cost.resource.itemName} ×{cost.count}");
            return costs.Count == 0 ? "无" : string.Join("、", costs);
        }

        private void ClearDrawnCards()
        {
            foreach (InventionCard3D card in drawnCards)
            {
                if (card == null) continue;
                card.CurrentSlot?.ClearCard();
                DestroyObject(card.gameObject);
            }
            drawnCards.Clear();
        }

        private void SynchronizeMasteredCards()
        {
            var desired = new HashSet<InventionData>();
            foreach (InventionData invention in inventionSystem.AllInventions)
                if (invention != null && inventionSystem.IsUnlocked(invention))
                    desired.Add(invention);

            MasteredGrid?.EnsureCapacityPreservingCards(desired.Count, masteredGridColumns);

            for (int index = masteredCards.Count - 1; index >= 0; index--)
            {
                InventionCard3D card = masteredCards[index];
                if (card != null && desired.Remove(card.Data))
                {
                    card.ConfigureState(true, false, string.Empty);
                    card.ConfigureInspectionContent(GetInspectionContent(card.Data));
                    continue;
                }
                if (card != null)
                {
                    card.CurrentSlot?.ClearCard();
                    DestroyObject(card.gameObject);
                }
                masteredCards.RemoveAt(index);
            }

            foreach (InventionData invention in inventionSystem.AllInventions)
            {
                if (!desired.Contains(invention)) continue;
                InventionCard3D card = EntityCreator.CreateInventionCard(invention, MasteredParent);
                card.OnEffectMenuRequested = selected => OnInventionEffectRequested?.Invoke(selected);
                card.ConfigureState(true, false, string.Empty);
                card.ConfigureInspectionContent(GetInspectionContent(invention));
                if (MasteredGrid != null && MasteredGrid.TryPlaceCard(card))
                    masteredCards.Add(card);
                else
                    DestroyObject(card.gameObject);
            }
        }

        private void ClearMasteredCards()
        {
            foreach (InventionCard3D card in masteredCards)
            {
                if (card == null) continue;
                card.CurrentSlot?.ClearCard();
                DestroyObject(card.gameObject);
            }
            masteredCards.Clear();
        }

        private void RebuildReadOnlyPreview()
        {
            if (candidateExpansion?.ContentRoot == null || inventionSystem == null) return;
            ValidatePreviewRefs();
            ClearReadOnlyPreview();
            int count = eligibleInventions.Count;
            previewGrid.EnsureCapacityPreservingCards(count, previewGridColumns);
            Vector3 previewPosition = previewGrid.transform.localPosition;
            previewPosition.z = -((previewGrid.Rows - 1) * (previewGrid.SlotH + previewGrid.Gap) + previewGrid.SlotH) * 0.5f;
            previewGrid.transform.localPosition = previewPosition;
            candidateExpansion.RefreshFrame();
            foreach (InventionData invention in eligibleInventions)
            {
                InventionCard3D card = EntityCreator.CreateInventionCard(invention, previewRoot);
                card.ConfigurePreview();
                card.ConfigureInspectionContent(GetInspectionContent(invention));
                card.OnEffectMenuRequested = null;
                card.OnUnlockRequested = null;
                if (previewGrid.TryPlaceCard(card))
                    previewCards.Add(card);
                else
                    DestroyObject(card.gameObject);
            }
        }

        private void ValidatePreviewRefs()
        {
            if (candidateExpansion == null) return;
            if (previewRoot == null || previewGrid == null || drawEntry == null) throw new System.InvalidOperationException("InventionZone 缺少持久化 previewRoot、previewGrid 或 drawEntry 引用。");
        }

        private void ClearReadOnlyPreview()
        {
            foreach (InventionCard3D card in previewCards)
            {
                if (card == null) continue;
                card.CurrentSlot?.ClearCard();
                DestroyObject(card.gameObject);
            }
            previewCards.Clear();
        }

        private static void DestroyObject(Object target)
        {
            if (target == null) return;
            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }

        public SlotGrid MasteredGrid => masteredGrid ?? _grid;
        public SlotGrid CandidateGrid => candidateGrid ?? _grid;
        private Transform MasteredParent => MasteredGrid != null ? MasteredGrid.transform.parent : transform;
        private Transform CandidateParent => CandidateGrid != null ? CandidateGrid.transform.parent : transform;
    }
}
