// Horror Tycoon — céu de desenho (skybox procedural, URP 17)
// Gradiente topo → horizonte (horizonte = cor da névoa, para emendar sem costura),
// lua cartunesca com halo e estrelas esparsas que piscam devagar.
// Guia: Art/Direcao_de_Arte_v1.md §3.1 (céu #0D1024 → #1E2A44).
Shader "HorrorTycoon/Sky"
{
    Properties
    {
        _TopColor ("Topo", Color) = (0.051, 0.063, 0.141, 1)
        _HorizonColor ("Horizonte", Color) = (0.169, 0.208, 0.314, 1)
        _GroundColor ("Abaixo do horizonte", Color) = (0.07, 0.08, 0.12, 1)
        _HorizonSharpness ("Curva do gradiente", Range(0.2, 4)) = 1.2
        [Header(Lua)]
        _MoonDirection ("Direção da lua (mundo)", Vector) = (0.4, 0.8, 0.45, 0)
        [HDR] _MoonColor ("Cor da lua", Color) = (1.6, 1.65, 1.5, 1)
        _MoonSize ("Tamanho (cos)", Range(0.98, 0.9999)) = 0.9975
        [HDR] _MoonHaloColor ("Halo", Color) = (0.25, 0.32, 0.6, 1)
        _MoonHaloSize ("Tamanho do halo", Range(1, 64)) = 10
        [Header(Estrelas)]
        _StarDensity ("Densidade", Range(0, 1)) = 0.07
        _StarScale ("Escala da grade", Float) = 70
        [HDR] _StarColor ("Cor", Color) = (1.2, 1.2, 1.4, 1)
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex SkyVert
            #pragma fragment SkyFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _HorizonColor;
                half4 _GroundColor;
                half _HorizonSharpness;
                float4 _MoonDirection;
                half4 _MoonColor;
                float _MoonSize;
                half4 _MoonHaloColor;
                half _MoonHaloSize;
                half _StarDensity;
                float _StarScale;
                half4 _StarColor;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings SkyVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.dir = input.positionOS.xyz;
                return o;
            }

            float Hash2(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            half4 SkyFrag(Varyings input) : SV_Target
            {
                float3 d = normalize(input.dir);
                half up = saturate(d.y);
                half3 col = lerp(_HorizonColor.rgb, _TopColor.rgb, pow(up, 1.0 / _HorizonSharpness));
                col = lerp(col, _GroundColor.rgb, saturate(-d.y * 6.0));

                // Estrelas: uma por célula (no máximo), só acima do horizonte.
                float2 g = float2(atan2(d.x, d.z), d.y) * _StarScale;
                float2 cell = floor(g);
                float h = Hash2(cell);
                if (h < _StarDensity && d.y > 0.12)
                {
                    float2 c = cell + 0.5 + (float2(Hash2(cell + 3.1), Hash2(cell + 7.7)) - 0.5) * 0.6;
                    float r = length(g - c);
                    float tw = 0.65 + 0.35 * sin(_Time.y * (1.0 + h * 9.0) + h * 40.0);
                    col += _StarColor.rgb * smoothstep(0.16, 0.02, r) * tw * saturate((d.y - 0.12) * 4.0);
                }

                // Lua de desenho: disco chapado + halo.
                float3 md = normalize(_MoonDirection.xyz);
                float mdot = dot(d, md);
                col += _MoonHaloColor.rgb * pow(saturate(mdot), _MoonHaloSize * 40.0) * 0.8;
                half disc = smoothstep(_MoonSize - 0.0004, _MoonSize + 0.0002, mdot);
                // "Crateras" de desenho: duas manchas mais escuras.
                float3 right = normalize(cross(md, float3(0, 1, 0)));
                float3 upv = cross(right, md);
                float2 mp = float2(dot(d - md, right), dot(d - md, upv)) / sqrt(max(1e-5, 2.0 * (1.0 - _MoonSize)));
                half crater = smoothstep(0.22, 0.17, length(mp - float2(-0.25, 0.2))) + smoothstep(0.14, 0.1, length(mp - float2(0.3, -0.25)));
                col = lerp(col, _MoonColor.rgb * (1.0 - 0.18 * saturate(crater)), disc);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
