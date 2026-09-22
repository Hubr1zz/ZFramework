using System;
using System.Collections.Generic;
using System.Threading;
using CardTactics.CombatSystem;
using Cysharp.Threading.Tasks;
using Config;
using GameplayBase;
using GameplayBase.CombatSystem;
using HuntingInDarkness.Combat;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Cards;
using HuntingInDarkness.GameCore.Combat;
using SO.Boss.ActionCard;
using SO.Boss.HitLocation;
using UnityEngine;
using HuntingInDarkness.Settlement;

namespace Core
{
    internal sealed class PlayableShowdownPhaseManager : IDisposable, IPlayableShowdownPhasePort, IPlayableShowdownGameplayPort
    {
        private PlayableCombatSession current;
        private bool disposed;

        internal PlayableCombatSession Current => current;

        PlayableCombatSession IPlayableShowdownPhasePort.Current => Current;
        IPlayableShowdownGameplayPort IPlayableShowdownPhasePort.Gameplay => this;
        CombatManager IPlayableShowdownGameplayPort.CombatManager => current?.CombatManager;
        TurnPhase IPlayableShowdownGameplayPort.CurrentPhase => current?.CurrentPhase ?? TurnPhase.PlayerTurn;
        int IPlayableShowdownGameplayPort.CurrentTurnNumber => current?.CurrentTurnNumber ?? 0;
        IReadOnlyList<ICharacterState> IPlayableShowdownGameplayPort.PlayerCharacters => current?.PlayerCharacters ?? Array.Empty<ICharacterState>();
        IBossState IPlayableShowdownGameplayPort.Boss => current?.Boss;
        IReadOnlyList<HitLocationRuntimeState> IPlayableShowdownGameplayPort.BossHitLocationStates => current?.BossHitLocationStates ?? Array.Empty<HitLocationRuntimeState>();
        IReadOnlyList<BossActionCardData> IPlayableShowdownGameplayPort.BossRevealedCards => current?.BossRevealedCards ?? Array.Empty<BossActionCardData>();
        Character IPlayableShowdownGameplayPort.GetCharacter(int characterId) => current?.GetCharacter(characterId);
        CharacterRuntimeData IPlayableShowdownGameplayPort.GetCharacterData(int characterId) => current?.GetCharacterData(characterId);
        IReadOnlyList<ICharacterActionCardInstanceState> IPlayableShowdownGameplayPort.GetCardsOf(int characterId) => current?.GetCardsOf(characterId) ?? Array.Empty<ICharacterActionCardInstanceState>();
        ICharacterActionCardInstanceState IPlayableShowdownGameplayPort.GetCard(int cardInstanceId) => current?.GetCard(cardInstanceId);
        Vector3 IPlayableShowdownGameplayPort.GetEntityWorldPosition(int entityId) => current?.GetEntityWorldPosition(entityId) ?? Vector3.zero;
        void IPlayableShowdownGameplayPort.SelectCharacter(int characterId) => current?.OnSelectCharacter(characterId);
        void IPlayableShowdownGameplayPort.PlayCard(int cardInstanceId, int targetEntityId) => current?.OnPlayCard(cardInstanceId, targetEntityId);
        void IPlayableShowdownGameplayPort.RestoreCard(int cardInstanceId) => current?.OnRestoreCard(cardInstanceId);
        void IPlayableShowdownGameplayPort.DiscardCard(int cardInstanceId) => current?.OnDiscardCard(cardInstanceId);
        void IPlayableShowdownGameplayPort.EndTurn() => current?.OnEndTurn();
        bool IPlayableShowdownGameplayPort.AssistOvertimeCharacter(int helperId, int targetId) => current?.TryAssistOvertimeCharacter(helperId, targetId) == true;
        int IPlayableShowdownGameplayPort.AddInspiration(int characterId, int amount) => current?.AddCombatInspiration(characterId, amount) ?? 0;
        UniTask<InspirationGain> IPlayableShowdownGameplayPort.AddInspirationAsync(int characterId, CombatInspirationColor color, CancellationToken cancellationToken) => current != null ? current.AddCombatInspirationAsync(characterId, color, cancellationToken) : UniTask.FromResult(new InspirationGain(InspirationGainResult.Rejected, default));
        IReadOnlyList<CombatInspirationToken> IPlayableShowdownGameplayPort.GetInspirationTokens(int characterId) => current?.GetCombatInspirationTokens(characterId) ?? Array.Empty<CombatInspirationToken>();
        int IPlayableShowdownGameplayPort.GetInspirationCapacity(int characterId) => current?.GetCombatInspirationCapacity(characterId) ?? 0;
        bool IPlayableShowdownGameplayPort.RelieveOvertimeCharacter(int targetId) => current?.TryRelieveOvertimeCharacter(targetId) == true;
        TimelineActionStatus IPlayableShowdownGameplayPort.GetTimelineStatus(int characterId) => current?.GetTimelineStatus(characterId) ?? TimelineActionStatus.Done;
        bool IPlayableShowdownPhasePort.TryPrepare(PlayableCombatSessionConfiguration configuration, out string reason) => TryPrepare(configuration, out reason);
        void IPlayableShowdownPhasePort.Start(IReadOnlyList<HunterInstance> hunters, HunterManagementSystem hunterManagement, Action onPartyDefeated) => Start(hunters, hunterManagement, onPartyDefeated);
        void IPlayableShowdownPhasePort.Update() => Update();
        void IPlayableShowdownPhasePort.DisposeCurrent() => DisposeCurrent();

        internal bool TryPrepare(PlayableCombatSessionConfiguration configuration, out string reason)
        {
            ThrowIfDisposed();
            if (current?.IsActive == true)
            {
                reason = string.Empty;
                return true;
            }

            current = null;
            try
            {
                current = new PlayableCombatSession(configuration);
                current.PublishReady();
                reason = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                current?.Dispose();
                current = null;
                reason = $"决战运行态初始化异常：{exception.Message}";
                return false;
            }
        }

        internal void Start(IReadOnlyList<HunterInstance> hunters, HunterManagementSystem hunterManagement, Action onPartyDefeated)
        {
            ThrowIfDisposed();
            current?.Start(hunters, hunterManagement, onPartyDefeated);
        }

        internal void Update()
        {
            if (disposed) return;
            current?.Update();
        }

        internal void DisposeCurrent()
        {
            if (disposed) return;
            current?.Dispose();
            current = null;
        }

        internal void ResetCurrent() => DisposeCurrent();

        public void Dispose()
        {
            if (disposed) return;
            DisposeCurrent();
            disposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(PlayableShowdownPhaseManager));
        }
    }
}
