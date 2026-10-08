// HUD nova (UI Toolkit): compõe a RenderTexture do painel na tela (HudRoot, correção de cor URP + HDR).
// A textura guarda as cores já em espaço gamma (pré-multiplicadas pelo alfa). Se o destino converte para
// sRGB na escrita, o HudRoot liga _Linearize e elas voltam para linear antes; senão vão cruas.
// Resultado: #15121A no USS aparece como #15121A na tela.
Shader "Hidden/HorrorTycoon/HudComposite"
{
    Properties
    {
        _MainTex ("Painel", 2D) = "black" {}
        _Linearize ("Converter para linear", Float) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Overlay" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Off ZWrite Off ZTest Always
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Linearize;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                if (c.a <= 0.0001) return fixed4(0, 0, 0, 0);
                float3 rgb = saturate(c.rgb / c.a);
                if (_Linearize > 0.5) rgb = GammaToLinearSpace(rgb);
                return fixed4(rgb * c.a, c.a);
            }
            ENDCG
        }
    }
}
