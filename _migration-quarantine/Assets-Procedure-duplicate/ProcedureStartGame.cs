using System;
using Cysharp.Threading.Tasks;
using Launcher;
using TEngine;

namespace Procedure
{
    public class ProcedureStartGame : ProcedureBase
    {
        public override bool UseNativeDialog { get; }

        protected override void OnEnter(IFsm<IProcedureModule> procedureOwner)
        {
            base.OnEnter(procedureOwner);
            GameApp.Entrance();
            StartGame().Forget();
        }

        private async UniTaskVoid StartGame()
        {
            await UniTask.Yield();
            LauncherMgr.HideAllUI();
        }
    }
}
