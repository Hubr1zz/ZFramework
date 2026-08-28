using Cards3D;
using Core;
using GameplayBase.Board;
using UnityEngine;

namespace HuntingInDarkness.ViewLayer.Flow
{
    /// <summary>世界空间桌面的空白点击取消选择；只发布表现输入事件，不进入玩法 ActionQueue。</summary>
    public sealed class BackgroundDeselectionInput3D : MonoBehaviour
    {
        private void Update()
        {
            if (!Input.GetMouseButtonDown(0) || UnityEngine.Camera.main == null) return;
            Ray ray = UnityEngine.Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (hit.collider.GetComponent<EntityClickHandler>() != null) return;
                if (hit.collider.GetComponentInParent<CardView3D>() != null) return;
            }
            EventBus.Publish(new CharacterDeselectedEvent());
        }
    }
}
