// Horror Tycoon — HT_Toon (URP 17 / Unity 6.3, Forward+ e Forward)
// Toon "massinha": rampa de 3 degraus sobre half-Lambert, sombra violeta (nunca preta),
// luzes pontuais em degraus (poça de lâmpada com borda de desenho), rim de lua (personagens),
// casca de contorno (passe "Outline", LightMode SRPDefaultUnlit), névoa (MixFog),
// ShadowCaster + DepthOnly + DepthNormals (para SSAO e para o contorno de ambiente).
//
// Ambiente: o passe de casca fica DESLIGADO por material (Material.SetShaderPassEnabled("SRPDefaultUnlit", false)).
// Personagem: _RIM ligado, casca 2,5 px, borda da rampa 0,02.
// Guia: Art/Direcao_de_Arte_v1.md §7 e §8.1.
//
// BURACO DE VISÃO (estilo Baldur's Gate 3, keyword _SEETHROUGH por material): paredes, batentes, janelas, beirais e
// móveis recortam um círculo com borda de pincel em volta dos atores que estão ESCONDIDOS atrás deles.
// Alvos/raios vêm de globais (_HT_SeeThrough*, componente SeeThroughTargets). Só recorta o que está NA FRENTE do
// alvo (teste de profundidade), nunca acima de uma altura (a faixa de cima da parede fica de pé = linha da planta)
// nem rente ao chão (tapetes, fitas). Mesmo recorte em ForwardToon, Outline, DepthOnly e DepthNormals;
// ShadowCaster NÃO recorta (a sombra da parede continua). Docs/Tecnico/SeeThrough_Oclusao.md.
Shader "HorrorTycoon/Toon"
{
    Properties
    {
        [Header(Cor)]
        [MainTexture] _BaseMap ("Textura (paleta, opcional)", 2D) = "white" {}
        [MainColor] _BaseColor ("Cor base", Color) = (1, 1, 1, 1)

        [Header(Rampa toon)]
        _ShadeTint ("Meio-tom (multiplica)", Color) = (0.722, 0.682, 0.859, 1)
        _ShadowTint ("Sombra (multiplica)", Color) = (0.357, 0.306, 0.549, 1)
        _MidThreshold ("Limiar meio-tom", Range(0, 1)) = 0.55
        _ShadowThreshold ("Limiar sombra", Range(0, 1)) = 0.30
        _EdgeSoftness ("Suavidade da borda", Range(0.001, 0.2)) = 0.04
        _AmbientStrength ("Força do ambiente", Range(0, 3)) = 1.5
        _ShadowStrength ("Força da sombra projetada", Range(0, 1)) = 1

        [Header(Gradiente vertical (sujeira na base))]
        _GradientStrength ("Escurece a base em", Range(0, 0.6)) = 0.15
        _GradientScale ("Escala Y (objeto)", Float) = 1
        _GradientOffset ("Offset Y (objeto)", Float) = 0.5

        [Header(Faixas no mundo (paredes))]
        _TopColor ("Cor do topo (faces para cima)", Color) = (0.16, 0.13, 0.2, 1)
        _TopBlend ("Mistura do topo", Range(0, 1)) = 0
        _BandColor ("Rodapé", Color) = (0.29, 0.2, 0.157, 1)
        _BandHeight ("Altura do rodapé (m, 0 = off)", Float) = 0
        _StripeColor ("Faixa", Color) = (0.478, 0.416, 0.502, 1)
        _StripeY ("Altura da faixa (m)", Float) = 0.9
        _StripeWidth ("Meia-largura da faixa (m, 0 = off)", Float) = 0

        [Header(Luzes pontuais em degraus)]
        _PointGain ("Ganho das luzes pontuais", Range(0, 2)) = 0.8
        _PointHigh ("Limiar 100% (1 - d/alcance)", Range(0, 1)) = 0.5
        _PointLow ("Limiar 45%", Range(0, 1)) = 0.16
        _PointLowLevel ("Nível do degrau baixo", Range(0, 1)) = 0.45
        _PointBackFace ("Faces de costas para a lâmpada", Range(0, 1)) = 0.15

        [Header(Rim de lua (personagens))]
        [Toggle(_RIM)] _UseRim ("Rim", Float) = 0
        _RimColor ("Cor do rim", Color) = (0.549, 0.651, 1, 1)
        _RimStrength ("Força do rim", Range(0, 2)) = 0.4
        _RimPower ("Potência do rim", Range(0.5, 8)) = 3.5
        _RimThreshold ("Corte do rim", Range(0, 1)) = 0.22

        [Header(Especular duro)]
        [Toggle(_SPECULAR)] _UseSpecular ("Especular", Float) = 0
        _SpecThreshold ("Limiar (Blinn)", Range(0.5, 1)) = 0.92
        _SpecularColor ("Cor do brilho", Color) = (1, 1, 1, 1)

        [Header(Emissao)]
        [HDR] _EmissionColor ("Emissão", Color) = (0, 0, 0, 1)

        [Header(Contorno (casca invertida))]
        _OutlineWidth ("Largura (px a 1080p)", Range(0, 10)) = 2.5
        _OutlineColor ("Cor do contorno", Color) = (0.043, 0.039, 0.07, 1)
        _OutlineFromBase ("Usar base x 0,25 (0..1)", Range(0, 1)) = 1

        [Header(Buraco de visao)]
        [Toggle(_SEETHROUGH)] _SeeThrough ("Recortável (buraco em volta do ator escondido)", Float) = 0
        [ToggleUI] _SeeThroughKeepTop ("Mantém o topo (linha da planta)", Float) = 1

        [Header(Render)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    // Um único CBUFFER igual em todos os passes = compatível com o SRP Batcher.
    CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST;
        half4 _BaseColor;
        half4 _ShadeTint;
        half4 _ShadowTint;
        half _MidThreshold;
        half _ShadowThreshold;
        half _EdgeSoftness;
        half _AmbientStrength;
        half _ShadowStrength;
        half _GradientStrength;
        float _GradientScale;
        float _GradientOffset;
        half4 _TopColor;
        half _TopBlend;
        half4 _BandColor;
        float _BandHeight;
        half4 _StripeColor;
        float _StripeY;
        float _StripeWidth;
        half _PointGain;
        half _PointHigh;
        half _PointLow;
        half _PointLowLevel;
        half _PointBackFace;
        half4 _RimColor;
        half _RimStrength;
        half _RimPower;
        half _RimThreshold;
        half _SpecThreshold;
        half4 _SpecularColor;
        half4 _EmissionColor;
        float _OutlineWidth;
        half4 _OutlineColor;
        half _OutlineFromBase;
        half _Cull;
        half _SeeThrough;
        half _SeeThroughKeepTop;
    CBUFFER_END

    TEXTURE2D(_BaseMap);
    SAMPLER(sampler_BaseMap);

    // ---------------------------------------------------------------- Buraco de visão (globais: fora do CBUFFER,
    // o SRP Batcher continua valendo). O array tem SEMPRE 8 posições (o tamanho trava no primeiro envio).
    float4 _HT_SeeThroughTargets[8];  // xyz = alvo (peito, mundo); w = raio em metros × abertura (0 = fechado)
    float _HT_SeeThroughCount;        // quantos alvos válidos
    float _HT_SeeThroughOn;           // 0/1 por câmera (monitor do diretor e Scene View = 0)
    float4 _HT_SeeThroughParams;      // x = viés de profundidade (m), y = altura acima da qual nunca recorta (m),
                                      // z = ruído da borda (fração do raio), w = escala do ruído (ciclos por raio)
    float4 _HT_SeeThroughInk;         // rgb = cor da tinta; a = largura da tinta (px a 1080p)
    float4 _HT_SeeThroughParams2;     // x = largura do escurecimento (px a 1080p), y = força do escurecimento,
                                      // z = raio mínimo (px a 1080p)

    float HT_STHash(float2 p)
    {
        p = frac(p * float2(123.34, 456.21));
        p += dot(p, p + 45.32);
        return frac(p.x * p.y);
    }

    float HT_STNoise(float2 p)
    {
        float2 i = floor(p);
        float2 f = frac(p);
        f = f * f * (3.0 - 2.0 * f);
        float a = HT_STHash(i), b = HT_STHash(i + float2(1, 0)), c = HT_STHash(i + float2(0, 1)), d = HT_STHash(i + float2(1, 1));
        return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
    }

    // Distância (px a 1080p) do fragmento até a borda do buraco mais próximo (negativa = dentro).
    // Descarta o fragmento se estiver dentro de um buraco. 1e5 = nenhum buraco perto.
    float HT_SeeThroughClip(float3 positionWS, float3 normalWS)
    {
    #if defined(_SEETHROUGH)
        if (_HT_SeeThroughOn < 0.5 || _HT_SeeThroughCount < 0.5) return 1e5;
        // Rente ao chão (tapete, fita) nunca recorta. Paredes (_SeeThroughKeepTop): tudo acima de uma altura (a face de
        // cima e uma faixa fina logo abaixo) fica de pé = a "linha da planta" continua legível. Beiral/móveis: sem essa regra.
        if (positionWS.y < 0.06) return 1e5;
        if (_SeeThroughKeepTop > 0.5 && positionWS.y > _HT_SeeThroughParams.y) return 1e5;

        float4 fragClip = TransformWorldToHClip(positionWS);
        float2 fragNdc = fragClip.xy / fragClip.w;
        float fragDepth = -TransformWorldToView(positionWS).z;
        float aspect = _ScreenParams.x / _ScreenParams.y;
        float halfH = _ScreenParams.y * 0.5;
        float pxScale = 1080.0 / _ScreenParams.y; // medidas em px "a 1080p"
        bool ortho = unity_OrthoParams.w > 0.5;
        float p11 = abs(UNITY_MATRIX_P[1][1]);

        float best = 1e5;
        int count = (int)min(_HT_SeeThroughCount, 8.0);
        for (int i = 0; i < count; i++)
        {
            float4 t = _HT_SeeThroughTargets[i];
            if (t.w <= 0.001) continue;
            float tDepth = -TransformWorldToView(t.xyz).z;
            if (tDepth <= 0.05) continue;
            if (fragDepth > tDepth - _HT_SeeThroughParams.x) continue; // só o que está NA FRENTE do alvo

            float4 tc = TransformWorldToHClip(t.xyz);
            float2 tNdc = tc.xy / tc.w;
            float rNdc = t.w * p11 / (ortho ? 1.0 : tDepth);
            rNdc = max(rNdc, _HT_SeeThroughParams2.z / pxScale / halfH * saturate(t.w * 4.0));
            float2 d = fragNdc - tNdc;
            d.x *= aspect;
            float2 dn = d / rNdc;
            float n = HT_STNoise(dn * _HT_SeeThroughParams.w + float2(i * 17.3, i * 5.1)) - 0.5;
            float dist = length(dn) + n * 2.0 * _HT_SeeThroughParams.z;
            best = min(best, (dist - 1.0) * rNdc * halfH * pxScale);
        }
        clip(best);
        return best;
    #else
        return 1e5;
    #endif
    }

    // Borda de pincel: faixa de tinta logo fora do buraco + leve escurecimento (como luz de lanterna).
    half3 HT_SeeThroughInk(half3 color, float edgePx)
    {
    #if defined(_SEETHROUGH)
        if (edgePx >= 1e4) return color;
        half dark = saturate(edgePx / max(1.0, _HT_SeeThroughParams2.x));
        color *= lerp(1.0h - _HT_SeeThroughParams2.y, 1.0h, dark * dark);
        half ink = 1.0h - smoothstep(_HT_SeeThroughInk.a - 0.75, _HT_SeeThroughInk.a + 0.75, edgePx);
        color = lerp(color, _HT_SeeThroughInk.rgb, ink);
    #endif
        return color;
    }
    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
            "IgnoreProjector" = "True"
        }
        LOD 300

        // ------------------------------------------------------------------ Cor (toon)
        Pass
        {
            Name "ForwardToon"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ToonVert
            #pragma fragment ToonFrag

            #pragma shader_feature_local_fragment _RIM
            #pragma shader_feature_local_fragment _SPECULAR
            #pragma shader_feature_local_fragment _SEETHROUGH

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3  normalWS   : TEXCOORD2;
                half2  gradFog    : TEXCOORD3; // x = gradiente vertical, y = fator de névoa
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings ToonVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.gradFog.x = saturate(input.positionOS.y * _GradientScale + _GradientOffset);
                o.gradFog.y = ComputeFogFactor(pos.positionCS.z);
                return o;
            }

            // Degrau suave: 0 abaixo do limiar, 1 acima.
            half Step2(half threshold, half x, half soft)
            {
                return smoothstep(threshold - soft, threshold + soft, x);
            }

            // Uma luz pontual/spot em degraus (poça de luz de desenho).
            half3 PunctualToon(uint loopIndex, float3 positionWS, half3 N, half3 albedo, uint meshLayers)
            {
            #if USE_CLUSTER_LIGHT_LOOP
                int idx = loopIndex;
            #else
                int idx = GetPerObjectLightIndex(loopIndex);
            #endif

            #if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
                float4 lightPos = _AdditionalLightsBuffer[idx].position;
                half3 color = _AdditionalLightsBuffer[idx].color.rgb;
                half4 atten = _AdditionalLightsBuffer[idx].attenuation;
                half4 spotDir = _AdditionalLightsBuffer[idx].spotDirection;
                uint layerMask = _AdditionalLightsBuffer[idx].layerMask;
            #else
                float4 lightPos = _AdditionalLightsPosition[idx];
                half3 color = _AdditionalLightsColor[idx].rgb;
                half4 atten = _AdditionalLightsAttenuation[idx];
                half4 spotDir = _AdditionalLightsSpotDir[idx];
                uint layerMask = asuint(_AdditionalLightsLayerMasks[idx]);
            #endif

            #ifdef _LIGHT_LAYERS
                if (!IsMatchingLightLayer(layerMask, meshLayers)) return 0;
            #endif

                float3 v = lightPos.xyz - positionWS * lightPos.w;
                float d2 = max(dot(v, v), 1e-4);
                half3 L = half3(v * rsqrt(d2));

                // atten.x = 1 / alcance²  ->  f = 1 - d/alcance (linear, 1 no centro, 0 na borda).
                half f = lightPos.w > 0.5 ? half(1.0 - sqrt(saturate(d2 * atten.x))) : 1.0h;
                half e = 0.015h;
                half level = Step2(_PointHigh, f, e) * (1.0h - _PointLowLevel) + Step2(_PointLow, f, e) * _PointLowLevel;

                // Spot: cone com borda dura.
                half spot = AngleAttenuation(spotDir.xyz, L, atten.zw);
                level *= Step2(0.12h, spot, 0.04h);

                half ndl = dot(N, L) * 0.5h + 0.5h;
                half facing = lerp(_PointBackFace, 1.0h, Step2(0.45h, ndl, 0.03h));
                return albedo * color * (level * facing * _PointGain);
            }

            half4 ToonFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 positionWS = input.positionWS;
                half3 N = normalize(input.normalWS);
                float seeThroughEdge = HT_SeeThroughClip(positionWS, N); // buraco de visão (descarta dentro)
                half3 V = SafeNormalize(GetWorldSpaceViewDir(positionWS));

                // ---- Cor base: textura × cor, gradiente, faixas de parede, topo
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                if (_StripeWidth > 0.0 && abs(positionWS.y - _StripeY) < _StripeWidth) albedo = _StripeColor.rgb;
                if (_BandHeight > 0.0 && positionWS.y < _BandHeight) albedo = _BandColor.rgb;
                albedo *= lerp(1.0h - _GradientStrength, 1.0h, input.gradFog.x);
                albedo = lerp(albedo, _TopColor.rgb, _TopBlend * Step2(0.7h, N.y, 0.02h));

                // ---- Dados de luz
                InputData inputData = (InputData)0;
                inputData.positionWS = positionWS;
                inputData.normalWS = N;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
                inputData.shadowCoord = shadowCoord;

                Light mainLight = GetMainLight(shadowCoord, positionWS, half4(1, 1, 1, 1));
                half ao = 1.0h;
                #if defined(_SCREEN_SPACE_OCCLUSION)
                    AmbientOcclusionFactor aoFactor = GetScreenSpaceAmbientOcclusion(inputData.normalizedScreenSpaceUV);
                    ao = aoFactor.indirectAmbientOcclusion;
                #endif
                uint meshLayers = GetMeshRenderingLayer();

                // ---- Rampa de 3 degraus (luz / meio-tom lilás / sombra violeta)
                half NdotL = dot(N, mainLight.direction);
                half halfLambert = NdotL * 0.5h + 0.5h;
                half shadow = lerp(1.0h, mainLight.shadowAttenuation, _ShadowStrength);
                half t = min(halfLambert, lerp(0.0h, 1.0h, shadow));
                half soft = _EdgeSoftness;
                half toMid = Step2(_ShadowThreshold, t, soft);
                half toLit = Step2(_MidThreshold, t, soft);
                half3 ramp = lerp(_ShadowTint.rgb, lerp(_ShadeTint.rgb, half3(1, 1, 1), toLit), toMid);

                half3 color = albedo * ramp * mainLight.color * mainLight.distanceAttenuation;

                // ---- Ambiente (gradiente céu/equador/chão vem como SH)
                color += albedo * SampleSH(N) * (_AmbientStrength * ao);

                // ---- Luzes pontuais em degraus
                #if defined(_ADDITIONAL_LIGHTS)
                    uint pixelLightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        color += PunctualToon(lightIndex, positionWS, N, albedo, meshLayers);
                    LIGHT_LOOP_END
                #endif

                // ---- Especular duro (cabelo, óculos, pupila)
                #if defined(_SPECULAR)
                    half3 H = SafeNormalize(mainLight.direction + V);
                    half spec = Step2(_SpecThreshold, saturate(dot(N, H)), 0.01h) * toLit;
                    color += _SpecularColor.rgb * spec * (mainLight.color + 0.35h);
                #endif

                // ---- Rim de lua (só personagens)
                #if defined(_RIM)
                    half rim = pow(1.0h - saturate(dot(N, V)), _RimPower);
                    rim = Step2(_RimThreshold, rim, 0.04h);
                    half moonSide = Step2(0.35h, halfLambert, 0.1h);
                    color += _RimColor.rgb * (rim * moonSide * _RimStrength);
                #endif

                color += _EmissionColor.rgb;
                color = HT_SeeThroughInk(color, seeThroughEdge);
                color = MixFog(color, input.gradFog.y);
                return half4(color, 1);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------ Casca de contorno
        // Desenhada no mesmo material (o URP desenha "SRPDefaultUnlit" junto com os opacos).
        // Para o ambiente: Material.SetShaderPassEnabled("SRPDefaultUnlit", false).
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex OutlineVert
            #pragma fragment OutlineFrag
            #pragma multi_compile_instancing
            #pragma shader_feature_local_fragment _SEETHROUGH
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderVariablesFunctions.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half   fog        : TEXCOORD1;
                float3 positionWS : TEXCOORD2; // antes da extrusão (o buraco usa a superfície)
                half3  normalWS   : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings OutlineVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float4 positionCS = TransformWorldToHClip(positionWS);
                float3 normalCS = mul((float3x3)GetWorldToHClipMatrix(), normalWS);

                // Extrusão em clip space: largura constante em pixels (referência 1080p).
                float2 dir = normalCS.xy;
                float len = length(dir);
                dir = len > 1e-5 ? dir / len : float2(0, 0);
                float px = _OutlineWidth * (_ScreenParams.y / 1080.0);
                positionCS.xy += dir * (px * 2.0) / _ScreenParams.xy * positionCS.w;

                o.positionCS = positionCS;
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.fog = ComputeFogFactor(positionCS.z);
                o.positionWS = positionWS;
                o.normalWS = normalWS;
                if (_OutlineWidth <= 0.001) o.positionCS = float4(0, 0, -10, 1); // largura 0 = descarta
                return o;
            }

            half4 OutlineFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                HT_SeeThroughClip(input.positionWS, normalize(input.normalWS));
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                half3 c = lerp(_OutlineColor.rgb, albedo * 0.25h, _OutlineFromBase);
                c = MixFog(c, input.fog);
                return half4(c, 1);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------ Sombra
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                o.positionCS = ApplyShadowClamping(positionCS);
                return o;
            }

            half4 ShadowFrag(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }

        // ------------------------------------------------------------------ Profundidade
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            #pragma shader_feature_local_fragment _SEETHROUGH

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
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }

            half DepthFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                HT_SeeThroughClip(input.positionWS, normalize(input.normalWS)); // mesmo recorte da cor
                return input.positionCS.z;
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------ Normais (SSAO + contorno de ambiente)
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex NormalsVert
            #pragma fragment NormalsFrag
            #pragma multi_compile_instancing
            #pragma shader_feature_local_fragment _SEETHROUGH
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3  normalWS   : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings NormalsVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }

            void NormalsFrag(Varyings input, out half4 outNormalWS : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
            #endif
            )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                HT_SeeThroughClip(input.positionWS, normalize(input.normalWS)); // SSAO e contorno enxergam o buraco
            #if defined(_GBUFFER_NORMALS_OCT)
                float3 n = normalize(input.normalWS);
                float2 oct = PackNormalOctQuadEncode(n);
                float2 remapped = saturate(oct * 0.5 + 0.5);
                outNormalWS = half4(PackFloat2To888(remapped), 0.0);
            #else
                outNormalWS = half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
            #endif
            #ifdef _WRITE_RENDERING_LAYERS
                outRenderingLayers = EncodeMeshRenderingLayer();
            #endif
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
