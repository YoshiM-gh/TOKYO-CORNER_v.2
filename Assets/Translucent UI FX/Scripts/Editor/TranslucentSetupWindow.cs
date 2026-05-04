using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Reflection;

namespace TranslucentUIFX.Editor
{
    [InitializeOnLoad]
    public class TranslucentSetupWindow : EditorWindow
    {
        private static readonly string s_PrefKey = "TranslucentUIFX_SetupShown";

        static TranslucentSetupWindow()
        {
            EditorApplication.delayCall += Initialize;
        }

        private static void Initialize()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (SessionState.GetBool(s_PrefKey, false)) return; // Only check once per editor session to avoid annoyance

            var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urpAsset == null) return;

            // Use reflection to grab the inner ScriptableRendererData as Unity hides it frequently 
            ScriptableRendererData rendererData = null;

            // "scriptableRendererData" is often internal or protected depending on the exact unity minor version. 
            // In Unity 2022/6, we can usually get it from m_RendererDataList.
            FieldInfo rendererDataListField = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.Instance | BindingFlags.NonPublic);
            if (rendererDataListField != null)
            {
                var rendererDataArray = rendererDataListField.GetValue(urpAsset) as ScriptableRendererData[];
                if (rendererDataArray != null && rendererDataArray.Length > 0)
                {
                    // get the default one
                    FieldInfo indexField = typeof(UniversalRenderPipelineAsset).GetField("m_DefaultRendererIndex", BindingFlags.Instance | BindingFlags.NonPublic);
                    int defaultIndex = indexField != null ? (int)indexField.GetValue(urpAsset) : 0;
                    if (defaultIndex >= 0 && defaultIndex < rendererDataArray.Length) {
                        rendererData = rendererDataArray[defaultIndex];
                    } else {
                        rendererData = rendererDataArray[0];
                    }
                }
            }

            if (rendererData == null) return;

            bool hasFeature = false;
            foreach (var feature in rendererData.rendererFeatures)
            {
                if (feature != null && feature is TranslucentRendererFeature)
                {
                    hasFeature = true;
                    break;
                }
            }

            if (!hasFeature)
            {
                SessionState.SetBool(s_PrefKey, true); // Don't show again this session
                ShowWindow();
            }
        }

        public static void ShowWindow()
        {
            var window = GetWindow<TranslucentSetupWindow>(true, "Translucent Image", true);
            window.minSize = new Vector2(400, 200);
            window.maxSize = new Vector2(400, 200);
            
            // Try to center window
            Rect rect = window.position;
            if (SceneView.lastActiveSceneView != null)
            {
                rect.x = SceneView.lastActiveSceneView.position.x + SceneView.lastActiveSceneView.position.width / 2 - 200;
                rect.y = SceneView.lastActiveSceneView.position.y + SceneView.lastActiveSceneView.position.height / 2 - 100;
            }
            window.position = rect;
            window.Show();
        }

        private void OnGUI()
        {
            // Title Header
            EditorGUILayout.Space(15);
            GUIStyle titleStyle = new GUIStyle(EditorStyles.label);
            titleStyle.fontSize = 16;
            titleStyle.padding = new RectOffset(10, 10, 0, 0);
            titleStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
            titleStyle.wordWrap = true;

            EditorGUILayout.LabelField("Missing Renderer Feature", titleStyle);
            EditorGUILayout.Space(10);
            
            // Description Text
            GUIStyle descStyle = new GUIStyle(EditorStyles.label);
            descStyle.fontSize = 13;
            descStyle.wordWrap = true;
            descStyle.padding = new RectOffset(10, 10, 0, 0);
            descStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
            
            EditorGUILayout.LabelField("Translucent Image needs a renderer feature added to the active Renderer Asset. Do you want to add it now?", descStyle);
            
            EditorGUILayout.Space(15);
            
            // Link
            GUIStyle linkStyle = new GUIStyle(EditorStyles.label);
            linkStyle.normal.textColor = new Color(0.35f, 0.6f, 0.9f); // Unity link blue
            linkStyle.fontSize = 13;
            linkStyle.padding = new RectOffset(10, 10, 0, 0);
            if (GUILayout.Button("More info", linkStyle, GUILayout.Width(80)))
            {
                Application.OpenURL("https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest/index.html?subfolder=/manual/urp-renderer-feature.html");
            }
            
            // Native EditorGUI Utility changing cursor when hovering 
            EditorGUIUtility.AddCursorRect(GUILayoutUtility.GetLastRect(), MouseCursor.Link);

            // Bottom padded Button
            GUILayout.FlexibleSpace();
            
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.fontSize = 14;
            buttonStyle.fixedHeight = 30;
            buttonStyle.margin = new RectOffset(20, 20, 10, 10);
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(10);
            if (GUILayout.Button("Select Current Renderer Asset", buttonStyle))
            {
                HighlightRendererData();
                this.Close();
            }
            GUILayout.Space(10);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(5);
        }

        private void HighlightRendererData()
        {
            var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urpAsset != null)
            {
                ScriptableRendererData activeRenderer = null;
                
                // Reflection to grab active renderer safely
                FieldInfo rendererListField = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.Instance | BindingFlags.NonPublic);
                if (rendererListField != null)
                {
                    var rendererDataArray = rendererListField.GetValue(urpAsset) as ScriptableRendererData[];
                    
                    FieldInfo indexField = typeof(UniversalRenderPipelineAsset).GetField("m_DefaultRendererIndex", BindingFlags.Instance | BindingFlags.NonPublic);
                    int defaultIndex = indexField != null ? (int)indexField.GetValue(urpAsset) : 0;
                    
                    if (rendererDataArray != null && rendererDataArray.Length > 0 && defaultIndex < rendererDataArray.Length)
                    {
                        activeRenderer = rendererDataArray[defaultIndex];
                    }
                }

                if (activeRenderer != null)
                {
                    Selection.activeObject = activeRenderer;
                    EditorGUIUtility.PingObject(activeRenderer);
                }
                else
                {
                    // Fallback to pinging the Pipeline Asset if we couldn't resolve the exact Renderer Data
                    Selection.activeObject = urpAsset;
                    EditorGUIUtility.PingObject(urpAsset);
                    Debug.LogWarning("TranslucentUIFX: Could not automatically select inner Renderer Data. Highlighted Pipeline Asset instead. Please find your Renderer Data in the Inspector manually.");
                }
            }
        }
    }
}
