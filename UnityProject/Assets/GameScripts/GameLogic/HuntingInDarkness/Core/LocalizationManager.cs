using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Core
{
    /// <summary>
    /// 全局本地化与字体管理。
    /// 职责：① 运行时向 TMP 动态字体图集注入中文字符集；② 提供字符串键值查找接口（预留多语言扩展）。
    /// 使用前提：TMP FontAsset 须在 Inspector 中将 Atlas Population Mode 设为 Dynamic。
    /// </summary>
    public class LocalizationManager
    {
        public static LocalizationManager Instance { get; private set; }

        // ─── 字体 ─────────────────────────────────────────────────────
        private readonly TMP_FontAsset _primaryFont;

        // ─── 字符串本地化（预留）─────────────────────────────────────
        private readonly Dictionary<string, string> _strings = new();

        // ═══════════════════════════════════════════
        // 初始化入口（由 GameManager 调用）
        // ═══════════════════════════════════════════

        /// <param name="fontAsset">微软雅黑 SDF（须为 Dynamic 模式）</param>
        /// <param name="characterSet">zh-cn.txt 等字符列表文件</param>
        /// <param name="localizationTable">可选：key=value 格式的本地化文本文件</param>
        public static void Initialize(
            TMP_FontAsset fontAsset,
            TextAsset characterSet,
            TextAsset localizationTable = null)
        {
            if (Instance != null)
            {
                Debug.LogWarning("[LocalizationManager] 已经初始化，跳过重复调用");
                return;
            }
            Instance = new LocalizationManager(fontAsset, characterSet, localizationTable);
        }

        private LocalizationManager(
            TMP_FontAsset fontAsset,
            TextAsset characterSet,
            TextAsset localizationTable)
        {
            _primaryFont = fontAsset;

            if (characterSet != null)
                PopulateFontAtlas(characterSet.text);
            else
                Debug.LogWarning("[LocalizationManager] 未提供字符集文件，动态图集不会预热");

            if (localizationTable != null)
                ParseLocalizationTable(localizationTable.text);
        }

        // ═══════════════════════════════════════════
        // 字体图集
        // ═══════════════════════════════════════════

        /// <summary>
        /// 将字符集字符串中的所有字符批量注入 TMP 动态图集。
        /// FontAsset 必须为 Dynamic 模式，否则 TryAddCharacters 无法写入新字形。
        /// </summary>
        private void PopulateFontAtlas(string characters)
        {
            if (_primaryFont == null)
            {
                Debug.LogWarning("[LocalizationManager] 未配置字体资源，跳过图集预热");
                return;
            }

            bool success = _primaryFont.TryAddCharacters(characters, out string missing);

            if (!success && !string.IsNullOrEmpty(missing))
                Debug.LogWarning(
                    $"[LocalizationManager] {missing.Length} 个字符无法加入图集（字体中不存在或图集容量不足）");
            else
                Debug.Log($"[LocalizationManager] 字体图集预热完成，已加载字符 {characters.Length - (missing?.Length ?? 0)} 个");
        }

        /// <summary>
        /// 运行时按需追加字符（例如加载存档后遇到玩家自定义名称）。
        /// </summary>
        public void EnsureCharacters(string text)
        {
            if (_primaryFont == null || string.IsNullOrEmpty(text)) return;
            _primaryFont.TryAddCharacters(text, out _);
        }

        // ═══════════════════════════════════════════
        // 字符串本地化（简单键值，预留扩展）
        // ═══════════════════════════════════════════

        /// <summary>
        /// 按 key 查找本地化字符串；未找到时返回 key 本身。
        /// </summary>
        public string GetText(string key)
        {
            return _strings.TryGetValue(key, out var value) ? value : key;
        }

        /// <summary>
        /// 解析 "key=value" 每行一条的本地化文本文件。
        /// </summary>
        private void ParseLocalizationTable(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return;

            foreach (var line in raw.Split('\n'))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;

                int sep = trimmed.IndexOf('=');
                if (sep <= 0) continue;

                var key = trimmed[..sep].Trim();
                var val = trimmed[(sep + 1)..].Trim();
                _strings[key] = val;
            }

            Debug.Log($"[LocalizationManager] 本地化表加载完成，共 {_strings.Count} 条");
        }
    }
}
