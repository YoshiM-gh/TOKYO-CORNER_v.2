using System.Linq;
using CharacterCustomizationTool.Editor.Character;
using CharacterCustomizationTool.Editor.Enums;
using CharacterCustomizationTool.Editor.Extensions;
using CharacterCustomizationTool.Editor.State;
using UnityEngine;

namespace CharacterCustomizationTool.Editor.Randomizer.Steps
{
    public abstract class SlotStepBase : IRandomizerStep
    {
        protected abstract SlotType SlotType { get; }

        protected virtual float Probability => 1f;

        public StepResult Process(CustomizableCharacter character, CharacterState state, GroupType[] groups)
        {
            if (Random.value > Probability)
            {
                return EmptyResult();
            }

            var slot = character.CreateSlotsFor(state.BodyType, state.Gender).FirstOrDefault(s => s.Type.Equals(SlotType));
            if (slot == null)
            {
                return EmptyResult();
            }

            var slotGroups = slot.GroupTypes.ToArray();
            if (!slotGroups.Any(groups.Contains))
            {
                return EmptyResult();
            }

            var availableGroups = slotGroups.Where(groups.Contains).ToArray();
            if (availableGroups.Length == 0)
            {
                return EmptyResult();
            }

            var randomGroup = availableGroups.Random();
            if (!slot.TryGetVariantsCountInGroup(randomGroup, out var count))
            {
                return EmptyResult();
            }

            var newSlotState = new SlotState(SlotType, randomGroup, true, Random.Range(0, count));

            return new StepResult(state.Update(newSlotState), groups.Where(g => !slotGroups.Contains(g)).ToArray());

            StepResult EmptyResult()
            {
                return new StepResult(state, groups);
            }
        }
    }
}