using System;
using System.Collections.Generic;
using HuntingInDarkness.Data;
using UnityEngine;

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

        private static readonly Dictionary<string, HashSet<EventData>> ownersByIdentifier = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, HashSet<EventData>> ownersByCanonicalId = new(StringComparer.Ordinal);
        private static readonly List<EventData> registeredEvents = new();
        public static bool IsConfigured { get; private set; }
        public static bool IsValid { get; private set; }
        public static string Diagnostic { get; private set; } = string.Empty;

        internal readonly struct RuntimeState
        {
            public RuntimeState(bool isConfigured, IReadOnlyList<EventData> events)
            {
                IsConfigured = isConfigured;
                Events = events;
            }

            public bool IsConfigured { get; }
            public IReadOnlyList<EventData> Events { get; }
        }

        internal static RuntimeState CaptureState() => new(IsConfigured, new List<EventData>(registeredEvents));

        internal static void RestoreState(RuntimeState state)
        {
            ResetRuntimeState();
            if (state.IsConfigured)
                Configure(state.Events);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            ownersByIdentifier.Clear();
            ownersByCanonicalId.Clear();
            registeredEvents.Clear();
            IsConfigured = false;
            IsValid = false;
            Diagnostic = string.Empty;
        }

        public static void Configure(IEnumerable<EventData> events)
        {
            ownersByIdentifier.Clear();
            ownersByCanonicalId.Clear();
            registeredEvents.Clear();
            IsConfigured = true;
            IsValid = true;
            Diagnostic = string.Empty;
            if (events == null) return;

            foreach (EventData gameEvent in events)
            {
                if (gameEvent == null) continue;
                registeredEvents.Add(gameEvent);
                if (!gameEvent.HasExplicitContentId)
                {
                    IsValid = false;
                    Diagnostic = $"营地事件缺少显式稳定 ContentId：{gameEvent.name}";
                    continue;
                }
                AddOwner(gameEvent.ContentId, gameEvent, ownersByCanonicalId);
                AddOwner(gameEvent.ContentId, gameEvent);
                AddOwner(gameEvent.name, gameEvent);
            }

            foreach (KeyValuePair<string, HashSet<EventData>> pair in ownersByIdentifier)
                if (pair.Value.Count > 1)
                {
                    IsValid = false;
                    Diagnostic = $"事件稳定身份或资产名别名冲突：{pair.Key}";
                    break;
                }
        }

        public static bool TryResolveUnique(string identifier, out EventData gameEvent)
        {
            return TryResolve(identifier, ownersByIdentifier, out gameEvent);
        }

        public static bool TryResolveCanonical(string identifier, out EventData gameEvent)
        {
            return TryResolve(identifier, ownersByCanonicalId, out gameEvent);
        }

        private static bool TryResolve(string identifier, IReadOnlyDictionary<string, HashSet<EventData>> ownersByKey, out EventData gameEvent)
        {
            gameEvent = null;
            string key = identifier?.Trim() ?? string.Empty;
            if (key.Length == 0 || !ownersByKey.TryGetValue(key, out HashSet<EventData> owners) || owners.Count != 1) return false;
            foreach (EventData owner in owners)
            {
                if (owner == null || string.IsNullOrWhiteSpace(owner.ContentId)) return false;
                gameEvent = owner;
                return true;
            }
            return false;
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

        private static void AddOwner(string identifier, EventData gameEvent)
        {
            string key = identifier?.Trim() ?? string.Empty;
            if (key.Length == 0) return;
            AddOwner(key, gameEvent, ownersByIdentifier);
        }

        private static void AddOwner(string identifier, EventData gameEvent, Dictionary<string, HashSet<EventData>> ownersByKey)
        {
            string key = identifier?.Trim() ?? string.Empty;
            if (key.Length == 0) return;
            if (!ownersByKey.TryGetValue(key, out HashSet<EventData> owners))
            {
                owners = new HashSet<EventData>();
                ownersByKey.Add(key, owners);
            }
            owners.Add(gameEvent);
        }
    }
}
