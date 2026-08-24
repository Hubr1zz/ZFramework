using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using HuntingInDarkness.Data;
using UnityEngine;

namespace Core
{
    public readonly struct CampaignSaveCandidates
    {
        public CampaignSaveCandidates(string primary, string backup)
        {
            Primary = primary;
            Backup = backup;
        }

        public string Primary { get; }
        public string Backup { get; }
    }

    [Serializable]
    internal sealed class CampaignSaveEnvelope
    {
        public string Format;
        public int SchemaVersion;
        public string PayloadSha256;
        public string Payload;
    }

    public static class CampaignSaveCodec
    {
        public const int CurrentSchemaVersion = 1;
        private const string Format = "HuntingInDarkness.CampaignSave";
        private const string MagicHeader = "HID-CAMPAIGN-SAVE\n";

        public static string Encode(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload)) throw new ArgumentException("存档内容为空。", nameof(payload));
            var envelope = new CampaignSaveEnvelope
            {
                Format = Format,
                SchemaVersion = CurrentSchemaVersion,
                PayloadSha256 = ComputeHash(payload),
                Payload = payload
            };
            return MagicHeader + JsonUtility.ToJson(envelope, true);
        }

        public static bool TryDecode(string content, out string payload, out bool isLegacy, out string reason)
        {
            payload = string.Empty;
            isLegacy = false;
            if (string.IsNullOrWhiteSpace(content))
            {
                reason = "存档文件为空。";
                return false;
            }
            if (!content.StartsWith(MagicHeader, StringComparison.Ordinal))
            {
                if (!content.Contains("\"CurrentYear\"", StringComparison.Ordinal))
                {
                    reason = "文件既不是有效封套，也不是可识别的旧版存档。";
                    return false;
                }
                payload = content;
                isLegacy = true;
                reason = string.Empty;
                return true;
            }

            try
            {
                CampaignSaveEnvelope envelope = JsonUtility.FromJson<CampaignSaveEnvelope>(content.Substring(MagicHeader.Length));
                if (envelope == null || envelope.Format != Format || envelope.SchemaVersion <= 0 || envelope.SchemaVersion > CurrentSchemaVersion || string.IsNullOrWhiteSpace(envelope.Payload))
                {
                    reason = "存档封套版本或内容无效。";
                    return false;
                }
                if (!string.Equals(envelope.PayloadSha256, ComputeHash(envelope.Payload), StringComparison.OrdinalIgnoreCase))
                {
                    reason = "存档内容校验失败。";
                    return false;
                }
                payload = envelope.Payload;
                reason = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                reason = $"存档封套无法解析：{exception.Message}";
                return false;
            }
        }

        private static string ComputeHash(string payload)
        {
            using SHA256 sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var result = new StringBuilder(hash.Length * 2);
            foreach (byte value in hash)
                result.Append(value.ToString("x2"));
            return result.ToString();
        }
    }

    public static class CampaignSaveFileStore
    {
        public const string BackupSuffix = ".bak";
        public const string TemporarySuffix = ".tmp";

        public static bool TryWrite(string savePath, string content, out string reason)
        {
            if (string.IsNullOrWhiteSpace(savePath) || string.IsNullOrWhiteSpace(content))
            {
                reason = "存档路径或内容为空。";
                return false;
            }

            string temporaryPath = savePath + TemporarySuffix;
            string backupPath = savePath + BackupSuffix;
            try
            {
                string directory = Path.GetDirectoryName(savePath);
                if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
                WriteThrough(temporaryPath, content);
                if (!File.Exists(savePath))
                {
                    File.Move(temporaryPath, savePath);
                    reason = string.Empty;
                    return true;
                }

                try
                {
                    File.Replace(temporaryPath, savePath, backupPath, true);
                }
                catch (NotSupportedException)
                {
                    ReplaceWithRecoverableFallback(temporaryPath, savePath, backupPath);
                }
                reason = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                TryDelete(temporaryPath);
                reason = exception.Message;
                return false;
            }
        }

        public static CampaignSaveCandidates ReadCandidates(string savePath)
        {
            if (string.IsNullOrWhiteSpace(savePath)) return default;
            return new CampaignSaveCandidates(TryRead(savePath), TryRead(savePath + BackupSuffix));
        }

        public static bool HasAnyCandidate(string savePath)
        {
            if (string.IsNullOrWhiteSpace(savePath)) return false;
            return File.Exists(savePath) || File.Exists(savePath + BackupSuffix);
        }

        public static bool DeleteAll(string savePath)
        {
            if (string.IsNullOrWhiteSpace(savePath)) return false;
            bool existed = File.Exists(savePath) || File.Exists(savePath + BackupSuffix) || File.Exists(savePath + TemporarySuffix);
            DeleteIfExists(savePath);
            DeleteIfExists(savePath + BackupSuffix);
            DeleteIfExists(savePath + TemporarySuffix);
            return existed;
        }

        private static void WriteThrough(string path, string content)
        {
            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            writer.Write(content);
            writer.Flush();
            stream.Flush(true);
        }

        private static void ReplaceWithRecoverableFallback(string temporaryPath, string savePath, string backupPath)
        {
            string previousContent = File.ReadAllText(savePath, Encoding.UTF8);
            if (!File.Exists(backupPath)) File.Copy(savePath, backupPath);
            File.Delete(savePath);
            File.Move(temporaryPath, savePath);
            WriteThrough(backupPath, previousContent);
        }

        private static string TryRead(string path)
        {
            try
            {
                return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : null;
            }
            catch
            {
                return null;
            }
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }

        private static bool TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    public static class CampaignSaveRecovery
    {
        public static bool TryRestore(CampaignSaveCandidates candidates, out CampaignSnapshot data, out bool usedBackup, out string reason)
        {
            usedBackup = false;
            if (TryDeserialize(candidates.Primary, out data, out string primaryReason))
            {
                reason = string.Empty;
                return true;
            }
            if (TryDeserialize(candidates.Backup, out data, out string backupReason))
            {
                usedBackup = true;
                reason = string.Empty;
                return true;
            }
            reason = $"主存档：{primaryReason}；备份：{backupReason}";
            return false;
        }

        private static bool TryDeserialize(string content, out CampaignSnapshot data, out string reason)
        {
            data = null;
            if (content == null)
            {
                reason = "文件不存在。";
                return false;
            }
            if (!CampaignSaveCodec.TryDecode(content, out string payload, out _, out reason)) return false;
            try
            {
                if (payload.Contains("\"CampaignSchemaVersion\"", StringComparison.Ordinal))
                {
                    data = JsonUtility.FromJson<CampaignSnapshot>(payload);
                    NormalizeEmptyReferenceRecords(data);
                    if (data?.Settlement != null && data.CampaignSchemaVersion > 0 && data.CampaignSchemaVersion <= CampaignSnapshot.CurrentSchemaVersion) return true;
                    reason = "战役快照版本或营地状态无效。";
                    return false;
                }
                SettlementInstance legacySettlement = JsonUtility.FromJson<SettlementInstance>(payload);
                if (legacySettlement != null)
                {
                    data = new CampaignSnapshot { Settlement = legacySettlement };
                    NormalizeEmptyReferenceRecords(data);
                    return true;
                }
                reason = "存档状态为空。";
                return false;
            }
            catch (Exception exception)
            {
                reason = $"存档状态无法解析：{exception.Message}";
                return false;
            }
        }

        private static void NormalizeEmptyReferenceRecords(CampaignSnapshot data)
        {
            if (data?.Settlement?.PendingHuntReturn != null && string.IsNullOrWhiteSpace(data.Settlement.PendingHuntReturn.RecordId))
                data.Settlement.PendingHuntReturn = null;
            if (data != null && !data.HasActiveHuntState)
                data.ActiveHunt = null;
        }
    }
}
