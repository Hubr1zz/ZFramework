using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cards3D;
using Core;
using Cysharp.Threading.Tasks;
using GameplayBase;
using HuntingInDarkness.ActionFlow.Campaign;
using HuntingInDarkness.ActionFlow.Events;
using HuntingInDarkness.ActionFlow.Hunt;
using HuntingInDarkness.ActionFlow.Presentation;
using HuntingInDarkness.ActionFlow.Settlement;
using HuntingInDarkness.Bootstrap;
using HuntingInDarkness.ContentTables;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Hunt;
using HuntingInDarkness.GameCore.Hunters;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.Settlement;
using HuntingInDarkness.ViewLayer.Hunt;
using HuntingInDarkness.ViewLayer.Settlement;
using HuntingInDarkness.ViewLayer.Tabletop;
using NUnit.Framework;
using TMPro;
using UI;
using UI.Hunt;
using UnityEngine;
using UnityEngine.TestTools;

namespace HuntingInDarkness.Adapter.PlayModeTests
{
    public sealed class GameManagerCampaignLoopPlayModeTests
    {
        private const int FrameTimeout = 600;
        private GameObject managerObject;
        private PlayableHuntDestinationCatalog destinationCatalog;
        private PlayableCampaignContentCandidate contentCandidate;
        private HexTileData patchedTileConfig;
        private EventData originalPatchedTileRevealEvent;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            ResetContentAssembly();
            contentCandidate = null;
            destinationCatalog = null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            RestorePatchedTile();
            if (managerObject != null)
                UnityEngine.Object.Destroy(managerObject);
            yield return null;
            ResetContentAssembly();
        }

        [UnityTest]
        public IEnumerator PublicCommands_CompleteTwoSeasonLoopAndAllowNextDeparture()
        {
            var persistence = new MemoryCampaignPersistence();
            GameManager manager = CreateProductionManager(persistence);
            yield return WaitForSettlementIdle(manager);
            int initialYear = manager.SettlementData.CurrentYear;
            int hunterId = manager.SettlementData.GetAliveHunters()[0].InstanceId;

            UniTask<CampaignPhaseTransitionResult>.Awaiter bareTransition = manager.TransitionToPhaseAsync(GamePhase.Hunt).GetAwaiter();
            yield return WaitForCompletion(bareTransition);
            CampaignPhaseTransitionResult bareTransitionResult = bareTransition.GetResult();
            Assert.That(bareTransitionResult.Succeeded, Is.False, "正式运行不得通过旧阶段 API 绕过出发名册门禁。");

            UniTask<SettlementDepartureCommandResult>.Awaiter departure = manager.DepartForHuntAsync(new[] { hunterId }, GetDestination(initialYear)).GetAwaiter();
            yield return WaitForCompletion(departure);
            SettlementDepartureCommandResult departureResult = departure.GetResult();
            Assert.That(departureResult.Succeeded, Is.True, departureResult.Reason);
            Assert.That(manager.CurrentGamePhase, Is.EqualTo(GamePhase.Hunt));
            Assert.That(manager.ActiveHuntHunters, Has.Count.EqualTo(1));
            Assert.That(manager.SettlementData.DepartingHunterIds, Is.Empty);

            UniTask<HuntRetreatCommandResult>.Awaiter retreat = manager.RequestRetreatAsync().GetAwaiter();
            yield return WaitForCompletion(retreat);
            HuntRetreatCommandResult retreatResult = retreat.GetResult();
            Assert.That(retreatResult.Succeeded, Is.True, retreatResult.Reason);
            yield return WaitForSettlementIdle(manager);

            Assert.That(manager.CurrentGamePhase, Is.EqualTo(GamePhase.Settlement));
            Assert.That(manager.SettlementData.CurrentYear, Is.EqualTo(initialYear));
            Assert.That(manager.SettlementData.CurrentSeasonIndex, Is.EqualTo(1));
            Assert.That(manager.SettlementData.HuntHistory, Has.Count.EqualTo(1));
            Assert.That(manager.SettlementData.PendingHuntReturn, Is.Null);
            Assert.That(manager.SettlementData.DepartingHunterIds, Is.Empty);

            UniTask<SettlementDepartureCommandResult>.Awaiter secondDeparture = manager.DepartForHuntAsync(new[] { hunterId }, GetDestination(manager.SettlementData.CurrentYear)).GetAwaiter();
            yield return WaitForCompletion(secondDeparture);
            SettlementDepartureCommandResult secondDepartureResult = secondDeparture.GetResult();
            Assert.That(secondDepartureResult.Succeeded, Is.True, secondDepartureResult.Reason);
            UniTask<HuntRetreatCommandResult>.Awaiter secondRetreat = manager.RequestRetreatAsync().GetAwaiter();
            yield return WaitForCompletion(secondRetreat);
            HuntRetreatCommandResult secondRetreatResult = secondRetreat.GetResult();
            Assert.That(secondRetreatResult.Succeeded, Is.True, secondRetreatResult.Reason);
            yield return WaitForSettlementIdle(manager);

            Assert.That(manager.SettlementData.CurrentYear, Is.EqualTo(initialYear + 1));
            Assert.That(manager.SettlementData.CurrentSeasonIndex, Is.Zero);
            Assert.That(manager.SettlementData.HuntHistory, Has.Count.EqualTo(2));
            Assert.That(persistence.HasAppliedPendingSave(initialYear + 1), Is.True, "缺少已应用但仍保留回营检查点的第一阶段存档。");

            UniTask<SettlementDepartureCommandResult>.Awaiter nextDeparture = manager.DepartForHuntAsync(new[] { hunterId }, GetDestination(manager.SettlementData.CurrentYear)).GetAwaiter();
            yield return WaitForCompletion(nextDeparture);
            SettlementDepartureCommandResult nextDepartureResult = nextDeparture.GetResult();
            Assert.That(nextDepartureResult.Succeeded, Is.True, nextDepartureResult.Reason);
            Assert.That(manager.CurrentGamePhase, Is.EqualTo(GamePhase.Hunt));
        }

        [UnityTest]
        public IEnumerator HuntCompletionNotice_FormatsSeasonAndYearBoundary()
        {
            var persistence = new MemoryCampaignPersistence();
            GameManager manager = CreateProductionManager(persistence);
            yield return WaitForSettlementIdle(manager);
            SettlementNoticePresenter3D notice = managerObject.GetComponent<SettlementNoticePresenter3D>();
            Assert.That(notice, Is.Not.Null);
            notice.ResetForCampaignChange();

            EventBus.Publish(new HuntCompletedEvent
            {
                CompletedYear = 1,
                CompletedSeasonIndex = 0,
                CompletedSeasonId = "season_early",
                CompletedSeasonDisplayName = "早季",
                AdvancedToYear = 1,
                AdvancedToSeasonIndex = 1,
                AdvancedToSeasonId = "season_late",
                AdvancedToSeasonDisplayName = "晚季",
                TotalHunts = 1
            });
            yield return null;
            Assert.That(notice.ActiveNoticeTitle, Is.EqualTo("季节推进 · 回营"));
            Assert.That(notice.ActiveNoticeBody, Does.Contain("第 1 年·早季"));
            Assert.That(notice.ActiveNoticeBody, Does.Contain("第 1 年·晚季"));

            notice.PresentHuntDepartureBlocked("请先完成当前营地流程。");
            Assert.That(notice.ActiveNoticeTitle, Is.EqualTo("暂不能出猎"));
            Assert.That(notice.ActiveNoticeBody, Does.Contain("请先完成当前营地流程"));
            yield return null;
            notice.ClearHuntDepartureBlocked();
            Assert.That(notice.ActiveNoticeTitle, Is.EqualTo("季节推进 · 回营"));
            Assert.That(notice.ActiveNoticeBody, Does.Contain("第 1 年·晚季"));
            Assert.That(notice.PendingNoticeCount, Is.Zero);

            notice.ResetForCampaignChange();
            EventBus.Publish(new HuntCompletedEvent
            {
                CompletedYear = 1,
                CompletedSeasonIndex = 1,
                CompletedSeasonId = "season_late",
                CompletedSeasonDisplayName = "晚季",
                AdvancedToYear = 2,
                AdvancedToSeasonIndex = 0,
                AdvancedToSeasonId = "season_early",
                AdvancedToSeasonDisplayName = "早季",
                TotalHunts = 2
            });
            yield return null;
            Assert.That(notice.ActiveNoticeTitle, Is.EqualTo("新年抵达 · 回营"));
            Assert.That(notice.ActiveNoticeBody, Does.Contain("第 1 年·晚季"));
            Assert.That(notice.ActiveNoticeBody, Does.Contain("第 2 年·早季"));

            notice.ResetForCampaignChange();
            EventBus.Publish(new HuntCompletedEvent { CompletedYear = 2, CompletedSeasonIndex = 0, AdvancedToYear = 2, AdvancedToSeasonIndex = 1, TotalHunts = 3 });
            yield return null;
            Assert.That(notice.ActiveNoticeBody, Does.Contain("第 2 年·第 1 季"));
            Assert.That(notice.ActiveNoticeBody, Does.Contain("第 2 年·第 2 季"));
        }

        [UnityTest]
        public IEnumerator CampLedgerPanel_UsesBoundSeasonDisplayName()
        {
            var root = new GameObject("calendar-ledger-test");
            CampLedgerPanel3D panel = CampLedgerPanel3D.Create(root.transform);
            panel.SetCalendarSeason(new SeasonDefinition("season_custom", "霜降", 0));
            panel.Open(new SettlementInstance { CurrentYear = 3, CurrentSeasonIndex = 0 }, Vector3.zero);
            yield return null;

            TextMeshPro title = panel.GetComponentsInChildren<TextMeshPro>(true).FirstOrDefault(text => text.name == "Title");
            Assert.That(title, Is.Not.Null);
            Assert.That(title.text, Does.Contain("第 3 年 · 霜降"));

            UnityEngine.Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DepartureBlockNotice_DeduplicatesAndClearsAfterRetry()
        {
            var persistence = new MemoryCampaignPersistence { DelayAppliedReturn = true };
            GameManager manager = CreateProductionManager(persistence);
            yield return WaitForSettlementIdle(manager);
            int year = manager.SettlementData.CurrentYear;
            int hunterId = manager.SettlementData.GetAliveHunters()[0].InstanceId;
            PlayableHuntDestination destination = GetDestination(year);
            SettlementNoticePresenter3D notice = managerObject.GetComponent<SettlementNoticePresenter3D>();

            UniTask<SettlementDepartureCommandResult>.Awaiter departure = manager.DepartForHuntAsync(new[] { hunterId }, destination).GetAwaiter();
            yield return WaitForCompletion(departure);
            Assert.That(departure.GetResult().Succeeded, Is.True);
            UniTask<HuntRetreatCommandResult>.Awaiter retreat = manager.RequestRetreatAsync().GetAwaiter();
            yield return WaitForCompletion(retreat);
            Assert.That(retreat.GetResult().Succeeded, Is.True);
            yield return WaitForAppliedReturnSave(persistence);
            notice.ResetForCampaignChange();

            manager.RequestHuntDeparture(new[] { hunterId });
            manager.RequestHuntDeparture(new[] { hunterId });
            yield return null;
            Assert.That(notice.ActiveNoticeTitle, Is.EqualTo("暂不能出猎"));
            Assert.That(notice.ActiveNoticeBody, Does.Contain("请先完成上一场远征的回营结算"));
            Assert.That(notice.PendingNoticeCount, Is.Zero);

            persistence.CompleteAppliedReturnSave();
            yield return WaitForSettlementIdle(manager);
            UniTask<SettlementDepartureCommandResult>.Awaiter retry = manager.DepartForHuntAsync(new[] { hunterId }, destination).GetAwaiter();
            yield return WaitForCompletion(retry);
            Assert.That(retry.GetResult().Succeeded, Is.True, retry.GetResult().Reason);
            Assert.That(notice.ActiveNoticeTitle, Is.Not.EqualTo("暂不能出猎"));
        }

        [UnityTest]
        public IEnumerator ExpeditionReward_CraftsEquipsAndRestoresStableBuild()
        {
            var persistence = new MemoryCampaignPersistence();
            GameManager manager = CreateProductionManager(persistence);
            yield return WaitForSettlementIdle(manager);
            int initialYear = manager.SettlementData.CurrentYear;
            int hunterId = manager.SettlementData.GetAliveHunters()[0].InstanceId;

            UniTask<SettlementDepartureCommandResult>.Awaiter departure = manager.DepartForHuntAsync(new[] { hunterId }, GetDestination(initialYear)).GetAwaiter();
            yield return WaitForCompletion(departure);
            SettlementDepartureCommandResult departureResult = departure.GetResult();
            Assert.That(departureResult.Succeeded, Is.True, departureResult.Reason);

            HuntManager huntManager = manager.ActiveHuntRuntime.Manager;
            ItemData blackSalt = PlayableSettlementItemRegistry.Items.Single(item => item.ContentId == "black_salt");
            HunterInstance activeHunter = manager.ActiveHuntHunters.Single(hunter => hunter.InstanceId == hunterId);
            var resourceCommand = new HuntEventResourceCommand(huntManager);
            Assert.That(resourceCommand.TryApply(EventEffectType.AddResource, blackSalt.ContentId, 1, activeHunter, out PlayableEventResourceChange reward, out string rewardReason), Is.True, rewardReason);
            Assert.That(reward.Scope, Is.EqualTo(PlayableEventResourceScope.HuntCollectibles));
            Assert.That(manager.SettlementData.GetResource(blackSalt.ContentId), Is.Zero, "狩猎奖励不得提前写入营地库存。");
            Assert.That(manager.SettlementData.HasDiscoveredMaterial(blackSalt.ContentId), Is.False, "未回营的携带物不得提前解锁素材知识。");

            UniTask<HuntRetreatCommandResult>.Awaiter retreat = manager.RequestRetreatAsync().GetAwaiter();
            yield return WaitForCompletion(retreat);
            HuntRetreatCommandResult retreatResult = retreat.GetResult();
            Assert.That(retreatResult.Succeeded, Is.True, retreatResult.Reason);
            yield return WaitForSettlementIdle(manager);
            Assert.That(manager.SettlementData.CurrentYear, Is.EqualTo(initialYear));
            Assert.That(manager.SettlementData.CurrentSeasonIndex, Is.EqualTo(1));
            Assert.That(manager.SettlementData.GetResource(blackSalt.ContentId), Is.EqualTo(1));
            Assert.That(manager.SettlementData.HasDiscoveredMaterial(blackSalt.ContentId), Is.True);
            Assert.That(manager.SettlementData.PendingHuntReturn, Is.Null);

            IReadOnlyList<CraftRecipe> recipes = manager.SettlementRecipes;
            CraftRecipe recipe = recipes.Single(candidate => candidate.outputItem?.ContentId == "salt_ward" && candidate.ingredients.Any(ingredient => ingredient.item?.ContentId == blackSalt.ContentId));
            UniTask<SettlementCraftCommandResult>.Awaiter craft = manager.CraftAsync(recipe).GetAwaiter();
            yield return WaitForCompletion(craft);
            SettlementCraftCommandResult craftResult = craft.GetResult();
            Assert.That(craftResult.Succeeded, Is.True, craftResult.Reason);
            ItemData saltWard = recipe.outputItem;
            Assert.That(manager.SettlementData.GetResource(blackSalt.ContentId), Is.Zero);
            Assert.That(manager.SettlementData.HasDiscoveredMaterial(blackSalt.ContentId), Is.True, "耗尽素材不得遗忘已发现配方。");
            Assert.That(manager.SettlementData.GetStoredEquipment(saltWard.ContentId), Is.EqualTo(1));

            UniTask<SettlementEquipmentCommandResult>.Awaiter equip = manager.EquipItemAsync(hunterId, saltWard).GetAwaiter();
            yield return WaitForCompletion(equip);
            SettlementEquipmentCommandResult equipResult = equip.GetResult();
            Assert.That(equipResult.Succeeded, Is.True, equipResult.Reason);
            yield return WaitForPersistedEquipment(persistence, hunterId, saltWard.ContentId);

            CampaignSnapshot saved = JsonUtility.FromJson<CampaignSnapshot>(persistence.Payload);
            Assert.That(saved.Settlement.CurrentYear, Is.EqualTo(initialYear));
            Assert.That(saved.Settlement.CurrentSeasonIndex, Is.EqualTo(1));
            Assert.That(saved.Settlement.PendingHuntReturn?.RecordId, Is.Null.Or.Empty, "最终存档不得保留有效的回营检查点。");
            Assert.That(saved.Settlement.HuntHistory, Has.Count.EqualTo(1));
            Assert.That(saved.Settlement.GetResource(blackSalt.ContentId), Is.Zero);
            Assert.That(saved.Settlement.HasDiscoveredMaterial(blackSalt.ContentId), Is.True);
            Assert.That(saved.Settlement.GetStoredEquipment(saltWard.ContentId), Is.Zero);
            Assert.That(saved.Settlement.GetHunter(hunterId).EquippedItemIds.Count(itemId => itemId == saltWard.ContentId), Is.EqualTo(1));

            persistence.SnapshotToLoad = saved;
            UnityEngine.Object.Destroy(managerObject);
            managerObject = null;
            yield return null;
            GameManager restoredManager = CreateProductionManager(persistence, true);
            UniTask<CampaignStartupResult>.Awaiter restore = restoredManager.ContinueCampaignAsync().GetAwaiter();
            yield return WaitForCompletion(restore);
            CampaignStartupResult restoreResult = restore.GetResult();
            Assert.That(restoreResult.Succeeded, Is.True, restoreResult.Reason);
            yield return WaitForSettlementIdle(restoredManager);

            HunterInstance restoredHunter = restoredManager.SettlementData.GetHunter(hunterId);
            Assert.That(restoredManager.SettlementData.HasDiscoveredMaterial(blackSalt.ContentId), Is.True);
            ItemInstance restoredWard = restoredHunter.Equipment.Single(item => item.Data?.ContentId == saltWard.ContentId);
            Assert.That(restoredHunter.EquippedItemIds.Count(itemId => itemId == saltWard.ContentId), Is.EqualTo(1));

            UniTask<SettlementDepartureCommandResult>.Awaiter buildDeparture = restoredManager.DepartForHuntAsync(new[] { hunterId }, GetDestination(restoredManager.SettlementData.CurrentYear)).GetAwaiter();
            yield return WaitForCompletion(buildDeparture);
            SettlementDepartureCommandResult buildDepartureResult = buildDeparture.GetResult();
            Assert.That(buildDepartureResult.Succeeded, Is.True, buildDepartureResult.Reason);
            HuntManager restoredHuntManager = restoredManager.ActiveHuntRuntime.Manager;
            Assert.That(restoredHuntManager.NoiseProfile.TryCreatePlan(restoredManager.ActiveHuntHunters, out NoiseCheckPlan buildNoisePlan), Is.True);
            Assert.That(buildNoisePlan.NoiseScore, Is.Zero, "盐纹护符应在下一次狩猎中抵消单人队伍的基础噪音。");
            Assert.That(buildNoisePlan.DangerCardCount, Is.Zero);

            UniTask<HuntRetreatCommandResult>.Awaiter buildRetreat = restoredManager.RequestRetreatAsync().GetAwaiter();
            yield return WaitForCompletion(buildRetreat);
            HuntRetreatCommandResult buildRetreatResult = buildRetreat.GetResult();
            Assert.That(buildRetreatResult.Succeeded, Is.True, buildRetreatResult.Reason);
            yield return WaitForSettlementIdle(restoredManager);

            UniTask<SettlementEquipmentCommandResult>.Awaiter unequip = restoredManager.UnequipItemAsync(hunterId, restoredWard.InstanceId).GetAwaiter();
            yield return WaitForCompletion(unequip);
            SettlementEquipmentCommandResult unequipResult = unequip.GetResult();
            Assert.That(unequipResult.Succeeded, Is.True, unequipResult.Reason);
            Assert.That(restoredManager.SettlementData.GetStoredEquipment(saltWard.ContentId), Is.EqualTo(1));
            Assert.That(restoredHunter.EquippedItemIds, Does.Not.Contain(saltWard.ContentId));
        }

        [UnityTest]
        public IEnumerator SettlementTable3D_DragEquipAndUseConsumableThroughGameManager()
        {
            var persistence = new MemoryCampaignPersistence();
            GameManager manager = CreateProductionManager(persistence);
            yield return WaitForSettlementIdle(manager);

            ItemData saltWard = PlayableSettlementItemRegistry.Items.Single(item => item.ContentId == "salt_ward");
            ItemData poultice = PlayableSettlementItemRegistry.Items.Single(item => item.ContentId == "mushroom_flesh_poultice");
            HunterInstance hunter = manager.SettlementData.GetAliveHunters()[0];
            hunter.HP.arms = Mathf.Max(0, hunter.MaxHP.arms - 1);
            manager.SettlementData.AddStoredItem(saltWard, 1);
            manager.SettlementData.AddStoredItem(poultice, 1);

            SettlementTable3D table = managerObject.GetComponentInChildren<SettlementTable3D>(true);
            Assert.That(table, Is.Not.Null);
            table.Refresh();
            yield return null;

            HunterCard3D hunterCard = table.GetComponentsInChildren<HunterCard3D>(true).Single(card => card.Hunter?.InstanceId == hunter.InstanceId);
            hunterCard.OnHunterClicked?.Invoke(hunterCard);
            yield return null;

            HunterEquipmentPanel3D equipmentPanel = GetPrivateField<HunterEquipmentPanel3D>(table, "hunterEquipmentPanel");
            SlotGrid equipmentGrid = GetPrivateField<SlotGrid>(equipmentPanel, "equipmentGrid");
            SlotGrid useGrid = GetPrivateField<SlotGrid>(equipmentPanel, "consumableUseGrid");
            SettlementItemCard3D saltWardCard = FindStorageCard(equipmentPanel, saltWard);
            Assert.That(saltWardCard, Is.Not.Null);
            BeginAndDrop(saltWardCard, equipmentGrid.Slots[0]);
            yield return WaitUntil(() => manager.SettlementData.GetStoredItem(saltWard) == 0 && hunter.Equipment.Any(item => item?.Data == saltWard), "等待正式 3D 装备命令提交超时。");
            yield return null;
            Assert.That(FindStorageCard(equipmentPanel, saltWard), Is.Null);
            Assert.That(equipmentGrid.Slots.Select(slot => slot.OccupantCard).OfType<SettlementItemCard3D>().Any(card => card.Item == saltWard && card.Instance != null), Is.True);
            CardSlot equippedSlot = equipmentGrid.Slots.Single(slot => slot.OccupantCard is SettlementItemCard3D card && card.Item == saltWard && card.Instance != null);
            Assert.That(equippedSlot.OccupantCard.CurrentSlot, Is.SameAs(equippedSlot));

            SettlementItemCard3D poulticeCard = FindStorageCard(equipmentPanel, poultice);
            Assert.That(poulticeCard, Is.Not.Null);
            BeginAndDrop(poulticeCard, useGrid.Slots[0]);
            Assert.That(useGrid.Slots[0].OccupantCard, Is.Null);
            HunterRecoveryPanel3D recoveryPanel = GetPrivateField<HunterRecoveryPanel3D>(table, "hunterRecoveryPanel");
            yield return WaitUntil(() => recoveryPanel != null && recoveryPanel.gameObject.activeSelf, "等待消耗品恢复面板打开超时。");

            HunterRecoveryCard3D armsCard = recoveryPanel.GetComponentsInChildren<HunterRecoveryCard3D>(true).Single(card => card.BodyPart == HunterBodyPart.Arms);
            armsCard.OnRecoveryRequested?.Invoke(armsCard);
            yield return WaitUntil(() => manager.SettlementData.GetStoredItem(poultice) == 0 && hunter.HP.arms == hunter.MaxHP.arms, "等待正式 3D 消耗品命令提交超时。");
            Assert.That(useGrid.Slots[0].OccupantCard, Is.Null);
        }

        [UnityTest]
        public IEnumerator ExplorationPort_CompletesTabletopRevealMoveHarvestReturnAndRejectsStaleSession()
        {
            var persistence = new MemoryCampaignPersistence();
            GameManager manager = CreateProductionManager(persistence);
            EnsureMainCamera();
            yield return WaitForSettlementIdle(manager);
            int initialYear = manager.SettlementData.CurrentYear;
            int hunterId = manager.SettlementData.GetAliveHunters()[0].InstanceId;

            UniTask<SettlementDepartureCommandResult>.Awaiter departure = manager.DepartForHuntAsync(new[] { hunterId }, GetDestination(initialYear)).GetAwaiter();
            yield return WaitForCompletion(departure);
            SettlementDepartureCommandResult departureResult = departure.GetResult();
            Assert.That(departureResult.Succeeded, Is.True, departureResult.Reason);

            HuntMapVisualizer visualizer = managerObject.GetComponentInChildren<HuntMapVisualizer>(true);
            Assert.That(visualizer, Is.Not.Null);
            HuntStatusBoard3D statusBoard = visualizer.GetComponentInChildren<HuntStatusBoard3D>(true);
            Assert.That(statusBoard, Is.Not.Null);
            Assert.That(statusBoard.ActiveHunterCardCount, Is.EqualTo(1));
            PlayableHuntSquadPawn3D pawn = visualizer.GetComponentInChildren<PlayableHuntSquadPawn3D>(true);
            Assert.That(pawn, Is.Not.Null);
            Assert.That(pawn.HunterCount, Is.EqualTo(1));
            PlayableHuntMapIntroCamera3D mapIntro = visualizer.GetComponent<PlayableHuntMapIntroCamera3D>();
            Assert.That(mapIntro, Is.Not.Null);
            Assert.That(mapIntro.Plan.Duration, Is.GreaterThan(0f));
            if (mapIntro.IsPresenting)
                Assert.That(PlayableHuntInputGuard.IsBlocked, Is.True, "地图入场运镜播放期间应锁定狩猎输入。");
            yield return WaitForHuntInputReady();
            yield return WaitForActiveHuntSnapshot(persistence);
            CampaignSnapshot initialSnapshot = JsonUtility.FromJson<CampaignSnapshot>(persistence.Payload);
            Assert.That(visualizer.GetComponentsInChildren<PlayableHexTileCard3D>(true), Has.Length.EqualTo(initialSnapshot.ActiveHunt.Tiles.Count));

            ActiveHuntTileSnapshot targetTile = initialSnapshot.ActiveHunt.Tiles.FirstOrDefault(tile => tile.State == TileState.Interactable && !tile.HasBossEncounter);
            Assert.That(targetTile, Is.Not.Null, "正式路线缺少可安全探索的相邻地块。");
            var coordinate = new Vector2Int(targetTile.X, targetTile.Y);
            IHuntExplorationPort explorationPort = manager.ActiveHuntExplorationPort;
            Assert.That(explorationPort, Is.Not.Null);
            Assert.That(explorationPort.TryCreateSnapshot(coordinate, -1, out HuntExplorationSnapshot target), Is.True);

            UniTask<HuntTileCommandResult>.Awaiter reveal = explorationPort.SubmitTileAsync(target).GetAwaiter();
            PlayableHexTileCard3D tileCard = FindTileCard(visualizer, coordinate);
            yield return WaitForTileFlip(tileCard);
            Assert.That(reveal.IsCompleted, Is.False, "ActionQueue 不得在实体地形卡翻面完成前结束命令。");
            yield return WaitForPresentationCompletion(reveal);
            HuntTileCommandResult revealResult = reveal.GetResult();
            Assert.That(revealResult.Succeeded, Is.True, revealResult.Reason);
            Assert.That(tileCard.IsFaceUp, Is.True);

            UniTask<HuntTileCommandResult>.Awaiter move = explorationPort.SubmitTileAsync(target).GetAwaiter();
            yield return WaitForSquadMovement(pawn);
            Assert.That(move.IsCompleted, Is.False, "ActionQueue 不得在实体小队棋子落位前结束命令。");
            yield return WaitForPresentationCompletion(move);
            HuntTileCommandResult moveResult = move.GetResult();
            Assert.That(moveResult.Succeeded, Is.True, moveResult.Reason);
            PlayableHuntResourceMarker3D[] arrivedMarkers = visualizer.GetComponentsInChildren<PlayableHuntResourceMarker3D>(true);
            Assert.That(arrivedMarkers.Any(marker => marker.IsAvailableForHarvest), Is.True, "小队落位后当前地块的实体资源棋子应提供采集交互。");

            yield return WaitForActiveHuntSnapshot(persistence, coordinate);
            CampaignSnapshot exploredSnapshot = JsonUtility.FromJson<CampaignSnapshot>(persistence.Payload);
            ActiveHuntTileSnapshot exploredTile = exploredSnapshot.ActiveHunt.Tiles.Single(tile => tile.X == coordinate.x && tile.Y == coordinate.y);
            Assert.That(exploredTile.State, Is.EqualTo(TileState.Revealed));
            Assert.That(exploredTile.ResourcePoints, Is.Not.Empty, "基础路线的非起始地块应提供最小可采集内容。");
            Assert.That(visualizer.GetComponentsInChildren<PlayableHuntResourceMarker3D>(true), Has.Length.GreaterThanOrEqualTo(exploredTile.ResourcePoints.Count));

            Assert.That(explorationPort.TryCreateSnapshot(coordinate, 0, out HuntExplorationSnapshot resourceTarget), Is.True);
            UniTask<bool>.Awaiter selection = explorationPort.SubmitResourcePointAsync(resourceTarget).GetAwaiter();
            yield return WaitForCompletion(selection);
            Assert.That(selection.GetResult(), Is.True);
            HuntHarvestPanel3D harvestPanel = visualizer.GetComponentInChildren<HuntHarvestPanel3D>(true);
            Assert.That(harvestPanel, Is.Not.Null);
            Assert.That(harvestPanel.IsOpen, Is.True);
            int poolCardCount = exploredTile.ResourcePoints[0].MaterialItemIds?.Count > 0 ? exploredTile.ResourcePoints[0].MaterialItemIds.Count : exploredTile.ResourcePoints[0].DrawCount;
            Assert.That(harvestPanel.CardCount, Is.EqualTo(poolCardCount));
            Assert.That(harvestPanel.CardCount, Is.GreaterThan(0));

            for (int cardIndex = 0; cardIndex < exploredTile.ResourcePoints[0].DrawCount; cardIndex++)
            {
                HuntHarvestCard3D harvestCard = harvestPanel.GetComponentsInChildren<HuntHarvestCard3D>(true).Single(card => card.CardIndex == cardIndex);
                Assert.That(harvestCard.RevealRequested, Is.Not.Null);
                Assert.That(harvestPanel.RevealedCount, Is.EqualTo(cardIndex));
                Assert.That(harvestPanel.TryRevealCard(cardIndex), Is.True, $"第 {cardIndex + 1} 张实体采集牌未接受点击。");
                yield return WaitForHarvestStep(harvestPanel, cardIndex + 1);
                HuntHarvestCard3D presentedCard = harvestPanel.GetComponentsInChildren<HuntHarvestCard3D>(true).Single(card => card.CardIndex == cardIndex);
                Assert.That(presentedCard.IsRevealed, Is.True, $"第 {cardIndex + 1} 张实体采集牌未完成翻面。");
            }
            HuntHarvestControlCard3D controlCard = harvestPanel.GetComponentInChildren<HuntHarvestControlCard3D>(true);
            Assert.That(controlCard, Is.Not.Null);
            Assert.That(controlCard.Clicked, Is.Not.Null);
            Assert.That(harvestPanel.IsOperationRunning, Is.False);
            Assert.That(harvestPanel.TryActivateControlCard(), Is.True);
            yield return null;
            Assert.That(harvestPanel.IsOpen, Is.False);
            Assert.That(PlayableHuntInputGuard.IsBlocked, Is.False);

            yield return WaitForActiveHuntSnapshot(persistence, coordinate, true);
            CampaignSnapshot harvestedSnapshot = JsonUtility.FromJson<CampaignSnapshot>(persistence.Payload);
            ActiveHuntTileSnapshot harvestedTile = harvestedSnapshot.ActiveHunt.Tiles.Single(tile => tile.X == coordinate.x && tile.Y == coordinate.y);
            Assert.That(harvestedTile.ResourcePoints[0].IsExhausted, Is.True);

            HuntRetreatPanel3D retreatPanel = visualizer.GetComponentInChildren<HuntRetreatPanel3D>(true);
            Assert.That(retreatPanel, Is.Not.Null);
            retreatPanel.RequestOpen();
            Assert.That(retreatPanel.IsConfirmationOpen, Is.True);
            Assert.That(PlayableHuntInputGuard.IsBlocked, Is.True, "实体回营确认桌打开时应独占狩猎输入。");
            TabletopEventChoiceCard3D confirmRetreat = retreatPanel.GetComponentsInChildren<TabletopEventChoiceCard3D>(true).Single(card => card.DisplayName == "结算并回营");
            if (!confirmRetreat.IsInteractable)
            {
                TabletopEventChoiceCard3D abandonRetreatMaterial = retreatPanel.GetComponentsInChildren<TabletopEventChoiceCard3D>(true).First(card => card.DisplayName.StartsWith("放弃 · "));
                Assert.That(abandonRetreatMaterial.IsInteractable, Is.True, "远离营地且携带素材时应先选择一张放弃素材牌。");
                abandonRetreatMaterial.Clicked.Invoke();
                yield return null;
                confirmRetreat = retreatPanel.GetComponentsInChildren<TabletopEventChoiceCard3D>(true).Single(card => card.DisplayName == "结算并回营");
            }
            Assert.That(confirmRetreat.IsInteractable, Is.True);
            confirmRetreat.Clicked.Invoke();
            yield return WaitForSettlementIdle(manager);
            Assert.That(manager.SettlementData.CurrentYear, Is.EqualTo(initialYear));
            Assert.That(manager.SettlementData.CurrentSeasonIndex, Is.EqualTo(1));
            Assert.That(manager.SettlementData.PendingHuntReturn, Is.Null);

            UniTask<SettlementDepartureCommandResult>.Awaiter nextDeparture = manager.DepartForHuntAsync(new[] { hunterId }, GetDestination(initialYear + 1)).GetAwaiter();
            yield return WaitForCompletion(nextDeparture);
            SettlementDepartureCommandResult nextDepartureResult = nextDeparture.GetResult();
            Assert.That(nextDepartureResult.Succeeded, Is.True, nextDepartureResult.Reason);
            Assert.That(explorationPort.TryCreateSnapshot(Vector2Int.zero, -1, out _), Is.False, "上一轮 View 端口不得为新会话签发快照。");
            UniTask<HuntTileCommandResult>.Awaiter staleRequest = explorationPort.SubmitTileAsync(target).GetAwaiter();
            yield return WaitForCompletion(staleRequest);
            Assert.That(staleRequest.GetResult().Succeeded, Is.False, "上一轮地图 View 的请求不得写入新狩猎会话。");
        }

        [UnityTest]
        public IEnumerator HuntEventParentChild_PersistsCheckpointAcrossManagerRebuildAndUsesRealCards()
        {
            var persistence = new MemoryCampaignPersistence();
            var sourceRandom = new FixedTabletopInteraction(10);
            persistence.SnapshotToLoad = CreateYearThreeSettlementSnapshot();
            GameManager manager = CreateProductionManager(persistence, true, true, sourceRandom);
            UniTask<CampaignStartupResult>.Awaiter start = manager.ContinueCampaignAsync().GetAwaiter();
            yield return WaitForCompletion(start);
            Assert.That(start.GetResult().Succeeded, Is.True, start.GetResult().Reason);
            yield return WaitForSettlementIdle(manager);
            Assert.That(manager.SettlementData.CurrentYear, Is.EqualTo(3));
            int hunterId = manager.SettlementData.GetAliveHunters()[0].InstanceId;
            UniTask<SettlementDepartureCommandResult>.Awaiter departure = manager.DepartForHuntAsync(new[] { hunterId }, GetDestination(3)).GetAwaiter();
            yield return WaitForCompletion(departure);
            Assert.That(departure.GetResult().Succeeded, Is.True, departure.GetResult().Reason);
            yield return WaitForHuntInputReady();

            PlayableSettlementEventView eventView = managerObject.GetComponent<PlayableSettlementEventView>();
            Assert.That(eventView, Is.Not.Null);
            HuntManager huntManager = manager.ActiveHuntRuntime.Manager;
            Assert.That(huntManager.BoundRoute.TryResolveEvent("hunt_rust_burial", out EventData parent), Is.True);
            Assert.That(huntManager.BoundRoute.TryResolveEvent("hunt_rust_burial_open_eyes", out EventData child), Is.True);
            var targetTile = huntManager.Map.Values.FirstOrDefault(tile => tile.State == TileState.Interactable && !tile.HasBossEncounter && tile.Config != null);
            Assert.That(targetTile, Is.Not.Null, "正式路线缺少可注入狩猎事件的可交互地块。");
            HuntMapVisualizer visualizer = managerObject.GetComponentInChildren<HuntMapVisualizer>(true);
            Assert.That(visualizer, Is.Not.Null, "正式狩猎场景缺少地图可视化器。");
            PlayableHexTileCard3D targetTileCard = FindTileCard(visualizer, targetTile.AxialCoord);
            Assert.That(targetTileCard, Is.Not.Null, "目标地块缺少实体地形卡。");
            patchedTileConfig = targetTile.Config;
            originalPatchedTileRevealEvent = patchedTileConfig.tileRevealEvent;
            patchedTileConfig.tileRevealEvent = parent;
            try
            {
                IHuntExplorationPort explorationPort = manager.ActiveHuntExplorationPort;
                Assert.That(explorationPort.TryCreateSnapshot(targetTile.AxialCoord, -1, out HuntExplorationSnapshot snapshot), Is.True);
                // 该夹具验证事件卡跨重建；让实体地形卡翻面 presenter 立即完成，避免批处理帧时间冻结在 IsFlipping。
                targetTileCard.enabled = false;
                UniTask<HuntTileCommandResult>.Awaiter reveal = explorationPort.SubmitTileAsync(snapshot).GetAwaiter();
                yield return WaitForChoice(eventView, "抽一张石片决定挖掘位置");
                Assert.That(eventView.ActivePanel.GetComponentInChildren<TabletopEventPrimaryCard3D>(true).DisplayName, Is.EqualTo("锈蚀葬坑"));
                FindChoice(eventView, "抽一张石片决定挖掘位置").Clicked.Invoke();
                yield return WaitUntil(() => sourceRandom.LastRequest.HasValue, "等待狩猎父事件抽牌请求超时。");
                Assert.That(sourceRandom.LastRequest.Value.Kind, Is.EqualTo(TabletopRandomInteractionKind.DrawCards));
                Assert.That(sourceRandom.LastRequest.Value.Count, Is.EqualTo(1));
                Assert.That(sourceRandom.LastRequest.Value.Sides, Is.EqualTo(10));
                yield return WaitForChoice(eventView, "接受结果");
                FindChoice(eventView, "接受结果").Clicked.Invoke();
                yield return WaitForChoice(eventView, "继续");

                UnityEngine.Object.Destroy(eventView);
                yield return null;
                yield return WaitForCompletion(reveal);
                Assert.That(reveal.GetResult().Succeeded, Is.False, "取消父事件结果确认后，原命令应失败但保留 checkpoint。");
                RestorePatchedTile();
                yield return WaitForPendingHuntEvent(persistence, child.ContentId);

                CampaignSnapshot checkpoint = JsonUtility.FromJson<CampaignSnapshot>(persistence.Payload);
                Assert.That(checkpoint.HasActiveHunt, Is.True);
                Assert.That(checkpoint.ActiveHunt.EventStore.CommittedSequences, Is.Not.Empty);
                Assert.That(checkpoint.ActiveHunt.EventStore.PendingOccurrences.Count(occurrence => occurrence.EventId == child.ContentId), Is.EqualTo(1));
                Assert.That(checkpoint.Settlement.GetResource("metal_fragment"), Is.Zero);
                Assert.That(checkpoint.ActiveHunt.Collectibles.SelectMany(collectible => collectible.Items).Where(item => item.ItemId == "metal_fragment").Sum(item => item.Count), Is.EqualTo(1));
                int checkpointSaveCount = persistence.Snapshots.Count;
                UnityEngine.Object.Destroy(managerObject);
                managerObject = null;
                yield return null;

                persistence.SnapshotToLoad = checkpoint;
                var restoredRandom = new FixedTabletopInteraction(1);
                GameManager restoredManager = CreateProductionManager(persistence, true, true, restoredRandom);
                UniTask<CampaignStartupResult>.Awaiter restore = restoredManager.ContinueCampaignAsync().GetAwaiter();
                yield return WaitForCompletion(restore);
                Assert.That(restore.GetResult().Succeeded, Is.True, restore.GetResult().Reason);
                Assert.That(restoredManager.CurrentGamePhase, Is.EqualTo(GamePhase.Hunt));
                PlayableSettlementEventView restoredEventView = managerObject.GetComponent<PlayableSettlementEventView>();
                UniTask<HuntRetreatCommandResult>.Awaiter retreat = restoredManager.RequestRetreatAsync().GetAwaiter();
                yield return WaitForChoice(restoredEventView, "用一份金属碎片封住石片的缝隙");
                FindChoice(restoredEventView, "用一份金属碎片封住石片的缝隙").Clicked.Invoke();
                yield return WaitForChoice(restoredEventView, "继续");
                FindChoice(restoredEventView, "继续").Clicked.Invoke();
                yield return WaitForCompletion(retreat);
                HuntRetreatCommandResult retreatResult = retreat.GetResult();
                Assert.That(retreatResult.Succeeded, Is.True, retreatResult.Reason);
                yield return WaitForSettlementIdle(restoredManager);
                Assert.That(restoredRandom.RequestCount, Is.Zero, "恢复 child 的安全资源选项不应重放父事件骰子。");
                CampaignSnapshot drainedCheckpoint = persistence.Snapshots.Skip(checkpointSaveCount).LastOrDefault(snapshot => snapshot?.HasActiveHunt == true && snapshot.ActiveHunt.EventStore?.PendingOccurrences?.Count == 0);
                Assert.That(drainedCheckpoint, Is.Not.Null, "child 完成后缺少 pending 清空的活动 Hunt checkpoint。");
                Assert.That(drainedCheckpoint.Settlement.GetResource("metal_fragment"), Is.Zero);
                Assert.That(drainedCheckpoint.ActiveHunt.Collectibles.SelectMany(collectible => collectible.Items).Where(item => item.ItemId == "metal_fragment").Sum(item => item.Count), Is.Zero);
                Assert.That(restoredManager.SettlementData.GetResource("metal_fragment"), Is.Zero);
                Assert.That(restoredManager.SettlementData.PendingHuntReturn, Is.Null);
                Assert.That(restoredEventView.ActivePanel == null || !restoredEventView.ActivePanel.IsOpen, Is.True);
                Assert.That(PlayableHuntInputGuard.IsBlocked, Is.False);
            }
            finally { RestorePatchedTile(); }
        }

        [UnityTest]
        public IEnumerator FailedReturnCheckpoint_LeavesCampaignInHuntWithoutAdvancingYear()
        {
            var persistence = new MemoryCampaignPersistence { RejectPendingReturn = true };
            GameManager manager = CreateProductionManager(persistence);
            yield return WaitForSettlementIdle(manager);
            int initialYear = manager.SettlementData.CurrentYear;
            int hunterId = manager.SettlementData.GetAliveHunters()[0].InstanceId;

            UniTask<SettlementDepartureCommandResult>.Awaiter departure = manager.DepartForHuntAsync(new[] { hunterId }, GetDestination(initialYear)).GetAwaiter();
            yield return WaitForCompletion(departure);
            SettlementDepartureCommandResult departureResult = departure.GetResult();
            Assert.That(departureResult.Succeeded, Is.True, departureResult.Reason);

            UniTask<HuntRetreatCommandResult>.Awaiter retreat = manager.RequestRetreatAsync().GetAwaiter();
            yield return WaitForCompletion(retreat);
            HuntRetreatCommandResult retreatResult = retreat.GetResult();
            Assert.That(retreatResult.Succeeded, Is.False);
            Assert.That(manager.CurrentGamePhase, Is.EqualTo(GamePhase.Hunt));
            Assert.That(manager.SettlementData.CurrentYear, Is.EqualTo(initialYear));
            Assert.That(manager.SettlementData.PendingHuntReturn, Is.Null);
            Assert.That(manager.SettlementData.HuntHistory, Is.Empty);
        }

        [UnityTest]
        public IEnumerator RejectedEncounterHandoff_ReleasesHuntLockAndAllowsRetreat()
        {
            var persistence = new MemoryCampaignPersistence();
            GameManager manager = CreateProductionManager(persistence);
            yield return WaitForSettlementIdle(manager);
            int initialYear = manager.SettlementData.CurrentYear;
            int hunterId = manager.SettlementData.GetAliveHunters()[0].InstanceId;

            UniTask<SettlementDepartureCommandResult>.Awaiter departure = manager.DepartForHuntAsync(new[] { hunterId }, GetDestination(initialYear)).GetAwaiter();
            yield return WaitForCompletion(departure);
            SettlementDepartureCommandResult departureResult = departure.GetResult();
            Assert.That(departureResult.Succeeded, Is.True, departureResult.Reason);

            PlayableHuntActionSession huntSession = manager.ActiveHuntRuntime.ActionSession;
            InvokePrivate(huntSession, "LockEncounterHandoff");
            Assert.That(GetPrivateField<bool>(huntSession, "gameplayLocked"), Is.True);
            var request = new CampaignEncounterRequest(huntSession.SessionId, "missing-encounter", CampaignEncounterSourceKind.HuntEvent, GamePhase.Hunt, Vector2Int.zero, "test-event", GetDestination(initialYear).DestinationId);
            EventBus.Publish(new CampaignEncounterRequestedEvent { Request = request });
            yield return WaitForEncounterLockRelease(huntSession);

            Assert.That(manager.CurrentGamePhase, Is.EqualTo(GamePhase.Hunt));
            Assert.That(manager.IsHuntActionSessionActive, Is.True);
            UniTask<HuntRetreatCommandResult>.Awaiter retreat = manager.RequestRetreatAsync().GetAwaiter();
            yield return WaitForCompletion(retreat);
            HuntRetreatCommandResult retreatResult = retreat.GetResult();
            Assert.That(retreatResult.Succeeded, Is.True, retreatResult.Reason);
        }

        [UnityTest]
        public IEnumerator DelayedAppliedReturnSave_BlocksNextDepartureUntilPersistenceCompletes()
        {
            var persistence = new MemoryCampaignPersistence { DelayAppliedReturn = true };
            GameManager manager = CreateProductionManager(persistence);
            yield return WaitForSettlementIdle(manager);
            int year = manager.SettlementData.CurrentYear;
            int hunterId = manager.SettlementData.GetAliveHunters()[0].InstanceId;

            UniTask<SettlementDepartureCommandResult>.Awaiter departure = manager.DepartForHuntAsync(new[] { hunterId }, GetDestination(year)).GetAwaiter();
            yield return WaitForCompletion(departure);
            SettlementDepartureCommandResult departureResult = departure.GetResult();
            Assert.That(departureResult.Succeeded, Is.True, departureResult.Reason);

            UniTask<HuntRetreatCommandResult>.Awaiter retreat = manager.RequestRetreatAsync().GetAwaiter();
            yield return WaitForCompletion(retreat);
            HuntRetreatCommandResult retreatResult = retreat.GetResult();
            Assert.That(retreatResult.Succeeded, Is.True, retreatResult.Reason);
            yield return WaitForAppliedReturnSave(persistence);

            UniTask<CampaignPhaseTransitionResult>.Awaiter interruptedTransition = manager.TransitionToPhaseAsync(GamePhase.BossFight).GetAwaiter();
            yield return WaitForCompletion(interruptedTransition);
            CampaignPhaseTransitionResult interruptedTransitionResult = interruptedTransition.GetResult();
            Assert.That(interruptedTransitionResult.Succeeded, Is.False, "回营事务未结束时不得切换到其他阶段。");
            Assert.That(manager.CurrentGamePhase, Is.EqualTo(GamePhase.Settlement));
            Assert.That(manager.IsHuntReturnInFlight, Is.True);

            UniTask<SettlementDepartureCommandResult>.Awaiter blockedDeparture = manager.DepartForHuntAsync(new[] { hunterId }, GetDestination(year + 1)).GetAwaiter();
            yield return WaitForCompletion(blockedDeparture);
            Assert.That(blockedDeparture.GetResult().Succeeded, Is.False, "清理检查点持久化期间不得发起下一场远征。");

            persistence.CompleteAppliedReturnSave();
            yield return WaitForSettlementIdle(manager);
            UniTask<SettlementDepartureCommandResult>.Awaiter nextDeparture = manager.DepartForHuntAsync(new[] { hunterId }, GetDestination(year + 1)).GetAwaiter();
            yield return WaitForCompletion(nextDeparture);
            SettlementDepartureCommandResult nextDepartureResult = nextDeparture.GetResult();
            Assert.That(nextDepartureResult.Succeeded, Is.True, nextDepartureResult.Reason);
        }

        [UnityTest]
        public IEnumerator DeferredStartup_WaitsWithoutCreatingOrSavingCampaignRuntime()
        {
            var persistence = new MemoryCampaignPersistence();
            GameManager manager = CreateProductionManager(persistence, true);
            yield return null;
            yield return null;

            Assert.That(manager.CampaignStartupState, Is.EqualTo(CampaignStartupState.AwaitingChoice));
            Assert.That(manager.IsCampaignRuntimeActive, Is.False);
            Assert.That(manager.IsCampaignActionSessionActive, Is.False);
            Assert.That(manager.SettlementData, Is.Null);
            Assert.That(persistence.Snapshots, Is.Empty);
            Assert.That(persistence.DeleteCount, Is.Zero);

            UniTask<CampaignPhaseTransitionResult>.Awaiter transition = manager.TransitionToPhaseAsync(GamePhase.Settlement).GetAwaiter();
            yield return WaitForCompletion(transition);
            Assert.That(transition.GetResult().Succeeded, Is.False);
        }

        [UnityTest]
        public IEnumerator StartNewCampaign_DeletesOnceAndPublishesRuntimeOnce()
        {
            var persistence = new MemoryCampaignPersistence();
            GameManager manager = CreateProductionManager(persistence, true);
            yield return null;

            UniTask<CampaignStartupResult>.Awaiter first = manager.StartNewCampaignAsync().GetAwaiter();
            yield return WaitForCompletion(first);
            CampaignStartupResult firstResult = first.GetResult();
            UniTask<CampaignStartupResult>.Awaiter duplicate = manager.StartNewCampaignAsync().GetAwaiter();
            yield return WaitForCompletion(duplicate);

            Assert.That(firstResult.Succeeded, Is.True, firstResult.Reason);
            Assert.That(duplicate.GetResult().Succeeded, Is.False);
            Assert.That(persistence.DeleteCount, Is.EqualTo(1));
            Assert.That(manager.CampaignStartupState, Is.EqualTo(CampaignStartupState.Active));
            Assert.That(manager.SettlementData, Is.Not.Null);
            yield return WaitForSettlementIdle(manager);
        }

        [UnityTest]
        public IEnumerator StartNewCampaign_DeleteFailureKeepsEntryRetryable()
        {
            var persistence = new MemoryCampaignPersistence { RejectDelete = true };
            GameManager manager = CreateProductionManager(persistence, true);
            yield return null;

            UniTask<CampaignStartupResult>.Awaiter rejected = manager.StartNewCampaignAsync().GetAwaiter();
            yield return WaitForCompletion(rejected);
            Assert.That(rejected.GetResult().Succeeded, Is.False);
            Assert.That(manager.CampaignStartupState, Is.EqualTo(CampaignStartupState.AwaitingChoice));
            Assert.That(manager.SettlementData, Is.Null);
            Assert.That(manager.IsCampaignActionSessionActive, Is.False);

            persistence.RejectDelete = false;
            UniTask<CampaignStartupResult>.Awaiter retry = manager.StartNewCampaignAsync().GetAwaiter();
            yield return WaitForCompletion(retry);
            CampaignStartupResult retryResult = retry.GetResult();
            Assert.That(retryResult.Succeeded, Is.True, retryResult.Reason);
            Assert.That(persistence.DeleteCount, Is.EqualTo(2));
            Assert.That(manager.ActionEnvironmentInstallers.InstallerCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator RestartCampaign_WaitsForReliableDeleteBeforeReplacingRuntime()
        {
            var persistence = new MemoryCampaignPersistence { DelayDelete = true };
            GameManager manager = CreateProductionManager(persistence);
            yield return null;
            yield return WaitForSettlementIdle(manager);
            SettlementInstance previousSettlement = manager.SettlementData;
            SettlementNoticePresenter3D noticePresenter = managerObject.GetComponent<SettlementNoticePresenter3D>();
            Assert.That(noticePresenter, Is.Not.Null);
            EventBus.Publish(new HuntCompletedEvent { CompletedYear = 1, AdvancedToYear = 2, TotalHunts = 1 });
            EventBus.Publish(new HuntCompletedEvent { CompletedYear = 2, AdvancedToYear = 3, TotalHunts = 2 });
            yield return null;
            Assert.That(noticePresenter.IsPresenting, Is.True);
            Assert.That(noticePresenter.PendingNoticeCount, Is.EqualTo(1));

            UniTask<CampaignRestartResult>.Awaiter restart = manager.RestartCampaignAsync().GetAwaiter();
            yield return null;

            Assert.That(restart.IsCompleted, Is.False, "删除仍在等待时不得提前完成重启命令。");
            Assert.That(manager.SettlementData, Is.SameAs(previousSettlement), "删除确认前不得替换权威营地。");
            Assert.That(persistence.DeleteCount, Is.EqualTo(1));
            Assert.That(noticePresenter.IsPresenting, Is.True, "删除确认前不得清空当前战役的营地消息。");
            Assert.That(noticePresenter.PendingNoticeCount, Is.EqualTo(1));

            persistence.CompleteDelete(true);
            yield return WaitForCompletion(restart);
            CampaignRestartResult result = restart.GetResult();

            Assert.That(result.Succeeded, Is.True, result.Reason);
            Assert.That(manager.CurrentGamePhase, Is.EqualTo(GamePhase.Settlement));
            Assert.That(manager.SettlementData, Is.Not.SameAs(previousSettlement));
            Assert.That(manager.SettlementData.GetAliveHunters(), Is.Not.Empty);
            Assert.That(persistence.Payload, Is.Not.Null.And.Not.Empty, "删除完成后必须先建立新战役稳定快照。");
            yield return WaitForSettlementIdle(manager);
            Assert.That(noticePresenter.IsPresenting, Is.False, "成功重启后不得展示上一战役的营地消息。");
            Assert.That(noticePresenter.PendingNoticeCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator RestartCampaign_DeleteFailureKeepsCurrentRuntimeRetryable()
        {
            var persistence = new MemoryCampaignPersistence { RejectDelete = true };
            GameManager manager = CreateProductionManager(persistence);
            yield return null;
            yield return WaitForSettlementIdle(manager);
            SettlementInstance previousSettlement = manager.SettlementData;

            UniTask<CampaignRestartResult>.Awaiter restart = manager.RestartCampaignAsync().GetAwaiter();
            yield return WaitForCompletion(restart);

            Assert.That(restart.GetResult().Succeeded, Is.False);
            Assert.That(manager.SettlementData, Is.SameAs(previousSettlement));
            Assert.That(manager.IsCampaignActionSessionActive, Is.True);

            persistence.RejectDelete = false;
            UniTask<CampaignRestartResult>.Awaiter retry = manager.RestartCampaignAsync().GetAwaiter();
            yield return WaitForCompletion(retry);
            Assert.That(retry.GetResult().Succeeded, Is.True, retry.GetResult().Reason);
            Assert.That(manager.SettlementData, Is.Not.SameAs(previousSettlement));
        }

        [UnityTest]
        public IEnumerator ContinueCampaign_PublishesPreparedSettlementWithoutNewYearProjection()
        {
            var source = new SettlementManager();
            source.EnsureStartingConditions();
            source.Data.CurrentYear = 7;
            source.Data.AddResource("startup-test-resource", 3);
            var persistence = new MemoryCampaignPersistence { SnapshotToLoad = ActiveHuntSnapshotAdapter.CaptureSettlement(source.Data) };
            GameManager manager = CreateProductionManager(persistence, true);
            yield return null;

            UniTask<CampaignStartupResult>.Awaiter load = manager.ContinueCampaignAsync().GetAwaiter();
            yield return WaitForCompletion(load);
            CampaignStartupResult result = load.GetResult();

            Assert.That(result.Succeeded, Is.True, result.Reason);
            Assert.That(manager.CampaignStartupState, Is.EqualTo(CampaignStartupState.Active));
            Assert.That(manager.SettlementData.CurrentYear, Is.EqualTo(7));
            Assert.That(manager.SettlementData.GetResource("startup-test-resource"), Is.EqualTo(3));
            Assert.That(persistence.LoadCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ContinueCampaign_DelayedLoadRejectsDuplicateAndKeepsAwaitingStateOnFailure()
        {
            var persistence = new MemoryCampaignPersistence { DelayLoad = true };
            GameManager manager = CreateProductionManager(persistence, true);
            yield return null;

            UniTask<CampaignStartupResult>.Awaiter first = manager.ContinueCampaignAsync().GetAwaiter();
            yield return null;
            Assert.That(manager.CampaignStartupState, Is.EqualTo(CampaignStartupState.Loading));
            UniTask<CampaignStartupResult>.Awaiter duplicate = manager.ContinueCampaignAsync().GetAwaiter();
            yield return WaitForCompletion(duplicate);
            Assert.That(duplicate.GetResult().Succeeded, Is.False);
            persistence.CompleteLoad(null);
            yield return WaitForCompletion(first);

            Assert.That(first.GetResult().Succeeded, Is.False);
            Assert.That(manager.CampaignStartupState, Is.EqualTo(CampaignStartupState.AwaitingChoice));
            Assert.That(manager.SettlementData, Is.Null);
            Assert.That(manager.IsCampaignActionSessionActive, Is.False);
            Assert.That(persistence.LoadCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ContinueCampaign_RestoresActiveHuntFromDeferredEntry()
        {
            var persistence = new MemoryCampaignPersistence();
            GameManager sourceManager = CreateProductionManager(persistence, true);
            yield return null;
            UniTask<CampaignStartupResult>.Awaiter start = sourceManager.StartNewCampaignAsync().GetAwaiter();
            yield return WaitForCompletion(start);
            Assert.That(start.GetResult().Succeeded, Is.True);
            yield return WaitForSettlementIdle(sourceManager);
            int hunterId = sourceManager.SettlementData.GetAliveHunters()[0].InstanceId;
            UniTask<SettlementDepartureCommandResult>.Awaiter departure = sourceManager.DepartForHuntAsync(new[] { hunterId }, GetDestination(sourceManager.SettlementData.CurrentYear)).GetAwaiter();
            yield return WaitForCompletion(departure);
            SettlementDepartureCommandResult departureResult = departure.GetResult();
            Assert.That(departureResult.Succeeded, Is.True, departureResult.Reason);
            yield return null;
            CampaignSnapshot activeSnapshot = JsonUtility.FromJson<CampaignSnapshot>(persistence.Payload);
            Assert.That(activeSnapshot?.HasActiveHunt, Is.True);

            UnityEngine.Object.Destroy(managerObject);
            managerObject = null;
            yield return null;
            string contentBundleId = activeSnapshot.ActiveHunt.ContentBundleId;
            activeSnapshot.ActiveHunt.ContentBundleId = "missing-startup-bundle";
            persistence.SnapshotToLoad = activeSnapshot;
            GameManager restoredManager = CreateProductionManager(persistence, true);
            yield return null;
            UniTask<CampaignStartupResult>.Awaiter rejectedRestore = restoredManager.ContinueCampaignAsync().GetAwaiter();
            yield return WaitForCompletion(rejectedRestore);
            Assert.That(rejectedRestore.GetResult().Succeeded, Is.False);
            Assert.That(restoredManager.CampaignStartupState, Is.EqualTo(CampaignStartupState.AwaitingChoice));
            Assert.That(restoredManager.SettlementData, Is.Null);
            Assert.That(restoredManager.IsCampaignActionSessionActive, Is.False);
            Assert.That(restoredManager.IsHuntActionSessionActive, Is.False);
            Assert.That(restoredManager.ActionEnvironmentInstallers.InstallerCount, Is.Zero);

            activeSnapshot.ActiveHunt.ContentBundleId = contentBundleId;
            UniTask<CampaignStartupResult>.Awaiter restore = restoredManager.ContinueCampaignAsync().GetAwaiter();
            yield return WaitForCompletion(restore);
            CampaignStartupResult restoreResult = restore.GetResult();

            Assert.That(restoreResult.Succeeded, Is.True, restoreResult.Reason);
            Assert.That(restoredManager.CampaignStartupState, Is.EqualTo(CampaignStartupState.Active));
            Assert.That(restoredManager.CurrentGamePhase, Is.EqualTo(GamePhase.Hunt));
            Assert.That(restoredManager.IsHuntActionSessionActive, Is.True);
            Assert.That(restoredManager.ActiveHuntHunters, Has.Count.EqualTo(1));
        }

        private GameManager CreateProductionManager(ICampaignPersistencePort persistence, bool deferStartup = false, bool useRealEventView = false, ITabletopRandomInteractionPresenter randomPresenter = null)
        {
            PlayableBootstrapSettings settings = Resources.Load<PlayableBootstrapSettings>("HuntingInDarkness/PlayableBootstrapSettings");
            if (contentCandidate == null)
            {
                Assert.That(settings, Is.Not.Null);
                PlayableSymptomRuntime.Configure(settings.Symptoms);
                Assert.That(PlayableCampaignContentAssembler.TryBuild(settings, out contentCandidate, out PlayableContentDiagnosticReport buildReport), Is.True, buildReport.ToString());
                Assert.That(PlayableCampaignContentAssembler.Install(contentCandidate, out PlayableContentDiagnosticReport installReport), Is.True, installReport.ToString());
                destinationCatalog = contentCandidate.HuntDestinations;
            }

            managerObject = new GameObject("GameManager Campaign Loop Smoke");
            managerObject.SetActive(false);
            var manager = managerObject.AddComponent<GameManager>();
            manager.ConfigurePlayableRuntime(contentCandidate.DefaultBattleSetup, contentCandidate.CellSize);
            manager.ConfigureSettlementContent(contentCandidate.SettlementContent);
            manager.ConfigureWorkshopContent(contentCandidate.WorkshopContent);
            Assert.That(manager.ConfigurePlayableStartup(deferStartup), Is.True);
            if (!useRealEventView)
                manager.SetPlayableEventInput(new ImmediateEventInput(() => manager.SettlementData));
            Assert.That(manager.ConfigureTabletopInteraction(randomPresenter ?? new ImmediateTabletopInteraction()), Is.True);
            Assert.That(manager.ConfigureCampaignPersistence(persistence), Is.True);
            if (useRealEventView)
                PlayableGameBootstrap.EnsureRequiredWorldSpacePorts(managerObject, manager, settings);
            managerObject.SetActive(true);
            Assert.That(manager.ConfigureCampaignPersistence(new MemoryCampaignPersistence()), Is.False, "Awake 后即使停用对象也不得替换持久化端口。");
            return manager;
        }

        private PlayableHuntDestination GetDestination(int year)
        {
            List<PlayableHuntDestination> destinations = destinationCatalog.GetAvailable(year);
            Assert.That(destinations, Is.Not.Empty, $"第 {year} 年缺少可用狩猎目的地。");
            return destinations[0];
        }

        private static CampaignSnapshot CreateYearThreeSettlementSnapshot()
        {
            var source = new SettlementManager(17);
            source.EnsureStartingConditions();
            source.Data.CurrentYear = 3;
            source.Data.CurrentSeasonIndex = 0;
            return new CampaignSnapshot { Settlement = source.Data, CampaignSchemaVersion = CampaignSnapshot.CurrentSchemaVersion };
        }

        private void RestorePatchedTile()
        {
            if (patchedTileConfig == null) return;
            patchedTileConfig.tileRevealEvent = originalPatchedTileRevealEvent;
            patchedTileConfig = null;
            originalPatchedTileRevealEvent = null;
        }

        private void EnsureMainCamera()
        {
            if (Camera.main != null) return;
            var cameraObject = new GameObject("Campaign Hunt Smoke Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(managerObject.transform, false);
            cameraObject.AddComponent<Camera>();
        }

        private static PlayableHexTileCard3D FindTileCard(HuntMapVisualizer visualizer, Vector2Int coordinate)
        {
            string expectedName = $"Tile_{coordinate.x}_{coordinate.y}";
            PlayableHexTileCard3D card = visualizer.GetComponentsInChildren<PlayableHexTileCard3D>(true).FirstOrDefault(candidate => candidate.gameObject.name == expectedName);
            Assert.That(card, Is.Not.Null, $"缺少地块实体 {expectedName}。");
            return card;
        }

        private static SettlementItemCard3D FindStorageCard(HunterEquipmentPanel3D panel, ItemData item)
        {
            SlotGrid storageGrid = GetPrivateField<SlotGrid>(panel, "storageGrid");
            return storageGrid.Slots.Select(slot => slot.OccupantCard).OfType<SettlementItemCard3D>().SingleOrDefault(card => card.Item == item);
        }

        private static void BeginAndDrop(SettlementItemCard3D card, CardSlot target)
        {
            MethodInfo beginDrag = typeof(CardView3D).GetMethod("BeginDrag", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(beginDrag, Is.Not.Null);
            beginDrag.Invoke(card, null);
            SetPrivateField(card, "hoverSlot", target);
            MethodInfo endDrag = typeof(SlotDraggableCardView3D).GetMethod("OnEndDrag", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(endDrag, Is.Not.Null);
            endDrag.Invoke(card, null);
        }

        private static TabletopEventChoiceCard3D FindChoice(PlayableSettlementEventView view, string title) => view.ActivePanel.GetComponentsInChildren<TabletopEventChoiceCard3D>(true).Single(card => card.IsInteractable && card.DisplayName == title);

        private static IEnumerator WaitForChoice(PlayableSettlementEventView view, string title)
        {
            yield return WaitUntil(() => view != null && view.ActivePanel != null && view.ActivePanel.IsOpen && view.ActivePanel.GetComponentsInChildren<TabletopEventChoiceCard3D>(true).Any(card => card.IsInteractable && card.DisplayName == title), $"等待实体事件选项 {title} 超时。");
        }

        private static IEnumerator WaitUntil(Func<bool> condition, string message)
        {
            for (int frame = 0; frame < FrameTimeout; frame++)
            {
                if (condition())
                    yield break;
                yield return null;
            }
            Assert.Fail(message);
        }

        private static IEnumerator WaitForHuntInputReady()
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (!PlayableHuntInputGuard.IsBlocked) yield break;
                yield return null;
            }
            Assert.Fail("等待狩猎地图入场运镜释放输入超时。");
        }

        private static IEnumerator WaitForTileFlip(PlayableHexTileCard3D card)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (card != null && card.IsFlipping) yield break;
                yield return null;
            }
            Assert.Fail("等待实体地形卡开始翻面超时。");
        }

        private static IEnumerator WaitForSquadMovement(PlayableHuntSquadPawn3D pawn)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (pawn != null && pawn.IsMoving) yield break;
                yield return null;
            }
            Assert.Fail("等待实体小队棋子开始移动超时。");
        }

        private static IEnumerator WaitForPresentationCompletion<T>(UniTask<T>.Awaiter awaiter)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (awaiter.IsCompleted) yield break;
                yield return null;
            }
            Assert.Fail("等待 3D 表现命令完成超时。");
        }

        private static IEnumerator WaitForHarvestStep(HuntHarvestPanel3D panel, int expectedRevealedCount)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (panel != null && !panel.IsOperationRunning && panel.RevealedCount >= expectedRevealedCount) yield break;
                yield return null;
            }
            Assert.Fail($"等待 3D 采集牌推进超时：revealed={panel?.RevealedCount}, expected={expectedRevealedCount}, running={panel?.IsOperationRunning}。");
        }

        private static IEnumerator WaitForActiveHuntSnapshot(MemoryCampaignPersistence persistence, Vector2Int? squadCoordinate = null, bool requireExhaustedPoint = false)
        {
            for (int frame = 0; frame < FrameTimeout; frame++)
            {
                CampaignSnapshot snapshot = string.IsNullOrWhiteSpace(persistence.Payload) ? null : JsonUtility.FromJson<CampaignSnapshot>(persistence.Payload);
                bool coordinateMatches = !squadCoordinate.HasValue || snapshot?.ActiveHunt != null && snapshot.ActiveHunt.SquadX == squadCoordinate.Value.x && snapshot.ActiveHunt.SquadY == squadCoordinate.Value.y;
                bool exhaustedMatches = !requireExhaustedPoint || snapshot?.ActiveHunt?.Tiles.Any(tile => tile.X == squadCoordinate.Value.x && tile.Y == squadCoordinate.Value.y && tile.ResourcePoints.Any(point => point.IsExhausted)) == true;
                if (snapshot?.HasActiveHunt == true && coordinateMatches && exhaustedMatches) yield break;
                yield return null;
            }
            Assert.Fail("等待活动狩猎检查点持久化超时。");
        }

        private static IEnumerator WaitForPendingHuntEvent(MemoryCampaignPersistence persistence, string eventId)
        {
            for (int frame = 0; frame < FrameTimeout; frame++)
            {
                CampaignSnapshot snapshot = string.IsNullOrWhiteSpace(persistence.Payload) ? null : JsonUtility.FromJson<CampaignSnapshot>(persistence.Payload);
                if (snapshot?.HasActiveHunt == true && snapshot.ActiveHunt.EventStore?.PendingOccurrences?.Count(occurrence => occurrence.EventId == eventId) == 1)
                    yield break;
                yield return new WaitForFixedUpdate();
            }
            Assert.Fail($"等待狩猎事件 checkpoint 持久化超时：{eventId}。");
        }

        private static IEnumerator WaitForPersistedEquipment(MemoryCampaignPersistence persistence, int hunterId, string itemId)
        {
            for (int frame = 0; frame < FrameTimeout; frame++)
            {
                CampaignSnapshot snapshot = string.IsNullOrWhiteSpace(persistence.Payload) ? null : JsonUtility.FromJson<CampaignSnapshot>(persistence.Payload);
                HunterInstance hunter = snapshot?.Settlement?.GetHunter(hunterId);
                if (hunter?.EquippedItemIds?.Count(candidate => candidate == itemId) == 1 && snapshot.Settlement.GetStoredEquipment(itemId) == 0)
                    yield break;
                yield return new WaitForFixedUpdate();
            }
            Assert.Fail($"等待装备 {itemId} 的营地检查点持久化超时。");
        }

        private static IEnumerator WaitForSettlementIdle(GameManager manager)
        {
            int stableFrames = 0;
            for (int frame = 0; frame < FrameTimeout; frame++)
            {
                bool ready = manager.SettlementData != null && manager.IsCampaignActionSessionActive && manager.CurrentGamePhase == GamePhase.Settlement && !manager.IsHuntReturnInFlight && !manager.IsSettlementActionSessionRunning && manager.IsSettlementEventRestoreReady && manager.SettlementData.PendingHuntReturn == null;
                stableFrames = ready ? stableFrames + 1 : 0;
                if (stableFrames >= 2)
                    yield break;
                yield return new WaitForFixedUpdate();
            }
            Assert.Fail($"等待营地 Runner 空闲超时：data={manager.SettlementData != null}, campaign={manager.IsCampaignActionSessionActive}, phase={manager.CurrentGamePhase}, running={manager.IsSettlementActionSessionRunning}, restore={manager.IsSettlementEventRestoreReady}。");
        }

        private static IEnumerator WaitForCompletion<T>(UniTask<T>.Awaiter awaiter)
        {
            for (int frame = 0; frame < FrameTimeout; frame++)
            {
                if (awaiter.IsCompleted)
                    yield break;
                yield return new WaitForFixedUpdate();
            }
            Assert.Fail("等待异步命令完成超时。");
        }

        private static IEnumerator WaitForAppliedReturnSave(MemoryCampaignPersistence persistence)
        {
            for (int frame = 0; frame < FrameTimeout; frame++)
            {
                if (persistence.IsAppliedReturnSavePending)
                    yield break;
                yield return new WaitForFixedUpdate();
            }
            Assert.Fail($"等待已应用回营状态存档开始超时：{persistence.DescribeSnapshots()}。");
        }

        private static IEnumerator WaitForEncounterLockRelease(PlayableHuntActionSession session)
        {
            for (int frame = 0; frame < FrameTimeout; frame++)
            {
                if (!GetPrivateField<bool>(session, "gameplayLocked"))
                    yield break;
                yield return new WaitForFixedUpdate();
            }
            Assert.Fail("等待遭遇交接失败后释放狩猎锁超时。");
        }

        private static void ResetContentAssembly()
        {
            InvokeReset(typeof(PlayableCampaignContentAssembler));
            InvokeReset(typeof(PlayableHuntDestinationRuntime));
            InvokeReset(typeof(PlayableHuntContentRuntime));
            InvokeReset(typeof(PlayableSettlementContentRuntime));
            PlayableEventTableRuntime.ClearCache();
        }

        private static void InvokeReset(System.Type type)
        {
            MethodInfo method = type.GetMethod("ResetRuntimeState", BindingFlags.Static | BindingFlags.NonPublic);
            method?.Invoke(null, null);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"缺少字段 {fieldName}。");
            return (T)field.GetValue(target);
        }

        private static void InvokePrivate(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"缺少方法 {methodName}。");
            method.Invoke(target, null);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = null;
            for (System.Type type = target.GetType(); type != null && field == null; type = type.BaseType)
                field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            Assert.That(field, Is.Not.Null, $"缺少字段 {fieldName}。");
            field.SetValue(target, value);
        }

        private sealed class ImmediateEventInput : IPlayableEventInput
        {
            private readonly System.Func<SettlementInstance> settlementProvider;

            public ImmediateEventInput(System.Func<SettlementInstance> settlementProvider)
            {
                this.settlementProvider = settlementProvider ?? throw new System.ArgumentNullException(nameof(settlementProvider));
            }

            public UniTask ConfirmNarrativeAsync(EventData gameEvent, HunterInstance actor, CancellationToken cancellationToken) => UniTask.CompletedTask;

            public UniTask<PlayableEventChoiceSelection> SelectChoiceAsync(EventData gameEvent, HunterInstance actor, IReadOnlyList<HunterInstance> hunters, IPlayableEventResourceAvailability resourceAvailability, CancellationToken cancellationToken)
            {
                SettlementInstance settlement = settlementProvider();
                if (gameEvent?.options == null || settlement == null)
                    return UniTask.FromResult(new PlayableEventChoiceSelection(-1, null));
                for (int optionIndex = 0; optionIndex < gameEvent.options.Count; optionIndex++)
                {
                    EventOption option = gameEvent.options[optionIndex];
                    bool needsHunter = option.checkType != CheckType.None || PlayableEventOptionAvailability.RequiresHunter(option);
                    if (actor != null && PlayableEventOptionAvailability.CanUse(option, actor, settlement, resourceAvailability, out _))
                        return UniTask.FromResult(new PlayableEventChoiceSelection(optionIndex, actor));
                    if (!needsHunter && PlayableEventOptionAvailability.CanUse(option, null, settlement, resourceAvailability, out _))
                        return UniTask.FromResult(new PlayableEventChoiceSelection(optionIndex, null));
                    if (hunters == null) continue;
                    foreach (HunterInstance hunter in hunters)
                        if (hunter != null && PlayableEventOptionAvailability.CanUse(option, hunter, settlement, resourceAvailability, out _))
                            return UniTask.FromResult(new PlayableEventChoiceSelection(optionIndex, hunter));
                }
                return UniTask.FromResult(new PlayableEventChoiceSelection(-1, null));
            }

            public UniTask<PlayableEventCheckDecision> PresentCheckAsync(PlayableEventChoiceTransaction transaction, CancellationToken cancellationToken) => UniTask.FromResult(PlayableEventCheckDecision.Accept);

            public UniTask ConfirmResultAsync(EventData gameEvent, EventResolutionResult result, CancellationToken cancellationToken) => UniTask.CompletedTask;
        }

        private sealed class ImmediateTabletopInteraction : ITabletopRandomInteractionPresenter
        {
            public UniTask<TabletopRandomInteractionResult> PresentAsync(TabletopRandomInteractionRequest request, CancellationToken cancellationToken)
            {
                var values = new List<int>(request.Count);
                var cardIds = new List<string>(request.Count);
                for (int index = 0; index < request.Count; index++)
                {
                    values.Add(1);
                    if (request.Kind != TabletopRandomInteractionKind.PhysicalDice)
                        cardIds.Add($"{request.DeckId}:{index}");
                }
                return UniTask.FromResult(new TabletopRandomInteractionResult(request.InteractionId, values, cardIds));
            }
        }

        private sealed class FixedTabletopInteraction : ITabletopRandomInteractionPresenter
        {
            private readonly int value;

            public FixedTabletopInteraction(int value) => this.value = value;

            public int RequestCount { get; private set; }
            public TabletopRandomInteractionRequest? LastRequest { get; private set; }

            public UniTask<TabletopRandomInteractionResult> PresentAsync(TabletopRandomInteractionRequest request, CancellationToken cancellationToken)
            {
                RequestCount++;
                LastRequest = request;
                var values = new List<int>(request.Count);
                var cardIds = new List<string>(request.Count);
                for (int index = 0; index < request.Count; index++)
                {
                    values.Add(value);
                    if (request.Kind != TabletopRandomInteractionKind.PhysicalDice)
                        cardIds.Add($"{request.DeckId}:{index}");
                }
                return UniTask.FromResult(new TabletopRandomInteractionResult(request.InteractionId, values, cardIds));
            }
        }

        private sealed class MemoryCampaignPersistence : ICampaignPersistencePort
        {
            public bool RejectPendingReturn { get; set; }
            public bool DelayAppliedReturn { get; set; }
            public bool DelayLoad { get; set; }
            public bool DelayDelete { get; set; }
            public CampaignSnapshot SnapshotToLoad { get; set; }
            public int DeleteCount { get; private set; }
            public int LoadCount { get; private set; }
            public string Payload { get; private set; }
            public List<CampaignSnapshot> Snapshots { get; } = new();
            private List<bool> pendingReturnFlags = new();
            public bool IsAppliedReturnSavePending => appliedReturnSaveCompletion != null;
            private UniTaskCompletionSource<bool> appliedReturnSaveCompletion;
            private UniTaskCompletionSource<CampaignSnapshot> loadCompletion;
            private UniTaskCompletionSource<bool> deleteCompletion;
            private bool hasDelayedAppliedReturn;

            public UniTask<bool> TrySavePayloadAsync(string payload, CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CampaignSnapshot snapshot = JsonUtility.FromJson<CampaignSnapshot>(payload);
                Snapshots.Add(snapshot);
                bool hasPendingReturn = payload.Contains("\"PendingHuntReturn\": {");
                pendingReturnFlags.Add(hasPendingReturn);
                if (RejectPendingReturn && hasPendingReturn)
                    return UniTask.FromResult(false);
                Payload = payload;
                if (DelayAppliedReturn && !hasDelayedAppliedReturn && snapshot?.Settlement != null && (snapshot.Settlement.CurrentSeasonIndex > 0 || snapshot.Settlement.CurrentYear > 1) && hasPendingReturn && snapshot.Settlement.HuntHistory.Count == 1)
                {
                    hasDelayedAppliedReturn = true;
                    appliedReturnSaveCompletion = new UniTaskCompletionSource<bool>();
                    return appliedReturnSaveCompletion.Task;
                }
                return UniTask.FromResult(true);
            }

            public bool TrySavePayloadImmediate(string payload)
            {
                Payload = payload;
                return true;
            }

            public UniTask<bool> HasSaveAsync(CancellationToken cancellationToken = default) => UniTask.FromResult(SnapshotToLoad != null || !string.IsNullOrWhiteSpace(Payload));

            public UniTask<CampaignSnapshot> LoadAsync(CancellationToken cancellationToken = default)
            {
                LoadCount++;
                if (!DelayLoad)
                {
                    NormalizeLoadedSnapshot(SnapshotToLoad);
                    return UniTask.FromResult(SnapshotToLoad);
                }
                loadCompletion = new UniTaskCompletionSource<CampaignSnapshot>();
                return loadCompletion.Task;
            }

            public bool RejectDelete { get; set; }

            public UniTask<bool> TryDeleteAsync(CancellationToken cancellationToken = default)
            {
                DeleteCount++;
                if (RejectDelete) return UniTask.FromResult(false);
                if (DelayDelete)
                {
                    deleteCompletion = new UniTaskCompletionSource<bool>();
                    return deleteCompletion.Task;
                }
                Payload = null;
                SnapshotToLoad = null;
                return UniTask.FromResult(true);
            }

            public void CompleteDelete(bool succeeded)
            {
                if (succeeded)
                {
                    Payload = null;
                    SnapshotToLoad = null;
                }
                UniTaskCompletionSource<bool> completion = deleteCompletion;
                deleteCompletion = null;
                DelayDelete = false;
                completion?.TrySetResult(succeeded);
            }

            public void CompleteLoad(CampaignSnapshot snapshot)
            {
                NormalizeLoadedSnapshot(snapshot);
                UniTaskCompletionSource<CampaignSnapshot> completion = loadCompletion;
                loadCompletion = null;
                completion?.TrySetResult(snapshot);
            }

            private static void NormalizeLoadedSnapshot(CampaignSnapshot snapshot)
            {
                if (snapshot?.Settlement?.PendingHuntReturn != null && string.IsNullOrWhiteSpace(snapshot.Settlement.PendingHuntReturn.RecordId))
                    snapshot.Settlement.PendingHuntReturn = null;
            }

            public void CompleteAppliedReturnSave()
            {
                UniTaskCompletionSource<bool> completion = appliedReturnSaveCompletion;
                appliedReturnSaveCompletion = null;
                completion?.TrySetResult(true);
            }

            public string DescribeSnapshots()
            {
                var descriptions = new List<string>();
                for (int index = 0; index < Snapshots.Count; index++)
                {
                    CampaignSnapshot snapshot = Snapshots[index];
                    descriptions.Add($"year={snapshot?.Settlement?.CurrentYear},pending={pendingReturnFlags[index]},history={snapshot?.Settlement?.HuntHistory?.Count}");
                }
                return string.Join(";", descriptions);
            }

            public bool HasAppliedPendingSave(int year)
            {
                for (int index = 0; index < Snapshots.Count; index++)
                    if (Snapshots[index]?.Settlement?.CurrentYear == year && pendingReturnFlags[index]) return true;
                return false;
            }

        }
    }
}
