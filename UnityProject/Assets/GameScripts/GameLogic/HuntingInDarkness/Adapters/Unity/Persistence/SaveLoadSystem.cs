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
                string json = JsonUtility.ToJson(data, prettyPrint: true);
                await UniTask.RunOnThreadPool(
                    () => System.IO.File.WriteAllText(SavePath, json),
                    cancellationToken: cancellationToken);
                Debug.Log($"[SaveLoad] 存档成功 → {SavePath}");
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
                string json = await UniTask.RunOnThreadPool(() =>
                    {
                        if (!System.IO.File.Exists(SavePath)) return null;
                        return System.IO.File.ReadAllText(SavePath);
                    },
                    cancellationToken: cancellationToken);
                if (json == null)
                {
                    Debug.Log("[SaveLoad] 无存档文件，返回 null");
                    return null;
                }
                await UniTask.SwitchToMainThread(cancellationToken);
                var data = JsonUtility.FromJson<SettlementInstance>(json);
                Debug.Log($"[SaveLoad] 读档成功 ← {SavePath}（年份 {data?.CurrentYear}）");
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

        public static UniTask<bool> HasSaveFileAsync(CancellationToken cancellationToken = default) =>
            UniTask.RunOnThreadPool(
                () => System.IO.File.Exists(SavePath),
                cancellationToken: cancellationToken);

        public static async UniTask DeleteSaveAsync(CancellationToken cancellationToken = default)
        {
            bool deleted = await UniTask.RunOnThreadPool(() =>
            {
                if (!System.IO.File.Exists(SavePath)) return false;
                System.IO.File.Delete(SavePath);
                return true;
            }, cancellationToken: cancellationToken);

            if (deleted)
                Debug.Log("[SaveLoad] 存档已删除");
        }
    }
}
