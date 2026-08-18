using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameplayBase.CombatSystem;
using HuntingInDarkness.GameCore.Combat;
using HuntingInDarkness.GameCore.Foundation;

namespace HuntingInDarkness.Combat
{
    /// <summary>把纯目标优先级规则适配为现有玩家输入协议。</summary>
    public sealed class PlayableBossTargetResolver
    {
        private readonly IRandomSource random;

        public PlayableBossTargetResolver(IRandomSource random)
        {
            this.random = random ?? throw new System.ArgumentNullException(nameof(random));
        }

        public async UniTask<int> ResolveAsync(string actionName, BossTargetPolicy policy, IReadOnlyList<BossTargetCandidate> candidates, IPlayerInputProvider input, CancellationToken cancellationToken = default)
        {
            List<int> priorityTargets = BossTargetRules.GetPriorityTargets(candidates, policy, random);
            if (priorityTargets.Count == 0)
                return -1;
            if (priorityTargets.Count == 1)
                return priorityTargets[0];

            if (input != null)
            {
                int selected = await input.RequestSelectTarget(BuildPrompt(actionName, policy), priorityTargets, cancellationToken);
                if (priorityTargets.Contains(selected))
                    return selected;

                await input.ShowResult("没有指定目标，怪物将从合法目标中随机锁定一名猎人。", cancellationToken);
            }

            return BossTargetRules.SelectFallback(priorityTargets, random);
        }

        private static string BuildPrompt(string actionName, BossTargetPolicy policy)
        {
            string instruction = policy switch
            {
                BossTargetPolicy.Nearest => "选择一名距离最近的猎人",
                BossTargetPolicy.MostInjured => "选择一名伤势最重的猎人",
                _ => "为怪物选择一名目标猎人"
            };
            return $"<b>{actionName}</b>\n{instruction}";
        }
    }
}
