using CharacterCustomizationTool.Editor.State;

namespace CharacterCustomizationTool.Editor.SlotValidation
{
    public interface ISlotValidationRules
    {
        SlotState[] Validate(SlotState slotState);
    }
}