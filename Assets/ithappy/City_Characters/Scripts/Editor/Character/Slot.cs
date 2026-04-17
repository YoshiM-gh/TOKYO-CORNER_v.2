using System;
using System.Collections.Generic;
using System.Linq;
using CharacterCustomizationTool.Editor.Entries;
using CharacterCustomizationTool.Editor.Enums;
using CharacterCustomizationTool.Editor.Extensions;
using CharacterCustomizationTool.Editor.State;
using UnityEngine;

namespace CharacterCustomizationTool.Editor.Character
{
    public class Slot
    {
        public SlotType Type { get; }

        private readonly GroupEntry[] _groups;

        public IEnumerable<GroupType> GroupTypes => _groups.Select(g => g.Type);
        public string Name => Type.ToString();
        public int VariantsCount => _groups.SelectMany(g => g.Variants).Count();

        public Slot(SlotType type, GroupEntry[] slotGroupEntries)
        {
            Type = type;
            _groups = slotGroupEntries;
        }

        public bool IsOfType(SlotType type)
        {
            return Type == type;
        }

        public bool HasVariant(GroupType groupType, int variantIndex)
        {
            return HasGroup(groupType) && variantIndex < _groups.First(g => g.Type.Equals(groupType)).Count;
        }

        public GameObject GetVariant(SlotState slotState)
        {
            return _groups.First(g => g.Type.Equals(slotState.GroupType)).Variants[slotState.VariantIndex];
        }

        public bool TryGetVariantByName(string key, out int index)
        {
            GameObject v = null;

            foreach (var group in _groups)
            {
                foreach (var variant in group.Variants)
                {
                    if (variant.name.Contains(key, StringComparison.InvariantCultureIgnoreCase))
                    {
                        v = variant;
                        break;
                    }
                }

                if (v)
                {
                    break;
                }
            }

            if (v)
            {
                index = _groups.First(g => g.Variants.Contains(v)).Variants.IndexOf(v);
                return true;
            }

            index = 0;
            return false;
        }

        public int VariantIndexBy(SlotState slotState)
        {
            var index = 0;

            foreach (var groupEntry in _groups)
            {
                if (groupEntry.Type.Equals(slotState.GroupType))
                {
                    index += slotState.VariantIndex;

                    return index;
                }

                index += groupEntry.Count;
            }

            return index;
        }

        public SlotState GetNext(SlotState slotState)
        {
            var groupIndex = Array.FindIndex(_groups, g => g.Type.Equals(slotState.GroupType));
            var group = _groups[groupIndex];

            if (slotState.VariantIndex + 1 >= group.Count)
            {
                var nextGroupIndex = _groups.NextIndex(groupIndex);
                var nextGroup = _groups[nextGroupIndex];

                return new SlotState(Type, nextGroup.Type, slotState.IsEnabled, 0);
            }

            return new SlotState(Type, slotState.GroupType, slotState.IsEnabled, slotState.VariantIndex + 1);
        }

        public SlotState GetPrevious(SlotState slotState)
        {
            var groupIndex = Array.FindIndex(_groups, g => g.Type.Equals(slotState.GroupType));

            if (slotState.VariantIndex - 1 < 0)
            {
                var previousGroupIndex = _groups.PreviousIndex(groupIndex);
                var previousGroup = _groups[previousGroupIndex];

                return new SlotState(Type, previousGroup.Type, slotState.IsEnabled, previousGroup.Count - 1);
            }

            return new SlotState(Type, slotState.GroupType, slotState.IsEnabled, slotState.VariantIndex - 1);
        }

        public (Mesh mesh, Material[] materials) GetMesh(SlotState slotState)
        {
            var variant = GetVariant(slotState);
            var skinnedMeshRenderer = variant.GetComponent<SkinnedMeshRenderer>();

            return (skinnedMeshRenderer.sharedMesh, skinnedMeshRenderer.sharedMaterials);
        }

        public bool TryGetVariantsCountInGroup(GroupType groupType, out int count)
        {
            var group = _groups.FirstOrDefault(g => g.Type.Equals(groupType));
            if (group)
            {
                count = group.Count;
                return true;
            }

            count = 0;
            return false;
        }

        public Mesh[] GetVariants()
        {
            return _groups
                .SelectMany(g => g.Variants)
                .Select(v => v.GetComponent<SkinnedMeshRenderer>().sharedMesh)
                .ToArray();
        }

        private bool HasGroup(GroupType groupType)
        {
            return _groups.Any(g => g.Type.Equals(groupType));
        }
    }
}