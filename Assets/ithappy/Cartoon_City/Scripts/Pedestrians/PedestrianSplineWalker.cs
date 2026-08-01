using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

namespace Pedestrians
{
    /// <summary>
    /// Moves a character along a closed spline. Visibility and movement pause are managed by PedestrianSplinePath.
    /// </summary>
    public class PedestrianSplineWalker : MonoBehaviour
    {
        [SerializeField] private SplineContainer container;
        [SerializeField] private int splineIndex;
        [SerializeField] private float speed;
        [SerializeField] private float distanceAlong;
        [SerializeField] private bool isVisible = true;

        private float splineLength;
        private PedestrianSplinePath path;
        private List<Renderer> renderers;
        private List<SkinnedMeshRenderer> skinnedRenderers;
        private Animator animator;

        public bool IsVisible => isVisible;

        private void Awake()
        {
            renderers = new List<Renderer>(GetComponentsInChildren<Renderer>(true));
            skinnedRenderers = new List<SkinnedMeshRenderer>(GetComponentsInChildren<SkinnedMeshRenderer>(true));
            animator = GetComponentInChildren<Animator>(true);
        }

        /// <summary>
        /// Restore splineLength and path at runtime (they are set in editor by Initialize but not serialized).
        /// </summary>
        private void EnsureInitialized()
        {
            if (splineLength > 0 && path != null) return;

            if (container != null && splineLength <= 0 && container.Splines != null &&
                splineIndex >= 0 && splineIndex < container.Splines.Count)
            {
                splineLength = container.Splines[splineIndex].GetLength();
            }

            if (path == null)
            {
                Transform t = transform.parent;
                if (t != null && t.parent != null)
                    path = t.parent.GetComponent<PedestrianSplinePath>();
            }
        }

        /// <summary>
        /// Call once after spawn. lengthCached should be the spline length for splineIndex (e.g. container.Splines[splineIndex].GetLength()).
        /// </summary>
        public void Initialize(SplineContainer container, int splineIndex, float speed, float startDistance, float lengthCached, PedestrianSplinePath pathRef)
        {
            if (container == null) return;
            this.container = container;
            this.splineIndex = splineIndex;
            this.speed = speed;
            this.distanceAlong = startDistance;
            this.splineLength = lengthCached > 0 ? lengthCached : container.Splines[splineIndex].GetLength();
            this.path = pathRef;
            ApplyPositionAndRotation();
        }

        /// <summary>
        /// Path calls this with visibility and its flags. When hidden, renderers/animator are disabled only if the path flags say so.
        /// </summary>
        public void SetVisible(bool visible, bool disableRenderersWhenHidden, bool disableAnimatorWhenHidden)
        {
            if (isVisible == visible) return;
            isVisible = visible;

            bool enableRenderers = visible || !disableRenderersWhenHidden;
            bool enableAnimator = visible || !disableAnimatorWhenHidden;

            if (renderers != null)
                for (int i = 0; i < renderers.Count; i++)
                    if (renderers[i] != null) renderers[i].enabled = enableRenderers;
            if (skinnedRenderers != null)
                for (int i = 0; i < skinnedRenderers.Count; i++)
                    if (skinnedRenderers[i] != null) skinnedRenderers[i].enabled = enableRenderers;
            if (animator != null)
                animator.enabled = enableAnimator;
        }

        private void Start()
        {
            EnsureInitialized();
        }

        private void Update()
        {
            EnsureInitialized();
            bool shouldMove = isVisible || (path != null && path.SimulateWhenHidden);
            if (!shouldMove || container == null || splineLength <= 0) return;

            distanceAlong += speed * Time.deltaTime;
            distanceAlong = Mathf.Repeat(distanceAlong, splineLength);
            ApplyPositionAndRotation();
        }

        private void ApplyPositionAndRotation()
        {
            if (container == null || container.Splines == null || splineIndex < 0 || splineIndex >= container.Splines.Count)
                return;
            if (splineLength <= 0) return;

            // Normalized t for closed spline: distance along / length wrapped to [0,1)
            float t = (distanceAlong % splineLength) / splineLength;
            if (t < 0) t += 1f;

            container.Evaluate(splineIndex, t, out float3 pos, out float3 tangent, out float3 up);
            transform.position = new Vector3(pos.x, pos.y, pos.z);
            Vector3 tan = new Vector3(tangent.x, tangent.y, tangent.z);
            if (tan.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(tan, Vector3.up);
        }
    }
}
