using CharacterCustomizationTool.Editor.Character;
using CharacterCustomizationTool.Editor.Enums;
using CharacterCustomizationTool.Editor.State;
using UnityEngine;

namespace CharacterCustomizationTool.Editor.Randomizer.Steps.Impl
{
    public class FaceStep : StepBase
    {
        protected override GroupType GroupType => GroupType.Face;

        public override StepResult Process(CustomizableCharacter character, CharacterState state, GroupType[] groups)
        {
            if (!GeneratorSettings.StandardFace || !character.TryGetVariantByName(SlotType.Face, "usual", out var faceIndex))
            {
                var facesCount = character.GetVariantsCount(SlotType.Face);
                faceIndex = Random.Range(0, facesCount);
            }

            var faceSlotState = new SlotState(SlotType.Face, GroupType.Face, true, faceIndex);

            return new StepResult(state.Update(faceSlotState), RemoveSelf(groups));
        }
    }
}