using HuntingInDarkness.Data;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UnityEditor;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableHunterRecoveryContentTests
    {
        private const string CatalogPath = "Assets/GameScripts/GameLogic/HuntingInDarkness/Content/Settlement/PlayableSettlementContentCatalog.asset";

        [Test]
        public void Catalog_ProvidesResourceBackedBasicRecovery()
        {
            PlayableSettlementContentCatalog catalog = AssetDatabase.LoadAssetAtPath<PlayableSettlementContentCatalog>(CatalogPath);

            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.RecoveryCostItem, Is.Not.Null);
            Assert.That(catalog.RecoveryCostItem.itemType, Is.EqualTo(ItemType.Resource));
            Assert.That(catalog.RecoveryCost, Is.GreaterThan(0));
            Assert.That(catalog.RecoveryAmount, Is.GreaterThan(0));
        }
    }
}
