using System;
using Unity.Collections;
using UnityEngine;

namespace ITHappy
{
    public class TrafficPath : MonoBehaviour
    {
        [SerializeField]
        private SplineConnector m_Road;

        public void GetArrayReferences(out NativeArray<TrafficManager.SplineData>[] splineData, 
            out NativeArray<TrafficManager.CrossData> crossData, out NativeArray<TrafficManager.CrossState> crossState)
        {
            splineData = new NativeArray<TrafficManager.SplineData>[2];

            const int chunkSize = 4096;
            for (int i = 0; i < 2; i++)
            {
                var arr = new TrafficManager.SplineData[chunkSize];
                int srcOffset = chunkSize * i;
                int copyCount = Math.Min(chunkSize, Math.Max(0, m_Road.Splines.Length - srcOffset));
                if (copyCount > 0)
                    Array.Copy(m_Road.Splines, srcOffset, arr, 0, copyCount);
                for (int j = copyCount; j < chunkSize; j++)
                    arr[j] = TrafficManager.SplineData.Empty;

                splineData[i] = new(arr, Allocator.Persistent);
                if (splineData == null || splineData.Length == 0)
                {
                    Debug.LogError("null road is being used");
                    throw new Exception();
                }
            }

            crossData = new(m_Road.Crosses, Allocator.Persistent);
            crossState = new(crossData.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            for(int i = 0; i < crossData.Length; i++)
            {
                crossState[i] = new(crossData[i].timing.y, 0);
            }
        }
    }
}