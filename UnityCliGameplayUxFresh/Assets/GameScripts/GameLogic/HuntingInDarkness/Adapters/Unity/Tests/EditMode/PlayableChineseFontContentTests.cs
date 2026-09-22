using NUnit.Framework;
using UnityEditor;
using TMPro;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class PlayableChineseFontContentTests
    {
        private const string FontAssetPath = "Assets/AssetRaw/Fonts/HuntingInDarkness/NotoSansSC-Regular.otf";

        [Test]
        public void BundledFont_CoversCoreTabletopChineseText()
        {
            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(FontAssetPath);
            Assert.That(sourceFont, Is.Not.Null);

            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(sourceFont);
            try
            {
                const string coreText = "营地狩猎资源装备猎人事件选择确认返回移动翻开采集工坊发明年鉴";
                bool added = fontAsset.TryAddCharacters(coreText, out string missing);

                Assert.That(added, Is.True);
                Assert.That(missing, Is.Empty);
                Assert.That(fontAsset.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Dynamic));
            }
            finally
            {
                Object.DestroyImmediate(fontAsset);
            }
        }
    }
}
