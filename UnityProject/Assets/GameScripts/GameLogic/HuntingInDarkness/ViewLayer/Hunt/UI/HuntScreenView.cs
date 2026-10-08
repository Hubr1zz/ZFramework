using System;
using System.Collections.Generic;
using System.Threading;
using Core;
using Cysharp.Threading.Tasks;
using GameplayBase;
using HuntingInDarkness.ActionFlow.Hunt;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Hunters;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.ViewLayer.Presentation;
using HuntingInDarkness.ViewLayer.Hunt;
using HuntingInDarkness.ViewLayer.Tabletop;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Hunt
{
    public sealed class HuntScreenView : MonoBehaviour
    {
        private static PlayableHarvestTransaction suspendedHarvestTransaction;
        private static Guid suspendedHarvestSessionId;
        private static Vector2Int suspendedHarvestCoordinate;
        private static int suspendedHarvestPointIndex;
        private static readonly Dictionary<int, string> suspendedHarvestResults = new();
        [SerializeField] private TMP_Text phaseText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text detailText;
        [SerializeField] private TMP_Text instructionText;
        [SerializeField] private Transform hunterContent;
        [SerializeField] private Transform cargoContent;
        [SerializeField] private Transform operationContent;
        [SerializeField] private ScreenEventChoice entryPrefab;
        [SerializeField] private Button refreshButton;
        [SerializeField] private Button retreatButton;

        private readonly List<ScreenEventChoice> hunterEntries = new();
        private readonly List<ScreenEventChoice> cargoEntries = new();
        private readonly List<ScreenEventChoice> operationEntries = new();
        private CancellationTokenSource lifetimeSource;
        private HuntScreenWindow ownerWindow;
        private HuntManager manager;
        private HuntMapVisualizer visualizer;
        private IHuntExplorationPort explorationPort;
        private IPlayableHuntRetreatInput retreatInput;
        private Vector2Int selectedCoordinate;
        private int selectedResourceIndex = -1;
        private bool hasSelectedCoordinate;
        private bool submitting;
        private bool preparingHarvest;
        private bool retreatPreviewOpen;
        private bool consumableSelectionOpen;
        private IDisposable selectionLease;
        private Guid boundSessionId;
        private PlayableHarvestTransaction activeHarvestTransaction;
        private Vector2Int activeHarvestCoordinate;
        private int activeHarvestPointIndex;
        private string pendingAbandonedItemId = string.Empty;

        public static HuntScreenView Current { get; private set; }
        public bool IsBound => manager != null && explorationPort != null && ownerWindow != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Current = null;

        public void BindWindow(HuntScreenWindow window)
        {
            ownerWindow = window ?? throw new ArgumentNullException(nameof(window));
            ValidateReferences();
            lifetimeSource = new CancellationTokenSource();
            if (refreshButton != null) refreshButton.onClick.AddListener(Refresh);
            if (retreatButton != null) retreatButton.onClick.AddListener(OpenRetreatPreview);
            Current = this;
        }

        public void Bind(HuntManager huntManager, HuntMapVisualizer huntVisualizer, IHuntExplorationPort port, IPlayableHuntRetreatInput retreat)
        {
            if (visualizer != null && visualizer != huntVisualizer)
            {
                visualizer.ScreenTileSelected -= OnScreenTileSelected;
                visualizer.SetScreenInteractionEnabled(false);
            }
            if (manager != null && manager != huntManager && manager.OnResourcePointPresentationRequested == OnResourcePointSelected)
                manager.OnResourcePointPresentationRequested = null;
            manager = huntManager ?? throw new ArgumentNullException(nameof(huntManager));
            visualizer = huntVisualizer ?? throw new ArgumentNullException(nameof(huntVisualizer));
            explorationPort = port ?? throw new ArgumentNullException(nameof(port));
            boundSessionId = port.SessionId;
            if (suspendedHarvestTransaction != null && suspendedHarvestSessionId == boundSessionId)
            {
                activeHarvestTransaction = suspendedHarvestTransaction;
                activeHarvestCoordinate = suspendedHarvestCoordinate;
                activeHarvestPointIndex = suspendedHarvestPointIndex;
            }
            else if (suspendedHarvestTransaction != null)
            {
                suspendedHarvestTransaction = null;
                suspendedHarvestResults.Clear();
            }
            retreatInput = retreat ?? throw new ArgumentNullException(nameof(retreat));
            visualizer.ScreenTileSelected -= OnScreenTileSelected;
            visualizer.ScreenTileSelected += OnScreenTileSelected;
            visualizer.SetScreenInteractionEnabled(true);
            manager.OnResourcePointPresentationRequested = OnResourcePointSelected;
            Refresh();
        }

        public void NotifyWindowClosed()
        {
            lifetimeSource?.Cancel();
            lifetimeSource?.Dispose();
            lifetimeSource = null;
            if (visualizer != null)
            {
                visualizer.ScreenTileSelected -= OnScreenTileSelected;
                visualizer.SetScreenInteractionEnabled(false);
            }
            if (manager != null && manager.OnResourcePointPresentationRequested == OnResourcePointSelected)
                manager.OnResourcePointPresentationRequested = null;
            if (activeHarvestTransaction != null)
            {
                if (activeHarvestTransaction.IsCommitted || activeHarvestTransaction.IsCancelled || activeHarvestTransaction.Cancel())
                {
                    suspendedHarvestTransaction = null;
                    suspendedHarvestResults.Clear();
                }
                else
                {
                    suspendedHarvestTransaction = activeHarvestTransaction;
                    suspendedHarvestSessionId = boundSessionId;
                    suspendedHarvestCoordinate = activeHarvestCoordinate;
                    suspendedHarvestPointIndex = activeHarvestPointIndex;
                }
            }
            if (Current == this) Current = null;
            ownerWindow = null;
            manager = null;
            visualizer = null;
            explorationPort = null;
            retreatInput = null;
            selectionLease?.Dispose();
            selectionLease = null;
            activeHarvestTransaction = null;
        }

        public void Refresh()
        {
            if (!IsBound) return;
            phaseText.text = $"狩猎　·　第 {manager.CurrentYear} 年　·　探索 {CountRevealedTiles()} 格　·　噪音 {manager.LastNoiseResolution.Plan.NoiseScore}";
            RenderHunters();
            if (!retreatPreviewOpen && !consumableSelectionOpen) RenderOperations();
            if (retreatButton != null)
                retreatButton.interactable = !submitting && !preparingHarvest && !retreatInput.IsReturnCheckpointLocked;
            if (retreatPreviewOpen || consumableSelectionOpen) return;
            if (!hasSelectedCoordinate)
            {
                detailText.text = $"队伍位置：{manager.SquadPosition.x}, {manager.SquadPosition.y}\n选择地图地块查看可执行操作。";
                return;
            }
            RenderSelectedTile();
        }

        private void ValidateReferences()
        {
            if (phaseText == null || statusText == null || detailText == null || instructionText == null || hunterContent == null || cargoContent == null || operationContent == null || entryPrefab == null || retreatButton == null)
                throw new MissingReferenceException($"[{nameof(HuntScreenView)}] Prefab 显式引用未完整绑定。");
        }

        private void Update()
        {
            if (!IsBound || submitting || preparingHarvest || (!retreatPreviewOpen && !consumableSelectionOpen)) return;
            if (!Input.GetKeyDown(KeyCode.Escape) && !Input.GetMouseButtonDown(1)) return;
            if (retreatPreviewOpen) CloseRetreatPreview();
            else CloseConsumableSelection();
        }

        private void RenderHunters()
        {
            EnsureEntries(hunterEntries, manager.ActiveHunters.Count);
            for (int index = 0; index < hunterEntries.Count; index++)
            {
                ScreenEventChoice entry = hunterEntries[index];
                if (index >= manager.ActiveHunters.Count || manager.ActiveHunters[index] == null)
                {
                    entry.gameObject.SetActive(false);
                    continue;
                }
                HunterInstance hunter = manager.ActiveHunters[index];
                string summary = hunter.IsAlive ? $"头{hunter.HP.head} 躯{hunter.HP.body}\n臂{hunter.HP.arms} 腿{hunter.HP.legs} · 意志{hunter.Willpower}" : "已阵亡";
                entry.Configure(hunter.Name, summary, hunter.IsAlive && !submitting && !retreatPreviewOpen && !consumableSelectionOpen, manager.SelectedHunter == hunter ? "当前行动者" : string.Empty, () => SelectHunter(hunter));
            }
        }

        private void RenderOperations()
        {
            int resourceCount = SelectedTile()?.ResourcePoints?.Count ?? 0;
            EnsureEntries(operationEntries, resourceCount + 3);
            for (int index = 0; index < operationEntries.Count; index++) operationEntries[index].gameObject.SetActive(false);
            RenderCargo();
            int next = 0;
            if (hasSelectedCoordinate && SelectedTile() is HexTileInstance selectedTile)
            {
                Vector2Int actionCoordinate = selectedCoordinate;
                if (selectedTile.State == TileState.Interactable)
                    operationEntries[next++].Configure("探索地块", "揭示内容与本次噪音判定", !submitting && !preparingHarvest, "翻开后处理噪音与遭遇", () => SubmitTile(actionCoordinate));
                else if (selectedTile.State == TileState.Revealed && actionCoordinate != manager.SquadPosition)
                {
                    bool adjacent = manager.IsAdjacentToSquad(actionCoordinate);
                    operationEntries[next++].Configure("移动到这里", "移动到相邻地块", adjacent && !submitting && !preparingHarvest, adjacent ? "确认移动" : "超出移动范围", () => SubmitTile(actionCoordinate));
                }
                if (selectedTile.State == TileState.Revealed)
                for (int pointIndex = 0; pointIndex < selectedTile.ResourcePoints.Count; pointIndex++)
                {
                    ResourcePointInstance point = selectedTile.ResourcePoints[pointIndex];
                    if (point == null) continue;
                    bool canReach = selectedCoordinate == manager.SquadPosition;
                    bool harvestable = canReach && manager.IsHarvestablePoint(point) && manager.SelectedHunter != null;
                    string cause = point.IsExhausted ? "已采集" : harvestable ? "可查看与采集" : canReach ? "当前不可采集" : "需要先到达这里";
                    int selectedPointIndex = pointIndex;
                    Vector2Int selectedPointCoordinate = selectedCoordinate;
                    operationEntries[next++].Configure(point.ResourceName, $"抽取 {point.DrawCount} 张", !submitting && !preparingHarvest, cause, () => SelectResource(selectedPointCoordinate, selectedPointIndex));
                }
            }
            if (next < operationEntries.Count)
                operationEntries[next++].Configure("撤退", "查看回营损失与日历变化", !retreatInput.IsReturnCheckpointLocked && !preparingHarvest && !submitting, retreatInput.IsReturnCheckpointLocked ? "当前结算不能撤退" : string.Empty, OpenRetreatPreview);
            statusText.text = submitting ? "正在结算…" : preparingHarvest ? "采集已开始，完成所有揭示后才能离开" : instructionText.text;
        }

        private void RenderCargo()
        {
            HunterInstance hunter = manager.SelectedHunter;
            int capacity = hunter?.Collectibles?.Count ?? 0;
            EnsureEntries(cargoEntries, capacity, cargoContent);
            int slot = 0;
            if (hunter?.Collectibles == null) return;
            foreach (ItemInstance item in hunter.Collectibles)
            {
                if (item?.Data == null || item.Count <= 0) continue;
                HuntCollectiblePresentation presentation = HuntCollectiblePresentation.Create(new[] { item });
                bool canUse = presentation.Stacks.Count > 0 && presentation.Stacks[0].CanUseInHunt;
                ItemInstance selectedItem = item;
                cargoEntries[slot++].Configure(item.Data.itemName, $"携带 {item.Count}", canUse && !submitting && !preparingHarvest && !retreatPreviewOpen && !consumableSelectionOpen, canUse ? "点击选择身体部位" : "仅供携带", () => SelectConsumable(selectedItem));
            }
            for (; slot < cargoEntries.Count; slot++) cargoEntries[slot].gameObject.SetActive(false);
        }

        private void RenderSelectedTile()
        {
            HexTileInstance tile = SelectedTile();
            if (tile == null)
            {
                detailText.text = "所选地块已失效。";
                return;
            }
            if (tile.State != TileState.Revealed)
            {
                detailText.text = $"未探索地块　·　({selectedCoordinate.x}, {selectedCoordinate.y})\n点击下方操作揭示地块。";
                return;
            }
            string tileName = tile.Config != null ? tile.Config.tileName : "已探索地块";
            string location = $"{tileName}　·　({selectedCoordinate.x}, {selectedCoordinate.y})\n";
            if (tile.State == TileState.Revealed && selectedCoordinate != manager.SquadPosition)
            {
                detailText.text = location + (IsAdjacent(selectedCoordinate) ? "可移动到此地块。" : "超出当前移动范围。远处内容仅供查看。");
                return;
            }
            detailText.text = location + (tile.ResourcePoints == null || tile.ResourcePoints.Count == 0 ? "当前地块没有资源点。" : "查看右侧资源；采集必须先抵达所在位置。");
            instructionText.text = $"当前行动者：{manager.SelectedHunter?.Name ?? "无可行动猎人"}";
        }

        private void ConfigureSingleOperation(string title, string body, bool available, Action action)
        {
            EnsureEntries(operationEntries, 1);
            operationEntries[0].Configure(title, body, available && !submitting && !preparingHarvest, available ? string.Empty : "当前不可执行", action);
        }

        private void SelectHunter(HunterInstance hunter) => SelectHunterAsync(hunter).Forget();

        private async UniTaskVoid SelectHunterAsync(HunterInstance hunter)
        {
            if (submitting || retreatPreviewOpen || consumableSelectionOpen || hunter == null || !hunter.IsAlive) return;
            submitting = true;
            using IDisposable gate = ScreenModalInputGate.Acquire(this);
            try
            {
                HuntActorSelectionResult result = await explorationPort.SubmitActorSelectionAsync(hunter.InstanceId);
                if (!IsSessionCurrent(lifetimeSource?.Token ?? default)) return;
                instructionText.text = result.Succeeded ? $"当前行动者：{hunter.Name}" : result.Reason;
            }
            finally
            {
                submitting = false;
                if (IsBound) Refresh();
            }
        }

        private void OnScreenTileSelected(Vector2Int coordinate)
        {
            if (retreatPreviewOpen || consumableSelectionOpen || preparingHarvest) return;
            selectedCoordinate = coordinate;
            selectedResourceIndex = -1;
            hasSelectedCoordinate = true;
            Refresh();
        }

        private void OnResourcePointSelected(HuntResourcePointPresentationRequest request)
        {
            if (retreatPreviewOpen || consumableSelectionOpen || preparingHarvest || submitting) return;
            selectedCoordinate = request.Coordinate;
            selectedResourceIndex = request.PointIndex;
            hasSelectedCoordinate = true;
            SelectResource(request.Coordinate, request.PointIndex);
        }

        private void SelectResource(Vector2Int coordinate, int pointIndex)
        {
            if (retreatPreviewOpen || preparingHarvest) return;
            selectedCoordinate = coordinate;
            selectedResourceIndex = pointIndex;
            hasSelectedCoordinate = true;
            if (manager.Map.TryGetValue(coordinate, out HexTileInstance tile) && pointIndex >= 0 && pointIndex < tile.ResourcePoints.Count)
            {
                ResourcePointInstance point = tile.ResourcePoints[pointIndex];
                detailText.text = $"{point.ResourceName}\n抽取 {point.DrawCount} 张，完成后获得命中的材料。\n{(coordinate == manager.SquadPosition ? "小队已抵达" : "需要先到达这里")}";
                ConfigureSingleOperation("开始采集", $"{point.DrawCount} 次抽取 · 无需逐张领取", coordinate == manager.SquadPosition && manager.IsHarvestablePoint(point), () => Harvest(coordinate, pointIndex));
            }
        }

        private void SelectConsumable(ItemInstance item) => ShowConsumableParts(item);

        private void ShowConsumableParts(ItemInstance item)
        {
            if (submitting || preparingHarvest || retreatPreviewOpen || consumableSelectionOpen || manager.SelectedHunter == null || item?.Data == null) return;
            consumableSelectionOpen = true;
            selectionLease ??= ScreenModalInputGate.Acquire(this);
            HuntCollectiblePresentation presentation = HuntCollectiblePresentation.Create(new[] { item });
            bool usable = presentation.Stacks.Count > 0 && presentation.Stacks[0].CanUseInHunt;
            int recoveryAmount = presentation.Stacks.Count > 0 ? presentation.Stacks[0].EffectAmount : 0;
            detailText.text = $"{item.Data.itemName} × {item.Count}\n每次使用消耗 1 件，成功恢复 {recoveryAmount} 点。选择需要恢复的身体部位。";
            EnsureEntries(operationEntries, 6);
            HunterBodyPart[] parts = { HunterBodyPart.Head, HunterBodyPart.Torso, HunterBodyPart.Arms, HunterBodyPart.Legs };
            for (int index = 0; index < operationEntries.Count; index++) operationEntries[index].gameObject.SetActive(false);
            int slot = 0;
            foreach (HunterBodyPart part in parts)
            {
                bool canRecover = HunterRecoveryRules.CanRecover(manager.SelectedHunter, part, out string reason);
                bool canUse = usable && canRecover;
                if (!usable) reason = "该物品不能在狩猎中使用";
                HunterBodyPart selectedPart = part;
                operationEntries[slot++].Configure(PartName(part), canUse ? "使用此消耗品恢复" : reason, canUse && !submitting, canUse ? "可用" : "不可用", () => UseConsumable(item, selectedPart));
            }
            operationEntries[slot].Configure("返回地块", "取消部位选择", !submitting, string.Empty, CloseConsumableSelection);
        }

        private void CloseConsumableSelection()
        {
            consumableSelectionOpen = false;
            selectionLease?.Dispose();
            selectionLease = null;
            if (this != null && Current == this) Refresh();
        }

        private async UniTaskVoid UseConsumable(ItemInstance item, HunterBodyPart part)
        {
            if (submitting || manager.SelectedHunter == null || item?.Data == null) return;
            submitting = true;
            using IDisposable gate = ScreenModalInputGate.Acquire(this);
            try
            {
                HuntConsumableCommandResult result = await explorationPort.UseConsumableAsync(manager.SelectedHunter.InstanceId, item.Data.ContentId, part);
                if (!IsSessionCurrent(lifetimeSource?.Token ?? default)) return;
                instructionText.text = result.Succeeded ? $"{PartName(part)}已恢复。" : result.Reason;
            }
            finally
            {
                submitting = false;
                if (IsBound) Refresh();
                CloseConsumableSelection();
            }
        }

        private async UniTaskVoid Harvest(Vector2Int coordinate, int pointIndex)
        {
            if (preparingHarvest || submitting || !explorationPort.TryCreateSnapshot(coordinate, pointIndex, out HuntExplorationSnapshot snapshot)) return;
            preparingHarvest = true;
            using IDisposable gate = ScreenModalInputGate.Acquire(this);
            PlayableHarvestTransaction transaction = null;
            PhysicalInteractionScreenView stage = null;
            try
            {
                if (activeHarvestTransaction != null && !activeHarvestTransaction.IsCommitted && !activeHarvestTransaction.IsCancelled)
                {
                    if (activeHarvestCoordinate != coordinate || activeHarvestPointIndex != pointIndex)
                    {
                        instructionText.text = "上一项采集仍未完成，请先继续同一资源点。";
                        return;
                    }
                    transaction = activeHarvestTransaction;
                }
                else
                {
                    transaction = await explorationPort.PrepareHarvestAsync(snapshot);
                    activeHarvestTransaction = transaction;
                    activeHarvestCoordinate = coordinate;
                    activeHarvestPointIndex = pointIndex;
                }
                if (transaction == null)
                {
                    instructionText.text = "当前无法开始采集。";
                    return;
                }
                stage = await PhysicalInteractionScreenView.OpenAsync(lifetimeSource.Token);
                await stage.BeginCardSelectionAsync($"采集 · {transaction.ResourceName}", $"选择背面朝上的牌　剩余抽取 {Math.Max(0, transaction.RevealLimit - transaction.RevealedCount)} 次", transaction.CardCount, lifetimeSource.Token);
                for (int cardIndex = 0; cardIndex < transaction.CardCount; cardIndex++)
                    if (!transaction.CanRevealCard(cardIndex) && suspendedHarvestResults.TryGetValue(cardIndex, out string previousResult))
                        await stage.RevealSelectionAsync(cardIndex, previousResult, lifetimeSource.Token);
                while (!transaction.IsCommitted && !transaction.IsCancelled)
                {
                    if (transaction.IsComplete)
                    {
                        PlayableHarvestStepResult commitResult = await explorationPort.AdvanceHarvestAsync(explorationPort.SessionId, transaction, -1);
                        if (commitResult.Succeeded && commitResult.IsCompleted)
                        {
                            instructionText.text = $"采集完成 · 获得 {commitResult.Obtained.Count} 件材料。";
                            break;
                        }
                        string retryReason = string.IsNullOrWhiteSpace(commitResult.Reason) ? "采集提交尚未完成。" : commitResult.Reason;
                        stage.PresentCardsResult(retryReason);
                        await stage.WaitForContinueAsync("重试提交", lifetimeSource.Token);
                        continue;
                    }
                    var availableIndices = new List<int>();
                    for (int cardIndex = 0; cardIndex < transaction.CardCount; cardIndex++)
                        if (transaction.CanRevealCard(cardIndex)) availableIndices.Add(cardIndex);
                    if (availableIndices.Count == 0)
                    {
                        stage.PresentCardsResult("采集牌组状态待确认。当前事务仍保留，继续后重新检查剩余牌。");
                        await stage.WaitForContinueAsync("继续采集", lifetimeSource.Token);
                        continue;
                    }
                    int cardIndexSelected = await stage.WaitForCardSelectionAsync($"选择一张背面朝上的牌　剩余抽取 {Math.Max(0, transaction.RevealLimit - transaction.RevealedCount)} 次", availableIndices, lifetimeSource.Token);
                    PlayableHarvestStepResult result = await explorationPort.AdvanceHarvestAsync(explorationPort.SessionId, transaction, cardIndexSelected);
                    if (result.HasRevealedCard)
                    {
                        ItemData material = transaction.Point.ResolveMaterial(result.RevealedCard.MaterialId);
                        string summary = result.RevealedCard.IsHit ? $"命中 · {material?.itemName ?? result.RevealedCard.MaterialId}" : "未命中";
                        suspendedHarvestResults[cardIndexSelected] = summary;
                        await stage.RevealSelectionAsync(cardIndexSelected, summary, lifetimeSource.Token);
                        await UniTask.Delay(TimeSpan.FromMilliseconds(650), cancellationToken: lifetimeSource.Token);
                    }
                    if (result.Succeeded && result.IsCompleted)
                    {
                        instructionText.text = $"采集完成 · 获得 {result.Obtained.Count} 件材料。";
                        break;
                    }
                    if (!result.Succeeded)
                    {
                        instructionText.text = string.IsNullOrWhiteSpace(result.Reason) ? "本次采集操作未完成；采集事务仍保留。" : result.Reason;
                        stage.PresentCardsResult(instructionText.text);
                        await stage.WaitForContinueAsync("继续采集", lifetimeSource.Token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                if (transaction != null && (transaction.IsCommitted || transaction.IsCancelled || transaction.Cancel()))
                {
                    activeHarvestTransaction = null;
                    suspendedHarvestTransaction = null;
                    suspendedHarvestResults.Clear();
                    if (this != null && Current == this) instructionText.text = "已取消尚未揭示的采集。";
                }
                else if (transaction != null)
                {
                    suspendedHarvestTransaction = transaction;
                    suspendedHarvestSessionId = boundSessionId;
                    suspendedHarvestCoordinate = coordinate;
                    suspendedHarvestPointIndex = pointIndex;
                }
            }
            finally
            {
                stage?.EndCardSelection();
                stage?.Close();
                if (transaction != null && (transaction.IsCommitted || transaction.IsCancelled))
                {
                    activeHarvestTransaction = null;
                    if (suspendedHarvestTransaction == transaction) suspendedHarvestTransaction = null;
                    suspendedHarvestResults.Clear();
                }
                else if (transaction != null && transaction.RevealedCount > 0)
                {
                    activeHarvestTransaction = transaction;
                    suspendedHarvestTransaction = transaction;
                    suspendedHarvestSessionId = boundSessionId;
                    suspendedHarvestCoordinate = coordinate;
                    suspendedHarvestPointIndex = pointIndex;
                }
                preparingHarvest = false;
                if (this != null && Current == this && IsSessionCurrent(default)) Refresh();
            }
        }

        private void OpenRetreatPreview()
        {
            if (preparingHarvest || submitting || retreatPreviewOpen || consumableSelectionOpen || retreatInput.IsReturnCheckpointLocked) return;
            retreatPreviewOpen = true;
            selectionLease ??= ScreenModalInputGate.Acquire(this);
            HuntRetreatPreview preview = retreatInput.GetRetreatPreview();
            RenderRetreatPreview(preview);
        }

        private void RenderRetreatPreview(HuntRetreatPreview preview)
        {
            detailText.text = FormatRetreatPreview(preview);
            EnsureEntries(operationEntries, Mathf.Max(1, preview.LootItems.Count + 2));
            for (int index = 0; index < operationEntries.Count; index++) operationEntries[index].gameObject.SetActive(false);
            if (preview.RequiresAbandonment)
            {
                for (int index = 0; index < preview.LootItems.Count; index++)
                {
                    HuntRetreatLootItem item = preview.LootItems[index];
                    operationEntries[index].Configure($"选择放弃 {item.DisplayName}", $"携带 {item.Count} · 回营必须放弃一份", true, pendingAbandonedItemId == item.ContentId ? "当前选择" : string.Empty, () => SelectAbandonedItem(preview, item.ContentId));
                }
                bool selectedItemExists = false;
                string selectedItemName = string.Empty;
                foreach (HuntRetreatLootItem item in preview.LootItems)
                {
                    if (item.ContentId != pendingAbandonedItemId) continue;
                    selectedItemExists = true;
                    selectedItemName = item.DisplayName;
                    break;
                }
                operationEntries[preview.LootItems.Count].Configure("确认撤退", "提交回营并放弃所选物品", selectedItemExists, selectedItemExists ? $"将放弃：{selectedItemName}" : "先选择一份物品", () => ConfirmRetreat(preview, pendingAbandonedItemId));
                operationEntries[preview.LootItems.Count + 1].Configure("返回狩猎", "暂不撤退", true, string.Empty, CloseRetreatPreview);
                return;
            }
            operationEntries[0].Configure("确认撤退", "确认带回所示物品并推进日历", preview.Calendar.IsAvailable, preview.Calendar.IsAvailable ? string.Empty : preview.Calendar.Reason, () => ConfirmRetreat(preview, string.Empty));
            operationEntries[1].Configure("返回狩猎", "暂不撤退", true, string.Empty, CloseRetreatPreview);
        }

        private void SelectAbandonedItem(HuntRetreatPreview preview, string itemId)
        {
            if (!retreatPreviewOpen || submitting || preparingHarvest) return;
            pendingAbandonedItemId = itemId ?? string.Empty;
            RenderRetreatPreview(preview);
        }

        private void CloseRetreatPreview()
        {
            retreatPreviewOpen = false;
            pendingAbandonedItemId = string.Empty;
            selectionLease?.Dispose();
            selectionLease = null;
            Refresh();
        }

        private async UniTaskVoid ConfirmRetreat(HuntRetreatPreview preview, string abandonedItemId)
        {
            if (submitting || preparingHarvest || retreatInput.IsReturnCheckpointLocked) return;
            if (!preview.Calendar.IsAvailable)
            {
                instructionText.text = preview.Calendar.Reason;
                return;
            }
            submitting = true;
            using IDisposable gate = ScreenModalInputGate.Acquire(this);
            try
            {
                HuntRetreatCommandResult result = await retreatInput.RequestRetreatAsync(new HuntRetreatDecision(abandonedItemId));
                if (!IsSessionCurrent(lifetimeSource?.Token ?? default)) return;
                instructionText.text = result.Succeeded ? "回营结算已提交。" : result.Reason;
            }
            finally
            {
                submitting = false;
                retreatPreviewOpen = false;
                selectionLease?.Dispose();
                selectionLease = null;
                if (IsBound) Refresh();
            }
        }

        private void SubmitTile(Vector2Int coordinate) => SubmitTileAsync(coordinate).Forget();

        private async UniTaskVoid SubmitTileAsync(Vector2Int coordinate)
        {
            if (submitting || !explorationPort.TryCreateSnapshot(coordinate, -1, out HuntExplorationSnapshot snapshot)) return;
            submitting = true;
            using IDisposable gate = ScreenModalInputGate.Acquire(this);
            try
            {
                HuntTileCommandResult result = await explorationPort.SubmitTileAsync(snapshot);
                if (!IsSessionCurrent(lifetimeSource?.Token ?? default)) return;
                instructionText.text = result.Succeeded ? result.Commit.Kind == HuntTileInteractionKind.Reveal ? "地块已揭示。" : "队伍已移动。" : result.Reason;
                if (!result.Succeeded) statusText.text = result.Reason;
            }
            finally
            {
                submitting = false;
                if (IsBound) Refresh();
            }
        }

        private void EnsureEntries(List<ScreenEventChoice> entries, int count, Transform parent = null)
        {
            while (entries.Count < count)
            {
                Transform content = parent != null ? parent : entries == hunterEntries ? hunterContent : entries == cargoEntries ? cargoContent : operationContent;
                ScreenEventChoice entry = Instantiate(entryPrefab, content);
                entries.Add(entry);
            }
            foreach (ScreenEventChoice entry in entries) entry.gameObject.SetActive(false);
        }

        private bool IsSessionCurrent(CancellationToken token) => !token.IsCancellationRequested && IsBound && explorationPort.SessionId == boundSessionId;

        private HexTileInstance SelectedTile() => hasSelectedCoordinate && manager.Map.TryGetValue(selectedCoordinate, out HexTileInstance tile) ? tile : null;

        private bool IsAdjacent(Vector2Int coordinate) => manager.IsAdjacentToSquad(coordinate);

        private int CountRevealedTiles()
        {
            int count = 0;
            foreach (HexTileInstance tile in manager.Map.Values)
                if (tile.State == TileState.Revealed) count++;
            return count;
        }

        private static string PartName(HunterBodyPart part) => part switch { HunterBodyPart.Head => "头部", HunterBodyPart.Torso => "躯干", HunterBodyPart.Arms => "手臂", HunterBodyPart.Legs => "腿部", _ => part.ToString() };

        private static string FormatRetreatPreview(HuntRetreatPreview preview)
        {
            HuntReturnCalendarPreview calendar = preview.Calendar;
            string date = calendar.IsAvailable ? $"第 {calendar.CurrentYear} 年 {calendar.CurrentSeasonName} → 第 {calendar.NextYear} 年 {calendar.NextSeasonName}" : calendar.Reason;
            var lootLines = new List<string>(preview.LootItems.Count);
            foreach (HuntRetreatLootItem item in preview.LootItems) lootLines.Add($"{item.DisplayName} × {item.Count}");
            string loot = lootLines.Count == 0 ? "无携带物" : string.Join("\n", lootLines);
            return $"回营预览\n{date}\n救援人口：{preview.RescuedPopulation}\n携带物：\n{loot}\n{(preview.RequiresAbandonment ? "离开营地时必须选择放弃一份物品。" : "无需放弃携带物。")}";
        }

        private void OnDestroy()
        {
            NotifyWindowClosed();
            if (refreshButton != null) refreshButton.onClick.RemoveListener(Refresh);
            if (retreatButton != null) retreatButton.onClick.RemoveListener(OpenRetreatPreview);
        }
    }
}
