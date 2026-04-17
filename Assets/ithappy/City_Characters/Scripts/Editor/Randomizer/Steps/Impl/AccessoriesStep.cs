using CharacterCustomizationTool.Editor.Enums;

namespace CharacterCustomizationTool.Editor.Randomizer.Steps.Impl
{
    public class AccessoriesStep : SlotStepBase
    {
        protected override SlotType SlotType => SlotType.Accessories;
        protected override float Probability => .2f;
    }
}