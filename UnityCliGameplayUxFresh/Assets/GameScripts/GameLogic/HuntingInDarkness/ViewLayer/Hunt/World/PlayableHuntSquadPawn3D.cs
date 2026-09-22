using System.Collections.Generic;
using UnityEngine;

namespace HuntingInDarkness.Hunt
{
    /// <summary>把已提交的小队位置表现为可等待的桌游棋子移动，不持有地图权威状态。</summary>
    [DisallowMultipleComponent]
    public sealed class PlayableHuntSquadPawn3D : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float moveDuration = 0.38f;
        [SerializeField, Min(0f)] private float hopHeight = 0.34f;
        [SerializeField] private Color baseColor = new(0.24f, 0.18f, 0.10f);
        [SerializeField] private Color primaryPawnColor = new(0.96f, 0.78f, 0.24f);
        [SerializeField] private Color secondaryPawnColor = new(0.72f, 0.34f, 0.20f);

        private readonly List<GameObject> hunterPawns = new();
        private readonly List<Material> generatedMaterials = new();
        private Vector3 moveStart;
        private Vector3 moveTarget;
        private float moveElapsed;
        private int inputOwnerId;
        private bool holdsInputGuard;
        private bool isBuilt;
        private bool isMoving;

        public int HunterCount { get; private set; }
        public bool IsMoving => isMoving;
        public float MoveProgress => !isMoving || moveDuration <= 0f ? 1f : Mathf.Clamp01(moveElapsed / moveDuration);

        public void Initialize(int hunterCount)
        {
            EnsureBuilt();
            HunterCount = Mathf.Clamp(hunterCount, 0, hunterPawns.Count);
            for (int index = 0; index < hunterPawns.Count; index++)
                hunterPawns[index].SetActive(index < HunterCount);
        }

        public void Place(Vector3 worldPosition, bool immediate)
        {
            if (!isBuilt)
                Initialize(1);
            if (immediate || moveDuration <= 0f || Vector3.SqrMagnitude(transform.position - worldPosition) <= 0.0001f)
            {
                transform.position = worldPosition;
                moveTarget = worldPosition;
                isMoving = false;
                moveElapsed = moveDuration;
                ReleaseInputGuard();
                return;
            }

            moveStart = transform.position;
            moveTarget = worldPosition;
            moveElapsed = 0f;
            isMoving = true;
            Vector3 direction = moveTarget - moveStart;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            AcquireInputGuard();
        }

        private void Update()
        {
            if (!isMoving)
                return;
            moveElapsed += Time.deltaTime;
            float progress = moveDuration <= 0f ? 1f : Mathf.Clamp01(moveElapsed / moveDuration);
            float easedProgress = progress * progress * (3f - 2f * progress);
            Vector3 position = Vector3.Lerp(moveStart, moveTarget, easedProgress);
            position.y += Mathf.Sin(progress * Mathf.PI) * hopHeight;
            transform.position = position;
            if (progress < 1f)
                return;
            transform.position = moveTarget;
            isMoving = false;
            ReleaseInputGuard();
        }

        private void EnsureBuilt()
        {
            if (isBuilt)
                return;
            isBuilt = true;
#if UNITY_6000_5_OR_NEWER
            inputOwnerId = GetEntityId().GetHashCode();
#else
            inputOwnerId = GetInstanceID();
#endif
            if (inputOwnerId == 0)
                inputOwnerId = int.MinValue;
            CreatePrimitive("Pawn Base", PrimitiveType.Cylinder, new Vector3(0f, -0.22f, 0f), new Vector3(0.36f, 0.035f, 0.36f), baseColor);
            Vector3[] pawnPositions =
            {
                new(-0.14f, 0f, 0.12f),
                new(0.14f, 0f, 0.12f),
                new(-0.14f, 0f, -0.12f),
                new(0.14f, 0f, -0.12f)
            };
            for (int index = 0; index < pawnPositions.Length; index++)
                hunterPawns.Add(CreatePrimitive($"Hunter Pawn {index + 1}", PrimitiveType.Capsule, pawnPositions[index], new Vector3(0.11f, 0.18f, 0.11f), index % 2 == 0 ? primaryPawnColor : secondaryPawnColor));
        }

        private GameObject CreatePrimitive(string objectName, PrimitiveType primitiveType, Vector3 localPosition, Vector3 localScale, Color color)
        {
            GameObject primitive = GameObject.CreatePrimitive(primitiveType);
            primitive.name = objectName;
            primitive.transform.SetParent(transform, false);
            primitive.transform.localPosition = localPosition;
            primitive.transform.localScale = localScale;
            Collider primitiveCollider = primitive.GetComponent<Collider>();
            if (primitiveCollider != null)
                Destroy(primitiveCollider);
            Renderer primitiveRenderer = primitive.GetComponent<Renderer>();
            Material material = new(Shader.Find("Standard")) { color = color };
            primitiveRenderer.material = material;
            generatedMaterials.Add(material);
            return primitive;
        }

        private void AcquireInputGuard()
        {
            if (holdsInputGuard)
                return;
            PlayableHuntInputGuard.Acquire(inputOwnerId);
            holdsInputGuard = true;
        }

        private void ReleaseInputGuard()
        {
            if (!holdsInputGuard)
                return;
            PlayableHuntInputGuard.Release(inputOwnerId);
            holdsInputGuard = false;
        }

        private void OnDisable()
        {
            if (isMoving)
                transform.position = moveTarget;
            isMoving = false;
            ReleaseInputGuard();
        }

        private void OnDestroy()
        {
            ReleaseInputGuard();
            foreach (Material material in generatedMaterials)
                if (material != null) Destroy(material);
            generatedMaterials.Clear();
        }
    }
}
