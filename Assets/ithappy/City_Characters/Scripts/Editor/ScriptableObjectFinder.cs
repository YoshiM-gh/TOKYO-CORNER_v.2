using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CharacterCustomizationTool.Editor
{
    public static class ScriptableObjectFinder
    {
        public static T FindInstanceOfType<T>() where T : ScriptableObject
        {
            var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");

            if (guids.Length == 0)
            {
                return null;
            }

            var path = AssetDatabase.GUIDToAssetPath(guids.First());

            return AssetDatabase.LoadAssetAtPath<T>(path);
        }
    }
}