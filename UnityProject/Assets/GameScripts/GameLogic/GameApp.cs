using Core;
using GameLogic;
using TEngine;
using UnityEngine;

/// <summary>
/// 游戏App。
/// </summary>
public partial class GameApp
{
    private static bool _entered;

    /// <summary>
    /// 游戏逻辑主入口。
    /// </summary>
    public static void Entrance()
    {
        if (_entered) return;
        GameEventHelper.Init();
        Log.Info("======= Entrance Hunting in Darkness =======");
        if (!StartGameLogic()) return;

        _entered = true;
        Utility.Unity.AddDestroyListener(Release);
    }
    
    private static bool StartGameLogic()
    {
        var manager = Object.FindObjectOfType<GameManager>();
        if (manager != null) return true;

        Log.Error("Hunting in Darkness startup aborted: no configured GameManager exists in the active scene. " +
                  "Add the migrated GameManager to the bootstrap scene and assign its content ScriptableObjects.");
        return false;
    }
    
    private static void Release()
    {
        EventBus.Clear();
        SingletonSystem.Release();
        GameModule.Shutdown();
        _entered = false;
        Log.Info("======= Release Hunting in Darkness =======");
    }
}
