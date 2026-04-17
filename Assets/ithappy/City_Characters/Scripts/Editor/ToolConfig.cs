using System.Collections.Generic;
using CharacterCustomizationTool.Editor.Character;
using CharacterCustomizationTool.Editor.Entries;
using UnityEditor;
using UnityEngine;

namespace CharacterCustomizationTool.Editor
{
    [CreateAssetMenu(fileName = "ToolConfig", menuName = ToolName + "/Tool Config", order = 0)]
    public class ToolConfig : ScriptableObject
    {
        public const string ToolName = "Character Customization Tool";
        public const string ConfigsFolderName = "Configs";

        [SerializeField]
        private string _packageName;
        [SerializeField]
        private List<BodyTypeEntry> _bodyTypes;

        private static ToolConfig _instance;
        private static string _rootPath;

        public static string PackageName => Instance._packageName;
        public static string RootPath => string.IsNullOrEmpty(_rootPath) ? _rootPath = BaseMeshAccessor.FindRoot() : _rootPath;
        public static string AnimationController => RootPath + "Animations/Animation_Controllers/Character_Movement.controller";
        public static string SavedCharacters => RootPath + "Saved_Characters/";
        public static string Meshes => RootPath + "Meshes";
        public static string Faces => Meshes + "/Faces/";
        public static string Configs => RootPath + ConfigsFolderName;
        public static string BodyTypes => $"{Configs}/BodyTypes";
        public static List<BodyTypeEntry> BodyTypeEntries => Instance._bodyTypes;

        private static ToolConfig Instance => !_instance ? _instance = ScriptableObjectFinder.FindInstanceOfType<ToolConfig>() : _instance;

        public static void Reload()
        {
            _instance = null;
            _rootPath = null;
        }

        public static void SetBodyTypes(IEnumerable<BodyTypeEntry> bodyTypes)
        {
            Instance._bodyTypes.Clear();
            Instance._bodyTypes.AddRange(bodyTypes);
            EditorUtility.SetDirty(Instance);
        }

        private void OnValidate()
        {
            Reload();
        }
    }
}