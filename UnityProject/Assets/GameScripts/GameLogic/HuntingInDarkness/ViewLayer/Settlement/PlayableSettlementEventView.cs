using System.Collections.Generic;
using System.Threading;
using Core;
using Cysharp.Threading.Tasks;
using GameplayBase;
using HuntingInDarkness.Data;
using HuntingInDarkness.Hunt;
using HuntingInDarkness.Settlement;
using UnityEngine;

namespace HuntingInDarkness.ViewLayer.Settlement
{
    /// <summary>可玩组合根的事件 View：选择对象、展示判定、重投，再提交唯一结果。</summary>
    public sealed class PlayableSettlementEventView : MonoBehaviour, IHuntEventInput
    {
        private enum HuntPromptKind
        {
            None,
            Narrative,
            Choice,
            Check,
            Result
        }

        private const int WindowId = 68022;
        private static int nextHuntInputOwnerId;
        private GameManager manager;
        private int huntInputOwnerId;
        private EventData currentEvent;
        private GamePhase eventPhase;
        private HunterInstance currentHunter;
        private PlayableEventChoiceTransaction transaction;
        private int pendingOptionIndex = -1;
        private string resultText;
        private GUIStyle windowStyle;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle resultStyle;
        private Texture2D windowTexture;
        private HuntPromptKind huntPrompt;
        private UniTaskCompletionSource narrativeSource;
        private UniTaskCompletionSource<HuntEventChoiceSelection> choiceSource;
        private UniTaskCompletionSource<HuntEventCheckDecision> checkSource;
        private UniTaskCompletionSource resultSource;

        public void Initialize(GameManager gameManager)
        {
            manager = gameManager;
            huntInputOwnerId = System.Threading.Interlocked.Increment(ref nextHuntInputOwnerId);
            if (manager != null)
                manager.SetHuntEventInput(this);
        }

        public async UniTask ConfirmNarrativeAsync(EventData gameEvent, HunterInstance actor, CancellationToken cancellationToken)
        {
            BeginHuntPrompt(HuntPromptKind.Narrative, gameEvent, actor);
            narrativeSource = new UniTaskCompletionSource();
            try
            {
                await narrativeSource.Task.AttachExternalCancellation(cancellationToken);
            }
            finally
            {
                narrativeSource = null;
                EndHuntPrompt(HuntPromptKind.Narrative);
            }
        }

        public async UniTask<HuntEventChoiceSelection> SelectChoiceAsync(EventData gameEvent, HunterInstance actor, IReadOnlyList<HunterInstance> hunters, CancellationToken cancellationToken)
        {
            BeginHuntPrompt(HuntPromptKind.Choice, gameEvent, actor);
            choiceSource = new UniTaskCompletionSource<HuntEventChoiceSelection>();
            try
            {
                return await choiceSource.Task.AttachExternalCancellation(cancellationToken);
            }
            finally
            {
                choiceSource = null;
                EndHuntPrompt(HuntPromptKind.Choice);
            }
        }

        public async UniTask<HuntEventCheckDecision> PresentCheckAsync(PlayableEventChoiceTransaction preparedTransaction, CancellationToken cancellationToken)
        {
            BeginHuntPrompt(HuntPromptKind.Check, preparedTransaction.GameEvent, preparedTransaction.Actor);
            transaction = preparedTransaction;
            checkSource = new UniTaskCompletionSource<HuntEventCheckDecision>();
            try
            {
                return await checkSource.Task.AttachExternalCancellation(cancellationToken);
            }
            finally
            {
                checkSource = null;
                EndHuntPrompt(HuntPromptKind.Check);
            }
        }

        public async UniTask ConfirmResultAsync(EventData gameEvent, EventResolutionResult result, CancellationToken cancellationToken)
        {
            BeginHuntPrompt(HuntPromptKind.Result, gameEvent, null);
            resultText = string.IsNullOrWhiteSpace(result.ResultText) ? (result.Success ? "判定成功。" : "判定失败。") : result.ResultText;
            resultSource = new UniTaskCompletionSource();
            try
            {
                await resultSource.Task.AttachExternalCancellation(cancellationToken);
            }
            finally
            {
                resultSource = null;
                EndHuntPrompt(HuntPromptKind.Result);
            }
        }

        private void OnGUI()
        {
            if (manager == null || manager.CurrentGamePhase != eventPhase) return;
            if (currentEvent == null && transaction == null && string.IsNullOrEmpty(resultText)) return;

            EnsureStyles();
            int previousDepth = GUI.depth;
            GUI.depth = -900;
            GUI.Button(new Rect(0f, 0f, Screen.width, Screen.height), GUIContent.none, GUIStyle.none);
            GUI.Window(WindowId, GetWindowRect(), DrawWindow, eventPhase == GamePhase.Hunt ? "狩猎事件" : "营地事件", windowStyle);
            GUI.depth = previousDepth;
        }

        private Rect GetWindowRect()
        {
            float width = Mathf.Min(620f, Screen.width - 48f);
            float height = Mathf.Min(500f, Screen.height - 48f);
            return new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
        }

        private void DrawWindow(int windowId)
        {
            GUILayout.Space(10f);
            if (!string.IsNullOrEmpty(resultText))
            {
                DrawCommittedResult();
                return;
            }
            if (pendingOptionIndex >= 0)
            {
                DrawHunterSelection();
                return;
            }
            if (transaction != null)
            {
                DrawPreparedCheck();
                return;
            }
            DrawEvent();
        }

        private void DrawEvent()
        {
            if (currentEvent == null) return;

            GUILayout.Label(currentEvent.eventName, titleStyle);
            if (currentHunter != null)
                GUILayout.Label($"与 {currentHunter.Name} 有关", bodyStyle);
            GUILayout.Space(12f);
            GUILayout.Label(currentEvent.displayText, bodyStyle);
            GUILayout.FlexibleSpace();

            if (currentEvent.eventType == GameEventType.Choice && currentEvent.options != null && currentEvent.options.Count > 0)
            {
                int availableOptionCount = 0;
                for (int index = 0; index < currentEvent.options.Count; index++)
                {
                    int optionIndex = index;
                    EventOption option = currentEvent.options[index];
                    string label = option.checkType == CheckType.None ? option.optionText : $"{option.optionText}  【{GetCheckName(option.checkType)}判定 · 目标 {option.checkTarget}】";
                    string requirements = PlayableEventOptionAvailability.GetRequirements(option);
                    if (!string.IsNullOrEmpty(requirements)) label += $"\n{requirements}";
                    bool available = CanPresentOption(option, out string reason);
                    if (available) availableOptionCount++;
                    GUI.enabled = available;
                    float buttonHeight = string.IsNullOrEmpty(requirements) ? 42f : 58f;
                    if (GUILayout.Button(label, GUILayout.Height(buttonHeight)))
                        BeginChoice(optionIndex);
                    GUI.enabled = true;
                    if (!available)
                        GUILayout.Label(reason, bodyStyle);
                }
                if (availableOptionCount == 0 && GUILayout.Button("没有可行的行动，只能接受沉默", GUILayout.Height(44f)))
                {
                    if (huntPrompt == HuntPromptKind.Choice)
                        choiceSource?.TrySetResult(new HuntEventChoiceSelection(-1, null));
                    else
                        ResolveNarrative();
                }
                return;
            }

            if (GUILayout.Button(currentEvent.eventType == GameEventType.Combat ? "迎接战斗" : "接受结果", GUILayout.Height(44f)))
                ResolveNarrative();
        }

        private void DrawHunterSelection()
        {
            EventOption option = currentEvent.options[pendingOptionIndex];
            GUILayout.Label("选择执行判定的猎人", titleStyle);
            string selectionDescription = option.checkType == CheckType.None ? option.optionText : $"{option.optionText}\n使用 {GetCheckName(option.checkType)}，目标 {option.checkTarget}";
            GUILayout.Label(selectionDescription, bodyStyle);
            GUILayout.Space(12f);

            var hunters = eventPhase == GamePhase.Hunt ? manager.ActiveHuntHunters : manager.SettlementData?.GetAvailableHunters();
            if (hunters == null || hunters.Count == 0)
            {
                GUILayout.Label("营地中没有能够执行判定的猎人。", resultStyle);
            }
            else
            {
                foreach (HunterInstance hunter in hunters)
                {
                    bool available = PlayableEventOptionAvailability.CanUse(option, hunter, manager.SettlementData, out string reason);
                    string label = option.checkType == CheckType.None ? $"{hunter.Name} · 意志 {hunter.Willpower}/{hunter.WillpowerMax}" : $"{hunter.Name} · {GetCheckName(option.checkType)} {GetCheckBonus(hunter, option.checkType)} · 意志 {hunter.Willpower}/{hunter.WillpowerMax}";
                    if (!available) label += $" · {reason}";
                    GUI.enabled = available;
                    if (GUILayout.Button(label, GUILayout.Height(40f)))
                        PrepareChoice(hunter);
                    GUI.enabled = true;
                }
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("返回事件选项", GUILayout.Height(36f)))
                pendingOptionIndex = -1;
        }

        private void DrawPreparedCheck()
        {
            GUILayout.Label(transaction.GameEvent.eventName, titleStyle);
            GUILayout.Label(transaction.Option.optionText, bodyStyle);
            if (transaction.Actor != null)
                GUILayout.Label($"{transaction.Actor.Name} 使用 {GetCheckName(transaction.Option.checkType)}", bodyStyle);
            GUILayout.Space(18f);
            GUILayout.Label($"骰值 {transaction.RollValue} + 属性 {transaction.Bonus} = {transaction.Total}\n目标 {transaction.Target}  →  {(transaction.Success ? "成功" : "失败")}", resultStyle);
            if (transaction.HasRerolled)
                GUILayout.Label("已消耗 1 意志重投，并保留较高骰值。", bodyStyle);
            GUILayout.FlexibleSpace();

            GUI.enabled = transaction.CanReroll;
            if (GUILayout.Button(transaction.CanReroll ? "消耗 1 意志重投" : "无法继续重投", GUILayout.Height(42f)))
            {
                if (huntPrompt == HuntPromptKind.Check)
                    checkSource?.TrySetResult(HuntEventCheckDecision.Reroll);
                else
                    transaction.TryReroll();
            }
            GUI.enabled = true;
            if (GUILayout.Button("接受这个结果", GUILayout.Height(44f)))
            {
                if (huntPrompt == HuntPromptKind.Check)
                    checkSource?.TrySetResult(HuntEventCheckDecision.Accept);
                else
                    CommitChoice();
            }
        }

        private void DrawCommittedResult()
        {
            GUILayout.Label(transaction?.GameEvent.eventName ?? "事件结果", titleStyle);
            GUILayout.Space(16f);
            GUILayout.Label(resultText, resultStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("继续", GUILayout.Height(44f)))
                ContinueEventChain();
        }

        private void ShowEvent(EventData gameEvent, HunterInstance hunter)
        {
            if (huntPrompt != HuntPromptKind.None) return;
            if (manager == null || manager.CurrentGamePhase != GamePhase.Settlement && manager.CurrentGamePhase != GamePhase.Hunt) return;
            eventPhase = manager.CurrentGamePhase;
            if (eventPhase == GamePhase.Hunt)
                PlayableHuntInputGuard.Acquire(huntInputOwnerId);
            currentEvent = gameEvent;
            currentHunter = hunter;
            transaction = null;
            pendingOptionIndex = -1;
            resultText = string.Empty;
        }

        private void BeginChoice(int optionIndex)
        {
            EventOption option = currentEvent.options[optionIndex];
            if (!CanPresentOption(option, out _)) return;
            if ((option.checkType != CheckType.None || PlayableEventOptionAvailability.RequiresHunter(option)) && currentHunter == null)
            {
                pendingOptionIndex = optionIndex;
                return;
            }

            pendingOptionIndex = optionIndex;
            if (huntPrompt == HuntPromptKind.Choice)
            {
                choiceSource?.TrySetResult(new HuntEventChoiceSelection(optionIndex, currentHunter));
                return;
            }
            PrepareChoice(currentHunter);
        }

        private bool CanPresentOption(EventOption option, out string reason)
        {
            if (currentHunter != null)
                return PlayableEventOptionAvailability.CanUse(option, currentHunter, manager.SettlementData, out reason);

            bool needsHunter = option.checkType != CheckType.None || PlayableEventOptionAvailability.RequiresHunter(option);
            if (!needsHunter)
                return PlayableEventOptionAvailability.CanUse(option, null, manager.SettlementData, out reason);

            var hunters = eventPhase == GamePhase.Hunt ? manager.ActiveHuntHunters : manager.SettlementData?.GetAvailableHunters();
            if (hunters != null)
                foreach (HunterInstance hunter in hunters)
                    if (PlayableEventOptionAvailability.CanUse(option, hunter, manager.SettlementData, out _))
                    {
                        reason = string.Empty;
                        return true;
                    }
            reason = "当前没有猎人满足该选项。";
            return false;
        }

        private void PrepareChoice(HunterInstance hunter)
        {
            if (huntPrompt == HuntPromptKind.Choice)
            {
                choiceSource?.TrySetResult(new HuntEventChoiceSelection(pendingOptionIndex, hunter));
                return;
            }
            transaction = manager.PrepareSettlementChoice(currentEvent, pendingOptionIndex, hunter);
            pendingOptionIndex = -1;
            if (transaction == null)
            {
                resultText = "这个选项现在无法结算。";
                return;
            }
            if (!transaction.RequiresCheck)
                CommitChoice();
        }

        private void CommitChoice()
        {
            EventResolutionResult result = transaction.Commit();
            if (eventPhase == GamePhase.Settlement)
                manager.SaveSettlementProgress();
            currentEvent = null;
            currentHunter = null;
            resultText = string.IsNullOrWhiteSpace(result.ResultText) ? (result.Success ? "判定成功。" : "判定失败。") : result.ResultText;
        }

        private void ContinueEventChain()
        {
            if (huntPrompt == HuntPromptKind.Result)
            {
                resultSource?.TrySetResult();
                return;
            }
            PlayableEventChoiceTransaction completed = transaction;
            transaction = null;
            resultText = string.Empty;
            completed?.Continue();
            ReleaseHuntInputIfIdle();
        }

        private void ResolveNarrative()
        {
            if (huntPrompt == HuntPromptKind.Narrative)
            {
                narrativeSource?.TrySetResult();
                return;
            }
            EventData resolved = currentEvent;
            currentEvent = null;
            currentHunter = null;
            manager.ResolveSettlementNarrative(resolved);
            if (eventPhase == GamePhase.Settlement)
                manager.SaveSettlementProgress();
            ReleaseHuntInputIfIdle();
        }

        private void ReleaseHuntInputIfIdle()
        {
            if (currentEvent != null || transaction != null || !string.IsNullOrEmpty(resultText)) return;
            PlayableHuntInputGuard.Release(huntInputOwnerId);
        }

        private void BeginHuntPrompt(HuntPromptKind prompt, EventData gameEvent, HunterInstance actor)
        {
            if (huntPrompt != HuntPromptKind.None) throw new System.InvalidOperationException("狩猎事件输入端口已经在处理另一项请求。");
            huntPrompt = prompt;
            eventPhase = manager != null ? manager.CurrentGamePhase : GamePhase.Settlement;
            currentEvent = gameEvent;
            currentHunter = actor;
            transaction = null;
            pendingOptionIndex = -1;
            resultText = string.Empty;
            PlayableHuntInputGuard.Acquire(huntInputOwnerId);
        }

        private void EndHuntPrompt(HuntPromptKind prompt)
        {
            if (huntPrompt != prompt) return;
            huntPrompt = HuntPromptKind.None;
            currentEvent = null;
            currentHunter = null;
            transaction = null;
            pendingOptionIndex = -1;
            resultText = string.Empty;
            PlayableHuntInputGuard.Release(huntInputOwnerId);
        }

        private static int GetCheckBonus(HunterInstance hunter, CheckType checkType)
        {
            if (hunter == null) return 0;
            return checkType switch
            {
                CheckType.Courage => hunter.Courage,
                CheckType.Luck => hunter.Luck,
                CheckType.Strength => hunter.Stats?.strength ?? 0,
                CheckType.Evasion => hunter.Stats?.evasion ?? 0,
                CheckType.Understanding => hunter.Understanding,
                _ => 0
            };
        }

        private static string GetCheckName(CheckType checkType)
        {
            return checkType switch
            {
                CheckType.Courage => "胆识",
                CheckType.Luck => "命运",
                CheckType.Strength => "力量",
                CheckType.Evasion => "敏捷",
                CheckType.Understanding => "知识",
                _ => "无属性"
            };
        }

        private void EnsureStyles()
        {
            if (windowStyle != null) return;

            windowTexture = new Texture2D(1, 1);
            windowTexture.SetPixel(0, 0, new Color(0.025f, 0.018f, 0.015f, 0.985f));
            windowTexture.Apply();
            windowStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(16, 16, 16, 16),
                normal = { background = windowTexture }
            };
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 23,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.96f, 0.76f, 0.36f) }
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                wordWrap = true,
                normal = { textColor = new Color(0.8f, 0.82f, 0.84f) }
            };
            resultStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                normal = { textColor = Color.white }
            };
        }

        private void OnDestroy()
        {
            narrativeSource?.TrySetCanceled();
            choiceSource?.TrySetCanceled();
            checkSource?.TrySetCanceled();
            resultSource?.TrySetCanceled();
            PlayableHuntInputGuard.Release(huntInputOwnerId);
            if (manager != null)
            {
                manager.ClearHuntEventInput(this);
            }
            if (windowTexture != null)
                Destroy(windowTexture);
        }
    }
}
