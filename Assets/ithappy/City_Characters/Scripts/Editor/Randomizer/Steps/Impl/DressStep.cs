using System.Collections.Generic;
using System.Linq;
using CharacterCustomizationTool.Editor.Character;
using CharacterCustomizationTool.Editor.Enums;
using CharacterCustomizationTool.Editor.State;
using UnityEngine;

namespace CharacterCustomizationTool.Editor.Randomizer.Steps.Impl
{
    public class DressStep : StepBase
    {
        private const float Probability = .2f;

        private readonly IEnumerable<GroupType> _compatibleGroups = new[] { GroupType.Shoes, GroupType.Bracelet, GroupType.HeelShoes, GroupType.Necklace, GroupType.Chain, GroupType.Watch, };

        protected override GroupType GroupType => GroupType.Dress;

        public override StepResult Process(CustomizableCharacter character, CharacterState state, GroupType[] groups)
        {
            var slot = character.CreateSlotsFor(state.BodyType, state.Gender).FirstOrDefault(s => s.Type.Equals(SlotType.Outerwear));
            if (slot == null
                || !slot.GroupTypes.Any(g => g.Equals(GroupType))
                || !groups.Contains(GroupType.Dress)
                || state.Gender == Gender.Male
                || Random.value > Probability)
            {
                return new StepResult(state, RemoveSelf(groups));
            }

            var variantsCount = character.GetVariantsCountInGroup(state, GroupType);
            var newSlotState = new SlotState(SlotType.Outerwear, GroupType, true, Random.Range(0, variantsCount));

            return new StepResult(state.Update(newSlotState), groups.Where(g => _compatibleGroups.Contains(g)));
        }
    }
}