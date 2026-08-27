using System.Reflection;
using HuntingInDarkness.Settlement;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class CampaignCalendarConfigTests
    {
        private const string CalendarPath = "Assets/GameScripts/GameLogic/HuntingInDarkness/Content/Settlement/CampaignCalendarConfig.asset";

        [Test]
        public void TryCreateDefinition_RejectsNonFirstDefaultSeason()
        {
            CampaignCalendarConfig source = AssetDatabase.LoadAssetAtPath<CampaignCalendarConfig>(CalendarPath);
            CampaignCalendarConfig clone = Object.Instantiate(source);
            try
            {
                typeof(CampaignCalendarConfig).GetField("defaultSeasonId", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(clone, "season_late");

                Assert.That(clone.TryCreateDefinition(out _, out string reason), Is.False);
                Assert.That(reason, Does.Contain("列表首项"));
            }
            finally
            {
                Object.DestroyImmediate(clone);
            }
        }
    }
}
