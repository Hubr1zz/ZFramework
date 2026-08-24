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
            SettlementInstance data,
            CancellationToken cancellationToken = default)
        {
            await TrySaveAsync(data, cancellationToken);
        }

        public static async UniTask<bool> TrySaveAsync(SettlementInstance data, CancellationToken cancellationToken = default)
        {
            if (data == null)
                return false;
            try
            {
                string savePath = SavePath;
                int saveVersion = Interlocked.Increment(ref nextSaveVersion);
                string json = CampaignSaveCodec.Encode(JsonUtility.ToJson(data, prettyPrint: true));
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

        public static void SaveImmediate(SettlementInstance data)
        {
            if (data == null)
                return;
            try
            {
                string savePath = SavePath;
                int saveVersion = Interlocked.Increment(ref nextSaveVersion);
                string json = CampaignSaveCodec.Encode(JsonUtility.ToJson(data, prettyPrint: true));
                if (TryWriteSnapshot(savePath, json, saveVersion))
                    Debug.Log($"[SaveLoad] 退出前存档成功 → {savePath}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SaveLoad] 退出前存档失败: {ex.Message}");
            }
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

        public static async UniTask<SettlementInstance> LoadAsync(
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
                if (!CampaignSaveRecovery.TryRestore(candidates, out SettlementInstance data, out bool usedBackup, out string reason))
                {
                    Debug.LogError($"[SaveLoad] 主存档与备份均不可用。{reason}");
                    return null;
                }
                if (usedBackup)
                    Debug.LogWarning("[SaveLoad] 主存档损坏，已从上一份备份恢复。");
                Debug.Log($"[SaveLoad] 读档成功 ← {savePath}（年份 {data?.CurrentYear}）");
                return data;
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

        public static async UniTask DeleteSaveAsync(CancellationToken cancellationToken = default)
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
        }

    }
}
