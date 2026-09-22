using System.Collections.Generic;
using System.Reflection;
using Core;
using HuntingInDarkness.Bootstrap;
using HuntingInDarkness.ContentTables;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Foundation;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableSettlementProjectionTransactionTests
    {
        private const string SettingsPath = "Assets/AssetRaw/Configs/HuntingInDarkness/PlayableBootstrapSettings.asset";
        private static readonly MethodInfo resetAssemblerMethod = typeof(PlayableCampaignContentAssembler).GetMethod("ResetRuntimeState", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo resetSettlementContentRuntimeMethod = typeof(PlayableSettlementContentRuntime).GetMethod("ResetRuntimeState", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo prepareCandidateMethod = typeof(SettlementManager).GetMethod("TryPrepareCandidate", BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(SettlementInstance), typeof(SettlementManager).MakeByRefType(), typeof(string).MakeByRefType() }, null);
        private static readonly MethodInfo consumeCandidateMethod = typeof(SettlementManager).GetMethod("TryConsumePreparedCandidate", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly PropertyInfo randomSourceProperty = typeof(SettlementManager).GetProperty("RandomSource", BindingFlags.Instance | BindingFlags.NonPublic);

        [SetUp]
        public void SetUp()
        {
            PlayableBootstrapSettings settings = AssetDatabase.LoadAssetAtPath<PlayableBootstrapSettings>(SettingsPath);
            Assert.That(PlayableCampaignContentAssembler.TryBuild(PlayableContentSourceTestAssets.LoadBundle(settings), out PlayableCampaignContentCandidate candidate, out PlayableContentDiagnosticReport buildReport), Is.True, buildReport.ToString());
            Assert.That(PlayableCampaignContentAssembler.Install(candidate, out PlayableContentDiagnosticReport installReport), Is.True, installReport.ToString());
        }

        [TearDown]
        public void TearDown()
        {
            resetAssemblerMethod.Invoke(null, null);
            PlayableHuntContentRuntime.Configure(null);
            resetSettlementContentRuntimeMethod.Invoke(null, null);
            PlayableEventTableRuntime.ClearCache();
            PlayableBloodlineRuntime.Configure(null);
        }

        [Test]
        public void TryInjectData_FailedProjectionPreservesCurrentRuntimeGraph()
        {
            var manager = new SettlementManager(17);
            manager.EnsureStartingConditions();
            var control = new SettlementManager(17);
            control.EnsureStartingConditions();
            SettlementInstance previousData = manager.Data;
            object previousTimeline = manager.Timeline;
            object previousEvents = manager.Events;
            object previousInventions = manager.Inventions;
            object previousWorkshop = manager.Workshop;
            object previousHunterManagement = manager.HunterMgmt;
            HunterInstance previousHunter = previousData.Hunters[0];
            SettlementInstance rejected = CreateLoadedSettlement(501);
            rejected.CampaignPacingSchemaVersion = 0;
            rejected.HuntsPerYear = 2;
            rejected.HuntsCompletedThisYear = 1;
            rejected.SettlementModifierSchemaVersion = PlayableSettlementModifierRuntime.CurrentSchemaVersion;
            rejected.ActiveModifiers = new List<SettlementModifierState>
            {
                CreateModifier("duplicate"),
                CreateModifier("duplicate")
            };

            LogAssert.Expect(LogType.Error, "[SettlementManager] 存档包含重复或空白的持续修正。");
            bool injected = manager.TryInjectData(rejected, out string reason);

            Assert.That(injected, Is.False);
            Assert.That(reason, Does.Contain("持续修正"));
            Assert.That(manager.Data, Is.SameAs(previousData));
            Assert.That(manager.Timeline, Is.SameAs(previousTimeline));
            Assert.That(manager.Events, Is.SameAs(previousEvents));
            Assert.That(manager.Inventions, Is.SameAs(previousInventions));
            Assert.That(manager.Workshop, Is.SameAs(previousWorkshop));
            Assert.That(manager.HunterMgmt, Is.SameAs(previousHunterManagement));
            Assert.That(manager.Data.Hunters[0], Is.SameAs(previousHunter));
            Assert.That(GetRandom(manager).Next(0, int.MaxValue), Is.EqualTo(GetRandom(control).Next(0, int.MaxValue)), "失败候选不得推进旧 Manager 的随机序列。");
        }

        [Test]
        public void TryInjectData_CurrentDataAliasIsRejectedWithoutMutation()
        {
            var manager = new SettlementManager(19);
            manager.EnsureStartingConditions();
            SettlementInstance current = manager.Data;
            HunterInstance hunter = current.Hunters[0];
            current.DeparturePreparationToken = "persisted-token";
            current.RuntimeDeparturePreparationToken = "runtime-token";
            Assert.That(PlayableSettlementItemRegistry.TryGet("salt_ward", out ItemData ward), Is.True);
            Assert.That(PlayableSettlementItemRegistry.TryGet("black_salt", out ItemData salt), Is.True);
            var equipment = new ItemInstance(ward);
            var collectible = new ItemInstance(salt);
            hunter.Equipment.Add(equipment);
            hunter.Collectibles.Add(collectible);

            bool injected = manager.TryInjectData(current, out string reason);

            Assert.That(injected, Is.False);
            Assert.That(reason, Does.Contain("当前权威营地数据"));
            Assert.That(manager.Data, Is.SameAs(current));
            Assert.That(current.DeparturePreparationToken, Is.EqualTo("persisted-token"));
            Assert.That(current.RuntimeDeparturePreparationToken, Is.EqualTo("runtime-token"));
            Assert.That(hunter.Equipment, Has.Count.EqualTo(1));
            Assert.That(hunter.Equipment[0], Is.SameAs(equipment));
            Assert.That(hunter.Collectibles, Has.Count.EqualTo(1));
            Assert.That(hunter.Collectibles[0], Is.SameAs(collectible));
        }

        [Test]
        public void TryInjectData_ValidProjectionCommitsOneCoherentBinding()
        {
            var manager = new SettlementManager(23);
            manager.EnsureStartingConditions();
            SettlementInstance previousData = manager.Data;
            object previousTimeline = manager.Timeline;
            var loaded = CreateLoadedSettlement(601);
            loaded.DeparturePreparationToken = "persisted-token";
            loaded.RuntimeDeparturePreparationToken = "runtime-token-must-not-survive-load";
            Assert.That(PlayableSettlementItemRegistry.TryGet("salt_ward", out ItemData ward), Is.True);
            Assert.That(PlayableSettlementItemRegistry.TryGet("black_salt", out ItemData salt), Is.True);
            loaded.Hunters[0].EquippedItemIds.Add(ward.ContentId);
            loaded.Hunters[0].Traits.Add("守望者");
            var previousEquipment = new ItemInstance(ward);
            loaded.Hunters[0].Equipment.Add(previousEquipment);
            loaded.Hunters[0].Collectibles.Add(new ItemInstance(salt));
            bool injected = manager.TryInjectData(loaded, out string reason);

            Assert.That(injected, Is.True, reason);
            Assert.That(manager.Data, Is.SameAs(loaded));
            Assert.That(manager.Data, Is.Not.SameAs(previousData));
            Assert.That(manager.Timeline, Is.Not.SameAs(previousTimeline));
            Assert.That(manager.Data.DeparturePreparationToken, Is.EqualTo("persisted-token"));
            Assert.That(manager.Data.RuntimeDeparturePreparationToken, Is.Empty);
            Assert.That(manager.Data.ItemIdentitySchemaVersion, Is.EqualTo(PlayableSettlementItemRegistry.CurrentIdentitySchemaVersion));
            Assert.That(manager.Data.TraitIdentitySchemaVersion, Is.EqualTo(PlayableTraitRegistry.CurrentIdentitySchemaVersion));
            Assert.That(manager.Data.InventionIdentitySchemaVersion, Is.EqualTo(PlayableSettlementInventionRegistry.CurrentIdentitySchemaVersion));
            Assert.That(manager.Data.TimelineEventIdentitySchemaVersion, Is.EqualTo(PlayableSettlementEventRegistry.CurrentIdentitySchemaVersion));
            Assert.That(manager.Data.CampaignPacingSchemaVersion, Is.EqualTo(SettlementInstance.CurrentCampaignPacingSchemaVersion));
            Assert.That(manager.Data.Hunters, Has.Count.EqualTo(1));
            Assert.That(manager.Data.Hunters[0].Traits, Is.EqualTo(new[] { "trait_watcher" }));
            Assert.That(manager.Data.Hunters[0].Equipment, Has.Count.EqualTo(1));
            Assert.That(manager.Data.Hunters[0].Equipment[0], Is.Not.SameAs(previousEquipment));
            Assert.That(manager.Data.Hunters[0].Equipment[0].Data, Is.SameAs(ward));
            Assert.That(manager.Data.Hunters[0].Collectibles, Is.Empty);
            AssertBindingField(manager.Timeline, "_settlement", manager.Data);
            AssertBindingField(manager.HunterMgmt, "_settlement", manager.Data);
            AssertBindingField(manager.Events, "_settlement", manager.Data);
            AssertBindingField(manager.Events, "delayedEventScheduler", manager.Timeline);
            AssertBindingField(manager.Events, "hunterDeathCommand", manager.HunterMgmt);
            AssertBindingField(manager.Inventions, "_settlement", manager.Data);
            AssertBindingField(manager.Workshop, "_settlement", manager.Data);
            AssertBindingField(manager.Workshop, "_inventionSystem", manager.Inventions);
        }

        [Test]
        public void PreparedCandidate_RejectsDoubleCommitAndRetiredPlan()
        {
            SettlementManager first = PrepareCandidate(CreateLoadedSettlement(701));

            Assert.That(ConsumeCandidate(first, out string firstReason), Is.True, firstReason);
            Assert.That(ConsumeCandidate(first, out string duplicateReason), Is.False);
            Assert.That(duplicateReason, Does.Contain("已经提交"));

            SettlementManager stale = PrepareCandidate(CreateLoadedSettlement(702));
            resetSettlementContentRuntimeMethod.Invoke(null, null);

            Assert.That(ConsumeCandidate(stale, out string staleReason), Is.False);
            Assert.That(staleReason, Does.Contain("内容计划已经失效"));
        }

        private static SettlementInstance CreateLoadedSettlement(int hunterId)
        {
            return new SettlementInstance
            {
                CurrentYear = 4,
                CampaignCalendarId = "standard_two_season_v1",
                CurrentSeasonIndex = 0,
                HuntsPerYear = 1,
                CampaignPacingSchemaVersion = SettlementInstance.CurrentCampaignPacingSchemaVersion,
                Hunters = new List<HunterInstance> { new(null, hunterId) { Name = $"Loaded-{hunterId}" } },
                Resources = new List<ResourceEntry> { new() { Key = "black_salt", Value = 2 } },
                HuntHistory = new List<HuntRecord>
                {
                    new() { RecordId = $"hunt-{hunterId}", ReturnSchemaVersion = HuntRecord.CurrentReturnSchemaVersion, Year = 3, ParticipantHunterIds = new List<int> { hunterId } }
                },
                PendingEventChains = new List<SettlementEventChainCheckpoint>()
            };
        }

        private static SettlementModifierState CreateModifier(string id)
        {
            return new SettlementModifierState
            {
                ModifierId = id,
                SourceKind = SettlementModifierSourceKind.Invention,
                SourceId = "unknown",
                Kind = InventionEffectKind.ModifyStrength,
                Target = InventionEffectTarget.AllLivingAndFutureHunters,
                ConfiguredValue = 1,
                Value = 1
            };
        }

        private static SettlementManager PrepareCandidate(SettlementInstance data)
        {
            var arguments = new object[] { data, null, null };
            Assert.That((bool)prepareCandidateMethod.Invoke(null, arguments), Is.True, arguments[2] as string);
            return (SettlementManager)arguments[1];
        }

        private static bool ConsumeCandidate(SettlementManager candidate, out string reason)
        {
            var arguments = new object[] { null };
            bool consumed = (bool)consumeCandidateMethod.Invoke(candidate, arguments);
            reason = arguments[0] as string;
            return consumed;
        }

        private static void AssertBindingField(object owner, string fieldName, object expected)
        {
            FieldInfo field = owner.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            Assert.That(field.GetValue(owner), Is.SameAs(expected), fieldName);
        }

        private static IRandomSource GetRandom(SettlementManager manager) => (IRandomSource)randomSourceProperty.GetValue(manager);
    }
}
