using CharacterCustomizationTool.Editor.Enums;

namespace CharacterCustomizationTool.Editor.Randomizer.Steps.Impl
{
    public class GlassesStep : SlotStepBase
    {
        protected override SlotType SlotType => SlotType.Glasses;
        protected override float Probability => .2f;
    }
}