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
    /// <summary>世界空间营火招募板：候选卡、命名牌与提交按钮只产生意图，事务由 Settlement ActionQueue 执行。</summary>
    public sealed class RecruitmentPanel3D : WorldSpaceViewPanel
    {
        private const int CardsPerPage = 5;
        private readonly List<RecruitmentTemplateCard3D> cards = new();
        private readonly List<int> visibleTemplateIndices = new();
        private TextMeshPro summaryText;
        private TextMeshPro nameText;
        private TextMeshPro statusText;
        private Renderer nameSlateRenderer;
        private SettlementInstance settlement;
        private PlayableSettlementContentCatalog catalog;
        private Func<HunterData, string, UniTask<RecruitHunterCommandResult>> recruitCommand;
        private int selectedTemplateIndex = -1;
        private int pageIndex;
        private string requestedName = string.Empty;
        private string persistentStatus = string.Empty;
        private bool isBuilt;
        private bool isSubmitting;
        private bool isNameFocused;
        private IMECompositionMode previousImeMode;

        public static RecruitmentPanel3D Create(Transform parent)
        {
            var gameObject = new GameObject("RecruitmentPanel3D");
            gameObject.transform.SetParent(parent, false);
            var panel = gameObject.AddComponent<RecruitmentPanel3D>();
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
            SetSize(6.4f, 4.1f);
            summaryText = BuildText("Summary", new Vector3(0f, 0.015f, 1.53f), 0.08f, new Vector2(5.6f, 0.34f));
            BuildNameSlate();
            statusText = BuildText("Status", new Vector3(0f, 0.015f, -0.93f), 0.07f, new Vector2(5.5f, 0.34f));
            BuildButton("PreviousPage", "上一页", new Vector3(-2.82f, 0.03f, 0.45f), new Vector3(0.48f, 0.04f, 0.32f), PreviousPage, new Color(0.20f, 0.24f, 0.30f));
            BuildButton("NextPage", "下一页", new Vector3(2.82f, 0.03f, 0.45f), new Vector3(0.48f, 0.04f, 0.32f), NextPage, new Color(0.20f, 0.24f, 0.30f));
            BuildButton("Confirm", "接纳并记入年鉴", new Vector3(-0.9f, 0.03f, -1.60f), new Vector3(1.55f, 0.04f, 0.38f), Submit, new Color(0.36f, 0.25f, 0.10f));
            BuildButton("Close", "暂不接纳", new Vector3(0.9f, 0.03f, -1.60f), new Vector3(1.20f, 0.04f, 0.38f), ClosePanel, new Color(0.34f, 0.13f, 0.13f));
        }

        public void Open(SettlementInstance settlementData, PlayableSettlementContentCatalog content, Func<HunterData, string, UniTask<RecruitHunterCommandResult>> command, Vector3 worldPosition)
        {
            if (settlementData == null || content == null) return;
            settlement = settlementData;
            catalog = content;
            recruitCommand = command;
            selectedTemplateIndex = FindFirstTemplateIndex();
            pageIndex = selectedTemplateIndex < 0 ? 0 : selectedTemplateIndex / CardsPerPage;
            requestedName = string.Empty;
            persistentStatus = string.Empty;
            isSubmitting = false;
            ReleaseNameFocus();
            Rebuild();
            ShowAt(worldPosition);
        }

        public void RefreshVisible()
        {
            if (!gameObject.activeSelf || settlement == null || catalog == null || isSubmitting) return;
            Rebuild();
        }

        private void Update()
        {
            if (!gameObject.activeSelf || !isNameFocused || isSubmitting) return;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                ReleaseNameFocus();
                RefreshNameSlate();
                return;
            }
            if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.V))
                AppendText(GUIUtility.systemCopyBuffer);
            foreach (char character in Input.inputString)
            {
                if (character == '\b')
                {
                    if (requestedName.Length > 0) requestedName = requestedName[..^1];
                    continue;
                }
                if (character == '\n' || character == '\r')
                {
                    Submit();
                    return;
                }
                if (!char.IsControl(character)) AppendText(character.ToString());
            }
            RefreshNameSlate();
        }

        private void Rebuild()
        {
            ClearCards();
            Title.text = "营火招募 · 为陌生人留下名字";
            summaryText.text = $"接纳成本：{FormatCost()}　·　每年限一次　·　候选 {CountTemplates()} 人";
            bool canRecruit = CanRecruit(out string reason);
            int startIndex = pageIndex * CardsPerPage;
            float spacing = CardView3D.CW + 0.18f;
            int pageCount = Mathf.Min(CardsPerPage, Mathf.Max(0, catalog.RecruitmentTemplates.Count - startIndex));
            float startX = -(pageCount - 1) * spacing * 0.5f;
            int cardOffset = 0;
            for (int index = startIndex; index < catalog.RecruitmentTemplates.Count && index < startIndex + CardsPerPage; index++)
            {
                HunterData template = catalog.RecruitmentTemplates[index];
                if (template == null) continue;
                RecruitmentTemplateCard3D card = RecruitmentTemplateCard3D.Create(template, ContentRoot, new Vector3(startX + cardOffset * spacing, 0f, 0.45f));
                card.Selected = SelectTemplate;
                card.ConfigureState(index == selectedTemplateIndex, canRecruit && !isSubmitting);
                cards.Add(card);
                visibleTemplateIndices.Add(index);
                cardOffset++;
            }
            statusText.text = !string.IsNullOrEmpty(persistentStatus) ? persistentStatus : canRecruit ? "点击命名牌后输入名字，再选择一张候选卡接纳。" : reason;
            RefreshNameSlate();
        }

        private void SelectTemplate(RecruitmentTemplateCard3D card)
        {
            if (card?.Template == null || isSubmitting) return;
            int visibleIndex = cards.IndexOf(card);
            if (visibleIndex < 0 || visibleIndex >= visibleTemplateIndices.Count) return;
            selectedTemplateIndex = visibleTemplateIndices[visibleIndex];
            persistentStatus = string.Empty;
            RefreshCardStates();
        }

        private void Submit()
        {
            if (isSubmitting) return;
            SubmitAsync().Forget();
        }

        private async UniTaskVoid SubmitAsync()
        {
            if (recruitCommand == null)
            {
                statusText.text = "招募命令尚未接入。";
                return;
            }
            HunterData template = GetSelectedTemplate();
            if (template == null)
            {
                statusText.text = "请先选择一张候选猎人卡。";
                return;
            }
            if (!RecruitmentRules.TryNormalizeName(requestedName, GetExistingNames(), out _, out string validationReason))
            {
                statusText.text = validationReason;
                FocusNameSlate();
                return;
            }
            ReleaseNameFocus();
            isSubmitting = true;
            RefreshCardStates();
            statusText.text = "火光正在回应……";
            try
            {
                RecruitHunterCommandResult result = await recruitCommand.Invoke(template, requestedName);
                if (this == null) return;
                isSubmitting = false;
                persistentStatus = result.Succeeded ? $"{result.Hunter.Name} 已加入营地 · 血脉：{result.Hunter.BloodlineName}\n可以为其分配装备。" : result.Reason;
                if (result.Succeeded) requestedName = string.Empty;
                Rebuild();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (this == null) return;
                isSubmitting = false;
                persistentStatus = "招募命令执行异常，请重试。";
                Rebuild();
            }
        }

        private void PreviousPage()
        {
            if (isSubmitting || pageIndex <= 0) return;
            pageIndex--;
            Rebuild();
        }

        private void NextPage()
        {
            if (isSubmitting || (pageIndex + 1) * CardsPerPage >= catalog.RecruitmentTemplates.Count) return;
            pageIndex++;
            Rebuild();
        }

        private void FocusNameSlate()
        {
            if (isSubmitting || isNameFocused) return;
            isNameFocused = true;
            previousImeMode = Input.imeCompositionMode;
            Input.imeCompositionMode = IMECompositionMode.On;
            persistentStatus = string.Empty;
            RefreshNameSlate();
        }

        private void ReleaseNameFocus()
        {
            if (!isNameFocused) return;
            isNameFocused = false;
            Input.imeCompositionMode = previousImeMode;
        }

        private void ClosePanel()
        {
            ReleaseNameFocus();
            Hide();
        }

        private void OnDisable() => ReleaseNameFocus();

        private void AppendText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            foreach (char character in text)
            {
                if (char.IsControl(character) || requestedName.Length >= RecruitmentRules.MaximumNameLength) continue;
                requestedName += character;
            }
        }

        private void RefreshNameSlate()
        {
            if (nameText == null) return;
            string composition = isNameFocused ? Input.compositionString : string.Empty;
            string visibleName = requestedName + composition;
            nameText.text = string.IsNullOrEmpty(visibleName) ? "点击命名牌并输入名字" : visibleName;
            nameText.color = isNameFocused ? new Color(0.98f, 0.82f, 0.42f) : new Color(0.88f, 0.86f, 0.78f);
            if (nameSlateRenderer != null) nameSlateRenderer.material.color = isNameFocused ? new Color(0.28f, 0.20f, 0.08f) : new Color(0.12f, 0.12f, 0.14f);
        }

        private void RefreshCardStates()
        {
            bool canRecruit = CanRecruit(out _);
            for (int index = 0; index < cards.Count; index++)
                cards[index].ConfigureState(visibleTemplateIndices[index] == selectedTemplateIndex, canRecruit && !isSubmitting);
        }

        private bool CanRecruit(out string reason)
        {
            if (settlement == null || catalog == null || !HasTemplate())
            {
                reason = "暂无候选猎人。";
                return false;
            }
            int aliveCount = settlement.GetAliveHunters().Count;
            int cost = RecruitmentRules.GetCost(aliveCount, catalog.RecruitmentCost);
            if (cost > 0 && catalog.RecruitmentCostItem == null)
            {
                reason = "接纳物资尚未配置。";
                return false;
            }
            int availableResource = settlement.GetResource(catalog.RecruitmentCostItem);
            return RecruitmentRules.CanRecruit(settlement.CurrentYear, settlement.LastRecruitmentYear, aliveCount, catalog.MaximumLivingHunters, availableResource, catalog.RecruitmentCost, out reason);
        }

        private HunterData GetSelectedTemplate()
        {
            if (catalog == null || selectedTemplateIndex < 0 || selectedTemplateIndex >= catalog.RecruitmentTemplates.Count) return null;
            return catalog.RecruitmentTemplates[selectedTemplateIndex];
        }

        private IEnumerable<string> GetExistingNames()
        {
            if (settlement == null) yield break;
            foreach (HunterState hunter in settlement.Hunters)
                if (hunter != null) yield return hunter.Name;
        }

        private int FindFirstTemplateIndex()
        {
            if (catalog == null) return -1;
            for (int index = 0; index < catalog.RecruitmentTemplates.Count; index++)
                if (catalog.RecruitmentTemplates[index] != null) return index;
            return -1;
        }

        private int CountTemplates()
        {
            if (catalog == null) return 0;
            int count = 0;
            foreach (HunterData template in catalog.RecruitmentTemplates)
                if (template != null) count++;
            return count;
        }

        private bool HasTemplate() => CountTemplates() > 0;

        private string FormatCost()
        {
            int cost = RecruitmentRules.GetCost(settlement?.GetAliveHunters().Count ?? 0, catalog?.RecruitmentCost ?? 0);
            if (cost == 0) return "无人守火时免费援助";
            return catalog?.RecruitmentCostItem != null ? $"{catalog.RecruitmentCostItem.itemName} ×{cost}" : "物资未配置";
        }

        private void BuildNameSlate()
        {
            GameObject slate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slate.name = "NameSlate";
            slate.transform.SetParent(transform, false);
            slate.transform.localPosition = new Vector3(0f, 0.03f, -0.48f);
            slate.transform.localScale = new Vector3(3.8f, 0.04f, 0.42f);
            nameSlateRenderer = slate.GetComponent<Renderer>();
            nameSlateRenderer.material.color = new Color(0.12f, 0.12f, 0.14f);
            slate.AddComponent<ClickProxy>().OnClick = FocusNameSlate;
            var textObject = new GameObject("Name");
            textObject.transform.SetParent(slate.transform, false);
            textObject.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            textObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            textObject.transform.localScale = new Vector3(1f / 3.8f, 1f, 1f / 0.42f);
            nameText = textObject.AddComponent<TextMeshPro>();
            nameText.fontSize = 0.12f;
            nameText.alignment = TextAlignmentOptions.Center;
            nameText.rectTransform.sizeDelta = new Vector2(3.55f, 0.30f);
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
            label.fontSize = 0.09f;
            label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = new Vector2(scale.x - 0.08f, scale.z - 0.06f);
        }

        private void ClearCards()
        {
            foreach (RecruitmentTemplateCard3D card in cards)
                if (card != null) Destroy(card.gameObject);
            cards.Clear();
            visibleTemplateIndices.Clear();
        }
    }
}
