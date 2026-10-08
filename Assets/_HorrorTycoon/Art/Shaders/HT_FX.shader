// Horror Tycoon — HT_FX (unlit transparente, URP 17)
// Um shader só para os efeitos baratos de atmosfera:
//   - névoa de cômodo não descoberto (noise 3D rolando + dissolve na descoberta);
//   - névoa rasteira (planos grandes, com "buraco" na pegada da casa);
//   - raios de luz falsos (cone aditivo com fade por fresnel e pela distância à base);
//   - fita crepe semitransparente.
// Suaviza a interseção com a cena pela profundidade (soft particles).
// Guia: Art/Direcao_de_Arte_v1.md §5 e §6.
Shader "HorrorTycoon/FX"
{
    Properties
    {
        _BaseColor ("Cor (alpha = opacidade)", Color) = (0.1, 0.11, 0.17, 0.6)
        [Header(Noise)]
        _NoiseScale ("Escala do noise (1/m)", Float) = 0.6
        _NoiseSpeed ("Velocidade (m/s, xyz)", Vector) = (0.03, 0.01, 0.02, 0)
        _NoiseStrength ("Força do noise (0 = liso)", Range(0, 1)) = 0.6
        _NoiseContrast ("Contraste do noise", Range(0.5, 4)) = 1.6
        [Header(Fades)]
        _SoftFade ("Interseção suave (m, 0 = off)", Float) = 0.4
        _HeightFadeBottom ("Alpha na base (Y objeto -0,5)", Range(0, 1)) = 1
        _HeightFadeTop ("Alpha no topo (Y objeto +0,5)", Range(0, 1)) = 1
        _FresnelFade ("Fade por fresnel (0..8)", Range(0, 8)) = 0
        _CameraFade ("Some perto da câmera (m)", Float) = 1.5
        _Dissolve ("Dissolve (0..1)", Range(0, 1)) = 0
        [Header(Buraco retangular XZ (mundo))]
        _HoleRect ("Buraco (minX, minZ, maxX, maxZ)", Vector) = (0, 0, 0, 0)
        _HoleSoft ("Borda do buraco (m)", Float) = 1
        [Header(Render)]
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        [Toggle] _ApplyFog ("Aplicar névoa", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "FX"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend], Zero One
            ZWrite Off
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex FxVert
            #pragma fragment FxFrag
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _NoiseScale;
                float4 _NoiseSpeed;
                half _NoiseStrength;
                half _NoiseContrast;
                float _SoftFade;
                half _HeightFadeBottom;
                half _HeightFadeTop;
                half _FresnelFade;
                float _CameraFade;
                half _Dissolve;
                float4 _HoleRect;
                float _HoleSoft;
                half _SrcBlend;
                half _DstBlend;
                half _Cull;
                half _ApplyFog;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3  normalWS   : TEXCOORD1;
                float4 screenPos  : TEXCOORD2;
                half2  heightFog  : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ---- Value noise 3D barato (2 oitavas)
            float Hash3(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float Noise3(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(Hash3(i + float3(0, 0, 0)), Hash3(i + float3(1, 0, 0)), f.x),
                                 lerp(Hash3(i + float3(0, 1, 0)), Hash3(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(Hash3(i + float3(0, 0, 1)), Hash3(i + float3(1, 0, 1)), f.x),
                                 lerp(Hash3(i + float3(0, 1, 1)), Hash3(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            float Fbm(float3 p)
            {
                return Noise3(p) * 0.65 + Noise3(p * 2.13 + 7.1) * 0.35;
            }

            Varyings FxVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.screenPos = ComputeScreenPos(pos.positionCS);
                o.heightFog.x = lerp(_HeightFadeBottom, _HeightFadeTop, saturate(input.positionOS.y + 0.5));
                o.heightFog.y = ComputeFogFactor(pos.positionCS.z);
                return o;
            }

            half4 FxFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 p = input.positionWS;
                half alpha = _BaseColor.a * input.heightFog.x;

                // Noise rolando no mundo.
                float n = Fbm(p * _NoiseScale + _NoiseSpeed.xyz * _Time.y * _NoiseScale);
                half nn = saturate((n - 0.5) * _NoiseContrast + 0.5);
                alpha *= lerp(1.0h, nn * 1.6h, _NoiseStrength);

                // Dissolve com borda de noise (descoberta do cômodo).
                half d = _Dissolve * 1.25h;
                alpha *= saturate((n + 0.25h - d) * 5.0h);

                // Fresnel (raios de luz): some quando a superfície fica de lado.
                half3 V = SafeNormalize(GetWorldSpaceViewDir(p));
                half facing = abs(dot(normalize(input.normalWS), V));
                alpha *= _FresnelFade > 0.0h ? pow(facing, _FresnelFade) : 1.0h;

                // Buraco retangular (ex.: a névoa rasteira não entra na casa).
                if (_HoleRect.z > _HoleRect.x)
                {
                    float2 inside = min(p.xz - _HoleRect.xy, _HoleRect.zw - p.xz);
                    float dist = min(inside.x, inside.y); // > 0 dentro do retângulo
                    alpha *= saturate(-dist / max(_HoleSoft, 1e-3));
                }

                // Soft particles: some ao encostar na geometria opaca.
                float2 uv = input.screenPos.xy / input.screenPos.w;
                float sceneZ = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                float fragZ = LinearEyeDepth(input.positionCS.z, _ZBufferParams);
                if (unity_OrthoParams.w > 0.5)
                {
                    // Ortográfica (monitor do diretor): profundidade linear direta.
                    float rawS = SampleSceneDepth(uv);
                    float rawF = input.positionCS.z;
                    #if UNITY_REVERSED_Z
                        rawS = 1.0 - rawS; rawF = 1.0 - rawF;
                    #endif
                    sceneZ = lerp(_ProjectionParams.y, _ProjectionParams.z, rawS);
                    fragZ = lerp(_ProjectionParams.y, _ProjectionParams.z, rawF);
                }
                if (_SoftFade > 0.0) alpha *= saturate((sceneZ - fragZ) / _SoftFade);
                alpha *= saturate((fragZ - _ProjectionParams.y) / max(_CameraFade, 1e-3));

                half3 color = _BaseColor.rgb;
                if (_ApplyFog > 0.5h)
                {
                    // Aditivo: a névoa apaga o efeito em vez de pintar com a cor dela.
                    half fogKeep = MixFogColor(half3(1, 1, 1), half3(0, 0, 0), input.heightFog.y).r;
                    if (_DstBlend == 1.0h) alpha *= fogKeep;
                    else color = MixFog(color, input.heightFog.y);
                }
                return half4(color, saturate(alpha));
            }
            ENDHLSL
        }
    }

    FallBack Off
}
