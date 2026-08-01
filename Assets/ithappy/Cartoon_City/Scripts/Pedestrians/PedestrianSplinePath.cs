using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

namespace Pedestrians
{
    /// <summary>
    /// Place on the same GameObject as SplineContainer. Manages visibility of baked pedestrians by distance to camera.
    /// </summary>
    public class PedestrianSplinePath : MonoBehaviour
    {
        [SerializeField] private List<GameObject> characterPrefabs = new List<GameObject>();
#pragma warning disable CS0414 // Used by Editor when baking
        [SerializeField] private float speedMin = 0.8f;
        [SerializeField] private float speedMax = 1.8f;
        [SerializeField] private float spacingMin = 3f;
        [SerializeField] private float spacingMax = 8f;
#pragma warning restore CS0414
        [SerializeField] private float visibilityDistance = 60f;
        [SerializeField] private float visibilityCheckInterval = 0.2f;
        [SerializeField] private bool disableAnimatorWhenHidden = true;
        [SerializeField] private bool disableRenderersWhenHidden = true;
        [SerializeField] private Transform cameraOverride;
        [SerializeField] private int randomSeed;
        [SerializeField] private bool drawGizmos;
        [SerializeField] private bool simulateWhenHidden = false;

        private readonly List<PedestrianSplineWalker> walkers = new List<PedestrianSplineWalker>();
        private float visibilityTimer;
        private bool noCameraWarningLogged;

        public bool SimulateWhenHidden => simulateWhenHidden;

        private void Start()
        {
            RefreshWalkerList();
        }

        /// <summary>
        /// Editor can call this after baking to register walkers. At runtime we also refresh from children.
        /// </summary>
        public void RefreshWalkerList()
        {
            walkers.Clear();
            Transform pedRoot = transform.Find("Pedestrians");
            if (pedRoot != null)
            {
                var list = pedRoot.GetComponentsInChildren<PedestrianSplineWalker>(true);
                for (int i = 0; i < list.Length; i++)
                    if (list[i] != null) walkers.Add(list[i]);
            }
        }

        public void RegisterWalker(PedestrianSplineWalker w)
        {
            if (w != null && !walkers.Contains(w)) walkers.Add(w);
        }

        private void Update()
        {
            visibilityTimer -= Time.deltaTime;
            if (visibilityTimer > 0) return;
            visibilityTimer = Mathf.Max(0.0001f, visibilityCheckInterval);

            Camera cam = cameraOverride != null ? cameraOverride.GetComponent<Camera>() : null;
            if (cam == null) cam = Camera.main;
            if (cam == null)
            {
                if (!noCameraWarningLogged)
                {
                    noCameraWarningLogged = true;
                    Debug.LogWarning("PedestrianSplinePath: No camera (Camera.main or cameraOverride). Treating all as visible.");
                }
                SetAllVisible(true);
                return;
            }

            Vector3 camPos = cam.transform.position;
            for (int i = 0; i < walkers.Count; i++)
            {
                PedestrianSplineWalker w = walkers[i];
                if (w == null) continue;
                float dist = Vector3.Distance(w.transform.position, camPos);
                bool visible = dist <= visibilityDistance;
                w.SetVisible(visible, disableRenderersWhenHidden, disableAnimatorWhenHidden);
            }
        }

        private void SetAllVisible(bool visible)
        {
            for (int i = 0; i < walkers.Count; i++)
                if (walkers[i] != null)
                    walkers[i].SetVisible(visible, disableRenderersWhenHidden, disableAnimatorWhenHidden);
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;
            Gizmos.color = new Color(0f, 1f, 0f, 0.15f);
            Gizmos.DrawWireSphere(transform.position, visibilityDistance);
        }
    }
}
