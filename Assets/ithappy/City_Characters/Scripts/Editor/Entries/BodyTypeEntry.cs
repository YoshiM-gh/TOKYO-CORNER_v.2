using System.Collections.Generic;
using CharacterCustomizationTool.Editor.Enums;
using UnityEngine;

namespace CharacterCustomizationTool.Editor.Entries
{
    [CreateAssetMenu(menuName = ToolConfig.ToolName + "/Body Type Entry", fileName = "BodyTypeEntry", order = 1)]
    public class BodyTypeEntry : ScriptableObject
    {
        public BodyType BodyType;
        public GameObject BaseMesh;
        public List<GenderEntry> Genders;
    }
}