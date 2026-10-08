// Horror Tycoon — contorno de ambiente por detecção de bordas (tela cheia)
// Roberts Cross em profundidade LINEAR (limiar relativo à distância, para a teleobjetiva)
// e em normais. A linha some com a névoa e com a distância (o fundo não vira ruído).
// Usado pelo renderer feature HT_EdgeOutlineFeature (Render Graph). Guia §7 e §8.1.
Shader "Hidden/HorrorTycoon/EdgeOutline"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend Off

        Pass
        {
            Name "EdgeOutline"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment EdgeFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            float4 _EdgeColor;        // rgb, a = opacidade
            float4 _EdgeParams;       // x = espessura (px), y = limiar de profundidade (relativo), z = limiar de normal, w = distância máxima (m)
            float4 _EdgeFade;         // x = início do fade por distância (m), y = força do fade por névoa, z = densidade da névoa (exp²)

            float LinearDepthAt(float2 uv)
            {
                float raw = SampleSceneDepth(uv);
                if (unity_OrthoParams.w > 0.5)
                {
                    #if UNITY_REVERSED_Z
                        raw = 1.0 - raw;
                    #endif
                    return lerp(_ProjectionParams.y, _ProjectionParams.z, raw);
                }
                return LinearEyeDepth(raw, _ZBufferParams);
            }

            half4 EdgeFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                float2 texel = _BlitTexture_TexelSize.xy * _EdgeParams.x * 0.5;
                float2 uv0 = uv + float2(-texel.x, -texel.y);
                float2 uv1 = uv + float2( texel.x,  texel.y);
                float2 uv2 = uv + float2( texel.x, -texel.y);
                float2 uv3 = uv + float2(-texel.x,  texel.y);

                float d0 = LinearDepthAt(uv0);
                float d1 = LinearDepthAt(uv1);
                float d2 = LinearDepthAt(uv2);
                float d3 = LinearDepthAt(uv3);
                float dc = min(min(d0, d1), min(d2, d3));

                // Céu / plano distante: sem linha.
                if (dc > _EdgeParams.w) return color;

                float depthEdge = sqrt((d1 - d0) * (d1 - d0) + (d3 - d2) * (d3 - d2)) / max(dc, 0.01);
                depthEdge = smoothstep(_EdgeParams.y, _EdgeParams.y * 1.6, depthEdge);

                float3 n0 = SampleSceneNormals(uv0);
                float3 n1 = SampleSceneNormals(uv1);
                float3 n2 = SampleSceneNormals(uv2);
                float3 n3 = SampleSceneNormals(uv3);
                float normalEdge = sqrt(dot(n1 - n0, n1 - n0) + dot(n3 - n2, n3 - n2));
                normalEdge = smoothstep(_EdgeParams.z, _EdgeParams.z * 1.5, normalEdge);

                float edge = max(depthEdge, normalEdge);

                // Fade: distância e névoa (exp², a mesma conta do built-in).
                float distFade = 1.0 - saturate((dc - _EdgeFade.x) / max(1.0, _EdgeParams.w - _EdgeFade.x));
                float fogF = _EdgeFade.z * dc;
                float fogFade = lerp(1.0, saturate(exp2(-fogF * fogF)), _EdgeFade.y);
                edge *= distFade * fogFade * _EdgeColor.a;

                color.rgb = lerp(color.rgb, _EdgeColor.rgb, edge);
                return color;
            }
            ENDHLSL
        }
    }
}
