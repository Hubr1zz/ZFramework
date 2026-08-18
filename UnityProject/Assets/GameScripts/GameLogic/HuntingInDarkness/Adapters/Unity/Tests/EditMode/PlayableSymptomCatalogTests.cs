using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HuntingInDarkness.Tests
{
    public sealed class PlayableSymptomCatalogTests
    {
        private const string CatalogPath = "Assets/GameScripts/GameLogic/HuntingInDarkness/Content/Settlement/Symptoms/PlayableSymptomCatalog.asset";

        [Test]
        public void ConfiguredCatalog_SynchronizesTemplateAilmentAndSurvivesJsonRoundTrip()
        {
            PlayableSymptomCatalog catalog = AssetDatabase.LoadAssetAtPath<PlayableSymptomCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.IsConfigured, Is.True);
            PlayableSymptomRuntime.Configure(catalog);

            var template = ScriptableObject.CreateInstance<HunterData>();
            template.initialStats.strength = 2;
            template.startingAilments.Add("胆怯");
            var hunter = new HunterInstance(template, 701);
            PlayableSymptomRuntime.SynchronizeHunter(hunter);
            PlayableSymptomRuntime.SynchronizeHunter(hunter);

            Assert.That(hunter.Stats.strength, Is.EqualTo(1));
            Assert.That(hunter.SymptomStates, Has.Count.EqualTo(1));
            string json = JsonUtility.ToJson(hunter);
            HunterInstance restored = JsonUtility.FromJson<HunterInstance>(json);
            Assert.That(restored.SymptomStates[0].SymptomId, Is.EqualTo("symptom_cowardice"));
            Assert.That(restored.Stats.strength, Is.EqualTo(1));
            Object.DestroyImmediate(template);
        }
    }
}
