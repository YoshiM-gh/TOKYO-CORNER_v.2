using CharacterCustomizationTool.Editor.Character;
using CharacterCustomizationTool.Editor.Enums;
using CharacterCustomizationTool.Editor.Extensions;
using CharacterCustomizationTool.Editor.SlotValidation;
using UnityEditor;
using UnityEngine;

namespace CharacterCustomizationTool.Editor
{
    public class SlotsEditor
    {
        private const float CharacterButtonWidth = 80;
        private const float SlotPreviewSize = 160;
        private const float SlotButtonWidth = 40;

        private Vector2 _scrollPosition;

        public static void DrawBodyTypeAndGenderSelection(CustomizableCharacter character)
        {
            using (new GUILayout.VerticalScope())
            {
                GUILayout.Space(5);

                using (new GUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    RenderLeftButton(CharacterButtonWidth).Do(character.SelectPreviousBodyType);
                    RenderName(character.BodyType.ToString());
                    RenderRightButton(CharacterButtonWidth).Do(character.SelectNextBodyType);
                }

                GUILayout.Space(5);

                using (new EditorGUI.DisabledScope(character.BodyType == BodyType.Pumped))
                {
                    using (new GUILayout.HorizontalScope(EditorStyles.helpBox))
                    {
                        RenderLeftButton(CharacterButtonWidth).Do(character.SelectPreviousGender);
                        RenderName(character.Gender.ToString());
                        RenderRightButton(CharacterButtonWidth).Do(character.SelectNextGender);
                    }
                }

                GUILayout.Space(5);
            }
        }

        public void DrawSlots(CustomizableCharacter character)
        {
            var slots = character.Slots;

            using (var scrollViewScope = new GUILayout.ScrollViewScope(_scrollPosition))
            {
                _scrollPosition = scrollViewScope.scrollPosition;

                using (new GUILayout.VerticalScope())
                {
                    const int step = 4;
                    var index = 0;
                    while (index < slots.Length)
                    {
                        using (new GUILayout.HorizontalScope(GUILayout.Width(100)))
                        {
                            for (var i = index; i < index + step; i++)
                            {
                                if (i < slots.Length)
                                {
                                    RenderSlot(character, slots[i].Type);
                                }
                                else if (i == slots.Length)
                                {
                                    RenderFullBody(character);
                                }
                            }
                        }

                        index += step;
                    }
                }
            }
        }

        private static void RenderSlot(CustomizableCharacter character, SlotType slotType)
        {
            using (new GUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(SlotPreviewSize)))
            {
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(character.GetName(slotType));
                    GUILayout.FlexibleSpace();
                }

                DrawPreview(character.GetPreview(slotType));

                using (new EditorGUI.DisabledScope(AlwaysOnRule.IsAlwaysOn(slotType)))
                {
                    RenderToggle(character.IsEnabled(slotType)).Then(v => character.SetEnabled(slotType, v));
                }

                using (new GUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    RenderLeftButton(SlotButtonWidth).Do(() => character.SelectPrevious(slotType));
                    RenderCounter(character.GetSelectedVariantIndex(slotType), character.GetVariantsCount(slotType));
                    RenderRightButton(SlotButtonWidth).Do(() => character.SelectNext(slotType));
                }
            }
        }

        private static void RenderFullBody(CustomizableCharacter character)
        {
            if (!character.IsFullBodyAvailable())
            {
                return;
            }

            using (new GUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(SlotPreviewSize)))
            {
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(character.GetFullBodyName());
                    GUILayout.FlexibleSpace();
                }

                var previewObject = character.GetFullBodyPreview();
                previewObject.SetActive(true);
                DrawPreview(previewObject);
                previewObject.SetActive(false);
                RenderToggle(character.IsFullBodyEnabled()).Then(character.SetFullBodyEnabled);

                using (new GUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    RenderLeftButton(SlotButtonWidth).Do(character.SelectPreviousFullBody);
                    RenderCounter(character.GetSelectedFullBodyIndex(), character.GetFullBodyVariantsCount());
                    RenderRightButton(SlotButtonWidth).Do(character.SelectNextFullBody);
                }
            }
        }

        private static void DrawPreview(GameObject gameObject)
        {
            var style = new GUIStyle();
            var texture = AssetPreview.GetAssetPreview(gameObject);

            if (texture)
            {
                style.normal.background = AssetPreview.GetAssetPreview(texture);
            }

            GUILayout.Box(GUIContent.none, style, GUILayout.Width(SlotPreviewSize), GUILayout.Height(SlotPreviewSize));
        }

        private static void RenderName(string name)
        {
            var style = new GUIStyle
            {
                alignment = TextAnchor.MiddleCenter,
                normal =
                {
                    textColor = Color.white
                }
            };

            GUILayout.Label(name, style);
        }

        private static bool RenderToggle(bool isEnabled)
        {
            return GUILayout.Toggle(isEnabled, "Enabled");
        }

        private static bool RenderLeftButton(float width) => RenderButton("<", width);

        private static bool RenderRightButton(float width) => RenderButton(">", width);

        private static bool RenderButton(string text, float width) => GUILayout.Button(text, GUILayout.Width(width));

        private static void RenderCounter(int selectedIndex, int count)
        {
            var style = new GUIStyle
            {
                alignment = TextAnchor.MiddleCenter,

                normal =
                {
                    textColor = Color.white
                },
            };

            GUILayout.Label($"{selectedIndex + 1}/{count}", style);
        }
    }
}