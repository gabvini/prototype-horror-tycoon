using System.Collections.Generic;
using HorrorTycoon.Cameras;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace HorrorTycoon.EditorTools
{
    /// <summary>
    /// CENÁRIO POR CÓDIGO: monta o "lado de fora" da casa (árvores, arbustos, cerca, túmulos,
    /// abóboras, olhos no mato...) e espalha os esconderijos de câmera (CameraVantage).
    ///
    /// Regras:
    ///   - Seed fixa (System.Random): toda reconstrução sai IGUAL. Mude a Seed para outra floresta.
    ///   - Tudo vira poucas malhas: uma malha combinada por material (salva em Art/Cenario/Malhas).
    ///   - Só os TRONCOS têm colisor (cápsulas). Folhas, arbustos e props não têm colisor,
    ///     para o CinematicDirector poder filmar "através" deles.
    ///   - Nenhuma árvore fica na linha de visão esconderijo → casa (o diretor rejeitaria o plano).
    ///   - Materiais são criados só se faltarem (cache): o Gabriel pode ajustar as cores no
    ///     Inspector e reconstruir sem perder. Para voltar às cores do código, use o menu
    ///     "Horror Tycoon/Cenário/Restaurar cores dos materiais".
    ///
    /// Tom: terror EXAGERADO e GOSTOSO, meio desenho animado — árvores tortas, pinheiros
    /// gordinhos de cones empilhados, arbustos redondos, cerca torta, abóboras sorrindo.
    /// </summary>
    public static class SceneryBuilder
    {
        public const int Seed = 1031;
        public const string RootName = "Cenario";

        private const string ArtFolder = "Assets/_HorrorTycoon/Art/Cenario";
        private const string MatFolder = ArtFolder + "/Materiais";
        private const string MeshFolder = ArtFolder + "/Malhas";
        private const string LabScenePath = "Assets/_HorrorTycoon/Scenes/Lab_Cenario.unity";

        /// <summary>
        /// Onde fica a casa e o que o cenário precisa evitar. O PADRÃO são os valores da casa fixa do P0
        /// (exatamente os números de antes: a floresta do P0 sai igual). ForHouse() = casa gerada, com as
        /// mesmas folgas em volta do retângulo MÁXIMO da geração (a casa muda a cada run; o cenário não).
        /// </summary>
        public sealed class SiteLayout
        {
            public Vector3 HouseCenter = new Vector3(0f, 0f, -0.5f);
            /// <summary>Pegada da casa + 1,7 m de folga (nada do cenário entra aqui).</summary>
            public float MinX = -7.2f, MaxX = 7.2f, MinZ = -7.7f, MaxZ = 7.7f;
            /// <summary>Fachada da frente (o caminho começa aqui).</summary>
            public float FrontZ = -6f;
            public float FenceZ = -15f;
            public float FenceClearX = 14.5f;
            public float RoadStartZ = -14f;
            public float RoadBendFrom = -16f, RoadBendTo = -24f;
            /// <summary>Meia largura livre entre a fachada e o portão (caminho).</summary>
            public float PathHalfWidth = 1.8f;
            /// <summary>Contorno que os esconderijos precisam enxergar: x0, x1, z0, z1.</summary>
            public Vector4 SightRect = new Vector4(-5.7f, 5.7f, -9.8f, 6.2f);
            // Arbustos rente à casa (faixas em volta da pegada).
            public float HugXMin = -7.6f, HugXMax = 7.6f;
            public Vector2 HugFrontZ = new Vector2(-8.6f, -8.0f), HugBackZ = new Vector2(8.0f, 8.6f);
            public Vector2 HugWestX = new Vector2(-8.4f, -7.6f), HugEastX = new Vector2(7.6f, 8.4f);
            public Vector2 HugSideZ = new Vector2(-8f, 8f);
            public float HugPathClear = 3f;
            /// <summary>Raio sem árvores em volta do centro.</summary>
            public float TreeClear = 11f;
            /// <summary>Também tira árvores de perto do retângulo (casas grandes, retângulo além do raio).</summary>
            public bool TreeAvoidHouseRect;
            public float RoadViewZ = -8f;
            public Vector2 VantageFront = new Vector2(17.5f, 19f), VantageSide = new Vector2(13.5f, 18.5f);
            public Vector3 Graveyard = new Vector3(11.5f, 0f, 3.5f);
            /// <summary>Deslocamento das abóboras (P0: perto do caminho, na frente da varanda).</summary>
            public Vector3 PropShift = Vector3.zero;
            public float StumpClear = 10f;
            public Vector2 Eyes = new Vector2(20f, 36f);

            /// <summary>
            /// Casa gerada: retângulo máximo de largura 2·halfWidth centrado em x = 0, fachada em frontZ, fundo em frontZ + depth.
            /// Mesmas folgas do P0 (cerca 9 m à frente da fachada, faixas de arbusto, etc.).
            /// </summary>
            public static SiteLayout ForHouse(float halfWidth, float frontZ, float depth)
            {
                float backZ = frontZ + depth;
                float fence = frontZ - 9f;
                var s = new SiteLayout();
                s.HouseCenter = new Vector3(0f, 0f, (frontZ + backZ) * 0.5f - 0.5f);
                s.MinX = -halfWidth - 1.7f;
                s.MaxX = halfWidth + 1.7f;
                s.MinZ = frontZ - 1.7f;
                s.MaxZ = backZ + 1.7f;
                s.FrontZ = frontZ;
                s.FenceZ = fence;
                s.RoadStartZ = fence + 1f;
                s.RoadBendFrom = fence - 1f;
                s.RoadBendTo = fence - 9f;
                // A porta da frente sorteada fica a até ~7 m do meio (22 m de largura) e o caminho sai dela em diagonal
                // até o portão: o quintal da frente inteiro fica livre (varanda com 3,4 m de largura).
                s.PathHalfWidth = Mathf.Min(halfWidth * 0.75f, 8.5f);
                s.SightRect = new Vector4(-halfWidth - 0.2f, halfWidth + 0.2f, frontZ - 3.8f, backZ + 0.2f);
                s.HugXMin = s.MinX - 0.4f;
                s.HugXMax = s.MaxX + 0.4f;
                s.HugFrontZ = new Vector2(s.MinZ - 0.9f, s.MinZ - 0.3f);
                s.HugBackZ = new Vector2(s.MaxZ + 0.3f, s.MaxZ + 0.9f);
                s.HugWestX = new Vector2(s.MinX - 1.2f, s.MinX - 0.4f);
                s.HugEastX = new Vector2(s.MaxX + 0.4f, s.MaxX + 1.2f);
                s.HugSideZ = new Vector2(s.MinZ - 0.3f, s.MaxZ + 0.3f);
                s.HugPathClear = s.PathHalfWidth + 1.2f;
                float radius = new Vector2(s.MaxX, s.MaxZ - s.HouseCenter.z).magnitude;
                s.TreeClear = Mathf.Max(11f, radius - 2f);
                s.TreeAvoidHouseRect = true;
                s.RoadViewZ = frontZ - 2f;
                s.VantageFront = new Vector2(radius + 5f, radius + 6.5f);
                s.VantageSide = new Vector2(radius + 1.5f, radius + 6f);
                s.Graveyard = new Vector3(s.MaxX + 4.3f, 0f, 3.5f);
                s.PropShift = new Vector3(0f, 0f, fence + 1.2f + 8.6f); // abóboras perto do portão
                s.StumpClear = Mathf.Max(10f, radius);
                s.Eyes = new Vector2(Mathf.Max(20f, radius + 6f), Mathf.Max(36f, radius + 22f));
                return s;
            }
        }

        private static SiteLayout Site = new SiteLayout();

        private static Vector3 HouseCenter => Site.HouseCenter;
        private static float HouseMinX => Site.MinX;
        private static float HouseMaxX => Site.MaxX;
        private static float HouseMinZ => Site.MinZ;
        private static float HouseMaxZ => Site.MaxZ;
        private static float FenceZ => Site.FenceZ;

        // ================================================================== Materiais

        private struct MatDef
        {
            public string Key;
            public Color Color;
            public bool Unlit;
            public bool Shadows;
            public float Smooth;

            public MatDef(string key, Color color, bool unlit, bool shadows, float smooth)
            {
                Key = key;
                Color = color;
                Unlit = unlit;
                Shadows = shadows;
                Smooth = smooth;
            }
        }

        // Paleta fria e azulada (a lua faz o resto). Só abóboras e olhos são "quentes"/brilhantes.
        private static readonly MatDef[] MatDefs =
        {
            new MatDef("Casca",     new Color(0.22f, 0.17f, 0.20f), false, true,  0.05f),
            new MatDef("PinheiroA", new Color(0.09f, 0.21f, 0.20f), false, true,  0.05f),
            new MatDef("PinheiroB", new Color(0.10f, 0.16f, 0.23f), false, true,  0.05f),
            new MatDef("ArbustoA",  new Color(0.12f, 0.28f, 0.22f), false, true,  0.10f),
            new MatDef("ArbustoB",  new Color(0.19f, 0.27f, 0.17f), false, true,  0.10f),
            new MatDef("Grama",     new Color(0.15f, 0.27f, 0.20f), false, false, 0.05f),
            new MatDef("Terra",     new Color(0.20f, 0.16f, 0.14f), false, false, 0.02f),
            new MatDef("Pedra",     new Color(0.42f, 0.45f, 0.52f), false, true,  0.15f),
            new MatDef("Madeira",   new Color(0.34f, 0.26f, 0.22f), false, true,  0.05f),
            new MatDef("Correio",   new Color(0.62f, 0.16f, 0.20f), false, true,  0.30f),
            new MatDef("Abobora",   new Color(0.95f, 0.45f, 0.10f), false, true,  0.25f),
            new MatDef("Brilho",    new Color(1.60f, 1.05f, 0.30f), true,  false, 0f),
            new MatDef("Olhos",     new Color(1.80f, 2.00f, 0.55f), true,  false, 0f),
        };

        // ================================================================== Entrada principal

        /// <summary>
        /// Cria todo o cenário como filho de 'parent' (num objeto "Cenario"; se já existir, é refeito).
        /// Coordenadas em metros, com o 'parent' na origem do mundo.
        /// </summary>
        public static void Build(Transform parent) => Build(parent, null);

        /// <summary>Igual ao Build(parent), mas em volta de outra casa (null = casa fixa do P0).</summary>
        public static void Build(Transform parent, SiteLayout site)
        {
            Site = site ?? new SiteLayout();
            try
            {
                BuildInternal(parent);
            }
            finally
            {
                Site = new SiteLayout();
            }
        }

        private static void BuildInternal(Transform parent)
        {
            EnsureFolders();

            Transform old = parent != null ? parent.Find(RootName) : null;
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var root = new GameObject(RootName).transform;
            root.SetParent(parent, false);

            var c = new Ctx(new System.Random(Seed));
            c.Colliders = new GameObject("Colisores_Troncos");
            c.Colliders.transform.SetParent(root, false);

            BuildSightTargets(c);
            PlaceVantages(c, root);            // 1) esconderijos primeiro: o resto respeita a linha de visão
            PlaceProps(c, root);               // 2) cerca, correio, túmulos, abóboras (reservam espaço)
            PlaceTrees(c);                     // 3) floresta
            PlaceBushes(c);                    // 4) arbustos espalhados e rente à casa
            PlaceEyes(c);                      // 5) olhos brilhando no mato
            PlaceGround(c);                    // 6) estrada de terra, manchas e tufos de grama

            int tris = FinalizeMeshes(c, root);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Cenário] Montado (seed {Seed}): {c.Vantages.Count} esconderijos, {c.TreeCount} árvores, " +
                      $"{c.BushCount} arbustos, {c.Trunks} colisores de tronco, {tris} triângulos em {c.MeshObjects} malhas.");
        }

        // ================================================================== Contexto

        private sealed class Ctx
        {
            public readonly System.Random Rng;
            public readonly Dictionary<string, MeshBuilder> Mb = new Dictionary<string, MeshBuilder>();
            public readonly List<Vector3> Vantages = new List<Vector3>();
            public readonly List<Vector2> SightTargets = new List<Vector2>();
            /// <summary>Árvores: (x, raio visual, z).</summary>
            public readonly List<Vector3> Trees = new List<Vector3>();
            /// <summary>Troncos: (x, raio, z).</summary>
            public readonly List<Vector3> TrunkList = new List<Vector3>();
            /// <summary>Áreas reservadas para props: (x, raio, z).</summary>
            public readonly List<Vector3> KeepOut = new List<Vector3>();
            public GameObject Colliders;
            public int TreeCount, BushCount, Trunks, MeshObjects;

            public Ctx(System.Random rng) { Rng = rng; }

            public MeshBuilder M(string key)
            {
                MeshBuilder mb;
                if (!Mb.TryGetValue(key, out mb))
                {
                    mb = new MeshBuilder();
                    Mb[key] = mb;
                }
                return mb;
            }

            public float R(float a, float b) { return a + (float)Rng.NextDouble() * (b - a); }
            public bool Chance(float p) { return Rng.NextDouble() < p; }
            public string Leaf() { return Chance(0.5f) ? "ArbustoA" : "ArbustoB"; }
        }

        // ================================================================== Esconderijos (CameraVantage)

        // Ângulos (graus) em volta da casa: 0 = frente (-Z), 90 = direita (+X).
        private static readonly float[] VantageAngles = { 14f, 44f, 74f, 104f, 134f, 164f, 196f, 226f, 256f, 286f, 316f, 346f };

        private static void PlaceVantages(Ctx c, Transform root)
        {
            var holder = new GameObject("Esconderijos_Camera").transform;
            holder.SetParent(root, false);

            for (int i = 0; i < VantageAngles.Length; i++)
            {
                float ang = (VantageAngles[i] + c.R(-5f, 5f)) * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Sin(ang), 0f, -Mathf.Cos(ang));
                bool front = Mathf.Abs(Mathf.DeltaAngle(VantageAngles[i], 0f)) < 30f;
                float dist = front ? c.R(Site.VantageFront.x, Site.VantageFront.y) : c.R(Site.VantageSide.x, Site.VantageSide.y);
                float height = c.R(1.7f, 3.6f);

                Vector3 pos = HouseCenter + dir * dist + Vector3.up * height;
                Vector3 look = HouseCenter + Vector3.up * 1.2f;

                var go = new GameObject($"Esconderijo_{i + 1:00}");
                go.transform.SetParent(holder, false);
                go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos, Vector3.up));
                go.AddComponent<CameraVantage>();
                c.Vantages.Add(pos);

                VantageFoliage(c, pos, dist);
            }
        }

        /// <summary>
        /// Folhagem NA FRENTE da lente (entre o esconderijo e a casa): o topo fica logo abaixo da
        /// linha de visão (folhas na borda de baixo do quadro) e dois raminhos pelos lados.
        /// Sem colisor (o diretor deixa os primeiros 3 m do raio terem obstáculo, mas nem precisa).
        /// </summary>
        private static void VantageFoliage(Ctx c, Vector3 vpos, float dist)
        {
            Vector3 flat = new Vector3(vpos.x, 0f, vpos.z);
            Vector3 fwd = (new Vector3(HouseCenter.x, 0f, HouseCenter.z) - flat).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            float bd = c.R(1.5f, 2.2f);
            float h = vpos.y;
            float lineY = h + (1.3f - h) * (bd / dist);
            // O topo da folhagem encosta na linha de visão: numa teleobjetiva (FOV ~7-16°) ela ocupa
            // mais ou menos o terço de baixo do quadro (varia com o alvo). Ajuste fino: some/subtraia aqui.
            float top = lineY + 0.02f;
            Vector3 bp = flat + fwd * bd;
            string leaf = c.Leaf();
            MeshBuilder m = c.M(leaf);

            if (h > 2.6f)
            {
                // Arvoreta: tronco fino + copa redonda logo abaixo da linha de visão.
                float ry = 0.45f;
                float cy = top - 0.14f - ry * 1.12f; // copa redonda: sem facetas achatando o topo
                Frustum(c.M("Casca"), bp + Vector3.down * 0.1f, bp + Vector3.up * cy, 0.11f, 0.06f, 5, c.R(0f, 6f), false, false);
                Blob(m, bp + Vector3.up * cy, new Vector3(0.75f, ry, 0.7f), Quaternion.Euler(0f, c.R(0f, 360f), 0f), 0.12f, 0f, c.Rng);
                for (int k = 0; k < 3; k++)
                {
                    float a = c.R(0f, Mathf.PI * 2f);
                    Vector3 off = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * c.R(0.35f, 0.55f);
                    float s = c.R(0.28f, 0.38f);
                    Blob(m, bp + off + Vector3.up * (cy - c.R(0.1f, 0.3f)), new Vector3(s * 1.1f, s * 0.85f, s * 1.1f), Quaternion.identity, 0.12f, 0f, c.Rng);
                }
                Bush(c, bp + right * c.R(-0.3f, 0.3f), c.R(0.8f, 1.0f), leaf); // moita no pé
            }
            else
            {
                // Moita alta: um "ovo" de folhas do chão até a linha de visão + calombos de folhas
                // na superfície (silhueta irregular na borda do quadro).
                float ry = top * 0.5f / 1.12f;
                float rx = c.R(0.65f, 0.8f);
                Vector3 eggC = bp + Vector3.up * (top * 0.5f - 0.05f);
                Blob(m, eggC, new Vector3(rx, ry, rx * 0.9f), Quaternion.Euler(0f, c.R(0f, 360f), 0f), 0.1f, 0f, c.Rng);
                for (int k = 0; k < 7; k++)
                {
                    float a = c.R(0f, Mathf.PI * 2f);
                    float yk = c.R(0.25f, 0.8f) * top;
                    float r = c.R(0.22f, 0.32f);
                    if (yk + r * 0.9f > top) yk = top - r * 0.9f;
                    float ny = (yk - eggC.y) / ry;
                    float ring = rx * Mathf.Sqrt(Mathf.Max(0.05f, 1f - ny * ny));
                    Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    Blob(m, bp + d * (ring * 0.85f) + Vector3.up * yk, new Vector3(r, r * 0.8f, r), Quaternion.Euler(0f, c.R(0f, 360f), 0f), 0.12f, 0f, c.Rng);
                }
                Bush(c, bp + right * c.R(-0.4f, 0.4f) - fwd * 0.2f, c.R(0.9f, 1.15f), leaf);
            }

            // Raminhos de folhas subindo pelos lados da lente (cadeia de bolinhas saindo do topo).
            for (int s = -1; s <= 1; s += 2)
            {
                if (!c.Chance(0.75f)) continue;
                Vector3 sp = bp + right * (s * c.R(0.24f, 0.4f)) + fwd * c.R(-0.2f, 0.1f) + Vector3.up * (lineY + c.R(-0.04f, 0.1f));
                Vector3 from = bp + right * (s * 0.2f) + Vector3.up * (top - 0.2f);
                for (int k = 0; k < 3; k++)
                {
                    float t = (k + 1f) / 3f;
                    float r = Mathf.Lerp(0.12f, 0.075f, t);
                    Blob(m, Vector3.Lerp(from, sp, t), new Vector3(r * 1.2f, r * 0.8f, r), Quaternion.Euler(0f, c.R(0f, 360f), c.R(-20f, 20f)), 0.15f, 0f, c.Rng);
                }
            }
            c.BushCount++;
        }

        // ================================================================== Linha de visão

        /// <summary>Pontos do contorno da casa + quintal da frente (onde os atores ficam).</summary>
        private static void BuildSightTargets(Ctx c)
        {
            float x0 = Site.SightRect.x, x1 = Site.SightRect.y, z0 = Site.SightRect.z, z1 = Site.SightRect.w;
            for (float x = x0; x <= x1 + 0.01f; x += 1f)
            {
                c.SightTargets.Add(new Vector2(x, z0));
                c.SightTargets.Add(new Vector2(x, z1));
            }
            for (float z = z0; z <= z1 + 0.01f; z += 1f)
            {
                c.SightTargets.Add(new Vector2(x0, z));
                c.SightTargets.Add(new Vector2(x1, z));
            }
            c.SightTargets.Add(new Vector2(HouseCenter.x, HouseCenter.z));
        }

        /// <summary>Algo de raio 'clearance' em 'p' atrapalharia algum esconderijo de ver a casa?</summary>
        private static bool BlocksSight(Ctx c, Vector2 p, float clearance)
        {
            foreach (var v in c.Vantages)
            {
                var v2 = new Vector2(v.x, v.z);
                foreach (var t in c.SightTargets)
                {
                    if (DistPointSegment(p, v2, t) < clearance) return true;
                }
            }
            return false;
        }

        private static float DistPointSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude);
            t = Mathf.Clamp01(t);
            return Vector2.Distance(p, a + ab * t);
        }

        private static bool NearHouse(float x, float z, float margin)
        {
            return x > HouseMinX - margin && x < HouseMaxX + margin && z > HouseMinZ - margin && z < HouseMaxZ + margin;
        }

        /// <summary>Caminho (porta → portão) e estrada de terra.</summary>
        private static bool OnPathOrRoad(float x, float z, float margin)
        {
            if (z < Site.FrontZ && z > Site.RoadStartZ && Mathf.Abs(x) < Site.PathHalfWidth + margin) return true;
            if (z <= Site.RoadStartZ && Mathf.Abs(x - RoadX(z)) < 1.3f + margin) return true;
            return false;
        }

        private static float RoadX(float z)
        {
            float k = Mathf.InverseLerp(Site.RoadBendFrom, Site.RoadBendTo, z);
            return Mathf.Sin(z * 0.17f) * 0.8f * k;
        }

        private static bool InKeepOut(Ctx c, float x, float z, float r)
        {
            foreach (var k in c.KeepOut)
            {
                float dx = x - k.x, dz = z - k.z;
                float rr = r + k.y;
                if (dx * dx + dz * dz < rr * rr) return true;
            }
            return false;
        }

        // ================================================================== Árvores

        private static void PlaceTrees(Ctx c)
        {
            const int wanted = 95;
            for (int attempt = 0; attempt < 6000 && c.TreeCount < wanted; attempt++)
            {
                float x = c.R(-43.5f, 43.5f);
                float z = c.R(-43.5f, 43.5f);
                float d = new Vector2(x - HouseCenter.x, z - HouseCenter.z).magnitude;
                if (d < Site.TreeClear) continue;
                if (Site.TreeAvoidHouseRect && NearHouse(x, z, 2f)) continue;
                if (z < Site.RoadViewZ && Mathf.Abs(x) < 3f + (Site.RoadViewZ - z) * 0.12f) continue; // vista da estrada
                if (z > FenceZ - 1.5f && z < FenceZ + 1.5f && Mathf.Abs(x) < Site.FenceClearX) continue; // cerca
                float density = Mathf.Lerp(0.25f, 1f, Mathf.InverseLerp(Site.TreeClear, Site.TreeClear + 15f, d));
                if (!c.Chance(density)) continue;

                bool dead = c.Chance(d < Site.TreeClear + 9f ? 0.55f : 0.3f);
                float h = dead ? c.R(5f, 8.5f) : c.R(6.5f, 9f) + Mathf.InverseLerp(15f, 40f, d) * 2f;
                float visR = h * (dead ? 0.34f : 0.31f);

                if (InKeepOut(c, x, z, visR * 0.6f)) continue;
                bool tooClose = false;
                foreach (var t in c.Trees)
                {
                    float min = (visR + t.y) * 0.55f + 0.6f;
                    float dx = x - t.x, dz = z - t.z;
                    if (dx * dx + dz * dz < min * min) { tooClose = true; break; }
                }
                if (tooClose) continue;
                if (BlocksSight(c, new Vector2(x, z), visR + 0.4f)) continue;

                Vector3 p = new Vector3(x, 0f, z);
                float trunkR = dead ? DeadTree(c, p, h) : Pine(c, p, h);
                c.Trees.Add(new Vector3(x, visR, z));
                c.TrunkList.Add(new Vector3(x, trunkR, z));
                AddTrunkCollider(c, p, trunkR);
                c.TreeCount++;
            }
        }

        /// <summary>Só o tronco colide (cápsula de 4,2 m). Copas e galhos não.</summary>
        private static void AddTrunkCollider(Ctx c, Vector3 p, float radius)
        {
            var col = c.Colliders.AddComponent<CapsuleCollider>();
            col.direction = 1;
            col.radius = radius;
            col.height = 4.2f;
            col.center = new Vector3(p.x, 2.1f, p.z);
            c.Trunks++;
        }

        /// <summary>Pinheiro gordinho: tronco curto + 3/4 cones empilhados, ponta caída. Retorna o raio do tronco.</summary>
        private static float Pine(Ctx c, Vector3 p, float h)
        {
            MeshBuilder bark = c.M("Casca");
            MeshBuilder leaves = c.M(c.Chance(0.5f) ? "PinheiroA" : "PinheiroB");
            float la = c.R(0f, Mathf.PI * 2f);
            Vector3 lean = new Vector3(Mathf.Cos(la), 0f, Mathf.Sin(la)) * c.R(0f, 0.07f) * h;

            Frustum(bark, p + Vector3.down * 0.1f, p + Vector3.up * (h * 0.3f) + lean * 0.3f, 0.3f, 0.17f, 6, c.R(0f, 6f), false, false);

            int tiers = h > 8f ? 4 : 3;
            for (int t = 0; t < tiers; t++)
            {
                float k = t / (tiers - 1f);
                float y0 = Mathf.Lerp(h * 0.2f, h * 0.7f, k);
                float r = Mathf.Lerp(h * 0.3f, h * 0.13f, k) * c.R(0.9f, 1.1f);
                float coneH = t == tiers - 1 ? h - y0 : r * 1.3f;
                Vector3 center = p + Vector3.up * y0 + lean * (y0 / h);
                Vector3 apex = center + Vector3.up * coneH + lean * (coneH / h);
                if (t == tiers - 1)
                {
                    float da = c.R(0f, Mathf.PI * 2f);
                    apex += new Vector3(Mathf.Cos(da), 0f, Mathf.Sin(da)) * c.R(0.25f, 0.6f); // ponta caída (cartoon)
                }
                Cone(leaves, center, r, apex, 7, c.R(0f, 6f), 0.12f, c.Rng);
            }
            return 0.3f;
        }

        /// <summary>Árvore morta retorcida: tronco em espiral + galhos que se curvam como garras. Retorna o raio do tronco.</summary>
        private static float DeadTree(Ctx c, Vector3 p, float h)
        {
            MeshBuilder bark = c.M("Casca");
            const int segs = 5;
            float r0 = c.R(0.3f, 0.4f);
            float bendBase = c.R(0f, 360f);
            float bendDir = c.Chance(0.5f) ? 55f : -55f;
            Vector3 cur = p + Vector3.down * 0.15f;
            Vector3 dir = Vector3.up;
            float segLen = h / segs;
            var nodes = new List<Vector3>();
            var nodeR = new List<float>();
            var nodeDir = new List<Vector3>();

            for (int s = 0; s < segs; s++)
            {
                float t = (s + 1f) / segs;
                Vector3 side = Quaternion.Euler(0f, bendBase + s * bendDir, 0f) * Vector3.forward;
                dir = (dir + side * c.R(0.15f, 0.35f) * t).normalized;
                if (dir.y < 0.55f) dir = (dir + Vector3.up * 0.5f).normalized;
                Vector3 next = cur + dir * segLen;
                float ra = r0 * Mathf.Lerp(1f, 0.2f, s / (float)segs);
                float rb = r0 * Mathf.Lerp(1f, 0.2f, t);
                if (s == 0) ra *= 1.7f; // pé alargado
                Frustum(bark, cur - dir * 0.05f, next, ra, rb, 6, s * 0.4f, false, false);
                nodes.Add(next);
                nodeR.Add(rb);
                nodeDir.Add(dir);
                cur = next;
            }
            Frustum(bark, cur - dir * 0.03f, cur + dir * 0.7f, nodeR[segs - 1], 0f, 5, 0f, false, false);

            // Raízes aparentes (3 cones deitados).
            for (int k = 0; k < 3; k++)
            {
                float a = (k / 3f + c.R(-0.08f, 0.08f)) * Mathf.PI * 2f;
                Vector3 o = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Frustum(bark, p + Vector3.up * 0.35f, p + o * (r0 * 3.2f) + Vector3.down * 0.05f, r0 * 0.45f, 0f, 5, 0f, false, false);
            }

            int branches = 3 + c.Rng.Next(3);
            for (int b = 0; b < branches; b++)
            {
                int ni = 1 + c.Rng.Next(segs - 2);
                Vector3 bc = nodes[ni];
                float br = nodeR[ni] * 0.6f;
                float a = c.R(0f, Mathf.PI * 2f);
                Vector3 bdir = (new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) + Vector3.up * 0.4f).normalized;
                Vector3 curl = Vector3.Cross(Vector3.up, bdir) * (c.Chance(0.5f) ? 1f : -1f);
                float L = h * c.R(0.16f, 0.28f);
                for (int k = 0; k < 3; k++)
                {
                    bdir = (bdir + Vector3.up * 0.3f + curl * 0.3f).normalized;
                    Vector3 nxt = bc + bdir * (L / 3f);
                    float nr = br * 0.6f;
                    Frustum(bark, bc - bdir * 0.03f, nxt, br, k == 2 ? 0f : nr, 5, 0f, false, false);
                    if (k == 1 && c.Chance(0.6f))
                    {
                        Vector3 twig = (bdir - curl * 0.8f + Vector3.up * 0.3f).normalized;
                        Frustum(bark, nxt, nxt + twig * (L * 0.3f), nr * 0.7f, 0f, 4, 0f, false, false);
                    }
                    bc = nxt;
                    br = nr;
                }
            }
            return r0;
        }

        // ================================================================== Arbustos

        private static void PlaceBushes(Ctx c)
        {
            // a) Rente à casa: baixinhos (< 1,1 m), longe do caminho e do ponto de partida dos atores.
            int placed = 0;
            for (int attempt = 0; attempt < 400 && placed < 10; attempt++)
            {
                float x, z;
                int side = c.Rng.Next(4);
                if (side == 0) { x = c.R(Site.HugXMin, Site.HugXMax); z = c.R(Site.HugFrontZ.x, Site.HugFrontZ.y); }
                else if (side == 1) { x = c.R(Site.HugXMin, Site.HugXMax); z = c.R(Site.HugBackZ.x, Site.HugBackZ.y); }
                else if (side == 2) { x = c.R(Site.HugWestX.x, Site.HugWestX.y); z = c.R(Site.HugSideZ.x, Site.HugSideZ.y); }
                else { x = c.R(Site.HugEastX.x, Site.HugEastX.y); z = c.R(Site.HugSideZ.x, Site.HugSideZ.y); }
                if (z < Site.FrontZ && Mathf.Abs(x) < Site.HugPathClear) continue;
                if (InKeepOut(c, x, z, 0.5f)) continue;
                Bush(c, new Vector3(x, 0f, z), c.R(0.6f, 1.1f), c.Leaf());
                c.KeepOut.Add(new Vector3(x, 0.7f, z));
                placed++;
            }

            // b) Espalhados pelo terreno.
            placed = 0;
            for (int attempt = 0; attempt < 3000 && placed < 55; attempt++)
            {
                float x = c.R(-43f, 43f);
                float z = c.R(-43f, 43f);
                if (NearHouse(x, z, 2f) || OnPathOrRoad(x, z, 0.6f)) continue;
                float size = c.R(0.7f, 1.4f);
                if (InKeepOut(c, x, z, size * 0.7f)) continue;
                bool hit = false;
                foreach (var t in c.TrunkList)
                {
                    float dx = x - t.x, dz = z - t.z;
                    if (dx * dx + dz * dz < 1.2f * 1.2f) { hit = true; break; }
                }
                if (hit) continue;
                if (BlocksSight(c, new Vector2(x, z), size * 0.7f + 0.3f)) continue;
                Bush(c, new Vector3(x, 0f, z), size, c.Leaf());
                c.KeepOut.Add(new Vector3(x, size * 0.6f, z));
                placed++;
            }
        }

        /// <summary>Moita redonda: 3 a 6 bolas. Altura ≈ 0,95 × size.</summary>
        private static void Bush(Ctx c, Vector3 p, float size, string leafKey)
        {
            MeshBuilder m = c.M(leafKey);
            Blob(m, p + Vector3.up * (size * 0.42f), new Vector3(size * 0.62f, size * 0.48f, size * 0.62f),
                 Quaternion.Euler(0f, c.R(0f, 360f), 0f), 0.12f, 0f, c.Rng);
            int n = 2 + c.Rng.Next(4);
            for (int k = 0; k < n; k++)
            {
                float a = c.R(0f, Mathf.PI * 2f);
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float s2 = size * c.R(0.5f, 0.75f);
                Vector3 cpos = p + d * (size * c.R(0.35f, 0.6f)) + Vector3.up * (s2 * 0.38f);
                Blob(m, cpos, new Vector3(s2 * 0.6f, s2 * 0.46f, s2 * 0.6f), Quaternion.Euler(0f, c.R(0f, 360f), 0f), 0.12f, 0f, c.Rng);
            }
            c.BushCount++;
        }

        // ================================================================== Props

        private static void PlaceProps(Ctx c, Transform root)
        {
            Fence(c);
            Mailbox(c, new Vector3(2.5f, 0f, FenceZ - 0.9f), -8f);
            Graveyard(c, Site.Graveyard);
            Pumpkin(c, new Vector3(2.6f, 0f, -8.6f) + Site.PropShift, 0.34f, root, true);
            Pumpkin(c, new Vector3(3.15f, 0f, -8.15f) + Site.PropShift, 0.22f, root, false);
            Pumpkin(c, new Vector3(-2.5f, 0f, -9.3f) + Site.PropShift, 0.27f, root, false);

            // Tocos espalhados.
            int stumps = 0;
            for (int attempt = 0; attempt < 200 && stumps < 5; attempt++)
            {
                float x = c.R(-25f, 25f), z = c.R(-25f, 25f);
                float d = new Vector2(x, z - HouseCenter.z).magnitude;
                if (d < Site.StumpClear || OnPathOrRoad(x, z, 1f) || InKeepOut(c, x, z, 1f)) continue;
                Stump(c, new Vector3(x, 0f, z));
                c.KeepOut.Add(new Vector3(x, 1f, z));
                stumps++;
            }
        }

        /// <summary>Cerca de estacas torta na frente, com o portão aberto no caminho.</summary>
        private static void Fence(Ctx c)
        {
            MeshBuilder wood = c.M("Madeira");
            for (int side = -1; side <= 1; side += 2)
            {
                // Postes de 2,2 em 2,2 m (de x = ±2 até ±13), mais um trecho virando para trás.
                var posts = new List<Vector3>();
                for (float a = 2f; a <= 13.01f; a += 2.2f) posts.Add(new Vector3(side * a, 0f, FenceZ + Mathf.Sin(a * 1.3f) * 0.12f));
                Vector3 last = posts[posts.Count - 1];
                posts.Add(last + new Vector3(0f, 0f, 2.1f));
                posts.Add(last + new Vector3(side * 0.2f, 0f, 4.2f));

                var tops = new List<Vector3>();
                foreach (var pp in posts)
                {
                    float hgt = c.R(1.05f, 1.3f);
                    Quaternion tilt = Quaternion.Euler(c.R(-7f, 7f), c.R(-10f, 10f), c.R(-7f, 7f));
                    Box(wood, pp + tilt * (Vector3.up * (hgt * 0.5f - 0.1f)), new Vector3(0.13f, hgt, 0.13f), tilt);
                    tops.Add(pp + tilt * (Vector3.up * (hgt - 0.1f)));
                    Box(wood, pp + tilt * (Vector3.up * (hgt - 0.08f)), new Vector3(0.18f, 0.06f, 0.18f), tilt); // chapéu do poste
                }

                for (int i = 0; i < posts.Count - 1; i++)
                {
                    if (i == posts.Count - 3 && c.Chance(0.5f)) continue; // um vão quebrado
                    Vector3 a = posts[i], b = posts[i + 1];
                    Vector3 along = b - a;
                    float len = along.magnitude;
                    Vector3 dirA = along / len;
                    Quaternion rot = Quaternion.LookRotation(Vector3.Cross(dirA, Vector3.up), Vector3.up);
                    // Duas travessas.
                    for (int r = 0; r < 2; r++)
                    {
                        float y = r == 0 ? 0.32f : 0.78f;
                        float droop = c.R(-0.06f, 0.06f);
                        Vector3 p0 = a + Vector3.up * y, p1 = b + Vector3.up * (y + droop);
                        Quaternion rr = Quaternion.LookRotation(p1 - p0, Vector3.up);
                        Box(wood, (p0 + p1) * 0.5f, new Vector3(0.05f, 0.09f, (p1 - p0).magnitude + 0.1f), rr);
                    }
                    // Estacas pontudas.
                    for (float s = 0.2f; s < len - 0.1f; s += 0.3f)
                    {
                        if (c.Chance(0.1f)) continue; // estaca faltando
                        Vector3 bp = a + dirA * s + rot * Vector3.forward * 0.07f;
                        float ph = c.R(0.85f, 1.05f);
                        Quaternion lean = rot * Quaternion.Euler(c.R(-4f, 4f), 0f, c.R(-8f, 8f));
                        var prof = new[]
                        {
                            new Vector2(-0.055f, -0.1f), new Vector2(0.055f, -0.1f), new Vector2(0.055f, ph),
                            new Vector2(0f, ph + 0.09f), new Vector2(-0.055f, ph),
                        };
                        Prism(wood, prof, 0.035f, Matrix4x4.TRS(bp, lean, Vector3.one));
                    }
                }
            }
            c.KeepOut.Add(new Vector3(-7.5f, 1.2f, FenceZ));
            c.KeepOut.Add(new Vector3(7.5f, 1.2f, FenceZ));
        }

        /// <summary>Caixa de correio torta, vermelha, com bandeirinha levantada.</summary>
        private static void Mailbox(Ctx c, Vector3 p, float tiltDeg)
        {
            Matrix4x4 M = Matrix4x4.TRS(p, Quaternion.Euler(0f, c.R(-15f, 15f), tiltDeg), Vector3.one);
            Quaternion rot = M.rotation;
            MeshBuilder wood = c.M("Madeira");
            MeshBuilder red = c.M("Correio");
            Box(wood, M.MultiplyPoint3x4(new Vector3(0f, 0.5f, 0f)), new Vector3(0.11f, 1.1f, 0.11f), rot);
            Box(red, M.MultiplyPoint3x4(new Vector3(0f, 1.15f, 0f)), new Vector3(0.3f, 0.2f, 0.52f), rot);
            Frustum(red, M.MultiplyPoint3x4(new Vector3(0f, 1.25f, -0.26f)), M.MultiplyPoint3x4(new Vector3(0f, 1.25f, 0.26f)), 0.15f, 0.15f, 10, 0f, true, true);
            Box(red, M.MultiplyPoint3x4(new Vector3(0.17f, 1.38f, 0.1f)), new Vector3(0.02f, 0.32f, 0.03f), rot);
            Box(red, M.MultiplyPoint3x4(new Vector3(0.17f, 1.48f, 0.17f)), new Vector3(0.02f, 0.11f, 0.15f), rot);
            c.KeepOut.Add(new Vector3(p.x, 0.8f, p.z));
        }

        /// <summary>Cemiteriozinho do lado da casa: lápides tortas (redondas e cruzes) com montinhos de terra.</summary>
        private static void Graveyard(Ctx c, Vector3 center)
        {
            MeshBuilder stone = c.M("Pedra");
            MeshBuilder dirt = c.M("Terra");
            int i = 0;
            for (int row = 0; row < 2; row++)
            {
                for (int col = 0; col < 4; col++, i++)
                {
                    if (row == 1 && col == 3) continue;
                    Vector3 p = center + new Vector3(row * 2.2f - 1.1f + c.R(-0.25f, 0.25f), 0f, col * 1.6f - 2.4f + c.R(-0.2f, 0.2f));
                    // Lápide olha para a casa (-X); inclinação exagerada.
                    Quaternion rot = Quaternion.Euler(c.R(-14f, 14f), -90f + c.R(-15f, 15f), c.R(-12f, 12f));
                    Matrix4x4 M = Matrix4x4.TRS(p + Vector3.down * 0.08f, rot, Vector3.one);
                    bool cross = i % 4 == 2;
                    if (cross)
                    {
                        float hh = c.R(0.9f, 1.15f);
                        Box(stone, M.MultiplyPoint3x4(new Vector3(0f, hh * 0.5f, 0f)), new Vector3(0.14f, hh, 0.12f), rot);
                        Box(stone, M.MultiplyPoint3x4(new Vector3(0f, hh * 0.72f, 0f)), new Vector3(0.52f, 0.13f, 0.12f), rot);
                    }
                    else
                    {
                        float w = c.R(0.32f, 0.42f), hh = c.R(0.55f, 0.85f);
                        var prof = new List<Vector2> { new Vector2(-w, 0f), new Vector2(w, 0f) };
                        for (int k = 0; k <= 8; k++)
                        {
                            float a = k / 8f * Mathf.PI;
                            prof.Add(new Vector2(Mathf.Cos(a) * w, hh + Mathf.Sin(a) * w));
                        }
                        Prism(stone, prof.ToArray(), 0.16f, M);
                    }
                    // Montinho de terra na frente da lápide (lado da casa).
                    Vector3 mound = p + new Vector3(-0.75f, 0f, 0f);
                    Blob(dirt, mound, new Vector3(0.45f, 0.14f, 0.85f), Quaternion.Euler(0f, c.R(-10f, 10f), 0f), 0.1f, 0f, c.Rng);
                }
            }
            c.KeepOut.Add(new Vector3(center.x, 3.6f, center.z));
        }

        private static void Stump(Ctx c, Vector3 p)
        {
            MeshBuilder wood = c.M("Madeira");
            float r = c.R(0.3f, 0.42f);
            float h = c.R(0.35f, 0.6f);
            Frustum(wood, p + Vector3.down * 0.05f, p + Vector3.up * h, r * 1.15f, r, 8, c.R(0f, 6f), false, true);
            for (int k = 0; k < 3; k++)
            {
                float a = (k / 3f + c.R(-0.1f, 0.1f)) * Mathf.PI * 2f;
                Vector3 o = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Frustum(wood, p + Vector3.up * (h * 0.5f) + o * r * 0.5f, p + o * (r * 2.4f) + Vector3.down * 0.04f, r * 0.35f, 0f, 5, 0f, false, false);
            }
        }

        /// <summary>Abóbora com cara acesa (triângulos brilhantes). A maior ganha uma luzinha laranja.</summary>
        private static void Pumpkin(Ctx c, Vector3 p, float r, Transform root, bool withLight)
        {
            MeshBuilder body = c.M("Abobora");
            MeshBuilder glow = c.M("Brilho");
            float ry = r * 0.75f;
            Vector3 center = p + Vector3.up * (ry * 0.92f);
            Blob(body, center, new Vector3(r, ry, r), Quaternion.Euler(0f, c.R(0f, 360f), 0f), 0.03f, 0.09f, c.Rng);
            Vector3 stemTop = center + Vector3.up * (ry * 1.25f) + new Vector3(c.R(-0.04f, 0.04f), 0f, c.R(-0.04f, 0.04f));
            Frustum(c.M("Madeira"), center + Vector3.up * (ry * 0.8f), stemTop, r * 0.13f, r * 0.08f, 5, 0f, false, true);

            // Rosto virado para a frente da casa (onde ficam as câmeras de frente).
            Vector3 look = new Vector3(0f, 0f, FenceZ - 1f) - new Vector3(p.x, 0f, p.z);
            Vector3 f = look.normalized;
            Vector3 right = Vector3.Cross(Vector3.up, f);
            System.Func<float, float, Vector3> S = (fx, fy) =>
            {
                float x = fx * r, y = fy * ry;
                float s = 1f - (x / r) * (x / r) - (y / ry) * (y / ry);
                float depth = r * Mathf.Sqrt(Mathf.Max(0.02f, s)) * 1.1f + 0.004f;
                return center + right * x + Vector3.up * y + f * depth;
            };
            // Olhos (triângulos) e nariz.
            glow.TriBoth(S(-0.48f, 0.12f), S(-0.14f, 0.12f), S(-0.30f, 0.45f));
            glow.TriBoth(S(0.14f, 0.12f), S(0.48f, 0.12f), S(0.30f, 0.45f));
            glow.TriBoth(S(-0.08f, -0.05f), S(0.08f, -0.05f), S(0f, 0.08f));
            // Boca em zigue-zague.
            const int n = 7;
            for (int i = 0; i < n - 1; i++)
            {
                float x0 = Mathf.Lerp(-0.55f, 0.55f, i / (n - 1f));
                float x1 = Mathf.Lerp(-0.55f, 0.55f, (i + 1) / (n - 1f));
                float t0 = (i % 2 == 0) ? -0.16f : -0.26f;
                float t1 = ((i + 1) % 2 == 0) ? -0.16f : -0.26f;
                float b0 = -0.32f - 0.22f * (1f - (x0 / 0.55f) * (x0 / 0.55f));
                float b1 = -0.32f - 0.22f * (1f - (x1 / 0.55f) * (x1 / 0.55f));
                if (i == 0) t0 = b0 = -0.2f;
                if (i == n - 2) t1 = b1 = -0.2f;
                glow.TriBoth(S(x0, t0), S(x1, t1), S(x1, b1));
                glow.TriBoth(S(x0, t0), S(x1, b1), S(x0, b0));
            }

            c.KeepOut.Add(new Vector3(p.x, r + 0.3f, p.z));
            if (!withLight) return;
            var lgo = new GameObject("Luz_Abobora");
            lgo.transform.SetParent(root, false);
            lgo.transform.position = center + f * 0.5f + Vector3.up * 0.2f;
            var light = lgo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.55f, 0.2f);
            light.range = 3.5f;
            light.intensity = 0.8f;
            light.shadows = LightShadows.None;
        }

        // ================================================================== Olhos no mato

        /// <summary>Pares de olhos amarelos espiando ao lado de troncos lá no fundo (exagerado, de desenho).</summary>
        private static void PlaceEyes(Ctx c)
        {
            MeshBuilder m = c.M("Olhos");
            int placed = 0;
            for (int attempt = 0; attempt < 300 && placed < 7; attempt++)
            {
                if (c.TrunkList.Count == 0) break;
                Vector3 t = c.TrunkList[c.Rng.Next(c.TrunkList.Count)];
                float d = new Vector2(t.x - HouseCenter.x, t.z - HouseCenter.z).magnitude;
                if (d < Site.Eyes.x || d > Site.Eyes.y) continue;
                Vector3 toHouse = (new Vector3(HouseCenter.x, 0f, HouseCenter.z) - new Vector3(t.x, 0f, t.z)).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, toHouse) * (c.Chance(0.5f) ? 1f : -1f);
                Vector3 mid = new Vector3(t.x, c.R(0.6f, 2.2f), t.z) + side * (t.y + 0.3f) + toHouse * 0.1f;
                Quaternion face = Quaternion.LookRotation(toHouse, Vector3.up);
                float tilt = c.R(10f, 22f);
                Vector3 radii = new Vector3(0.075f, 0.045f, 0.03f);
                Blob(m, mid - face * Vector3.right * 0.11f, radii, face * Quaternion.Euler(0f, 0f, -tilt), 0f, 0f, c.Rng);
                Blob(m, mid + face * Vector3.right * 0.11f, radii, face * Quaternion.Euler(0f, 0f, tilt), 0f, 0f, c.Rng);
                placed++;
            }
        }

        // ================================================================== Chão (sem colisor)

        private static void PlaceGround(Ctx c)
        {
            MeshBuilder dirt = c.M("Terra");
            MeshBuilder grass = c.M("Grama");

            // Estrada de terra saindo do portão até a borda do mapa.
            float prevL = 0f, prevR = 0f, prevZ = 0f;
            bool first = true;
            for (float z = Site.RoadStartZ; z >= -44.6f; z -= 1f)
            {
                float cx = RoadX(z);
                float hw = 1.1f + c.R(-0.12f, 0.12f);
                if (!first)
                {
                    Vector3 a = new Vector3(prevL, 0.02f, prevZ), b = new Vector3(prevR, 0.02f, prevZ);
                    Vector3 cc = new Vector3(cx + hw, 0.02f, z), d = new Vector3(cx - hw, 0.02f, z);
                    Vector3 below = (a + cc) * 0.5f + Vector3.down;
                    dirt.Quad(a, b, cc, d, below);
                }
                prevL = cx - hw;
                prevR = cx + hw;
                prevZ = z;
                first = false;
            }

            // Manchas de terra irregulares.
            int patches = 0;
            for (int attempt = 0; attempt < 200 && patches < 8; attempt++)
            {
                float x = c.R(-30f, 30f), z = c.R(-30f, 30f);
                if (NearHouse(x, z, 1.5f) || OnPathOrRoad(x, z, 1.5f)) continue;
                float rad = c.R(0.8f, 1.9f);
                Vector3 ctr = new Vector3(x, 0.015f, z);
                const int seg = 12;
                var ring = new Vector3[seg];
                for (int k = 0; k < seg; k++)
                {
                    float a = k / (float)seg * Mathf.PI * 2f;
                    float rr = rad * c.R(0.7f, 1.15f);
                    ring[k] = ctr + new Vector3(Mathf.Cos(a) * rr, 0f, Mathf.Sin(a) * rr);
                }
                for (int k = 0; k < seg; k++) dirt.Tri(ctr, ring[k], ring[(k + 1) % seg], ctr + Vector3.down);
                patches++;
            }

            // Tufos de grama (folhas finas, dupla face, sem sombra).
            int tufts = 0;
            for (int attempt = 0; attempt < 3000 && tufts < 320; attempt++)
            {
                float x = c.R(-43f, 43f), z = c.R(-43f, 43f);
                if (NearHouse(x, z, 0.3f) || OnPathOrRoad(x, z, 0.2f)) continue;
                Vector3 bp = new Vector3(x, 0f, z);
                int blades = 4 + c.Rng.Next(3);
                for (int k = 0; k < blades; k++)
                {
                    float a = c.R(0f, Mathf.PI * 2f);
                    Vector3 o = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    Vector3 sd = Vector3.Cross(Vector3.up, o) * 0.035f;
                    float h = c.R(0.2f, 0.45f);
                    Vector3 b0 = bp + o * c.R(0f, 0.08f);
                    grass.TriBoth(b0 - sd, b0 + sd, b0 + Vector3.up * h + o * (h * c.R(0.2f, 0.5f)));
                }
                tufts++;
            }
        }

        // ================================================================== Malhas e assets

        private static int FinalizeMeshes(Ctx c, Transform root)
        {
            var holder = new GameObject("Malhas").transform;
            holder.SetParent(root, false);
            int tris = 0;
            foreach (var def in MatDefs)
            {
                MeshBuilder mb;
                if (!c.Mb.TryGetValue(def.Key, out mb) || mb.V.Count == 0) continue;
                Mesh mesh = SaveMesh("Cen_" + def.Key, mb);
                var go = new GameObject("Malha_" + def.Key);
                go.transform.SetParent(holder, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = GetMaterial(def, false);
                mr.shadowCastingMode = def.Shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                mr.receiveShadows = !def.Unlit;
                tris += mb.T.Count / 3;
                c.MeshObjects++;
            }
            return tris;
        }

        /// <summary>Salva (ou atualiza, mantendo o GUID) a malha combinada em Art/Cenario/Malhas.</summary>
        private static Mesh SaveMesh(string name, MeshBuilder mb)
        {
            string path = $"{MeshFolder}/{name}.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh();
            mesh.Clear();
            mesh.name = name;
            mesh.indexFormat = mb.V.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(mb.V);
            mesh.SetNormals(mb.N);
            mesh.SetTriangles(mb.T, 0);
            mesh.RecalculateBounds();
            if (isNew) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        /// <summary>Carrega o material do cache; cria se faltar. 'reset' reaplica as cores do código.</summary>
        private static Material GetMaterial(MatDef d, bool reset)
        {
            string path = $"{MatFolder}/M_Cen_{d.Key}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = mat == null;
            if (isNew)
            {
                mat = new Material(Shader.Find(d.Unlit ? "Universal Render Pipeline/Unlit" : HorrorTycoon.Art.ToonMaterials.ToonShaderName));
                AssetDatabase.CreateAsset(mat, path);
            }
            if (isNew || reset)
            {
                mat.SetColor("_BaseColor", d.Color);
                if (!d.Unlit)
                {
                    HTVisualSetup.UpgradeToToon(mat);
                    if (HorrorTycoon.Art.ToonMaterials.IsToon(mat)) HorrorTycoon.Art.ToonMaterials.ApplyEnvironment(mat, d.Color, 0.12f);
                    mat.SetFloat("_GradientScale", 0.5f);
                    mat.SetFloat("_GradientOffset", 0f);
                }
                mat.enableInstancing = true;
                EditorUtility.SetDirty(mat);
            }
            else if (!d.Unlit && !HorrorTycoon.Art.ToonMaterials.IsToon(mat))
            {
                // Visual toon (guia de arte §7): troca o URP/Lit antigo pelo toon, MANTENDO a cor ajustada à mão.
                HTVisualSetup.UpgradeToToon(mat);
            }
            return mat;
        }

        [MenuItem("Horror Tycoon/Cenário/Restaurar cores dos materiais")]
        public static void ResetMaterials()
        {
            EnsureFolders();
            foreach (var d in MatDefs) GetMaterial(d, true);
            AssetDatabase.SaveAssets();
            Debug.Log("[Cenário] Cores dos materiais restauradas para as do código.");
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/_HorrorTycoon", "Art");
            EnsureFolder("Assets/_HorrorTycoon/Art", "Cenario");
            EnsureFolder(ArtFolder, "Materiais");
            EnsureFolder(ArtFolder, "Malhas");
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }

        // ================================================================== Cena de teste

        [MenuItem("Horror Tycoon/Cenário/Montar cena de teste (Lab_Cenario)")]
        public static void BuildLabSceneMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildLabScene();
        }

        /// <summary>
        /// Cena de laboratório: chão, caixa cinza no lugar da casa, lua, névoa e o cenário.
        /// Sem diálogos (pode ser chamada por automação). Salva em Scenes/Lab_Cenario.unity.
        /// </summary>
        public static void BuildLabScene()
        {
            EnsureFolders();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.18f, 0.26f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.02f;
            RenderSettings.fogColor = new Color(0.04f, 0.05f, 0.08f);
            RenderSettings.skybox = null;

            var labMats = new MatDef[]
            {
                new MatDef("Lab_Chao", new Color(0.10f, 0.17f, 0.12f), false, false, 0.05f),
                new MatDef("Lab_Casa", new Color(0.45f, 0.45f, 0.47f), false, true, 0.1f),
                new MatDef("Lab_Ator", new Color(0.85f, 0.75f, 0.6f), false, true, 0.2f),
            };

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Chao";
            ground.transform.localScale = new Vector3(9f, 1f, 9f);
            ground.GetComponent<Renderer>().sharedMaterial = GetMaterial(labMats[0], false);

            var house = GameObject.CreatePrimitive(PrimitiveType.Cube);
            house.name = "Casa_Placeholder";
            house.transform.position = new Vector3(0f, 1.35f, 0f);
            house.transform.localScale = new Vector3(11.4f, 2.7f, 12.4f);
            house.GetComponent<Renderer>().sharedMaterial = GetMaterial(labMats[1], false);

            for (int i = 0; i < 2; i++)
            {
                var actor = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                actor.name = $"Ator_Teste_{i + 1}";
                actor.transform.position = new Vector3(i == 0 ? -0.9f : 0.9f, 0.9f, -8.5f);
                actor.transform.localScale = new Vector3(0.6f, 0.9f, 0.6f);
                Object.DestroyImmediate(actor.GetComponent<Collider>());
                actor.GetComponent<Renderer>().sharedMaterial = GetMaterial(labMats[2], false);
            }

            var moon = new GameObject("Lua");
            var ml = moon.AddComponent<Light>();
            ml.type = LightType.Directional;
            ml.intensity = 0.9f;
            ml.color = new Color(0.55f, 0.65f, 1f);
            ml.shadows = LightShadows.Soft;
            moon.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

            var porch = new GameObject("Luz_Varanda");
            var pl = porch.AddComponent<Light>();
            pl.type = LightType.Point;
            pl.color = new Color(1f, 0.8f, 0.5f);
            pl.range = 6f;
            pl.intensity = 2f;
            porch.transform.position = new Vector3(0f, 2.4f, -6.6f);

            var camGo = new GameObject("Camera_Lab");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = RenderSettings.fogColor;
            cam.fieldOfView = 50f;
            cam.farClipPlane = 300f;
            camGo.transform.position = new Vector3(-14f, 18f, -16f);
            camGo.transform.LookAt(new Vector3(0f, 0f, -1f));

            var world = new GameObject("Mundo");
            Build(world.transform);

            EditorSceneManager.SaveScene(scene, LabScenePath);
        }

        // ================================================================== Geometria

        /// <summary>
        /// Acumula triângulos de UM material. No fim vira uma única malha (como um vertex buffer do GM).
        /// A orientação de cada triângulo é corrigida usando um ponto "de dentro" do sólido.
        /// </summary>
        private sealed class MeshBuilder
        {
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Vector3> N = new List<Vector3>();
            public readonly List<int> T = new List<int>();

            /// <summary>Triângulo facetado; vira a face para fora de 'inside'.</summary>
            public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 inside)
            {
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (n.sqrMagnitude < 1e-12f) return;
                if (Vector3.Dot(n, (a + b + c) / 3f - inside) < 0f)
                {
                    Vector3 t = b; b = c; c = t;
                    n = -n;
                }
                n.Normalize();
                Add(a, b, c, n, n, n);
            }

            /// <summary>Triângulo com normais suaves (bolas de folhagem).</summary>
            public void TriSmooth(Vector3 a, Vector3 b, Vector3 c, Vector3 na, Vector3 nb, Vector3 nc, Vector3 inside)
            {
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (n.sqrMagnitude < 1e-12f) return;
                if (Vector3.Dot(n, (a + b + c) / 3f - inside) < 0f)
                {
                    Add(a, c, b, na, nc, nb);
                    return;
                }
                Add(a, b, c, na, nb, nc);
            }

            /// <summary>Triângulo de dupla face (grama, rosto da abóbora).</summary>
            public void TriBoth(Vector3 a, Vector3 b, Vector3 c)
            {
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (n.sqrMagnitude < 1e-12f) return;
                n.Normalize();
                Add(a, b, c, n, n, n);
                Add(a, c, b, -n, -n, -n);
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 inside)
            {
                Tri(a, b, c, inside);
                Tri(a, c, d, inside);
            }

            private void Add(Vector3 a, Vector3 b, Vector3 c, Vector3 na, Vector3 nb, Vector3 nc)
            {
                int i = V.Count;
                V.Add(a); V.Add(b); V.Add(c);
                N.Add(na); N.Add(nb); N.Add(nc);
                T.Add(i); T.Add(i + 1); T.Add(i + 2);
            }
        }

        /// <summary>Tronco de cone (cilindro afinando) entre p0 e p1. r1 = 0 vira cone.</summary>
        private static void Frustum(MeshBuilder m, Vector3 p0, Vector3 p1, float r0, float r1, int seg, float phase, bool capBottom, bool capTop)
        {
            Vector3 axis = p1 - p0;
            float len = axis.magnitude;
            if (len < 1e-4f) return;
            axis /= len;
            Vector3 refv = Mathf.Abs(axis.y) < 0.95f ? Vector3.up : Vector3.right;
            Vector3 u = Vector3.Cross(axis, refv).normalized;
            Vector3 w = Vector3.Cross(axis, u);
            Vector3 inside = (p0 + p1) * 0.5f;
            var a = new Vector3[seg];
            var b = new Vector3[seg];
            for (int i = 0; i < seg; i++)
            {
                float ang = phase + i * Mathf.PI * 2f / seg;
                Vector3 d = u * Mathf.Cos(ang) + w * Mathf.Sin(ang);
                a[i] = p0 + d * r0;
                b[i] = p1 + d * r1;
            }
            bool tip = r1 < 0.002f;
            for (int i = 0; i < seg; i++)
            {
                int j = (i + 1) % seg;
                if (tip) m.Tri(a[i], a[j], p1, inside);
                else m.Quad(a[i], a[j], b[j], b[i], inside);
                if (capBottom) m.Tri(p0, a[j], a[i], inside);
                if (capTop && !tip) m.Tri(p1, b[i], b[j], inside);
            }
        }

        /// <summary>Cone com base horizontal (andar de pinheiro), ápice livre, borda levemente irregular.</summary>
        private static void Cone(MeshBuilder m, Vector3 baseCenter, float r, Vector3 apex, int seg, float phase, float wobble, System.Random rng)
        {
            Vector3 inside = baseCenter + (apex - baseCenter) * 0.25f;
            var ring = new Vector3[seg];
            for (int i = 0; i < seg; i++)
            {
                float ang = phase + i * Mathf.PI * 2f / seg;
                float rr = r * (1f + ((float)rng.NextDouble() * 2f - 1f) * wobble);
                float dy = ((float)rng.NextDouble() * 2f - 1f) * wobble * r * 0.4f;
                ring[i] = baseCenter + new Vector3(Mathf.Cos(ang) * rr, dy, Mathf.Sin(ang) * rr);
            }
            for (int i = 0; i < seg; i++)
            {
                int j = (i + 1) % seg;
                m.Tri(ring[i], ring[j], apex, inside);
                m.Tri(baseCenter, ring[j], ring[i], inside);
            }
        }

        /// <summary>Caixa (usa o prisma com perfil retangular).</summary>
        private static void Box(MeshBuilder m, Vector3 center, Vector3 size, Quaternion rot)
        {
            float x = size.x * 0.5f, y = size.y * 0.5f;
            var prof = new[] { new Vector2(-x, -y), new Vector2(x, -y), new Vector2(x, y), new Vector2(-x, y) };
            Prism(m, prof, size.z, Matrix4x4.TRS(center, rot, Vector3.one));
        }

        /// <summary>Extruda um perfil 2D CONVEXO (no plano XY local) com espessura 'depth' em Z.</summary>
        private static void Prism(MeshBuilder m, Vector2[] prof, float depth, Matrix4x4 M)
        {
            int n = prof.Length;
            Vector2 cen = Vector2.zero;
            foreach (var p in prof) cen += p;
            cen /= n;
            float hz = depth * 0.5f;
            Vector3 inside = M.MultiplyPoint3x4(new Vector3(cen.x, cen.y, 0f));
            var f = new Vector3[n];
            var b = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                f[i] = M.MultiplyPoint3x4(new Vector3(prof[i].x, prof[i].y, -hz));
                b[i] = M.MultiplyPoint3x4(new Vector3(prof[i].x, prof[i].y, hz));
            }
            Vector3 fc = M.MultiplyPoint3x4(new Vector3(cen.x, cen.y, -hz));
            Vector3 bc = M.MultiplyPoint3x4(new Vector3(cen.x, cen.y, hz));
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                m.Quad(f[i], f[j], b[j], b[i], inside);
                m.Tri(fc, f[i], f[j], inside);
                m.Tri(bc, b[j], b[i], inside);
            }
        }

        /// <summary>
        /// "Bola" de icosfera achatada/esticada, com vértices tremidos (visual orgânico).
        /// 'lobes' &gt; 0 cria gomos verticais (abóbora).
        /// </summary>
        private static void Blob(MeshBuilder m, Vector3 c, Vector3 radii, Quaternion rot, float jitter, float lobes, System.Random rng)
        {
            Vector3[] uv = Ico.Verts;
            var pos = new Vector3[uv.Length];
            var nor = new Vector3[uv.Length];
            for (int i = 0; i < uv.Length; i++)
            {
                Vector3 u = uv[i];
                float k = 1f + ((float)rng.NextDouble() * 2f - 1f) * jitter;
                if (lobes > 0f) k *= 1f + lobes * Mathf.Cos(8f * Mathf.Atan2(u.z, u.x)) * (1f - Mathf.Abs(u.y));
                pos[i] = c + rot * (Vector3.Scale(u, radii) * k);
                nor[i] = (rot * new Vector3(u.x / radii.x, u.y / radii.y, u.z / radii.z)).normalized;
            }
            int[] t = Ico.Tris;
            for (int i = 0; i < t.Length; i += 3)
            {
                m.TriSmooth(pos[t[i]], pos[t[i + 1]], pos[t[i + 2]], nor[t[i]], nor[t[i + 1]], nor[t[i + 2]], c);
            }
        }

        /// <summary>Icosfera unitária (1 subdivisão: 42 vértices, 80 triângulos), calculada uma vez.</summary>
        private static class Ico
        {
            public static readonly Vector3[] Verts;
            public static readonly int[] Tris;

            static Ico()
            {
                float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
                var v = new List<Vector3>
                {
                    new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                    new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                    new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
                };
                for (int i = 0; i < v.Count; i++) v[i] = v[i].normalized;
                int[] f =
                {
                    0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                    3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
                };
                var cache = new Dictionary<long, int>();
                System.Func<int, int, int> mid = (a, b) =>
                {
                    long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    int idx;
                    if (cache.TryGetValue(key, out idx)) return idx;
                    v.Add(((v[a] + v[b]) * 0.5f).normalized);
                    idx = v.Count - 1;
                    cache[key] = idx;
                    return idx;
                };
                var tris = new List<int>();
                for (int i = 0; i < f.Length; i += 3)
                {
                    int a = f[i], b = f[i + 1], c = f[i + 2];
                    int ab = mid(a, b), bc = mid(b, c), ca = mid(c, a);
                    tris.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                Verts = v.ToArray();
                Tris = tris.ToArray();
            }
        }
    }
}
