using System;
using System.Collections.Generic;
using Cards3D;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.ActionFlow.Settlement;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Settlement;
using TMPro;
using UnityEngine;

namespace UI
{
    /// <summary>以实体症状卡呈现猎人的弱点，并只通过 Settlement ActionQueue 提交内化或克服。</summary>
    public sealed class HunterSymptomPanel3D : WorldSpaceViewPanel
    {
        private const int CardsPerPage = 4;
        private readonly List<SymptomDefinition> definitions = new();
        private readonly List<HunterSymptomCard3D> cards = new();
        private TextMeshPro summaryText;
        private TextMeshPro detailText;
        private TextMeshPro pageText;
        private TextMeshPro statusText;
        private GameObject internalizeButton;
        private GameObject overcomeButton;
        private HunterInstance hunter;
        private SettlementInstance settlement;
        private PlayableSymptomCatalog catalog;
        private Func<int, string, SymptomResolutionChoice, UniTask<HunterSymptomCommandResult>> command;
        private string selectedSymptomId;
        private int pageIndex;
        private bool isBuilt;
        private bool isSubmitting;

        public static HunterSymptomPanel3D Create(Transform parent)
        {
            var gameObject = new GameObject("HunterSymptomPanel3D");
            gameObject.transform.SetParent(parent, false);
            var panel = gameObject.AddComponent<HunterSymptomPanel3D>();
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
            SetSize(6.0f, 4.5f);
            summaryText = BuildText("Summary", new Vector3(0f, 0.015f, 1.72f), 0.08f, new Vector2(5.1f, 0.32f));
            detailText = BuildText("Detail", new Vector3(0f, 0.015f, -0.52f), 0.07f, new Vector2(5.0f, 0.70f));
            pageText = BuildText("Page", new Vector3(0f, 0.015f, 0.12f), 0.065f, new Vector2(2.0f, 0.22f));
            statusText = BuildText("Status", new Vector3(0f, 0.015f, -1.82f), 0.07f, new Vector2(5.0f, 0.30f));
            BuildButton("PreviousPage", "上一页", new Vector3(-2.50f, 0.03f, 0.12f), new Vector3(0.58f, 0.04f, 0.26f), PreviousPage, new Color(0.20f, 0.24f, 0.30f));
            BuildButton("NextPage", "下一页", new Vector3(2.50f, 0.03f, 0.12f), new Vector3(0.58f, 0.04f, 0.26f), NextPage, new Color(0.20f, 0.24f, 0.30f));
            internalizeButton = BuildButton("Internalize", "面对并内化", new Vector3(-1.05f, 0.03f, -1.32f), new Vector3(1.30f, 0.04f, 0.34f), () => Submit(SymptomResolutionChoice.Internalize), new Color(0.25f, 0.34f, 0.43f));
            overcomeButton = BuildButton("Overcome", "克服弱点", new Vector3(1.05f, 0.03f, -1.32f), new Vector3(1.30f, 0.04f, 0.34f), () => Submit(SymptomResolutionChoice.Overcome), new Color(0.43f, 0.24f, 0.16f));
            BuildButton("Close", "关闭", new Vector3(2.62f, 0.03f, 2.02f), new Vector3(0.52f, 0.04f, 0.22f), Hide, new Color(0.40f, 0.14f, 0.13f));
        }

        public void Open(HunterInstance selectedHunter, SettlementInstance settlementData, PlayableSymptomCatalog symptomCatalog, Func<int, string, SymptomResolutionChoice, UniTask<HunterSymptomCommandResult>> onResolve, Vector3 worldPosition)
        {
            if (selectedHunter == null || settlementData == null) return;
            hunter = selectedHunter;
            settlement = settlementData;
            catalog = symptomCatalog;
            command = onResolve;
            selectedSymptomId = string.Empty;
            pageIndex = 0;
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
            CollectDefinitions();
            Title.text = $"{hunter.Name} · 面对症状";
            summaryText.text = $"意志 {hunter.Willpower}/{hunter.WillpowerMax}　胆识 {hunter.Courage}　待分配成长 {hunter.UnspentGrowth}";
            int pageCount = Mathf.Max(1, Mathf.CeilToInt((float)definitions.Count / CardsPerPage));
            pageIndex = Mathf.Clamp(pageIndex, 0, pageCount - 1);
            pageText.text = definitions.Count == 0 ? "没有需要面对的症状" : $"症状 {pageIndex + 1}/{pageCount} 页";

            int startIndex = pageIndex * CardsPerPage;
            int endIndex = Mathf.Min(startIndex + CardsPerPage, definitions.Count);
            if (!IsSelectionOnPage(startIndex, endIndex)) selectedSymptomId = startIndex < endIndex ? definitions[startIndex].Id : string.Empty;
            float spacing = CardView3D.CW + 0.22f;
            float startX = -(endIndex - startIndex - 1) * spacing * 0.5f;
            for (int index = startIndex; index < endIndex; index++)
            {
                SymptomDefinition definition = definitions[index];
                HunterSymptomState state = HunterSymptomRules.Find(hunter, definition.Id);
                HunterSymptomCard3D card = HunterSymptomCard3D.Create(definition, state, ContentRoot, new Vector3(startX + (index - startIndex) * spacing, 0f, 0.92f));
                card.Selected = SelectCard;
                card.SetSelected(string.Equals(selectedSymptomId, definition.Id, StringComparison.Ordinal));
                cards.Add(card);
            }
            RefreshSelection();
        }

        private void CollectDefinitions()
        {
            definitions.Clear();
            if (hunter.SymptomStates == null || catalog == null) return;
            foreach (HunterSymptomState state in hunter.SymptomStates)
                if (state != null && !state.IsOvercome && catalog.TryGetById(state.SymptomId, out SymptomDefinition definition))
                    definitions.Add(definition);
            definitions.Sort((left, right) => string.Compare(left.DisplayName, right.DisplayName, StringComparison.Ordinal));
        }

        private void SelectCard(HunterSymptomCard3D card)
        {
            if (card?.Definition == null || isSubmitting) return;
            selectedSymptomId = card.Definition.Id;
            foreach (HunterSymptomCard3D current in cards)
                current.SetSelected(ReferenceEquals(current, card));
            RefreshSelection();
        }

        private void RefreshSelection()
        {
            SymptomDefinition definition = GetSelectedDefinition();
            if (definition == null)
            {
                detailText.text = "症状会保留负面影响；内化会追加正向收益，克服则移除原有负面影响。";
                SetButtonState(internalizeButton, false);
                SetButtonState(overcomeButton, false);
                statusText.text = "当前猎人没有可处理的已配置症状。";
                return;
            }

            HunterSymptomState state = HunterSymptomRules.Find(hunter, definition.Id);
            detailText.text = $"{definition.Description}\n内化：每年一次，意志 -{definition.ReflectionWillpowerCost}，进度 {state.InternalizationProgress}/{definition.InternalizationThreshold}。　克服：胆识需 {definition.OvercomeCourageRequirement}，成长 -{definition.OvercomeGrowthCost}。";
            string internalizeReason = string.Empty;
            string overcomeReason = string.Empty;
            bool canInternalize = !isSubmitting && HunterSymptomRules.CanInternalize(hunter, definition, settlement.CurrentYear, out internalizeReason);
            bool canOvercome = !isSubmitting && HunterSymptomRules.CanOvercome(hunter, definition, out overcomeReason);
            SetButtonState(internalizeButton, canInternalize);
            SetButtonState(overcomeButton, canOvercome);
            statusText.text = isSubmitting ? "营火正在回应这次选择……" : $"内化：{(canInternalize ? "可执行" : internalizeReason)}　　克服：{(canOvercome ? "可执行" : overcomeReason)}";
        }

        private void Submit(SymptomResolutionChoice choice)
        {
            if (isSubmitting) return;
            SymptomDefinition definition = GetSelectedDefinition();
            if (definition == null) return;
            if (choice == SymptomResolutionChoice.Internalize && !HunterSymptomRules.CanInternalize(hunter, definition, settlement.CurrentYear, out string internalizeReason))
            {
                statusText.text = internalizeReason;
                return;
            }
            if (choice == SymptomResolutionChoice.Overcome && !HunterSymptomRules.CanOvercome(hunter, definition, out string overcomeReason))
            {
                statusText.text = overcomeReason;
                return;
            }
            ResolveAsync(definition.Id, choice).Forget();
        }

        private async UniTaskVoid ResolveAsync(string symptomId, SymptomResolutionChoice choice)
        {
            if (command == null)
            {
                statusText.text = "症状命令尚未接入。";
                return;
            }
            isSubmitting = true;
            RefreshSelection();
            try
            {
                HunterSymptomCommandResult result = await command.Invoke(hunter.InstanceId, symptomId, choice);
                if (this == null) return;
                isSubmitting = false;
                Rebuild();
                statusText.text = result.Succeeded ? FormatResult(result) : result.Reason;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (this == null) return;
                isSubmitting = false;
                Rebuild();
                statusText.text = "症状命令执行异常，请重试。";
            }
        }

        private void PreviousPage()
        {
            if (isSubmitting || pageIndex <= 0) return;
            pageIndex--;
            selectedSymptomId = string.Empty;
            Rebuild();
        }

        private void NextPage()
        {
            if (isSubmitting || (pageIndex + 1) * CardsPerPage >= definitions.Count) return;
            pageIndex++;
            selectedSymptomId = string.Empty;
            Rebuild();
        }

        private SymptomDefinition GetSelectedDefinition()
        {
            foreach (SymptomDefinition definition in definitions)
                if (string.Equals(definition.Id, selectedSymptomId, StringComparison.Ordinal)) return definition;
            return null;
        }

        private bool IsSelectionOnPage(int startIndex, int endIndex)
        {
            for (int index = startIndex; index < endIndex; index++)
                if (string.Equals(definitions[index].Id, selectedSymptomId, StringComparison.Ordinal)) return true;
            return false;
        }

        private void ClearCards()
        {
            foreach (HunterSymptomCard3D card in cards)
                if (card != null) Destroy(card.gameObject);
            cards.Clear();
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

        private GameObject BuildButton(string name, string labelText, Vector3 position, Vector3 scale, Action onClick, Color color)
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
            label.rectTransform.sizeDelta = new Vector2(scale.x - 0.08f, scale.z - 0.05f);
            label.overflowMode = TextOverflowModes.Ellipsis;
            return button;
        }

        private static void SetButtonState(GameObject button, bool enabled)
        {
            if (button == null) return;
            if (button.TryGetComponent(out Collider collider)) collider.enabled = enabled;
            Color enabledColor = button.name == "Internalize" ? new Color(0.25f, 0.34f, 0.43f) : new Color(0.43f, 0.24f, 0.16f);
            button.GetComponent<Renderer>().material.color = enabled ? enabledColor : new Color(0.15f, 0.15f, 0.16f);
        }

        private static string FormatResult(HunterSymptomCommandResult result)
        {
            if (result.Choice == SymptomResolutionChoice.Overcome) return $"已克服“{result.SymptomName}”，成长 {result.PreviousGrowth} → {result.CurrentGrowth}。";
            string completed = result.IsInternalized ? "，并将弱点内化为新的力量" : string.Empty;
            return $"面对“{result.SymptomName}”：内化 {result.PreviousProgress} → {result.CurrentProgress}，意志 {result.PreviousWillpower} → {result.CurrentWillpower}{completed}。";
        }
    }
}
