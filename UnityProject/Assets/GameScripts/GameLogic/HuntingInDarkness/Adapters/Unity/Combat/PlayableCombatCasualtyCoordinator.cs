using System;
using System.Collections.Generic;
using Core;
using GameplayBase;
using GameplayBase.CombatSystem;
using HuntingInDarkness.Data;
using HuntingInDarkness.Settlement;

namespace HuntingInDarkness.Combat
{
    /// <summary>把战斗死亡事件协调为营地永久死亡、装备返还和整队失败结算。</summary>
    public sealed class PlayableCombatCasualtyCoordinator : IDisposable
    {
        private readonly Dictionary<int, CasualtyBinding> bindings = new();
        private TimelineManager timeline;
        private IBoardCommand boardCommand;
        private HunterManagementSystem hunterManagement;
        private Action onPartyDefeated;
        private bool partyDefeatCompleted;

        public PlayableCombatCasualtyCoordinator()
        {
            EventBus.Subscribe<CharacterWoundedEvent>(OnCharacterWounded);
            EventBus.Subscribe<CharacterDiedEvent>(OnCharacterDied);
        }

        public void Bind(IReadOnlyList<HunterInstance> hunters, IReadOnlyList<CharacterRuntimeData> characters, IReadOnlyDictionary<int, UI.CharacterEntity> characterViews, TimelineManager combatTimeline, IBoardCommand combatBoardCommand, HunterManagementSystem combatHunterManagement, Action partyDefeated)
        {
            bindings.Clear();
            timeline = combatTimeline;
            boardCommand = combatBoardCommand;
            hunterManagement = combatHunterManagement;
            onPartyDefeated = partyDefeated;
            partyDefeatCompleted = false;
            if (hunters == null || characters == null) return;

            var activeHunters = new List<HunterInstance>();
            foreach (HunterInstance hunter in hunters)
                if (hunter != null && hunter.IsAlive)
                    activeHunters.Add(hunter);

            int count = Math.Min(activeHunters.Count, characters.Count);
            for (int index = 0; index < count; index++)
            {
                CharacterRuntimeData character = characters[index];
                if (character == null) continue;
                UI.CharacterEntity view = null;
                characterViews?.TryGetValue(character.Id, out view);
                bindings[character.Id] = new CasualtyBinding(activeHunters[index], character, view);
            }
        }

        public void Dispose()
        {
            EventBus.Unsubscribe<CharacterWoundedEvent>(OnCharacterWounded);
            EventBus.Unsubscribe<CharacterDiedEvent>(OnCharacterDied);
            bindings.Clear();
        }

        public HunterInstance GetHunter(int characterId) => bindings.TryGetValue(characterId, out CasualtyBinding binding) ? binding.Hunter : null;

        private void OnCharacterWounded(CharacterWoundedEvent evt)
        {
            if (!bindings.TryGetValue(evt.CharacterId, out CasualtyBinding binding)) return;
            PlayableHunterInjuryAdapter.Sync(binding.Hunter, binding.Character.CombatStats);
        }

        private void OnCharacterDied(CharacterDiedEvent evt)
        {
            if (!bindings.TryGetValue(evt.CharacterId, out CasualtyBinding binding) || !binding.Hunter.IsAlive) return;

            PlayableHunterInjuryAdapter.Sync(binding.Hunter, binding.Character.CombatStats);
            hunterManagement?.KillHunter(binding.Hunter);
            PlayableHunterCombatAdapter.DeactivateCharacter(binding.Character);
            timeline?.MarkCharacterDone(evt.CharacterId);
            boardCommand?.RemoveEntity(evt.CharacterId);
            if (binding.View != null)
                binding.View.gameObject.SetActive(false);
            if (HasLivingDeployedHunter() || partyDefeatCompleted) return;
            partyDefeatCompleted = true;
            onPartyDefeated?.Invoke();
        }

        private bool HasLivingDeployedHunter()
        {
            foreach (CasualtyBinding binding in bindings.Values)
                if (binding.Hunter.IsAlive)
                    return true;
            return false;
        }

        private sealed class CasualtyBinding
        {
            public HunterInstance Hunter { get; }
            public CharacterRuntimeData Character { get; }
            public UI.CharacterEntity View { get; }

            public CasualtyBinding(HunterInstance hunter, CharacterRuntimeData character, UI.CharacterEntity view)
            {
                Hunter = hunter;
                Character = character;
                View = view;
            }
        }
    }
}
