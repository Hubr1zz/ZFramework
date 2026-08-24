using System;
using System.Collections.Generic;
using HuntingInDarkness.Data;

namespace HuntingInDarkness.Settlement
{
    /// <summary>运行时事件工作项；持久数据仍只保存稳定 ID，不保存资产引用。</summary>
    public readonly struct SettlementEventWork
    {
        public SettlementEventWork(EventData gameEvent, AnnalEntry timelineEntry = null, SettlementEventChainOccurrence restoredOccurrence = null)
        {
            Event = gameEvent;
            TimelineEntry = timelineEntry;
            RestoredOccurrence = restoredOccurrence;
        }

        public EventData Event { get; }
        public AnnalEntry TimelineEntry { get; }
        public SettlementEventChainOccurrence RestoredOccurrence { get; }
    }

    /// <summary>
    /// 营地事件的稳定身份目录，以及年鉴旧身份的安全迁移入口。
    /// 事件资产名只作为一次性兼容别名，永不作为新的持久化身份。
    /// </summary>
    public static class PlayableSettlementEventRegistry
    {
        public const int CurrentIdentitySchemaVersion = 1;

        public static bool IsConfigured => PlayableSettlementContentRuntime.RegistryBundle.EventsConfigured;
        public static bool IsValid => PlayableSettlementContentRuntime.RegistryBundle.EventsValid;
        public static string Diagnostic => PlayableSettlementContentRuntime.RegistryBundle.EventDiagnostic;

        public static void Configure(IEnumerable<EventData> events) => PlayableSettlementContentRuntime.ConfigureLegacyEvents(events);

        public static bool TryResolveUnique(string identifier, out EventData gameEvent)
        {
            return PlayableSettlementContentRuntime.RegistryBundle.TryGetEvent(identifier, out gameEvent);
        }

        public static bool TryResolveCanonical(string identifier, out EventData gameEvent)
        {
            return PlayableSettlementContentRuntime.RegistryBundle.TryGetCanonicalEvent(identifier, out gameEvent);
        }

        public static bool MigratePersistentState(SettlementInstance settlement)
        {
            if (settlement == null) return false;
            if (settlement.TimelineEventIdentitySchemaVersion > CurrentIdentitySchemaVersion)
            {
                settlement.TimelineEventIdentityMigrationDiagnostic = $"营地事件身份 schema {settlement.TimelineEventIdentitySchemaVersion} 高于当前版本 {CurrentIdentitySchemaVersion}，已拒绝继续装配。";
                return false;
            }
            settlement.Timeline ??= new List<AnnalEntry>();
            bool changed = false;
            bool unresolvedEventEntry = false;
            foreach (AnnalEntry entry in settlement.Timeline)
            {
                if (!IsTimelineEventEntry(entry)) continue;
                if (!TryResolveUnique(entry.EventId, out EventData gameEvent))
                {
                    unresolvedEventEntry = true;
                    continue;
                }

                string canonicalId = gameEvent.ContentId;
                if (!string.Equals(entry.EventId, canonicalId, StringComparison.Ordinal))
                {
                    entry.EventId = canonicalId;
                    changed = true;
                }
                if (!string.IsNullOrWhiteSpace(gameEvent.eventName) && !string.Equals(entry.EventName, gameEvent.eventName, StringComparison.Ordinal))
                {
                    entry.EventName = gameEvent.eventName;
                    changed = true;
                }
            }

            if (unresolvedEventEntry)
            {
                settlement.TimelineEventIdentityMigrationDiagnostic = "部分营地事件年鉴条目的旧身份未知或存在歧义，已保留原值并等待内容目录补全。";
                return changed;
            }

            if (settlement.TimelineEventIdentitySchemaVersion < CurrentIdentitySchemaVersion)
            {
                settlement.TimelineEventIdentitySchemaVersion = CurrentIdentitySchemaVersion;
                settlement.TimelineEventIdentityMigrationDiagnostic = string.Empty;
                changed = true;
            }
            return changed;
        }

        public static bool IsTimelineEventEntry(AnnalEntry entry)
        {
            if (entry == null) return false;
            return entry.EntryType == TimelineEntryType.MainStory
                || entry.EntryType == TimelineEntryType.Random
                || entry.EntryType == TimelineEntryType.Scheduled;
        }

    }
}
