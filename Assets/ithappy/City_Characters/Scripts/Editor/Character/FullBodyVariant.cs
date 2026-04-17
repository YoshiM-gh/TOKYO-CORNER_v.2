using System.Linq;
using CharacterCustomizationTool.Editor.Entries;
using UnityEngine;

namespace CharacterCustomizationTool.Editor.Character
{
    public class FullBodyVariant
    {
        public FullBodyElement[] Elements { get; }
        public GameObject PreviewObject { get; }

        public FullBodyVariant(FullBodyEntry fullBodyEntry)
        {
            Elements = fullBodyEntry.Slots.Select(s =>
            {
                var r = s.GetComponentInChildren<SkinnedMeshRenderer>();
                return new FullBodyElement(r.sharedMesh, r.sharedMaterials);
            }).ToArray();

            PreviewObject = PreviewCreator.CreateVariantPreview(Elements);
        }

        public class FullBodyElement
        {
            public Mesh Mesh { get; }
            public Material[] Materials { get; }

            public FullBodyElement(Mesh mesh, Material[] materials)
            {
                Mesh = mesh;
                Materials = materials;
            }
        }
    }
}