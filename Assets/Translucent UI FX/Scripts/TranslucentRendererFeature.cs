using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TranslucentUIFX
{
    public enum TranslucentUpdateMode
    {
        [InspectorName("⚡ Real-Time (Always)")] Always,
        [InspectorName("🧠 Smart Update (Camera Based)")] SmartUpdate,
        [InspectorName("📉 Performance (Interval)")] Interval,
        [InspectorName("🎯 Manual (Script Triggered)")] Manual
    }

    public enum TranslucentInjectionPoint
    {
        [InspectorName("✨ Auto (Recommended for URP 2D & 3D)")]
        Auto = 0,

        [InspectorName("Before Transparents (Camera & World Space UI)")]
        BeforeRenderingTransparents = RenderPassEvent.BeforeRenderingTransparents,
        
        [InspectorName("After Transparents (Overlay UI Default)")]
        AfterRenderingTransparents = RenderPassEvent.AfterRenderingTransparents,
        
        [InspectorName("After Post-Processing (Includes Final Effects)")]
        AfterRenderingPostProcessing = RenderPassEvent.AfterRenderingPostProcessing
    }

    public class TranslucentRendererFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class TranslucentSettings
        {
            [Header("Render Pipeline Routing")]
            [Tooltip("Auto intelligently matches URP 2D vs URP 3D constraints.")]
            public TranslucentInjectionPoint injectionPoint = TranslucentInjectionPoint.Auto;

            [Header("Internal Backend Configuration")]
            [Tooltip("Default fallback if no active instances specify a mode.")]
            public TranslucentUpdateMode updateMode = TranslucentUpdateMode.Always;
            [Tooltip("Default fallback if no active instances specify an interval.")]
            [Range(1, 120)] public int updateIntervalFrames = 3;
        }

        public TranslucentSettings settings = new TranslucentSettings();
        TranslucentRenderPass m_ScriptablePass;

        public static void RequestUpdate()
        {
            s_UpdateRequestedFrame = Time.frameCount;
        }

        public static string GlobalBlurTextureName = "_TranslucentUI_BlurredTex";
        public static int GlobalBlurTextureID => Shader.PropertyToID(GlobalBlurTextureName);

        private static int s_UpdateRequestedFrame = -1;

        internal static bool IsUpdateRequested()
        {
            return Time.frameCount <= s_UpdateRequestedFrame || s_UpdateRequestedFrame == -1;
        }

        private Material m_BlurMaterial;

        public override void Create()
        {
            if (m_BlurMaterial == null)
            {
                Shader shader = Shader.Find("Hidden/TranslucentUIFX/Blur");
                if (shader != null)
                    m_BlurMaterial = CoreUtils.CreateEngineMaterial(shader);
            }
            
            if (m_BlurMaterial != null)
                m_ScriptablePass = new TranslucentRenderPass(settings, m_BlurMaterial);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            // Do not run in preview or reflection
            if (renderingData.cameraData.cameraType == CameraType.Preview || renderingData.cameraData.cameraType == CameraType.Reflection)
                return;

            // Only run if there are active UI elements requiring the blur and a valid material
            if (TranslucentImageFX.ActiveInstances.Count == 0 || m_BlurMaterial == null || m_ScriptablePass == null)
                return;

            // Automatically resolve "Auto" injection point based on 2D vs 3D pipeline
            if (settings.injectionPoint == TranslucentInjectionPoint.Auto)
            {
                bool isURP2D = renderer.GetType().Name.Contains("Renderer2D");
                // 3D works flawlessly at AfterRenderingTransparents.
                // 2D inherently skips Transparents, so it must use BeforeRenderingPostProcessing 
                // to grab the texture before it gets blitted to the unreadable backbuffer.
                m_ScriptablePass.renderPassEvent = isURP2D ? RenderPassEvent.BeforeRenderingPostProcessing : RenderPassEvent.AfterRenderingTransparents;
            }
            else
            {
                m_ScriptablePass.renderPassEvent = (RenderPassEvent)settings.injectionPoint;
            }

            renderer.EnqueuePass(m_ScriptablePass);
        }
        
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (m_ScriptablePass != null)
                m_ScriptablePass.Dispose();
                
            CoreUtils.Destroy(m_BlurMaterial);
        }
    }
}
