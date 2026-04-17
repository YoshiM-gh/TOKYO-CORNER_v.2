using System.Collections.Generic;
using CharacterCustomizationTool.Editor.Enums;
using CharacterCustomizationTool.Editor.State;

namespace CharacterCustomizationTool.Editor.Randomizer
{
    public class StepResult
    {
        public CharacterState State { get; }
        public IEnumerable<GroupType> AvailableGroups { get; }

        public StepResult(CharacterState state, IEnumerable<GroupType> availableGroups)
        {
            State = state;
            AvailableGroups = availableGroups;
        }
    }
}