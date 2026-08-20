using System.Collections.Generic;
using HuntingInDarkness.Data;
using TMPro;
using UnityEngine;

namespace HuntingInDarkness.Hunt
{
    /// <summary>一个可独立选择的地图资源点棋子；只转发精确索引，不提交采集结果。</summary>
    [DisallowMultipleComponent]
    public sealed class PlayableHuntResourceMarker3D : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float hoverScale = 1.12f;
        [SerializeField] private Color baseColor = new(0.18f, 0.13f, 0.08f);
        [SerializeField] private Color labelColor = new(0.96f, 0.89f, 0.67f);

        private readonly List<Material> generatedMaterials = new();
        private HuntManager manager;
        private Renderer resourceRenderer;
        private TextMeshPro label;
        private Color resourceColor;
        private bool isAvailableForHarvest;
        private bool isBuilt;

        public ResourcePointInstance Point { get; private set; }
        public int PointIndex { get; private set; }
        public bool IsAvailableForHarvest => isAvailableForHarvest;
        public Vector3 PresentationPosition => transform.position + new Vector3(0f, 0.58f, -1.55f);

        public static PlayableHuntResourceMarker3D Create(Transform parent, HuntManager manager, Vector2Int tileCoordinate, int pointIndex, ResourcePointInstance point, Vector3 localPosition)
        {
            var markerObject = new GameObject($"ResourcePoint_{pointIndex}_{point?.ResourceName ?? "Unknown"}");
            markerObject.transform.SetParent(parent, false);
            markerObject.transform.localPosition = localPosition;
            var marker = markerObject.AddComponent<PlayableHuntResourceMarker3D>();
            marker.Present(manager, tileCoordinate, pointIndex, point);
            return marker;
        }

        public void Present(HuntManager manager, Vector2Int tileCoordinate, int pointIndex, ResourcePointInstance point)
        {
            this.manager = manager;
            Point = point;
            PointIndex = pointIndex;
            EnsureBuilt();
            GetComponent<ResourceMarkerClickHandler>().Initialize(manager, tileCoordinate, pointIndex);
            string displayName = string.IsNullOrWhiteSpace(point?.ResourceName) ? "未知资源" : point.ResourceName;
            resourceColor = CreateResourceColor(displayName);
            RefreshAvailability();
        }

        public void RefreshAvailability()
        {
            if (!isBuilt) return;
            isAvailableForHarvest = manager != null && manager.IsHarvestablePoint(Point);
            string displayName = string.IsNullOrWhiteSpace(Point?.ResourceName) ? "未知资源" : Point.ResourceName;
            label.text = isAvailableForHarvest ? $"{displayName}\n点击采集 · 抽取 {Mathf.Max(0, Point?.DrawCount ?? 0)}" : $"{displayName}\n先移动到此处";
            transform.localScale = Vector3.one;
            PresentBaseColor();
        }

        private void EnsureBuilt()
        {
            if (isBuilt)
                return;
            isBuilt = true;
            var hitCollider = gameObject.AddComponent<BoxCollider>();
            hitCollider.center = new Vector3(0f, 0.10f, 0f);
            hitCollider.size = new Vector3(0.62f, 0.42f, 0.62f);
            gameObject.AddComponent<ResourceMarkerClickHandler>();
            CreatePrimitive("Token Base", PrimitiveType.Cylinder, new Vector3(0f, -0.09f, 0f), new Vector3(0.30f, 0.04f, 0.30f), baseColor);
            resourceRenderer = CreatePrimitive("Resource Stack", PrimitiveType.Cube, new Vector3(0f, 0.04f, 0f), new Vector3(0.32f, 0.16f, 0.26f), Color.white).GetComponent<Renderer>();
            CreatePrimitive("Resource Piece A", PrimitiveType.Sphere, new Vector3(-0.10f, 0.16f, 0.02f), Vector3.one * 0.12f, Color.white, resourceRenderer.sharedMaterial);
            CreatePrimitive("Resource Piece B", PrimitiveType.Sphere, new Vector3(0.10f, 0.15f, -0.03f), Vector3.one * 0.10f, Color.white, resourceRenderer.sharedMaterial);
            var labelObject = new GameObject("Resource Label");
            labelObject.transform.SetParent(transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 0.29f, 0f);
            labelObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            label = labelObject.AddComponent<TextMeshPro>();
            label.fontSize = 0.072f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = labelColor;
            label.rectTransform.sizeDelta = new Vector2(0.78f, 0.34f);
#if UNITY_6000_0_OR_NEWER
            label.textWrappingMode = TextWrappingModes.Normal;
#else
            label.enableWordWrapping = true;
#endif
            label.overflowMode = TextOverflowModes.Ellipsis;
        }

        private GameObject CreatePrimitive(string objectName, PrimitiveType primitiveType, Vector3 localPosition, Vector3 localScale, Color color, Material sharedMaterial = null)
        {
            GameObject primitive = GameObject.CreatePrimitive(primitiveType);
            primitive.name = objectName;
            primitive.transform.SetParent(transform, false);
            primitive.transform.localPosition = localPosition;
            primitive.transform.localScale = localScale;
            Collider primitiveCollider = primitive.GetComponent<Collider>();
            if (primitiveCollider != null)
            {
                primitiveCollider.enabled = false;
                DestroyGeneratedObject(primitiveCollider);
            }
            Renderer renderer = primitive.GetComponent<Renderer>();
            if (sharedMaterial != null)
            {
                renderer.sharedMaterial = sharedMaterial;
                return primitive;
            }
            var material = new Material(Shader.Find("Standard")) { color = color };
            renderer.sharedMaterial = material;
            generatedMaterials.Add(material);
            return primitive;
        }

        private static Color CreateResourceColor(string resourceName)
        {
            uint hash = 2166136261;
            foreach (char character in resourceName)
            {
                hash ^= character;
                hash *= 16777619;
            }
            return Color.HSVToRGB(hash % 360 / 360f, 0.42f, 0.78f);
        }

        public void SetHovered(bool hovered)
        {
            if (!hovered)
            {
                transform.localScale = Vector3.one;
                PresentBaseColor();
                return;
            }
            if (!isAvailableForHarvest || PlayableHuntInputGuard.IsBlocked) return;
            if (GetComponentInParent<PlayableHexTileCard3D>()?.IsFlipping == true) return;
            transform.localScale = Vector3.one * hoverScale;
            resourceRenderer.sharedMaterial.color = Color.Lerp(resourceColor, Color.white, 0.22f);
        }

        private void OnMouseEnter() => SetHovered(true);

        private void OnMouseExit() => SetHovered(false);

        private void PresentBaseColor()
        {
            if (resourceRenderer == null) return;
            resourceRenderer.sharedMaterial.color = isAvailableForHarvest ? resourceColor : Color.Lerp(resourceColor, baseColor, 0.58f);
        }

        private void OnDestroy()
        {
            foreach (Material material in generatedMaterials)
                DestroyGeneratedObject(material);
            generatedMaterials.Clear();
        }

        private static void DestroyGeneratedObject(Object target)
        {
            if (target == null) return;
            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }
    }
}
