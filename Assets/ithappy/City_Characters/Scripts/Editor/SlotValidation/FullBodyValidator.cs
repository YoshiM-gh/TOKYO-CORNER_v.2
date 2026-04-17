using System;
using System.Linq;
using CharacterCustomizationTool.Editor.Character;
using CharacterCustomizationTool.Editor.Enums;
using CharacterCustomizationTool.Editor.State;

namespace CharacterCustomizationTool.Editor.SlotValidation
{
    public static class FullBodyValidator
    {
        public static void Validate(CustomizableCharacter character, FullBodyState fullBodyState)
        {
            var allowedGroups = new[] { GroupType.HatHairstyle, GroupType.HatBeard, GroupType.HatMustache };

            if (!fullBodyState.IsEnabled) return;

            foreach (var slotType in Enum.GetValues(typeof(SlotType)).Cast<SlotType>())
            {
                switch (slotType)
                {
                    case SlotType.Hair:
                        TryToAllowCostume(character, SlotType.Hair, allowedGroups);
                        break;
                    case SlotType.FacialHair:
                        TryToAllowCostume(character, SlotType.FacialHair, allowedGroups);
                        break;
                    default:
                        character.SetEnabled(slotType, AlwaysOnRule.IsAlwaysOn(slotType));
                        break;
                }
            }
        }

        private static void TryToAllowCostume(CustomizableCharacter character, SlotType slotType, GroupType[] allowedGroups)
        {
            if (character.TryGetSlotState(slotType, out var slotState))
            {
                character.SetEnabled(slotType, slotState.IsEnabled && allowedGroups.Contains(slotState.GroupType));
            }
        }
    }
}