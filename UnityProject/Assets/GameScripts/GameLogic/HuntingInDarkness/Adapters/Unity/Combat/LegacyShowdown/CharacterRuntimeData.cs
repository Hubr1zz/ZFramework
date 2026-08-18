using System.Collections.Generic;
using Core;
using HuntingInDarkness.Combat;
using SO.Character;

namespace GameplayBase.CombatSystem
{
    /// <summary>
    /// 角色运行时数据。实现 ICharacterState 供外部只读查询。
    /// </summary>
    public class CharacterRuntimeData : ICharacterState
    {
        private readonly List<WeaponData> availableWeapons = new();

        public int Id { get; set; }
        public string Name { get; set; }
        public int CurrentTimePoints { get; set; }
        public int Willpower { get; set; }
        public int CombatInspiration { get; set; }
        public CharacterActionState ActionState { get; set; } = CharacterActionState.Idle;

        // ─── 战斗属性 ───
        public CharacterCombatStats CombatStats { get; set; } = new();

        // ─── 装备 ───
        public WeaponData EquippedWeapon { get; set; }
        public bool IsCombatActive { get; private set; } = true;

        /// <summary>
        /// 返回角色当前持有的所有武器列表，供 IWeaponResolver 筛选。
        /// 当前仅有单一武器槽；未来扩展多槽时在此处添加即可。
        /// </summary>
        public List<WeaponData> GetAvailableWeapons()
        {
            if (availableWeapons.Count > 0) return new List<WeaponData>(availableWeapons);

            var fallback = new List<WeaponData>();
            if (EquippedWeapon != null)
                fallback.Add(EquippedWeapon);
            return fallback;
        }

        public void SetAvailableWeapons(IReadOnlyList<WeaponData> weapons)
        {
            availableWeapons.Clear();
            if (weapons == null) return;
            foreach (WeaponData weapon in weapons)
                if (weapon != null)
                    availableWeapons.Add(weapon);
        }

        public void SetCombatActive(bool isActive) => IsCombatActive = isActive;

        // ─── 关联的 Character 实体 ───
        public Character CharacterEntity { get; set; }

        private readonly List<CharacterActionCardInstance> _hand = new();
        public IReadOnlyList<ICharacterActionCardInstanceState> Hand => _hand;

        // ─── 手牌管理 ───

        public void AddCard(CharacterActionCardInstance characterActionCard)
        {
            characterActionCard.OwnerCharacterId = Id;
            _hand.Add(characterActionCard);
        }

        public void RemoveCard(int cardInstanceId)
        {
            _hand.RemoveAll(c => c.InstanceId == cardInstanceId);
        }

        public CharacterActionCardInstance GetCardInstance(int cardInstanceId)
        {
            return _hand.Find(c => c.InstanceId == cardInstanceId);
        }

        public List<CharacterActionCardInstance> GetAllCardInstances() => _hand;
    }
}
