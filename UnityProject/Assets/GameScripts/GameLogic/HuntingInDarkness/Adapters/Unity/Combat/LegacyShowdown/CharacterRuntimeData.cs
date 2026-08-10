using System.Collections.Generic;
using Core;
using SO.Character;

namespace GameplayBase.CombatSystem
{
    /// <summary>
    /// 角色运行时数据。实现 ICharacterState 供外部只读查询。
    /// </summary>
    public class CharacterRuntimeData : ICharacterState
    {
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

        /// <summary>
        /// 返回角色当前持有的所有武器列表，供 IWeaponResolver 筛选。
        /// 当前仅有单一武器槽；未来扩展多槽时在此处添加即可。
        /// </summary>
        public List<WeaponData> GetAvailableWeapons()
        {
            var list = new List<WeaponData>();
            if (EquippedWeapon != null) list.Add(EquippedWeapon);
            return list;
        }

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
