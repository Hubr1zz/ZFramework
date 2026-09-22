using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameplayBase;
using GameplayBase.CombatSystem;
using SO.Character;

namespace HuntingInDarkness.Combat
{
    /// <summary>从本次出战装备中选择武器，并将武器的速度与射程投影到旧战斗流程。</summary>
    public sealed class PlayableLoadoutWeaponResolver : IWeaponResolver
    {
        public async UniTask<WeaponData> ResolveAsync(ActionCardContext context, IPlayerInputProvider input, CancellationToken cancellationToken = default)
        {
            if (context?.GameContext is not ICombatRuntimeDataProvider combatData) return null;

            CharacterRuntimeData character = combatData.GetCharacterData(context.SourceCharacterId);
            if (character == null) return null;

            var candidates = GetInRangeWeapons(context, character.GetAvailableWeapons());
            if (candidates.Count == 0)
            {
                await input.ShowResult("Boss 不在当前武器的有效射程内。", cancellationToken);
                return null;
            }

            var weapon = candidates.Count == 1 ? candidates[0] : await input.RequestSelectWeapon("选择本次攻击使用的武器", candidates, cancellationToken);
            PlayableHunterCombatAdapter.ActivateWeapon(character, weapon);
            return weapon;
        }

        private static List<WeaponData> GetInRangeWeapons(ActionCardContext context, List<WeaponData> weapons)
        {
            if (context.BoardQuery == null || context.GameContext?.Boss == null) return weapons;

            var origin = context.BoardQuery.GetEntityPosition(context.SourceCharacterId);
            var target = context.BoardQuery.GetEntityPosition(context.GameContext.Boss.Id);
            int distance = context.BoardQuery.GetDistance(origin, target);
            var result = new List<WeaponData>(weapons.Count);
            foreach (var weapon in weapons)
                if (PlayableHunterCombatAdapter.IsWithinRange(weapon, distance))
                    result.Add(weapon);
            return result;
        }
    }
}
