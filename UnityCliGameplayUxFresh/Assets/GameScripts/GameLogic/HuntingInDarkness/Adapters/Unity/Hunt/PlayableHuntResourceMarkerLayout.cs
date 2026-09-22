using UnityEngine;

namespace HuntingInDarkness.Hunt
{
    /// <summary>把同一地块的资源点索引投影为稳定的棋子局部位置。</summary>
    public static class PlayableHuntResourceMarkerLayout
    {
        private const int MarkersPerRing = 6;

        public static bool TryGetLocalPosition(int markerIndex, int markerCount, float baseRadius, out Vector3 position)
        {
            position = default;
            if (markerCount <= 0 || markerIndex < 0 || markerIndex >= markerCount)
                return false;

            int ringIndex = markerIndex / MarkersPerRing;
            int firstMarkerInRing = ringIndex * MarkersPerRing;
            int markersInRing = Mathf.Min(MarkersPerRing, markerCount - firstMarkerInRing);
            int indexInRing = markerIndex - firstMarkerInRing;
            float safeBaseRadius = float.IsNaN(baseRadius) || float.IsInfinity(baseRadius) ? 0.48f : Mathf.Max(0.1f, baseRadius);
            float radius = safeBaseRadius + ringIndex * 0.18f;
            float angle = (-55f + indexInRing * 360f / markersInRing) * Mathf.Deg2Rad;
            position = new Vector3(Mathf.Cos(angle) * radius, 0.24f, Mathf.Sin(angle) * radius);
            return true;
        }
    }
}
