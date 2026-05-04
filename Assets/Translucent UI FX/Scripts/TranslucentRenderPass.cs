using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace TranslucentUIFX
{
    public class TranslucentRenderPass : ScriptableRenderPass
    {
        private TranslucentRendererFeature.TranslucentSettings m_Settings;
        private Material m_BlurMaterial;
        private ProfilingSampler m_ProfilingSampler = new ProfilingSampler("Translucent UI FX Pass");

        private RTHandle m_PersistentTemp0;
        private RTHandle m_PersistentTemp1;
        private RTHandle m_PersistentBlurredTexture;

        private Dictionary<Camera, (Vector3 pos, Quaternion rot)> m_CameraTransforms = new Dictionary<Camera, (Vector3, Quaternion)>();

        private static readonly int s_TranslucentBlurredTextureID = Shader.PropertyToID("_TranslucentUI_BlurredTex");

        public TranslucentRenderPass(TranslucentRendererFeature.TranslucentSettings settings, Material blurMaterial)
        {
            m_Settings = settings;
            m_BlurMaterial = blurMaterial;
            this.renderPassEvent = (RenderPassEvent)settings.injectionPoint;
        }

        private void GetDynamicSettings(out TranslucentUpdateMode activeMode, out int activeInterval)
        {
            // Start with backend fallbacks
            activeMode = m_Settings.updateMode;
            activeInterval = m_Settings.updateIntervalFrames;

            if (TranslucentImageFX.ActiveInstances.Count == 0) return;

            activeMode = TranslucentUpdateMode.Manual; // Start at lowest priority
            activeInterval = 120; // Highest interval

            foreach (var instance in TranslucentImageFX.ActiveInstances)
            {
                if (instance.UpdateMode < activeMode) activeMode = instance.UpdateMode;
                if (instance.UpdateInterval < activeInterval) activeInterval = instance.UpdateInterval;
            }
        }

        private bool NeedsUpdate(Camera camera)
        {
            if (m_PersistentBlurredTexture == null) return true;
            if (TranslucentRendererFeature.IsUpdateRequested()) return true;

            GetDynamicSettings(out var activeMode, out var activeInterval);

            switch (activeMode)
            {
                case TranslucentUpdateMode.Always:
                    return true;
                
                case TranslucentUpdateMode.Interval:
                    return Time.frameCount % Mathf.Max(1, activeInterval) == 0;
                
                case TranslucentUpdateMode.Manual:
                    return TranslucentRendererFeature.IsUpdateRequested();

                case TranslucentUpdateMode.SmartUpdate:
                    bool changed = true;
                    if (m_CameraTransforms.TryGetValue(camera, out var lastTransform))
                    {
                        changed = (lastTransform.pos != camera.transform.position || 
                                   lastTransform.rot != camera.transform.rotation);
                    }
                    if (changed)
                    {
                        m_CameraTransforms[camera] = (camera.transform.position, camera.transform.rotation);
                    }
                    return changed;
            }
            return true;
        }

        private int CalculateDownsample(PerformanceMode reqQuality)
        {
            return reqQuality == PerformanceMode.High ? 2 : 
                   reqQuality == PerformanceMode.Medium ? 3 : 4;
        }

        public void Dispose()
        {
            m_PersistentTemp0?.Release();
            m_PersistentTemp1?.Release();
            m_PersistentBlurredTexture?.Release();
        }

        // --- RENDERGRAPH PIPELINE ---
        private class PassData
        {
            public TextureHandle srcCam;
            public TextureHandle temp0;
            public TextureHandle temp1;
            public TextureHandle blurredTex;
            public Material blurMaterial;
            public int iterations;
            public float maxBlur;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (m_BlurMaterial == null || TranslucentImageFX.ActiveInstances.Count == 0) return;

            var reqQuality = PerformanceMode.Low;
            float maxBlur = 0f;
            foreach (var instance in TranslucentImageFX.ActiveInstances) {
                if (instance.QualityMode > reqQuality) reqQuality = instance.QualityMode;
                if (instance.BlurStrength > maxBlur) maxBlur = instance.BlurStrength;
            }

            int downsample = CalculateDownsample(reqQuality);
            int iterations = reqQuality == PerformanceMode.High ? 4 :
                             reqQuality == PerformanceMode.Medium ? 3 : 2;

            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            desc.width = Mathf.Max(1, desc.width / downsample);
            desc.height = Mathf.Max(1, desc.height / downsample);
            desc.msaaSamples = 1;

            RenderingUtils.ReAllocateHandleIfNeeded(ref m_PersistentTemp0, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_TempBlur0");
            RenderingUtils.ReAllocateHandleIfNeeded(ref m_PersistentTemp1, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_TempBlur1");
            RenderingUtils.ReAllocateHandleIfNeeded(ref m_PersistentBlurredTexture, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_TranslucentUI_PersistentBlur");

            if (!NeedsUpdate(cameraData.camera))
            {
                TextureHandle blurredTexHandle = renderGraph.ImportTexture(m_PersistentBlurredTexture);
                
                using (var builder = renderGraph.AddUnsafePass<PassData>("Translucent UI FX - Skip Update", out var passData))
                {
                    builder.UseTexture(blurredTexHandle, AccessFlags.Read);
                    passData.blurredTex = blurredTexHandle;
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc((PassData data, UnsafeGraphContext context) =>
                    {
                        var cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                        cmd.SetGlobalTexture(s_TranslucentBlurredTextureID, data.blurredTex);
                    });
                }
                return;
            }

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            TextureHandle srcCam = resourceData.activeColorTexture;
            if (!srcCam.IsValid()) return;

            TextureHandle t0 = renderGraph.ImportTexture(m_PersistentTemp0);
            TextureHandle t1 = renderGraph.ImportTexture(m_PersistentTemp1);
            TextureHandle finalTex = renderGraph.ImportTexture(m_PersistentBlurredTexture);

            using (var builder = renderGraph.AddUnsafePass<PassData>("Translucent UI FX - RenderGraph", out var passData, m_ProfilingSampler))
            {
                passData.blurMaterial = m_BlurMaterial;
                passData.iterations = iterations;
                passData.maxBlur = maxBlur;

                builder.UseTexture(srcCam, AccessFlags.Read);
                builder.UseTexture(t0, AccessFlags.ReadWrite);
                builder.UseTexture(t1, AccessFlags.ReadWrite);
                builder.UseTexture(finalTex, AccessFlags.Write);

                passData.srcCam = srcCam;
                passData.temp0 = t0;
                passData.temp1 = t1;
                passData.blurredTex = finalTex;

                builder.AllowPassCulling(false); // Force it to run and write to the global shader propert

                builder.SetRenderFunc((PassData data, UnsafeGraphContext context) =>
                {
                    CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);

                    // 1. Blit from Active Color to Temp0
                    Blitter.BlitCameraTexture(cmd, data.srcCam, data.temp0);

                    var src = data.temp0;
                    var dst = data.temp1;
                    
                    float baseOffset = data.maxBlur * 3.5f;

                    // 2. Iterative blur
                    for (int i = 0; i < data.iterations; i++)
                    {
                        float currentOffset = (i + 1f) * baseOffset;
                        cmd.SetGlobalVector("_TranslucentBlurOffset", new Vector4(currentOffset / desc.width, currentOffset / desc.height, 0, 0));
                        Blitter.BlitCameraTexture(cmd, src, dst, data.blurMaterial, 0);

                        var temp = src;
                        src = dst;
                        dst = temp;
                    }

                    // 3. Output to Final Texture
                    Blitter.BlitCameraTexture(cmd, src, data.blurredTex);

                    // 4. Expose to UI components
                    cmd.SetGlobalTexture(s_TranslucentBlurredTextureID, data.blurredTex);
                });
            }
        }
        // Unity 6 RenderGraph natively manages execution; obsolete code stripped.
    }
}
