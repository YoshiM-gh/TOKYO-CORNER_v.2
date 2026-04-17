using System.Collections.Generic;
using CharacterCustomizationTool.Editor.Enums;
using UnityEngine;

namespace CharacterCustomizationTool.Editor.Entries
{
    [CreateAssetMenu(menuName = ToolConfig.ToolName + "/Slot Entry", fileName = "SlotEntry")]
    public class SlotEntry : ScriptableObject
    {
        public SlotType Type;
        public BodyType BodyType;
        public Gender Gender;
        public List<GroupEntry> Groups;
    }
}