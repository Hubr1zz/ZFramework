using System;
using System.Collections.Generic;
using System.Threading;
using CardGame.ActionQueue;
using Core;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.Data;
using HuntingInDarkness.Settlement;

namespace HuntingInDarkness.ActionFlow.Settlement
{
    public readonly struct SettlementCraftCommandResult
    {
        public SettlementCraftCommandResult(bool succeeded, string reason, string recipeName, string outputName, int outputCount)
        {
            Succeeded = succeeded;
            Reason = reason ?? string.Empty;
            RecipeName = recipeName ?? string.Empty;
            OutputName = outputName ?? string.Empty;
            OutputCount = outputCount;
        }

        public bool Succeeded { get; }
        public string Reason { get; }
        public string RecipeName { get; }
        public string OutputName { get; }
        public int OutputCount { get; }

        public static SettlementCraftCommandResult Failed(string reason) => new(false, reason, string.Empty, string.Empty, 0);
    }

    public struct SettlementCraftedEvent
    {
        public string RecipeName;
        public string OutputName;
        public int OutputCount;
    }

    public sealed class CraftSettlementRecipeAction : CommandAction, ISourceAction, ITargetAction
    {
        private readonly SettlementInstance settlement;
        private readonly WorkshopSystem workshop;
        private readonly CraftRecipe recipe;
        private readonly ActionEventOutbox eventOutbox;

        public CraftSettlementRecipeAction(SettlementInstance settlement, WorkshopSystem workshop, CraftRecipe recipe, ActionEventOutbox eventOutbox, IReactorEntity source, IReactorEntity target)
        {
            this.settlement = settlement ?? throw new ArgumentNullException(nameof(settlement));
            this.workshop = workshop ?? throw new ArgumentNullException(nameof(workshop));
            this.recipe = recipe;
            this.eventOutbox = eventOutbox ?? throw new ArgumentNullException(nameof(eventOutbox));
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Target = target ?? throw new ArgumentNullException(nameof(target));
        }

        public SettlementCraftCommandResult Result { get; private set; }
        public IReactorEntity Source { get; }
        public IReactorEntity Target { get; }

        protected override UniTask<ActionOutcome> ExecuteAsync(ActionExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!workshop.AllRecipes.Contains(recipe)) return Fail("配方不属于当前营地。");
            if (!workshop.CanCraft(recipe, out string reason)) return Fail(reason);

            Dictionary<string, int> previousResources = CaptureIngredientAmounts();
            cancellationToken.ThrowIfCancellationRequested();
            List<ItemInstance> output = workshop.TryCraft(recipe);
            if (output.Count == 0) return Fail("制作提交失败。");

            foreach (KeyValuePair<string, int> previous in previousResources)
                eventOutbox.Stage(new ResourceChangedEvent { ResourceName = previous.Key, OldAmount = previous.Value, NewAmount = settlement.GetResource(previous.Key) });
            Result = new SettlementCraftCommandResult(true, string.Empty, recipe.recipeName, recipe.outputItem.itemName, output.Count);
            eventOutbox.Stage(new SettlementCraftedEvent { RecipeName = recipe.recipeName, OutputName = recipe.outputItem.itemName, OutputCount = output.Count });
            eventOutbox.Stage(new SettlementTransactionCommittedEvent { TransactionId = $"craft:{recipe.requiredWorkshopId}:{recipe.recipeName}", Kind = SettlementTransactionKind.Crafting });
            return UniTask.FromResult(ActionOutcome.Success());
        }

        private Dictionary<string, int> CaptureIngredientAmounts()
        {
            var amounts = new Dictionary<string, int>();
            foreach (RecipeIngredient ingredient in recipe.ingredients)
                if (ingredient?.item != null && !amounts.ContainsKey(ingredient.item.ContentId))
                    amounts.Add(ingredient.item.ContentId, settlement.GetResource(ingredient.item));
            return amounts;
        }

        private UniTask<ActionOutcome> Fail(string reason)
        {
            Result = SettlementCraftCommandResult.Failed(reason);
            return UniTask.FromResult(ActionOutcome.Failure(reason));
        }
    }
}
