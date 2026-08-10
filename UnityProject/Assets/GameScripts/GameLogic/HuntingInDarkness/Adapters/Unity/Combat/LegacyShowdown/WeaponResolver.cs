using System;
using System.Collections.Generic;
using Core;
using Cysharp.Threading.Tasks;
using SO.Character;
using UnityEngine;

namespace GameplayBase.CombatSystem
{
    // ═══════════════════════════════════════════
    // 武器解析策略接口
    // ═══════════════════════════════════════════

    /// <summary>
    /// 决定攻击行动卡使用哪把武器的策略接口。
    /// 平行于 ITargetSelector 的设计：可替换、可测试、支持异步（玩家选择武器时需要等待 UI）。
    /// </summary>
    public interface IWeaponResolver
    {
        UniTask<WeaponData> ResolveAsync(ActionCardContext context, IPlayerInputProvider input);
    }

    // ═══════════════════════════════════════════
    // 默认：使用角色当前装备的武器
    // ═══════════════════════════════════════════

    /// <summary>
    /// 直接读取 CharacterRuntimeData.EquippedWeapon，不需要玩家额外操作。
    /// 适用于卡牌与武器一对一绑定、或角色只有单一武器槽的情况。
    /// </summary>
    public class EquippedWeaponResolver : IWeaponResolver
    {
        public UniTask<WeaponData> ResolveAsync(ActionCardContext context, IPlayerInputProvider input)
        {
            if (context.GameContext is GameManager gm)
            {
                var data = gm.GetCharacterData(context.SourceCharacterId);
                return UniTask.FromResult(data?.EquippedWeapon);
            }
            Debug.LogWarning("[EquippedWeaponResolver] GameContext 不是 GameManager，无法获取武器");
            return UniTask.FromResult<WeaponData>(null);
        }
    }

    // ═══════════════════════════════════════════
    // 玩家主动选择：从装备栏选择符合条件的武器
    // ═══════════════════════════════════════════

    /// <summary>
    /// 弹出武器选择 UI，等待玩家点击装备栏中符合筛选条件的武器后继续流程。
    /// 候选武器来自 CharacterRuntimeData.GetAvailableWeapons()，可附加过滤谓词。
    /// 只有一件候选武器时直接使用，不显示 UI。
    /// </summary>
    public class PlayerSelectWeaponResolver : IWeaponResolver
    {
        private readonly string _prompt;
        private readonly Func<WeaponData, bool> _filter;

        /// <param name="prompt">显示给玩家的提示文字</param>
        /// <param name="filter">候选武器的过滤条件，null 表示不过滤</param>
        public PlayerSelectWeaponResolver(string prompt = "选择武器", Func<WeaponData, bool> filter = null)
        {
            _prompt = prompt;
            _filter = filter;
        }

        public async UniTask<WeaponData> ResolveAsync(ActionCardContext context, IPlayerInputProvider input)
        {
            var candidates = GetCandidates(context);

            if (candidates.Count == 0)
            {
                Debug.LogWarning("[PlayerSelectWeaponResolver] 没有符合条件的武器可供选择");
                return null;
            }

            if (candidates.Count == 1)
                return candidates[0];

            return await input.RequestSelectWeapon(_prompt, candidates);
        }

        private List<WeaponData> GetCandidates(ActionCardContext context)
        {
            if (context.GameContext is not GameManager gm) return new();
            var data = gm.GetCharacterData(context.SourceCharacterId);
            if (data == null) return new();

            var available = data.GetAvailableWeapons();
            return _filter != null ? available.FindAll(w => _filter(w)) : available;
        }
    }
}