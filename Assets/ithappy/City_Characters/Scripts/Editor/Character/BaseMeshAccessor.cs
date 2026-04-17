using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CharacterCustomizationTool.Editor.Character
{
    public static class BaseMeshAccessor
    {
        private static readonly string[] Keywords =
        {
            "Base",
            "Basic"
        };

        private static string[] Paths => new[]
        {
            ToolConfig.RootPath,
            ToolConfig.RootPath + "Meshes/",
        };

        public static string FindRoot()
        {
            var anchorAssetPath = FindBaseMeshPath();
            var pathParts = anchorAssetPath.Split('/');
            var packTitleParts = ToolConfig.PackageName.Split('_');
            var rootFound = false;
            for (var i = pathParts.Length - 1; i >= 0; i--)
            {
                if (rootFound)
                {
                    break;
                }

                foreach (var part in packTitleParts)
                {
                    rootFound = false;

                    if (!pathParts[i].Contains(part))
                    {
                        pathParts[i] = string.Empty;
                        break;
                    }

                    rootFound = true;
                }
            }

            var root = string.Join("/", pathParts.Where(p => !string.IsNullOrEmpty(p)).ToArray()) + "/";

            return root;
        }

        public static GameObject Load()
        {
            var availableBaseMeshes = new List<GameObject>();

            foreach (var path in Paths)
            {
                var meshesInFolder = FindInFolder(path);
                availableBaseMeshes.AddRange(meshesInFolder);
            }

            var baseMesh = availableBaseMeshes.First(m => m);

            return baseMesh;
        }

        private static string FindBaseMeshPath()
        {
            foreach (var keyword in Keywords)
            {
                foreach (var guid in AssetDatabase.FindAssets(keyword))
                {
                    var baseMeshPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (baseMeshPath.Contains(".fbx") && baseMeshPath.Contains(ToolConfig.PackageName))
                    {
                        return baseMeshPath;
                    }
                }
            }

            return string.Empty;
        }

        private static IEnumerable<GameObject> FindInFolder(string path)
        {
            var meshes = new List<GameObject>();

            foreach (var keyword in Keywords)
            {
                var foundMeshes = AssetLoader.LoadAssets<GameObject>(keyword, path);
                meshes.AddRange(foundMeshes);
            }

            return meshes;
        }
    }
}