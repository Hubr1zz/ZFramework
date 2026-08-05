using GameLogic;
using TEngine;
#pragma warning disable CS0436


/// <summary>
/// 游戏App。
/// </summary>
public partial class GameApp
{
    /// <summary>
    /// 游戏逻辑主入口。
    /// </summary>
    public static void Entrance()
    {
        GameEventHelper.Init();
        Log.Warning("======= Entrance GameApp =======");
        Utility.Unity.AddDestroyListener(Release);
        Log.Warning("======= StartGameLogic =======");
        StartGameLogic();
    }
    
    private static void StartGameLogic()
    {
        // GameEvent.Get<ILoginUI>().ShowLoginUI();
        GameModule.UI.ShowUIAsync<BattleMainUI>();
    }
    
    private static void Release()
    {
        SingletonSystem.Release();
        Log.Warning("======= Release GameApp =======");
    }
}
