using System.Collections.Generic;
using System.Linq;
using CharacterCustomizationTool.Editor.Character;
using CharacterCustomizationTool.Editor.Enums;

namespace CharacterCustomizationTool.Editor.State
{
    public readonly struct CharacterState
    {
        public BodyType BodyType { get; }
        public Gender Gender { get; }
        public SlotState[] Slots { get; }
        public FullBodyState FullBodyState { get; }

        private CharacterState(BodyType bodyType, Gender gender, SlotState[] slots, FullBodyState fullBodyState)
        {
            BodyType = bodyType;
            Gender = gender;
            Slots = slots;
            FullBodyState = fullBodyState;
        }

        public static CharacterState CreateDefault(BodyType bodyType, Gender gender, IEnumerable<Slot> slots)
        {
            var slotStates = slots
                .Select(slot => new SlotState(slot.Type, slot.GroupTypes.First(), false, 0))
                .ToArray();

            return new CharacterState(bodyType, gender, slotStates, new FullBodyState(false, 0));
        }

        public bool TryGetSlotState(SlotType slotType, out SlotState slotState)
        {
            if (HasSlot(slotType))
            {
                slotState = Slots.First(s => slotType.Equals(s.SlotType));
                return true;
            }

            slotState = default;
            return false;
        }

        public CharacterState WithBodyType(BodyType bodyType)
        {
            return new CharacterState(bodyType, Gender, Slots, FullBodyState);
        }

        public CharacterState WithGender(Gender gender)
        {
            return new CharacterState(BodyType, gender, Slots, FullBodyState);
        }

        public CharacterState Update(SlotState newSlotState)
        {
            var slotStates = Slots.Where(s => s.SlotType != newSlotState.SlotType).Append(newSlotState).ToArray();

            return new CharacterState(BodyType, Gender, slotStates, FullBodyState);
        }

        public CharacterState UpdateFullBody(FullBodyState fullBodyState)
        {
            return new CharacterState(BodyType, Gender, Slots, fullBodyState);
        }

        private bool HasSlot(SlotType slotType)
        {
            return Slots.Any(s => s.SlotType.Equals(slotType));
        }

        public override string ToString()
        {
            var s = Slots.Aggregate(string.Empty, (current, slot) => current + $"Enabled: {slot.IsEnabled} | Index: {slot.VariantIndex} | Slot: {slot.SlotType} | Group: {slot.GroupType}\n");

            return $"BodyType: {BodyType} | Gender: {Gender} | Slots: {Slots.Count(s => s.IsEnabled)}\n" +
                   $"\n{s}" +
                   $"\nFull Body Enabled: {FullBodyState.IsEnabled} | Index: {FullBodyState.VariantIndex}\n";
        }
    }
}