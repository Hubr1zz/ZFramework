using HuntingInDarkness.Bootstrap;
using HuntingInDarkness.Combat;
using HuntingInDarkness.GameCore.Cards;
using HuntingInDarkness.GameCore.Combat;
using NUnit.Framework;
using SO.Boss.ActionCard;
using UnityEditor;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableEncourageActionTests
    {
        private const string CardPath = "Assets/GameScripts/GameLogic/HuntingInDarkness/Content/Combat/PlayableCards/PlayableEncourage.asset";
        private const string SettingsPath = "Assets/GameScripts/GameLogic/HuntingInDarkness/Resources/HuntingInDarkness/PlayableBootstrapSettings.asset";

        [Test]
        public void RelieveOvertimeCharacter_RestoresTimeWithoutSpendingWillpower()
        {
            var timeline = CreateOvertimeTimeline();

            bool relieved = timeline.TryRelieveOvertimeCharacter(2, out TimePointChange change);

            Assert.That(relieved, Is.True);
            Assert.That(change.OldValue, Is.EqualTo(-4));
            Assert.That(change.NewValue, Is.EqualTo(-3));
            Assert.That(timeline.Get(2).Status, Is.EqualTo(TimelineActionStatus.Ready));
            Assert.That(timeline.Get(1).Willpower, Is.EqualTo(2));
        }

        [Test]
        public void LegacyAssist_StillSpendsExactlyOneWillpower()
        {
            var timeline = CreateOvertimeTimeline();

            AssistanceResult result = timeline.TryAssistOvertimeCharacter(1, 2);

            Assert.That(result.Success, Is.True);
            Assert.That(timeline.Get(1).Willpower, Is.EqualTo(1));
            Assert.That(timeline.Get(2).CurrentTimePoints, Is.EqualTo(-3));
        }

        [Test]
        public void EncourageAsset_UsesWillpowerCostAndPreparedEffect()
        {
            CharacterActionCardData card = AssetDatabase.LoadAssetAtPath<CharacterActionCardData>(CardPath);

            Assert.That(card, Is.Not.Null);
            Assert.That(card.cardId, Is.EqualTo("playable.encourage"));
            Assert.That(card.CreateRuntimeDefinition().Costs.Count, Is.EqualTo(1));
            Assert.That(card.CreateRuntimeDefinition().Costs[0].Kind, Is.EqualTo(ActionCardCostKind.Willpower));
            Assert.That(card.CreateRuntimeDefinition().Costs[0].Amount, Is.EqualTo(1));
            Assert.That(card.faceUpEffects, Has.Count.EqualTo(1));
            Assert.That(card.faceUpEffects[0].CreateRuntime(), Is.TypeOf<PlayablePreparedEncourageEffect>());
            Assert.That(card.IsDiscardable, Is.False);
        }

        [Test]
        public void BootstrapSettings_ProvidesEncourageAsSharedHunterCard()
        {
            PlayableBootstrapSettings settings = AssetDatabase.LoadAssetAtPath<PlayableBootstrapSettings>(SettingsPath);
            CharacterActionCardData card = AssetDatabase.LoadAssetAtPath<CharacterActionCardData>(CardPath);

            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.CreateBattleSetup().SharedHunterCards, Does.Contain(card));
        }

        private static TimelineService CreateOvertimeTimeline()
        {
            var timeline = new TimelineService();
            timeline.Register(1, isBoss: false, initialWillpower: 2);
            timeline.Register(2, isBoss: false, initialWillpower: 2);
            timeline.SetRoundLimit(3);
            timeline.ProcessOverflowForNewPlayerTurn();
            timeline.AddTimePoints(2, 7);
            timeline.ProcessOverflowForNewPlayerTurn();
            return timeline;
        }
    }
}
