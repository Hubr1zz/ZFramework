using System.Threading;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.Data;

namespace Core
{
    /// <summary>战役组合根使用的持久化端口；具体存储由 Unity Adapter 提供。</summary>
    /// <remarks>实现必须在异步保存、即时保存与删除之间保持线性化或最后调用获胜，避免旧检查点晚到覆盖新状态。</remarks>
    public interface ICampaignPersistencePort
    {
        UniTask<bool> TrySavePayloadAsync(string payload, CancellationToken cancellationToken = default);
        bool TrySavePayloadImmediate(string payload);
        UniTask<bool> HasSaveAsync(CancellationToken cancellationToken = default);
        UniTask<CampaignSnapshot> LoadAsync(CancellationToken cancellationToken = default);
        UniTask<bool> TryDeleteAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>默认使用现有 SaveLoadSystem 文件存储实现持久化端口。</summary>
    public sealed class SaveLoadSystemCampaignPersistenceAdapter : ICampaignPersistencePort
    {
        public UniTask<bool> TrySavePayloadAsync(string payload, CancellationToken cancellationToken = default)
            => SaveLoadSystem.TrySavePayloadAsync(payload, cancellationToken);

        public bool TrySavePayloadImmediate(string payload)
            => SaveLoadSystem.TrySavePayloadImmediate(payload);

        public UniTask<bool> HasSaveAsync(CancellationToken cancellationToken = default)
            => SaveLoadSystem.HasSaveFileAsync(cancellationToken);

        public UniTask<CampaignSnapshot> LoadAsync(CancellationToken cancellationToken = default)
            => SaveLoadSystem.LoadAsync(cancellationToken);

        public UniTask<bool> TryDeleteAsync(CancellationToken cancellationToken = default)
            => SaveLoadSystem.TryDeleteSaveAsync(cancellationToken);
    }
}
