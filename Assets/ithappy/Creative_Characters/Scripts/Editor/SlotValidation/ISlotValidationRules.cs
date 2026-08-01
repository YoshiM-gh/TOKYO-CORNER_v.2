using ithappy.Creative_Characters.CharacterCustomizationTool.Editor.Character;

namespace ithappy.Creative_Characters.CharacterCustomizationTool.Editor.SlotValidation
{
    public interface ISlotValidationRules
    {
        void Validate(CustomizableCharacter character, SlotType type, bool isToggled);
    }
}