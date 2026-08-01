using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace ithappy
{
    /// <summary>
    /// Moves a train (root with several child car prefabs) along a <see cref="SplineContainer"/> — e.g. scene objects
    /// <c>RollerCoaster_01_Spline</c> / <c>RollerCoaster_02_Spline</c>. Lead car advances by speed; each car follows at a fixed distance behind along the track.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RollerCoasterTrainController : MonoBehaviour
    {
        [Header("Spline")]
        [Tooltip("Spline Container from the scene (GameObject with RollerCoaster_XX_Spline).")]
        [SerializeField] private SplineContainer splineContainer;

        [SerializeField] private int splineIndex;

        [Header("Motion")]
        [SerializeField] private float speedMetersPerSecond = 8f;

        [Tooltip("Distance in meters along the rail for the front of the train (first car).")]
        [SerializeField] private float leadDistanceAlong;

        [Tooltip("When the track is open (not closed), repeat the ride from the start after the end.")]
        [SerializeField] private bool loopOpenTrack = true;

        [Header("Train (nested prefabs)")]
        [Tooltip("If empty, direct children of this object are used in hierarchy order (first child = front).")]
        [SerializeField] private Transform[] carRoots;

        [Tooltip("Uniform spacing in meters between consecutive cars along the rail (rear is further back along the path).")]
        [SerializeField] private float spacingMeters = 4f;

        [Header("Orientation")]
        [SerializeField] private bool useSplineUpForRotation = true;

        private void Awake()
        {
            if (carRoots == null || carRoots.Length == 0)
            {
                int n = transform.childCount;
                carRoots = new Transform[n];
                for (int i = 0; i < n; i++)
                    carRoots[i] = transform.GetChild(i);
            }
        }

        private void Update()
        {
            if (splineContainer == null || splineContainer.Splines == null ||
                splineIndex < 0 || splineIndex >= splineContainer.Splines.Count)
                return;

            Spline spline = splineContainer.Splines[splineIndex];
            float length = spline.GetLength();
            if (length <= 0.0001f) return;

            bool closed = spline.Closed;
            leadDistanceAlong += speedMetersPerSecond * Time.deltaTime;
            leadDistanceAlong = WrapLeadDistance(leadDistanceAlong, length, closed, loopOpenTrack);

            if (carRoots == null) return;

            for (int i = 0; i < carRoots.Length; i++)
            {
                if (carRoots[i] == null) continue;
                float d = leadDistanceAlong - i * spacingMeters;
                d = WrapCarDistance(d, length, closed, loopOpenTrack);
                PlaceOnSpline(spline, d, carRoots[i]);
            }
        }

        private static float WrapLeadDistance(float d, float length, bool closed, bool loopOpenTrack)
        {
            if (closed)
                return (d % length + length) % length;

            if (loopOpenTrack)
                return (d % length + length) % length;

            return Mathf.Min(d, length);
        }

        private static float WrapCarDistance(float d, float length, bool closed, bool loopOpenTrack)
        {
            if (closed)
                return (d % length + length) % length;

            if (loopOpenTrack)
                return (d % length + length) % length;

            return Mathf.Clamp(d, 0f, length);
        }

        private void PlaceOnSpline(Spline spline, float distanceAlong, Transform car)
        {
            float t = SplineUtility.GetNormalizedInterpolation(spline, distanceAlong, PathIndexUnit.Distance);
            splineContainer.Evaluate(splineIndex, t, out float3 pos, out float3 tangent, out float3 up);

            car.position = new Vector3(pos.x, pos.y, pos.z);

            Vector3 f = new Vector3(tangent.x, tangent.y, tangent.z);
            if (f.sqrMagnitude < 1e-8f) return;

            if (useSplineUpForRotation)
            {
                Vector3 u = new Vector3(up.x, up.y, up.z);
                if (u.sqrMagnitude < 1e-8f) u = Vector3.up;
                car.rotation = Quaternion.LookRotation(f.normalized, u.normalized);
            }
            else
            {
                car.rotation = Quaternion.LookRotation(f.normalized, Vector3.up);
            }
        }

        /// <summary>Assign spline from code or use in other scripts.</summary>
        public void SetSpline(SplineContainer container, int index)
        {
            splineContainer = container;
            splineIndex = index;
        }
    }
}
