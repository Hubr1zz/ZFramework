#if UNITY_EDITOR
using HuntingInDarkness.Data;
using UnityEditor;
using UnityEngine;

namespace HuntingInDarkness.Editor
{
    /// <summary>
    /// 编辑器工具：一键生成 M1 测试用 ScriptableObject 资产。
    /// 菜单路径：HuntingInDarkness → Generate Test Assets
    /// </summary>
    public static class TestAssetGenerator
    {
        private const string BasePath = "Assets/ScriptableObjects";

        [MenuItem("HuntingInDarkness/Generate Test Assets")]
        public static void GenerateAll()
        {
            GenerateResources();
            GenerateHunters();
            GenerateInventions();
            GenerateEvents();
            GenerateTiles();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[TestAssetGenerator] 测试资产生成完毕！");
            EditorUtility.DisplayDialog("完成", "M1 测试资产已生成到 Assets/ScriptableObjects/", "OK");
        }

        // ─── 资源 SO ─────────────────────────────────────────────

        private static void GenerateResources()
        {
            EnsureDir($"{BasePath}/Items");

            CreateItem("Bone",    ItemType.Resource, "骨骼",    ItemTag.Bone);
            CreateItem("Hide",    ItemType.Resource, "兽皮",    ItemTag.Hide);
            CreateItem("Stone",   ItemType.Resource, "石头",    ItemTag.Stone);
            CreateItem("Organ",   ItemType.Resource, "内脏",    ItemTag.Organ);
            CreateItem("Herb",    ItemType.Resource, "草药",    ItemTag.Herb);
            CreateItem("Wood",    ItemType.Resource, "木材",    ItemTag.Wood);

            // 基础武器
            var sword = CreateItem("BoneSword", ItemType.Weapon, "骨剑", ItemTag.Weapon);
            if (sword != null)
            {
                sword.weaponStats = new WeaponStats { speed = 1, power = 3, accuracy = 0, range = 1,
                    specialRule = "无特殊规则" };
                EditorUtility.SetDirty(sword);
            }

            // 基础防具
            var hide = CreateItem("HideArmor", ItemType.Armor, "兽皮护甲", ItemTag.Cloth);
            if (hide != null)
            {
                hide.armorStats = new ArmorStats { armorBody = 1, armorArms = 1 };
                EditorUtility.SetDirty(hide);
            }
        }

        private static ItemData CreateItem(string fileName, ItemType type, string displayName, ItemTag tag)
        {
            string path = $"{BasePath}/Items/{fileName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<ItemData>(path);
            if (existing != null) return existing;

            var item = ScriptableObject.CreateInstance<ItemData>();
            item.itemName = displayName;
            item.itemType = type;
            item.tags.Add(tag);
            AssetDatabase.CreateAsset(item, path);
            return item;
        }

        // ─── 猎人模板 SO ─────────────────────────────────────────

        private static void GenerateHunters()
        {
            EnsureDir($"{BasePath}/Hunters");

            CreateHunter("Hunter_Warrior",  "战士",    3, 0, 0, 5, 3);
            CreateHunter("Hunter_Scout",    "斥候",    1, 2, 1, 7, 2);
            CreateHunter("Hunter_Shaman",   "萨满",    1, 1, 0, 5, 2);
        }

        private static void CreateHunter(string fileName, string name,
            int str, int acc, int eva, int move, int willpower)
        {
            string path = $"{BasePath}/Hunters/{fileName}.asset";
            if (AssetDatabase.LoadAssetAtPath<HunterData>(path) != null) return;

            var h = ScriptableObject.CreateInstance<HunterData>();
            h.hunterName = name;
            h.initialWillpower = willpower;
            h.initialStats = new HunterCombatStats
                { strength = str, accuracy = acc, evasion = eva, movement = move };
            AssetDatabase.CreateAsset(h, path);
        }

        // ─── 发明树 SO ───────────────────────────────────────────

        private static void GenerateInventions()
        {
            EnsureDir($"{BasePath}/Settlement");

            // 第一层基础发明（无前置）
            var train    = CreateInvention("Inv_Training",    "训练",    InventionCategory.Basic,
                "猎人获得额外 +1 力量", null);
            var faith    = CreateInvention("Inv_Faith",       "信仰",    InventionCategory.Knowledge,
                "每年开始时，全员回复1点意志点", null);
            var paper    = CreateInvention("Inv_PaperAndPen", "纸和笔",  InventionCategory.Knowledge,
                "解锁事件记录；骰检+1", null);
            var fire     = CreateInvention("Inv_Fire",        "火",      InventionCategory.Basic,
                "工坊可制作骨制武器", null);
            var tools    = CreateInvention("Inv_Tools",       "工具",    InventionCategory.Crafting,
                "制作时间减少一半", null);
            var story    = CreateInvention("Inv_Story",       "故事",    InventionCategory.Knowledge,
                "年度事件后额外抽1张随机事件", null);

            // 第二层（需前置）
            if (train != null && fire != null)
            {
                CreateInvention("Inv_BoneWeapons", "骨制武器", InventionCategory.Crafting,
                    "解锁骨剑/骨盾配方", new[] { fire, tools });
            }
            if (faith != null && story != null)
            {
                CreateInvention("Inv_HolyBlessing", "神圣祝福", InventionCategory.Special,
                    "狩猎开始时，所有猎人意志点+1", new[] { faith, story });
            }
        }

        private static InventionData CreateInvention(string fileName, string name,
            InventionCategory cat, string effectDesc, InventionData[] prereqs)
        {
            string path = $"{BasePath}/Settlement/{fileName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<InventionData>(path);
            if (existing != null) return existing;

            var inv = ScriptableObject.CreateInstance<InventionData>();
            inv.inventionName      = name;
            inv.category           = cat;
            inv.effectDescription  = effectDesc ?? "";
            if (prereqs != null)
                foreach (var p in prereqs)
                    if (p != null) inv.prerequisites.Add(p);
            AssetDatabase.CreateAsset(inv, path);
            return inv;
        }

        // ─── 事件 SO ─────────────────────────────────────────────

        private static void GenerateEvents()
        {
            EnsureDir($"{BasePath}/Events");

            // 叙事事件
            CreateNarrativeEvent("Evt_FirstNight", "第一夜",
                "【第一夜】黑暗降临，营地升起第一堆篝火。幸存者们围坐，默默回忆逝去的同伴。",
                "所有猎人恢复1点意志点。",
                new[] { new EventEffect { effectType = EventEffectType.AddWillpower,
                    targetName = "all", value = 1, description = "全员+1意志点" } });

            // 抉择事件
            CreateChoiceEvent("Evt_StrangeRuin", "神秘废墟",
                "【神秘废墟】你们发现了一座废弃的雕像，上面刻满了奇怪的符文。",
                "废墟中有骨骼散落，似乎曾有人在此祭祀。");

            // 战斗事件（占位）
            CreateCombatEvent("Evt_BossEncounter", "怪物出没",
                "【怪物出没】地面震动，巨大的身影从林中走出…",
                "命运的决战时刻到来了。");
        }

        private static void CreateNarrativeEvent(string fileName, string name,
            string display, string hidden, EventEffect[] effects)
        {
            string path = $"{BasePath}/Events/{fileName}.asset";
            if (AssetDatabase.LoadAssetAtPath<EventData>(path) != null) return;

            var e = ScriptableObject.CreateInstance<EventData>();
            e.eventName   = name;
            e.eventType   = GameEventType.Narrative;
            e.displayText = display;
            e.hiddenText  = hidden;
            if (effects != null)
                foreach (var eff in effects) e.immediateEffects.Add(eff);
            AssetDatabase.CreateAsset(e, path);
        }

        private static void CreateChoiceEvent(string fileName, string name,
            string display, string hidden)
        {
            string path = $"{BasePath}/Events/{fileName}.asset";
            if (AssetDatabase.LoadAssetAtPath<EventData>(path) != null) return;

            var e = ScriptableObject.CreateInstance<EventData>();
            e.eventName   = name;
            e.eventType   = GameEventType.Choice;
            e.displayText = display;
            e.hiddenText  = hidden;

            // 选项1：进入废墟探索（勇气判定）
            e.options.Add(new EventOption
            {
                optionText   = "进入废墟探索",
                checkType    = CheckType.Courage,
                checkTarget  = 2,
                successText  = "你找到了一块骨骼，上面有奇异花纹。获得1骨骼。",
                failText     = "废墟内阴暗潮湿，精神受到轻微冲击。获得1压抑值。",
                successEffects = new System.Collections.Generic.List<EventEffect>
                {
                    new EventEffect { effectType = EventEffectType.AddResource,
                        targetName = "Bone", value = 1 }
                },
                failEffects = new System.Collections.Generic.List<EventEffect>
                {
                    new EventEffect { effectType = EventEffectType.AddInsanity,
                        targetName = "selected", value = 1 }
                }
            });

            // 选项2：绕道而行
            e.options.Add(new EventOption
            {
                optionText    = "绕道而行",
                checkType     = CheckType.None,
                successText   = "队伍平安绕过废墟，未有损失。",
                alwaysAvailable = true
            });

            AssetDatabase.CreateAsset(e, path);
        }

        private static void CreateCombatEvent(string fileName, string name,
            string display, string hidden)
        {
            string path = $"{BasePath}/Events/{fileName}.asset";
            if (AssetDatabase.LoadAssetAtPath<EventData>(path) != null) return;

            var e = ScriptableObject.CreateInstance<EventData>();
            e.eventName   = name;
            e.eventType   = GameEventType.Combat;
            e.displayText = display;
            e.hiddenText  = hidden;
            e.immediateEffects.Add(new EventEffect
                { effectType = EventEffectType.TriggerCombat, targetName = "DefaultBoss" });
            AssetDatabase.CreateAsset(e, path);
        }

        // ─── 地块 SO ─────────────────────────────────────────────

        private static void GenerateTiles()
        {
            EnsureDir($"{BasePath}/Hunt");

            CreateTile("Tile_Starting", "起始营地", TileType.Starting, 0);
            CreateTile("Tile_Plains",   "荒野平原", TileType.Plains,   3);
            CreateTile("Tile_Forest",   "幽暗森林", TileType.Forest,   2);
            CreateTile("Tile_Ruins",    "废弃废墟", TileType.Ruins,    1);
            CreateTile("Tile_Cave",     "黑暗洞穴", TileType.Cave,     1);
        }

        private static void CreateTile(string fileName, string name, TileType type, int weight)
        {
            string path = $"{BasePath}/Hunt/{fileName}.asset";
            if (AssetDatabase.LoadAssetAtPath<HexTileData>(path) != null) return;

            var t = ScriptableObject.CreateInstance<HexTileData>();
            t.tileName      = name;
            t.tileType      = type;
            t.spawnWeight   = weight;
            t.maxResourcePoints = type == TileType.Starting ? 0 : 2;
            t.bossEncounterWeight = type == TileType.Cave ? 3 : 0;
            t.description   = $"【{name}】地块描述占位符";

            // 给非起始地块加资源点池（等 ItemData 加载后填充）
            AssetDatabase.CreateAsset(t, path);
        }

        // ─── 工具 ────────────────────────────────────────────────

        private static void EnsureDir(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                var parts = path.Split('/');
                var current = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    var next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                        AssetDatabase.CreateFolder(current, parts[i]);
                    current = next;
                }
            }
        }
    }
}
#endif
