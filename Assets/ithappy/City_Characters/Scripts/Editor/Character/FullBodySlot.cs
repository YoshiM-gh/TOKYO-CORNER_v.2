using System.Collections.Generic;
using System.Linq;
using CharacterCustomizationTool.Editor.Entries;
using CharacterCustomizationTool.Editor.Extensions;
using CharacterCustomizationTool.Editor.State;
using UnityEngine;

namespace CharacterCustomizationTool.Editor.Character
{
    public class FullBodySlot
    {
        public const string Name = "Full Body";

        private readonly FullBodyVariant[] _variants;

        public bool HasVariants => _variants != null && _variants.Any();
        public int VariantsCount => _variants.Length;

        public FullBodySlot(IEnumerable<FullBodyEntry> fullBodyEntries)
        {
            _variants = fullBodyEntries.Select(e => new FullBodyVariant(e)).ToArray();
        }

        public FullBodyState GetPrevious(FullBodyState fullBodyState)
        {
            var previousIndex = _variants.PreviousIndex(fullBodyState.VariantIndex);

            return new FullBodyState(fullBodyState.IsEnabled, previousIndex);
        }

        public FullBodyState GetNext(FullBodyState fullBodyState)
        {
            var previousIndex = _variants.NextIndex(fullBodyState.VariantIndex);

            return new FullBodyState(fullBodyState.IsEnabled, previousIndex);
        }

        public GameObject GetPreview(FullBodyState fullBodyState)
        {
            return _variants[fullBodyState.VariantIndex].PreviewObject;
        }

        public IEnumerable<FullBodyVariant.FullBodyElement> GetMeshes(FullBodyState fullBodyState)
        {
            return _variants[fullBodyState.VariantIndex].Elements;
        }
    }
}