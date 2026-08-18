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
            if (data == null) return;
            try
            {
                string savePath = SavePath;
                string json = JsonUtility.ToJson(data, prettyPrint: true);
                int saveVersion = Interlocked.Increment(ref nextSaveVersion);
                await UniTask.RunOnThreadPool(
                    () =>
                    {
                        lock (SaveGate)
                        {
                            if (saveVersion < lastWrittenSaveVersion) return;
                            System.IO.File.WriteAllText(savePath, json);
                            lastWrittenSaveVersion = saveVersion;
                        }
                    },
                    cancellationToken: cancellationToken);
                Debug.Log($"[SaveLoad] 存档成功 → {savePath}");
            }
            catch (System.OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Owning Unity object was destroyed; cancellation is expected during teardown.
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SaveLoad] 存档失败: {ex.Message}");
            }
        }

        // ─── 读档 ─────────────────────────────────────────────────

        public static async UniTask<SettlementInstance> LoadAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                string savePath = SavePath;
                string json = await UniTask.RunOnThreadPool(() =>
                    {
                        lock (SaveGate)
                        {
                            if (!System.IO.File.Exists(savePath)) return null;
                            return System.IO.File.ReadAllText(savePath);
                        }
                    },
                    cancellationToken: cancellationToken);
                if (json == null)
                {
                    Debug.Log("[SaveLoad] 无存档文件，返回 null");
                    return null;
                }
                await UniTask.SwitchToMainThread(cancellationToken);
                var data = JsonUtility.FromJson<SettlementInstance>(json);
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
                    return System.IO.File.Exists(savePath);
            }, cancellationToken: cancellationToken);
        }

        public static async UniTask DeleteSaveAsync(CancellationToken cancellationToken = default)
        {
            string savePath = SavePath;
            int deleteVersion = Interlocked.Increment(ref nextSaveVersion);
            bool deleted = await UniTask.RunOnThreadPool(() =>
            {
                lock (SaveGate)
                {
                    lastWrittenSaveVersion = System.Math.Max(lastWrittenSaveVersion, deleteVersion);
                    if (!System.IO.File.Exists(savePath)) return false;
                    System.IO.File.Delete(savePath);
                    return true;
                }
            }, cancellationToken: cancellationToken);

            if (deleted)
                Debug.Log("[SaveLoad] 存档已删除");
        }
    }
}
