using HuntingInDarkness.Data;
using UnityEngine;

namespace HuntingInDarkness.Hunt
{
    /// <summary>为无 Prefab 的可游玩组合根创建 pointy-top 六边形薄棱柱。</summary>
    public static class PlayableHexTileFactory
    {
        private const int SideCount = 6;
        private const float Thickness = 0.16f;

        public static GameObject Create(float radius)
        {
            float safeRadius = Mathf.Max(0.1f, radius);
            var gameObject = new GameObject("HexTile", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider), typeof(PlayableHexTileCard3D));
            var mesh = CreateMesh(safeRadius, Thickness);
            gameObject.GetComponent<MeshFilter>().sharedMesh = mesh;
            gameObject.GetComponent<MeshCollider>().sharedMesh = mesh;
            gameObject.GetComponent<PlayableHexTileCard3D>().Initialize(safeRadius, Thickness);
            return gameObject;
        }

        private static Mesh CreateMesh(float radius, float thickness)
        {
            float halfHeight = thickness * 0.5f;
            var vertices = new Vector3[14];
            vertices[0] = new Vector3(0f, halfHeight, 0f);
            vertices[7] = new Vector3(0f, -halfHeight, 0f);
            for (int i = 0; i < SideCount; i++)
            {
                float angle = Mathf.Deg2Rad * (30f + i * 60f);
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                vertices[i + 1] = new Vector3(x, halfHeight, z);
                vertices[i + 8] = new Vector3(x, -halfHeight, z);
            }

            var triangles = new int[SideCount * 12];
            int triangleIndex = 0;
            for (int i = 0; i < SideCount; i++)
            {
                int next = (i + 1) % SideCount;
                AddTriangle(triangles, ref triangleIndex, 0, next + 1, i + 1);
                AddTriangle(triangles, ref triangleIndex, 7, i + 8, next + 8);
                AddTriangle(triangles, ref triangleIndex, i + 1, next + 1, next + 8);
                AddTriangle(triangles, ref triangleIndex, i + 1, next + 8, i + 8);
            }

            var mesh = new Mesh { name = "Playable Hex Tile" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddTriangle(int[] triangles, ref int index, int a, int b, int c)
        {
            triangles[index++] = a;
            triangles[index++] = b;
            triangles[index++] = c;
        }
    }

    /// <summary>六边形地块的轻量状态标签，不持有地图权威数据。</summary>
    public sealed class PlayableHexTileView : MonoBehaviour
    {
        private TextMesh label;

        public void Initialize(float radius, float thickness)
        {
            var labelObject = new GameObject("Tile Label");
            labelObject.transform.SetParent(transform, false);
            labelObject.transform.localPosition = new Vector3(0f, thickness * 0.5f + 0.025f, 0f);
            labelObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            label = labelObject.AddComponent<TextMesh>();
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 48;
            label.fontStyle = FontStyle.Bold;
            label.characterSize = radius * 0.025f;
            label.color = Color.white;
        }

        public void Present(HexTileInstance tile, TileState state)
        {
            if (label == null) return;
            if (state == TileState.Locked)
            {
                label.text = string.Empty;
                return;
            }

            if (state == TileState.Interactable)
            {
                label.text = "可探索";
                label.color = new Color(0.82f, 0.92f, 1f);
                return;
            }

            label.text = tile?.Config != null ? tile.Config.tileName : "未知地块";
            label.color = Color.white;
        }

        private void OnDestroy()
        {
            var mesh = GetComponent<MeshFilter>()?.sharedMesh;
            if (mesh != null)
                Destroy(mesh);
            var material = GetComponent<Renderer>()?.sharedMaterial;
            if (material != null)
                Destroy(material);
        }
    }
}
