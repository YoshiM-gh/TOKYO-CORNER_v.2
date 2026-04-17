using System.Collections.Generic;
using System.Linq;
using CharacterCustomizationTool.Editor.Character;
using CharacterCustomizationTool.Editor.Enums;
using CharacterCustomizationTool.Editor.State;
using UnityEngine;

namespace CharacterCustomizationTool.Editor.Randomizer.Steps.Impl
{
    public class BodyStep : StepBase
    {
        private readonly Dictionary<int, float> _indexWeights = new()
        {
            { 0, 20f },
            { 1, 40f },
            { 2, 20f },
            { 3, 20f }
        };

        protected override GroupType GroupType => GroupType.Body;

        public override StepResult Process(CustomizableCharacter character, CharacterState state, GroupType[] groups)
        {
            var skinColorIndex = GetRandomIndex();
            var slotState = new SlotState(SlotType.SkinColor, GroupType.Body, true, skinColorIndex);

            return new StepResult(state.Update(slotState), RemoveSelf(groups));
        }

        private int GetRandomIndex()
        {
            var totalWeight = _indexWeights.Values.Sum();
            var randomRoll = Random.Range(0, totalWeight);

            foreach (var entry in _indexWeights)
            {
                if (randomRoll < entry.Value)
                {
                    return entry.Key;
                }

                randomRoll -= entry.Value;
            }

            return 1;
        }
    }
}