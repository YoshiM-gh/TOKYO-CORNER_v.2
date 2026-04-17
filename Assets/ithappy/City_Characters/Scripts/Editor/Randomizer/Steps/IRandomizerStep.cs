using CharacterCustomizationTool.Editor.Character;
using CharacterCustomizationTool.Editor.Enums;
using CharacterCustomizationTool.Editor.State;

namespace CharacterCustomizationTool.Editor.Randomizer.Steps
{
    public interface IRandomizerStep
    {
        StepResult Process(CustomizableCharacter character, CharacterState state, GroupType[] groups);
    }
}