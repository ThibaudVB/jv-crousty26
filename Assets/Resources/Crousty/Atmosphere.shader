Shader "Skybox/CroustyAtmosphere"
{
    Properties
    {
        _SkyColor("Sky", Color) = (.63,.77,.92,1)
        _HorizonColor("Horizon", Color) = (.69,.79,.9,1)
        _SunColor("Sun", Color) = (1,.9,.7,1)
        _SunDirection("Sun direction", Vector) = (0,1,0,0)
        _CloudCoverage("Cloud coverage", Range(0,1)) = .24
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            float4 _SkyColor, _HorizonColor, _SunColor, _SunDirection;
            float _CloudCoverage, _CroustyCityNight, _CroustySkyTime;
            struct Varyings { float4 position : SV_POSITION; float3 direction : TEXCOORD0; };
            Varyings Vert(float4 vertex : POSITION)
            {
                Varyings output;
                output.position = UnityObjectToClipPos(vertex);
                output.direction = vertex.xyz;
                return output;
            }
            float Hash(float2 p)
            {
                float3 q = frac(float3(p.x,p.y,p.x) * .1031);
                q += dot(q, q.yzx + 33.33);
                return frac((q.x + q.y) * q.z);
            }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i),Hash(i + float2(1,0)),f.x),
                    lerp(Hash(i + float2(0,1)),Hash(i + 1),f.x),f.y);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float3 d = normalize(input.direction);
                float height = smoothstep(0,.65,d.y);
                half3 color = lerp(_HorizonColor.rgb, _SkyColor.rgb * lerp(.82,.45,_CroustyCityNight), height);
                float upper = smoothstep(.015,.19,d.y);
                float2 p = d.xz / (max(d.y,0) + .24) * 2.8 + float2(_CroustySkyTime * .006, _CroustySkyTime * .002);
                float cloudNoise = Noise(p) * .57 + Noise(p * 2.03 + 7.1) * .28 + Noise(p * 4.11 + 17.7) * .15;
                float cloud = smoothstep(.76 - _CloudCoverage * .4, .91 - _CloudCoverage * .4, cloudNoise) * upper;
                // Only around ninety stars across the upper hemisphere, with small
                // antialiased discs; clouds occlude them and daylight hides them.
                float2 starUV = float2(atan2(d.z,d.x) / 6.2831853 + .5, acos(clamp(d.y,-1,1)) / 3.1415927);
                float2 grid = starUV * float2(160,80), cell = floor(grid);
                float random = Hash(cell + 89.3);
                float2 center = .2 + .6 * float2(Hash(cell + 3.4),Hash(cell + 47.1));
                float radius = lerp(.035,.065,Hash(cell + 21.8));
                float aa = max(fwidth(grid.x),fwidth(grid.y)) * .65;
                float star = (1 - smoothstep(radius,radius + aa,length(frac(grid) - center))) * step(.986,random);
                star *= smoothstep(.72,.98,_CroustyCityNight) * upper * (1 - cloud);
                color += star * half3(.8,.86,1);
                float sunDot = dot(d, normalize(_SunDirection.xyz));
                float disk = smoothstep(.9995,.9998,sunDot) * upper;
                float halo = pow(saturate(sunDot),96) * .12 * upper;
                color += _SunColor.rgb * (disk + halo) * (1 - cloud);
                half3 cloudColor = lerp(half3(.95,.96,.98),half3(.11,.14,.22),_CroustyCityNight);
                cloudColor = lerp(cloudColor * .83, cloudColor, saturate(cloudNoise));
                color = lerp(color,cloudColor,cloud * .88);
                return half4(color,1);
            }
            ENDCG
        }
    }
}
