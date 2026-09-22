using System;
using System.Collections.Generic;
using System.Globalization;

namespace ZFramework.RTS
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class RtsParameterAttribute : Attribute
    {
        public RtsParameterAttribute(string key, RtsParameterType type, string defaultValue)
        { Key = key; Type = type; DefaultValue = defaultValue; }
        public string Key { get; }
        public RtsParameterType Type { get; }
        public string DefaultValue { get; }
        public double Min { get; set; } = double.NaN;
        public double Max { get; set; } = double.NaN;
    }

    public static class ScriptConfig
    {
        public static string Get(string config, string key, string fallback = "")
        {
            if (string.IsNullOrWhiteSpace(config)) return fallback;
            string[] entries = config.Split(';');
            foreach (string entry in entries)
            {
                int separator = entry.IndexOf('=');
                if (separator <= 0) continue;
                if (entry.Substring(0, separator).Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                    return Uri.UnescapeDataString(entry.Substring(separator + 1).Trim());
            }
            return entries.Length == 1 && entries[0].IndexOf('=') < 0 ? entries[0].Trim() : fallback;
        }

        public static float GetFloat(string config, string key, float fallback) =>
            float.TryParse(Get(config, key), NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ? value : fallback;
        public static int GetInt(string config, string key, int fallback) =>
            int.TryParse(Get(config, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : fallback;
        public static bool GetBool(string config, string key, bool fallback) =>
            bool.TryParse(Get(config, key), out bool value) ? value : fallback;
    }

    public readonly struct RtsTarget
    {
        public RtsTarget(int id, float x, float y, float z) { Id = id; X = x; Y = y; Z = z; }
        public int Id { get; }
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
    }

    public readonly struct RtsProjectileRequest
    {
        public RtsProjectileRequest(float x, float y, float z, int targetId, float speed, float damage)
        { X = x; Y = y; Z = z; TargetId = targetId; Speed = speed; Damage = damage; }
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public int TargetId { get; }
        public float Speed { get; }
        public float Damage { get; }
    }

    public interface IRtsTargetQueryV1 { bool TryFindNearest(float x, float y, float z, float range, out RtsTarget target); }
    public interface IRtsDamageServiceV1 { void ApplyDamage(int targetId, float damage); }
    public interface IRtsProjectileServiceV1 { void Spawn(in RtsProjectileRequest request); }
    public interface IRtsEffectServiceV1 { void Play(string effectId, float x, float y, float z); }
    public interface IRtsObjectPoolV1 { int Spawn(string assetKey, float x, float y, float z); void Despawn(int instanceId); }
    public interface IRtsAnimationServiceV1 { void Play(int instanceId, string state); }
    public interface IRtsAudioServiceV1 { void Play(string audioKey, float x, float y, float z); }
    public interface IRtsTimerServiceV1 { long Schedule(float delaySeconds, Action callback); void Cancel(long timerId); }

    public readonly struct RtsVector3
    {
        public RtsVector3(float x, float y, float z) { X = x; Y = y; Z = z; }
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public static RtsVector3 Zero => new RtsVector3(0f, 0f, 0f);
        public static RtsVector3 One => new RtsVector3(1f, 1f, 1f);
    }

    public readonly struct RtsColor
    {
        public RtsColor(float r, float g, float b, float a = 1f) { R = r; G = g; B = b; A = a; }
        public float R { get; }
        public float G { get; }
        public float B { get; }
        public float A { get; }
        public static RtsColor White => new RtsColor(1f, 1f, 1f, 1f);
    }

    public readonly struct RtsWorldEntitySpec
    {
        public RtsWorldEntitySpec(string prototype, RtsVector3 position, RtsVector3 rotation,
            RtsVector3 scale, RtsColor color, string text = "")
        {
            Prototype = prototype ?? string.Empty;
            Position = position;
            Rotation = rotation;
            Scale = scale;
            Color = color;
            Text = text ?? string.Empty;
        }

        public string Prototype { get; }
        public RtsVector3 Position { get; }
        public RtsVector3 Rotation { get; }
        public RtsVector3 Scale { get; }
        public RtsColor Color { get; }
        public string Text { get; }
    }

    public interface IRtsWorldReconcileV1 : IDisposable
    {
        int Upsert(string stableKey, in RtsWorldEntitySpec spec);
        void Commit();
    }

    public interface IRtsWorldServiceV1
    {
        IRtsWorldReconcileV1 BeginReconcile(string ownerKey);
        int Spawn(string ownerKey, string stableKey, in RtsWorldEntitySpec spec);
        void Despawn(int handle);
        bool Exists(int handle);
        bool TryGetHandle(string ownerKey, string stableKey, out int handle);
        void SetTransform(int handle, RtsVector3 position, RtsVector3 rotation, RtsVector3 scale);
        void SetColor(int handle, RtsColor color);
        void SetText(int handle, string text);
        int EntityCount { get; }
    }

}
