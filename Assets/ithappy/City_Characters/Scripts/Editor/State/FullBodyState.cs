namespace CharacterCustomizationTool.Editor.State
{
    public readonly struct FullBodyState
    {
        public readonly bool IsEnabled;
        public readonly int VariantIndex;

        public FullBodyState(bool isEnabled, int variantIndex)
        {
            IsEnabled = isEnabled;
            VariantIndex = variantIndex;
        }

        public FullBodyState Toggle(bool isToggled)
        {
            return new FullBodyState(isToggled, VariantIndex);
        }
    }
}