using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CharacterCustomizationTool.Editor.Extensions
{
    public static class EnumerableExtensions
    {
        public static T Random<T>(this IEnumerable<T> source)
        {
            if (source == null)
            {
                Debug.LogError("Source cannot be null.");
                return default;
            }

            var list = source as IList<T> ?? source.ToList();

            if (list.Count == 0)
            {
                Debug.LogError("Sequence contains no elements.");
                return default;
            }

            var index = UnityEngine.Random.Range(0, list.Count);

            return list[index];
        }
    }
}