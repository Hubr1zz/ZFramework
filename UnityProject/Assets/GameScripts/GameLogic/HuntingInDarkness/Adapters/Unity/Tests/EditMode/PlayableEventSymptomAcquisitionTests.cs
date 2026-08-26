using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Core;
using HuntingInDarkness.ActionFlow.Events;
using HuntingInDarkness.ActionFlow.Settlement;
using HuntingInDarkness.ContentTables;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableEventSymptomAcquisitionTests
    {
        private const string CatalogPath = "Assets/GameScripts/GameLogic/HuntingInDarkness/Content/Settlement/Symptoms/PlayableSymptomCatalog.asset";
        private PlayableSymptomCatalog catalog;

        [SetUp]
        public void SetUp()
        {
            catalog = AssetDatabase.LoadAssetAtPath<PlayableSymptomCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.IsConfigured, Is.True);
            PlayableSymptomRuntime.Configure(catalog);
        }

        [TearDown]
        public void TearDown() => PlayableSymptomRuntime.Configure(null);

        [Test]
        public async Task StableId_CommitsStateOnceAndPublishesAcquisitionBeforeTransaction()
        {
            SettlementInstance settlement = CreateSettlement();
            HunterInstance hunter = settlement.Hunters[0];
            EventData gameEvent = CreateEvent("event_symptom_exact", "symptom_cowardice", "symptom_cowardice");
            var received = new List<string>();
            Action<HunterSymptomAcquiredEvent> symptomHandler = evt => received.Add($"symptom:{evt.SourceEventId}:{evt.SymptomId}:{evt.HunterId}");
            Action<SettlementTransactionCommittedEvent> transactionHandler = evt =>
            {
                if (evt.Kind == SettlementTransactionKind.EventResolution) received.Add("transaction:event");
            };
            EventBus.Subscribe(symptomHandler);
            EventBus.Subscribe(transactionHandler);
            try
            {
                using var session = CreateSession(settlement);

                SettlementEventCommandResult result = await session.ResolveEventsAsync(new[] { gameEvent });

                Assert.That(result.Succeeded, Is.True, result.Reason);
                Assert.That(result.EffectResults.AppliedCount, Is.EqualTo(2));
                Assert.That(result.EffectResults.Effects[0].ResolvedTargetId, Is.EqualTo("symptom_cowardice"));
                Assert.That(result.EffectResults.Effects[0].StateChanged, Is.True);
                Assert.That(result.EffectResults.Effects[1].StateChanged, Is.False);
                Assert.That(HunterSymptomRules.Find(hunter, "symptom_cowardice"), Is.Not.Null);
                Assert.That(hunter.SymptomStates, Has.Count.EqualTo(1));
                Assert.That(hunter.Stats.strength, Is.EqualTo(1));
                Assert.That(hunter.Ailments, Is.EqualTo(new[] { "胆怯" }));
                Assert.That(received, Is.EqualTo(new[] { $"symptom:event_symptom_exact:symptom_cowardice:{hunter.InstanceId}", "transaction:event" }));
            }
            finally
            {
                EventBus.Unsubscribe(symptomHandler);
                EventBus.Unsubscribe(transactionHandler);
                UnityEngine.Object.DestroyImmediate(gameEvent);
            }
        }

        [Test]
        public async Task UnknownStableId_FailsOnlyThatEffectWithoutMutationOrFact()
        {
            SettlementInstance settlement = CreateSettlement();
            HunterInstance hunter = settlement.Hunters[0];
            EventData gameEvent = CreateEvent("event_symptom_missing", "symptom_missing");
            int factCount = 0;
            Action<HunterSymptomAcquiredEvent> handler = _ => factCount++;
            EventBus.Subscribe(handler);
            try
            {
                using var session = CreateSession(settlement);

                SettlementEventCommandResult result = await session.ResolveEventsAsync(new[] { gameEvent });

                Assert.That(result.Succeeded, Is.True, result.Reason);
                Assert.That(result.FailedEffectCount, Is.EqualTo(1));
                Assert.That(result.EffectResults.Effects[0].Reason, Does.Contain("未注册症状"));
                Assert.That(hunter.SymptomStates, Is.Empty);
                Assert.That(hunter.Ailments, Is.Empty);
                Assert.That(hunter.Stats.strength, Is.EqualTo(2));
                Assert.That(factCount, Is.Zero);
            }
            finally
            {
                EventBus.Unsubscribe(handler);
                UnityEngine.Object.DestroyImmediate(gameEvent);
            }
        }

        [Test]
        public async Task DisplayNameReference_IsRejectedOutsideLegacyMigration()
        {
            SettlementInstance settlement = CreateSettlement();
            HunterInstance hunter = settlement.Hunters[0];
            EventData gameEvent = CreateEvent("event_symptom_display_name", "胆怯");
            try
            {
                using var session = CreateSession(settlement);

                SettlementEventCommandResult result = await session.ResolveEventsAsync(new[] { gameEvent });

                Assert.That(result.FailedEffectCount, Is.EqualTo(1));
                Assert.That(hunter.SymptomStates, Is.Empty);
                Assert.That(hunter.Ailments, Is.Empty);
                Assert.That(hunter.Stats.strength, Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameEvent);
            }
        }

        [Test]
        public void SynchronizeHunter_MigratesStableIdAndDisplayNameWithoutDroppingUnknownTokens()
        {
            HunterInstance hunter = CreateSettlement().Hunters[0];
            hunter.Ailments.Add("symptom_cowardice");
            hunter.Ailments.Add("胆怯");
            hunter.Ailments.Add("legacy_unknown");

            PlayableSymptomRuntime.SynchronizeHunter(hunter);
            PlayableSymptomRuntime.SynchronizeHunter(hunter);

            Assert.That(hunter.SymptomStates, Has.Count.EqualTo(1));
            Assert.That(hunter.Stats.strength, Is.EqualTo(1));
            Assert.That(hunter.Ailments, Is.EqualTo(new[] { "legacy_unknown", "胆怯" }));
        }

        [Test]
        public void ProductionAndTableAddAilmentReferences_RequireExactCatalogId()
        {
            int configuredCount = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:EventData", new[] { "Assets/GameScripts/GameLogic/HuntingInDarkness/Content" }))
            {
                EventData gameEvent = AssetDatabase.LoadAssetAtPath<EventData>(AssetDatabase.GUIDToAssetPath(guid));
                foreach (EventEffect effect in GetEffects(gameEvent))
                {
                    if (effect?.effectType != EventEffectType.AddAilment) continue;
                    configuredCount++;
                    Assert.That(catalog.TryGetById(effect.targetName, out _), Is.True, $"{gameEvent.name} 必须使用可解析的稳定症状 ID：{effect.targetName}");
                }
            }

            MethodInfo validateEffects = typeof(PlayableEventTableRuntime).GetMethod("ValidateEffects", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(validateEffects, Is.Not.Null);
            var exactId = new List<EventEffectTableRecord> { new() { effectType = nameof(EventEffectType.AddAilment), targetName = "symptom_cowardice" } };
            var displayName = new List<EventEffectTableRecord> { new() { effectType = nameof(EventEffectType.AddAilment), targetName = "胆怯" } };
            Assert.That(validateEffects.Invoke(null, new object[] { exactId, true, catalog, PlayableBloodlineRuntime.Content, false, false }), Is.True);
            Assert.That(validateEffects.Invoke(null, new object[] { displayName, true, catalog, PlayableBloodlineRuntime.Content, false, false }), Is.False);
            Assert.That(configuredCount, Is.GreaterThan(0));
        }

        [Test]
        public void ProductionEventTables_ExerciseEveryExpandedSymptomByStableId()
        {
            var referenced = new HashSet<string>(StringComparer.Ordinal);
            CollectTableAilments(new JsonEventTableSource("HuntingInDarkness/Tables/events").Load(), referenced);
            CollectTableAilments(new JsonEventTableSource("HuntingInDarkness/Tables/hunt-events").Load(), referenced);
            string[] expected =
            {
                "symptom_blood_fever",
                "symptom_claustrophobia",
                "symptom_night_terrors",
                "symptom_shadow_fixation",
                "symptom_stone_lung",
                "symptom_trembling_hands",
                "symptom_whisper_sickness"
            };

            foreach (string symptomId in referenced)
                Assert.That(catalog.TryGetById(symptomId, out _), Is.True, $"事件必须引用已注册的稳定症状 ID：{symptomId}");
            foreach (string symptomId in expected)
                Assert.That(referenced, Does.Contain(symptomId), $"基础事件池尚未覆盖症状：{symptomId}");
        }

        [Test]
        public void TableRecoverableWound_RequiresSelectedHunterPositiveDamageAndKnownPart()
        {
            MethodInfo validateEffects = typeof(PlayableEventTableRuntime).GetMethod("ValidateEffects", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(validateEffects, Is.Not.Null);
            var valid = new List<EventEffectTableRecord> { new() { effectType = nameof(EventEffectType.AddRecoverableWound), targetName = "selected", bodyPart = "arms", value = 1 } };
            var immediate = new List<EventEffectTableRecord> { new() { effectType = nameof(EventEffectType.AddRecoverableWound), targetName = "selected", bodyPart = "arms", value = 1 } };
            var invalidPart = new List<EventEffectTableRecord> { new() { effectType = nameof(EventEffectType.AddRecoverableWound), targetName = "selected", bodyPart = "wings", value = 1 } };
            var invalidDamage = new List<EventEffectTableRecord> { new() { effectType = nameof(EventEffectType.AddRecoverableWound), targetName = "selected", bodyPart = "arms", value = 0 } };

            Assert.That(validateEffects.Invoke(null, new object[] { valid, true, catalog, PlayableBloodlineRuntime.Content, false, false }), Is.True);
            Assert.That(validateEffects.Invoke(null, new object[] { immediate, false, catalog, PlayableBloodlineRuntime.Content, false, false }), Is.False);
            Assert.That(validateEffects.Invoke(null, new object[] { invalidPart, true, catalog, PlayableBloodlineRuntime.Content, false, false }), Is.False);
            Assert.That(validateEffects.Invoke(null, new object[] { invalidDamage, true, catalog, PlayableBloodlineRuntime.Content, false, false }), Is.False);
        }

        [Test]
        public void HuntWorldEffect_RequiresHuntCategoryAndEmptyParameters()
        {
            MethodInfo validateEffects = typeof(PlayableEventTableRuntime).GetMethod("ValidateEffects", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(validateEffects, Is.Not.Null);
            object[] Arguments(EventEffectTableRecord effect, bool allowHuntWorldEffects) => new object[] { new List<EventEffectTableRecord> { effect }, true, catalog, PlayableBloodlineRuntime.Content, allowHuntWorldEffects, false };

            Assert.That(validateEffects.Invoke(null, Arguments(new EventEffectTableRecord { effectType = nameof(EventEffectType.ExhaustCurrentHuntTileResources), value = 0 }, true)), Is.True);
            Assert.That(validateEffects.Invoke(null, Arguments(new EventEffectTableRecord { effectType = nameof(EventEffectType.ExhaustCurrentHuntTileResources), value = 0 }, false)), Is.False);
            Assert.That(validateEffects.Invoke(null, Arguments(new EventEffectTableRecord { effectType = nameof(EventEffectType.ExhaustCurrentHuntTileResources), targetName = "x" }, true)), Is.False);
            Assert.That(validateEffects.Invoke(null, Arguments(new EventEffectTableRecord { effectType = nameof(EventEffectType.ExhaustCurrentHuntTileResources), bodyPart = "x" }, true)), Is.False);
            Assert.That(validateEffects.Invoke(null, Arguments(new EventEffectTableRecord { effectType = nameof(EventEffectType.ExhaustCurrentHuntTileResources), value = 1 }, true)), Is.False);
        }

        [Test]
        public void EventEffectType_PreservesSerializedValuesWhenEffectsAreAppended()
        {
            Assert.That((int)EventEffectType.AddAilment, Is.EqualTo(9));
            Assert.That((int)EventEffectType.KillHunter, Is.EqualTo(10));
            Assert.That((int)EventEffectType.ActivateBloodline, Is.EqualTo(15));
            Assert.That((int)EventEffectType.AddRecoverableWound, Is.EqualTo(16));
            Assert.That((int)EventEffectType.ExhaustCurrentHuntTileResources, Is.EqualTo(17));
            Assert.That((int)EventEffectType.CreateHuntNoiseLease, Is.EqualTo(18));
        }

        [Test]
        public void HuntNoiseLeaseEffect_RequiresSettlementCategoryAndValidParameters()
        {
            MethodInfo validateEffects = typeof(PlayableEventTableRuntime).GetMethod("ValidateEffects", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(validateEffects, Is.Not.Null);
            object[] Arguments(EventEffectTableRecord effect, bool allowSettlementEventEffects) => new object[] { new List<EventEffectTableRecord> { effect }, true, catalog, PlayableBloodlineRuntime.Content, false, allowSettlementEventEffects };

            Assert.That(validateEffects.Invoke(null, Arguments(new EventEffectTableRecord { effectType = nameof(EventEffectType.CreateHuntNoiseLease), targetName = "stone_vigil_risk", value = 2 }, true)), Is.True);
            Assert.That(validateEffects.Invoke(null, Arguments(new EventEffectTableRecord { effectType = nameof(EventEffectType.CreateHuntNoiseLease), targetName = "stone_vigil_risk", value = 2 }, false)), Is.False);
            Assert.That(validateEffects.Invoke(null, Arguments(new EventEffectTableRecord { effectType = nameof(EventEffectType.CreateHuntNoiseLease), targetName = "stone_vigil_risk", bodyPart = "arms", value = 2 }, true)), Is.False);
            Assert.That(validateEffects.Invoke(null, Arguments(new EventEffectTableRecord { effectType = nameof(EventEffectType.CreateHuntNoiseLease), targetName = "stone_vigil_risk", value = 11 }, true)), Is.False);
            Assert.That(validateEffects.Invoke(null, Arguments(new EventEffectTableRecord { effectType = nameof(EventEffectType.CreateHuntNoiseLease), targetName = "stone_vigil_risk", value = 2 }, false)), Is.False);
        }

        [Test]
        public async Task RecoverableWound_CommitsThroughSettlementRootAndPublishesBeforeTransaction()
        {
            SettlementInstance settlement = CreateSettlement();
            HunterInstance hunter = settlement.Hunters[0];
            EventData gameEvent = ScriptableObject.CreateInstance<EventData>();
            gameEvent.name = "event_recoverable_wound_asset";
            gameEvent.ConfigureContentId("event_recoverable_wound");
            gameEvent.eventType = GameEventType.Choice;
            gameEvent.options.Add(new EventOption { optionText = "撑住横梁", alwaysAvailable = true, successText = "横梁擦伤了手臂。", successEffects = new List<EventEffect> { new() { effectType = EventEffectType.AddRecoverableWound, targetName = "selected", bodyPart = "arms", value = 2 } } });
            var received = new List<string>();
            Action<HunterWoundedEvent> woundHandler = evt => received.Add($"wound:{evt.BodyPartId}:{evt.PreviousHealth}>{evt.CurrentHealth}");
            Action<SettlementTransactionCommittedEvent> transactionHandler = evt =>
            {
                if (evt.Kind == SettlementTransactionKind.EventResolution) received.Add("transaction:event");
            };
            EventBus.Subscribe(woundHandler);
            EventBus.Subscribe(transactionHandler);
            try
            {
                using var session = CreateSession(settlement);

                SettlementEventCommandResult result = await session.ResolveEventsAsync(new[] { gameEvent });

                Assert.That(result.Succeeded, Is.True, result.Reason);
                Assert.That(hunter.HP.arms, Is.EqualTo(1));
                Assert.That(result.EffectResults.Effects[0].ResolvedTargetId, Is.EqualTo("arms"));
                Assert.That(result.EffectResults.Effects[0].StateChanged, Is.True);
                Assert.That(received, Is.EqualTo(new[] { "wound:arms:3>1", "transaction:event" }));
            }
            finally
            {
                EventBus.Unsubscribe(woundHandler);
                EventBus.Unsubscribe(transactionHandler);
                UnityEngine.Object.DestroyImmediate(gameEvent);
            }
        }

        [Test]
        public void TableCache_ExplicitlyRebuildsWhenSymptomCatalogBecomesAvailable()
        {
            Type runtimeType = typeof(PlayableEventTableRuntime);
            FieldInfo recordsField = runtimeType.GetField("cachedRecords", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(recordsField, Is.Not.Null);
            try
            {
                PlayableEventTableRuntime.ClearCache();
                recordsField.SetValue(null, new List<EventTableRecord>
                {
                    new()
                    {
                        id = "table_symptom_cache",
                        eventName = "缓存症状事件",
                        eventType = nameof(GameEventType.Choice),
                        category = nameof(EventCategory.Hunt),
                        options = new List<EventOptionTableRecord>
                        {
                            new()
                            {
                                optionText = "接受痕迹",
                                checkType = nameof(CheckType.None),
                                successEffects = new List<EventEffectTableRecord> { new() { effectType = nameof(EventEffectType.AddAilment), targetName = "symptom_cowardice" } }
                            }
                        }
                    }
                });
                PlayableSymptomRuntime.Configure(null);
                LogAssert.Expect(LogType.Error, "[ContentTable] 事件 table_symptom_cache 含无效选项或效果。");
                Assert.That(PlayableEventTableRuntime.GetEvents(), Is.Empty);

                PlayableSymptomRuntime.Configure(catalog);
                IReadOnlyList<EventData> rebuilt = PlayableEventTableRuntime.Rebuild();

                Assert.That(rebuilt, Has.Count.EqualTo(1));
                Assert.That(rebuilt[0].ContentId, Is.EqualTo("table_symptom_cache"));
            }
            finally
            {
                PlayableEventTableRuntime.ClearCache();
                PlayableSymptomRuntime.Configure(catalog);
            }
        }

        [Test]
        public void Catalog_WithNonCanonicalStableId_IsNotConfigured()
        {
            PlayableSymptomCatalog invalidCatalog = ScriptableObject.CreateInstance<PlayableSymptomCatalog>();
            try
            {
                JsonUtility.FromJsonOverwrite("{\"symptoms\":[{\"id\":\" symptom_spaced \",\"displayName\":\"空白症状\"}]}", invalidCatalog);

                Assert.That(invalidCatalog.IsConfigured, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(invalidCatalog);
            }
        }

        [Test]
        public void Catalog_WithMissingDefinitionList_FailsSafely()
        {
            PlayableSymptomCatalog invalidCatalog = ScriptableObject.CreateInstance<PlayableSymptomCatalog>();
            FieldInfo symptomsField = typeof(PlayableSymptomCatalog).GetField("symptoms", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(symptomsField, Is.Not.Null);
            try
            {
                symptomsField.SetValue(invalidCatalog, null);

                Assert.That(invalidCatalog.IsConfigured, Is.False);
                Assert.That(invalidCatalog.GetDefinitions(), Is.Empty);
                Assert.That(invalidCatalog.TryGetById("symptom_cowardice", out _), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(invalidCatalog);
            }
        }

        private static SettlementInstance CreateSettlement()
        {
            var settlement = new SettlementInstance { CurrentYear = 1 };
            var hunter = new HunterInstance(null, 17) { Name = "雾行者" };
            hunter.Stats.strength = 2;
            settlement.Hunters.Add(hunter);
            return settlement;
        }

        private static void CollectTableAilments(IReadOnlyList<EventTableRecord> records, ISet<string> results)
        {
            foreach (EventTableRecord record in records ?? Array.Empty<EventTableRecord>())
                foreach (EventOptionTableRecord option in record?.options ?? new List<EventOptionTableRecord>())
                {
                    CollectTableAilments(option?.successEffects, results);
                    CollectTableAilments(option?.failEffects, results);
                }
        }

        private static void CollectTableAilments(IReadOnlyList<EventEffectTableRecord> effects, ISet<string> results)
        {
            foreach (EventEffectTableRecord effect in effects ?? Array.Empty<EventEffectTableRecord>())
                if (effect != null && string.Equals(effect.effectType, nameof(EventEffectType.AddAilment), StringComparison.OrdinalIgnoreCase))
                    results.Add(effect.targetName);
        }

        private static PlayableSettlementActionSession CreateSession(SettlementInstance settlement) => new(settlement, EmptyWeaponTrainingContent.Instance, new EventSystem(settlement, FirstRandom.Instance));

        private static EventData CreateEvent(string eventId, params string[] symptomIds)
        {
            EventData gameEvent = ScriptableObject.CreateInstance<EventData>();
            gameEvent.name = $"{eventId}_asset";
            gameEvent.ConfigureContentId(eventId);
            gameEvent.eventType = GameEventType.Choice;
            var effects = new List<EventEffect>();
            foreach (string symptomId in symptomIds)
                effects.Add(new EventEffect { effectType = EventEffectType.AddAilment, targetName = symptomId, description = "获得症状" });
            gameEvent.options.Add(new EventOption { optionText = "面对脚步声", alwaysAvailable = true, successText = "黑暗留下了痕迹。", successEffects = effects });
            return gameEvent;
        }

        private static IEnumerable<EventEffect> GetEffects(EventData gameEvent)
        {
            if (gameEvent?.immediateEffects != null)
                foreach (EventEffect effect in gameEvent.immediateEffects)
                    yield return effect;
            if (gameEvent?.options == null) yield break;
            foreach (EventOption option in gameEvent.options)
            {
                if (option?.successEffects != null)
                    foreach (EventEffect effect in option.successEffects)
                        yield return effect;
                if (option?.failEffects != null)
                    foreach (EventEffect effect in option.failEffects)
                        yield return effect;
            }
        }

        private sealed class EmptyWeaponTrainingContent : IWeaponTrainingContent
        {
            public static EmptyWeaponTrainingContent Instance { get; } = new();
            public string RequiredInventionId => string.Empty;
            public string CostResourceId => string.Empty;
            public int ResourceCost => 0;
            public int Experience => 1;
            public bool TryGetFamily(string masteryId, out WeaponMasteryFamilyDefinition family)
            {
                family = null;
                return false;
            }
        }

        private sealed class FirstRandom : IRandomSource
        {
            public static FirstRandom Instance { get; } = new();
            public int Next(int minInclusive, int maxExclusive) => minInclusive;
            public double NextDouble() => 0d;
        }
    }
}
