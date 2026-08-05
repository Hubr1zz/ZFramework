# UI 与事件

UI 业务位于 `Assets/GameScripts/HotFix/GameLogic/UI/`，框架实现位于 `Module/UIModule/`。窗口继承 `UIWindow`，复用子区域继承 `UIWidget`，通过 `GameModule.UI` 打开和关闭。

模块间广播使用静态 `GameEvent`；窗口内部需要随生命周期自动清理的监听使用 `AddUIEvent`。`GameApp.Entrance()` 必须在首次接口事件之前调用 `GameEventHelper.Init()`。

UI 中的异步资源加载必须考虑窗口关闭后的取消与释放，避免回调持有已销毁对象。
