using UnityEditor;

namespace ithappy.Creative_Characters.CharacterCustomizationTool.Editor.Character
{
    public static class SlotLibraryLoader
    {
        public static SlotLibrary LoadSlotLibrary()
        {
            return AssetDatabase.LoadAssetAtPath<SlotLibrary>(AssetsPath.SlotLibrary);
        }
    }
}