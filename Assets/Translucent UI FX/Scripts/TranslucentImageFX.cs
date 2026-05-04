using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TranslucentUIFX
{
    public enum PerformanceMode
    {
        Low,
        Medium,
        High
    }

    public enum GlassPreset
    {
        Custom,
        DefaultGlass,
        SoftFrost,
        DarkGlass,
        WhiteGlass,
        LiquidGlass,
        StrongBlur
    }

    public enum EdgeShape
    {
        Rectangle,
        Circle,
        RoundedRect,
        Diamond
    }

    [AddComponentMenu("UI/Translucent Image FX")]
    [RequireComponent(typeof(CanvasRenderer))]
    public class TranslucentImageFX : Image
    {
        [Header("Translucent FX Settings")]
        public GlassPreset CurrentPreset = GlassPreset.DefaultGlass;
        public bool AdvancedMode = false;
        [Range(0f, 1f)] public float GlassIntensity = 1f;
        [Range(0f, 1f)] public float LuminosityBoost = 0.1f;
        
        [Range(0f, 3f)] public float Brightness = 1.0f;
        [Range(0f, 3f)] public float Saturation = 1.0f;
        [Range(0f, 3f)] public float Contrast = 1.0f;
        
        public bool AutoReadability = false;
        
        [Range(0f, 1f)] public float BlurStrength = 0.2f;
        public Color TintColor = new Color(0.8f, 0.9f, 1.0f, 0.3f);
        [Range(0f, 1f)] public float FrostAmount = 0f;
        [Range(0f, 1f)] public float NoiseAmount = 0.05f;
        
        [Header("Optical Glass Settings")]
        [Range(-0.1f, 0.1f)] public float RefractionAmount = 0.02f;
        public bool SphericalDistortion = false;
        [Range(0f, 0.1f)] public float ChromaticAberration = 0f;
        [Range(0f, 1f)] public float SpecularGlare = 0.5f;
        
        public bool EnableEdgeLighting = true;
        public EdgeShape EdgeShape = EdgeShape.Rectangle;
        [Range(0f, 0.5f)] public float EdgeRounding = 0.1f;
        public Color EdgeLightColor = new Color(1f, 1f, 1f, 0.8f);
        [Range(0f, 1f)] public float EdgeLightWidth = 0.05f;
        [Range(0.1f, 10f)] public float EdgeLightPower = 2.0f;

        [Header("Performance & Quality")]
        public PerformanceMode QualityMode = PerformanceMode.High;
        public TranslucentUpdateMode UpdateMode = TranslucentUpdateMode.Always;
        [Range(1, 120)] public int UpdateInterval = 3;
        
        // Keep track of active instances so the renderer feature can read them.
        public static readonly List<TranslucentImageFX> ActiveInstances = new List<TranslucentImageFX>();

        private Material m_CustomMaterial;

        public void ApplyPreset(GlassPreset preset)
        {
            CurrentPreset = preset;
            if (preset == GlassPreset.Custom) return;

            AdvancedMode = false;

            switch (preset)
            {
                case GlassPreset.DefaultGlass:
                    GlassIntensity = 0.9f;
                    LuminosityBoost = 0.25f;
                    Brightness = 1.0f;
                    Saturation = 1.0f;
                    Contrast = 1.0f;
                    AutoReadability = false;
                    TintColor = new Color(0.85f, 0.92f, 1.0f, 0.25f); // slight cool blue
                    FrostAmount = 0.15f;
                    BlurStrength = 0.2f;
                    EnableEdgeLighting = true;
                    EdgeLightColor = new Color(1f, 1f, 1f, 0.7f);
                    EdgeLightWidth = 0.02f; // Sleek, thin premium edge
                    EdgeLightPower = 1.5f;
                    NoiseAmount = 0.015f;
                    RefractionAmount = 0.015f; // Slight lens bulge
                    SphericalDistortion = false;
                    ChromaticAberration = 0f;
                    SpecularGlare = 0f;
                    break;
                case GlassPreset.SoftFrost:
                    GlassIntensity = 1.0f;
                    LuminosityBoost = 0.12f;
                    Brightness = 1.15f;
                    Saturation = 0.8f;
                    Contrast = 0.9f;
                    AutoReadability = true;
                    TintColor = new Color(1.0f, 0.97f, 0.94f, 0.45f); // "Light through paper" warm tint shift
                    FrostAmount = 0.42f;
                    BlurStrength = 0.2f;
                    EnableEdgeLighting = true;
                    EdgeLightColor = new Color(1f, 1f, 1f, 0.35f);  // Softer edges
                    EdgeLightWidth = 0.08f;                         // Melting deep into surface
                    EdgeLightPower = 1.2f;                          // Extremely gentle curve
                    NoiseAmount = 0.002f;
                    RefractionAmount = 0.0f;                        // Flat paper look
                    SphericalDistortion = false;
                    ChromaticAberration = 0f;
                    SpecularGlare = 0f;
                    break;
                case GlassPreset.DarkGlass:
                    GlassIntensity = 1.0f;
                    LuminosityBoost = 0.14f;
                    Brightness = 0.8f;
                    Saturation = 1.3f;
                    Contrast = 1.4f;
                    AutoReadability = true;
                    TintColor = new Color(0.02f, 0.04f, 0.16f, 0.75f); // Rich deep indigo
                    FrostAmount = 0.23f; // Reduced foggy haze for extreme clarity
                    BlurStrength = 0.2f;
                    EnableEdgeLighting = true;
                    EdgeLightColor = new Color(0.1f, 0.12f, 0.3f, 0.9f); // Subtle indigo rim glow
                    EdgeLightWidth = 0.03f;                       // Pulled tighter
                    EdgeLightPower = 4.0f;                        // High exponential curve fixes "border" stroke look
                    NoiseAmount = 0.0f;
                    RefractionAmount = 0.03f;                     // Heavy glass distortion
                    SphericalDistortion = false;
                    ChromaticAberration = 0.0f;
                    SpecularGlare = 0f;
                    break;
                case GlassPreset.WhiteGlass:
                    GlassIntensity = 1.0f;
                    LuminosityBoost = 0.14f;
                    Brightness = 0.8f;
                    Saturation = 1.3f;
                    Contrast = 1.4f;
                    AutoReadability = true;
                    TintColor = new Color(1.0f, 1.0f, 1.0f, 0.75f); // Solid clean white glass
                    FrostAmount = 0.23f; 
                    BlurStrength = 0.2f;
                    EnableEdgeLighting = true;
                    EdgeLightColor = new Color(0.85f, 0.85f, 0.85f, 0.9f); // Bright greyish-white edge
                    EdgeLightWidth = 0.03f;                       
                    EdgeLightPower = 4.0f;                        
                    NoiseAmount = 0.0f;
                    RefractionAmount = 0.03f;                     
                    SphericalDistortion = false;
                    ChromaticAberration = 0f;
                    SpecularGlare = 0f;
                    break;
                case GlassPreset.LiquidGlass:
                    GlassIntensity = 1.0f;
                    LuminosityBoost = 0.0f;
                    Brightness = 1.05f;
                    Saturation = 1.3f; // Bump sat slightly to compensate for heavy rim masking
                    Contrast = 1.15f;
                    AutoReadability = false;
                    TintColor = new Color(1.0f, 1.0f, 1.0f, 0.0f); // Purely transparent body
                    FrostAmount = 0.02f; // Almost no frost
                    BlurStrength = 0.0f; // Crystal clear
                    EnableEdgeLighting = true;
                    EdgeShape = EdgeShape.Circle;
                    EdgeLightColor = new Color(0.85f, 0.9f, 1.0f, 0.2f); // Gentle inner stroke to supplement Specular reflection
                    EdgeLightWidth = 0.03f;                       
                    EdgeLightPower = 4.0f;                        
                    NoiseAmount = 0.0f;
                    RefractionAmount = -0.15f; // Negative stretch creates true Barrel distortion (bulging outwards)      
                    SphericalDistortion = true; 
                    ChromaticAberration = 0.08f; 
                    SpecularGlare = 0.0f;
                    break;
                case GlassPreset.StrongBlur:
                    GlassIntensity = 1.0f;
                    LuminosityBoost = 0.17f; // High natural lift
                    Brightness = 1.05f;
                    Saturation = 1.1f;
                    Contrast = 1.1f;
                    TintColor = new Color(0.95f, 0.95f, 1.0f, 0.2f); // Subtle clear tint
                    FrostAmount = 0.25f;
                    BlurStrength = 0.5f; // Pushed higher for abstract blobs
                    EnableEdgeLighting = true;
                    EdgeLightColor = new Color(1f, 1f, 1f, 0.2f); // Extremely faint outline
                    EdgeLightWidth = 0.15f;                       // Very ultra-soft wide bleed
                    EdgeLightPower = 1.0f;                        // Linear glow, no sharp crease
                    NoiseAmount = 0.01f;
                    RefractionAmount = 0.05f;
                    SphericalDistortion = false;
                    ChromaticAberration = 0f;
                    SpecularGlare = 0f;
                    break;
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            ActiveInstances.Add(this);
            SetMaterialDirty();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            ActiveInstances.Remove(this);
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            SetMaterialDirty();
            TranslucentRendererFeature.RequestUpdate();
        }
#endif

        #region Animation Helpers
        
        private Coroutine m_AnimationCoroutine;

        /// <summary>
        /// Instantly enables the GameObject and smoothly animates the complete glass material into existence.
        /// </summary>
        public void FadeIn(float duration = 0.5f)
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            AnimateGlassIntensity(1f, duration);
        }

        /// <summary>
        /// Smoothly fades out the glass material. Optionally disables the GameObject when finished.
        /// </summary>
        public void FadeOut(float duration = 0.5f, bool disableOnComplete = false)
        {
            AnimateGlassIntensity(0f, duration, disableOnComplete);
        }

        /// <summary>
        /// Animates the master Glass Intensity slider, perfectly syncing Blur, Frost, and Edge Lighting over time.
        /// </summary>
        public void AnimateGlassIntensity(float targetIntensity, float duration, bool disableOnComplete = false)
        {
            if (!Application.isPlaying)
            {
                GlassIntensity = targetIntensity;
                SetMaterialDirty();
                if (disableOnComplete && targetIntensity <= 0f) gameObject.SetActive(false);
                return;
            }

            if (m_AnimationCoroutine != null) StopCoroutine(m_AnimationCoroutine);
            if (gameObject.activeInHierarchy)
            {
                m_AnimationCoroutine = StartCoroutine(SmoothGlassRoutine(targetIntensity, duration, disableOnComplete));
            }
        }

        private System.Collections.IEnumerator SmoothGlassRoutine(float target, float duration, bool disable)
        {
            float start = GlassIntensity;
            float time = 0f;
            
            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                float t = time / duration;
                
                // Smoothstep curve for cinematic premium easing
                t = t * t * (3f - 2f * t);
                
                GlassIntensity = Mathf.Lerp(start, target, t);
                SetMaterialDirty();
                yield return null;
            }

            GlassIntensity = target;
            SetMaterialDirty();
            
            if (disable && target <= 0.001f)
            {
                gameObject.SetActive(false);
            }
        }
        
        #endregion

        private static Material s_DefaultTranslucentMaterial;

        public static Material GetDefaultMaterial()
        {
            if (s_DefaultTranslucentMaterial == null)
            {
                Shader shader = Shader.Find("UI/TranslucentUIFX");
                if (shader != null)
                {
                    s_DefaultTranslucentMaterial = new Material(shader);
                    s_DefaultTranslucentMaterial.hideFlags = HideFlags.HideAndDontSave;
                }
            }
            return s_DefaultTranslucentMaterial;
        }

        public override Material materialForRendering
        {
            get
            {
                Material baseMat = base.materialForRendering;
                
                // Completely invisible Zero-Config workflow: If the user drops this component without explicitly passing
                // an assigned material, Unity returns the Default-UI material. We intercept and force our own base glass shader!
                if (baseMat == null || baseMat.shader.name != "UI/TranslucentUIFX")
                {
                    baseMat = GetDefaultMaterial();
                }

                if (!isActiveAndEnabled || baseMat == null)
                    return baseMat;

                if (m_CustomMaterial == null || m_CustomMaterial.shader != baseMat.shader)
                {
                    m_CustomMaterial = new Material(baseMat);
                    m_CustomMaterial.hideFlags = HideFlags.HideAndDontSave;
                }
                
                m_CustomMaterial.CopyPropertiesFromMaterial(baseMat);

                m_CustomMaterial.SetFloat("_Brightness", Brightness);
                m_CustomMaterial.SetFloat("_Saturation", Saturation);
                m_CustomMaterial.SetFloat("_Contrast", Contrast);
                
                m_CustomMaterial.SetFloat("_AutoReadability", AutoReadability ? 0.75f : 0.0f);

                m_CustomMaterial.SetFloat("_LuminosityBoost", LuminosityBoost);
                
                m_CustomMaterial.SetFloat("_BlurStrength", BlurStrength * GlassIntensity);
                
                Color finalTint = TintColor;
                finalTint.a *= GlassIntensity;
                m_CustomMaterial.SetColor("_TintColor", finalTint);
                
                m_CustomMaterial.SetFloat("_FrostAmount", FrostAmount * GlassIntensity);
                m_CustomMaterial.SetFloat("_NoiseAmount", NoiseAmount);
                m_CustomMaterial.SetFloat("_RefractionAmount", RefractionAmount);
                m_CustomMaterial.SetFloat("_SphericalDistortion", SphericalDistortion ? 1.0f : 0.0f);
                m_CustomMaterial.SetFloat("_ChromaticAberration", ChromaticAberration);
                m_CustomMaterial.SetFloat("_SpecularGlare", SpecularGlare);
                
                if (EnableEdgeLighting)
                {
                    Color finalEdge = EdgeLightColor;
                    finalEdge.a *= GlassIntensity;
                    m_CustomMaterial.SetColor("_EdgeColor", finalEdge);
                    m_CustomMaterial.SetFloat("_EdgeWidth", EdgeLightWidth);
                    m_CustomMaterial.SetFloat("_EdgePower", EdgeLightPower);
                    
                    float shapeIndex = 0.0f;
                    if (EdgeShape == EdgeShape.Circle) shapeIndex = 1.0f;
                    else if (EdgeShape == EdgeShape.RoundedRect) shapeIndex = 2.0f;
                    else if (EdgeShape == EdgeShape.Diamond) shapeIndex = 3.0f;
                    
                    m_CustomMaterial.SetFloat("_EdgeShape", shapeIndex);
                    m_CustomMaterial.SetFloat("_EdgeRounding", EdgeRounding);
                }
                else
                {
                    m_CustomMaterial.SetColor("_EdgeColor", Color.clear);
                    m_CustomMaterial.SetFloat("_EdgeWidth", 0f);
                    m_CustomMaterial.SetFloat("_EdgePower", 1f);
                    m_CustomMaterial.SetFloat("_EdgeShape", 0f);
                }
                
                return m_CustomMaterial;
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (m_CustomMaterial != null)
            {
                if (Application.isPlaying)
                    Destroy(m_CustomMaterial);
                else
                    DestroyImmediate(m_CustomMaterial);
            }
        }
    }
}
