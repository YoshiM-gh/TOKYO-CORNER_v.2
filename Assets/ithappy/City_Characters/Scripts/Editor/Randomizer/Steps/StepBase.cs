using System.Linq;
using CharacterCustomizationTool.Editor.Character;
using CharacterCustomizationTool.Editor.Enums;
using CharacterCustomizationTool.Editor.State;

namespace CharacterCustomizationTool.Editor.Randomizer.Steps
{
    public abstract class StepBase : IRandomizerStep
    {
        protected abstract GroupType GroupType { get; }

        public abstract StepResult Process(CustomizableCharacter character, CharacterState state, GroupType[] groups);

        protected GroupType[] RemoveSelf(GroupType[] groups) => groups.Where(g => g != GroupType).ToArray();
    }
}