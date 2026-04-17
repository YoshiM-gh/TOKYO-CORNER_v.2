using CharacterCustomizationTool.Editor.Enums;

namespace CharacterCustomizationTool.Editor.Randomizer.Steps.Impl
{
    public class GlovesStep : SlotStepBase
    {
        protected override SlotType SlotType => SlotType.Gloves;
        protected override float Probability => .1f;
    }
}