using System.Collections.Generic;
using UnityEngine;

namespace HuntingInDarkness.Hunt
{
    /// <summary>把已生成地图的世界坐标投影为确定性的入场镜头计划，不持有相机或地图状态。</summary>
    public static class PlayableHuntMapIntroPlanner
    {
        public static bool TryCreate(IReadOnlyList<Vector3> tilePositions, float pitch, float playHeight, float overviewScale, float minimumOverviewHeight, float maximumOverviewHeight, float duration, out PlayableHuntMapIntroPlan plan)
        {
            plan = default;
            if (tilePositions == null || tilePositions.Count == 0)
                return false;

            Vector3 minimum = tilePositions[0];
            Vector3 maximum = tilePositions[0];
            if (!IsFinite(minimum))
                return false;
            for (int index = 1; index < tilePositions.Count; index++)
            {
                if (!IsFinite(tilePositions[index]))
                    return false;
                minimum = Vector3.Min(minimum, tilePositions[index]);
                maximum = Vector3.Max(maximum, tilePositions[index]);
            }

            float safePitch = Mathf.Clamp(Sanitize(pitch, 55f), 20f, 80f);
            float safePlayHeight = Mathf.Max(1f, Sanitize(playHeight, 10f));
            float safeMinimumOverviewHeight = Mathf.Max(safePlayHeight, Sanitize(minimumOverviewHeight, 13f));
            float safeMaximumOverviewHeight = Mathf.Max(safeMinimumOverviewHeight, Sanitize(maximumOverviewHeight, 20f));
            float mapSpan = Mathf.Max(maximum.x - minimum.x, maximum.z - minimum.z);
            float overviewHeight = Mathf.Clamp(Mathf.Max(safeMinimumOverviewHeight, mapSpan * Mathf.Max(0.1f, Sanitize(overviewScale, 1.25f))), safeMinimumOverviewHeight, safeMaximumOverviewHeight);
            Vector3 center = (minimum + maximum) * 0.5f;
            Quaternion rotation = Quaternion.Euler(safePitch, 0f, 0f);
            plan = new PlayableHuntMapIntroPlan(CreateCameraPosition(center, safePitch, overviewHeight), CreateCameraPosition(center, safePitch, safePlayHeight), rotation, Mathf.Max(0f, Sanitize(duration, 0f)), center);
            return true;
        }

        private static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float Sanitize(float value, float fallback) => IsFinite(value) ? value : fallback;

        private static Vector3 CreateCameraPosition(Vector3 center, float pitch, float height)
        {
            float backwardDistance = height / Mathf.Tan(pitch * Mathf.Deg2Rad);
            return center + new Vector3(0f, height, -backwardDistance);
        }
    }

    public readonly struct PlayableHuntMapIntroPlan
    {
        public Vector3 OverviewPosition { get; }
        public Vector3 PlayPosition { get; }
        public Quaternion Rotation { get; }
        public float Duration { get; }
        public Vector3 MapCenter { get; }

        public PlayableHuntMapIntroPlan(Vector3 overviewPosition, Vector3 playPosition, Quaternion rotation, float duration, Vector3 mapCenter)
        {
            OverviewPosition = overviewPosition;
            PlayPosition = playPosition;
            Rotation = rotation;
            Duration = duration;
            MapCenter = mapCenter;
        }
    }
}
