using Core;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace HuntingInDarkness.ViewLayer.Flow
{
    /// <summary>可游玩组合根的战役入口，只协调存档 Adapter 与 GameManager 公开命令。</summary>
    public sealed class PlayableStartMenu : MonoBehaviour
    {
        private const int WindowId = 68020;
        private const float MenuWidth = 520f;
        private const float MenuHeight = 440f;
        private GameManager manager;
        private string gameTitle;
        private string titleTagline;
        private string statusText;
        private bool visible = true;
        private bool checkingSave = true;
        private bool hasSave;
        private bool busy;
        private bool confirmNewGame;
        private GUIStyle backgroundStyle;
        private GUIStyle titleStyle;
        private GUIStyle taglineStyle;
        private GUIStyle statusStyle;
        private Texture2D backgroundTexture;

        public void Initialize(GameManager gameManager, string title, string tagline)
        {
            manager = gameManager;
            gameTitle = string.IsNullOrWhiteSpace(title) ? "黑暗狩猎" : title;
            titleTagline = tagline ?? string.Empty;
            if (manager != null)
                manager.SettlementProgressLoadCompleted += OnLoadCompleted;
            CheckSaveAsync().Forget();
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
            }
        }

        private void OnGUI()
        {
            if (!visible) return;

            EnsureStyles();
            int previousDepth = GUI.depth;
            GUI.depth = -1000;
            GUI.ModalWindow(WindowId, new Rect(0f, 0f, Screen.width, Screen.height), DrawWindow, GUIContent.none, backgroundStyle);
            GUI.depth = previousDepth;
        }

        private void DrawWindow(int windowId)
        {
            var menuRect = new Rect((Screen.width - MenuWidth) * 0.5f, (Screen.height - MenuHeight) * 0.5f, MenuWidth, MenuHeight);
            GUILayout.BeginArea(menuRect);
            GUILayout.FlexibleSpace();
            GUILayout.Label(gameTitle, titleStyle);
            GUILayout.Space(12f);
            GUILayout.Label(titleTagline, taglineStyle);
            GUILayout.FlexibleSpace();

            if (confirmNewGame)
            {
                GUILayout.Label("开始新战役会删除现有存档。这个决定无法撤回。", statusStyle);
                GUILayout.Space(10f);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("返回", GUILayout.Height(44f)))
                    confirmNewGame = false;
                GUI.enabled = !busy;
                if (GUILayout.Button("确认，开始新战役", GUILayout.Height(44f)))
                    StartNewGameAsync().Forget();
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Label(checkingSave ? "正在寻找狩猎记录……" : statusText, statusStyle);
                GUILayout.Space(10f);
                GUI.enabled = hasSave && !checkingSave && !busy;
                if (GUILayout.Button(busy ? "正在唤回记忆……" : hasSave ? "继续战役" : "暂无可继续的战役", GUILayout.Height(48f)))
                    ContinueGame();
                GUI.enabled = !busy && !checkingSave;
                if (GUILayout.Button("开始新战役", GUILayout.Height(44f)))
                {
                    if (hasSave)
                        confirmNewGame = true;
                    else
                        StartNewGameAsync().Forget();
                }
                GUI.enabled = true;
            }

            GUILayout.Space(18f);
            GUILayout.Label("每次返回营地都会自动保存；发明与制造会立即保存。", statusStyle);
            GUILayout.EndArea();
        }

        private void ContinueGame()
        {
            if (manager == null || !hasSave || busy) return;
            busy = true;
            statusText = "正在读取战役……";
            manager.LoadSettlementProgress();
        }

        private async UniTaskVoid StartNewGameAsync()
        {
            if (busy) return;
            busy = true;
            try
            {
                await manager.DeleteCampaignSaveAsync(this.GetCancellationTokenOnDestroy());
                visible = false;
            }
            catch (System.OperationCanceledException)
            {
                return;
            }
            catch (System.Exception exception)
            {
                statusText = $"无法开始新战役：{exception.Message}";
                confirmNewGame = false;
            }
            finally
            {
                busy = false;
            }
        }

        private void OnLoadCompleted(bool success)
        {
            busy = false;
            if (!success)
            {
                statusText = "存档无法读取。你仍可以开始新战役。";
                return;
            }

            FindAnyObjectByType<PlayableFlowGuide>()?.SkipOpeningNarrative();
            visible = false;
        }

        private void EnsureStyles()
        {
            if (backgroundStyle != null) return;

            backgroundTexture = new Texture2D(1, 1);
            backgroundTexture.SetPixel(0, 0, new Color(0.018f, 0.015f, 0.018f, 1f));
            backgroundTexture.Apply();
            backgroundStyle = new GUIStyle(GUI.skin.box) { normal = { background = backgroundTexture } };
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 46,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.96f, 0.76f, 0.36f) }
            };
            taglineStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 19,
                wordWrap = true,
                normal = { textColor = new Color(0.82f, 0.84f, 0.86f) }
            };
            statusStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                wordWrap = true,
                normal = { textColor = new Color(0.66f, 0.68f, 0.72f) }
            };
        }

        private void OnDestroy()
        {
            if (manager != null)
                manager.SettlementProgressLoadCompleted -= OnLoadCompleted;
            if (backgroundTexture != null)
                Destroy(backgroundTexture);
        }
    }
}
