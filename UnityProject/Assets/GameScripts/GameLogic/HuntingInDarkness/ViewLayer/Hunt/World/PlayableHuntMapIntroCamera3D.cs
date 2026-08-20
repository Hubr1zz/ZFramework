using System.Collections.Generic;
using UnityEngine;

namespace HuntingInDarkness.Hunt
{
    /// <summary>在地图完成构建后播放短暂桌面俯瞰，并在镜头稳定前阻止玩法输入。</summary>
    [DisallowMultipleComponent]
    public sealed class PlayableHuntMapIntroCamera3D : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float duration = 1.25f;
        [SerializeField, Range(20f, 80f)] private float pitch = 55f;
        [SerializeField, Min(1f)] private float playHeight = 10f;
        [SerializeField, Min(0.1f)] private float overviewScale = 1.25f;
        [SerializeField, Min(1f)] private float minimumOverviewHeight = 13f;
        [SerializeField, Min(1f)] private float maximumOverviewHeight = 20f;
        [SerializeField, Min(0f)] private float activationTimeout = 0.75f;

        private Camera targetCamera;
        private PlayableHuntMapIntroPlan plan;
        private float elapsed;
        private float pendingElapsed;
        private int inputOwnerId;
        private bool holdsInputGuard;
        private bool isPending;
        private bool isPresenting;

        public bool IsPresenting => isPending || isPresenting;
        public float Progress => !isPresenting || plan.Duration <= 0f ? isPending ? 0f : 1f : Mathf.Clamp01(elapsed / plan.Duration);
        public PlayableHuntMapIntroPlan Plan => plan;

        public void Present(Camera presentationCamera, IReadOnlyList<Vector3> tilePositions)
        {
            if (!PlayableHuntMapIntroPlanner.TryCreate(tilePositions, pitch, playHeight, overviewScale, minimumOverviewHeight, maximumOverviewHeight, duration, out PlayableHuntMapIntroPlan nextPlan) || presentationCamera == null)
            {
                SettleAndRelease();
                return;
            }

            targetCamera = presentationCamera;
            plan = nextPlan;
            elapsed = 0f;
            pendingElapsed = 0f;
            isPending = true;
            isPresenting = false;
            AcquireInputGuard();
        }

        public void Skip() => SettleAndRelease();

        private void LateUpdate()
        {
            if ((isPending || isPresenting) && targetCamera == null)
            {
                SettleAndRelease();
                return;
            }
            if (isPending)
            {
                pendingElapsed += Time.unscaledDeltaTime;
                HuntCameraController cameraController = targetCamera.GetComponent<HuntCameraController>();
                if (cameraController != null && !cameraController.enabled && pendingElapsed < Mathf.Max(0f, activationTimeout))
                    return;
                isPending = false;
                isPresenting = true;
                targetCamera.transform.SetPositionAndRotation(plan.OverviewPosition, plan.Rotation);
            }

            if (!isPresenting)
                return;
            elapsed += Time.unscaledDeltaTime;
            float progress = plan.Duration <= 0f ? 1f : Mathf.Clamp01(elapsed / plan.Duration);
            float easedProgress = progress * progress * (3f - 2f * progress);
            targetCamera.transform.SetPositionAndRotation(Vector3.Lerp(plan.OverviewPosition, plan.PlayPosition, easedProgress), plan.Rotation);
            if (progress < 1f)
                return;
            SettleAndRelease();
        }

        private void AcquireInputGuard()
        {
            if (holdsInputGuard)
                return;
            EnsureInputOwnerId();
            PlayableHuntInputGuard.Acquire(inputOwnerId);
            holdsInputGuard = true;
        }

        private void EnsureInputOwnerId()
        {
            if (inputOwnerId != 0)
                return;
#if UNITY_6000_5_OR_NEWER
            inputOwnerId = GetEntityId().GetHashCode();
#else
            inputOwnerId = GetInstanceID();
#endif
            if (inputOwnerId == 0)
                inputOwnerId = int.MinValue + 1;
        }

        private void SettleAndRelease()
        {
            if ((isPending || isPresenting) && targetCamera != null)
                targetCamera.transform.SetPositionAndRotation(plan.PlayPosition, plan.Rotation);
            isPending = false;
            isPresenting = false;
            elapsed = plan.Duration;
            if (!holdsInputGuard)
                return;
            PlayableHuntInputGuard.Release(inputOwnerId);
            holdsInputGuard = false;
        }

        private void OnDisable() => SettleAndRelease();
        private void OnDestroy() => SettleAndRelease();
    }
}
