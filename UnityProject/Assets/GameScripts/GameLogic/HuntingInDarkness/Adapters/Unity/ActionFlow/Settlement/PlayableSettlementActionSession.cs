using System;
using System.Collections.Generic;
using System.Threading;
using CardGame.ActionQueue;
using Cysharp.Threading.Tasks;
using Core;
using HuntingInDarkness.ActionFlow.Events;
using HuntingInDarkness.ActionFlow.Presentation;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Hunters;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.Settlement;

namespace HuntingInDarkness.ActionFlow.Settlement
{
    /// <summary>营地阶段的 Action 执行环境；持久营地数据由 SettlementInstance 继续拥有。</summary>
    public sealed class PlayableSettlementActionSession : IDisposable
    {
        private readonly SettlementInstance settlement;
        private readonly IWeaponTrainingContent weaponTrainingContent;
        private readonly ISettlementCareContent careContent;
        private readonly ISettlementEquipmentContent equipmentContent;
        private readonly ISettlementConsumableContent consumableContent;
        private readonly WorkshopSystem workshopSystem;
        private readonly InventionSystem inventionSystem;
        private readonly PlayableWorkshopCatalog workshopCatalog;
        private readonly ISettlementSymptomContent symptomContent;
        private readonly PlayableWorkshopConstructionService workshopConstructionService;
        private readonly EventSystem eventSystem;
        private readonly IPlayableEventSettlementCommand settlementEventCommand;
        private readonly Func<string, EventData> resolveEvent;
        private readonly TimelineSystem timelineSystem;
        private readonly HunterManagementSystem hunterManagement;
        private readonly IPlayableCampaignPersistentEffectProjection persistentEffectProjection;
        private readonly ITabletopRandomInteractionPresenter randomInteractionPresenter;
        private readonly ActionEnvironment environment;

        public PlayableSettlementActionSession(SettlementInstance settlement, IWeaponTrainingContent weaponTrainingContent, EventSystem eventSystem = null, IPlayableEventInput eventInput = null, ISettlementCareContent careContent = null, ISettlementEquipmentContent equipmentContent = null, ITabletopRandomInteractionPresenter randomInteractionPresenter = null, WorkshopSystem workshopSystem = null, InventionSystem inventionSystem = null, PlayableWorkshopCatalog workshopCatalog = null, ISettlementSymptomContent symptomContent = null, IActionEnvironmentInstallerRegistry installerRegistry = null, Func<string, EventData> resolveEvent = null, TimelineSystem timeline = null, HunterManagementSystem hunterManagement = null, ISettlementConsumableContent consumableContent = null, IPlayableCampaignPersistentEffectProjection persistentEffectProjection = null)
        {
            this.settlement = settlement ?? throw new ArgumentNullException(nameof(settlement));
            this.weaponTrainingContent = weaponTrainingContent ?? throw new ArgumentNullException(nameof(weaponTrainingContent));
            this.careContent = careContent ?? new PlayableSettlementCareContentAdapter(null);
            this.equipmentContent = equipmentContent ?? new PlayableSettlementEquipmentContentAdapter(null);
            this.consumableContent = consumableContent ?? new PlayableSettlementConsumableContentAdapter(null);
            this.workshopSystem = workshopSystem;
            this.inventionSystem = inventionSystem;
            this.workshopCatalog = workshopCatalog;
            this.symptomContent = symptomContent;
            workshopConstructionService = new PlayableWorkshopConstructionService(() => this.settlement);
            this.eventSystem = eventSystem;
            settlementEventCommand = new SettlementHuntNoiseLeaseCommand(this.settlement, persistentEffectProjection);
            this.resolveEvent = resolveEvent;
            timelineSystem = timeline ?? new TimelineSystem(settlement, new SystemRandomSource());
            this.hunterManagement = hunterManagement ?? new HunterManagementSystem(settlement, new SystemRandomSource());
            this.persistentEffectProjection = persistentEffectProjection;
            this.randomInteractionPresenter = randomInteractionPresenter;
            EventInput = eventInput;
            SessionId = Guid.NewGuid();
            environment = new ActionEnvironment(new ActionEnvironmentConfiguration
            {
                Name = "Settlement",
                Kind = ActionEnvironmentKind.Settlement,
                MaxActionsPerChain = 128,
                TraceCapacity = 24
            }, installerRegistry);
        }

        public bool IsActive => !environment.IsDisposed;
        public Guid SessionId { get; }
        public ReactorRegistry Reactors => environment.Reactors;
        public ReactionGateRegistry ReactionGates => environment.ReactionGates;
        public bool IsRunning => environment.IsRunning;
        public IPlayableEventInput EventInput { get; set; }

        public async UniTask<SettlementDepartureCommandResult> PrepareDepartureAsync(System.Collections.Generic.IReadOnlyList<int> hunterIds, CancellationToken cancellationToken = default)
        {
            if (!IsActive)
                return SettlementDepartureCommandResult.Failed("当前不在营地阶段。");

            var outbox = new ActionEventOutbox();
            ReactorEntityHandle settlementEntity = environment.EntityHandles.GetOrCreate("settlement", "active", "营地");
            ReactorEntityHandle squadEntity = environment.EntityHandles.GetOrCreate("hunt-squad", SessionId.ToString("N"), "本次狩猎小队");
            var action = new PrepareSettlementDepartureAction(settlement, hunterIds, outbox, settlementEntity, squadEntity);
            ActionOutcome outcome = await environment.ExecuteAsync(action, outbox, cancellationToken: cancellationToken);
            if (outcome.IsSuccess)
                return action.Result;
            return string.IsNullOrWhiteSpace(action.Result.Reason) ? SettlementDepartureCommandResult.Failed(outcome.Reason) : action.Result;
        }

        public async UniTask<SettlementHuntReturnCommandResult> ApplyHuntReturnAsync(HuntRecord huntRecord, CancellationToken cancellationToken = default)
        {
            if (!IsActive) return SettlementHuntReturnCommandResult.Failed("当前不在营地阶段。");
            if (huntRecord == null) return SettlementHuntReturnCommandResult.Failed("狩猎记录为空。");

            var outbox = new ActionEventOutbox();
            ReactorEntityHandle settlementEntity = environment.EntityHandles.GetOrCreate("settlement", "active", "营地");
            ReactorEntityHandle huntEntity = environment.EntityHandles.GetOrCreate("hunt-return", huntRecord.RecordId ?? "legacy", "远征归来");
            var action = new ApplySettlementHuntReturnAction(timelineSystem, huntRecord, outbox, settlement, hunterManagement, persistentEffectProjection, settlementEntity, huntEntity);
            ActionOutcome outcome = await environment.ExecuteAsync(action, outbox, cancellationToken: cancellationToken);
            if (outcome.IsSuccess) return action.Result;
            return string.IsNullOrWhiteSpace(action.Result.Reason) ? SettlementHuntReturnCommandResult.Failed(outcome.Reason) : action.Result;
        }

        public bool CanRecruit(out string reason)
        {
            bool hasTemplate = false;
            foreach (HunterData template in careContent.RecruitmentTemplates)
                if (template != null)
                {
                    hasTemplate = true;
                    break;
                }
            if (!hasTemplate)
            {
                reason = "没有可用的新猎人模板。";
                return false;
            }

            int livingCount = settlement.GetAliveHunters().Count;
            int resourceCost = RecruitmentRules.GetCost(livingCount, careContent.RecruitmentCost);
            if (resourceCost > 0 && string.IsNullOrWhiteSpace(careContent.RecruitmentCostResourceId))
            {
                reason = "招募成本尚未配置。";
                return false;
            }
            int availableResource = string.IsNullOrWhiteSpace(careContent.RecruitmentCostResourceId) ? 0 : settlement.GetResource(careContent.RecruitmentCostResourceId);
            return RecruitmentRules.CanRecruit(settlement.CurrentYear, settlement.LastRecruitmentYear, livingCount, careContent.MaximumLivingHunters, availableResource, careContent.RecruitmentCost, out reason);
        }

        public async UniTask<RecruitHunterCommandResult> RecruitHunterAsync(HunterData template, string requestedName)
        {
            if (!IsActive) return RecruitHunterCommandResult.Failed("当前不在营地阶段。");

            var outbox = new ActionEventOutbox();
            ReactorEntityHandle settlementEntity = environment.EntityHandles.GetOrCreate("settlement", "active", "营地");
            ReactorEntityHandle recruitEntity = environment.EntityHandles.GetOrCreate("recruitment-template", template != null ? template.name : "unknown", template != null ? template.hunterName : "未知猎人模板");
            int resourceCost = RecruitmentRules.GetCost(settlement.GetAliveHunters().Count, careContent.RecruitmentCost);
            var action = new RecruitHunterAction(settlement, template, requestedName, careContent.RecruitmentTemplates, careContent.RecruitmentCostResourceId, resourceCost, careContent.MaximumLivingHunters, outbox, settlementEntity, recruitEntity);
            ActionOutcome outcome = await environment.ExecuteAsync(action, outbox);
            if (outcome.IsSuccess) return action.Result;
            return string.IsNullOrWhiteSpace(action.Result.Reason) ? RecruitHunterCommandResult.Failed(outcome.Reason) : action.Result;
        }

        public bool HasRecoverableHunter()
        {
            foreach (HunterInstance hunter in settlement.GetAvailableHunters())
                if (IsWounded(hunter))
                    return true;
            return false;
        }

        public bool CanRecoverHunter(int hunterId, HunterBodyPart bodyPart, out string reason)
        {
            HunterInstance hunter = settlement.GetHunter(hunterId);
            if (!HunterRecoveryRules.CanRecover(hunter, bodyPart, out reason)) return false;
            if (careContent.RecoveryCost == 0) return true;
            if (string.IsNullOrWhiteSpace(careContent.RecoveryCostResourceId))
            {
                reason = "休养成本尚未配置。";
                return false;
            }
            if (settlement.GetResource(careContent.RecoveryCostResourceId) >= careContent.RecoveryCost) return true;
            reason = $"缺少 {careContent.RecoveryCostResourceId}。";
            return false;
        }

        public async UniTask<RecoverHunterCommandResult> RecoverHunterAsync(int hunterId, HunterBodyPart bodyPart)
        {
            if (!IsActive) return RecoverHunterCommandResult.Failed("当前不在营地阶段。");
            HunterInstance hunter = settlement.GetHunter(hunterId);
            if (hunter == null) return RecoverHunterCommandResult.Failed("猎人不属于当前营地。");

            var outbox = new ActionEventOutbox();
            ReactorEntityHandle settlementEntity = environment.EntityHandles.GetOrCreate("settlement", "active", "营地");
            ReactorEntityHandle hunterEntity = environment.EntityHandles.GetOrCreate("hunter", hunter.InstanceId.ToString(), hunter.Name);
            var action = new RecoverHunterAction(settlement, hunter, bodyPart, careContent.RecoveryCostResourceId, careContent.RecoveryCost, careContent.RecoveryAmount, outbox, settlementEntity, hunterEntity);
            ActionOutcome outcome = await environment.ExecuteAsync(action, outbox);
            if (outcome.IsSuccess) return action.Result;
            return string.IsNullOrWhiteSpace(action.Result.Reason) ? RecoverHunterCommandResult.Failed(outcome.Reason) : action.Result;
        }

        public bool CanUseConsumable(int hunterId, ItemData item, HunterBodyPart bodyPart, out string reason)
        {
            if (!IsActive)
            {
                reason = "当前不在营地阶段。";
                return false;
            }
            HunterInstance hunter = settlement.GetHunter(hunterId);
            if (hunter == null || !hunter.IsAvailable)
            {
                reason = "猎人不属于当前营地或当前不可用。";
                return false;
            }
            if (!consumableContent.TryGet(item, out ConsumableUsePlan plan))
            {
                reason = "消耗品内容尚未配置。";
                return false;
            }
            if (plan.Effect != ConsumableEffectKind.RecoverBodyPart)
            {
                reason = "消耗品效果尚未支持。";
                return false;
            }
            if (!HunterRecoveryRules.CanRecover(hunter, bodyPart, out reason)) return false;
            if (settlement.GetStoredItem(item) > 0) return true;
            reason = "营地库存中没有该消耗品。";
            return false;
        }

        public async UniTask<SettlementConsumableCommandResult> UseConsumableAsync(int hunterId, ItemData item, HunterBodyPart bodyPart, CancellationToken cancellationToken = default)
        {
            if (!IsActive) return SettlementConsumableCommandResult.Failed("当前不在营地阶段。");
            HunterInstance hunter = settlement.GetHunter(hunterId);
            if (hunter == null) return SettlementConsumableCommandResult.Failed("猎人不属于当前营地。");
            var outbox = new ActionEventOutbox();
            ReactorEntityHandle itemEntity = environment.EntityHandles.GetOrCreate("settlement-item", item != null ? item.ContentId : "unknown", item != null ? item.itemName : "未知消耗品");
            ReactorEntityHandle hunterEntity = environment.EntityHandles.GetOrCreate("hunter", hunter.InstanceId.ToString(), hunter.Name);
            var action = new UseSettlementConsumableAction(settlement, hunter, item, bodyPart, consumableContent, outbox, itemEntity, hunterEntity);
            ActionOutcome outcome = await environment.ExecuteAsync(action, outbox, cancellationToken: cancellationToken);
            if (outcome.IsSuccess) return action.Result;
            return string.IsNullOrWhiteSpace(action.Result.Reason) ? SettlementConsumableCommandResult.Failed(outcome.Reason) : action.Result;
        }

        public bool CanTrainWeapon(int hunterId, string masteryId, out string reason)
        {
            HunterInstance hunter = settlement.GetHunter(hunterId);
            if (hunter == null || !weaponTrainingContent.TryGetFamily(masteryId, out _))
            {
                reason = "训练内容尚未配置";
                return false;
            }
            if (!WeaponMasteryRules.CanIncrease(hunter, masteryId))
            {
                reason = "熟练度已达到上限";
                return false;
            }
            return WeaponTrainingRules.CanTrain(hunter.IsAvailable && !hunter.IsDead, settlement.IsInventionUnlocked(weaponTrainingContent.RequiredInventionId), settlement.GetResource(weaponTrainingContent.CostResourceId), weaponTrainingContent.ResourceCost, masteryId, weaponTrainingContent.Experience, out reason);
        }

        public bool CanSpendHunterGrowth(int hunterId, HunterGrowthChoice choice, out string reason)
        {
            return HunterAdvancementRules.CanSpendGrowth(settlement.GetHunter(hunterId), choice, out reason);
        }

        public async UniTask<HunterGrowthCommandResult> SpendHunterGrowthAsync(int hunterId, HunterGrowthChoice choice)
        {
            if (!IsActive) return HunterGrowthCommandResult.Failed("当前不在营地阶段。");
            HunterInstance hunter = settlement.GetHunter(hunterId);
            if (hunter == null) return HunterGrowthCommandResult.Failed("猎人不属于当前营地。");

            var outbox = new ActionEventOutbox();
            ReactorEntityHandle settlementEntity = environment.EntityHandles.GetOrCreate("settlement", "active", "营地");
            ReactorEntityHandle hunterEntity = environment.EntityHandles.GetOrCreate("hunter", hunter.InstanceId.ToString(), hunter.Name);
            var action = new SpendHunterGrowthAction(settlement, hunter, choice, outbox, settlementEntity, hunterEntity);
            ActionOutcome outcome = await environment.ExecuteAsync(action, outbox);
            if (outcome.IsSuccess) return action.Result;
            return string.IsNullOrWhiteSpace(action.Result.Reason) ? HunterGrowthCommandResult.Failed(outcome.Reason) : action.Result;
        }

        public IReadOnlyList<SymptomDefinition> GetHunterSymptoms(int hunterId)
        {
            HunterInstance hunter = settlement.GetHunter(hunterId);
            var definitions = new System.Collections.Generic.List<SymptomDefinition>();
            if (hunter?.SymptomStates == null || symptomContent == null) return definitions;
            foreach (HunterSymptomState state in hunter.SymptomStates)
                if (state != null && !state.IsOvercome && symptomContent.TryGetById(state.SymptomId, out SymptomDefinition definition))
                    definitions.Add(definition);
            return definitions;
        }

        public bool CanResolveHunterSymptom(int hunterId, string symptomId, SymptomResolutionChoice choice, out string reason)
        {
            HunterInstance hunter = settlement.GetHunter(hunterId);
            if (hunter == null)
            {
                reason = "猎人不属于当前营地。";
                return false;
            }
            if (symptomContent == null || !symptomContent.TryGetById(symptomId, out SymptomDefinition definition))
            {
                reason = "症状内容尚未配置。";
                return false;
            }
            if (choice == SymptomResolutionChoice.Internalize) return HunterSymptomRules.CanInternalize(hunter, definition, settlement.CurrentYear, out reason);
            if (choice == SymptomResolutionChoice.Overcome) return HunterSymptomRules.CanOvercome(hunter, definition, out reason);
            reason = "症状处理方式无效。";
            return false;
        }

        public async UniTask<HunterSymptomCommandResult> ResolveHunterSymptomAsync(int hunterId, string symptomId, SymptomResolutionChoice choice)
        {
            if (!IsActive) return HunterSymptomCommandResult.Failed("当前不在营地阶段。");
            HunterInstance hunter = settlement.GetHunter(hunterId);
            if (hunter == null) return HunterSymptomCommandResult.Failed("猎人不属于当前营地。");
            if (symptomContent == null) return HunterSymptomCommandResult.Failed("症状内容尚未配置。");

            var outbox = new ActionEventOutbox();
            ReactorEntityHandle hunterEntity = environment.EntityHandles.GetOrCreate("hunter", hunter.InstanceId.ToString(), hunter.Name);
            ReactorEntityHandle symptomEntity = environment.EntityHandles.GetOrCreate("symptom", symptomId, symptomId);
            var action = new ResolveHunterSymptomAction(settlement, hunter, symptomId, choice, symptomContent, outbox, hunterEntity, symptomEntity);
            ActionOutcome outcome = await environment.ExecuteAsync(action, outbox);
            if (outcome.IsSuccess) return action.Result;
            return string.IsNullOrWhiteSpace(action.Result.Reason) ? HunterSymptomCommandResult.Failed(outcome.Reason) : action.Result;
        }

        public bool CanEquipItem(int hunterId, ItemData item, out string reason)
        {
            HunterInstance hunter = settlement.GetHunter(hunterId);
            if (hunter == null)
            {
                reason = "猎人不属于当前营地。";
                return false;
            }
            if (!equipmentContent.Contains(item))
            {
                reason = "装备内容尚未配置。";
                return false;
            }
            if (settlement.GetStoredEquipment(item) <= 0)
            {
                reason = "装备仓库中已没有该物品。";
                return false;
            }
            return PlayableEquipmentRules.CanEquip(hunter, item, out reason);
        }

        public async UniTask<SettlementEquipmentCommandResult> EquipItemAsync(int hunterId, ItemData item)
        {
            if (!IsActive) return SettlementEquipmentCommandResult.Failed("当前不在营地阶段。");
            HunterInstance hunter = settlement.GetHunter(hunterId);
            if (hunter == null) return SettlementEquipmentCommandResult.Failed("猎人不属于当前营地。");

            var outbox = new ActionEventOutbox();
            ReactorEntityHandle hunterEntity = environment.EntityHandles.GetOrCreate("hunter", hunter.InstanceId.ToString(), hunter.Name);
            ReactorEntityHandle itemEntity = environment.EntityHandles.GetOrCreate("settlement-item", item != null ? item.ContentId : "unknown", item != null ? item.itemName : "未知装备");
            var action = new EquipHunterItemAction(settlement, hunter, item, equipmentContent, outbox, itemEntity, hunterEntity);
            ActionOutcome outcome = await environment.ExecuteAsync(action, outbox);
            if (outcome.IsSuccess) return action.Result;
            return string.IsNullOrWhiteSpace(action.Result.Reason) ? SettlementEquipmentCommandResult.Failed(outcome.Reason) : action.Result;
        }

        public async UniTask<SettlementEquipmentCommandResult> UnequipItemAsync(int hunterId, int equipmentInstanceId)
        {
            if (!IsActive) return SettlementEquipmentCommandResult.Failed("当前不在营地阶段。");
            HunterInstance hunter = settlement.GetHunter(hunterId);
            if (hunter == null) return SettlementEquipmentCommandResult.Failed("猎人不属于当前营地。");

            var outbox = new ActionEventOutbox();
            ReactorEntityHandle hunterEntity = environment.EntityHandles.GetOrCreate("hunter", hunter.InstanceId.ToString(), hunter.Name);
            ReactorEntityHandle storageEntity = environment.EntityHandles.GetOrCreate("settlement-equipment-storage", "active", "装备仓库");
            var action = new UnequipHunterItemAction(settlement, hunter, equipmentInstanceId, outbox, hunterEntity, storageEntity);
            ActionOutcome outcome = await environment.ExecuteAsync(action, outbox);
            if (outcome.IsSuccess) return action.Result;
            return string.IsNullOrWhiteSpace(action.Result.Reason) ? SettlementEquipmentCommandResult.Failed(outcome.Reason) : action.Result;
        }

        public bool CanCraft(CraftRecipe recipe, out string reason)
        {
            if (workshopSystem == null)
            {
                reason = "营地工坊尚未配置。";
                return false;
            }
            if (!workshopSystem.AllRecipes.Contains(recipe))
            {
                reason = "配方不属于当前营地。";
                return false;
            }
            return workshopSystem.CanCraft(recipe, out reason);
        }

        public async UniTask<SettlementCraftCommandResult> CraftAsync(CraftRecipe recipe)
        {
            if (!IsActive) return SettlementCraftCommandResult.Failed("当前不在营地阶段。");
            if (workshopSystem == null) return SettlementCraftCommandResult.Failed("营地工坊尚未配置。");

            var outbox = new ActionEventOutbox();
            ReactorEntityHandle workshopEntity = environment.EntityHandles.GetOrCreate("settlement-workshop", string.IsNullOrWhiteSpace(recipe?.requiredWorkshopId) ? "shared" : recipe.requiredWorkshopId, string.IsNullOrWhiteSpace(recipe?.requiredWorkshopId) ? "共享工坊" : recipe.requiredWorkshopId);
            ReactorEntityHandle recipeEntity = environment.EntityHandles.GetOrCreate("craft-recipe", recipe != null ? recipe.ContentId : "unknown", recipe != null ? recipe.recipeName : "未知配方");
            var action = new CraftSettlementRecipeAction(settlement, workshopSystem, recipe, outbox, workshopEntity, recipeEntity);
            ActionOutcome outcome = await environment.ExecuteAsync(action, outbox);
            if (outcome.IsSuccess) return action.Result;
            return string.IsNullOrWhiteSpace(action.Result.Reason) ? SettlementCraftCommandResult.Failed(outcome.Reason) : action.Result;
        }

        public bool CanUnlockInvention(InventionData invention, out string reason)
        {
            if (inventionSystem == null)
            {
                reason = "营地发明系统尚未配置。";
                return false;
            }
            if (invention == null || !inventionSystem.AllInventions.Contains(invention))
            {
                reason = "发明不属于当前营地。";
                return false;
            }
            return inventionSystem.CanUnlock(invention, out reason);
        }

        public async UniTask<SettlementInventionCommandResult> UnlockInventionAsync(InventionData invention)
        {
            if (!IsActive) return SettlementInventionCommandResult.Failed("当前不在营地阶段。");
            if (inventionSystem == null) return SettlementInventionCommandResult.Failed("营地发明系统尚未配置。");

            var outbox = new ActionEventOutbox();
            ReactorEntityHandle settlementEntity = environment.EntityHandles.GetOrCreate("settlement", "active", "营地");
            ReactorEntityHandle inventionEntity = environment.EntityHandles.GetOrCreate("settlement-invention", invention != null ? invention.ContentId : "unknown", invention != null ? invention.inventionName : "未知发明");
            var action = new UnlockSettlementInventionAction(settlement, inventionSystem, invention, outbox, settlementEntity, inventionEntity, hunter => environment.EntityHandles.GetOrCreate("hunter", hunter.InstanceId.ToString(), hunter.Name));
            ActionOutcome outcome = await environment.ExecuteAsync(action, outbox);
            if (outcome.IsSuccess) return action.Result;
            return string.IsNullOrWhiteSpace(action.Result.Reason) ? SettlementInventionCommandResult.Failed(outcome.Reason) : action.Result;
        }

        public bool CanActivateInventionEffect(InventionData invention, InventionActiveEffect effect, out string reason)
        {
            EventData gameEvent = effect != null ? resolveEvent?.Invoke(effect.eventId) : null;
            if (inventionSystem == null || invention == null || !inventionSystem.AllInventions.Contains(invention))
            {
                reason = "发明不属于当前营地。";
                return false;
            }
            if (effect == null || invention.activeEffects == null || !invention.activeEffects.Contains(effect))
            {
                reason = "主动效果不属于该发明。";
                return false;
            }
            bool eventAvailable = gameEvent != null && gameEvent.category == EventCategory.Triggered && string.Equals(gameEvent.ContentId, effect.eventId, StringComparison.Ordinal);
            return InventionActiveEffectRules.CanActivate(inventionSystem.IsUnlocked(invention), settlement.CurrentYear, effect.effectId, effect.eventId, effect.maxUsesPerYear, settlement.InventionActiveEffectUses, eventAvailable, out reason);
        }

        public async UniTask<SettlementInventionActiveEffectCommandResult> ActivateInventionEffectAsync(InventionData invention, InventionActiveEffect effect)
        {
            if (!IsActive) return SettlementInventionActiveEffectCommandResult.Failed("当前不在营地阶段。");
            if (eventSystem == null || inventionSystem == null) return SettlementInventionActiveEffectCommandResult.Failed("营地主动效果系统尚未配置。");
            EventData gameEvent = effect != null ? resolveEvent?.Invoke(effect.eventId) : null;

            var outbox = new ActionEventOutbox();
            ReactorEntityHandle settlementEntity = environment.EntityHandles.GetOrCreate("settlement", "active", "营地");
            ReactorEntityHandle inventionEntity = environment.EntityHandles.GetOrCreate("settlement-invention", invention != null ? invention.ContentId : "unknown", invention != null ? invention.inventionName : "未知发明");
            IReactorEntity ResolveEventEntity(EventData candidate) => environment.EntityHandles.GetOrCreate("settlement-event", candidate != null ? candidate.ContentId : "unknown", candidate != null ? candidate.eventName : "营地事件");
            var action = new ActivateSettlementInventionEffectAction(settlement, inventionSystem, invention, effect, gameEvent, eventSystem, EventInput, SessionId, outbox, settlementEntity, inventionEntity, ResolveEventEntity, randomInteractionPresenter);
            ActionOutcome outcome = await environment.ExecuteAsync(action, outbox);
            if (outcome.IsSuccess) return action.Result;
            return string.IsNullOrWhiteSpace(action.Result.Reason) ? SettlementInventionActiveEffectCommandResult.Failed(outcome.Reason) : action.Result;
        }

        public async UniTask<SettlementWorkshopConstructionResult> BuildWorkshopAsync(PlayableWorkshopDefinition definition)
        {
            if (!IsActive) return SettlementWorkshopConstructionResult.Failed("当前不在营地阶段。");
            if (workshopCatalog == null) return SettlementWorkshopConstructionResult.Failed("营地工坊蓝图尚未配置。");

            var outbox = new ActionEventOutbox();
            ReactorEntityHandle settlementEntity = environment.EntityHandles.GetOrCreate("settlement", "active", "营地");
            string workshopId = string.IsNullOrWhiteSpace(definition?.WorkshopId) ? "unknown" : definition.WorkshopId;
            ReactorEntityHandle workshopEntity = environment.EntityHandles.GetOrCreate("settlement-workshop", workshopId, definition != null ? definition.DisplayName : "未知工坊");
            var action = new BuildSettlementWorkshopAction(settlement, workshopConstructionService, workshopCatalog.Workshops, definition, outbox, settlementEntity, workshopEntity);
            ActionOutcome outcome = await environment.ExecuteAsync(action, outbox);
            if (outcome.IsSuccess) return action.Result;
            return string.IsNullOrWhiteSpace(action.Result.Reason) ? SettlementWorkshopConstructionResult.Failed(outcome.Reason) : action.Result;
        }

        public async UniTask<WeaponTrainingCommandResult> TrainWeaponAsync(int hunterId, string masteryId)
        {
            if (!IsActive) return WeaponTrainingCommandResult.Failed("当前不在营地阶段");
            HunterInstance hunter = settlement.GetHunter(hunterId);
            if (hunter == null || !weaponTrainingContent.TryGetFamily(masteryId, out WeaponMasteryFamilyDefinition family)) return WeaponTrainingCommandResult.Failed("训练内容尚未配置");

            var outbox = new ActionEventOutbox();
            ReactorEntityHandle settlementEntity = environment.EntityHandles.GetOrCreate("settlement", "active", "营地");
            ReactorEntityHandle hunterEntity = environment.EntityHandles.GetOrCreate("hunter", hunter.InstanceId.ToString(), hunter.Name);
            var action = new TrainWeaponAction(settlement, hunter, family, weaponTrainingContent.RequiredInventionId, weaponTrainingContent.CostResourceId, weaponTrainingContent.ResourceCost, weaponTrainingContent.Experience, outbox, settlementEntity, hunterEntity);
            ActionOutcome outcome = await environment.ExecuteAsync(action, outbox);
            if (outcome.IsSuccess) return action.Result;
            return string.IsNullOrWhiteSpace(action.Result.Reason) ? WeaponTrainingCommandResult.Failed(outcome.Reason) : action.Result;
        }

        public async UniTask<SettlementEventCommandResult> ResolveEventsAsync(System.Collections.Generic.IReadOnlyList<EventData> events, string restoredChainId = null, System.Collections.Generic.IReadOnlyList<SettlementEventChainOccurrence> restoredOccurrences = null)
        {
            if (events == null) return SettlementEventCommandResult.Success(0, false);
            var works = new System.Collections.Generic.List<SettlementEventWork>(events.Count);
            bool hasOccurrences = restoredOccurrences != null && restoredOccurrences.Count == events.Count;
            for (int index = 0; index < events.Count; index++)
                works.Add(new SettlementEventWork(events[index], null, hasOccurrences ? restoredOccurrences[index] : null));
            return await ResolveEventsAsync(works, restoredChainId);
        }

        public async UniTask<SettlementEventCommandResult> ResolveEventsAsync(System.Collections.Generic.IReadOnlyList<SettlementEventWork> works, string restoredChainId = null)
        {
            if (!IsActive) return SettlementEventCommandResult.Failed("当前不在营地阶段", 0);
            if (eventSystem == null) return SettlementEventCommandResult.Failed("营地事件系统尚未配置", 0);
            if (works == null || works.Count == 0) return SettlementEventCommandResult.Success(0, false);

            var outbox = new ActionEventOutbox();
            ReactorEntityHandle settlementEntity = environment.EntityHandles.GetOrCreate("settlement", "active", "营地");
            ReactorEntityHandle chainEntity = environment.EntityHandles.GetOrCreate("settlement-event-chain", SessionId.ToString("N"), "营地事件链");
            IReactorEntity ResolveEventEntity(EventData gameEvent) => environment.EntityHandles.GetOrCreate("settlement-event", gameEvent != null ? gameEvent.ContentId : "unknown", gameEvent != null ? gameEvent.eventName : "营地事件");
            var action = new ResolveSettlementEventChainAction(eventSystem, EventInput, works, SessionId, outbox, settlementEntity, chainEntity, ResolveEventEntity, randomInteractionPresenter, restoredChainId, settlementCommand: settlementEventCommand);
            ActionOutcome outcome = await environment.ExecuteAsync(action, outbox);
            if (outcome.IsSuccess) return action.Result;
            return string.IsNullOrWhiteSpace(action.Result.Reason) ? SettlementEventCommandResult.Failed(outcome.Reason, action.Result.ResolvedCount) : action.Result;
        }

        private static bool IsWounded(HunterInstance hunter)
        {
            return HunterRecoveryRules.CanRecover(hunter, HunterBodyPart.Head, out _)
                || HunterRecoveryRules.CanRecover(hunter, HunterBodyPart.Torso, out _)
                || HunterRecoveryRules.CanRecover(hunter, HunterBodyPart.Arms, out _)
                || HunterRecoveryRules.CanRecover(hunter, HunterBodyPart.Legs, out _);
        }

        public void Dispose() => environment.Dispose();
    }
}
