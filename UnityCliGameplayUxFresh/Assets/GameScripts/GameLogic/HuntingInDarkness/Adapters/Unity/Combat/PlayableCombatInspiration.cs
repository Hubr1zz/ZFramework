using System.Collections.Generic;
using System;
using Core;
using GameplayBase;
using GameplayBase.CombatSystem;
using HuntingInDarkness.GameCore.Cards;
using SO.Boss.ActionCard;
using UnityEngine;

namespace HuntingInDarkness.Combat
{
    public interface ICombatInspirationReadModel
    {
        IReadOnlyList<CombatInspirationToken> GetCombatInspirationTokens(int characterId);
        int GetCombatInspirationCapacity(int characterId);
    }

    public struct CombatInspirationChangedEvent
    {
        public int CharacterId;
        public int OldCount;
        public int NewCount;
    }

    public static class CombatInspirationPresentation
    {
        public static string GetName(CombatInspirationColor color)
        {
            return color switch
            {
                CombatInspirationColor.Red => "红·残暴",
                CombatInspirationColor.Blue => "蓝·精湛",
                CombatInspirationColor.Yellow => "黄·速度",
                _ => "未知灵感"
            };
        }
    }

    public sealed class PlayableShowdownGuidanceView : MonoBehaviour
    {
        private Action endWindup;
        private Func<IReadOnlyList<BossActionCardData>> getCards;
        private BossActionStage stage = BossActionStage.Windup;
        private TurnPhase phase = TurnPhase.PlayerTurn;
        private string notice = string.Empty;
        private float noticeUntil;

        public static PlayableShowdownGuidanceView Create(Transform parent, Action endWindup, Func<IReadOnlyList<BossActionCardData>> getCards)
        {
            var gameObject = new GameObject("ShowdownGuidance");
            gameObject.transform.SetParent(parent, false);
            var view = gameObject.AddComponent<PlayableShowdownGuidanceView>();
            view.endWindup = endWindup;
            view.getCards = getCards;
            return view;
        }

        private void OnEnable()
        {
            EventBus.Subscribe<TurnPhaseChangedEvent>(OnPhaseChanged);
            EventBus.Subscribe<BossActionStageChangedEvent>(OnStageChanged);
            EventBus.Subscribe<CombatGuidanceNoticeEvent>(OnNotice);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<TurnPhaseChangedEvent>(OnPhaseChanged);
            EventBus.Unsubscribe<BossActionStageChangedEvent>(OnStageChanged);
            EventBus.Unsubscribe<CombatGuidanceNoticeEvent>(OnNotice);
        }

        private void OnGUI()
        {
            IReadOnlyList<BossActionCardData> cards = getCards?.Invoke();
            BossActionCardData card = cards != null && cards.Count > 0 ? cards[0] : null;
            string title = stage switch
            {
                BossActionStage.Execution => "Boss 执行",
                BossActionStage.Recovery => "Boss 后摇",
                _ => "猎人前摇"
            };
            string instruction = stage switch
            {
                BossActionStage.Execution => "按行动卡提示操控 Boss 落点与目标。",
                BossActionStage.Recovery => "查看结算结果并确认，随后刷新下一张行动卡。",
                _ => "选择猎人和行动卡。累计 TP 最高者须等待队友追上。"
            };

            GUILayout.BeginArea(new Rect(16f, 16f, 440f, 190f), GUI.skin.box);
            GUILayout.Label($"{title}　|　{(card != null ? card.actionName : "等待行动卡")}");
            if (card != null)
            {
                GUILayout.Label($"前摇上限：{Mathf.Max(1, card.timePointCost)} TP");
                GUILayout.Label(stage == BossActionStage.Windup ? card.windupDescription : instruction);
            }
            else
            {
                GUILayout.Label(instruction);
            }
            if (phase == TurnPhase.PlayerTurn && stage == BossActionStage.Windup && GUILayout.Button("结束全队前摇（未使用 TP 不保留）", GUILayout.Height(34f)))
                endWindup?.Invoke();
            if (!string.IsNullOrWhiteSpace(notice) && Time.unscaledTime < noticeUntil)
                GUILayout.Label($"提示：{notice}");
            GUILayout.EndArea();
        }

        private void OnPhaseChanged(TurnPhaseChangedEvent evt) => phase = evt.NewPhase;
        private void OnStageChanged(BossActionStageChangedEvent evt) => stage = evt.Stage;
        private void OnNotice(CombatGuidanceNoticeEvent evt)
        {
            notice = evt.Message;
            noticeUntil = Time.unscaledTime + 4f;
        }
    }
}
