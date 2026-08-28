using System.Collections.Generic;
using HuntingInDarkness.Bootstrap;
using HuntingInDarkness.Combat;
using HuntingInDarkness.GameCore.Cards;
using NUnit.Framework;
using SO.Boss.ActionCard;
using UnityEditor;
using GameplayBase.CombatSystem.Cards.FlipConditions;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableCombatInspirationTests
    {
        private const string CardFolder = "Assets/GameScripts/GameLogic/HuntingInDarkness/Content/Combat/PlayableCards/";
        private const string SettingsPath = "Assets/AssetRaw/Configs/HuntingInDarkness/PlayableBootstrapSettings.asset";

        [Test]
        public void Mind_RequiresExplicitReplacementAtCapacity()
        {
            var mind = new CombatInspirationMind(2);
            InspirationGain red = mind.TryAdd(CombatInspirationColor.Red);
            InspirationGain blue = mind.TryAdd(CombatInspirationColor.Blue);

            InspirationGain full = mind.TryAdd(CombatInspirationColor.Yellow);
            InspirationGain replaced = mind.TryAdd(CombatInspirationColor.Yellow, red.Token.Id);

            Assert.That(red.Result, Is.EqualTo(InspirationGainResult.Added));
            Assert.That(blue.Result, Is.EqualTo(InspirationGainResult.Added));
            Assert.That(full.Result, Is.EqualTo(InspirationGainResult.RequiresReplacement));
            Assert.That(replaced.Result, Is.EqualTo(InspirationGainResult.Replaced));
            Assert.That(mind.Tokens.Count, Is.EqualTo(2));
            Assert.That(mind.Tokens, Has.None.Matches<CombatInspirationToken>(token => token.Id == red.Token.Id));
            Assert.That(mind.Tokens, Has.Some.Matches<CombatInspirationToken>(token => token.Color == CombatInspirationColor.Yellow));
        }

        [Test]
        public void Mind_SpendsOnlyDistinctMatchingTokens()
        {
            var mind = new CombatInspirationMind();
            InspirationGain red = mind.TryAdd(CombatInspirationColor.Red);
            InspirationGain blue = mind.TryAdd(CombatInspirationColor.Blue);

            Assert.That(mind.CanSpend(new[] { red.Token.Id }, InspirationRequirement.Red, 1), Is.True);
            Assert.That(mind.CanSpend(new[] { blue.Token.Id }, InspirationRequirement.Red, 1), Is.False);
            Assert.That(mind.CanSpend(new[] { red.Token.Id, red.Token.Id }, InspirationRequirement.Any, 2), Is.False);
            Assert.That(mind.TrySpend(new[] { blue.Token.Id }), Is.True);
            Assert.That(mind.Tokens.Count, Is.EqualTo(1));
        }

        [Test]
        public void ResourcePool_ReservesSpecificColorsBeforeAnyCosts()
        {
            var pool = new ActionCardResourcePool();
            pool.Register(1);
            pool.TryAddCombatInspiration(1, CombatInspirationColor.Red);
            pool.TryAddCombatInspiration(1, CombatInspirationColor.Blue);
            var validCosts = new List<ActionCardCostDefinition>
            {
                new(ActionCardCostKind.CombatInspiration, 1, inspirationRequirement: InspirationRequirement.Red),
                new(ActionCardCostKind.CombatInspiration, 1, inspirationRequirement: InspirationRequirement.Any)
            };
            var invalidCosts = new List<ActionCardCostDefinition>
            {
                new(ActionCardCostKind.CombatInspiration, 1, inspirationRequirement: InspirationRequirement.Red),
                new(ActionCardCostKind.CombatInspiration, 2, inspirationRequirement: InspirationRequirement.Any)
            };

            Assert.That(pool.CanPayCosts(1, validCosts), Is.True);
            Assert.That(pool.CanPayCosts(1, invalidCosts), Is.False);
        }

        [Test]
        public void ResourcePool_LegacyAdjustmentStillSupportsRemoval()
        {
            var pool = new ActionCardResourcePool();
            pool.Register(1, 3);

            Assert.That(pool.AddCombatInspiration(1, -2), Is.EqualTo(1));
            Assert.That(pool.GetCombatInspiration(1), Is.EqualTo(1));
        }

        [Test]
        public void FocusRoll_MapsAllNineOutcomesToTwoValidColors()
        {
            var outcomes = new HashSet<(CombatInspirationColor, CombatInspirationColor)>();
            for (int roll = 0; roll < FocusInspirationRules.OutcomeCount; roll++)
                outcomes.Add(FocusInspirationRules.ResolveRoll(roll));

            Assert.That(outcomes.Count, Is.EqualTo(9));
            Assert.That(FocusInspirationRules.ResolveRoll(-10), Is.EqualTo((CombatInspirationColor.Red, CombatInspirationColor.Red)));
            Assert.That(FocusInspirationRules.ResolveRoll(99), Is.EqualTo((CombatInspirationColor.Yellow, CombatInspirationColor.Yellow)));
        }

        [Test]
        public void PlayableCards_ExposeFormalFocusAndInspirationCosts()
        {
            CharacterActionCardData focus = LoadCard("PlayableFocus.asset");
            CharacterActionCardData move = LoadCard("PlayableAdvance.asset");
            CharacterActionCardData attack = LoadCard("PlayableStrike.asset");

            Assert.That(focus.faceUpEffects[0].CreateRuntime(), Is.TypeOf<PlayableFocusEffect>());
            AssertFormalBasicCost(focus, inspirationCost: 0);
            AssertFormalBasicCost(move, inspirationCost: 1);
            AssertFormalBasicCost(attack, inspirationCost: 1);
        }

        [Test]
        public void BootstrapSettings_ProvidesFocusToEveryHunter()
        {
            PlayableBootstrapSettings settings = AssetDatabase.LoadAssetAtPath<PlayableBootstrapSettings>(SettingsPath);
            CharacterActionCardData focus = LoadCard("PlayableFocus.asset");

            Assert.That(settings.CreateBattleSetup().SharedHunterCards, Does.Contain(focus));
        }

        [Test]
        public void ActionCardState_FaceDownCardCannotBePlayedUntilRestored()
        {
            var state = new ActionCardState(1, 1, isDiscardable: true);

            state.Flip();
            Assert.That(state.CanPlay, Is.False);
            Assert.That(state.CanDiscard, Is.False);

            state.Restore();
            Assert.That(state.CanPlay, Is.True);
            Assert.That(state.CanDiscard, Is.True);
        }

        [Test]
        public void BasicCards_ExposeFormalFlipRestoreAndBurstLifecycle()
        {
            CharacterActionCardData attack = LoadCard("PlayableStrike.asset");
            CharacterActionCardData move = LoadCard("PlayableAdvance.asset");

            Assert.That(attack.flipConditions[0], Is.TypeOf<FlipOnPlayConditionData>());
            Assert.That(attack.restoreConditions[0], Is.TypeOf<CombatInspirationRestoreConditionData>());
            var attackRestore = (CombatInspirationRestoreCondition)attack.restoreConditions[0].CreateRuntime();
            Assert.That(attackRestore.Cost.Kind, Is.EqualTo(ActionCardCostKind.CombatInspiration));
            Assert.That(attackRestore.Cost.Amount, Is.EqualTo(1));
            Assert.That(attack.CreateRuntimeDefinition().AllowsBurst, Is.True);
            Assert.That(attack.burstReward.timePointReward, Is.EqualTo(1));

            Assert.That(move.flipConditions[0], Is.TypeOf<FlipOnPlayConditionData>());
            Assert.That(move.restoreConditions[0], Is.TypeOf<RestoreOnTurnEndData>());
            Assert.That(move.burstReward.bonusEffects[0].CreateRuntime(), Is.TypeOf<GainCombatInspirationEffect>());
            Assert.That(move.CreateRuntimeDefinition().AllowsBurst, Is.True);

            var attackInstance = new Core.CharacterActionCardInstance(attack, 1);
            var moveInstance = new Core.CharacterActionCardInstance(move, 1);
            attackInstance.SetFace(GameplayBase.CardFace.FaceDown);
            moveInstance.SetFace(GameplayBase.CardFace.FaceDown);
            Assert.That(attackInstance.CanRestore, Is.True);
            Assert.That(moveInstance.CanRestore, Is.False);
        }

        private static CharacterActionCardData LoadCard(string fileName)
        {
            CharacterActionCardData card = AssetDatabase.LoadAssetAtPath<CharacterActionCardData>(CardFolder + fileName);
            Assert.That(card, Is.Not.Null);
            return card;
        }

        private static void AssertFormalBasicCost(CharacterActionCardData card, int inspirationCost)
        {
            IReadOnlyList<ActionCardCostDefinition> costs = card.CreateRuntimeDefinition().Costs;
            Assert.That(costs.Count, Is.EqualTo(inspirationCost > 0 ? 2 : 1));
            Assert.That(costs[0].Kind, Is.EqualTo(ActionCardCostKind.TimePoint));
            Assert.That(costs[0].Amount, Is.EqualTo(2));
            if (inspirationCost <= 0) return;
            Assert.That(costs[1].Kind, Is.EqualTo(ActionCardCostKind.CombatInspiration));
            Assert.That(costs[1].Amount, Is.EqualTo(inspirationCost));
            Assert.That(costs[1].InspirationRequirement, Is.EqualTo(InspirationRequirement.Any));
        }
    }
}
