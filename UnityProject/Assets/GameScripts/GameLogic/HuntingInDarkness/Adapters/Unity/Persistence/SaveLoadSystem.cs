using HuntingInDarkness.Data;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Core
{
    /// <summary>
    /// 存档/读档系统。
    /// 使用 JsonUtility 将 SettlementInstance 序列化到 Application.persistentDataPath。
    ///
    /// 注意：SettlementInstance 中的 [System.NonSerialized] 字段（Equipment, Collectibles等）
    ///   不会被序列化；只保留 InstanceId 列表用于重建引用。
    /// </summary>
    public static class SaveLoadSystem
    {
        private const string SaveFileName = "settlement_save.json";
        private static readonly object SaveGate = new();
        private static int nextSaveVersion;
        private static int lastWrittenSaveVersion;

        private static string SavePath =>
            System.IO.Path.Combine(Application.persistentDataPath, SaveFileName);

        // ─── 存档 ─────────────────────────────────────────────────

        public static async UniTask SaveAsync(
            CampaignSnapshot snapshot,
            CancellationToken cancellationToken = default)
        {
            await TrySaveAsync(snapshot, cancellationToken);
        }

        public static async UniTask<bool> TrySaveAsync(CampaignSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            if (!TryCreatePayload(snapshot, out string payload, out string reason))
            {
                Debug.LogError($"[SaveLoad] 拒绝无效战役快照：{reason}");
                return false;
            }
            return await TrySavePayloadAsync(payload, cancellationToken);
        }

        public static async UniTask<bool> TrySavePayloadAsync(string payload, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(payload)) return false;
            try
            {
                string savePath = SavePath;
                int saveVersion = Interlocked.Increment(ref nextSaveVersion);
                string json = CampaignSaveCodec.Encode(payload);
                bool saved = await UniTask.RunOnThreadPool(() => TryWriteSnapshot(savePath, json, saveVersion), cancellationToken: cancellationToken);
                if (saved)
                    Debug.Log($"[SaveLoad] 存档成功 → {savePath}");
                return saved;
            }
            catch (System.OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Owning Unity object was destroyed; cancellation is expected during teardown.
                return false;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SaveLoad] 存档失败: {ex.Message}");
                return false;
            }
        }

        public static void SaveImmediate(CampaignSnapshot snapshot)
        {
            if (!TryCreatePayload(snapshot, out string payload, out string reason))
            {
                Debug.LogError($"[SaveLoad] 退出前拒绝无效战役快照：{reason}");
                return;
            }
            SavePayloadImmediate(payload);
        }

        public static void SavePayloadImmediate(string payload)
        {
            TrySavePayloadImmediate(payload);
        }

        public static bool TrySavePayloadImmediate(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload)) return false;
            try
            {
                string savePath = SavePath;
                int saveVersion = Interlocked.Increment(ref nextSaveVersion);
                string json = CampaignSaveCodec.Encode(payload);
                if (TryWriteSnapshot(savePath, json, saveVersion))
                {
                    Debug.Log($"[SaveLoad] 退出前存档成功 → {savePath}");
                    return true;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SaveLoad] 退出前存档失败: {ex.Message}");
            }
            return false;
        }

        private static bool TryWriteSnapshot(string savePath, string json, int saveVersion)
        {
            lock (SaveGate)
            {
                if (saveVersion < lastWrittenSaveVersion)
                    return true;
                if (!CampaignSaveFileStore.TryWrite(savePath, json, out string reason)) throw new System.IO.IOException(reason);
                lastWrittenSaveVersion = saveVersion;
                return true;
            }
        }

        // ─── 读档 ─────────────────────────────────────────────────

        public static async UniTask<CampaignSnapshot> LoadAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                string savePath = SavePath;
                CampaignSaveCandidates candidates = await UniTask.RunOnThreadPool(() =>
                    {
                        lock (SaveGate)
                            return CampaignSaveFileStore.ReadCandidates(savePath);
                    },
                    cancellationToken: cancellationToken);
                if (candidates.Primary == null && candidates.Backup == null)
                {
                    Debug.Log("[SaveLoad] 无存档文件，返回 null");
                    return null;
                }
                await UniTask.SwitchToMainThread(cancellationToken);
                if (!CampaignSaveRecovery.TryRestore(candidates, out CampaignSnapshot snapshot, out bool usedBackup, out string reason))
                {
                    Debug.LogError($"[SaveLoad] 主存档与备份均不可用。{reason}");
                    return null;
                }
                if (usedBackup)
                    Debug.LogWarning("[SaveLoad] 主存档损坏，已从上一份备份恢复。");
                Debug.Log($"[SaveLoad] 读档成功 ← {savePath}（年份 {snapshot?.Settlement?.CurrentYear}，阶段 {(snapshot?.HasActiveHunt == true ? "Hunt" : "Settlement")}）");
                return snapshot;
            }
            catch (System.OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return null;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SaveLoad] 读档失败: {ex.Message}");
                return null;
            }
        }

        // ─── 工具 ─────────────────────────────────────────────────

        public static UniTask<bool> HasSaveFileAsync(CancellationToken cancellationToken = default)
        {
            string savePath = SavePath;
            return UniTask.RunOnThreadPool(() =>
            {
                lock (SaveGate)
                    return CampaignSaveFileStore.HasAnyCandidate(savePath);
            }, cancellationToken: cancellationToken);
        }

        public static async UniTask<bool> TryDeleteSaveAsync(CancellationToken cancellationToken = default)
        {
            string savePath = SavePath;
            int deleteVersion = Interlocked.Increment(ref nextSaveVersion);
            lock (SaveGate)
                lastWrittenSaveVersion = System.Math.Max(lastWrittenSaveVersion, deleteVersion);
            bool deleted = await UniTask.RunOnThreadPool(() =>
            {
                lock (SaveGate)
                    return CampaignSaveFileStore.DeleteAll(savePath);
            }, cancellationToken: cancellationToken);

            if (deleted)
                Debug.Log("[SaveLoad] 存档已删除");
            lock (SaveGate)
                return !CampaignSaveFileStore.HasAnyCandidate(savePath);
        }

        public static bool TryCreatePayload(CampaignSnapshot snapshot, out string payload, out string reason)
        {
            payload = string.Empty;
            if (snapshot?.Settlement == null || snapshot.CampaignSchemaVersion <= 0 || snapshot.CampaignSchemaVersion > CampaignSnapshot.CurrentSchemaVersion)
            {
                reason = "战役快照版本或营地数据无效。";
                return false;
            }
            if (!snapshot.HasActiveHuntState && snapshot.ActiveHunt != null)
            {
                reason = "战役阶段标志与活动狩猎快照不一致。";
                return false;
            }
            if (snapshot.HasActiveHuntState)
            {
                ActiveHuntSnapshot active = snapshot.ActiveHunt;
                if (active == null || active.SchemaVersion != ActiveHuntSnapshot.CurrentSchemaVersion || string.IsNullOrWhiteSpace(active.ContentBundleId))
                {
                    reason = "活动狩猎快照版本或内容 Bundle 身份无效。";
                    return false;
                }
                if (snapshot.Settlement.PendingHuntReturn != null)
                {
                    reason = "活动狩猎与待结算回营记录不能同时保存。";
                    return false;
                }
            }
            payload = JsonUtility.ToJson(snapshot, prettyPrint: true);
            reason = string.Empty;
            return !string.IsNullOrWhiteSpace(payload);
        }

    }
}
