using System.Collections.Generic;
using System.Linq;
using CharacterCustomizationTool.Editor.Character;
using CharacterCustomizationTool.Editor.Enums;
using CharacterCustomizationTool.Editor.State;
using UnityEngine;

namespace CharacterCustomizationTool.Editor.Randomizer.Steps.Impl
{
    public class HatStep : StepBase
    {
        private const float Probability = .33f;

        private readonly IEnumerable<GroupType> _incompatibleGroups = new[] { GroupType.Mustache, GroupType.Beard, GroupType.Hairstyle, };

        protected override GroupType GroupType => GroupType.Hat;

        public override StepResult Process(CustomizableCharacter character, CharacterState state, GroupType[] groups)
        {
            if (CannotProcess(state, ref groups, out var result))
            {
                return result;
            }

            var newGroups = groups.Where(g => !_incompatibleGroups.Contains(g)).ToArray();

            return new StepResult(CreateCharacterState(character, state), newGroups);
        }

        private bool CannotProcess(CharacterState state, ref GroupType[] groups, out StepResult result)
        {
            var cannotProcess = !groups.Contains(GroupType);
            groups = RemoveSelf(groups);

            if (cannotProcess || Random.value > Probability)
            {
                result = new StepResult(state, groups);
                return true;
            }

            result = null;
            return false;
        }

        private CharacterState CreateCharacterState(CustomizableCharacter character, CharacterState state)
        {
            var variantsCount = character.GetVariantsCountInGroup(state.BodyType, state.Gender, GroupType);
            var slotState = new SlotState(SlotType.Hat, GroupType, true, Random.Range(0, variantsCount));

            return state.Update(slotState);
        }
    }
}