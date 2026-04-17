using UnityEngine;

namespace CharacterCustomizationTool.Editor.Character
{
    public static class PreviewCreator
    {
        public static GameObject CreateVariantPreview(FullBodyVariant.FullBodyElement[] elements)
        {
            var parent = new GameObject("PreviewParent");
            parent.hideFlags = HideFlags.HideAndDontSave;

            foreach (var element in elements)
            {
                var child = new GameObject("PreviewElement");
                child.transform.SetParent(parent.transform);

                child.AddComponent<MeshFilter>().sharedMesh = element.Mesh;
                child.transform.localPosition = Vector3.zero;
                child.hideFlags = HideFlags.HideAndDontSave;

                var renderer = child.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = element.Materials;
            }

            parent.SetActive(false);

            return parent;
        }
    }
}