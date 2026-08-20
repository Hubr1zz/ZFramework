using System.Collections.Generic;
using HuntingInDarkness.GameCore.Foundation;
using NUnit.Framework;

namespace HuntingInDarkness.GameCore.Tests
{
    public sealed class KeywordRulesTests
    {
        [Test]
        public void Contains_NormalizesCaseAndWhitespace()
        {
            var keywords = new HashSet<string> { "stone", "ritual" };

            Assert.That(KeywordRules.Contains(keywords, " Stone "), Is.True);
            Assert.That(KeywordRules.Contains(keywords, "wood"), Is.False);
            Assert.That(KeywordRules.Contains(keywords, " "), Is.False);
        }
    }
}
