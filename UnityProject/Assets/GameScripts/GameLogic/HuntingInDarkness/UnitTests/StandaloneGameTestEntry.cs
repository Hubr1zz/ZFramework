using UnityEngine;

namespace HuntingInDarkness.Testing
{
    /// <summary>
    /// 独立功能测试场景入口标记。
    /// 带有该组件的场景仍走 ZFramework 启动 Procedure，但不要求存在正式 GameManager。
    /// </summary>
    public abstract class StandaloneGameTestEntry : MonoBehaviour
    {
    }
}
