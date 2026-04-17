using CharacterCustomizationTool.Editor.Enums;

namespace CharacterCustomizationTool.Editor.State
{
    public readonly struct SlotState
    {
        public readonly SlotType SlotType;
        public readonly GroupType GroupType;
        public readonly bool IsEnabled;
        public readonly int VariantIndex;

        public SlotState(SlotType slotType, GroupType groupType, bool isEnabled, int variantIndex)
        {
            SlotType = slotType;
            GroupType = groupType;
            IsEnabled = isEnabled;
            VariantIndex = variantIndex;
        }

        public SlotState Toggle(bool isToggled)
        {
            return new SlotState(SlotType, GroupType, isToggled, VariantIndex);
        }
    }
}