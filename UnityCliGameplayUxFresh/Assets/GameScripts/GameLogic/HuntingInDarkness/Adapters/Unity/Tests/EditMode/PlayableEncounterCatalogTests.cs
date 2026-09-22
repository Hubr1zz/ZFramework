using GameplayBase.CombatSystem;
using HuntingInDarkness.Bootstrap;
using HuntingInDarkness.Combat;
using HuntingInDarkness.Data;
using NUnit.Framework;
using UnityEditor;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableEncounterCatalogTests
    {
        private const string SettingsPath = "Assets/AssetRaw/Configs/HuntingInDarkness/PlayableBootstrapSettings.asset";

        [Test]
        public void ConfiguredCatalog_ResolvesDefaultEncounterAndAllBossTilesReferenceIt()
        {
            PlayableBootstrapSettings settings = AssetDatabase.LoadAssetAtPath<PlayableBootstrapSettings>(SettingsPath);
            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.EncounterCatalog, Is.Not.Null);
            Assert.That(settings.EncounterCatalog.IsConfigured, Is.True);
            Assert.That(settings.EncounterCatalog.TryCreateSetup(settings.DefaultEncounterId, settings.CreateBattleSetup(), out BattleSetup setup), Is.True);
            Assert.That(setup.Boss, Is.Not.Null);
            Assert.That(setup.SharedHunterCards, Is.Not.Empty);

            int bossTileCount = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:HexTileData", new[] { "Assets/GameScripts/GameLogic/HuntingInDarkness/Content/Hunt" }))
            {
                HexTileData tile = AssetDatabase.LoadAssetAtPath<HexTileData>(AssetDatabase.GUIDToAssetPath(guid));
                if (tile == null || tile.bossEncounterWeight <= 0) continue;
                bossTileCount++;
                Assert.That(tile.bossEncounterId, Is.EqualTo(settings.DefaultEncounterId), tile.name);
                Assert.That(settings.EncounterCatalog.TryCreateSetup(tile.bossEncounterId, settings.CreateBattleSetup(), out _), Is.True, tile.name);
            }
            Assert.That(bossTileCount, Is.GreaterThan(0));
        }

        [Test]
        public void ConfiguredCatalog_RejectsUnknownEncounter()
        {
            PlayableBootstrapSettings settings = AssetDatabase.LoadAssetAtPath<PlayableBootstrapSettings>(SettingsPath);

            bool resolved = settings.EncounterCatalog.TryCreateSetup("missing-encounter", settings.CreateBattleSetup(), out BattleSetup setup);

            Assert.That(resolved, Is.False);
            Assert.That(setup, Is.Null);
        }
    }
}
