using TEngine;
using ProcedureOwner = TEngine.IFsm<TEngine.IProcedureModule>;

namespace Procedure
{
    /// <summary>
    /// 可选内容包初始化入口。
    /// </summary>
    /// <remarks>
    /// 默认资源包完成初始化和更新后进入此流程。未来可在这里扫描 DLC/Mod 清单、
    /// 校验兼容性，并通过 IResourceModule.InitPackage 初始化独立的 YooAsset Package。
    /// 当前没有外部内容包，因此直接进入预加载流程。
    /// </remarks>
    public sealed class ProcedureInitContentPackages : ProcedureBase
    {
        public override bool UseNativeDialog => true;

        protected override void OnEnter(ProcedureOwner procedureOwner)
        {
            base.OnEnter(procedureOwner);
            Log.Info("Optional content package initialization complete.");
            ChangeState<ProcedurePreload>(procedureOwner);
        }
    }
}
