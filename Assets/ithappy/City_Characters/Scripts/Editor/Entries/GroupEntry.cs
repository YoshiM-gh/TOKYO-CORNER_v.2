using System.Collections.Generic;
using CharacterCustomizationTool.Editor.Enums;
using UnityEngine;

namespace CharacterCustomizationTool.Editor.Entries
{
    [CreateAssetMenu(menuName = ToolConfig.ToolName + "/Slot Group Entry", fileName = "SlotGroupEntry", order = 1000)]
    public class GroupEntry : ScriptableObject
    {
        public GroupType Type;
        public BodyType BodyType;
        public Gender Gender;
        public List<GameObject> Variants;

        public int Count => Variants.Count;
    }
}