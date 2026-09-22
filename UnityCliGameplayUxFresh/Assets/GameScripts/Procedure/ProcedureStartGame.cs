using System;
using Cysharp.Threading.Tasks;
using Launcher;
using HuntingInDarkness.Bootstrap;
using ZFramework;

namespace Procedure
{
    public class ProcedureStartGame : ProcedureBase
    {
        public override bool UseNativeDialog { get; }

        protected override void OnEnter(IFsm<IProcedureModule> procedureOwner)
        {
            base.OnEnter(procedureOwner);
            StartGame().Forget();
        }

        private async UniTaskVoid StartGame()
        {
            await UniTask.Yield();
            PlayableContentSourcePrepareResult prepareResult = await PlayableContentSourceSystem.Instance.PrepareAsync();
            if (!prepareResult.Succeeded)
            {
                Log.Error("Hunting in Darkness content sources failed to prepare: {0}", prepareResult.Diagnostic);
                return;
            }
            GameApp.Entrance();
            if (GameApp.IsEntered)
                LauncherMgr.HideAllUI();
        }
    }
}
