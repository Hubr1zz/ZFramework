using System;
using UnityEngine;

namespace GameplayBase.Board
{
    /// <summary>
    /// 挂在实体棋子 GameObject 上，将 OnMouseDown 事件转发给 GameManager。
    /// </summary>
    public class EntityClickHandler : MonoBehaviour
    {
        public int EntityId;
        public Action<int> OnClicked;

        private void OnMouseDown() => OnClicked?.Invoke(EntityId);
    }
}
