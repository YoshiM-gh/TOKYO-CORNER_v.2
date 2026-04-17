using System;

namespace CharacterCustomizationTool.Editor.Extensions
{
    public static class ArrayExtensions
    {
        public static int NextIndex<T>(this T[] input, int currentIndex)
        {
            if (input == null || input.Length == 0)
            {
                throw new Exception("Invalid array.");
            }

            var nextIndex = (currentIndex + 1) % input.Length;

            return nextIndex;
        }

        public static int PreviousIndex<T>(this T[] input, int currentIndex)
        {
            if (input == null || input.Length == 0)
            {
                throw new Exception("Invalid array.");
            }

            var previousIndex = currentIndex - 1;
            if (previousIndex < 0)
            {
                return input.Length - 1;
            }

            return previousIndex;
        }
    }
}