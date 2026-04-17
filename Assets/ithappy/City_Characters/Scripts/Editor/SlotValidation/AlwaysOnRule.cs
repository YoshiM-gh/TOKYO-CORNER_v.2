using System;
using System.Linq;
using CharacterCustomizationTool.Editor.Enums;
using CharacterCustomizationTool.Editor.State;

namespace CharacterCustomizationTool.Editor.SlotValidation
{
    public class AlwaysOnRule : ISlotValidationRules
    {
        private static readonly SlotType[] AlwaysOnSlotTypes = { SlotType.SkinColor, SlotType.Face };

        public static bool IsAlwaysOn(SlotType slotType) => AlwaysOnSlotTypes.Contains(slotType);

        public SlotState[] Validate(SlotState slotState)
        {
            return AlwaysOnSlotTypes.Contains(slotState.SlotType) && !slotState.IsEnabled
                ? new[] { slotState.Toggle(true) }
                : Array.Empty<SlotState>();
        }
    }
}