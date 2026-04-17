using System;
using System.Linq;
using CharacterCustomizationTool.Editor.Character;
using CharacterCustomizationTool.Editor.Enums;
using CharacterCustomizationTool.Editor.Randomizer.Steps;
using CharacterCustomizationTool.Editor.Randomizer.Steps.Impl;
using CharacterCustomizationTool.Editor.State;

namespace CharacterCustomizationTool.Editor.Randomizer
{
    public static class RandomCharacterGenerator
    {
        private static readonly IRandomizerStep[] RandomizerSteps =
        {
            new BodyTypeAndGenderStep(),
            new BodyStep(),
            new FaceStep(),
            new CostumeStep(),
            new HairStep(),
            new HatStep(),
            new GlassesStep(),
            new GlovesStep(),
            new DressStep(),
            new OuterwearStep(),
            new OverallValidationStep(),
            new PlusSizeFemaleSwimsuitValidationStep(),
            new PantsStep(),
            new ShoesStep(),
            new AccessoriesStep(),
            new FacialHairStep()
        };

        public static void Randomize(CustomizableCharacter character)
        {
            var state = new CharacterState();
            var groups = Enum.GetValues(typeof(GroupType)).Cast<GroupType>().ToArray();

            foreach (var step in RandomizerSteps)
            {
                var result = step.Process(character, state, groups);
                state = result.State;
                groups = result.AvailableGroups.ToArray();
            }

            character.SetState(state);
        }
    }
}