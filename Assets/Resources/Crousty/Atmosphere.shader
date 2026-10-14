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
            float CloudField(float2 p)
            {
                // Broad rounded cloud masses with soft, layered edges.
                float2 warp = float2(Noise(p * .45), Noise(p * .45 + 31)) - .5;
                p += warp * 1.4;
                return Noise(p) * .58 + Noise(p * 2.0 + 7.1) * .28
                    + Noise(p * 4.0 + 17.7) * .10 + Noise(p * 8.0) * .04;
            }
            half3 Stars(float2 uv, float scale, float threshold, float strength)
            {
                float2 grid = uv * float2(scale, scale * .5);
                float2 cell = floor(grid);
                float seed = Hash(cell + 89.3);
                float2 center = .18 + .64 * float2(Hash(cell + 3.4), Hash(cell + 47.1));
                float distanceToStar = length(frac(grid) - center);
                float radius = lerp(.025, .075, pow(Hash(cell + 21.8), 3));
                float aa = max(length(fwidth(grid)), .008);
                float core = 1 - smoothstep(radius, radius + aa, distanceToStar);
                // Keep distant pinpoints visible without turning all stars into large discs.
                core *= saturate(radius / aa);
                float glow = exp(-distanceToStar * distanceToStar * 110) * .14;
                float twinkle = .88 + .12 * sin(_CroustySkyTime * 1.3 + seed * 100);
                half3 tint = lerp(half3(.66,.79,1), half3(1,.89,.72), Hash(cell + 11));
                return tint * (core + glow) * step(threshold, seed) * strength * twinkle;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float3 d = normalize(input.direction);
                float height = smoothstep(0,.65,d.y);
                half3 color = lerp(_HorizonColor.rgb, _SkyColor.rgb * lerp(.9,.45,_CroustyCityNight), height);
                float upper = smoothstep(.015,.22,d.y);
                float2 p = d.xz / (max(d.y,0) + .38) * 2.1
                    + float2(_CroustySkyTime * .003, _CroustySkyTime * .001);
                float density = CloudField(p);
                float threshold = lerp(.73, .38, _CloudCoverage);
                float cloud = smoothstep(threshold - .06, threshold + .12, density) * upper;
                float night = smoothstep(.65,.98,_CroustyCityNight);
                float2 starUV = float2(atan2(d.z,d.x) / 6.2831853 + .5, acos(clamp(d.y,-1,1)) / 3.1415927);
                half3 stars = Stars(starUV, 240, .94, .8) + Stars(starUV, 100, .965, 1.6);
                // A restrained, broken band of distant stars behind the pinpoints.
                float band = pow(saturate(1 - abs(dot(d, normalize(float3(.4,.3,.85))))), 18);
                float dust = Noise(d.xz * 16 + d.y * 7) * Noise(d.xy * 29);
                color += (stars + band * dust * half3(.035,.045,.085)) * night * upper * (1 - cloud);
                float sunDot = dot(d, normalize(_SunDirection.xyz));
                float disk = smoothstep(.9995,.9998,sunDot) * upper;
                float halo = pow(saturate(sunDot),96) * .12 * upper;
                color += _SunColor.rgb * (disk + halo) * (1 - cloud);
                float light = saturate(.55 + (density - CloudField(p + float2(.13,.09))) * 3);
                half3 cloudBase = lerp(half3(.68,.79,.89), half3(.055,.075,.13), night);
                half3 cloudTop = lerp(half3(1,.98,.94), half3(.16,.20,.29), night);
                color = lerp(color, lerp(cloudBase, cloudTop, light), cloud * .96);
                return half4(color,1);
            }
            ENDCG
        }
    }
}
