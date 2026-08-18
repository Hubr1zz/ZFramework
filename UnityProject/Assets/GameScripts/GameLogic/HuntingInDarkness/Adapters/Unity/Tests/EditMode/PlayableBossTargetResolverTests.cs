using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameplayBase.CombatSystem;
using HuntingInDarkness.Combat;
using HuntingInDarkness.GameCore.Combat;
using HuntingInDarkness.GameCore.Foundation;
using NUnit.Framework;
using SO.Character;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableBossTargetResolverTests
    {
        [Test]
        public void ResolveAsync_PlayerChoiceReturnsSelectedLegalTarget()
        {
            var input = new TargetInput(7);
            var candidates = new List<BossTargetCandidate>
            {
                new BossTargetCandidate(4, 0, 0),
                new BossTargetCandidate(7, 0, 0)
            };
            var resolver = new PlayableBossTargetResolver(new LastRandom());

            int target = resolver.ResolveAsync("黑暗撕咬", BossTargetPolicy.PlayerChoice, candidates, input).GetAwaiter().GetResult();

            Assert.That(target, Is.EqualTo(7));
            Assert.That(input.LastPrompt, Does.Contain("黑暗撕咬"));
            Assert.That(input.LastTargets, Is.EqualTo(new[] { 4, 7 }));
            Assert.That(input.Results, Is.Empty);
        }

        [Test]
        public void ResolveAsync_CancelFallsBackToRandomLegalTarget()
        {
            var input = new TargetInput(-1);
            var candidates = new List<BossTargetCandidate>
            {
                new BossTargetCandidate(4, 0, 0),
                new BossTargetCandidate(7, 0, 0)
            };
            var resolver = new PlayableBossTargetResolver(new LastRandom());

            int target = resolver.ResolveAsync("黑暗撕咬", BossTargetPolicy.PlayerChoice, candidates, input).GetAwaiter().GetResult();

            Assert.That(target, Is.EqualTo(7));
            Assert.That(input.Results, Has.Count.EqualTo(1));
            Assert.That(input.Results[0], Does.Contain("随机锁定"));
        }

        [Test]
        public void ResolveAsync_SinglePriorityTargetSkipsInput()
        {
            var input = new TargetInput(99);
            var candidates = new List<BossTargetCandidate>
            {
                new BossTargetCandidate(4, 3, 0),
                new BossTargetCandidate(7, 1, 0)
            };
            var resolver = new PlayableBossTargetResolver(new LastRandom());

            int target = resolver.ResolveAsync("黑暗撕咬", BossTargetPolicy.Nearest, candidates, input).GetAwaiter().GetResult();

            Assert.That(target, Is.EqualTo(7));
            Assert.That(input.RequestCount, Is.Zero);
        }

        private sealed class LastRandom : IRandomSource
        {
            public int Next(int minInclusive, int maxExclusive) => maxExclusive - 1;
            public double NextDouble() => 1d;
        }

        private sealed class TargetInput : IPlayerInputProvider
        {
            private readonly int selectedTarget;
            public int RequestCount { get; private set; }
            public string LastPrompt { get; private set; }
            public List<int> LastTargets { get; private set; }
            public List<string> Results { get; } = new();

            public TargetInput(int selectedTarget) => this.selectedTarget = selectedTarget;

            public UniTask<int> RequestSelectTarget(string prompt, List<int> validTargetIds)
            {
                RequestCount++;
                LastPrompt = prompt;
                LastTargets = new List<int>(validTargetIds);
                return UniTask.FromResult(selectedTarget);
            }

            public UniTask ShowResult(string message)
            {
                Results.Add(message);
                return UniTask.CompletedTask;
            }

            public UniTask<int> RequestRoll(string prompt, int maxExclusive) => UniTask.FromResult(0);
            public UniTask<Vector2Int?> RequestSelectTile(string prompt, List<Vector2Int> validTiles) => UniTask.FromResult<Vector2Int?>(null);
            public UniTask<int> RequestSelectCard(string prompt, List<int> validCardIds) => UniTask.FromResult(-1);
            public UniTask PlayShuffleAndReveal(List<HitLocationRuntimeState> allCards, List<HitLocationRuntimeState> toReveal) => UniTask.CompletedTask;
            public UniTask<HitLocationRuntimeState> RequestSelectRevealedCard(string prompt, List<HitLocationRuntimeState> revealedCards) => UniTask.FromResult<HitLocationRuntimeState>(null);
            public UniTask<WeaponData> RequestSelectWeapon(string prompt, List<WeaponData> candidates) => UniTask.FromResult<WeaponData>(null);
        }
    }
}
