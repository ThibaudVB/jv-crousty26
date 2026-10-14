Shader "Crousty/CityGlow"
{
    Properties
    {
        _BaseColor("Day color", Color) = (.15,.18,.22,1)
        [HDR] _GlowColor("Night glow", Color) = (1.2,.72,.27,1)
        _Power("Power", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Cull Off
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _GlowColor;
                float _Power;
            CBUFFER_END
            float _CroustyCityNight, _CroustyCityDaylight;
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float fog : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half3 color = lerp(_BaseColor.rgb * _CroustyCityDaylight, _GlowColor.rgb, _CroustyCityNight * _Power);
                return half4(MixFog(color, input.fog), 1);
            }
            ENDHLSL
        }
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            float4 _BaseColor, _GlowColor;
            float _Power, _CroustyCityNight, _CroustyCityDaylight;
            struct Varyings { float4 position : SV_POSITION; UNITY_FOG_COORDS(0) };
            Varyings Vert(float4 vertex : POSITION)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(vertex);
                UNITY_TRANSFER_FOG(output, output.position);
                return output;
            }
            float4 Frag(Varyings input) : SV_Target
            {
                float4 color = float4(lerp(_BaseColor.rgb * _CroustyCityDaylight, _GlowColor.rgb, _CroustyCityNight * _Power), 1);
                UNITY_APPLY_FOG(input.fogCoord, color);
                return color;
            }
            ENDCG
        }
    }
}
