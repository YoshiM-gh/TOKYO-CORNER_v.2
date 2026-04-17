using CharacterCustomizationTool.Editor.Character;
using CharacterCustomizationTool.Editor.Enums;
using CharacterCustomizationTool.Editor.State;
using Random = UnityEngine.Random;

namespace CharacterCustomizationTool.Editor.Randomizer.Steps.Impl
{
    public class CostumeStep : IRandomizerStep
    {
        private const float Probability = .2f;

        public StepResult Process(CustomizableCharacter character, CharacterState state, GroupType[] groups)
        {
            if (state.BodyType.Equals(BodyType.Pumped) || Random.value > Probability)
            {
                return new StepResult(state, groups);
            }

            var fullBodyVariantsCount = character.GetFullBodyVariantsCount(state);
            var fullBodyState = new FullBodyState(true, Random.Range(0, fullBodyVariantsCount));
            var resultState = state.UpdateFullBody(fullBodyState);

            return new StepResult(resultState, new[] { GroupType.HatHairstyle, GroupType.HatBeard, GroupType.HatMustache });
        }
    }
}