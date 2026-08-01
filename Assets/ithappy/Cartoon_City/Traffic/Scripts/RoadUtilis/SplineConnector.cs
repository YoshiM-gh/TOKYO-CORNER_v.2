using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ITHappy
{
    public class SplineConnector : MonoBehaviour
    {
        [SerializeField]
        private float m_MergeDistance = 0.05f;

        [SerializeField]
        private TrafficManager.SplineData[] m_Splines;
        [SerializeField]
        private TrafficManager.CrossData[] m_Crosses;

        public TrafficManager.SplineData[] Splines => m_Splines;
        public TrafficManager.CrossData[] Crosses => m_Crosses;

        [ContextMenu("GenArrays")]
        public void GenerateArrays()
        {
            GetCrosses(out var crosses);
            GenerateSplineList(crosses, out var splines);

            splinesArray();
            crossesArray();


            void splinesArray()
            {
                const int maxSplines = 8192;
                m_Splines = new TrafficManager.SplineData[maxSplines];
                int copyCount = Mathf.Min(splines.Count, maxSplines);
                if (splines.Count > maxSplines)
                    Debug.LogWarning($"SplineConnector: scene has {splines.Count} spline segments, but traffic system supports max {maxSplines}. Only the first {maxSplines} will be used.");
                for (int i = 0; i < copyCount; i++)
                    m_Splines[i] = splines[i];
                for (int i = copyCount; i < m_Splines.Length; i++)
                    m_Splines[i] = TrafficManager.SplineData.Empty;

                RoadUtilis.ConnectSplines(m_Splines, copyCount, m_MergeDistance);
            }

            void crossesArray()
            {
                m_Crosses = new TrafficManager.CrossData[MathExtension.GetClosestWithMult(crosses.Count, 4)];
                for (int i = 0; i < crosses.Count; i++)
                {
                    crosses[i].GetInfo(out var green, out var yellow);
                    m_Crosses[i] = new TrafficManager.CrossData(green, yellow);
                }
                for (int i = crosses.Count; i < m_Crosses.Length; i++)
                {
                    m_Crosses[i] = TrafficManager.CrossData.Empty;
                }
            }
        }

        private void GetCrosses(out List<RoadCross> crosses)
        {
            crosses = GetComponentsInChildren<RoadCross>().ToList();
        }

        private void GenerateSplineList(List<RoadCross> crosses, out List<TrafficManager.SplineData> splineData)
        {
            var sections = GetComponentsInChildren<RoadSection>();
            splineData = new List<TrafficManager.SplineData>();
            foreach (var section in sections)
            {
                section.GetSection(out var cross, out var crossOrder, out var splines);

                int crossIndex = (cross != null && crosses.Contains(cross)) ? crosses.FindIndex(x => x == cross) * 10 + crossOrder : -1;
                RoadUtilis.ParseSplinesToCurves(splines, splineData, section.transform, crossIndex);
            }
            RoadUtilis.OptimizeCurveList(splineData);
        }
    }
}