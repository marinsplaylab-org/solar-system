Shader "MarinsPlayLab/SolarWebGLLit"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)

        _SpecColor ("Spec Color", Color) = (0.1,0.1,0.1,1)
        _Smoothness ("Smoothness", Range(0,1)) = 0.2

        // Keep this at 0 for planets if you want a hard night-side.
        _Ambient ("Ambient (planets -> 0)", Range(0,1)) = 0

        // Emission (Sun material: set strength > 0)
        _EmissionMap ("Emission Map", 2D) = "black" {}
        [HDR]_EmissionColor ("Emission Color", Color) = (0,0,0,0)
        _EmissionStrength ("Emission Strength", Range(0,200)) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            // Additional lights (Point/Spot) in Forward
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS

            // Main light shadows (only matters if you have a main directional light)
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            // Optional: additional light shadows (Point/Spot shadow maps).
            // If this makes WebGL compilation fail again, comment it out.
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            TEXTURE2D(_EmissionMap);
            SAMPLER(sampler_EmissionMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _SpecColor;
                float  _Smoothness;
                float  _Ambient;

                float4 _EmissionColor;
                float  _EmissionStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 positionWS  : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3; // for main-light shadows (directional)
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);

                OUT.positionCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv         = IN.uv;

                OUT.shadowCoord = GetShadowCoord(posInputs);
                return OUT;
            }

            half3 EvalDiffuseSpec(Light l, half3 N, half3 V, half3 albedoRgb, out half3 specOut)
            {
                half3 diff = LightingLambert(l.color, l.direction, N);
                specOut = LightingSpecular(l.color, l.direction, N, V, _SpecColor, _Smoothness);
                return albedoRgb * diff;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;

                half3 N = normalize(IN.normalWS);
                half3 V = normalize(GetWorldSpaceViewDir(IN.positionWS));

                // Start with artist ambient (planets: set to 0)
                half3 color = albedo.rgb * _Ambient;

                // --- Primary "Sun" point light: use additional light 0 (if present) ---
                #if defined(_ADDITIONAL_LIGHTS)
                {
                    int count = GetAdditionalLightsCount();
                    if (count > 0)
                    {
                        // ShadowMask arg (half4(1,1,1,1)) is the common “no baked mask” default.
                        Light sun = GetAdditionalLight(0u, IN.positionWS, half4(1,1,1,1));

                        half3 specSun;
                        half3 diffSun = EvalDiffuseSpec(sun, N, V, albedo.rgb, specSun);

                        half att = sun.distanceAttenuation * sun.shadowAttenuation;
                        color += (diffSun + specSun) * att;
                    }
                }
                #endif

                // --- Optional main directional light contribution (if you have one) ---
                // This can help if you decide to use a directional light for shadows, etc.
                {
                    Light mainL = GetMainLight(IN.shadowCoord); // main shadowed light query :contentReference[oaicite:1]{index=1}
                    half3 specM;
                    half3 diffM = EvalDiffuseSpec(mainL, N, V, albedo.rgb, specM);

                    color += (diffM + specM) * mainL.shadowAttenuation;
                }

                // --- Emission (no keyword; Sun material sets strength > 0) ---
                half3 emisTex = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, IN.uv).rgb;
                half3 emission = emisTex * _EmissionColor.rgb * _EmissionStrength;
                color += emission;

                return half4(color, albedo.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
    }
}