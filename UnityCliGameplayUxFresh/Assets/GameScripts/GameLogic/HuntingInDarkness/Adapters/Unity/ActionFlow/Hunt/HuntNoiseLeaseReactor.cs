using CardGame.ActionQueue;

namespace HuntingInDarkness.ActionFlow.Hunt
{
    internal sealed class HuntNoiseLeaseReactor : GameActionReactor<PrepareHuntNoiseAction>
    {
        private readonly int modifier;
        private readonly string leaseId;

        public HuntNoiseLeaseReactor(string leaseId, int modifier)
        {
            this.leaseId = leaseId ?? string.Empty;
            this.modifier = modifier;
        }

        public override ReactionTiming Timing => ReactionTiming.BeforeExecution;
        public override int Priority => -1000;
        public override string Key => $"hunt-noise-lease:{leaseId}";

        public override bool Matches(ReactionContext context)
        {
            return context?.Action is PrepareHuntNoiseAction;
        }

        protected override void React(PrepareHuntNoiseAction action, ReactionContext context, ReactionResponse response)
        {
            action.AddNoiseModifier(modifier);
        }
    }
}
