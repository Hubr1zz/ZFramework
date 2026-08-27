using Core;
using GameLogic;
using HuntingInDarkness.Testing;
using HuntingInDarkness.Bootstrap;
using ZFramework;
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
#if UNITY_2023_1_OR_NEWER
        var manager = Object.FindAnyObjectByType<GameManager>();
#else
        var manager = Object.FindObjectOfType<GameManager>();
#endif
        if (manager != null) return true;

#if UNITY_2023_1_OR_NEWER
        var standaloneTest = Object.FindAnyObjectByType<StandaloneGameTestEntry>();
#else
        var standaloneTest = Object.FindObjectOfType<StandaloneGameTestEntry>();
#endif
        if (standaloneTest != null)
        {
            Log.Info("Standalone game test flow detected: {0}", standaloneTest.GetType().Name);
            return true;
        }

        if (PlayableGameBootstrap.EnsureInstalled()) return true;

        Log.Error("Hunting in Darkness startup aborted: the active scene is not the configured playable entry scene or its bootstrap settings are missing.");
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
