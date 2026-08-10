using System;
using System.Collections.Generic;
using Core;
using Cysharp.Threading.Tasks;
using GameplayBase;
using GameplayBase.CombatSystem;
using SO.Boss.ActionCard;
using SO.Boss.HitLocation;
using TMPro;
using UI;
using UnityEngine;

using Cards3D;

namespace CardTest3D
{
    /// <summary>
    /// 战斗测试场景引导（仅测试用）。
    /// 在 3DCardTest 场景中新建空 GameObject，挂上此脚本并在 Inspector 配置后 Play。
    /// 所有 SO 字段留空时自动使用内置示例数据。
    ///
    /// 布局：
    ///   左侧  — 猎人面板（行动卡 / 装备 / 数据槽）
    ///   右上  — Boss 行动卡抽牌堆 → 当前行动槽
    ///   右下  — Boss 部位卡抽牌堆 → 自动扩展部位区（抽出后翻正面动画）
    ///   中央  — 出牌区（点击行动卡模拟打出）
    /// </summary>
    public class CombatTestSetup : MonoBehaviour
    {
        // ─── Inspector 配置 ────────────────────────────────────────────────

        [Header("── 猎人 ──")]
        public string hunterName = "猎人·艾克";
        [Tooltip("行动卡 SO 列表，留空使用内置示例")]
        public List<CharacterActionCardData> hunterActionCards = new();
        [Tooltip("装备 SO 列表，留空则无装备")]
        public List<EquipmentCardData> hunterEquipment = new();

        [Header("── Boss ──")]
        public string bossName = "古龙";
        [Tooltip("Boss 行动卡（时点配置，无需效果；留空使用内置示例）")]
        public List<BossTestAction> bossActions = new();
        [Tooltip("Boss 部位卡 SO，留空使用内置示例")]
        public List<HitLocationCardData> bossHitLocations = new();

        [Serializable]
        public class BossTestAction
        {
            public string actionName  = "攻击";
            [TextArea(1, 2)] public string description = "—";
            public int timePointCost  = 2;
        }

        // ─── 运行时引用 ────────────────────────────────────────────────────

        HunterPanel3D _hunterPanel;
        Transform     _tableRoot;

        // 行动卡视图 → 运行时实例（用于点击翻面）
        readonly Dictionary<CardView3D, CharacterActionCardInstance> _actionCardInstances = new();
        readonly Dictionary<CardView3D, HitLocationRuntimeState>     _hitLocationStates       = new();

        // ─── 生命周期 ──────────────────────────────────────────────────────

        private void Start()
        {
            EnsureCamera();
            _tableRoot = CreateTable().transform;
            BuildHunterArea();
            BuildBossArea();
            BuildPlayZone();
        }

        // ─── 相机 ─────────────────────────────────────────────────────────

        private static void EnsureCamera()
        {
            if (Camera.main != null) return;
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = go.AddComponent<Camera>();
            cam.clearFlags      = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.08f, 0.10f);
            go.transform.position = new Vector3(0f, 9f, -4f);
            go.transform.rotation = Quaternion.Euler(62f, 0f, 0f);
        }

        // ─── 桌面 ─────────────────────────────────────────────────────────

        private static GameObject CreateTable()
        {
            var root = new GameObject("CombatTable");
            root.transform.position = Vector3.zero;
            var s = GameObject.CreatePrimitive(PrimitiveType.Plane);
            s.name = "TableSurface";
            s.transform.SetParent(root.transform, false);
            s.transform.localScale = new Vector3(2.2f, 1f, 2.2f);
            s.GetComponent<MeshRenderer>().material.color = new Color(0.15f, 0.13f, 0.11f);
            return root;
        }

        // ─── 猎人区域（左侧）────────────────────────────────────────────

        private void BuildHunterArea()
        {
            var figure = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            figure.name = "HunterFigure";
            figure.transform.SetParent(_tableRoot, false);
            figure.transform.localPosition = new Vector3(-7f, 0.5f, 0f);
            figure.GetComponent<MeshRenderer>().material.color = new Color(0.30f, 0.50f, 0.68f);
            MakeLabel(hunterName, _tableRoot, new Vector3(-7f, 0.01f, 1.5f), 0.16f);

            _hunterPanel = HunterPanel3D.Create(_tableRoot, new Vector3(-7f, 0.002f, 0f));

            // 行动卡
            var actionList = (hunterActionCards?.Count ?? 0) > 0
                ? hunterActionCards : MakeFallbackActionCards();
            foreach (var data in actionList)
            {
                if (data == null) continue;
                var inst = new CharacterActionCardInstance(data, 1);
                var view = CharacterActionCard.Create(inst, _tableRoot, Vector3.zero);
                view.ForceCategory(CardCategory.HunterAction);
                view.EnableDrag = true;
                view.OnClicked += OnActionCardClicked;   // 点击 → 模拟打出（翻面）
                _actionCardInstances[view] = inst;       // 记录实例，供翻面使用
                _hunterPanel.ActionGrid.TryPlaceCard(view);
            }

            // 装备卡
            if (hunterEquipment != null)
                foreach (var data in hunterEquipment)
                {
                    if (data == null) continue;
                    var card = EquipmentCard.Create(data, _tableRoot, Vector3.zero);
                    _hunterPanel.EquipGrid.TryPlaceCard(card);
                }
        }

        // ─── Boss 区域（右侧）────────────────────────────────────────────

        private void BuildBossArea()
        {
            MakeLabel(bossName, _tableRoot, new Vector3(6.5f, 0.01f, 6.5f), 0.22f);
            BuildBossActionArea();
            BuildHitLocationArea();
        }

        private void BuildBossActionArea()
        {
            MakeLabel("Boss 行动卡", _tableRoot, new Vector3(4.5f, 0.01f, 4.0f), 0.11f);

            // 当前行动槽（非堆叠，接受 BossAction）
            var actionSlot = CardSlot.Create(
                _tableRoot,
                _tableRoot.TransformPoint(new Vector3(6f, 0.002f, 2.5f)),
                CardView3D.CW + 0.10f, CardView3D.CH + 0.10f,
                false, CardCategory.BossAction);
            MakeLabel("当前行动", _tableRoot, new Vector3(6f, 0.01f, 3.4f), 0.09f);

            // 抽牌堆（堆叠，目标 = 当前行动槽）
            var drawPile = CardSlot.Create(
                _tableRoot,
                _tableRoot.TransformPoint(new Vector3(4f, 0.002f, 2.5f)),
                CardView3D.CW + 0.06f, CardView3D.CH + 0.06f,
                true);
            drawPile.TargetSlot = actionSlot;
            MakeLabel("抽取", _tableRoot, new Vector3(4f, 0.01f, 3.4f), 0.09f);

            // 压入 Boss 行动卡（反序使第一张在顶）
            var actions = (bossActions?.Count ?? 0) > 0
                ? bossActions : MakeFallbackBossActions();
            for (int i = actions.Count - 1; i >= 0; i--)
            {
                var a = actions[i];
                var data = ScriptableObject.CreateInstance<BossActionCardData>();
                data.actionName    = a.actionName;
                data.description   = a.description;
                data.timePointCost = a.timePointCost;
                var view = BossActionCard.Create(data, _tableRoot, Vector3.zero);
                view.ForceCategory(CardCategory.BossAction);
                drawPile.PushCard(view);
            }
        }

        private void BuildHitLocationArea()
        {
            MakeLabel("Boss 部位卡", _tableRoot, new Vector3(4.5f, 0.01f, -1.5f), 0.11f);

            // 扩展部位网格（单行，AutoExpand）
            var partGrid = SlotGrid.Create(
                _tableRoot, new Vector3(6.5f, 0.002f, -3.0f),
                1, 1,
                CardView3D.CW + 0.12f, CardView3D.CH + 0.12f, 0.08f,
                autoExpand: true, CardCategory.BossHitLocation);
            partGrid.AddLabel("已揭示部位");

            // 抽牌堆（堆叠，目标 = 扩展网格）
            var drawPile = CardSlot.Create(
                _tableRoot,
                _tableRoot.TransformPoint(new Vector3(3.5f, 0.002f, -3.0f)),
                CardView3D.CW + 0.06f, CardView3D.CH + 0.06f,
                true);
            drawPile.TargetGrid  = partGrid;
            drawPile.OnCardDrawn += OnHitLocationCardDrawn;
            MakeLabel("部位堆", _tableRoot, new Vector3(3.5f, 0.01f, -2.0f), 0.09f);

            // 填充部位卡（背面朝上）
            var parts = (bossHitLocations?.Count ?? 0) > 0
                ? bossHitLocations : MakeFallbackHitLocations();
            for (int i = parts.Count - 1; i >= 0; i--)
            {
                var p = parts[i];
                if (p == null) continue;
                var state = new HitLocationRuntimeState(p) { IsFaceUp = false };
                var view  = BossHitLocationCard.Create(state, _tableRoot, Vector3.zero);
                view.ForceCategory(CardCategory.BossHitLocation);
                _hitLocationStates[view] = state;
                drawPile.PushCard(view);
            }
        }

        // ─── 拖拽落牌区（可选：把卡从面板拖出后落在此处）────────────────────
        // 注意：点击行动卡 = 原地翻面（打出/恢复）；此区域仅供拖拽演示使用。

        private void BuildPlayZone()
        {
            MakeLabel("拖  拽  落  牌  区", _tableRoot, new Vector3(0f, 0.01f, -5.5f), 0.13f);
            const float w = 4.2f, h = 1.8f, T = 0.04f, D = 0.008f;
            float y = D * 0.5f + 0.001f;
            MakeZoneEdge("ZT", _tableRoot, new Vector3(0,      y,  h*0.5f       ), new Vector3(w+T, D, T));
            MakeZoneEdge("ZB", _tableRoot, new Vector3(0,      y, -h*0.5f - 0.6f), new Vector3(w+T, D, T));
            MakeZoneEdge("ZL", _tableRoot, new Vector3(-w*0.5f, y, -0.3f        ), new Vector3(T, D, h+0.6f));
            MakeZoneEdge("ZR", _tableRoot, new Vector3( w*0.5f, y, -0.3f        ), new Vector3(T, D, h+0.6f));
        }

        // ─── 回调 ─────────────────────────────────────────────────────────

        /// <summary>
        /// 点击猎人行动卡的正确处理：
        ///   正面朝上 → 模拟"打出"：执行效果（测试中为空）+ 翻面到背面
        ///   背面朝上 → 模拟"恢复"：翻回正面
        /// 卡牌始终留在面板槽内，不飞走。
        /// </summary>
        private void OnActionCardClicked(CardView3D card)
        {
            if (!_actionCardInstances.TryGetValue(card, out var inst)) return;
            if (card is not CharacterActionCard view) return;

            PlayOrRestoreCardAsync(view, inst).Forget();
        }

        private async UniTaskVoid PlayOrRestoreCardAsync(
            CharacterActionCard view,
            CharacterActionCardInstance inst)
        {
            // ── 翻面动画 0→90°（收起）──────────────────────────────────────
            const float half = 0.15f;
            for (float t = 0; t < half; t += Time.deltaTime)
            {
                view.transform.localEulerAngles =
                    new Vector3(Mathf.Lerp(0f, 90f, t / half), 0f, 0f);
                await UniTask.Yield(PlayerLoopTiming.Update, this.GetCancellationTokenOnDestroy());
            }

            // ── 切换面（打出→背面；恢复→正面）────────────────────────────
            var newFace = inst.CurrentFace == CardFace.FaceUp
                ? CardFace.FaceDown
                : CardFace.FaceUp;
            inst.SetFace(newFace);
            view.Refresh(inst);

            // ── 翻面动画 -90°→0°（展开）────────────────────────────────────
            for (float t = 0; t < half; t += Time.deltaTime)
            {
                view.transform.localEulerAngles =
                    new Vector3(Mathf.Lerp(-90f, 0f, t / half), 0f, 0f);
                await UniTask.Yield(PlayerLoopTiming.Update, this.GetCancellationTokenOnDestroy());
            }
            view.transform.localEulerAngles = Vector3.zero;
        }

        private void OnHitLocationCardDrawn(CardView3D card)
        {
            if (_hitLocationStates.TryGetValue(card, out var state))
                FlipHitLocationAsync(card as BossHitLocationCard, state).Forget();
        }

        private async UniTaskVoid FlipHitLocationAsync(
            BossHitLocationCard card,
            HitLocationRuntimeState state)
        {
            if (card == null) return;
            var cancellationToken = this.GetCancellationTokenOnDestroy();
            await UniTask.Delay(System.TimeSpan.FromSeconds(0.20f), cancellationToken: cancellationToken);
            const float dur = 0.15f;
            for (float t = 0; t < dur; t += Time.deltaTime)
            {
                card.transform.localEulerAngles =
                    new Vector3(Mathf.Lerp(0f, 90f, t / dur), 0f, 0f);
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
            state.IsFaceUp = true;
            card.Refresh();
            for (float t = 0; t < dur; t += Time.deltaTime)
            {
                card.transform.localEulerAngles =
                    new Vector3(Mathf.Lerp(-90f, 0f, t / dur), 0f, 0f);
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
            card.transform.localEulerAngles = Vector3.zero;
        }

        // ─── 工具 ─────────────────────────────────────────────────────────

        static void MakeLabel(string text, Transform parent,
            Vector3 localPos, float fontSize = 0.13f)
        {
            var go = new GameObject($"Lbl_{text}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text      = text;
            tmp.fontSize  = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color     = new Color(0.78f, 0.74f, 0.65f);
            tmp.rectTransform.sizeDelta = new Vector2(5f, 0.4f);
        }

        static void MakeZoneEdge(string n, Transform parent, Vector3 lp, Vector3 sc)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = n;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = lp;
            go.transform.localScale    = sc;
            Destroy(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().material.color = new Color(0.48f, 0.44f, 0.34f);
        }

        // ─── Fallback 示例数据 ────────────────────────────────────────────

        static List<CharacterActionCardData> MakeFallbackActionCards()
        {
            var list = new List<CharacterActionCardData>();
            (string n, string d, int c)[] items = {
                ("突袭",   "快速近身攻击。",     1),
                ("蓄力斩", "重击，命中部位。",   3),
                ("闪避",   "规避一次攻击。",     2),
                ("治疗草", "恢复 1 点临时伤。",  1),
                ("观察",   "翻开 1 张部位卡。",  0),
                ("陷阱",   "布置一个伤害格。",   2),
            };
            foreach (var (n, d, c) in items)
            {
                var data = ScriptableObject.CreateInstance<CharacterActionCardData>();
                data.cardName          = n;
                data.faceUpDescription = d;
                data.timePointCost     = c;
                list.Add(data);
            }
            return list;
        }

        static List<BossTestAction> MakeFallbackBossActions() => new()
        {
            new() { actionName = "爪击", description = "对前方角色造成 2 伤。",    timePointCost = 2 },
            new() { actionName = "咆哮", description = "所有角色 -1 时点。",       timePointCost = 1 },
            new() { actionName = "重踏", description = "范围攻击，命中所有角色。", timePointCost = 4 },
            new() { actionName = "吞噬", description = "指定角色陷入「濒死」。",   timePointCost = 5 },
        };

        static List<HitLocationCardData> MakeFallbackHitLocations()
        {
            var list = new List<HitLocationCardData>();
            (string n, string d, int hp, int t)[] items = {
                ("头部", "要害，命中获额外效果。", 2, 2),
                ("躯干", "高韧性防护区域。",       5, 4),
                ("尾部", "可削断，阻止扫尾。",     3, 2),
                ("前爪", "攻击主要来源。",          3, 3),
            };
            foreach (var (n, d, hp, t) in items)
            {
                var data = ScriptableObject.CreateInstance<HitLocationCardData>();
                data.locationName = n;
                data.description  = d;
                data.maxHp        = hp;
                data.toughness    = t;
                list.Add(data);
            }
            return list;
        }
    }
}
