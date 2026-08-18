using System.Collections.Generic;
using Core;
using Cysharp.Threading.Tasks;
using GameplayBase;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Settlement;
using HuntingInDarkness.Settlement;
using HuntingInDarkness.Combat;
using UnityEngine;

namespace HuntingInDarkness.ViewLayer.Settlement
{
    /// <summary>
    /// 可游玩组合根的营地流程 HUD。只读取 Settlement 数据并提交公开命令，
    /// 现有 SettlementManager 仍是出发校验与状态变更的唯一权威。
    /// </summary>
    public sealed class PlayableSettlementHud : MonoBehaviour
    {
        private enum SettlementPage
        {
            Camp,
            Workshop,
            Annals
        }

        private const int MaximumSquadSize = DepartureRules.MaximumHunters;
        private const int EventWindowId = 68021;
        private readonly HashSet<int> selectedHunterIds = new();
        private GameManager manager;
        private float panelWidth;
        private Vector2 scrollPosition;
        private bool selectionInitialized;
        private SettlementPage currentPage;
        private string progressionResult;
        private EventData currentEvent;
        private HunterInstance currentEventHunter;
        private string eventResult;
        private GUIStyle panelStyle;
        private GUIStyle titleStyle;
        private GUIStyle sectionStyle;
        private GUIStyle mutedStyle;
        private Texture2D panelTexture;
        private bool weaponTrainingInProgress;

        public event System.Action<List<int>> DepartureRequested;

        public void Initialize(GameManager gameManager, float width)
        {
            manager = gameManager;
            panelWidth = Mathf.Max(320f, width);
        }

        private void OnGUI()
        {
            if (manager == null || manager.CurrentGamePhase != GamePhase.Settlement) return;

            var data = manager.SettlementData;
            if (data == null) return;

            EnsureStyles();
            EnsureSelection(data);
            PruneSelection(data);

            float width = Mathf.Min(panelWidth, Screen.width - 32f);
            var area = new Rect(Screen.width - width - 16f, 16f, width, Screen.height - 32f);
            GUILayout.BeginArea(area, panelStyle);
            GUILayout.Label($"第 {data.CurrentYear} 年 · 无火营地", titleStyle);
            GUILayout.Label($"本年狩猎 {data.HuntsCompletedThisYear}/{Mathf.Max(1, data.HuntsPerYear)}  ·  总计 {data.HuntHistory.Count} 次  ·  年鉴 {data.Timeline.Count} 条", mutedStyle);
            DrawPageTabs();
            GUILayout.Space(10f);

            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            switch (currentPage)
            {
                case SettlementPage.Workshop:
                    DrawProgression(data);
                    break;
                case SettlementPage.Annals:
                    DrawAnnals(data);
                    break;
                default:
                    DrawResources(data);
                    GUILayout.Space(12f);
                    DrawHunters(data);
                    break;
            }
            GUILayout.EndScrollView();

            if (currentPage == SettlementPage.Camp)
            {
                GUILayout.Space(10f);
                DrawDeparture(data);
            }
            GUILayout.EndArea();

            if (currentEvent != null || !string.IsNullOrEmpty(eventResult))
                GUI.ModalWindow(EventWindowId, GetEventWindowRect(), DrawEventWindow, "营地事件", panelStyle);
        }

        private void DrawPageTabs()
        {
            GUILayout.BeginHorizontal();
            DrawPageTab(SettlementPage.Camp, "营地");
            DrawPageTab(SettlementPage.Workshop, "发明与工坊");
            DrawPageTab(SettlementPage.Annals, "年鉴");
            GUILayout.EndHorizontal();
        }

        private void DrawPageTab(SettlementPage page, string label)
        {
            bool selected = currentPage == page;
            if (GUILayout.Toggle(selected, label, GUI.skin.button, GUILayout.Height(30f)) && !selected)
            {
                currentPage = page;
                scrollPosition = Vector2.zero;
                progressionResult = string.Empty;
            }
        }

        private Rect GetEventWindowRect()
        {
            float width = Mathf.Min(560f, Screen.width - 48f);
            float height = Mathf.Min(420f, Screen.height - 48f);
            return new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
        }

        private void DrawEventWindow(int windowId)
        {
            GUILayout.Space(8f);
            if (!string.IsNullOrEmpty(eventResult))
            {
                GUILayout.Label(eventResult, sectionStyle);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("记入年鉴", GUILayout.Height(38f)))
                    eventResult = string.Empty;
                return;
            }

            if (currentEvent == null) return;
            GUILayout.Label(currentEvent.eventName, titleStyle);
            if (currentEventHunter != null)
                GUILayout.Label($"与 {currentEventHunter.Name} 有关", mutedStyle);
            GUILayout.Space(8f);
            GUILayout.Label(currentEvent.displayText, mutedStyle);
            GUILayout.FlexibleSpace();

            if (currentEvent.eventType == GameEventType.Choice)
            {
                for (int i = 0; i < currentEvent.options.Count; i++)
                {
                    int optionIndex = i;
                    if (GUILayout.Button(currentEvent.options[i].optionText, GUILayout.Height(36f)))
                        ResolveChoice(optionIndex);
                }
                return;
            }

            if (GUILayout.Button("接受结果", GUILayout.Height(40f)))
                ResolveNarrative();
        }

        private void ShowEvent(EventData gameEvent, HunterInstance hunter)
        {
            if (manager == null || manager.CurrentGamePhase != GamePhase.Settlement) return;
            if (FindAnyObjectByType<PlayableSettlementEventView>() != null) return;
            currentEvent = gameEvent;
            currentEventHunter = hunter;
            eventResult = string.Empty;
        }

        private void ResolveNarrative()
        {
            var resolvedEvent = currentEvent;
            currentEvent = null;
            currentEventHunter = null;
            manager.ResolveSettlementNarrative(resolvedEvent);
        }

        private void ResolveChoice(int optionIndex)
        {
            var resolvedEvent = currentEvent;
            var resolvedHunter = currentEventHunter;
            currentEvent = null;
            currentEventHunter = null;
            var result = manager.ResolveSettlementChoice(resolvedEvent, optionIndex, resolvedHunter);
            if (currentEvent == null)
                eventResult = result.ResultText;
        }

        private void DrawResources(SettlementInstance data)
        {
            GUILayout.Label("营地库存", sectionStyle);
            if (data.Resources.Count == 0)
            {
                GUILayout.Label("尚未带回任何物资。", mutedStyle);
                return;
            }

            GUILayout.BeginHorizontal();
            for (int i = 0; i < data.Resources.Count; i++)
            {
                if (i > 0 && i % 3 == 0)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                }
                var resource = data.Resources[i];
                GUILayout.Label($"{resource.Key}  ×{resource.Value}", GUI.skin.box, GUILayout.MinWidth(92f));
            }
            GUILayout.EndHorizontal();
        }

        private void DrawProgression(SettlementInstance data)
        {
            var inventionSystem = manager.SettlementInventions;
            var workshopSystem = manager.SettlementWorkshop;
            if (inventionSystem == null || workshopSystem == null)
            {
                GUILayout.Label("营地成长系统尚未初始化。", mutedStyle);
                return;
            }

            GUILayout.Label("发明", sectionStyle);
            GUILayout.Label("发明决定营地学会什么，并解锁对应的生产能力。", mutedStyle);
            foreach (var invention in inventionSystem.AllInventions)
                DrawInvention(inventionSystem, data, invention);

            DrawWeaponTraining(data);

            GUILayout.Space(12f);
            GUILayout.Label("加工台", sectionStyle);
            GUILayout.Label("把狩猎带回的材料转化为长期库存。", mutedStyle);
            foreach (var recipe in workshopSystem.AllRecipes)
                DrawRecipe(workshopSystem, data, recipe);

            GUILayout.Space(12f);
            DrawEquipmentStorage(data);

            if (!string.IsNullOrEmpty(progressionResult))
            {
                GUILayout.Space(10f);
                GUILayout.Label(progressionResult, GUI.skin.box);
            }
        }

        private void DrawWeaponTraining(SettlementInstance data)
        {
            PlayableWeaponMasteryCatalog catalog = PlayableWeaponMasteryRuntime.Catalog;
            if (catalog == null || !data.IsInventionUnlocked(catalog.TrainingInventionName)) return;
            GUILayout.Space(12f);
            GUILayout.Label("武器训练", sectionStyle);
            GUILayout.Label($"消耗 {catalog.TrainingCostItem.itemName} ×{catalog.TrainingCost}，为一名可出战猎人提升 {catalog.TrainingExperience} 点指定流派熟练度。", mutedStyle);
            foreach (HunterInstance hunter in data.GetAvailableHunters())
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label(hunter.Name, sectionStyle);
                GUILayout.BeginHorizontal();
                foreach (WeaponMasteryFamilyDefinition family in catalog.GetFamilies())
                {
                    bool canTrain = manager.CanTrainWeapon(hunter.InstanceId, family.Id, out string reason);
                    GUI.enabled = canTrain && !weaponTrainingInProgress;
                    if (GUILayout.Button(canTrain ? $"训练{family.DisplayName}" : reason, GUILayout.Height(32f)))
                        TrainWeaponAsync(hunter, family.Id).Forget();
                    GUI.enabled = true;
                }
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }
        }

        private async UniTaskVoid TrainWeaponAsync(HunterInstance hunter, string masteryId)
        {
            if (weaponTrainingInProgress || hunter == null) return;
            weaponTrainingInProgress = true;
            try
            {
                var result = await manager.TrainWeaponAsync(hunter.InstanceId, masteryId);
                progressionResult = result.Success ? $"{hunter.Name} 的{result.MasteryOutcome.MasteryName}熟练度提升至 {result.MasteryOutcome.NewValue}。" : result.Reason;
            }
            finally
            {
                weaponTrainingInProgress = false;
            }
        }

        private void DrawInvention(InventionSystem inventionSystem, SettlementInstance data, InventionData invention)
        {
            if (invention == null) return;

            bool unlocked = inventionSystem.IsUnlocked(invention);
            bool canUnlock = inventionSystem.CanUnlock(invention, out string reason);
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"{(unlocked ? "✓" : "○")} {invention.inventionName}", sectionStyle);
            GUILayout.Label(invention.description, mutedStyle);
            if (unlocked)
            {
                GUILayout.Label($"已解锁：{invention.effectDescription}", mutedStyle);
            }
            else
            {
                GUILayout.Label($"消耗：{FormatInventionCosts(invention, data)}", mutedStyle);
                GUI.enabled = canUnlock;
                if (GUILayout.Button(canUnlock ? "投入资源，完成发明" : reason, GUILayout.Height(34f)) && inventionSystem.TryUnlock(invention))
                {
                    progressionResult = $"营地已经掌握“{invention.inventionName}”。";
                    manager.SaveSettlementProgress();
                }
                GUI.enabled = true;
            }
            GUILayout.EndVertical();
        }

        private void DrawRecipe(WorkshopSystem workshopSystem, SettlementInstance data, CraftRecipe recipe)
        {
            if (recipe == null) return;

            bool unlocked = workshopSystem.IsRecipeUnlocked(recipe);
            bool canCraft = workshopSystem.CanCraft(recipe, out string reason);
            string outputName = recipe.outputItem != null ? recipe.outputItem.itemName : "未配置产物";
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"{(unlocked ? "◆" : "◇")} {recipe.recipeName}", sectionStyle);
            GUILayout.Label($"{FormatRecipeCosts(recipe, data)}  →  {outputName} ×{recipe.outputCount}", mutedStyle);
            GUI.enabled = canCraft;
            if (GUILayout.Button(canCraft ? "制造" : reason, GUILayout.Height(34f)))
            {
                var output = workshopSystem.TryCraft(recipe);
                if (output.Count > 0)
                {
                    progressionResult = $"制造完成：{outputName} ×{output.Count}";
                    manager.SaveSettlementProgress();
                }
            }
            GUI.enabled = true;
            GUILayout.EndVertical();
        }

        private void DrawEquipmentStorage(SettlementInstance data)
        {
            GUILayout.Label("装备仓库", sectionStyle);
            GUILayout.Label("制造完成的装备可以分配给猎人；死亡或卸下时会回到仓库。", mutedStyle);
            bool hasEquipment = false;
            foreach (var item in PlayableSettlementItemRegistry.Items)
            {
                if (item == null || item.itemType == ItemType.Resource) continue;
                int count = data.GetStoredEquipment(item.itemName);
                if (count <= 0) continue;

                hasEquipment = true;
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label($"{item.itemName} ×{count}", sectionStyle);
                GUILayout.Label(GetEquipmentDescription(item), mutedStyle);
                foreach (var hunter in data.GetAvailableHunters())
                {
                    bool canEquip = PlayableEquipmentRules.CanEquip(hunter, item, out string reason);
                    GUI.enabled = canEquip && data.GetStoredEquipment(item.itemName) > 0;
                    if (GUILayout.Button(canEquip ? $"装备给 {hunter.Name}" : $"{hunter.Name}：{reason}", GUILayout.Height(30f)) && manager.SettlementHunters.EquipItem(hunter, item))
                    {
                        progressionResult = $"{hunter.Name} 装备了 {item.itemName}。";
                        manager.SaveSettlementProgress();
                    }
                    GUI.enabled = true;
                }
                GUILayout.EndVertical();
            }

            if (!hasEquipment)
                GUILayout.Label("仓库里还没有可分配的装备。", mutedStyle);
        }

        private static string GetEquipmentDescription(ItemData item)
        {
            if (item.itemType == ItemType.Weapon && item.weaponStats != null)
                return $"武器 · 速度 {item.weaponStats.speed} · 威力 {item.weaponStats.power} · 精准 {item.weaponStats.accuracy} · 范围 {item.weaponStats.range}";
            if (item.itemType == ItemType.Armor && item.armorStats != null)
            {
                var parts = new List<string>();
                if (item.armorStats.armorHead > 0) parts.Add($"头部 +{item.armorStats.armorHead}");
                if (item.armorStats.armorBody > 0) parts.Add($"躯干 +{item.armorStats.armorBody}");
                if (item.armorStats.armorArms > 0) parts.Add($"手臂 +{item.armorStats.armorArms}");
                if (item.armorStats.armorLegs > 0) parts.Add($"腿部 +{item.armorStats.armorLegs}");
                return parts.Count > 0 ? $"防具 · {string.Join(" · ", parts)}" : "防具 · 未配置保护部位";
            }
            return item.description;
        }

        private static string FormatInventionCosts(InventionData invention, SettlementInstance data)
        {
            if (invention.costs.Count == 0) return "无";

            var labels = new List<string>();
            foreach (var cost in invention.costs)
                if (cost?.resource != null)
                    labels.Add($"{cost.resource.itemName} {data.GetResource(cost.resource.itemName)}/{cost.count}");
            return labels.Count > 0 ? string.Join("、", labels) : "无";
        }

        private static string FormatRecipeCosts(CraftRecipe recipe, SettlementInstance data)
        {
            if (recipe.ingredients.Count == 0) return "无需材料";

            var labels = new List<string>();
            foreach (var ingredient in recipe.ingredients)
                if (ingredient?.item != null)
                    labels.Add($"{ingredient.item.itemName} {data.GetResource(ingredient.item.itemName)}/{ingredient.count}");
            return labels.Count > 0 ? string.Join("、", labels) : "无需材料";
        }

        private void DrawAnnals(SettlementInstance data)
        {
            GUILayout.Label("狩猎记录", sectionStyle);
            if (data.HuntHistory.Count == 0)
                GUILayout.Label("尚未留下狩猎记录。", mutedStyle);

            for (int i = data.HuntHistory.Count - 1; i >= 0; i--)
            {
                var record = data.HuntHistory[i];
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label($"第 {record.Year} 年 · {(record.BossDefeated ? "讨伐成功" : "从黑暗中撤回")}");
                GUILayout.Label($"出发 {record.HuntersDeployed} 人  ·  失去 {record.HuntersLost} 人", mutedStyle);
                GUILayout.Label($"带回：{FormatCollectedResources(record.CollectedResources)}", mutedStyle);
                GUILayout.EndVertical();
            }

            GUILayout.Space(12f);
            GUILayout.Label("时间线", sectionStyle);
            if (data.Timeline.Count == 0)
            {
                GUILayout.Label("未来仍隐藏在黑暗里。", mutedStyle);
                return;
            }

            foreach (var entry in data.Timeline)
            {
                string eventName = string.IsNullOrEmpty(entry.EventName) ? entry.EventId : entry.EventName;
                string state = entry.IsCompleted ? "已发生" : "将发生";
                GUILayout.Label($"第 {entry.Year} 年 · {state} · {(entry.IsMilestone ? "★ " : string.Empty)}{eventName}", GUI.skin.box);
            }
        }

        private static string FormatCollectedResources(List<string> resources)
        {
            if (resources == null || resources.Count == 0) return "无";

            var counts = new Dictionary<string, int>();
            foreach (string resource in resources)
            {
                if (string.IsNullOrEmpty(resource)) continue;
                counts.TryGetValue(resource, out int count);
                counts[resource] = count + 1;
            }
            if (counts.Count == 0) return "无";

            var labels = new List<string>();
            foreach (var pair in counts)
                labels.Add($"{pair.Key} ×{pair.Value}");
            return string.Join("、", labels);
        }

        private void DrawHunters(SettlementInstance data)
        {
            GUILayout.Label($"出发小队  {selectedHunterIds.Count}/{MaximumSquadSize}", sectionStyle);
            var availableHunters = data.GetAvailableHunters();
            if (availableHunters.Count == 0)
            {
                GUILayout.Label("营地里没有能够出发的猎人。", mutedStyle);
            }

            foreach (var hunter in availableHunters)
            {
                bool selected = selectedHunterIds.Contains(hunter.InstanceId);
                GUILayout.BeginVertical(GUI.skin.box);
                bool requested = GUILayout.Toggle(selected, $"{hunter.Name}  ·  年龄 {hunter.Age}");
                GUILayout.Label($"意志 {hunter.Willpower}/{hunter.WillpowerMax}    命运 {hunter.Luck}    压抑 {hunter.Insanity}", mutedStyle);
                GUILayout.Label($"力量 {hunter.Stats.strength}  技巧 {hunter.Stats.accuracy}  敏捷 {hunter.Stats.evasion}  移动 {hunter.Stats.movement}", mutedStyle);
                GUILayout.Label($"胆识 {hunter.Courage}/8    知识 {hunter.Understanding}/8", mutedStyle);
                if (hunter.WeaponMasteries != null && hunter.WeaponMasteries.Count > 0)
                    GUILayout.Label($"武器熟练：{FormatWeaponMasteries(hunter.WeaponMasteries)}", mutedStyle);
                if (hunter.Traits != null && hunter.Traits.Count > 0)
                    GUILayout.Label($"特性：{string.Join("、", hunter.Traits)}", mutedStyle);
                if (hunter.Ailments != null && hunter.Ailments.Count > 0)
                    GUILayout.Label($"症状：{string.Join("、", hunter.Ailments)}", mutedStyle);
                if (hunter.PermConditions != null && hunter.PermConditions.Count > 0)
                    GUILayout.Label($"永久损伤：{string.Join("、", hunter.PermConditions)}", sectionStyle);
                DrawHunterGrowth(hunter);
                DrawHunterEquipment(hunter);
                GUILayout.EndVertical();

                if (requested == selected) continue;
                if (requested && selectedHunterIds.Count >= MaximumSquadSize) continue;
                if (requested)
                    selectedHunterIds.Add(hunter.InstanceId);
                else
                    selectedHunterIds.Remove(hunter.InstanceId);
            }

            DrawRetiredHunters(data);
        }

        private static string FormatWeaponMasteries(IReadOnlyList<WeaponMasteryState> masteries)
        {
            var labels = new List<string>();
            foreach (WeaponMasteryState mastery in masteries)
            {
                if (mastery == null) continue;
                string displayName = string.IsNullOrWhiteSpace(mastery.DisplayName) ? mastery.MasteryId : mastery.DisplayName;
                labels.Add($"{displayName} {mastery.Experience}");
            }
            return labels.Count > 0 ? string.Join("、", labels) : "无";
        }

        private void DrawRetiredHunters(SettlementInstance data)
        {
            List<HunterInstance> retiredHunters = data.Hunters.FindAll(hunter => hunter != null && hunter.IsAlive && hunter.Availability == HunterAvailabilityState.Retired);
            if (retiredHunters.Count == 0)
                return;

            GUILayout.Space(8f);
            GUILayout.Label("退休猎人", sectionStyle);
            foreach (HunterInstance hunter in retiredHunters)
                GUILayout.Label($"{hunter.Name}  ·  年龄 {hunter.Age}  ·  已退休", mutedStyle);
        }

        private void DrawHunterGrowth(HunterInstance hunter)
        {
            if (hunter.UnspentGrowth <= 0) return;

            GUILayout.Label($"待分配成长：{hunter.UnspentGrowth}", sectionStyle);
            GUILayout.BeginHorizontal();
            GUI.enabled = hunter.Courage < HunterAdvancementRules.MaximumGrowthAttribute;
            if (GUILayout.Button("+1 胆识", GUILayout.Height(28f)))
                manager.TrySpendHunterGrowth(hunter.InstanceId, HunterGrowthChoice.Courage);
            GUI.enabled = hunter.Understanding < HunterAdvancementRules.MaximumGrowthAttribute;
            if (GUILayout.Button("+1 知识", GUILayout.Height(28f)))
                manager.TrySpendHunterGrowth(hunter.InstanceId, HunterGrowthChoice.Understanding);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        private void DrawHunterEquipment(HunterInstance hunter)
        {
            if (hunter.Equipment == null || hunter.Equipment.Count == 0)
            {
                GUILayout.Label("装备：徒手", mutedStyle);
                return;
            }

            GUILayout.Label($"装备：{FormatEquipment(hunter.Equipment)}", mutedStyle);
            for (int i = 0; i < hunter.Equipment.Count; i++)
            {
                int slotIndex = i;
                string itemName = hunter.Equipment[i].Data.itemName;
                if (!GUILayout.Button($"卸下 {itemName}", GUILayout.Height(26f))) continue;
                if (manager.SettlementHunters.UnequipItem(hunter, slotIndex))
                    manager.SaveSettlementProgress();
                break;
            }
        }

        private static string FormatEquipment(List<ItemInstance> equipment)
        {
            var names = new List<string>();
            foreach (var item in equipment)
                if (item?.Data != null)
                    names.Add(item.Data.itemName);
            return names.Count > 0 ? string.Join("、", names) : "徒手";
        }

        private void DrawDeparture(SettlementInstance data)
        {
            bool canDepart = selectedHunterIds.Count > 0;
            GUI.enabled = canDepart;
            if (GUILayout.Button(canDepart ? $"带领 {selectedHunterIds.Count} 名猎人出发" : "至少选择 1 名猎人", GUILayout.Height(46f)))
            {
                var hunterIds = new List<int>();
                foreach (var hunter in data.GetAvailableHunters())
                    if (selectedHunterIds.Contains(hunter.InstanceId))
                        hunterIds.Add(hunter.InstanceId);
                if (DepartureRequested != null)
                    DepartureRequested(hunterIds);
                else
                    manager.TryDepartForHunt(hunterIds);
            }
            GUI.enabled = true;
        }

        private void EnsureSelection(SettlementInstance data)
        {
            if (selectionInitialized) return;

            foreach (var hunter in data.GetAvailableHunters())
            {
                if (selectedHunterIds.Count >= MaximumSquadSize) break;
                selectedHunterIds.Add(hunter.InstanceId);
            }
            selectionInitialized = true;
        }

        private void PruneSelection(SettlementInstance data)
        {
            selectedHunterIds.RemoveWhere(id => data.GetHunter(id)?.IsAvailable != true);
        }

        private void EnsureStyles()
        {
            if (panelStyle != null) return;

            panelTexture = new Texture2D(1, 1);
            panelTexture.SetPixel(0, 0, new Color(0.035f, 0.025f, 0.02f, 0.96f));
            panelTexture.Apply();
            panelStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(14, 14, 14, 14),
                normal = { background = panelTexture }
            };
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 21,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.96f, 0.78f, 0.46f) }
            };
            sectionStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            mutedStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                wordWrap = true,
                normal = { textColor = new Color(0.76f, 0.78f, 0.8f) }
            };
        }

        private void OnDestroy()
        {
            if (manager != null)
                manager.SettlementEventPresented -= ShowEvent;
            if (panelTexture != null)
                Destroy(panelTexture);
        }
    }
}
