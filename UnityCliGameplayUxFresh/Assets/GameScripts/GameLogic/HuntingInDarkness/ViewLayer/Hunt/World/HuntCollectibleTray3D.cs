using System.Collections.Generic;
using HuntingInDarkness.ActionFlow.Hunt;
using HuntingInDarkness.Data;
using HuntingInDarkness.ViewLayer.Hunt;
using TMPro;
using UnityEngine;

namespace UI.Hunt
{
    /// <summary>在狩猎状态桌上持续投影当前行动猎人的携带物卡牌。</summary>
    public sealed class HuntCollectibleTray3D : MonoBehaviour
    {
        private readonly List<HuntCollectibleCard3D> cards = new();
        private TextMeshPro titleText;
        private TextMeshPro statusText;
        private IPlayableHuntConsumableInput consumableInput;
        private HunterInstance currentHunter;
        private HuntConsumablePanel3D consumablePanel;
        private string presentationSignature;

        public int CardCount => cards.Count;
        public string OwnerName { get; private set; } = string.Empty;
        public bool IsConsumablePanelOpen => consumablePanel != null && consumablePanel.gameObject.activeSelf;

        public static HuntCollectibleTray3D Create(Transform parent)
        {
            var gameObject = new GameObject("HuntCollectibleTray3D");
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = HuntStatusBoardLayout.CollectibleTrayLocalPosition;
            var tray = gameObject.AddComponent<HuntCollectibleTray3D>();
            tray.BuildLabels();
            return tray;
        }

        public void Initialize(IPlayableHuntConsumableInput input)
        {
            consumableInput = input;
            presentationSignature = string.Empty;
        }

        public void Present(HunterInstance hunter)
        {
            HuntCollectiblePresentation presentation = HuntCollectiblePresentation.Create(hunter?.Collectibles, HuntStatusBoardLayout.MaximumCollectibleCards);
            string signature = CreateSignature(hunter, presentation.Stacks);
            if (string.Equals(signature, presentationSignature, System.StringComparison.Ordinal)) return;
            presentationSignature = signature;
            currentHunter = hunter;
            OwnerName = hunter?.Name ?? string.Empty;
            ClearCards();
            titleText.text = hunter != null ? $"{OwnerName} · 携带物" : "携带物";
            statusText.text = presentation.TotalCount > 0 ? $"共 {presentation.TotalCount} 件 · {presentation.DistinctCount} 类" : hunter != null ? "尚未获得狩猎素材" : "没有可行动猎人";
            int visibleCount = Mathf.Min(presentation.Stacks.Count, HuntStatusBoardLayout.MaximumCollectibleCards);
            for (int index = 0; index < visibleCount; index++)
            {
                HuntCollectibleStackPresentation stack = presentation.Stacks[index];
                System.Action clicked = stack.CanUseInHunt && consumableInput != null && hunter?.IsAlive == true && hunter.IsAvailable ? () => OpenConsumable(stack) : null;
                cards.Add(HuntCollectibleCard3D.Create(stack, transform, HuntStatusBoardLayout.GetCollectibleCardLocalPosition(index), clicked));
            }
        }

        private void OpenConsumable(HuntCollectibleStackPresentation stack)
        {
            if (currentHunter == null || consumableInput == null || !stack.CanUseInHunt || consumablePanel?.IsSubmitting == true) return;
            consumablePanel ??= HuntConsumablePanel3D.Create(transform.parent);
            consumablePanel.Open(currentHunter, stack, consumableInput, transform.position + new Vector3(0f, 0.05f, -2.2f), OnConsumableCompleted);
        }

        private void OnConsumableCompleted(HuntConsumableCommandResult result)
        {
            if (!result.Succeeded || currentHunter == null) return;
            presentationSignature = string.Empty;
            Present(currentHunter);
        }

        private void BuildLabels()
        {
            titleText = CreateText("Title", HuntStatusBoardLayout.CollectibleTitleLocalPosition, 0.12f, new Vector2(2.2f, 0.24f));
            statusText = CreateText("Status", HuntStatusBoardLayout.CollectibleStatusLocalPosition, 0.075f, new Vector2(2.2f, 0.18f));
        }

        private TextMeshPro CreateText(string objectName, Vector3 localPosition, float fontSize, Vector2 size)
        {
            var textObject = new GameObject(objectName);
            textObject.transform.SetParent(transform, false);
            textObject.transform.localPosition = localPosition;
            textObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var text = textObject.AddComponent<TextMeshPro>();
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.rectTransform.sizeDelta = size;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private static string CreateSignature(HunterInstance hunter, IReadOnlyList<HuntCollectibleStackPresentation> stacks)
        {
            var parts = new List<string>(stacks.Count + 1) { hunter != null ? $"{hunter.InstanceId}:{hunter.Name}" : "none" };
            foreach (HuntCollectibleStackPresentation stack in stacks)
                parts.Add($"{stack.ContentId}:{stack.DisplayName}:{stack.Count}");
            return string.Join("|", parts);
        }

        private void ClearCards()
        {
            foreach (HuntCollectibleCard3D card in cards)
                if (card != null)
                {
                    card.gameObject.SetActive(false);
                    Destroy(card.gameObject);
                }
            cards.Clear();
        }

        private void OnDestroy()
        {
            ClearCards();
            if (consumablePanel != null) Destroy(consumablePanel.gameObject);
        }
    }
}
