using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;
using Pedestrians;

namespace Pedestrians.Editor
{
    public static class PedestrianTools
    {
        private const string PedestriansChildName = "Pedestrians";

        [MenuItem("Tools/Pedestrians/Add PedestrianSplinePath To Selected")]
        public static void AddPedestrianSplinePathToSelected()
        {
            int added = 0;
            foreach (GameObject go in Selection.gameObjects)
            {
                if (go == null) continue;
                if (go.GetComponent<SplineContainer>() == null) continue;
                if (go.GetComponent<PedestrianSplinePath>() != null) continue;
                Undo.AddComponent<PedestrianSplinePath>(go);
                added++;
            }
            if (added > 0)
                Debug.Log($"Added PedestrianSplinePath to {added} object(s).");
        }

        [MenuItem("Tools/Pedestrians/Bake Pedestrians For Selected Paths")]
        public static void BakePedestriansForSelectedPaths()
        {
            bool anyReplace = false;
            foreach (GameObject go in Selection.gameObjects)
            {
                if (go == null) continue;
                var path = go.GetComponent<PedestrianSplinePath>();
                if (path == null) continue;
                Transform existing = go.transform.Find(PedestriansChildName);
                if (existing != null)
                {
                    anyReplace = true;
                    break;
                }
            }

            if (anyReplace && !EditorUtility.DisplayDialog("Replace existing?", "Some selected paths already have a 'Pedestrians' child. Replace them?", "Yes", "Cancel"))
                return;

            foreach (GameObject go in Selection.gameObjects)
            {
                if (go == null) continue;
                var path = go.GetComponent<PedestrianSplinePath>();
                if (path == null) continue;
                BakeOnePath(go, path, anyReplace);
            }

            var scene = SceneManager.GetActiveScene();
            if (scene.IsValid())
                EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void BakeOnePath(GameObject pathGo, PedestrianSplinePath path, bool replaceExisting)
        {
            SerializedObject so = new SerializedObject(path);
            var prefabsProp = so.FindProperty("characterPrefabs");
            var speedMinProp = so.FindProperty("speedMin");
            var speedMaxProp = so.FindProperty("speedMax");
            var spacingMinProp = so.FindProperty("spacingMin");
            var spacingMaxProp = so.FindProperty("spacingMax");
            var randomSeedProp = so.FindProperty("randomSeed");

            int prefabCount = prefabsProp.arraySize;
            if (prefabCount == 0)
            {
                EditorUtility.DisplayDialog("No prefabs", "Character Prefabs list is empty. Add at least one prefab to the path.", "OK");
                return;
            }

            float spacingMin = spacingMinProp.floatValue;
            float spacingMax = spacingMaxProp.floatValue;
            if (spacingMin <= 0) { spacingMin = 1f; spacingMinProp.floatValue = 1f; so.ApplyModifiedPropertiesWithoutUndo(); Debug.LogWarning("spacingMin clamped to 1."); }
            if (spacingMax < spacingMin) { spacingMax = spacingMin; spacingMaxProp.floatValue = spacingMax; so.ApplyModifiedPropertiesWithoutUndo(); Debug.LogWarning("spacingMax clamped to spacingMin."); }

            var container = pathGo.GetComponent<SplineContainer>();
            if (container == null || container.Splines == null || container.Splines.Count == 0)
            {
                Debug.LogWarning($"No SplineContainer or empty splines on {pathGo.name}. Skip bake.");
                return;
            }

            float length = container.Splines[0].GetLength();
            if (length <= 0)
            {
                Debug.LogWarning($"Spline length is 0 on {pathGo.name}. Skip bake.");
                return;
            }

            Transform existing = pathGo.transform.Find(PedestriansChildName);
            if (existing != null)
            {
                if (replaceExisting)
                    Undo.DestroyObjectImmediate(existing.gameObject);
                else
                    return;
            }

            GameObject pedRoot = new GameObject(PedestriansChildName);
            pedRoot.transform.SetParent(pathGo.transform, false);
            Undo.RegisterCreatedObjectUndo(pedRoot, "Bake Pedestrians");

            int seed = randomSeedProp.intValue;
            if (seed > 0)
                Random.InitState(seed);
            else
                Random.InitState((int)(EditorApplication.timeSinceStartup * 1000));

            float speedMin = speedMinProp.floatValue;
            float speedMax = speedMaxProp.floatValue;

            float distance = Random.Range(0f, length);
            int spawned = 0;

            while (distance < length)
            {
                int prefabIndex = Random.Range(0, prefabCount);
                GameObject prefab = prefabsProp.GetArrayElementAtIndex(prefabIndex).objectReferenceValue as GameObject;
                if (prefab == null) continue;

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, pedRoot.transform);
                if (instance == null) continue;
                Undo.RegisterCreatedObjectUndo(instance, "Bake Pedestrians");

                var walker = instance.GetComponent<PedestrianSplineWalker>();
                if (walker == null)
                    walker = Undo.AddComponent<PedestrianSplineWalker>(instance);

                float speed = Random.Range(speedMin, speedMax);
                walker.Initialize(container, 0, speed, distance, length, path);

                distance += Random.Range(spacingMin, spacingMax);
                spawned++;
            }

            path.RefreshWalkerList();
            Debug.Log($"Baked {spawned} pedestrians on {pathGo.name}.");
        }

        [MenuItem("Tools/Pedestrians/Clear Baked Pedestrians For Selected Paths")]
        public static void ClearBakedPedestriansForSelectedPaths()
        {
            foreach (GameObject go in Selection.gameObjects)
            {
                if (go == null) continue;
                if (go.GetComponent<PedestrianSplinePath>() == null) continue;
                Transform pedRoot = go.transform.Find(PedestriansChildName);
                if (pedRoot != null)
                {
                    Undo.DestroyObjectImmediate(pedRoot.gameObject);
                }
            }
            var scene = SceneManager.GetActiveScene();
            if (scene.IsValid())
                EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
