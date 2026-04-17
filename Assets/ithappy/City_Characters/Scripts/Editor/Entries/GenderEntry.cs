using System.Collections.Generic;
using CharacterCustomizationTool.Editor.Enums;
using UnityEngine;

namespace CharacterCustomizationTool.Editor.Entries
{
    [CreateAssetMenu(menuName = ToolConfig.ToolName + "/Gender Entry", fileName = "GenderEntry", order = 2)]
    public class GenderEntry : ScriptableObject
    {
        public BodyType BodyType;
        public Gender Gender;
        public List<SlotEntry> Slots;
        public List<FullBodyEntry> FullBodyEntries;
    }
}