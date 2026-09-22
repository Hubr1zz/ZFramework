using System;
using System.Collections.Generic;
using Cards3D;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.ActionFlow.Settlement;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Settlement;
using TMPro;
using UnityEngine;

namespace UI
{
    public sealed class SettlementFacilityDutyPanel3D : WorldSpaceViewPanel
    {
        private TextMeshPro detailsText;
        private TextMeshPro statusText;
        private GameObject assignButton;
        private GameObject cancelButton;
        private GameObject resolveButton;
        private SettlementInstance settlement;
        private IReadOnlyList<SettlementFacilityDutyDefinition> definitions;
        private Func<string, string, int, UniTask<SettlementFacilityDutyCommandResult>> assignCommand;
        private Func<string, UniTask<SettlementFacilityDutyCommandResult>> cancelCommand;
        private Func<string, UniTask<SettlementFacilityDutyCommandResult>> resolveCommand;
        private string selectedDutyId;
        private string selectedFacilityId;
        private int selectedHunterId;
        private bool submitting;
        private int presentationGeneration;
        private readonly List<SettlementFacilityDutyCard3D> dutyCards = new();
        private readonly List<HunterCard3D> hunterCards = new();

        public static SettlementFacilityDutyPanel3D Create(Transform parent)
        {
            var gameObject = new GameObject("SettlementFacilityDutyPanel3D");
            gameObject.transform.SetParent(parent, false);
            var panel = gameObject.AddComponent<SettlementFacilityDutyPanel3D>();
            panel.EnsureBuilt();
            panel.Hide();
            return panel;
        }

        public void EnsureBuilt()
        {
            if (detailsText != null) return;
            BuildBase();
            SetSize(4.4f, 2.8f);
            detailsText = BuildText("Details", new Vector3(0f, 0.015f, 0.45f), 0.08f, new Vector2(3.8f, 0.95f));
            statusText = BuildText("Status", new Vector3(0f, 0.015f, -0.35f), 0.075f, new Vector2(3.8f, 0.30f));
            assignButton = BuildButton("Assign", "派驻", new Vector3(-1.1f, 0.03f, -1.08f), new Color(0.40f, 0.32f, 0.10f), Assign);
            cancelButton = BuildButton("Cancel", "取消", new Vector3(0f, 0.03f, -1.08f), new Color(0.42f, 0.25f, 0.12f), Cancel);
            resolveButton = BuildButton("Resolve", "掷骰结算", new Vector3(1.1f, 0.03f, -1.08f), new Color(0.18f, 0.35f, 0.20f), Resolve);
            BuildButton("Close", "关闭", new Vector3(0f, 0.03f, -1.45f), new Color(0.42f, 0.15f, 0.14f), Hide);
        }

        public void Open(SettlementInstance settlementData, IReadOnlyList<SettlementFacilityDutyDefinition> dutyDefinitions, Func<string, string, int, UniTask<SettlementFacilityDutyCommandResult>> assign, Func<string, UniTask<SettlementFacilityDutyCommandResult>> cancel, Func<string, UniTask<SettlementFacilityDutyCommandResult>> resolve, Vector3 worldPosition)
        {
            if (settlementData == null || submitting) return;
            presentationGeneration++;
            settlement = settlementData;
            definitions = dutyDefinitions ?? Array.Empty<SettlementFacilityDutyDefinition>();
            assignCommand = assign;
            cancelCommand = cancel;
            resolveCommand = resolve;
            submitting = false;
            SelectFirstDuty();
            ShowAt(worldPosition);
            RefreshVisible();
        }

        public void RefreshVisible()
        {
            if (!gameObject.activeSelf || settlement == null) return;
            var lines = new List<string> { $"人口：{settlement.Population}", "设施岗位：" };
            foreach (SettlementFacilityDutyDefinition definition in definitions ?? Array.Empty<SettlementFacilityDutyDefinition>())
            {
                settlement.TryGetFacilityDuty(definition.DutyId, out SettlementFacilityDutyState state);
                lines.Add($"{definition.DisplayName} · {(state == null ? "可派驻" : state.Status == SettlementFacilityDutyStateStatus.Active ? $"猎人 {state.AssignedHunterId}，到期 {state.DueYear}-{state.DueSeasonIndex + 1}" : state.Status.ToString())}");
            }
            detailsText.text = string.Join("\n", lines);
            SettlementFacilityDutyState selectedState = null;
            bool hasDuty = selectedDutyId != null && settlement.TryGetFacilityDuty(selectedDutyId, out selectedState);
            bool due = hasDuty && SettlementFacilityDutyRules.IsDue(selectedState, settlement.CurrentYear, settlement.CurrentSeasonIndex);
            SetButtonState(assignButton, !submitting && !hasDuty && selectedHunterId > 0 && assignCommand != null);
            SetButtonState(cancelButton, !submitting && hasDuty && !due && cancelCommand != null);
            SetButtonState(resolveButton, !submitting && due && resolveCommand != null);
            statusText.text = hasDuty ? due ? "岗位已到期，请掷骰结算。" : "值守进行中，可在到期前取消。" : "选择岗位后派驻可用猎人。";
        }

        private void SelectFirstDuty()
        {
            selectedDutyId = null;
            selectedFacilityId = null;
            foreach (SettlementFacilityDutyDefinition definition in definitions ?? Array.Empty<SettlementFacilityDutyDefinition>())
            {
                selectedDutyId = definition.DutyId;
                selectedFacilityId = definition.RequiredFacilityId;
                break;
            }
            List<HunterInstance> hunters = settlement.GetDepartureEligibleHunters(settlement.CurrentYear, settlement.CurrentSeasonIndex);
            selectedHunterId = 0;
            ClearSelectionCards();
            int cardIndex = 0;
            foreach (SettlementFacilityDutyDefinition definition in definitions ?? Array.Empty<SettlementFacilityDutyDefinition>())
            {
                SettlementFacilityDutyCard3D card = SettlementFacilityDutyCard3D.Create(definition, ContentRoot, new Vector3(-1.3f + cardIndex++ * 1.3f, 0f, 0.65f));
                card.Clicked = SelectDuty;
                dutyCards.Add(card);
            }
            cardIndex = 0;
            foreach (HunterInstance hunter in hunters)
            {
                HunterCard3D card = HunterCard3D.Create(hunter, ContentRoot, new Vector3(-1.3f + cardIndex++ * 1.3f, 0f, -0.55f));
                card.OnHunterClicked = SelectHunter;
                hunterCards.Add(card);
            }
        }

        private void SelectDuty(SettlementFacilityDutyCard3D card)
        {
            if (submitting || card?.Definition == null) return;
            selectedDutyId = card.Definition.DutyId;
            selectedFacilityId = card.Definition.RequiredFacilityId;
            RefreshVisible();
        }

        private void SelectHunter(HunterCard3D card)
        {
            if (submitting || card?.Hunter == null || !card.Hunter.IsAvailable) return;
            selectedHunterId = card.Hunter.InstanceId;
            RefreshVisible();
        }

        private void ClearSelectionCards()
        {
            foreach (SettlementFacilityDutyCard3D card in dutyCards)
                if (card != null) Destroy(card.gameObject);
            foreach (HunterCard3D card in hunterCards)
                if (card != null) Destroy(card.gameObject);
            dutyCards.Clear();
            hunterCards.Clear();
        }

        private void Assign() => SubmitAssignAsync().Forget();
        private async UniTaskVoid SubmitAssignAsync()
        {
            if (submitting || selectedDutyId == null || selectedHunterId <= 0 || assignCommand == null) return;
            submitting = true;
            int completionGeneration = presentationGeneration;
            RefreshVisible();
            string feedback;
            try
            {
                SettlementFacilityDutyCommandResult result = await assignCommand.Invoke(selectedDutyId, selectedFacilityId, selectedHunterId);
                feedback = result.Succeeded ? "值守已派驻。" : result.Reason;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                feedback = "派驻失败，请重试。";
            }
            submitting = false;
            if (this == null || completionGeneration != presentationGeneration || !isActiveAndEnabled) return;
            RefreshVisible();
            statusText.text = feedback;
        }

        private void Cancel() => SubmitSingleAsync(cancelCommand, "取消成功。", "取消失败，请重试。").Forget();
        private void Resolve() => SubmitSingleAsync(resolveCommand, "值守结算完成。", "结算失败，请重试。").Forget();
        private async UniTaskVoid SubmitSingleAsync(Func<string, UniTask<SettlementFacilityDutyCommandResult>> command, string success, string failure)
        {
            if (submitting || selectedDutyId == null || command == null) return;
            submitting = true;
            int completionGeneration = presentationGeneration;
            RefreshVisible();
            string assignmentId = settlement.TryGetFacilityDuty(selectedDutyId, out SettlementFacilityDutyState state) ? state.AssignmentId : selectedDutyId;
            string feedback;
            try
            {
                SettlementFacilityDutyCommandResult result = await command.Invoke(assignmentId);
                feedback = result.Succeeded ? success : result.Reason;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                feedback = failure;
            }
            submitting = false;
            if (this == null || completionGeneration != presentationGeneration || !isActiveAndEnabled) return;
            RefreshVisible();
            statusText.text = feedback;
        }

        private void OnDisable() => presentationGeneration++;
        private void OnDestroy() => ClearSelectionCards();

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

        private GameObject BuildButton(string name, string label, Vector3 position, Color color, Action click)
        {
            GameObject button = GameObject.CreatePrimitive(PrimitiveType.Cube);
            button.name = name;
            button.transform.SetParent(transform, false);
            button.transform.localPosition = position;
            button.transform.localScale = new Vector3(0.9f, 0.04f, 0.28f);
            button.GetComponent<Renderer>().material.color = color;
            button.AddComponent<ClickProxy>().OnClick = click;
            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(button.transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            labelObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            TextMeshPro text = labelObject.AddComponent<TextMeshPro>();
            text.text = label;
            text.fontSize = 0.10f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.95f, 0.90f, 0.78f);
            text.rectTransform.sizeDelta = new Vector2(0.8f, 0.22f);
            return button;
        }

        private static void SetButtonState(GameObject button, bool enabled)
        {
            if (button == null) return;
            Collider collider = button.GetComponent<Collider>();
            if (collider != null) collider.enabled = enabled;
        }
    }
}
