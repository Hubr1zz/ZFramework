using System.Collections.Generic;
using Core;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.Bootstrap;
using HuntingInDarkness.ViewLayer.Tabletop;
using UnityEngine;

namespace HuntingInDarkness.ViewLayer.Flow
{
    /// <summary>以世界空间卡牌协调存档入口与开场叙事，并在结束前隐藏后台营地表现。</summary>
    public sealed class PlayableOpeningSequence3D : MonoBehaviour
    {
        private readonly HashSet<GameObject> gatedPhaseRoots = new();
        private GameManager manager;
        private PlayableBootstrapSettings settings;
        private TabletopEventPanel3D panel;
        private bool checkingSave;
        private bool hasSave;
        private bool busy;
        private bool completed;
        private string statusText = string.Empty;

        public bool IsOpen => !completed && panel != null && panel.IsOpen;
        public bool IsCheckingSave => checkingSave;
        public bool HasSave => hasSave;
        private string GameTitle => string.IsNullOrWhiteSpace(settings?.GameTitle) ? "黑暗狩猎" : settings.GameTitle;

        public void Initialize(GameManager gameManager, PlayableBootstrapSettings bootstrapSettings)
        {
            manager = gameManager;
            settings = bootstrapSettings;
            if (manager == null || settings == null)
            {
                CompleteSequence();
                return;
            }

            manager.SettlementProgressLoadCompleted += OnLoadCompleted;
            panel = TabletopEventPanel3D.Create(transform);
            GateCurrentPhasePresentation();

            if (settings.ShowStartMenu)
            {
                checkingSave = true;
                PresentStartMenu();
                CheckSaveAsync().Forget();
                return;
            }
            if (settings.ShowFlowGuide && settings.ShowOpeningNarrative)
            {
                PresentOpeningNarrative();
                return;
            }
            CompleteSequence();
        }

        private async UniTaskVoid CheckSaveAsync()
        {
            try
            {
                hasSave = await manager.HasCampaignSaveAsync(this.GetCancellationTokenOnDestroy());
                statusText = hasSave ? "发现仍未结束的狩猎记录。" : "尚未留下任何狩猎记录。";
            }
            catch (System.OperationCanceledException)
            {
                return;
            }
            catch (System.Exception exception)
            {
                statusText = $"无法检查存档：{exception.Message}";
            }
            finally
            {
                checkingSave = false;
                if (this != null && !completed)
                    PresentStartMenu();
            }
        }

        private void PresentStartMenu()
        {
            if (panel == null || settings == null)
                return;

            var choices = new[]
            {
                new TabletopEventChoicePresentation("继续战役", "从最近一次营地记录继续。", hasSave && !checkingSave && !busy, hasSave ? string.Empty : "暂无可继续的记录", ContinueGame),
                new TabletopEventChoicePresentation("开始新战役", "从黑暗中的第一次苏醒开始。", !checkingSave && !busy, string.Empty, RequestNewGame)
            };
            string saveStatus = checkingSave ? "正在寻找狩猎记录……" : statusText;
            string body = string.IsNullOrWhiteSpace(settings.TitleTagline) ? saveStatus : $"{settings.TitleTagline}\n\n{saveStatus}";
            panel.Present(GetPanelAnchor(), GameTitle, body, "所有玩法操作都将在实体桌面上完成", TabletopEventPrimaryTone.Narrative, choices);
        }

        private void RequestNewGame()
        {
            if (busy || checkingSave)
                return;
            if (hasSave)
            {
                PresentNewGameConfirmation();
                return;
            }
            StartNewGameAsync().Forget();
        }

        private void PresentNewGameConfirmation()
        {
            var choices = new[]
            {
                new TabletopEventChoicePresentation("返回", "保留现有战役记录。", true, string.Empty, PresentStartMenu),
                new TabletopEventChoicePresentation("确认新战役", "删除现有记录并重新开始。", true, "这个决定无法撤回", () => StartNewGameAsync().Forget())
            };
            panel.Present(GetPanelAnchor(), "舍弃旧日记录？", "旧战役的营地、猎人和物资记录将被删除。", "确认前可以返回", TabletopEventPrimaryTone.Failure, choices);
        }

        private async UniTaskVoid StartNewGameAsync()
        {
            if (busy)
                return;

            busy = true;
            PresentBusy("正在抹去旧日记录……");
            try
            {
                await manager.DeleteCampaignSaveAsync(this.GetCancellationTokenOnDestroy());
                hasSave = false;
                busy = false;
                if (settings.ShowFlowGuide && settings.ShowOpeningNarrative)
                {
                    PresentOpeningNarrative();
                    return;
                }
                CompleteSequence();
            }
            catch (System.OperationCanceledException)
            {
            }
            catch (System.Exception exception)
            {
                busy = false;
                statusText = $"无法开始新战役：{exception.Message}";
                PresentStartMenu();
            }
        }

        private void ContinueGame()
        {
            if (manager == null || !hasSave || busy)
                return;
            busy = true;
            PresentBusy("正在唤回营地记录……");
            manager.LoadSettlementProgress();
        }

        private void OnLoadCompleted(bool success)
        {
            busy = false;
            if (success)
            {
                CompleteSequence();
                return;
            }
            statusText = "存档无法读取。你仍可以开始新战役。";
            hasSave = false;
            PresentStartMenu();
        }

        private void PresentOpeningNarrative()
        {
            var choices = new[]
            {
                new TabletopEventChoicePresentation("踏入黑暗", "记住同伴的名字，开始准备第一次狩猎。", true, "点击翻开营地", CompleteSequence)
            };
            panel.Present(GetPanelAnchor(), "黑暗中的苏醒", settings.OpeningNarrative, "营地事件将在这张卡收起后继续", TabletopEventPrimaryTone.Narrative, choices);
        }

        private void PresentBusy(string message)
        {
            panel.Present(GetPanelAnchor(), GameTitle, message, "请稍候", TabletopEventPrimaryTone.Check, System.Array.Empty<TabletopEventChoicePresentation>());
        }

        private Vector3 GetPanelAnchor() => manager != null ? manager.ResolveTabletopEventAnchor(null) + new Vector3(0f, 0.62f, -2.35f) : transform.position;

        private void LateUpdate()
        {
            if (!completed)
                GateCurrentPhasePresentation();
        }

        private void GateCurrentPhasePresentation()
        {
            GameObject phaseRoot = manager != null ? manager.TabletopPresentationRoot?.gameObject : null;
            if (phaseRoot == null)
                return;
            gatedPhaseRoots.Add(phaseRoot);
            if (phaseRoot.activeSelf)
                phaseRoot.SetActive(false);
        }

        private void CompleteSequence()
        {
            if (completed)
                return;
            completed = true;
            if (manager != null)
                manager.SettlementProgressLoadCompleted -= OnLoadCompleted;
            panel?.Close();
            RestoreCurrentPhasePresentation();
            enabled = false;
        }

        private void RestoreCurrentPhasePresentation()
        {
            GameObject currentRoot = manager != null ? manager.TabletopPresentationRoot?.gameObject : null;
            foreach (GameObject phaseRoot in gatedPhaseRoots)
                if (phaseRoot != null)
                    phaseRoot.SetActive(phaseRoot == currentRoot);
            gatedPhaseRoots.Clear();
        }

        private void OnDestroy()
        {
            if (manager != null)
                manager.SettlementProgressLoadCompleted -= OnLoadCompleted;
            RestoreCurrentPhasePresentation();
        }
    }
}
