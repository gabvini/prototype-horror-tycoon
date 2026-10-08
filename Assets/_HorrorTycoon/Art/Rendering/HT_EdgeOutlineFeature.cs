using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace HorrorTycoon.Rendering
{
    /// <summary>
    /// Contorno do AMBIENTE por detecção de bordas (profundidade linear + normais), nativo de Render Graph.
    /// Desenha uma linha fina (1 px a 1080p) cor #1A1420 a 60%, apagando com a névoa e a distância.
    /// Roda antes dos transparentes: a névoa rasteira e os raios de luz ficam por cima da linha.
    /// Os atores têm a própria casca (HT_Toon); aqui eles ganham só a linha fina, o que combina.
    ///
    /// Onde fica: no Universal Renderer ativo (Assets/Settings/PC_Renderer.asset), adicionado pelo
    /// menu "Horror Tycoon/Visual/Configurar render (URP)" ou ao reconstruir a cena.
    /// Guia: Art/Direcao_de_Arte_v1.md §7 (tabela de contorno) e §8.1.
    /// </summary>
    [DisallowMultipleRendererFeature("HT Edge Outline")]
    public class HT_EdgeOutlineFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class Settings
        {
            [Tooltip("Cor da linha (alpha = opacidade). Guia: #1A1420 a 60%.")]
            public Color color = new Color(0.102f, 0.078f, 0.125f, 0.6f);
            [Tooltip("Espessura em pixels (referência 1080p).")]
            [Range(0.5f, 4f)] public float thickness = 1f;
            [Tooltip("Diferença de profundidade RELATIVA à distância que já vira linha (teleobjetiva comprime a profundidade).")]
            [Range(0.005f, 0.5f)] public float depthThreshold = 0.06f;
            [Tooltip("Diferença de normal que vira linha (quinas).")]
            [Range(0.05f, 2f)] public float normalThreshold = 0.5f;
            [Tooltip("Começa a apagar a linha a partir desta distância (m).")]
            public float fadeStart = 22f;
            [Tooltip("Sem linha depois desta distância (m).")]
            public float maxDistance = 60f;
            [Tooltip("Quanto a névoa apaga a linha (0 = nada, 1 = igual à névoa).")]
            [Range(0f, 1f)] public float fogFade = 1f;
            public RenderPassEvent passEvent = RenderPassEvent.BeforeRenderingTransparents;
        }

        public Settings settings = new Settings();
        [SerializeField] private Shader shader;

        private Material material;
        private EdgePass pass;

        public Shader Shader
        {
            get => shader;
            set => shader = value;
        }

        public override void Create()
        {
            if (shader == null) shader = Shader.Find("Hidden/HorrorTycoon/EdgeOutline");
            pass = new EdgePass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (shader == null) return;
            if (material == null) material = CoreUtils.CreateEngineMaterial(shader);
            if (material == null) return;
            pass.renderPassEvent = settings.passEvent;
            pass.Setup(material, settings);
            pass.ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(material);
            material = null;
        }

        private sealed class EdgePass : ScriptableRenderPass
        {
            private static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
            private static readonly int EdgeParamsId = Shader.PropertyToID("_EdgeParams");
            private static readonly int EdgeFadeId = Shader.PropertyToID("_EdgeFade");

            private Material material;
            private Settings settings;

            private sealed class PassData
            {
                public TextureHandle source;
                public Material material;
            }

            public EdgePass()
            {
                requiresIntermediateTexture = true;
            }

            public void Setup(Material mat, Settings s)
            {
                material = mat;
                settings = s;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (material == null || settings == null) return;
                var resourceData = frameData.Get<UniversalResourceData>();
                var cameraData = frameData.Get<UniversalCameraData>();
                if (cameraData.cameraType == CameraType.Preview || cameraData.cameraType == CameraType.Reflection) return;
                if (resourceData.isActiveTargetBackBuffer) return;

                // Espessura constante na tela (referência 1080p).
                float px = settings.thickness * Mathf.Max(0.5f, cameraData.cameraTargetDescriptor.height / 1080f);
                float fogDensity = RenderSettings.fog ? RenderSettings.fogDensity : 0f;
                material.SetColor(EdgeColorId, settings.color);
                material.SetVector(EdgeParamsId, new Vector4(px, settings.depthThreshold, settings.normalThreshold, settings.maxDistance));
                material.SetVector(EdgeFadeId, new Vector4(settings.fadeStart, settings.fogFade, fogDensity * 1.2011224f, 0f));

                TextureHandle source = resourceData.activeColorTexture;
                TextureDesc desc = renderGraph.GetTextureDesc(source);
                desc.name = "_HT_EdgeOutlineColor";
                desc.clearBuffer = false;
                TextureHandle destination = renderGraph.CreateTexture(desc);

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("HT Edge Outline", out var data))
                {
                    data.source = source;
                    data.material = material;
                    builder.UseTexture(source, AccessFlags.Read);
                    builder.UseAllGlobalTextures(true); // _CameraDepthTexture e _CameraNormalsTexture
                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                    builder.SetRenderFunc((PassData d, RasterGraphContext ctx) =>
                    {
                        Blitter.BlitTexture(ctx.cmd, d.source, new Vector4(1f, 1f, 0f, 0f), d.material, 0);
                    });
                }

                resourceData.cameraColor = destination;
            }
        }
    }
}
