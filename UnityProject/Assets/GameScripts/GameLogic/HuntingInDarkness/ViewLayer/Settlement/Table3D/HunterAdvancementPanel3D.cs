using System;
using System.Collections.Generic;
using Cards3D;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.ActionFlow.Settlement;
using HuntingInDarkness.Combat;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Settlement;
using TMPro;
using UnityEngine;

namespace UI
{
    public sealed class HunterAdvancementPanel3D : WorldSpaceViewPanel
    {
        private const int TrainingCardsPerPage = 5;
        private readonly List<HunterGrowthChoiceCard3D> growthCards = new();
        private readonly List<WeaponTrainingCard3D> trainingCards = new();
        private readonly List<WeaponMasteryFamilyDefinition> families = new();
        private TextMeshPro summaryText;
        private TextMeshPro trainingTitleText;
        private TextMeshPro statusText;
        private HunterInstance hunter;
        private SettlementInstance settlement;
        private PlayableWeaponMasteryCatalog catalog;
        private Func<int, HunterGrowthChoice, UniTask<HunterGrowthCommandResult>> growthCommand;
        private Func<int, string, UniTask<WeaponTrainingCommandResult>> trainingCommand;
        private int trainingPage;
        private bool isBuilt;
        private bool isSubmitting;

        public static HunterAdvancementPanel3D Create(Transform parent)
        {
            var gameObject = new GameObject("HunterAdvancementPanel3D");
            gameObject.transform.SetParent(parent, false);
            var panel = gameObject.AddComponent<HunterAdvancementPanel3D>();
            panel.EnsureBuilt();
            panel.Hide();
            return panel;
        }

        private void Awake() => EnsureBuilt();

        public void EnsureBuilt()
        {
            if (isBuilt) return;
            isBuilt = true;
            BuildBase();
            SetSize(6.4f, 4.6f);
            summaryText = BuildText("Summary", new Vector3(0f, 0.015f, 1.78f), 0.08f, new Vector2(5.5f, 0.32f));
            trainingTitleText = BuildText("TrainingTitle", new Vector3(0f, 0.015f, 0.36f), 0.085f, new Vector2(5.4f, 0.26f));
            statusText = BuildText("Status", new Vector3(0f, 0.015f, -1.82f), 0.07f, new Vector2(5.4f, 0.34f));
            BuildButton("PreviousPage", "上一页", new Vector3(-2.82f, 0.03f, -0.58f), new Vector3(0.48f, 0.04f, 0.30f), PreviousPage, new Color(0.20f, 0.24f, 0.30f));
            BuildButton("NextPage", "下一页", new Vector3(2.82f, 0.03f, -0.58f), new Vector3(0.48f, 0.04f, 0.30f), NextPage, new Color(0.20f, 0.24f, 0.30f));
            BuildButton("Close", "关闭", new Vector3(2.75f, 0.03f, 2.06f), new Vector3(0.5f, 0.04f, 0.22f), Hide, new Color(0.40f, 0.14f, 0.13f));
        }

        public void Open(HunterInstance selectedHunter, SettlementInstance settlementData, PlayableWeaponMasteryCatalog masteryCatalog, Func<int, HunterGrowthChoice, UniTask<HunterGrowthCommandResult>> onGrowth, Func<int, string, UniTask<WeaponTrainingCommandResult>> onTraining, Vector3 worldPosition)
        {
            if (selectedHunter == null || settlementData == null) return;
            hunter = selectedHunter;
            settlement = settlementData;
            catalog = masteryCatalog;
            growthCommand = onGrowth;
            trainingCommand = onTraining;
            trainingPage = 0;
            isSubmitting = false;
            Rebuild();
            ShowAt(worldPosition);
        }

        public void RefreshVisible()
        {
            if (!gameObject.activeSelf || hunter == null || settlement == null || isSubmitting) return;
            if (!ReferenceEquals(settlement.GetHunter(hunter.InstanceId), hunter) || !hunter.IsAvailable)
            {
                Hide();
                return;
            }
            Rebuild();
        }

        private void Rebuild()
        {
            ClearCards();
            Title.text = $"{hunter.Name} · 成长与训练";
            summaryText.text = $"年龄 {hunter.Age}　待分配 {hunter.UnspentGrowth}　胆识 {hunter.Courage}/{HunterAdvancementRules.MaximumGrowthAttribute}　知识 {hunter.Understanding}/{HunterAdvancementRules.MaximumGrowthAttribute}";
            BuildGrowthCards();
            BuildTrainingCards();
            statusText.text = hunter.UnspentGrowth > 0 ? "选择胆识或知识分配成长；武器训练会消耗配置资源。" : "暂无待分配成长；仍可进行已解锁的武器训练。";
        }

        private void BuildGrowthCards()
        {
            HunterGrowthChoiceCard3D courage = HunterGrowthChoiceCard3D.Create(hunter, HunterGrowthChoice.Courage, ContentRoot, new Vector3(-0.48f, 0f, 1.02f));
            HunterGrowthChoiceCard3D understanding = HunterGrowthChoiceCard3D.Create(hunter, HunterGrowthChoice.Understanding, ContentRoot, new Vector3(0.48f, 0f, 1.02f));
            ConfigureGrowthCard(courage);
            ConfigureGrowthCard(understanding);
            growthCards.Add(courage);
            growthCards.Add(understanding);
        }

        private void ConfigureGrowthCard(HunterGrowthChoiceCard3D card)
        {
            card.Requested = RequestGrowth;
            bool available = HunterAdvancementRules.CanSpendGrowth(hunter, card.Choice, out string reason);
            card.ConfigureState(!isSubmitting && available, isSubmitting ? "处理中" : reason);
        }

        private void BuildTrainingCards()
        {
            families.Clear();
            if (catalog != null) families.AddRange(catalog.GetFamilies());
            string cost = FormatTrainingCost();
            trainingTitleText.text = families.Count == 0 ? "武器训练 · 内容尚未配置" : $"武器训练 · {cost} · 每次 +{catalog.TrainingExperience}";
            int pageCount = Mathf.Max(1, Mathf.CeilToInt((float)families.Count / TrainingCardsPerPage));
            trainingPage = Mathf.Clamp(trainingPage, 0, pageCount - 1);
            int startIndex = trainingPage * TrainingCardsPerPage;
            int endIndex = Mathf.Min(startIndex + TrainingCardsPerPage, families.Count);
            float spacing = CardView3D.CW + 0.18f;
            float startX = -(endIndex - startIndex - 1) * spacing * 0.5f;
            for (int index = startIndex; index < endIndex; index++)
            {
                WeaponMasteryFamilyDefinition family = families[index];
                WeaponTrainingCard3D card = WeaponTrainingCard3D.Create(family, ContentRoot, new Vector3(startX + (index - startIndex) * spacing, 0f, -0.58f));
                card.Requested = RequestTraining;
                bool available = CanTrain(family, out string reason);
                card.ConfigureState(GetMasteryValue(family.Id), catalog.TrainingExperience, cost, !isSubmitting && available, isSubmitting ? "处理中" : reason);
                trainingCards.Add(card);
            }
        }

        private void RequestGrowth(HunterGrowthChoiceCard3D card)
        {
            if (card == null || isSubmitting) return;
            SpendGrowthAsync(card.Choice).Forget();
        }

        private async UniTaskVoid SpendGrowthAsync(HunterGrowthChoice choice)
        {
            if (growthCommand == null)
            {
                statusText.text = "成长命令尚未接入。";
                return;
            }
            isSubmitting = true;
            SetCardsEnabled(false);
            try
            {
                HunterGrowthCommandResult result = await growthCommand.Invoke(hunter.InstanceId, choice);
                if (this == null) return;
                isSubmitting = false;
                Rebuild();
                statusText.text = result.Succeeded ? $"{GetChoiceName(result.Choice)} {result.PreviousValue} → {result.CurrentValue}，剩余 {result.RemainingGrowth} 点。{FormatMilestones(result.Milestones)}" : result.Reason;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (this == null) return;
                isSubmitting = false;
                Rebuild();
                statusText.text = "成长命令执行异常，请重试。";
            }
        }

        private void RequestTraining(WeaponTrainingCard3D card)
        {
            if (card?.Family == null || isSubmitting) return;
            TrainAsync(card.Family.Id).Forget();
        }

        private async UniTaskVoid TrainAsync(string masteryId)
        {
            if (trainingCommand == null)
            {
                statusText.text = "训练命令尚未接入。";
                return;
            }
            isSubmitting = true;
            SetCardsEnabled(false);
            try
            {
                WeaponTrainingCommandResult result = await trainingCommand.Invoke(hunter.InstanceId, masteryId);
                if (this == null) return;
                isSubmitting = false;
                Rebuild();
                statusText.text = result.Success ? $"{result.MasteryOutcome.MasteryName} {result.MasteryOutcome.OldValue} → {result.MasteryOutcome.NewValue}。{FormatMasteryMilestones(result.MasteryOutcome.ReachedMilestoneNames)}" : result.Reason;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (this == null) return;
                isSubmitting = false;
                Rebuild();
                statusText.text = "训练命令执行异常，请重试。";
            }
        }

        private bool CanTrain(WeaponMasteryFamilyDefinition family, out string reason)
        {
            if (catalog == null || family == null || catalog.TrainingCostItem == null)
            {
                reason = "训练配置无效";
                return false;
            }
            if (!WeaponMasteryRules.CanIncrease(hunter, family.Id))
            {
                reason = "熟练度已达到上限";
                return false;
            }
            return WeaponTrainingRules.CanTrain(hunter.IsAvailable && !hunter.IsDead, settlement.IsInventionUnlocked(catalog.TrainingInventionId), settlement.GetResource(catalog.TrainingCostItem), catalog.TrainingCost, family.Id, catalog.TrainingExperience, out reason);
        }

        private int GetMasteryValue(string masteryId)
        {
            if (hunter.WeaponMasteries != null)
                foreach (WeaponMasteryState mastery in hunter.WeaponMasteries)
                    if (mastery != null && string.Equals(mastery.MasteryId, masteryId, StringComparison.Ordinal)) return Mathf.Max(0, mastery.Experience);
            return hunter.WeaponMasteries == null || hunter.WeaponMasteries.Count == 0 ? Mathf.Max(0, hunter.WeaponProficiency) : 0;
        }

        private string FormatTrainingCost()
        {
            if (catalog == null) return "训练内容未配置";
            if (catalog.TrainingCost == 0) return "无需物资";
            return catalog.TrainingCostItem != null ? $"{catalog.TrainingCostItem.itemName} ×{catalog.TrainingCost}" : "训练物资未配置";
        }

        private void PreviousPage()
        {
            if (isSubmitting || trainingPage <= 0) return;
            trainingPage--;
            Rebuild();
        }

        private void NextPage()
        {
            if (isSubmitting || (trainingPage + 1) * TrainingCardsPerPage >= families.Count) return;
            trainingPage++;
            Rebuild();
        }

        private void SetCardsEnabled(bool enabled)
        {
            foreach (HunterGrowthChoiceCard3D card in growthCards)
                if (card != null && card.TryGetComponent(out Collider collider)) collider.enabled = enabled;
            foreach (WeaponTrainingCard3D card in trainingCards)
                if (card != null && card.TryGetComponent(out Collider collider)) collider.enabled = enabled;
        }

        private void ClearCards()
        {
            foreach (HunterGrowthChoiceCard3D card in growthCards)
                if (card != null) Destroy(card.gameObject);
            foreach (WeaponTrainingCard3D card in trainingCards)
                if (card != null) Destroy(card.gameObject);
            growthCards.Clear();
            trainingCards.Clear();
        }

        private TextMeshPro BuildText(string name, Vector3 position, float fontSize, Vector2 size)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(ContentRoot, false);
            textObject.transform.localPosition = position;
            textObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            TextMeshPro text = textObject.AddComponent<TextMeshPro>();
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.82f, 0.82f, 0.78f);
            text.rectTransform.sizeDelta = size;
#if UNITY_6000_0_OR_NEWER
            text.textWrappingMode = TextWrappingModes.Normal;
#else
            text.enableWordWrapping = true;
#endif
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private void BuildButton(string name, string labelText, Vector3 position, Vector3 scale, Action onClick, Color color)
        {
            GameObject button = GameObject.CreatePrimitive(PrimitiveType.Cube);
            button.name = name;
            button.transform.SetParent(transform, false);
            button.transform.localPosition = position;
            button.transform.localScale = scale;
            button.GetComponent<Renderer>().material.color = color;
            button.AddComponent<ClickProxy>().OnClick = onClick;
            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(button.transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            labelObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            labelObject.transform.localScale = new Vector3(1f / scale.x, 1f, 1f / scale.z);
            TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
            label.text = labelText;
            label.fontSize = 0.085f;
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = new Vector2(scale.x - 0.06f, scale.z - 0.04f);
            label.overflowMode = TextOverflowModes.Ellipsis;
        }

        private static string GetChoiceName(HunterGrowthChoice choice) => choice == HunterGrowthChoice.Courage ? "胆识" : "知识";

        private static string FormatMilestones(IReadOnlyList<HunterGrowthMilestoneOutcome> milestones)
        {
            if (milestones == null || milestones.Count == 0) return string.Empty;
            var names = new List<string>();
            foreach (HunterGrowthMilestoneOutcome milestone in milestones)
                names.Add(milestone.DisplayName);
            return $" 达成：{string.Join("、", names)}";
        }

        private static string FormatMasteryMilestones(IReadOnlyList<string> milestones) => milestones != null && milestones.Count > 0 ? $" 达成：{string.Join("、", milestones)}" : string.Empty;
    }
}
