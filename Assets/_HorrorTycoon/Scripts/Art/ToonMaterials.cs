using UnityEngine;

namespace HorrorTycoon.Art
{
    /// <summary>
    /// Fábrica de materiais do visual toon (shader HorrorTycoon/Toon e HorrorTycoon/FX).
    /// Um lugar só para os "presets" do guia de arte (§7):
    ///   - Ambiente: rampa com borda 0,04, gradiente na base, SEM casca de contorno (o contorno do
    ///     ambiente vem do renderer feature de detecção de bordas).
    ///   - Personagem: borda 0,02, rim de lua, casca de 2,5 px na cor base × 0,25.
    ///   - Emissivo: olhos, lâmpadas, telas (passam do limiar do bloom).
    /// Usado em runtime (FurnitureKit, RoomAnchor, ActorView) e no editor (construtores de cena).
    /// Se o shader não for encontrado (build sem o shader incluído), cai no URP/Lit.
    /// </summary>
    public static class ToonMaterials
    {
        public const string ToonShaderName = "HorrorTycoon/Toon";
        public const string FxShaderName = "HorrorTycoon/FX";
        public const string OutlinePass = "SRPDefaultUnlit";

        public static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        public static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        public static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
        public static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        public static readonly int OutlineFromBaseId = Shader.PropertyToID("_OutlineFromBase");
        public static readonly int DissolveId = Shader.PropertyToID("_Dissolve");

        /// <summary>Contorno do ator selecionado (guia §7): 3,5 px, fita crepe #F5C542.</summary>
        public static readonly Color SelectedOutline = new Color(0.961f, 0.773f, 0.259f, 1f);
        public const float SelectedOutlineWidth = 3.5f;
        public const float CharacterOutlineWidth = 2.5f;

        /// <summary>Força do ambiente (gradiente céu/equador/chão). Calibrado por screenshot: 1,5 deixa o cenário em ~20–45% de valor.</summary>
        public const float AmbientStrength = 1.5f;
        /// <summary>Ganho das lâmpadas em degraus (poças de luz).</summary>
        public const float PointGain = 0.8f;
        /// <summary>Personagens passam perto das lâmpadas (cabeça a ~1 m do bulbo): ganho menor para o rosto não "estourar".</summary>
        public const float CharacterPointGain = 0.45f;

        private static Shader toon;
        private static Shader fx;

        public static Shader Toon
        {
            get
            {
                if (toon == null) toon = Shader.Find(ToonShaderName);
                return toon;
            }
        }

        public static Shader Fx
        {
            get
            {
                if (fx == null) fx = Shader.Find(FxShaderName);
                return fx;
            }
        }

        public static bool IsToon(Material m) => m != null && m.shader != null && m.shader.name == ToonShaderName;

        /// <summary>Material novo de AMBIENTE (cor chapada, sem casca).</summary>
        public static Material NewEnvironment(Color color, float gradient = 0.15f)
        {
            var m = Create();
            ApplyEnvironment(m, color, gradient);
            return m;
        }

        /// <summary>Preset de ambiente num material existente (troca o shader se preciso, mantém nada além da cor dada).</summary>
        public static void ApplyEnvironment(Material m, Color color, float gradient = 0.15f)
        {
            EnsureToon(m);
            m.SetColor(BaseColorId, color);
            if (!IsToon(m)) return;
            m.SetFloat("_EdgeSoftness", 0.04f);
            m.SetFloat("_AmbientStrength", AmbientStrength);
            m.SetFloat("_PointGain", PointGain);
            m.SetFloat("_GradientStrength", gradient);
            m.SetFloat("_GradientScale", 1f);
            m.SetFloat("_GradientOffset", 0.5f);
            SetKeyword(m, "_RIM", "_UseRim", false);
            SetKeyword(m, "_SPECULAR", "_UseSpecular", false);
            m.SetColor(EmissionColorId, Color.black);
            m.SetShaderPassEnabled(OutlinePass, false);
            m.enableInstancing = true;
        }

        /// <summary>Material novo de PERSONAGEM (rim de lua + casca colorida).</summary>
        public static Material NewCharacter(Color color, float outlinePx = CharacterOutlineWidth)
        {
            var m = Create();
            ApplyCharacter(m, color, outlinePx);
            return m;
        }

        /// <summary>
        /// Preset de personagem. 'gradientScale' 0,5 serve para a cápsula da Unity (Y de -1 a 1);
        /// para modelos do Blender (origem nos pés, 1,75 m), use ~0,6 e offset 0.
        /// </summary>
        public static void ApplyCharacter(Material m, Color color, float outlinePx = CharacterOutlineWidth,
                                          float gradientScale = 0.5f, float gradientOffset = 0.5f)
        {
            EnsureToon(m);
            m.SetColor(BaseColorId, color);
            if (!IsToon(m)) return;
            m.SetFloat("_EdgeSoftness", 0.02f);
            m.SetFloat("_AmbientStrength", AmbientStrength);
            m.SetFloat("_PointGain", CharacterPointGain);
            m.SetFloat("_GradientStrength", 0.1f);
            m.SetFloat("_GradientScale", gradientScale);
            m.SetFloat("_GradientOffset", gradientOffset);
            SetKeyword(m, "_RIM", "_UseRim", true);
            m.SetFloat("_RimStrength", 0.4f);
            m.SetFloat("_RimPower", 3.5f);
            m.SetFloat(OutlineWidthId, outlinePx);
            m.SetFloat(OutlineFromBaseId, 1f);
            m.SetColor(EmissionColorId, Color.black);
            m.SetShaderPassEnabled(OutlinePass, outlinePx > 0f);
            m.enableInstancing = true;
        }

        /// <summary>Peça que brilha (olhos, lâmpada, tela). Emissão em HDR passa do limiar do bloom (1,0).</summary>
        public static void ApplyEmissive(Material m, Color baseColor, Color emission)
        {
            ApplyEnvironment(m, baseColor, 0f);
            if (!IsToon(m)) return;
            m.SetColor(EmissionColorId, emission);
        }

        public static Material NewEmissive(Color baseColor, Color emission)
        {
            var m = Create();
            ApplyEmissive(m, baseColor, emission);
            return m;
        }

        /// <summary>Liga/desliga o contorno de "ator selecionado" sem trocar o material (MaterialPropertyBlock).</summary>
        public static void SetSelectedOutline(Renderer r, bool on)
        {
            if (r == null) return;
            var block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            if (on)
            {
                block.SetFloat(OutlineWidthId, SelectedOutlineWidth);
                block.SetColor(OutlineColorId, SelectedOutline);
                block.SetFloat(OutlineFromBaseId, 0f);
                r.SetPropertyBlock(block);
            }
            else
            {
                // Sem bloco: volta a valer o material (e o renderer volta para o SRP Batcher).
                r.SetPropertyBlock(null);
            }
        }

        /// <summary>
        /// Liga/desliga o BURACO DE VISÃO no material (keyword _SEETHROUGH): paredes, batentes, janelas, beirais e
        /// móveis. Pisos, chão e personagens NUNCA. Só mexe se mudar (não suja assets à toa).
        /// </summary>
        public static void SetSeeThrough(Material m, bool on)
        {
            if (m == null || !IsToon(m)) return;
            bool current = m.IsKeywordEnabled("_SEETHROUGH");
            if (current == on && Mathf.Approximately(m.GetFloat("_SeeThrough"), on ? 1f : 0f)) return;
            m.SetFloat("_SeeThrough", on ? 1f : 0f);
            if (on) m.EnableKeyword("_SEETHROUGH");
            else m.DisableKeyword("_SEETHROUGH");
        }

        /// <summary>Paredes: true (topo e faixa fina de cima nunca recortam). Beiral e móveis: false.</summary>
        public static void SetSeeThroughKeepTop(Material m, bool keep)
        {
            if (m == null || !IsToon(m)) return;
            if (Mathf.Approximately(m.GetFloat("_SeeThroughKeepTop"), keep ? 1f : 0f)) return;
            m.SetFloat("_SeeThroughKeepTop", keep ? 1f : 0f);
        }

        private static Material Create()
        {
            Shader s = Toon != null ? Toon : Shader.Find("Universal Render Pipeline/Lit");
            return new Material(s);
        }

        private static void EnsureToon(Material m)
        {
            if (m == null) return;
            if (Toon != null && m.shader != Toon) m.shader = Toon;
        }

        private static void SetKeyword(Material m, string keyword, string toggleProperty, bool on)
        {
            m.SetFloat(toggleProperty, on ? 1f : 0f);
            if (on) m.EnableKeyword(keyword);
            else m.DisableKeyword(keyword);
        }
    }
}
