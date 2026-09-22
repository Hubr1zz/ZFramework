using System.Threading;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.Hunt;
using UnityEngine;

namespace HuntingInDarkness.ActionFlow.Presentation
{
    public readonly struct HuntTileInteractionPresentationRequest
    {
        public HuntTileInteractionPresentationRequest(Vector2Int coordinate, HuntTileInteractionKind kind)
        {
            Coordinate = coordinate;
            Kind = kind;
        }

        public Vector2Int Coordinate { get; }
        public HuntTileInteractionKind Kind { get; }
    }

    /// <summary>ActionQueue 等待的狩猎地图表现端口；实现不得修改权威地图状态。</summary>
    public interface IHuntTileInteractionPresenter
    {
        UniTask PresentAsync(HuntTileInteractionPresentationRequest request, CancellationToken cancellationToken);
    }
}
