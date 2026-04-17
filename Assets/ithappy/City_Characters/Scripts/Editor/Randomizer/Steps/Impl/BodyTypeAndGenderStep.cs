using System;
using System.Linq;
using CharacterCustomizationTool.Editor.Character;
using CharacterCustomizationTool.Editor.Enums;
using CharacterCustomizationTool.Editor.Extensions;
using CharacterCustomizationTool.Editor.State;

namespace CharacterCustomizationTool.Editor.Randomizer.Steps.Impl
{
    public class BodyTypeAndGenderStep : IRandomizerStep
    {
        public StepResult Process(CustomizableCharacter character, CharacterState state, GroupType[] groups)
        {
            var randomBodyType = character.GetAvailableBodyTypes()
                .Where(b => GeneratorSettings.BodyTypes.Contains(b))
                .Where(b => !(!GeneratorSettings.Genders.Contains(Gender.Male) ? new[] { BodyType.Pumped } : Array.Empty<BodyType>()).Contains(b))
                .Random();

            var randomGender = character.GetAvailableGenders(randomBodyType)
                .Where(g => GeneratorSettings.Genders.Contains(g))
                .Random();

            var characterSate = CharacterState.CreateDefault(randomBodyType, randomGender, character.CreateSlotsFor(randomBodyType, randomGender));

            return new StepResult(characterSate, groups);
        }
    }
}