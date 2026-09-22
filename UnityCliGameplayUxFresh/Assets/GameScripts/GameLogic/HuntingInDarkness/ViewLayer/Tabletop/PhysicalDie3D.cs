using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace HuntingInDarkness.ViewLayer.Tabletop
{
    /// <summary>运行时生成的实体骰子；当前支持事件流程使用的 d10 与通用 d6。</summary>
    public sealed class PhysicalDie3D : MonoBehaviour
    {
        private Vector3[] faceNormals = Array.Empty<Vector3>();
        private int[] faceValues = Array.Empty<int>();
        private Mesh ownedMesh;

        public Rigidbody Body { get; private set; }
        public int Sides { get; private set; }

        public static PhysicalDie3D Create(int sides, Transform parent, Vector3 position, float size, Material material)
        {
            if (sides != 6 && sides != 10) throw new NotSupportedException($"暂不支持 d{sides} 物理骰子。");
            return sides == 10 ? CreateD10(parent, position, size, material) : CreateD6(parent, position, size, material);
        }

        public int GetUpwardValue() => ResolveUpwardValue(faceNormals, faceValues, transform.rotation);

        public static int ResolveUpwardValue(IReadOnlyList<Vector3> localFaceNormals, IReadOnlyList<int> values, Quaternion rotation)
        {
            if (localFaceNormals == null || values == null || localFaceNormals.Count == 0 || localFaceNormals.Count != values.Count) return 0;
            int result = values[0];
            float bestDot = float.NegativeInfinity;
            for (int index = 0; index < localFaceNormals.Count; index++)
            {
                float dot = Vector3.Dot(rotation * localFaceNormals[index], Vector3.up);
                if (dot <= bestDot) continue;
                bestDot = dot;
                result = values[index];
            }
            return result;
        }

        private static PhysicalDie3D CreateD6(Transform parent, Vector3 position, float size, Material material)
        {
            GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gameObject.name = "PhysicalDie_d6";
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.position = position;
            gameObject.transform.localScale = Vector3.one * size;
            gameObject.GetComponent<Renderer>().sharedMaterial = material;
            var die = gameObject.AddComponent<PhysicalDie3D>();
            die.Sides = 6;
            die.faceNormals = new[] { Vector3.up, Vector3.down, Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
            die.faceValues = new[] { 1, 6, 3, 4, 2, 5 };
            die.ConfigureBody();
            for (int index = 0; index < die.faceNormals.Length; index++)
                die.BuildFaceLabel(die.faceValues[index], die.faceNormals[index] * 0.505f, die.faceNormals[index], 0.18f);
            return die;
        }

        private static PhysicalDie3D CreateD10(Transform parent, Vector3 position, float size, Material material)
        {
            var gameObject = new GameObject("PhysicalDie_d10");
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.position = position;
            gameObject.transform.localScale = Vector3.one * size;
            var die = gameObject.AddComponent<PhysicalDie3D>();
            die.Sides = 10;
            die.BuildD10Mesh(material);
            die.ConfigureBody();
            return die;
        }

        private void ConfigureBody()
        {
            Body = gameObject.AddComponent<Rigidbody>();
            Body.mass = 0.12f;
            Body.linearDamping = 0.10f;
            Body.angularDamping = 0.12f;
            Body.maxAngularVelocity = 40f;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
        }

        private void BuildD10Mesh(Material material)
        {
            const int ringCount = 5;
            const float radius = 0.58f;
            const float height = 0.72f;
            var ring = new Vector3[ringCount];
            for (int index = 0; index < ringCount; index++)
            {
                float angle = Mathf.PI * 2f * index / ringCount - Mathf.PI * 0.5f;
                ring[index] = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            }

            var vertices = new List<Vector3>(30);
            var triangles = new List<int>(30);
            var normals = new List<Vector3>(30);
            var resolvedNormals = new List<Vector3>(10);
            var resolvedValues = new List<int>(10);
            Vector3 top = Vector3.up * height;
            Vector3 bottom = Vector3.down * height;
            for (int index = 0; index < ringCount; index++)
            {
                AddTriangleFace(vertices, triangles, normals, resolvedNormals, top, ring[index], ring[(index + 1) % ringCount]);
                resolvedValues.Add(index + 1);
            }
            for (int index = 0; index < ringCount; index++)
            {
                AddTriangleFace(vertices, triangles, normals, resolvedNormals, bottom, ring[(index + 1) % ringCount], ring[index]);
                resolvedValues.Add(index + 6);
            }

            ownedMesh = new Mesh { name = "RuntimeD10Mesh" };
            ownedMesh.SetVertices(vertices);
            ownedMesh.SetTriangles(triangles, 0);
            ownedMesh.SetNormals(normals);
            ownedMesh.RecalculateBounds();
            gameObject.AddComponent<MeshFilter>().sharedMesh = ownedMesh;
            gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            MeshCollider collider = gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = ownedMesh;
            collider.convex = true;
            faceNormals = resolvedNormals.ToArray();
            faceValues = resolvedValues.ToArray();
            for (int index = 0; index < faceNormals.Length; index++)
            {
                int vertexIndex = index * 3;
                Vector3 center = (vertices[vertexIndex] + vertices[vertexIndex + 1] + vertices[vertexIndex + 2]) / 3f;
                BuildFaceLabel(faceValues[index], center + faceNormals[index] * 0.012f, faceNormals[index], 0.16f);
            }
        }

        private static void AddTriangleFace(List<Vector3> vertices, List<int> triangles, List<Vector3> normals, List<Vector3> resolvedNormals, Vector3 first, Vector3 second, Vector3 third)
        {
            Vector3 normal = Vector3.Cross(second - first, third - first).normalized;
            Vector3 center = (first + second + third) / 3f;
            if (Vector3.Dot(normal, center) < 0f)
            {
                (second, third) = (third, second);
                normal = -normal;
            }
            int start = vertices.Count;
            vertices.Add(first);
            vertices.Add(second);
            vertices.Add(third);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            resolvedNormals.Add(normal);
        }

        private void BuildFaceLabel(int value, Vector3 localPosition, Vector3 normal, float fontSize)
        {
            var labelObject = new GameObject($"Face_{value}");
            labelObject.transform.SetParent(transform, false);
            labelObject.transform.localPosition = localPosition;
            Vector3 labelUp = Vector3.ProjectOnPlane(Vector3.up, normal).normalized;
            if (labelUp.sqrMagnitude < 0.01f)
                labelUp = Vector3.ProjectOnPlane(Vector3.forward, normal).normalized;
            labelObject.transform.localRotation = Quaternion.LookRotation(normal, labelUp);
            TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
            label.text = value.ToString();
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(0.96f, 0.90f, 0.68f);
            label.rectTransform.sizeDelta = new Vector2(0.30f, 0.20f);
        }

        private void OnDestroy()
        {
            if (ownedMesh != null)
                Destroy(ownedMesh);
        }
    }
}
