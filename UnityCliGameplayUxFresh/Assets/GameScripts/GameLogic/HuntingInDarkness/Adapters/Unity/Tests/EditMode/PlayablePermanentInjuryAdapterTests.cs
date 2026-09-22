using GameplayBase.CombatSystem;
using HuntingInDarkness.Combat;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Hunters;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HuntingInDarkness.Tests
{
    public sealed class PlayablePermanentInjuryAdapterTests
    {
        private const string CatalogPath = "Assets/GameScripts/GameLogic/HuntingInDarkness/Content/Combat/PermanentInjuries/PlayablePermanentInjuryCatalog.asset";

        [Test]
        public void Sync_AppliesPermanentInjuryPenaltyExactlyOnceAndPersistsId()
        {
            var hunter = new HunterInstance(null, 401);
            hunter.Stats.accuracy = 2;
            var combatStats = new CharacterCombatStats();
            combatStats.InitializeInjuryState(HunterInjuryProfile.CreateDefault());
            combatStats.InjuryState.AddPermanentInjury(new PermanentInjury("injury_blind_eye", "一眼失明", new PermanentInjuryStatModifiers(0, -1, 0, 0)));

            PlayableHunterInjuryAdapter.Sync(hunter, combatStats);
            PlayableHunterInjuryAdapter.Sync(hunter, combatStats);

            Assert.That(hunter.Stats.accuracy, Is.EqualTo(1));
            Assert.That(hunter.PermanentInjuryIds, Is.EqualTo(new[] { "injury_blind_eye" }));
            Assert.That(hunter.PermConditions, Is.EqualTo(new[] { "一眼失明" }));

            string json = JsonUtility.ToJson(hunter);
            HunterInstance restored = JsonUtility.FromJson<HunterInstance>(json);
            Assert.That(restored.PermanentInjuryIds, Is.EqualTo(new[] { "injury_blind_eye" }));
            Assert.That(restored.Stats.accuracy, Is.EqualTo(1));

            PlayablePermanentInjuryCatalog catalog = AssetDatabase.LoadAssetAtPath<PlayablePermanentInjuryCatalog>(CatalogPath);
            PlayablePermanentInjuryRuntime.Configure(catalog);
            var restoredCombatStats = new CharacterCombatStats();
            PlayableHunterInjuryAdapter.Apply(restored, restoredCombatStats);
            Assert.That(restoredCombatStats.PermanentWounds, Is.EqualTo(1));
        }

        [Test]
        public void ConfiguredCatalog_ResolvesInjuryThroughFatalSurvivalFlow()
        {
            PlayablePermanentInjuryCatalog catalog = AssetDatabase.LoadAssetAtPath<PlayablePermanentInjuryCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.IsConfigured, Is.True);

            var random = new FirstRandom();
            var state = new HunterInjuryState(HunterInjuryProfile.CreateDefault());
            state.ApplyDamage(HunterBodyPart.Torso, 4, random);

            HunterDamageResult result = state.ApplyDamage(HunterBodyPart.Torso, 1, random, permanentInjuryResolver: catalog);

            Assert.That(result.FatalInjuryTriggered, Is.True);
            Assert.That(result.IsDead, Is.False);
            Assert.That(result.PermanentInjury?.Id, Is.EqualTo("injury_broken_ribs"));
            Assert.That(state.PermanentInjuries, Has.Count.EqualTo(1));
            Assert.That(state.DeathDeck.DeathCardCount, Is.EqualTo(1));
        }

        private sealed class FirstRandom : HuntingInDarkness.GameCore.Foundation.IRandomSource
        {
            public int Next(int minInclusive, int maxExclusive) => minInclusive;
            public double NextDouble() => 0d;
        }
    }
}
