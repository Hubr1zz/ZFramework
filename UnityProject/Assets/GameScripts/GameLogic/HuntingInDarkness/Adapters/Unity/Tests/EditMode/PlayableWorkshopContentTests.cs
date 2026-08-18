using System.Linq;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableWorkshopContentTests
    {
        [Test]
        public void WorkshopCatalog_ProvidesBuildableArmorWorkshopAndGatedRecipe()
        {
            PlayableWorkshopCatalog catalog = Resources.Load<PlayableWorkshopCatalog>("HuntingInDarkness/PlayableWorkshopCatalog");
            PlayableSettlementContentExtension[] extensions = Resources.LoadAll<PlayableSettlementContentExtension>("HuntingInDarkness/SettlementExtensions");

            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.Workshops, Has.Count.EqualTo(1));
            PlayableWorkshopDefinition workshop = catalog.Workshops[0];
            Assert.That(workshop.WorkshopId, Is.EqualTo("armor_workshop"));
            Assert.That(workshop.DisplayName, Is.EqualTo("护甲工坊"));
            Assert.That(workshop.RequiredInvention, Is.Not.Null);
            Assert.That(workshop.RequiredInvention.inventionName, Is.EqualTo("工具"));
            Assert.That(workshop.Costs, Has.Count.EqualTo(2));
            Assert.That(workshop.Costs.All(cost => cost.Item != null && cost.Amount > 0), Is.True);
            Assert.That(extensions.SelectMany(extension => extension.Recipes).Any(recipe => recipe.requiredWorkshopId == workshop.WorkshopId && recipe.outputItem != null), Is.True);
        }
    }
}
