using System.Collections.Generic;
using HuntingInDarkness.ContentTables;
using HuntingInDarkness.Data;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableTraitIdentityTests
    {
        [Test]
        public void TraitTable_ResolvesStableIdsAndLegacyDisplayNames()
        {
            TextAsset table = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/AssetRaw/Configs/HuntingInDarkness/Tables/traits.json");

            bool loaded = PlayableTraitCatalog.TryLoad(table, out PlayableTraitCatalog catalog, out string reason);

            Assert.That(loaded, Is.True, reason);
            Assert.That(catalog.ContainsCanonicalId("trait_watcher"), Is.True);
            Assert.That(catalog.ResolveContentId("守望者"), Is.EqualTo("trait_watcher"));
            Assert.That(catalog.GetDisplayName("trait_watcher"), Is.EqualTo("守望者"));
        }

        [Test]
        public void Migration_NormalizesLegacyTraitsOnceAndPreservesUnknownContent()
        {
            TextAsset table = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/AssetRaw/Configs/HuntingInDarkness/Tables/traits.json");
            Assert.That(PlayableTraitCatalog.TryLoad(table, out PlayableTraitCatalog catalog, out string reason), Is.True, reason);
            var settlement = new SettlementInstance
            {
                Hunters = new List<HunterInstance>
                {
                    new(null, 1) { Traits = new List<string> { "守望者", "trait_watcher", "  mod_trait  " } }
                }
            };

            bool first = PlayableTraitRegistry.MigratePersistentState(settlement, catalog);
            bool second = PlayableTraitRegistry.MigratePersistentState(settlement, catalog);

            Assert.That(first, Is.True);
            Assert.That(second, Is.False);
            Assert.That(settlement.TraitIdentitySchemaVersion, Is.EqualTo(PlayableTraitRegistry.CurrentIdentitySchemaVersion));
            Assert.That(settlement.Hunters[0].Traits, Is.EqualTo(new[] { "trait_watcher", "mod_trait" }));
        }

        [Test]
        public void Migration_DoesNotRewriteNewerSchema()
        {
            var settlement = new SettlementInstance
            {
                TraitIdentitySchemaVersion = PlayableTraitRegistry.CurrentIdentitySchemaVersion + 1,
                Hunters = new List<HunterInstance> { new(null, 1) { Traits = new List<string> { "守望者" } } }
            };

            bool changed = PlayableTraitRegistry.MigratePersistentState(settlement, null);

            Assert.That(changed, Is.False);
            Assert.That(settlement.Hunters[0].Traits, Is.EqualTo(new[] { "守望者" }));
        }

        [Test]
        public void TraitTable_RejectsAliasCollisions()
        {
            var table = new TextAsset("{\"version\":1,\"traits\":[{\"id\":\"trait_one\",\"displayName\":\"同名\"},{\"id\":\"trait_two\",\"displayName\":\"同名\"}]}");

            bool loaded = PlayableTraitCatalog.TryLoad(table, out _, out string reason);

            Object.DestroyImmediate(table);
            Assert.That(loaded, Is.False);
            Assert.That(reason, Does.Contain("冲突"));
        }

        [Test]
        public void TraitTable_ProjectsConfiguredGameplayKeywords()
        {
            TextAsset table = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/AssetRaw/Configs/HuntingInDarkness/Tables/traits.json");
            Assert.That(PlayableTraitCatalog.TryLoad(table, out PlayableTraitCatalog catalog, out string reason), Is.True, reason);
            var keywords = new HashSet<string>();

            catalog.AddKeywords(keywords, new[] { "trait_stone_speaker" });

            Assert.That(keywords, Does.Contain("trait_stone_speaker"));
            Assert.That(keywords, Does.Contain("石语者"));
            Assert.That(keywords, Does.Contain("stone"));
        }
    }
}
