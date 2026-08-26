using System;
using System.Collections.Generic;
using System.Threading;
using CardGame.ActionQueue;
using Core;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.Data;
using HuntingInDarkness.Hunt;

namespace HuntingInDarkness.ActionFlow.Hunt
{
    public interface IPlayableHuntRetreatInput
    {
        bool IsReturnCheckpointLocked { get; }
        HuntRetreatPreview GetRetreatPreview();
        UniTask<HuntRetreatCommandResult> RequestRetreatAsync(HuntRetreatDecision decision);
    }

    public readonly struct HuntRetreatDecision
    {
        public HuntRetreatDecision(string abandonedResourceId)
        {
            AbandonedResourceId = abandonedResourceId?.Trim() ?? string.Empty;
        }

        public string AbandonedResourceId { get; }
        public bool HasAbandonedResource => !string.IsNullOrWhiteSpace(AbandonedResourceId);
        public static HuntRetreatDecision None => new(string.Empty);
    }

    public readonly struct HuntRetreatMaterial
    {
        public HuntRetreatMaterial(string contentId, string displayName, int count)
        {
            ContentId = contentId ?? string.Empty;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? ContentId : displayName;
            Count = Math.Max(0, count);
        }

        public string ContentId { get; }
        public string DisplayName { get; }
        public int Count { get; }
    }

    public readonly struct HuntRetreatPreview
    {
        private HuntRetreatPreview(bool isAtCamp, IReadOnlyList<HuntRetreatMaterial> materials)
        {
            IsAtCamp = isAtCamp;
            Materials = materials ?? Array.Empty<HuntRetreatMaterial>();
        }

        public bool IsAtCamp { get; }
        public bool RequiresAbandonment => !IsAtCamp && Materials.Count > 0;
        public IReadOnlyList<HuntRetreatMaterial> Materials { get; }

        public static HuntRetreatPreview Create(HuntManager manager)
        {
            if (manager == null)
                return new HuntRetreatPreview(false, Array.Empty<HuntRetreatMaterial>());

            var counts = new Dictionary<string, HuntRetreatMaterial>(StringComparer.Ordinal);
            foreach (HunterInstance hunter in manager.ActiveHunters)
            {
                if (hunter?.Collectibles == null) continue;
                foreach (ItemInstance item in hunter.Collectibles)
                {
                    string contentId = item?.Data?.ContentId;
                    if (string.IsNullOrWhiteSpace(contentId) || item.Count <= 0) continue;
                    if (counts.TryGetValue(contentId, out HuntRetreatMaterial existing))
                    {
                        counts[contentId] = new HuntRetreatMaterial(existing.ContentId, existing.DisplayName, existing.Count + item.Count);
                        continue;
                    }
                    counts.Add(contentId, new HuntRetreatMaterial(contentId, item.Data.itemName, item.Count));
                }
            }

            var materials = new List<HuntRetreatMaterial>(counts.Values);
            materials.Sort((left, right) => string.CompareOrdinal(left.ContentId, right.ContentId));
            return new HuntRetreatPreview(manager.IsSquadAtCamp, materials.AsReadOnly());
        }

        public static HuntRetreatPreview Empty => new(false, Array.Empty<HuntRetreatMaterial>());
    }

    public readonly struct HuntRetreatCommandResult
    {
        private HuntRetreatCommandResult(bool succeeded, string reason, HuntRecord record)
        {
            Succeeded = succeeded;
            Reason = reason ?? string.Empty;
            Record = record;
        }

        public bool Succeeded { get; }
        public string Reason { get; }
        public HuntRecord Record { get; }

        public static HuntRetreatCommandResult Success(HuntRecord record) => new(true, string.Empty, record);
        public static HuntRetreatCommandResult Failed(string reason) => new(false, reason, null);
    }

    public struct HuntRetreatPreparedEvent
    {
        public int Year;
        public int HuntersDeployed;
        public int HuntersLost;
        public string[] CollectedResources;
    }

    /// <summary>在 Hunt Runner 内生成不可变更权威状态的回营快照；资源转移只在 Campaign 接受阶段切换后执行。</summary>
    public sealed class PrepareHuntRetreatAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly HuntManager manager;
        private readonly int currentYear;
        private readonly ActionEventOutbox eventOutbox;
        private readonly HuntRetreatDecision decision;

        public PrepareHuntRetreatAction(HuntManager manager, int currentYear, HuntRetreatDecision decision, ActionEventOutbox eventOutbox, IReactorEntity source, IReactorEntity target)
        {
            this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
            this.currentYear = currentYear;
            this.decision = decision;
            this.eventOutbox = eventOutbox ?? throw new ArgumentNullException(nameof(eventOutbox));
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Target = target ?? throw new ArgumentNullException(nameof(target));
        }

        public HuntRetreatCommandResult Result { get; private set; }
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }

        protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (currentYear < 1)
                return Fail("营地年份无效，无法结算本次狩猎。");

            HuntRecord record = manager.CreateHuntRecord(false, currentYear);
            if (!TryApplyAbandonment(record, out string reason))
                return Fail(reason);
            HuntRecord resultRecord = CloneRecord(record);
            Result = HuntRetreatCommandResult.Success(resultRecord);
            eventOutbox.StageAfterCommit(new HuntRetreatPreparedEvent
            {
                Year = record.Year,
                HuntersDeployed = record.HuntersDeployed,
                HuntersLost = record.HuntersLost,
                CollectedResources = record.CollectedResources.ToArray()
            });
            return UniTask.FromResult(ActionOutcome.Success());
        }

        private UniTask<ActionOutcome> Fail(string reason)
        {
            Result = HuntRetreatCommandResult.Failed(reason);
            return UniTask.FromResult(ActionOutcome.Failure(reason));
        }

        private bool TryApplyAbandonment(HuntRecord record, out string reason)
        {
            reason = string.Empty;
            if (manager.IsSquadAtCamp)
            {
                if (decision.HasAbandonedResource)
                    return FailAbandonment("小队已在营地，不能伪造放弃素材。", out reason);
                return true;
            }

            bool hasCollectibles = record.CollectedResources != null && record.CollectedResources.Count > 0;
            if (!hasCollectibles)
            {
                if (decision.HasAbandonedResource)
                    return FailAbandonment("本次狩猎没有可放弃的携带素材。", out reason);
                return true;
            }
            if (!decision.HasAbandonedResource)
                return FailAbandonment("远离营地且携带素材时，必须选择放弃一份素材。", out reason);
            if (!HasLiveCollectible(decision.AbandonedResourceId))
                return FailAbandonment("选择的放弃素材已不在当前小队携带物中。", out reason);
            if (!record.CollectedResources.Remove(decision.AbandonedResourceId))
                return FailAbandonment("选择的放弃素材不在本次回营快照中。", out reason);
            return true;
        }

        private bool HasLiveCollectible(string contentId)
        {
            foreach (HunterInstance hunter in manager.ActiveHunters)
            {
                if (hunter?.Collectibles == null) continue;
                foreach (ItemInstance item in hunter.Collectibles)
                    if (item?.Data != null && item.Data.ContentId == contentId && item.Count > 0)
                        return true;
            }
            return false;
        }

        private static bool FailAbandonment(string message, out string reason)
        {
            reason = message;
            return false;
        }

        private static HuntRecord CloneRecord(HuntRecord source)
        {
            return new HuntRecord
            {
                RecordId = source.RecordId,
                ReturnSchemaVersion = source.ReturnSchemaVersion,
                Year = source.Year,
                HuntersDeployed = source.HuntersDeployed,
                HuntersLost = source.HuntersLost,
                BossDefeated = source.BossDefeated,
                ParticipantHunterIds = source.ParticipantHunterIds != null ? new List<int>(source.ParticipantHunterIds) : new List<int>(),
                CollectedResources = source.CollectedResources != null ? new List<string>(source.CollectedResources) : new List<string>()
            };
        }
    }
}
