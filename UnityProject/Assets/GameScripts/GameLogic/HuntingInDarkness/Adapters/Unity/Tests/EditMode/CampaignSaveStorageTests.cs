using System;
using System.IO;
using Core;
using HuntingInDarkness.Data;
using HuntingInDarkness.GameCore.Settlement;
using NUnit.Framework;
using UnityEngine;

namespace HuntingInDarkness.Adapter.Tests
{
    public sealed class CampaignSaveStorageTests
    {
        private string testDirectory;
        private string savePath;

        [SetUp]
        public void SetUp()
        {
            testDirectory = Path.Combine(Path.GetTempPath(), $"hunting-in-darkness-save-{Guid.NewGuid():N}");
            savePath = Path.Combine(testDirectory, "campaign.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(testDirectory)) Directory.Delete(testDirectory, true);
        }

        [Test]
        public void Codec_RoundTripsAndRejectsTamperedPayload()
        {
            string encoded = CampaignSaveCodec.Encode("{\"CurrentYear\":3}");

            Assert.That(CampaignSaveCodec.TryDecode(encoded, out string payload, out bool isLegacy, out string reason), Is.True, reason);
            Assert.That(payload, Is.EqualTo("{\"CurrentYear\":3}"));
            Assert.That(isLegacy, Is.False);

            string tampered = encoded.Replace("CurrentYear", "CurrentFear");
            Assert.That(CampaignSaveCodec.TryDecode(tampered, out _, out _, out reason), Is.False);
            Assert.That(reason, Does.Contain("校验失败"));
        }

        [Test]
        public void Codec_AcceptsLegacySettlementJson()
        {
            const string legacy = "{\"CurrentYear\":2}";

            Assert.That(CampaignSaveCodec.TryDecode(legacy, out string payload, out bool isLegacy, out string reason), Is.True, reason);
            Assert.That(payload, Is.EqualTo(legacy));
            Assert.That(isLegacy, Is.True);
        }

        [Test]
        public void Recovery_WrapsLegacySettlementAsSettlementOnlyCampaign()
        {
            string legacy = CampaignSaveCodec.Encode("{\"CurrentYear\":4}");
            var candidates = new CampaignSaveCandidates(legacy, null);

            Assert.That(CampaignSaveRecovery.TryRestore(candidates, out CampaignSnapshot restored, out bool usedBackup, out string reason), Is.True, reason);
            Assert.That(usedBackup, Is.False);
            Assert.That(restored.CampaignSchemaVersion, Is.EqualTo(CampaignSnapshot.CurrentSchemaVersion));
            Assert.That(restored.Settlement.CurrentYear, Is.EqualTo(4));
            Assert.That(restored.ActiveHunt, Is.Null);
        }

        [Test]
        public void CampaignPayload_RejectsActiveHuntAndPendingReturnTogether()
        {
            var settlement = new SettlementInstance { PendingHuntReturn = new HuntRecord { RecordId = "pending", Year = 2 } };
            var snapshot = new CampaignSnapshot { Settlement = settlement, HasActiveHuntState = true, ActiveHunt = new ActiveHuntSnapshot { ExpeditionId = "expedition" } };

            Assert.That(SaveLoadSystem.TryCreatePayload(snapshot, out _, out string reason), Is.False);
            Assert.That(reason, Does.Contain("不能同时"));
        }

        [Test]
        public void CampaignPayload_FreezesSettlementBeforeLiveStateChanges()
        {
            var settlement = new SettlementInstance { CurrentYear = 3 };
            var snapshot = new CampaignSnapshot { Settlement = settlement };

            Assert.That(SaveLoadSystem.TryCreatePayload(snapshot, out string payload, out string reason), Is.True, reason);
            settlement.CurrentYear = 9;
            CampaignSnapshot restored = JsonUtility.FromJson<CampaignSnapshot>(payload);

            Assert.That(restored.Settlement.CurrentYear, Is.EqualTo(3));
        }

        [Test]
        public void Recovery_DoesNotTurnSettlementOnlyNullsIntoActiveRecords()
        {
            CampaignSnapshot snapshot = ActiveHuntSnapshotAdapter.CaptureSettlement(new SettlementInstance { CurrentYear = 5 });
            Assert.That(SaveLoadSystem.TryCreatePayload(snapshot, out string payload, out string reason), Is.True, reason);

            Assert.That(CampaignSaveRecovery.TryRestore(new CampaignSaveCandidates(CampaignSaveCodec.Encode(payload), null), out CampaignSnapshot restored, out _, out reason), Is.True, reason);
            Assert.That(restored.HasActiveHunt, Is.False);
            Assert.That(restored.ActiveHunt, Is.Null);
            Assert.That(restored.Settlement.PendingHuntReturn, Is.Null);
        }

        [Test]
        public void SettlementJson_PersistsInventionActiveEffectUsage()
        {
            var settlement = new SettlementInstance();
            settlement.InventionActiveEffectUses.Add(new InventionActiveEffectUsage { EffectId = "prayer:vigil", Year = 3, UseCount = 1 });

            string json = JsonUtility.ToJson(settlement);
            SettlementInstance restored = JsonUtility.FromJson<SettlementInstance>(json);

            Assert.That(restored.InventionActiveEffectUses, Has.Count.EqualTo(1));
            Assert.That(restored.InventionActiveEffectUses[0].EffectId, Is.EqualTo("prayer:vigil"));
            Assert.That(restored.InventionActiveEffectUses[0].Year, Is.EqualTo(3));
        }

        [Test]
        public void SettlementJson_PersistsHunterOriginTemplateId()
        {
            var settlement = new SettlementInstance();
            settlement.Hunters.Add(new HunterInstance(null, 101) { OriginTemplateId = "ember_keeper_yao" });

            SettlementInstance restored = JsonUtility.FromJson<SettlementInstance>(JsonUtility.ToJson(settlement));

            Assert.That(restored.Hunters, Has.Count.EqualTo(1));
            Assert.That(restored.Hunters[0].OriginTemplateId, Is.EqualTo("ember_keeper_yao"));
        }

        [Test]
        public void FileStore_SecondWriteKeepsPreviousSnapshotAsBackup()
        {
            Assert.That(CampaignSaveFileStore.TryWrite(savePath, "first", out string reason), Is.True, reason);
            Assert.That(CampaignSaveFileStore.TryWrite(savePath, "second", out reason), Is.True, reason);
            Assert.That(CampaignSaveFileStore.TryWrite(savePath, "third", out reason), Is.True, reason);

            CampaignSaveCandidates candidates = CampaignSaveFileStore.ReadCandidates(savePath);

            Assert.That(candidates.Primary, Is.EqualTo("third"));
            Assert.That(candidates.Backup, Is.EqualTo("second"));
            Assert.That(File.Exists(savePath + CampaignSaveFileStore.TemporarySuffix), Is.False);
        }

        [Test]
        public void FileStore_CorruptPrimaryLeavesReadableBackupAndDeleteRemovesAllCandidates()
        {
            string first = CampaignSaveCodec.Encode("{\"CurrentYear\":1}");
            string second = CampaignSaveCodec.Encode("{\"CurrentYear\":2}");
            Assert.That(CampaignSaveFileStore.TryWrite(savePath, first, out string reason), Is.True, reason);
            Assert.That(CampaignSaveFileStore.TryWrite(savePath, second, out reason), Is.True, reason);
            File.WriteAllText(savePath, "corrupt");
            File.WriteAllText(savePath + CampaignSaveFileStore.TemporarySuffix, "stale");

            CampaignSaveCandidates candidates = CampaignSaveFileStore.ReadCandidates(savePath);

            Assert.That(CampaignSaveCodec.TryDecode(candidates.Primary, out _, out _, out _), Is.False);
            Assert.That(CampaignSaveRecovery.TryRestore(candidates, out CampaignSnapshot restored, out bool usedBackup, out reason), Is.True, reason);
            Assert.That(usedBackup, Is.True);
            Assert.That(restored.Settlement.CurrentYear, Is.EqualTo(1));
            Assert.That(CampaignSaveFileStore.DeleteAll(savePath), Is.True);
            Assert.That(CampaignSaveFileStore.HasAnyCandidate(savePath), Is.False);
            Assert.That(File.Exists(savePath + CampaignSaveFileStore.TemporarySuffix), Is.False);
        }
    }
}
