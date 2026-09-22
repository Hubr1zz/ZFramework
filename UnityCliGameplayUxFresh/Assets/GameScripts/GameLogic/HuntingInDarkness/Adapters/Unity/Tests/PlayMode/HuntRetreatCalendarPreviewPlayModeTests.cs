using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.ActionFlow.Hunt;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.ViewLayer.Hunt;
using HuntingInDarkness.ViewLayer.Tabletop;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace HuntingInDarkness.Adapter.PlayModeTests
{
    public sealed class HuntRetreatCalendarPreviewPlayModeTests
    {
        private readonly List<Object> createdObjects = new();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (Object createdObject in createdObjects)
                if (createdObject != null)
                    Object.Destroy(createdObject);
            createdObjects.Clear();
            yield return null;
        }

        [Test]
        public void Create_WithThreeSeasons_UsesConfiguredBoundary()
        {
            CampaignCalendarDefinition calendar = CreateCalendar();

            HuntReturnCalendarPreview sameYear = HuntReturnCalendarPreview.Create(calendar, 4, 0);
            HuntReturnCalendarPreview nextYear = HuntReturnCalendarPreview.Create(calendar, 4, 2);
            HuntReturnCalendarPreview singleSeason = HuntReturnCalendarPreview.Create(new CampaignCalendarDefinition("single_season", new[] { new SeasonDefinition("cycle", "周期", 0) }), 7, 0);

            Assert.That(sameYear.IsAvailable, Is.True);
            Assert.That(sameYear.NextYear, Is.EqualTo(4));
            Assert.That(sameYear.NextSeasonId, Is.EqualTo("deep_cold"));
            Assert.That(sameYear.AnnualEventGateOpens, Is.False);
            Assert.That(nextYear.IsAvailable, Is.True);
            Assert.That(nextYear.NextYear, Is.EqualTo(5));
            Assert.That(nextYear.NextSeasonId, Is.EqualTo("thaw"));
            Assert.That(nextYear.AnnualEventGateOpens, Is.True);
            Assert.That(singleSeason.NextYear, Is.EqualTo(8));
            Assert.That(singleSeason.NextSeasonId, Is.EqualTo("cycle"));
            Assert.That(singleSeason.AnnualEventGateOpens, Is.True);
        }

        [UnityTest]
        public IEnumerator RequestOpen_ProjectsSameYearSeasonWithoutAnnualEventClaim()
        {
            HuntReturnCalendarPreview calendar = HuntReturnCalendarPreview.Create(CreateCalendar(), 4, 0);
            HuntRetreatPanel3D panel = CreatePanel(calendar);

            panel.RequestOpen();
            yield return null;

            string body = panel.GetComponentInChildren<TabletopEventPrimaryCard3D>().GetComponentsInChildren<TextMeshPro>().Single(text => text.name == "Body").text;
            Assert.That(body, Does.Contain("成功回营后：第 4 年 · 深冬；不会创建新年度事件。"));
            Assert.That(body, Does.Not.Contain("第 5 年"));
            Assert.That(GetConfirmCard(panel).IsInteractable, Is.True);
        }

        [UnityTest]
        public IEnumerator RequestOpen_ProjectsYearBoundaryAsOptionalAnnualSettlement()
        {
            HuntReturnCalendarPreview calendar = HuntReturnCalendarPreview.Create(CreateCalendar(), 4, 2);
            HuntRetreatPanel3D panel = CreatePanel(calendar);

            panel.RequestOpen();
            yield return null;

            string body = panel.GetComponentInChildren<TabletopEventPrimaryCard3D>().GetComponentsInChildren<TextMeshPro>().Single(text => text.name == "Body").text;
            Assert.That(body, Does.Contain("成功回营后：第 5 年 · 融雪；将进入年度事件结算（如有）。"));
            Assert.That(body, Does.Not.Contain("必定"));
            Assert.That(GetConfirmCard(panel).IsInteractable, Is.True);
        }

        [UnityTest]
        public IEnumerator RequestOpen_WithInvalidCalendar_DisablesAuthoritativeConfirmation()
        {
            HuntReturnCalendarPreview calendar = HuntReturnCalendarPreview.Create(new CampaignCalendarDefinition("invalid", new List<SeasonDefinition>()), 4, 0);
            HuntRetreatPanel3D panel = CreatePanel(calendar);

            panel.RequestOpen();
            yield return null;

            Assert.That(GetConfirmCard(panel).IsInteractable, Is.False);
            TextMeshPro status = GetConfirmCard(panel).GetComponentsInChildren<TextMeshPro>().Single(text => text.name == "Status");
            Assert.That(status.text, Is.EqualTo("时间线不可用"));
        }

        private HuntRetreatPanel3D CreatePanel(HuntReturnCalendarPreview calendar)
        {
            var root = new GameObject("HuntRetreatCalendarPreviewTest");
            createdObjects.Add(root);
            var manager = new HuntManager(null, bindInitialContent: false);
            HuntRetreatPreview preview = HuntRetreatPreview.Create(manager).WithCalendar(calendar);
            HuntRetreatPanel3D panel = HuntRetreatPanel3D.Create(root.transform);
            panel.Initialize(new RetreatInput(preview), manager);
            return panel;
        }

        private static TabletopEventChoiceCard3D GetConfirmCard(HuntRetreatPanel3D panel)
            => panel.GetComponentsInChildren<TabletopEventChoiceCard3D>().Single(card => card.DisplayName == "结算并回营");

        private static CampaignCalendarDefinition CreateCalendar()
        {
            return new CampaignCalendarDefinition("three_seasons", new[]
            {
                new SeasonDefinition("thaw", "融雪", 0),
                new SeasonDefinition("deep_cold", "深冬", 1),
                new SeasonDefinition("long_night", "长夜", 2)
            });
        }

        private sealed class RetreatInput : IPlayableHuntRetreatInput
        {
            private readonly HuntRetreatPreview preview;

            public RetreatInput(HuntRetreatPreview preview)
            {
                this.preview = preview;
            }

            public bool IsReturnCheckpointLocked => false;
            public HuntRetreatPreview GetRetreatPreview() => preview;
            public UniTask<HuntRetreatCommandResult> RequestRetreatAsync(HuntRetreatDecision decision)
                => UniTask.FromResult(HuntRetreatCommandResult.Failed("测试不提交回营。"));
        }
    }
}
