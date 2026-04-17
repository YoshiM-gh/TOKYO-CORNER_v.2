using System;
using System.Linq;
using CharacterCustomizationTool.Editor.Character;
using CharacterCustomizationTool.Editor.Enums;
using CharacterCustomizationTool.Editor.State;

namespace CharacterCustomizationTool.Editor.Randomizer.Steps.Impl
{
    public class PlusSizeFemaleSwimsuitValidationStep : IRandomizerStep
    {
        public StepResult Process(CustomizableCharacter character, CharacterState state, GroupType[] groups)
        {
            if (state.BodyType != BodyType.PlusSize
                || state.Gender != Gender.Female
                || !TryGetSlot(state, SlotType.Outerwear, GroupType.Swimsuit, out var slot)
                || !slot.IsEnabled)
            {
                return new StepResult(state, groups);
            }

            var pantsSlot = character.CreateSlotsFor(state.BodyType, state.Gender).FirstOrDefault(s => s.Type.Equals(SlotType.Pants));
            var pantsGroups = pantsSlot != null ? pantsSlot.GroupTypes.ToArray() : Array.Empty<GroupType>();

            return new StepResult(state, groups.Except(pantsGroups).ToArray());
        }

        private static bool TryGetSlot(CharacterState state, SlotType slotType, GroupType groupType, out SlotState slot)
        {
            Func<SlotState, bool> overallPredicate = s => s.SlotType.Equals(slotType) && s.GroupType.Equals(groupType);

            var hasOverallSlot = state.Slots.Any(overallPredicate);
            if (hasOverallSlot)
            {
                slot = state.Slots.First(overallPredicate);
                return true;
            }

            slot = default;
            return false;
        }
    }
}