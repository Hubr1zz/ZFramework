using System.Collections.Generic;
using Core;
using GameplayBase.Board;
using GameplayBase.Config;
using SO.Combat;
using UnityEngine;

namespace GameplayBase.CombatSystem
{
    /// <summary>战斗中一个组件（障碍物/可互动物体）的运行时实例。</summary>
    public class ComponentInstance
    {
        public int               EntityId;
        public CombatComponentSO  Template;
        public Vector2Int         Tile;
        public HexDirection       Facing;
    }

    /// <summary>BattleGenerator 的产出：装配好的战斗初始状态（纯数据，不含可视化）。</summary>
    public class BattleResult
    {
        public BoardManager                                board;
        public List<CharacterRuntimeData>                  characters = new();
        public BossRuntimeData                             boss;
        public Dictionary<int, CharacterActionCardInstance> allCards = new();
        public List<ComponentInstance>                     components = new();
    }

    /// <summary>
    /// 战斗生成器（纯 C#）。消费 <see cref="BattleSetup"/>，装配棋盘、猎人、Boss 和组件，
    /// 产出 <see cref="BattleResult"/> 交给 GameManager 接管可视化与流程。
    /// </summary>
    public static class BattleGenerator
    {
        public const int BossEntityId = 999;
        private const int ComponentIdStart = 1000;

        public static BattleResult Generate(BattleSetup setup, float cellSize, int fallbackRadius = 3)
        {
            if (setup == null)
            {
                Debug.LogError("[BattleGenerator] setup 为空，无法生成战斗。");
                return null;
            }

            var rules = setup.FieldRules;
            int mapRadius = rules != null ? Mathf.Max(1, rules.mapRadius) : Mathf.Max(1, fallbackRadius);

            var result = new BattleResult
            {
                board = new BoardManager(mapRadius, cellSize)
            };

            var ctx = new GenContext(result.board, mapRadius);

            PlaceHunters(setup, rules, result, ctx);
            PlaceBoss(setup, rules, result, ctx);
            PlaceFixedComponents(rules, result, ctx);
            PlaceDynamicComponents(rules, result, ctx);

            return result;
        }

        // ─── 猎人 ─────────────────────────────────────────────────────

        private static void PlaceHunters(
            BattleSetup setup, CombatFieldRulesSO rules, BattleResult result, GenContext ctx)
        {
            var squad = setup.HunterSquad ?? new List<CharacterConfigSO>();
            var slots = rules?.hunterSpawnSlots;

            int id = 1;
            for (int i = 0; i < squad.Count; i++)
            {
                var config = squad[i];
                if (config == null) continue;

                var character = new CharacterRuntimeData
                {
                    Id              = id,
                    Name            = config.characterName,
                    CombatStats     = config.combatStats?.CreateRuntimeCopy() ?? new CharacterCombatStats(),
                    EquippedWeapon  = config.startingWeapon,
                    Willpower       = Mathf.Max(0, config.startingWillpower),
                    CombatInspiration = Mathf.Max(0, config.startingCombatInspiration),
                    CharacterEntity = new Character()
                };

                foreach (var cardData in config.startingCards)
                {
                    var cardInstance = new CharacterActionCardInstance(cardData, character.Id);
                    character.AddCard(cardInstance);
                    result.allCards[cardInstance.InstanceId] = cardInstance;
                }

                // 出生位置来自场地规则；不足则自动找空格
                Vector2Int tile;
                HexDirection facing;
                if (slots != null && i < slots.Count)
                {
                    tile   = slots[i].tile;
                    facing = slots[i].facing;
                    if (!ctx.IsFreeForPlacement(tile))
                    {
                        Debug.LogWarning($"[BattleGenerator] 猎人#{id} 出生槽 {tile} 不可用，改为自动放置。");
                        tile = ctx.FindFreeTile();
                    }
                }
                else
                {
                    Debug.LogWarning($"[BattleGenerator] 猎人#{id} 无出生槽，自动放置。");
                    tile   = ctx.FindFreeTile();
                    facing = HexDirection.E;
                }

                result.board.PlaceEntity(id, tile, facing);
                ctx.MarkOccupied(tile);

                result.characters.Add(character);
                id++;
            }
        }

        // ─── Boss ─────────────────────────────────────────────────────

        private static void PlaceBoss(
            BattleSetup setup, CombatFieldRulesSO rules, BattleResult result, GenContext ctx)
        {
            result.boss = new BossRuntimeData
            {
                Id   = BossEntityId,
                Name = setup.Boss != null ? setup.Boss.bossName : "Boss"
            };

            var slot = rules != null ? rules.bossSpawnSlot : default;
            Vector2Int tile = slot.tile;
            if (rules == null || !ctx.IsFreeForPlacement(tile))
                tile = ctx.FindFreeTile();

            result.board.PlaceEntity(BossEntityId, tile, slot.facing);
            ctx.MarkOccupied(tile);
        }

        // ─── 固定组件池 ───────────────────────────────────────────────

        private static void PlaceFixedComponents(
            CombatFieldRulesSO rules, BattleResult result, GenContext ctx)
        {
            if (rules == null) return;
            int nextId = ComponentIdStart;

            foreach (var entry in rules.fixedComponents)
            {
                if (entry?.component == null) continue;
                if (!ctx.IsFreeForPlacement(entry.tile))
                {
                    Debug.LogWarning($"[BattleGenerator] 固定组件 {entry.component.Key} 目标格 {entry.tile} 不可用，跳过。");
                    continue;
                }
                SpawnComponent(result, ctx, ref nextId, entry.component, entry.tile, entry.facing);
            }
        }

        // ─── 动态组件池 ───────────────────────────────────────────────

        private static void PlaceDynamicComponents(
            CombatFieldRulesSO rules, BattleResult result, GenContext ctx)
        {
            if (rules == null) return;
            int nextId = ComponentIdStart + result.components.Count;

            foreach (var entry in rules.dynamicComponents)
            {
                if (entry?.component == null) continue;

                for (int n = 0; n < entry.count; n++)
                {
                    if (!TryResolveTile(entry, ctx, out var tile))
                    {
                        Debug.Log($"[BattleGenerator] 动态组件 {entry.component.Key} 找不到满足规则的位置，放弃剩余 {entry.count - n} 个。");
                        break;
                    }
                    SpawnComponent(result, ctx, ref nextId, entry.component, tile, HexDirection.E);
                }
            }
        }

        /// <summary>对一个动态项求所有规则候选格的交集，取一个落子。</summary>
        private static bool TryResolveTile(DynamicComponentEntry entry, GenContext ctx, out Vector2Int tile)
        {
            tile = default;

            // 无规则 → 任意空格
            if (entry.rules == null || entry.rules.Count == 0)
            {
                var free = ctx.GetFreeTiles();
                if (free.Count == 0) return false;
                tile = free[0];
                return true;
            }

            HashSet<Vector2Int> intersection = null;
            foreach (var rule in entry.rules)
            {
                if (rule == null) continue;
                if (!rule.TryResolveCandidates(ctx, out var cands) || cands == null || cands.Count == 0)
                    return false; // 任一规则无解 → 整体无解

                if (intersection == null) intersection = new HashSet<Vector2Int>(cands);
                else intersection.IntersectWith(cands);

                if (intersection.Count == 0) return false;
            }

            if (intersection == null || intersection.Count == 0) return false;

            foreach (var t in intersection)
            {
                if (ctx.IsFreeForPlacement(t)) { tile = t; return true; }
            }
            return false;
        }

        private static void SpawnComponent(
            BattleResult result, GenContext ctx, ref int nextId,
            CombatComponentSO template, Vector2Int tile, HexDirection facing)
        {
            var inst = new ComponentInstance
            {
                EntityId = nextId++,
                Template = template,
                Tile     = tile,
                Facing   = facing
            };
            result.board.PlaceEntity(inst.EntityId, tile, facing);
            ctx.RegisterComponent(template.Key, tile);
            result.components.Add(inst);
        }

        // ═══════════════════════════════════════════
        // 生成期上下文
        // ═══════════════════════════════════════════

        private class GenContext : IBattleGenContext
        {
            private readonly BoardManager _board;
            private readonly int _mapRadius;
            private readonly HashSet<Vector2Int> _occupied = new();
            private readonly Dictionary<string, List<Vector2Int>> _componentTiles = new();

            public GenContext(BoardManager board, int mapRadius)
            {
                _board = board;
                _mapRadius = mapRadius;
            }

            public IBoardQuery Board => _board;
            public int MapRadius => _mapRadius;

            public bool IsOccupied(Vector2Int tile) => _occupied.Contains(tile);

            public bool IsFreeForPlacement(Vector2Int tile) =>
                _board.IsValidTile(tile) && !_occupied.Contains(tile);

            public void MarkOccupied(Vector2Int tile) => _occupied.Add(tile);

            public void RegisterComponent(string key, Vector2Int tile)
            {
                MarkOccupied(tile);
                if (!_componentTiles.TryGetValue(key, out var list))
                    _componentTiles[key] = list = new List<Vector2Int>();
                list.Add(tile);
            }

            public IReadOnlyList<Vector2Int> GetPlacedComponentTiles(string componentKey) =>
                _componentTiles.TryGetValue(componentKey, out var list)
                    ? list : System.Array.Empty<Vector2Int>();

            public IReadOnlyList<Vector2Int> GetFreeTiles()
            {
                var all = _board.GetAllCoords();
                all.RemoveAll(t => _occupied.Contains(t));
                return all;
            }

            public Vector2Int FindFreeTile()
            {
                var free = GetFreeTiles();
                return free.Count > 0 ? free[0] : Vector2Int.zero;
            }
        }
    }
}
