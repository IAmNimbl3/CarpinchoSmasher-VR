Shader "Carpincho/Teleport Hotspot GTA"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 0.18, 0.28, 1)
        _EmissionColor ("Emission Color", Color) = (1, 0.12, 0.22, 1)
        _Alpha ("Alpha", Range(0, 1)) = 0.45
        _TopFade ("Top Fade", Range(0.01, 1)) = 0.55
        _BottomGlow ("Bottom Glow", Range(0, 2)) = 0.8
        _RimPower ("Rim Power", Range(0.25, 8)) = 2.5
        _RimIntensity ("Rim Intensity", Range(0, 3)) = 1.2
        _PulseSpeed ("Pulse Speed", Range(0, 8)) = 1.5
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0.2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "HotspotGlow"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _EmissionColor;
                half _Alpha;
                half _TopFade;
                half _BottomGlow;
                half _RimPower;
                half _RimIntensity;
                half _PulseSpeed;
                half _PulseAmount;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetWorldSpaceViewDir(positionInputs.positionWS);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half height = saturate(input.uv.y);
                half verticalFade = saturate(1.0h - smoothstep(_TopFade, 1.0h, height));
                half baseGlow = saturate(1.0h - height) * _BottomGlow;

                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = normalize(input.viewDirWS);
                half rim = pow(saturate(1.0h - dot(normalWS, viewDirWS)), _RimPower) * _RimIntensity;

                half pulse = 1.0h + sin(_Time.y * _PulseSpeed) * _PulseAmount;
                half alpha = saturate((_Alpha * verticalFade + rim * 0.25h + baseGlow * 0.2h) * pulse);
                half3 color = _BaseColor.rgb + _EmissionColor.rgb * (rim + baseGlow) * pulse;

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
