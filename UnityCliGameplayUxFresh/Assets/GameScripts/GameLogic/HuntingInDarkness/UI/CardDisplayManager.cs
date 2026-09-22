using System.Collections.Generic;
using Core;
using GameplayBase;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// 卡牌显示总调度器。纯 C# 类，由 GameManager 在 Start() 中构造并持有。
    /// 职责：
    ///   1. 为 Boss 创建 BossCardTable
    ///   2. 监听 CharacterSelectedEvent → 通过 CharacterEntity 注册表显隐角色面板，
    ///      并发布 BoardFocusChangedEvent 驱动相机
    ///
    /// 注：角色面板现由各自的 <see cref="CharacterEntity"/>（Prefab/程序化）持有，
    /// 本类不再创建角色展台，只负责选中切换与相机聚焦。
    /// </summary>
    public class CardDisplayManager
    {
        private readonly IGameContext _gameContext;
        private readonly Transform _parent;
        private readonly Vector3 _bossTablePosition;
        private readonly IReadOnlyDictionary<int, CharacterEntity> _characterEntities;

        private BossCombatPanel3D _bossTable;
        private int _activeCharacterId = -1;

        public CardDisplayManager(
            IGameContext gameContext,
            Transform parent,
            float tableHeightOffset,
            float tableScale,
            Vector3 bossTablePosition,
            IReadOnlyDictionary<int, CharacterEntity> characterEntities)
        {
            _gameContext       = gameContext;
            _parent            = parent;
            _bossTablePosition = bossTablePosition;
            _characterEntities = characterEntities;

            BuildBossTable();

            EventBus.Subscribe<CharacterSelectedEvent>(OnCharacterSelected);
            EventBus.Subscribe<CharacterDeselectedEvent>(OnCharacterDeselected);

            Debug.Log($"[CardDisplayManager] 初始化完成，共 {_characterEntities.Count} 个角色实体。");
        }

        public void Dispose()
        {
            EventBus.Unsubscribe<CharacterSelectedEvent>(OnCharacterSelected);
            EventBus.Unsubscribe<CharacterDeselectedEvent>(OnCharacterDeselected);
            _bossTable?.Dispose();
        }

        // ─── Boss 展台 ───

        private void BuildBossTable()
        {
            var go = new GameObject("CardTable_Boss");
            go.transform.SetParent(_parent);
            go.transform.position = _bossTablePosition;

            _bossTable = new BossCombatPanel3D(
                go.transform,
                _gameContext.Boss,
                _gameContext.BossHitLocationStates,
                _gameContext.BossRevealedCards);
        }

        // ═══════════════════════════════════════════
        // 事件处理
        // ═══════════════════════════════════════════

        private void OnCharacterSelected(CharacterSelectedEvent evt)
        {
            if (_activeCharacterId == evt.CharacterId)
            {
                HideAll();
                _activeCharacterId = -1;
                EventBus.Publish(new BoardFocusChangedEvent { HasFocus = false });
                return;
            }

            _activeCharacterId = evt.CharacterId;

            foreach (var kv in _characterEntities)
            {
                if (kv.Key == evt.CharacterId)
                {
                    kv.Value.ShowPanel();
                    Vector3 characterPosition = _gameContext.GetEntityWorldPosition(evt.CharacterId);
                    Vector3 bossPosition = _gameContext.GetEntityWorldPosition(_gameContext.Boss.Id);
                    EventBus.Publish(new BoardFocusChangedEvent
                    {
                        HasFocus               = true,
                        CharacterWorldPosition = characterPosition,
                        BossWorldPosition      = bossPosition
                    });
                }
                else
                {
                    kv.Value.HidePanel();
                }
            }
        }

        private void OnCharacterDeselected(CharacterDeselectedEvent _)
        {
            if (_activeCharacterId == -1) return;
            HideAll();
            _activeCharacterId = -1;
            EventBus.Publish(new BoardFocusChangedEvent { HasFocus = false });
        }

        private void HideAll()
        {
            foreach (var kv in _characterEntities)
                kv.Value.HidePanel();
        }
    }
}
