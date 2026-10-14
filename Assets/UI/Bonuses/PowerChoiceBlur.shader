Shader "Crousty/PowerChoiceBlur"
{
    Properties { _MainTex ("World", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex;
        float4 _MainTex_TexelSize;
        fixed4 Blur(v2f_img i, float2 axis)
        {
            float2 d = _MainTex_TexelSize.xy * axis;
            fixed4 c = tex2D(_MainTex, i.uv) * .227027;
            c += tex2D(_MainTex, i.uv + d * 1.384615) * .316216;
            c += tex2D(_MainTex, i.uv - d * 1.384615) * .316216;
            c += tex2D(_MainTex, i.uv + d * 3.230769) * .070270;
            c += tex2D(_MainTex, i.uv - d * 3.230769) * .070270;
            return c;
        }
        fixed4 Horizontal(v2f_img i) : SV_Target { return Blur(i, float2(1,0)); }
        fixed4 Vertical(v2f_img i) : SV_Target { return Blur(i, float2(0,1)); }
        ENDCG
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment Horizontal
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment Vertical
            ENDCG
        }
    }
}
