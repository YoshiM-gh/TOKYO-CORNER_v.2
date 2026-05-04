using UnityEditor;
using UnityEditor.UI;
using UnityEngine;
using TranslucentUIFX;

namespace TranslucentUIFX.Editor
{
    [CustomEditor(typeof(TranslucentImageFX))]
    [CanEditMultipleObjects]
    public class TranslucentImageFXEditor : ImageEditor
    {
        [MenuItem("GameObject/UI/Translucent Image", false, 0)]
        static void CreateTranslucentImage(MenuCommand menuCommand)
        {
            GameObject parent = menuCommand.context as GameObject;
            
            // Auto-Canvas generation/discovery mimicking native Unity UI
            if (parent == null || parent.GetComponentInParent<Canvas>() == null)
            {
                Canvas canvas = Object.FindAnyObjectByType<Canvas>();
                if (canvas != null)
                {
                    parent = canvas.gameObject;
                }
                else
                {
                    EditorApplication.ExecuteMenuItem("GameObject/UI/Canvas");
                    parent = Selection.activeGameObject;
                }
            }

            GameObject go = new GameObject("Translucent Image");
            RectTransform rect = go.AddComponent<RectTransform>();
            go.AddComponent<CanvasRenderer>();
            TranslucentImageFX fx = go.AddComponent<TranslucentImageFX>();
            
            // Standardize size
            rect.sizeDelta = new Vector2(200, 200);
            
            GameObjectUtility.SetParentAndAlign(go, parent);
            Selection.activeGameObject = go;
            Undo.RegisterCreatedObjectUndo(go, "Create Standard Translucent Image");
        }
        SerializedProperty m_AdvancedMode;
        SerializedProperty m_CurrentPreset;
        SerializedProperty m_GlassIntensity;
        SerializedProperty m_LuminosityBoost;
        SerializedProperty m_QualityMode;
        SerializedProperty m_UpdateMode;
        SerializedProperty m_UpdateInterval;
        SerializedProperty m_Brightness;
        SerializedProperty m_Saturation;
        SerializedProperty m_Contrast;
        
        SerializedProperty m_BlurStrength;
        SerializedProperty m_TintColor;
        SerializedProperty m_FrostAmount;
        SerializedProperty m_NoiseAmount;
        SerializedProperty m_RefractionAmount;
        SerializedProperty m_SphericalDistortion;
        SerializedProperty m_ChromaticAberration;
        SerializedProperty m_SpecularGlare;
        
        SerializedProperty m_AutoReadability;
        SerializedProperty m_EnableEdgeLighting;
        SerializedProperty m_EdgeShape;
        SerializedProperty m_EdgeRounding;
        SerializedProperty m_EdgeLightColor;
        SerializedProperty m_EdgeLightWidth;
        SerializedProperty m_EdgeLightPower;
        
        bool m_ShowSupport = false;

        protected override void OnEnable()
        {
            base.OnEnable();
            m_AdvancedMode = serializedObject.FindProperty("AdvancedMode");
            m_CurrentPreset = serializedObject.FindProperty("CurrentPreset");
            m_GlassIntensity = serializedObject.FindProperty("GlassIntensity");
            m_LuminosityBoost = serializedObject.FindProperty("LuminosityBoost");
            m_QualityMode = serializedObject.FindProperty("QualityMode");
            m_UpdateMode = serializedObject.FindProperty("UpdateMode");
            m_UpdateInterval = serializedObject.FindProperty("UpdateInterval");
            m_Brightness = serializedObject.FindProperty("Brightness");
            m_Saturation = serializedObject.FindProperty("Saturation");
            m_Contrast = serializedObject.FindProperty("Contrast");
            
            m_BlurStrength = serializedObject.FindProperty("BlurStrength");
            m_TintColor = serializedObject.FindProperty("TintColor");
            m_FrostAmount = serializedObject.FindProperty("FrostAmount");
            m_NoiseAmount = serializedObject.FindProperty("NoiseAmount");
            m_RefractionAmount = serializedObject.FindProperty("RefractionAmount");
            m_SphericalDistortion = serializedObject.FindProperty("SphericalDistortion");
            m_ChromaticAberration = serializedObject.FindProperty("ChromaticAberration");
            m_SpecularGlare = serializedObject.FindProperty("SpecularGlare");
            m_AutoReadability = serializedObject.FindProperty("AutoReadability");
            m_EnableEdgeLighting = serializedObject.FindProperty("EnableEdgeLighting");
            m_EdgeShape = serializedObject.FindProperty("EdgeShape");
            m_EdgeRounding = serializedObject.FindProperty("EdgeRounding");
            m_EdgeLightColor = serializedObject.FindProperty("EdgeLightColor");
            m_EdgeLightWidth = serializedObject.FindProperty("EdgeLightWidth");
            m_EdgeLightPower = serializedObject.FindProperty("EdgeLightPower");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // 🧩 UI COMPONENT SETTINGS
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("🧩 UI Component Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            base.OnInspectorGUI();
            

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.Space(10);
            
            // 🥇 1. THEME PRESET (Highest visual level)
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.Space(2);
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(m_CurrentPreset, new GUIContent("🎨 Theme Preset (Quick Setup)"));
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                if (target is TranslucentImageFX fx) fx.ApplyPreset((GlassPreset)m_CurrentPreset.enumValueIndex);
                serializedObject.Update();
            }
            EditorGUILayout.Space(2);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);
            
            EditorGUI.BeginChangeCheck();
            
            // ⚡ 2. PERFORMANCE
            EditorGUILayout.LabelField("⚡ Performance", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Controls blur calculation frequency and framework cost.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(2);

            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(m_QualityMode);
            EditorGUILayout.PropertyField(m_UpdateMode, new GUIContent("Update Mode"));

            GUIStyle helperStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel);
            helperStyle.normal.textColor = Color.gray;

            switch ((TranslucentUpdateMode)m_UpdateMode.enumValueIndex)
            {
                case TranslucentUpdateMode.Always:
                    EditorGUILayout.LabelField("⭐ Recommended: Updates every frame for the exact 1-to-1 visual of what passes behind the UI.", helperStyle);
                    break;
                case TranslucentUpdateMode.SmartUpdate:
                    EditorGUILayout.LabelField("Updates only when the camera moves. Zero cost when idle. Great for 3D UI overlays.", helperStyle);
                    break;
                case TranslucentUpdateMode.Interval:
                    EditorGUILayout.LabelField("Updates every few frames to save performance. Great for mobile.", helperStyle);
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(m_UpdateInterval, new GUIContent("Interval Frames"));
                    EditorGUI.indentLevel--;
                    break;
                case TranslucentUpdateMode.Manual:
                    EditorGUILayout.LabelField("Updates only when triggered via script. Best for static menus.", helperStyle);
                    break;
            }
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(10);
            
            // 🎨 3. COLOR GRADING
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("🎨 Color Grading", EditorStyles.boldLabel);
            if (GUILayout.Button("Reset", EditorStyles.miniButtonRight, GUILayout.Width(50)))
            {
                m_Brightness.floatValue = 1.0f;
                m_Contrast.floatValue = 1.0f;
                m_Saturation.floatValue = 1.0f;
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("Adjust the visual tone and contrast of the blurred background.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(2);
            
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(m_AutoReadability, new GUIContent("Auto Readability", "Dynamically neutralizes extreme brights and darks in the background to guarantee UI text is always perfectly readable."));
            EditorGUILayout.PropertyField(m_Brightness, new GUIContent("Brightness"));
            EditorGUILayout.PropertyField(m_Contrast, new GUIContent("Contrast", "Pushes darks out and brights up. Incredible for fixing unreadable UI text over busy backgrounds."));
            EditorGUILayout.PropertyField(m_Saturation, new GUIContent("Saturation", "Intensity of colors passing through the glass. Distinct from Frost level."));
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(10);
            
            // 🧊 4. GLASS APPEARANCE
            EditorGUILayout.LabelField("🧊 Glass Appearance", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Controls the physical materials layered over the glass.", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(2);
            
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(m_GlassIntensity, new GUIContent("Glass Intensity ⭐", "Controls overall strength of the entire glass effect. Turn this down to fade the glass out."));
            EditorGUILayout.PropertyField(m_FrostAmount, new GUIContent("Frost Level", "Desaturates the background into soft, milky tones (perfect for acrylic glass)."));
            EditorGUILayout.PropertyField(m_LuminosityBoost, new GUIContent("Light Boost", "Enhances brightness of the blurred background for stronger glow."));
            EditorGUILayout.PropertyField(m_TintColor, new GUIContent("Glass Tint", "Base color multiplication for the glass."));
            EditorGUI.indentLevel--;
            
            EditorGUILayout.Space(15);
            
            // ▼ 5. ADVANCED SETTINGS
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.Space(2);
            
            m_AdvancedMode.boolValue = EditorGUILayout.Foldout(m_AdvancedMode.boolValue, "Advanced Settings", true, EditorStyles.foldoutHeader);
            
            if (m_AdvancedMode.boolValue)
            {
                EditorGUI.indentLevel++;
                
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("[ Blur Engine ]", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(m_BlurStrength, new GUIContent("Max Blur Multiplier", "Caps the maximum physical blur radius requested."));
                
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("[ Edge Lighting ]", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(m_EnableEdgeLighting, new GUIContent("Enable Edge Light"));
                if (m_EnableEdgeLighting.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(m_EdgeShape, new GUIContent("Edge Shape"));
                    if (m_EdgeShape.enumValueIndex == (int)EdgeShape.RoundedRect)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(m_EdgeRounding, new GUIContent("Corner Rounding"));
                        EditorGUI.indentLevel--;
                    }
                    EditorGUILayout.PropertyField(m_EdgeLightColor, new GUIContent("Edge Color"));
                    EditorGUILayout.PropertyField(m_EdgeLightWidth, new GUIContent("Edge Width"));
                    EditorGUILayout.PropertyField(m_EdgeLightPower, new GUIContent("Edge Softness"));
                    EditorGUI.indentLevel--;
                }
                
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("[ Optical Glass ]", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(m_SphericalDistortion, new GUIContent("Enable Spherical Convexity"));
                
                if (m_SphericalDistortion.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(m_RefractionAmount, new GUIContent("Refraction Bend"));
                    EditorGUILayout.PropertyField(m_SpecularGlare, new GUIContent("Specular Glare"));
                    EditorGUILayout.PropertyField(m_ChromaticAberration, new GUIContent("Chromatic Aberration"));
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("[ Surface ]", EditorStyles.miniBoldLabel);
                EditorGUILayout.PropertyField(m_NoiseAmount, new GUIContent("Film Grain"));
                
                EditorGUI.indentLevel--;
            }
            
            if (EditorGUI.EndChangeCheck() && m_CurrentPreset.enumValueIndex != (int)GlassPreset.Custom)
            {
                m_CurrentPreset.enumValueIndex = (int)GlassPreset.Custom;
            }

            // Enforce 2 decimal place precision for clean UI metrics
            m_Brightness.floatValue = (float)System.Math.Round(m_Brightness.floatValue, 2);
            m_Contrast.floatValue = (float)System.Math.Round(m_Contrast.floatValue, 2);
            m_Saturation.floatValue = (float)System.Math.Round(m_Saturation.floatValue, 2);
            m_GlassIntensity.floatValue = (float)System.Math.Round(m_GlassIntensity.floatValue, 2);
            m_LuminosityBoost.floatValue = (float)System.Math.Round(m_LuminosityBoost.floatValue, 2);
            m_FrostAmount.floatValue = (float)System.Math.Round(m_FrostAmount.floatValue, 2);
            m_BlurStrength.floatValue = (float)System.Math.Round(m_BlurStrength.floatValue, 2);
            m_EdgeLightWidth.floatValue = (float)System.Math.Round(m_EdgeLightWidth.floatValue, 2);
            m_EdgeLightPower.floatValue = (float)System.Math.Round(m_EdgeLightPower.floatValue, 2);

            EditorGUILayout.Space(10);
            
            // ▼ 6. SUPPORT
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.Space(2);
            
            m_ShowSupport = EditorGUILayout.Foldout(m_ShowSupport, "Support & Community", true, EditorStyles.foldoutHeader);
            if (m_ShowSupport)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Join our Discord for premium support, updates, and to share what you've built!", EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.Space(2);
                if (GUILayout.Button("🕹️ Join Discord Server"))
                {
                    Application.OpenURL("https://discord.gg/bXABNPthTb");
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(10);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
