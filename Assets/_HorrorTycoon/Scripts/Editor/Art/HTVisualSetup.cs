using HorrorTycoon.Art;
using HorrorTycoon.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HorrorTycoon.EditorTools
{
    /// <summary>
    /// Ferramentas de EDITOR do visual (guia de arte v1):
    ///   - configura o URP ativo (renderer feature de contorno, nomes das camadas de luz por cômodo);
    ///   - cria/atualiza materiais como assets (toon de ambiente, personagem, emissivo, FX, céu);
    ///   - aplica luz, névoa e pós-processamento da §6.
    /// Chamado pelo GreyboxSceneBuilder; também tem menus próprios em "Horror Tycoon/Visual".
    /// </summary>
    public static class HTVisualSetup
    {
        public const string MaterialsPath = "Assets/_HorrorTycoon/Materials";
        public const string ShaderFolder = "Assets/_HorrorTycoon/Art/Shaders";
        public const string MeshFolder = "Assets/_HorrorTycoon/Art/Meshes";
        public const string EdgeShaderPath = ShaderFolder + "/HT_EdgeOutline.shader";

        // ---- Paleta da §3 (hex do guia)
        public static readonly Color Moon = Hex(0x8CA6FF);
        public static readonly Color AmbientSky = Hex(0x2A3352);
        public static readonly Color AmbientEquator = Hex(0x2A2340);
        public static readonly Color AmbientGround = Hex(0x120E16);
        public static readonly Color FogColor = Hex(0x2B3550);
        public static readonly Color SkyTop = Hex(0x0D1024);
        public static readonly Color GroundMist = Hex(0x4A5A73);
        public static readonly Color RoomMist = Hex(0x1A1D2B);
        public static readonly Color LampWarm = Hex(0xFFB35C);
        public static readonly Color Tape = Hex(0xC9B458);

        public const float MoonIntensity = 0.5f;
        public const float FogDensity = 0.015f;

        /// <summary>Máscara de camada de luz (rendering layer) do slot: bit 1 + slot. Bit 0 = "Default".</summary>
        public static uint SlotLayer(int slot) => 1u << (slot + 1);
        public const uint AllLayers = 0xFFu;

        // ================================================================== URP

        [MenuItem("Horror Tycoon/Visual/Configurar render (URP)")]
        public static void EnsureRendererMenu()
        {
            EnsureRenderer();
            Debug.Log("[Visual] URP configurado (contorno de ambiente + camadas de luz).");
        }

        /// <summary>Garante o renderer feature de contorno no Universal Renderer ativo e nomeia as camadas de luz.</summary>
        public static void EnsureRenderer()
        {
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp == null)
            {
                Debug.LogWarning("[Visual] Nenhum URP ativo: contorno de ambiente não instalado.");
                return;
            }

            var so = new SerializedObject(urp);
            var list = so.FindProperty("m_RendererDataList");
            var lightLayers = so.FindProperty("m_SupportsLightLayers");
            if (lightLayers != null && !lightLayers.boolValue)
            {
                lightLayers.boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            var edgeShader = AssetDatabase.LoadAssetAtPath<Shader>(EdgeShaderPath);
            for (int i = 0; list != null && i < list.arraySize; i++)
            {
                var data = list.GetArrayElementAtIndex(i).objectReferenceValue as UniversalRendererData;
                if (data == null) continue;
                HT_EdgeOutlineFeature feature = null;
                foreach (var f in data.rendererFeatures)
                {
                    if (f is HT_EdgeOutlineFeature e) feature = e;
                }
                if (feature == null)
                {
                    feature = ScriptableObject.CreateInstance<HT_EdgeOutlineFeature>();
                    feature.name = "HT Edge Outline";
                    AssetDatabase.AddObjectToAsset(feature, data);
                    data.rendererFeatures.Add(feature);
                    if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId))
                    {
                        var dso = new SerializedObject(data);
                        var map = dso.FindProperty("m_RendererFeatureMap");
                        map.arraySize++;
                        map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                        dso.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
                feature.Shader = edgeShader;
                feature.SetActive(true);
                EditorUtility.SetDirty(feature);
                data.SetDirty();
                EditorUtility.SetDirty(data);
            }

            NameRenderingLayers();
            AssetDatabase.SaveAssets();
        }

        /// <summary>Camadas 1..7 = slots 0..6 (só troca nomes padrão "Light Layer N").</summary>
        private static void NameRenderingLayers()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0) return;
            var so = new SerializedObject(assets[0]);
            var layers = so.FindProperty("m_RenderingLayers");
            if (layers == null) return;
            bool changed = false;
            for (int slot = 0; slot <= 6; slot++)
            {
                int bit = slot + 1;
                if (bit >= layers.arraySize) break;
                var p = layers.GetArrayElementAtIndex(bit);
                string wanted = slot == 0 ? "Slot 0 Corredor" : $"Slot {slot}";
                if (string.IsNullOrEmpty(p.stringValue) || p.stringValue.StartsWith("Light Layer"))
                {
                    p.stringValue = wanted;
                    changed = true;
                }
            }
            if (changed) so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ================================================================== Materiais (assets)

        private static Material LoadOrCreate(string name, Shader shader)
        {
            string path = $"{MaterialsPath}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader != null ? shader : Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (shader != null && mat.shader != shader)
            {
                mat.shader = shader;
            }
            return mat;
        }

        public static Material Environment(string name, Color color, float gradient = 0.15f)
        {
            var m = LoadOrCreate(name, ToonMaterials.Toon);
            ToonMaterials.ApplyEnvironment(m, color, gradient);
            m.SetFloat("_TopBlend", 0f);
            m.SetFloat("_BandHeight", 0f);
            m.SetFloat("_StripeWidth", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Parede: malva empoeirado + rodapé escuro + faixa + topo escuro (o "corte" fica com cara de tinta).</summary>
        public static Material Wall(string name)
        {
            var m = Environment(name, Hex(0x5E5068), 0.18f);
            m.SetFloat("_BandHeight", 0.12f);
            m.SetColor("_BandColor", Hex(0x4A3328));
            m.SetFloat("_StripeY", 0.92f);
            m.SetFloat("_StripeWidth", 0.035f);
            m.SetColor("_StripeColor", Hex(0x7A6A80));
            m.SetColor("_TopColor", Hex(0x1C1726));
            m.SetFloat("_TopBlend", 1f);
            EditorUtility.SetDirty(m);
            return m;
        }

        public static Material Character(string name, Color color, float outlinePx = ToonMaterials.CharacterOutlineWidth)
        {
            var m = LoadOrCreate(name, ToonMaterials.Toon);
            ToonMaterials.ApplyCharacter(m, color, outlinePx);
            EditorUtility.SetDirty(m);
            return m;
        }

        public static Material Emissive(string name, Color baseColor, Color emission)
        {
            var m = LoadOrCreate(name, ToonMaterials.Toon);
            ToonMaterials.ApplyEmissive(m, baseColor, emission);
            EditorUtility.SetDirty(m);
            return m;
        }

        public enum FxBlend { Alpha, Additive }

        public static Material Fx(string name, Color color, FxBlend blend, float noiseScale, Vector3 noiseSpeed, float noiseStrength,
                                  float softFade, float heightBottom = 1f, float heightTop = 1f, float fresnel = 0f, bool twoSided = false)
        {
            var m = LoadOrCreate(name, ToonMaterials.Fx);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_NoiseScale", noiseScale);
            m.SetVector("_NoiseSpeed", noiseSpeed);
            m.SetFloat("_NoiseStrength", noiseStrength);
            m.SetFloat("_NoiseContrast", 1.7f);
            m.SetFloat("_SoftFade", softFade);
            m.SetFloat("_HeightFadeBottom", heightBottom);
            m.SetFloat("_HeightFadeTop", heightTop);
            m.SetFloat("_FresnelFade", fresnel);
            m.SetFloat("_Dissolve", 0f);
            m.SetVector("_HoleRect", Vector4.zero);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", blend == FxBlend.Additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_Cull", twoSided ? (float)CullMode.Off : (float)CullMode.Back);
            m.renderQueue = (int)RenderQueue.Transparent;
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Troca um material URP/Lit existente para o toon de ambiente, mantendo a cor (cores ajustadas à mão continuam).</summary>
        public static void UpgradeToToon(Material m, float gradient = 0.12f)
        {
            if (m == null || ToonMaterials.Toon == null) return;
            if (ToonMaterials.IsToon(m)) return;
            if (m.shader != null && m.shader.name.Contains("Unlit")) return; // brilhos ficam unlit (bloom)
            Color c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
            ToonMaterials.ApplyEnvironment(m, c, gradient);
            m.SetFloat("_GradientScale", 0.5f);
            m.SetFloat("_GradientOffset", 0f);
            EditorUtility.SetDirty(m);
        }

        /// <summary>
        /// Materiais que abrem o BURACO DE VISÃO (keyword _SEETHROUGH do HT_Toon): tudo que forma parede ou fica
        /// pendurado nela. Os mesmos assets servem a casa fixa (P0) e o kit da casa gerada (P2).
        /// Pisos, chão, fundação, caminho, deque e personagens ficam fora.
        /// </summary>
        public static readonly string[] SeeThroughMaterials =
        {
            "M_Parede", "M_Batente", "M_Beiral", "M_Janela_Moldura", "M_Janela_Vidro", "M_Janela_Sotao",
            "M_Tijolo", "M_Chamine_Topo", "M_Porta", "M_Latao", "M_Coluna", "M_Luminaria",
        };

        public static void ApplySeeThroughFlags()
        {
            foreach (var name in SeeThroughMaterials)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsPath}/{name}.mat");
                if (m == null) continue;
                ToonMaterials.SetSeeThrough(m, true);
                // Beiral/telhado fica ACIMA da parede e na frente da cabeça do ator: recorta inteiro.
                ToonMaterials.SetSeeThroughKeepTop(m, name != "M_Beiral" && name != "M_Chamine_Topo");
                EditorUtility.SetDirty(m);
            }
            AssetDatabase.SaveAssets();
        }

        // ================================================================== Malhas

        /// <summary>Cone aberto (raio de luz): ápice em y = +0,5 (raio 0,04), base em y = -0,5 (raio 0,5).</summary>
        public static Mesh ConeMesh()
        {
            EnsureFolder("Assets/_HorrorTycoon/Art", "Meshes");
            string path = MeshFolder + "/HT_Cone.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh { name = "HT_Cone" };
            const int seg = 20;
            var v = new Vector3[(seg + 1) * 2];
            var n = new Vector3[v.Length];
            var t = new int[seg * 6];
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                v[i * 2] = d * 0.04f + Vector3.up * 0.5f;
                v[i * 2 + 1] = d * 0.5f + Vector3.down * 0.5f;
                Vector3 nn = (d + Vector3.up * 0.46f).normalized;
                n[i * 2] = nn;
                n[i * 2 + 1] = nn;
            }
            for (int i = 0; i < seg; i++)
            {
                int k = i * 6, a0 = i * 2, b0 = i * 2 + 1, a1 = i * 2 + 2, b1 = i * 2 + 3;
                t[k] = a0; t[k + 1] = a1; t[k + 2] = b0;
                t[k + 3] = b0; t[k + 4] = a1; t[k + 5] = b1;
            }
            mesh.Clear();
            mesh.vertices = v;
            mesh.normals = n;
            mesh.triangles = t;
            mesh.RecalculateBounds();
            if (isNew) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        /// <summary>Salva uma caixa chanfrada como asset (para objetos da cena montados no editor).</summary>
        public static Mesh BevelAsset(Vector3 size, float bevel, float taper = 0f)
        {
            EnsureFolder("Assets/_HorrorTycoon/Art", "Meshes");
            string name = $"HT_Bevel_{Mathf.RoundToInt(size.x * 100)}x{Mathf.RoundToInt(size.y * 100)}x{Mathf.RoundToInt(size.z * 100)}_b{Mathf.RoundToInt(bevel * 1000)}_t{Mathf.RoundToInt(taper * 100)}";
            string path = $"{MeshFolder}/{name}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null) return mesh;
            mesh = BevelMesh.Build(size, bevel, taper);
            mesh.name = name;
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        // ================================================================== Luz, névoa, céu

        public static Material SkyMaterial(Vector3 moonDirection)
        {
            var m = LoadOrCreate("M_Ceu", Shader.Find("HorrorTycoon/Sky"));
            m.SetColor("_TopColor", SkyTop);
            m.SetColor("_HorizonColor", FogColor);
            m.SetColor("_GroundColor", Hex(0x161B2A));
            m.SetVector("_MoonDirection", moonDirection.normalized);
            m.SetFloat("_StarDensity", 0.07f);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Ambiente em gradiente, névoa exp² clara e céu (guia §6).</summary>
        public static void ApplyEnvironmentLighting(Light moon)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = AmbientSky;
            RenderSettings.ambientEquatorColor = AmbientEquator;
            RenderSettings.ambientGroundColor = AmbientGround;
            RenderSettings.ambientIntensity = 1f;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = FogDensity;
            RenderSettings.fogColor = FogColor;

            if (moon != null)
            {
                RenderSettings.sun = moon;
                RenderSettings.skybox = SkyMaterial(-moon.transform.forward);
            }
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.reflectionIntensity = 0f;
        }

        // ================================================================== Pós

        /// <summary>Valores da §6 aplicados SEMPRE no perfil (o perfil é a fonte; ajuste aqui ou no Inspector e rode de novo).</summary>
        public static void ApplyPostProcessing(VolumeProfile profile)
        {
            if (profile == null) return;

            var tone = Get<Tonemapping>(profile);
            tone.mode.Override(TonemappingMode.Neutral);

            var color = Get<ColorAdjustments>(profile);
            color.saturation.Override(-10f);
            color.contrast.Override(15f);
            color.postExposure.Override(0.45f); // desvio do guia: o toon + split toning escurecem; +0,45 devolve o valor 20–45% do cenário

            var split = Get<SplitToning>(profile);
            // Desvio do guia (#3B3A78 / #FFB36B / −20): no URP o split toning é proporcional à saturação da cor,
            // e os valores do guia afogavam o cenário em azul (S 60–90%). Mesma direção de matiz, menos saturado.
            split.shadows.Override(Hex(0x5A5878));
            split.highlights.Override(Hex(0xF0C08C));
            split.balance.Override(-10f);

            var vignette = Get<Vignette>(profile);
            vignette.intensity.Override(0.35f);
            vignette.smoothness.Override(0.45f);
            vignette.color.Override(Hex(0x0B0A12));

            var grain = Get<FilmGrain>(profile);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.25f);
            grain.response.Override(0.8f);

            var bloom = Get<Bloom>(profile);
            bloom.threshold.Override(1f);
            bloom.intensity.Override(0.5f);
            bloom.scatter.Override(0.6f);

            var ca = Get<ChromaticAberration>(profile);
            ca.intensity.Override(0f);
            var lens = Get<LensDistortion>(profile);
            lens.intensity.Override(0f);

            foreach (var component in profile.components)
            {
                if (component != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(component)) && AssetDatabase.Contains(profile))
                {
                    AssetDatabase.AddObjectToAsset(component, profile);
                }
            }
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }

        private static T Get<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (!profile.TryGet(out T c)) c = profile.Add<T>(true);
            c.active = true;
            return c;
        }

        // ================================================================== Utilidades

        public static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
        }

        public static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
