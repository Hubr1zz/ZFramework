using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
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
using HuntingInDarkness.Hunt;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
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
            if (managerObject != null)
                Object.Destroy(managerObject);
            yield return null;
            ResetContentAssembly();
        }

        [UnityTest]
        public IEnumerator PublicCommands_CompleteOneYearLoopAndAllowNextDeparture()
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
            Assert.That(manager.SettlementData.CurrentYear, Is.EqualTo(initialYear + 1));
            Assert.That(manager.SettlementData.HuntHistory, Has.Count.EqualTo(1));
            Assert.That(manager.SettlementData.PendingHuntReturn, Is.Null);
            Assert.That(manager.SettlementData.DepartingHunterIds, Is.Empty);
            Assert.That(persistence.HasAppliedPendingSave(initialYear + 1), Is.True, "缺少已应用但仍保留回营检查点的第一阶段存档。");

            UniTask<SettlementDepartureCommandResult>.Awaiter nextDeparture = manager.DepartForHuntAsync(new[] { hunterId }, GetDestination(manager.SettlementData.CurrentYear)).GetAwaiter();
            yield return WaitForCompletion(nextDeparture);
            SettlementDepartureCommandResult nextDepartureResult = nextDeparture.GetResult();
            Assert.That(nextDepartureResult.Succeeded, Is.True, nextDepartureResult.Reason);
            Assert.That(manager.CurrentGamePhase, Is.EqualTo(GamePhase.Hunt));
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
            Assert.That(manager.SettlementData.CurrentYear, Is.EqualTo(initialYear + 1));
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
            Assert.That(saved.Settlement.CurrentYear, Is.EqualTo(initialYear + 1));
            Assert.That(saved.Settlement.PendingHuntReturn?.RecordId, Is.Null.Or.Empty, "最终存档不得保留有效的回营检查点。");
            Assert.That(saved.Settlement.HuntHistory, Has.Count.EqualTo(1));
            Assert.That(saved.Settlement.GetResource(blackSalt.ContentId), Is.Zero);
            Assert.That(saved.Settlement.HasDiscoveredMaterial(blackSalt.ContentId), Is.True);
            Assert.That(saved.Settlement.GetStoredEquipment(saltWard.ContentId), Is.Zero);
            Assert.That(saved.Settlement.GetHunter(hunterId).EquippedItemIds.Count(itemId => itemId == saltWard.ContentId), Is.EqualTo(1));

            persistence.SnapshotToLoad = saved;
            Object.Destroy(managerObject);
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

            UniTask<HuntRetreatCommandResult>.Awaiter retreat = manager.RequestRetreatAsync().GetAwaiter();
            yield return WaitForCompletion(retreat);
            HuntRetreatCommandResult retreatResult = retreat.GetResult();
            Assert.That(retreatResult.Succeeded, Is.True, retreatResult.Reason);
            yield return WaitForSettlementIdle(manager);
            Assert.That(manager.SettlementData.CurrentYear, Is.EqualTo(initialYear + 1));
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

            Object.Destroy(managerObject);
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

        private GameManager CreateProductionManager(ICampaignPersistencePort persistence, bool deferStartup = false)
        {
            if (contentCandidate == null)
            {
                PlayableBootstrapSettings settings = Resources.Load<PlayableBootstrapSettings>("HuntingInDarkness/PlayableBootstrapSettings");
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
            manager.SetPlayableEventInput(new ImmediateEventInput());
            Assert.That(manager.ConfigureTabletopInteraction(new ImmediateTabletopInteraction()), Is.True);
            Assert.That(manager.ConfigureCampaignPersistence(persistence), Is.True);
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

        private sealed class ImmediateEventInput : IPlayableEventInput
        {
            public UniTask ConfirmNarrativeAsync(EventData gameEvent, HunterInstance actor, CancellationToken cancellationToken) => UniTask.CompletedTask;

            public UniTask<PlayableEventChoiceSelection> SelectChoiceAsync(EventData gameEvent, HunterInstance actor, IReadOnlyList<HunterInstance> hunters, CancellationToken cancellationToken)
            {
                HunterInstance selectedActor = actor ?? (hunters != null && hunters.Count > 0 ? hunters[0] : null);
                return UniTask.FromResult(new PlayableEventChoiceSelection(0, selectedActor));
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

        private sealed class MemoryCampaignPersistence : ICampaignPersistencePort
        {
            public bool RejectPendingReturn { get; set; }
            public bool DelayAppliedReturn { get; set; }
            public bool DelayLoad { get; set; }
            public CampaignSnapshot SnapshotToLoad { get; set; }
            public int DeleteCount { get; private set; }
            public int LoadCount { get; private set; }
            public string Payload { get; private set; }
            public List<CampaignSnapshot> Snapshots { get; } = new();
            private List<bool> pendingReturnFlags = new();
            public bool IsAppliedReturnSavePending => appliedReturnSaveCompletion != null;
            private UniTaskCompletionSource<bool> appliedReturnSaveCompletion;
            private UniTaskCompletionSource<CampaignSnapshot> loadCompletion;
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
                if (DelayAppliedReturn && !hasDelayedAppliedReturn && snapshot?.Settlement?.CurrentYear > 1 && hasPendingReturn && snapshot.Settlement.HuntHistory.Count == 1)
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

            public UniTask<bool> HasSaveAsync(CancellationToken cancellationToken = default) => UniTask.FromResult(!string.IsNullOrWhiteSpace(Payload));

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
                Payload = null;
                SnapshotToLoad = null;
                return UniTask.FromResult(true);
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
