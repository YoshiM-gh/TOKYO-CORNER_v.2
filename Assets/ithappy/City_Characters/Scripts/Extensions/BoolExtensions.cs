using System;

namespace CharacterCustomizationTool.Editor.Extensions
{
    public static class BoolExtensions
    {
        public static bool Do(this bool b, Action action)
        {
            if (b)
            {
                action();
            }

            return b;
        }
    }
}