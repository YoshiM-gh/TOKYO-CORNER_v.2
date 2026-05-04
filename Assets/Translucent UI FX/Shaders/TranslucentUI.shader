Shader "UI/TranslucentUIFX"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
        
        // TranslucentFX properties
        _BlurStrength("Blur Strength", Range(0, 1)) = 1.0
        _LuminosityBoost("Luminosity Boost", Range(0, 1)) = 0.0
        _TintColor("FX Tint Color", Color) = (0.8, 0.9, 1.0, 0.3)
        _FrostAmount("Frost Brightness", Range(0, 1)) = 0.0
        _NoiseAmount("Film Grain", Range(0, 1)) = 0.05
        _RefractionAmount("Refraction Distortion", Range(-0.1, 0.1)) = 0.02
        _SphericalDistortion("Spherical Distortion", Float) = 0.0
        _ChromaticAberration("Chromatic Aberration", Range(0, 0.1)) = 0.0
        _SpecularGlare("Specular Glare", Range(0, 1)) = 0.5
        
        _Brightness("Brightness", Range(0, 5)) = 1.0
        _Saturation("Saturation", Range(0, 5)) = 1.0
        _Contrast("Contrast", Range(0, 5)) = 1.0
        
        _AutoReadability("Auto Readability", Range(0, 1)) = 0.0
        
        _EdgeColor("Edge Highlight Color", Color) = (1,1,1,1)
        _EdgeWidth("Edge Highlight Width", Range(0, 1)) = 0.05
        _EdgePower("Edge Highlight Power", Range(0.1, 10)) = 2.0
        _EdgeShape("Edge Shape", Float) = 0.0
        _EdgeRounding("Edge Rounding Radius", Range(0, 0.5)) = 0.1
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
        HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float4 color        : COLOR;
                float2 texcoord     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float4 positionNDC  : TEXCOORD1;
                float4 color        : COLOR;
                float2 texcoord     : TEXCOORD0;
                float4 worldPosition : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float _BlurStrength;
            float _LuminosityBoost;
            float4 _TintColor;
            float _FrostAmount;
            float _NoiseAmount;
            float _RefractionAmount;
            float _SphericalDistortion;
            float _ChromaticAberration;
            float _SpecularGlare;
            
            float _Brightness;
            float _Saturation;
            float _Contrast;
            
            float _AutoReadability;
            
            float4 _EdgeColor;
            float _EdgeWidth;
            float _EdgePower;
            float _EdgeShape;
            float _EdgeRounding;
            
            float4 _ClipRect;
            CBUFFER_END
            
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;
            
            TEXTURE2D(_TranslucentUI_BlurredTex);
            SAMPLER(sampler_TranslucentUI_BlurredTex);

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionNDC = ComputeScreenPos(output.positionCS);
                output.worldPosition = input.positionOS;
                
                output.texcoord = input.texcoord;
                output.color = input.color * _Color;

                return output;
            }

            // Pseudo random noise
            float rand(float2 n) { 
                return frac(sin(dot(n, float2(12.9898, 4.1414))) * 43758.5453);
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Calculate screen UV
                float2 screenUV = input.positionNDC.xy / input.positionNDC.w;

                // Rect clip logic manually, as URP may not import UnityUI.cginc easily everywhere
                float2 uv = input.worldPosition.xy;
                bool clipFail = uv.x < _ClipRect.x || uv.x > _ClipRect.z || uv.y < _ClipRect.y || uv.y > _ClipRect.w;
                float clipAlpha = clipFail ? 0.0 : 1.0;
                // Simplified. If _ClipRect is not set, it's usually (-inf, -inf, inf, inf).
                // Actually the standard way is:
                // float clipAlpha = UnityGet2DClipping(input.worldPosition.xy, _ClipRect);

                // Sample MainTex (UI image mask/sprite)
                half4 uiColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.texcoord);
                uiColor *= input.color;
                
                // Lens Refraction (Magnify/distort background slightly)
                float2 centerOffset = input.texcoord - 0.5; // -0.5 to 0.5
                
                // Curve the refraction so it pinches at the edges (spherical mapping estimation)
                float3 normal3D;
                normal3D.xy = centerOffset * 2.0; 
                float r = saturate(length(normal3D.xy));
                
                // Authentic physical glass dome depth: perfectly flat in center, steep at rim.
                normal3D.z = sqrt(1.0 - r * r); 
                
                // Calculate physical Snell refraction vector using Normal dot View angle
                float NdotV = normal3D.z; // Simplified View = (0,0,1)
                
                // pow(val, 1.5) provides a thick stylized liquid glass rim while keeping the deep center readable
                float refractionMag = lerp(0.0, pow(1.0 - NdotV, 1.5), _SphericalDistortion);
                
                screenUV += normal3D.xy * refractionMag * _RefractionAmount * clipAlpha;
                
                // 1. Sample Blurred Screen (with optional Chromatic Aberration color splitting)
                half4 blurColor;
                if (_ChromaticAberration > 0.001)
                {
                    // Scale CA to prevent extreme tearing while preserving thick rainbow rims
                    float2 caOffset = normal3D.xy * refractionMag * (_ChromaticAberration * 0.6) * clipAlpha;
                    blurColor.r = SAMPLE_TEXTURE2D(_TranslucentUI_BlurredTex, sampler_TranslucentUI_BlurredTex, screenUV + caOffset).r;
                    blurColor.g = SAMPLE_TEXTURE2D(_TranslucentUI_BlurredTex, sampler_TranslucentUI_BlurredTex, screenUV).g;
                    blurColor.b = SAMPLE_TEXTURE2D(_TranslucentUI_BlurredTex, sampler_TranslucentUI_BlurredTex, screenUV - caOffset).b;
                    blurColor.a = SAMPLE_TEXTURE2D(_TranslucentUI_BlurredTex, sampler_TranslucentUI_BlurredTex, screenUV).a;
                }
                else
                {
                    blurColor = SAMPLE_TEXTURE2D(_TranslucentUI_BlurredTex, sampler_TranslucentUI_BlurredTex, screenUV);
                }
                
                // Base luminance for Saturation
                float baseLuminance = dot(blurColor.rgb, float3(0.299, 0.587, 0.114));

                // -- Color Correction Stack --
                blurColor.rgb = lerp(baseLuminance.xxx, blurColor.rgb, _Saturation);
                blurColor.rgb = (blurColor.rgb - 0.5) * _Contrast + 0.5;
                blurColor.rgb *= _Brightness;

                // Volumetric Inner Fresnel Shadow
                float fresnel = pow(1.0 - NdotV, 3.0);
                float shadowIntensity = fresnel * _SphericalDistortion * clipAlpha;
                blurColor.rgb = lerp(blurColor.rgb, blurColor.rgb * 0.2, shadowIntensity);
                
                // Re-calculate physical luminance after color stack for accurate frost/light scattering
                float luminance = dot(blurColor.rgb, float3(0.299, 0.587, 0.114));
                
                // 🧠 Auto Readability System
                float readabilityOffset = (0.5 - luminance) * _AutoReadability;
                blurColor.rgb = saturate(blurColor.rgb + readabilityOffset);
                luminance = dot(blurColor.rgb, float3(0.299, 0.587, 0.114));

                // 2. Stylized Realism: Mute and Desaturate raw colors
                blurColor.rgb = lerp(blurColor.rgb, luminance.xxx, _FrostAmount);
                
                // Additive Luminosity Boost
                blurColor.rgb += luminance.xxx * _LuminosityBoost;

                // Light absorption
                blurColor.rgb *= 0.85;

                // 3. Base logic: combine muted blur texture with user's tint color
                half4 finalColor = lerp(blurColor, _TintColor, _TintColor.a);
                
                // Optional slight physical brightness from frost particles
                finalColor.rgb += _FrostAmount * 0.1;

                // 4. Edge Highlight (Dual Layered: Crisp Rim + Inner Volume)
                float edgeDist = 0.0;
                
                if (_EdgeShape > 2.5)
                {
                    float2 b = float2(0.5, 0.5);
                    float2 q = abs(centerOffset);
                    float ndot_ab = (b.x - 2.0 * q.x) * b.x - (b.y - 2.0 * q.y) * b.y;
                    float h = clamp(ndot_ab / dot(b, b), -1.0, 1.0);
                    float d = length(q - 0.5 * b * float2(1.0 - h, 1.0 + h));
                    float signedDist = d * sign(q.x * b.y + q.y * b.x - b.x * b.y);
                    edgeDist = -signedDist;
                }
                else if (_EdgeShape > 1.5)
                {
                    float2 d = abs(centerOffset) - float2(0.5, 0.5) + float2(_EdgeRounding, _EdgeRounding);
                    float signedDist = length(max(d, 0.0)) + min(max(d.x, d.y), 0.0) - _EdgeRounding;
                    edgeDist = -signedDist;
                }
                else if (_EdgeShape > 0.5)
                {
                    edgeDist = 0.5 - length(centerOffset);
                }
                else
                {
                    float2 distToEdge = min(input.texcoord, 1.0 - input.texcoord);
                    edgeDist = min(distToEdge.x, distToEdge.y);
                }
                edgeDist = max(0.0001, edgeDist);
                
                // Crisp outer rim (approx 1-2 pixels)
                float crispRim = 1.0 - saturate(edgeDist * 400.0);
                
                // Soft inner fresnel
                float innerGlow = pow(saturate(1.0 - (edgeDist / max(0.001, _EdgeWidth))), _EdgePower);
                finalColor.rgb += _EdgeColor.rgb * _EdgeColor.a * innerGlow * uiColor.a * clipAlpha;
                
                // 5. Procedural Specular Highlight (The 'Apple Liquid' Glare)
                float3 lightDir = normalize(float3(-0.5, 0.55, 1.0)); // Top-Left bright spot
                float3 viewDir = float3(0.0, 0.0, 1.0);
                float3 halfVector = normalize(lightDir + viewDir);
                float NdotH = saturate(dot(normal3D, halfVector));
                
                float specular = pow(NdotH, 128.0); // Tight, sharp ping
                float specularIntensity = specular * _SphericalDistortion * clipAlpha * _SpecularGlare;
                
                // Add additive blinding white light
                finalColor.rgb += float3(1.0, 1.0, 1.0) * specularIntensity;

                // 5. Volume Scattering (Center Glow)
                float radialFalloff = length(input.texcoord - 0.5) * 1.414;
                float centerScatter = saturate(1.0 - radialFalloff);
                finalColor.rgb += centerScatter * _FrostAmount * 0.15;

                // 5. Film Grain (Color Noise to simulate frosted acrylic micro-texture)
                float noise = frac(sin(dot(input.positionCS.xy, float2(12.9898, 4.1414))) * 43758.5453) * 2.0 - 1.0;
                finalColor.rgb += noise * _NoiseAmount;

                // 6. Alpha Masking based on Canvas geometry/clipping
                finalColor.a = uiColor.a * clipAlpha;
                finalColor.rgb = saturate(finalColor.rgb);

                return finalColor;
            }
        ENDHLSL
        }
    }
}
