using HuntingInDarkness.ActionFlow.Events;
using HuntingInDarkness.Data;
using HuntingInDarkness.Hunt;

namespace HuntingInDarkness.ActionFlow.Hunt
{
    /// <summary>绑定一次地块事件提交；事件效果不能从内容字符串任意寻址地图。</summary>
    internal sealed class HuntTileEventWorldCommand : IPlayableEventWorldCommand
    {
        private readonly HuntManager manager;
        private readonly HuntTileInteractionCommit commit;

        public HuntTileEventWorldCommand(HuntManager manager, HuntTileInteractionCommit commit)
        {
            this.manager = manager;
            this.commit = commit;
        }

        public bool TryApply(EventEffect effect, out PlayableEventWorldChange change, out string reason)
        {
            if (effect?.effectType != EventEffectType.ExhaustCurrentHuntTileResources)
            {
                change = default;
                reason = "狩猎事件世界效果类型无效。";
                return false;
            }
            if (!string.IsNullOrWhiteSpace(effect.targetName) || !string.IsNullOrWhiteSpace(effect.bodyPart) || effect.value != 0)
            {
                change = default;
                reason = "狩猎事件世界效果参数无效。";
                return false;
            }
            if (manager == null)
            {
                change = default;
                reason = "狩猎管理器不存在。";
                return false;
            }
            return manager.TryExhaustEventTileResourcePoints(commit, out change, out reason);
        }
    }
}
