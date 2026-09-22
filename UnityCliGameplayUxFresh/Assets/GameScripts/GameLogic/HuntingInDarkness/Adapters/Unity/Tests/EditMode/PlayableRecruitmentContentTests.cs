using System.Collections.Generic;
using HuntingInDarkness.Data;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UnityEditor;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableRecruitmentContentTests
    {
        private const string CatalogPath = "Assets/GameScripts/GameLogic/HuntingInDarkness/Content/Settlement/PlayableSettlementContentCatalog.asset";

        [Test]
        public void Catalog_ProvidesNamedRecruitmentTemplatesAndResourceCost()
        {
            PlayableSettlementContentCatalog catalog = AssetDatabase.LoadAssetAtPath<PlayableSettlementContentCatalog>(CatalogPath);

            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.RecruitmentTemplates.Count, Is.GreaterThanOrEqualTo(3));
            Assert.That(catalog.RecruitmentCostItem, Is.Not.Null);
            Assert.That(catalog.RecruitmentCostItem.itemType, Is.EqualTo(ItemType.Resource));
            Assert.That(catalog.RecruitmentCost, Is.GreaterThan(0));
            Assert.That(catalog.RecruitmentPopulationCost, Is.EqualTo(1));
            Assert.That(catalog.MaximumLivingHunters, Is.GreaterThanOrEqualTo(4));

            var names = new HashSet<string>();
            var ids = new HashSet<string>();
            foreach (HunterData template in catalog.RecruitmentTemplates)
            {
                Assert.That(template, Is.Not.Null);
                Assert.That(template.HasExplicitContentId, Is.True);
                Assert.That(ids.Add(template.ContentId), Is.True, $"重复的招募模板 ID：{template.ContentId}");
                Assert.That(template.hunterName, Is.Not.Empty);
                Assert.That(names.Add(template.hunterName), Is.True, $"重复的招募模板名：{template.hunterName}");
            }
        }

    }
}
