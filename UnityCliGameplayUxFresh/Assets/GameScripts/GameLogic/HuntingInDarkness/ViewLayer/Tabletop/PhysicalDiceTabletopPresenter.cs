using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using HuntingInDarkness.ActionFlow.Presentation;
using TMPro;
using UnityEngine;

namespace HuntingInDarkness.ViewLayer.Tabletop
{
    /// <summary>在目标桌面物件附近投掷实体骰子，等待刚体稳定后回传可验证结果。</summary>
    public sealed class PhysicalDiceTabletopPresenter : MonoBehaviour, ITabletopRandomInteractionPresenter
    {
        [SerializeField, Min(0.15f)] private float dieSize = 0.34f;
        [SerializeField, Min(1)] private int maxDiceCount = 12;
        [SerializeField, Min(0.5f)] private float trayWidth = 2.0f;
        [SerializeField, Min(0.5f)] private float trayDepth = 1.55f;
        [SerializeField, Min(0.1f)] private float launchHeight = 1.25f;
        [SerializeField, Min(0f)] private float throwImpulse = 1.25f;
        [SerializeField, Min(0f)] private float torqueImpulse = 1.6f;
        [SerializeField, Min(0.01f)] private float stableLinearSpeed = 0.06f;
        [SerializeField, Min(0.01f)] private float stableAngularSpeed = 0.15f;
        [SerializeField, Min(0.05f)] private float stableDuration = 0.45f;
        [SerializeField, Min(1f)] private float settleTimeout = 8f;
        [SerializeField, Min(0f)] private float resultDisplayDuration = 1.1f;
        [SerializeField] private Material diceMaterialTemplate;
        [SerializeField] private Material trayMaterialTemplate;
        [SerializeField] private TMP_FontAsset resultFont;

        private bool isPresenting;

        public Func<TabletopRandomInteractionRequest, Vector3> AnchorResolver { private get; set; }
        public bool IsPresenting => isPresenting;
        public TabletopRandomInteractionResult LastCompletedResult { get; private set; }

        public async UniTask<TabletopRandomInteractionResult> PresentAsync(TabletopRandomInteractionRequest request, CancellationToken cancellationToken)
        {
            if (request.Kind != TabletopRandomInteractionKind.PhysicalDice) throw new NotSupportedException($"尚未实现 {request.Kind} 的桌面表现器。");
            if (request.Sides != 6 && request.Sides != 10) throw new NotSupportedException($"当前物理骰子只支持 d6 与 d10，收到 d{request.Sides}。");
            if (request.Count > maxDiceCount) throw new InvalidOperationException($"单次实体骰子数量不能超过 {maxDiceCount}。收到 {request.Count}。");
            while (isPresenting)
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);

            isPresenting = true;
            GameObject interactionRoot = null;
            Material diceMaterial = null;
            Material trayMaterial = null;
            try
            {
                Vector3 anchor = AnchorResolver != null ? AnchorResolver.Invoke(request) : transform.position;
                interactionRoot = new GameObject($"TabletopDice_{request.InteractionId}");
                interactionRoot.transform.position = anchor + Vector3.up * 0.10f;
                diceMaterial = diceMaterialTemplate != null ? diceMaterialTemplate : CreateMaterial(new Color(0.20f, 0.09f, 0.07f));
                trayMaterial = trayMaterialTemplate != null ? trayMaterialTemplate : CreateMaterial(new Color(0.16f, 0.10f, 0.065f));
                BuildTray(interactionRoot.transform, trayMaterial);
                List<PhysicalDie3D> dice = CreateDice(request, interactionRoot.transform, diceMaterial);
                ThrowDice(dice);
                await WaitForStableAsync(dice, cancellationToken);

                var values = new List<int>(dice.Count);
                foreach (PhysicalDie3D die in dice)
                    values.Add(die.GetUpwardValue());
                BuildResultLabel(interactionRoot.transform, values);
                if (resultDisplayDuration > 0f)
                    await UniTask.Delay(TimeSpan.FromSeconds(resultDisplayDuration), cancellationToken: cancellationToken);
                LastCompletedResult = new TabletopRandomInteractionResult(request.InteractionId, values, Array.Empty<string>());
                return LastCompletedResult;
            }
            finally
            {
                if (interactionRoot != null) Destroy(interactionRoot);
                if (diceMaterial != null && diceMaterial != diceMaterialTemplate) Destroy(diceMaterial);
                if (trayMaterial != null && trayMaterial != trayMaterialTemplate) Destroy(trayMaterial);
                isPresenting = false;
            }
        }

        private List<PhysicalDie3D> CreateDice(TabletopRandomInteractionRequest request, Transform parent, Material material)
        {
            var dice = new List<PhysicalDie3D>(request.Count);
            float spacing = dieSize * 1.35f;
            float start = -(request.Count - 1) * spacing * 0.5f;
            for (int index = 0; index < request.Count; index++)
            {
                Vector3 position = parent.position + new Vector3(start + index * spacing, launchHeight + index * 0.05f, 0f);
                PhysicalDie3D die = PhysicalDie3D.Create(request.Sides, parent, position, dieSize, material);
                die.transform.rotation = UnityEngine.Random.rotationUniform;
                dice.Add(die);
            }
            return dice;
        }

        private void ThrowDice(IReadOnlyList<PhysicalDie3D> dice)
        {
            foreach (PhysicalDie3D die in dice)
            {
                Vector3 horizontal = new Vector3(UnityEngine.Random.Range(-0.35f, 0.35f), 0f, UnityEngine.Random.Range(-0.25f, 0.25f));
                die.Body.AddForce(horizontal + Vector3.down * throwImpulse * 0.16f, ForceMode.Impulse);
                die.Body.AddTorque(UnityEngine.Random.onUnitSphere * torqueImpulse, ForceMode.Impulse);
            }
        }

        private async UniTask WaitForStableAsync(IReadOnlyList<PhysicalDie3D> dice, CancellationToken cancellationToken)
        {
            float startedAt = Time.time;
            float stableSince = -1f;
            while (Time.time - startedAt < settleTimeout)
            {
                cancellationToken.ThrowIfCancellationRequested();
                bool allStable = true;
                foreach (PhysicalDie3D die in dice)
                {
                    Rigidbody body = die.Body;
                    if (body.IsSleeping()) continue;
                    if (body.linearVelocity.sqrMagnitude <= stableLinearSpeed * stableLinearSpeed && body.angularVelocity.sqrMagnitude <= stableAngularSpeed * stableAngularSpeed) continue;
                    allStable = false;
                    break;
                }
                if (!allStable)
                {
                    stableSince = -1f;
                }
                else
                {
                    if (stableSince < 0f) stableSince = Time.time;
                    if (Time.time - stableSince >= stableDuration) return;
                }
                await UniTask.Yield(PlayerLoopTiming.FixedUpdate, cancellationToken);
            }

            foreach (PhysicalDie3D die in dice)
            {
                die.Body.linearVelocity = Vector3.zero;
                die.Body.angularVelocity = Vector3.zero;
                die.Body.Sleep();
            }
        }

        private void BuildTray(Transform parent, Material material)
        {
            CreateTrayPart("Base", parent, new Vector3(0f, -0.05f, 0f), new Vector3(trayWidth, 0.10f, trayDepth), material);
            const float wallHeight = 0.28f;
            const float wallThickness = 0.08f;
            CreateTrayPart("WallLeft", parent, new Vector3(-trayWidth * 0.5f, wallHeight * 0.5f, 0f), new Vector3(wallThickness, wallHeight, trayDepth), material);
            CreateTrayPart("WallRight", parent, new Vector3(trayWidth * 0.5f, wallHeight * 0.5f, 0f), new Vector3(wallThickness, wallHeight, trayDepth), material);
            CreateTrayPart("WallFront", parent, new Vector3(0f, wallHeight * 0.5f, trayDepth * 0.5f), new Vector3(trayWidth, wallHeight, wallThickness), material);
            CreateTrayPart("WallBack", parent, new Vector3(0f, wallHeight * 0.5f, -trayDepth * 0.5f), new Vector3(trayWidth, wallHeight, wallThickness), material);
        }

        private static void CreateTrayPart(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            part.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static Material CreateMaterial(Color color)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("当前渲染管线未提供 Standard Shader，请为实体骰子配置材质模板。");
            var material = new Material(shader) { color = color };
            material.SetFloat("_Glossiness", 0.18f);
            return material;
        }

        private void BuildResultLabel(Transform parent, IReadOnlyList<int> values)
        {
            int total = 0;
            foreach (int value in values)
                total += value;
            var labelObject = new GameObject("DiceResult");
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.localPosition = new Vector3(0f, 0.06f, -1.0f);
            labelObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
            label.text = values.Count == 1 ? $"ROLL  {total}" : $"ROLL  {string.Join(" + ", values)} = {total}";
            if (resultFont != null)
                label.font = resultFont;
            label.fontSize = 0.15f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(0.98f, 0.86f, 0.42f);
            label.rectTransform.sizeDelta = new Vector2(2.2f, 0.28f);
        }
    }
}
