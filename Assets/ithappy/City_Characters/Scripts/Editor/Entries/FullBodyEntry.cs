using CharacterCustomizationTool.Editor.Enums;
using UnityEngine;

namespace CharacterCustomizationTool.Editor.Entries
{
    [CreateAssetMenu(menuName = ToolConfig.ToolName + "/Full Body Entry", fileName = "FullBodyEntry")]
    public class FullBodyEntry : ScriptableObject
    {
        public BodyType BodyType;
        public Gender Gender;
        public GameObject[] Slots;
    }
}