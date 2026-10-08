using System.Collections.Generic;
using HorrorTycoon.Art;
using HorrorTycoon.Rooms.Generation;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HorrorTycoon.Rooms.Building
{
    /// <summary>
    /// MONTAGEM 3D da casa gerada, em tempo de execução (a casa muda a cada run).
    /// O RunPresenter chama Build(Run.Layout) ANTES de ligar os RoomAnchors e de calcular o NavMesh.
    ///
    /// Por espaço (índice = índice em FilmRun.Rooms):
    ///   raiz com RoomAnchor (porta principal + direção para fora), fundação, piso (colisor: clique e NavMesh),
    ///   "Interior" girado para a convenção do FurnitureKit (porta no +X local), névoa (só Salas) e lâmpada.
    /// Paredes: layout.Walls (cada trecho uma vez). Porta = vão + verga + batente; Passagem = vão de altura inteira.
    /// Toda parede/verga ganha WallCutaway; só paredes inteiras têm colisor.
    /// Fachada: janelas (acendem com o cômodo), beiral, varanda e caminho de terra até o portão.
    ///
    /// Arte: tudo vem do HouseArtKit (materiais/prefabs), do FurnitureKit ou do RoomDef.interiorPrefab.
    /// Mundo: HouseWorldMap (fachada da frente em z = frontZ; pegada centrada em x = 0). Publica HouseBounds.
    /// </summary>
    public class HouseBuilder : MonoBehaviour
    {
        /// <summary>Máscara com todas as camadas de luz usadas (Default + 7 grupos), igual ao P0.</summary>
        public const uint AllLayers = 0xFFu;
        /// <summary>Grupos de luz por espaço (bits 1..7, como os slots do P0).</summary>
        public const int LightGroupCount = 7;

        public static uint GroupLayer(int group) => 1u << (group + 1);

        private static readonly Color LampWarm = new Color(1f, 0.702f, 0.361f);    // #FFB35C
        private static readonly Color WallColor = new Color(0.369f, 0.314f, 0.408f); // #5E5068
        private static readonly Color DarkWood = new Color(0.290f, 0.200f, 0.157f);  // #4A3328
        private static readonly Color Shingle = new Color(0.165f, 0.133f, 0.212f);   // #2A2236
        private static readonly Color Foundation = new Color(0.227f, 0.204f, 0.251f); // #3A3440

        [SerializeField] private HouseArtKit artKit;

        [Header("Posição no mundo")]
        [Tooltip("z do mundo da fachada da frente (linha y = 0 da planta, onde fica a porta da frente).")]
        [SerializeField] private float frontZ = -10f;
        [Tooltip("Centraliza a pegada real da casa em x = 0 (senão centraliza o retângulo máximo).")]
        [SerializeField] private bool centerFootprintX = true;
        [Tooltip("Fim do caminho de terra (x, z do mundo): o portão da cerca.")]
        [SerializeField] private Vector2 pathEnd = new Vector2(0f, -19f);

        private sealed class PieceInfo
        {
            public WallCutaway Cut;
            public int Wall;
            public bool AlongX;
            public float Line;      // coordenada fixa (mundo)
            public float From, To;  // ao longo da parede (mundo)
            public bool Lintel;
            public bool Exterior;
            public float OutSign;   // externas: +1/-1 no eixo da normal (para fora da casa)
        }

        private HouseArtKit kit;
        private Transform root, wallsRoot, decorRoot;
        private readonly List<RoomAnchor> anchors = new List<RoomAnchor>();
        private readonly Dictionary<int, List<Renderer>> windowsBySpace = new Dictionary<int, List<Renderer>>();
        private readonly Dictionary<int, List<Renderer>> glowsBySpace = new Dictionary<int, List<Renderer>>();
        private readonly List<PieceInfo> pieces = new List<PieceInfo>();
        private readonly Dictionary<long, Material> fallbackMats = new Dictionary<long, Material>();
        private float[] interiorYaw = new float[0];
        private int[] lightGroups = new int[0];
        private bool publishedBounds;

        public HouseLayout Layout { get; private set; }
        public HouseWorldMap Map { get; private set; }
        public IReadOnlyList<RoomAnchor> Anchors => anchors;
        public HouseArtKit ArtKit => artKit;

        /// <summary>Usado pelo construtor de cena (editor).</summary>
        public void Configure(HouseArtKit kitAsset, float frontLineZ, Vector2 pathEndXZ)
        {
            artKit = kitAsset;
            frontZ = frontLineZ;
            pathEnd = pathEndXZ;
        }

        private void OnDestroy()
        {
            if (publishedBounds) HouseBounds.Clear();
        }

        // ================================================================== Entrada

        /// <summary>Monta a casa inteira. Chamar uma vez por run, antes de ligar os RoomAnchors e do NavMesh.</summary>
        public void Build(HouseLayout layout)
        {
            Clear();
            if (layout == null) return;

            kit = artKit != null ? artKit : ScriptableObject.CreateInstance<HouseArtKit>();
            Layout = layout;
            Map = new HouseWorldMap(layout, frontZ, centerFootprintX);
            int n = layout.Spaces.Count;
            interiorYaw = new float[n];
            lightGroups = HouseWorldMap.LightGroups(layout, kit.lampRange, LightGroupCount);

            // Monta tudo DESLIGADO: o Awake do RoomAnchor lê a lâmpada e os brilhos, então só pode rodar
            // depois de configurado (ao ligar a raiz no fim).
            root = new GameObject("Casa (gerada)").transform;
            root.gameObject.SetActive(false);
            wallsRoot = new GameObject("Paredes").transform;
            wallsRoot.SetParent(root, false);
            decorRoot = new GameObject("Enfeites (somem com o corte)").transform;
            decorRoot.SetParent(root, false);

            for (int i = 0; i < n; i++) BuildSpace(i);
            BuildWalls();
            BuildWindows();
            if (kit.eaves) BuildEaves();
            BuildPorchAndPath();
            MarkSeeThrough(wallsRoot);
            MarkSeeThrough(decorRoot);

            foreach (var anchor in anchors)
            {
                int i = anchor.SlotIndex;
                windowsBySpace.TryGetValue(i, out var windows);
                glowsBySpace.TryGetValue(i, out var glows);
                anchor.ConfigureVisuals(windows != null ? windows.ToArray() : null, glows != null ? glows.ToArray() : null);
            }

            root.gameObject.SetActive(true);

            Vector3 door = layout.FrontDoor != null ? Map.ToWorld(layout.FrontDoor.Position) : new Vector3(0f, 0f, frontZ);
            HouseBounds.Set(Map.FootprintWorld, door);
            publishedBounds = true;
        }

        /// <summary>
        /// Buraco de visão (SeeThroughTargets): paredes, vergas, batentes, janelas, beiral e varanda ganham a keyword
        /// _SEETHROUGH no material. Pisos/caminho/deque/fundação nunca (o ator pisa neles).
        /// </summary>
        private static void MarkSeeThrough(Transform parent)
        {
            if (parent == null) return;
            foreach (var r in parent.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null) continue;
                    string n = m.name;
                    if (n.Contains("Piso") || n.Contains("Caminho") || n.Contains("Deque") || n.Contains("Fundacao")) continue;
                    ToonMaterials.SetSeeThrough(m, true);
                }
            }
        }

        /// <summary>Apaga a casa montada (se houver).</summary>
        public void Clear()
        {
            if (root != null)
            {
                root.gameObject.SetActive(false); // some já das buscas (FindObjectsByType ignora inativos)
                Destroy(root.gameObject);
            }
            root = wallsRoot = decorRoot = null;
            anchors.Clear();
            windowsBySpace.Clear();
            glowsBySpace.Clear();
            pieces.Clear();
            Layout = null;
            Map = null;
        }

        public RoomAnchor AnchorOf(int space)
        {
            foreach (var a in anchors) if (a != null && a.SlotIndex == space) return a;
            return null;
        }

        // ================================================================== Espaços

        private void BuildSpace(int i)
        {
            var s = Layout.Spaces[i];
            Vector3 center = Map.SpaceCenter(i);
            var size = new Vector2(s.Rect.width, s.Rect.height);
            uint layer = GroupLayer(lightGroups[i]);
            bool isStart = i == Layout.StartSpaceIndex;
            string label = s.Def != null ? s.Def.DisplayName : s.Kind.ToString();

            var go = new GameObject($"Espaco_{i:00} ({label})");
            go.transform.SetParent(root, false);
            go.transform.localPosition = center;

            var slab = Primitive(PrimitiveType.Cube, go.transform, "Fundacao", new Vector3(0f, -0.1f, 0f),
                new Vector3(size.x + 0.2f, 0.2f, size.y + 0.2f), Mat(kit.foundationMaterial, Foundation), false);
            slab.GetComponent<Renderer>().renderingLayerMask = AllLayers;

            // Piso com colisor: o clique acha o RoomAnchor por ele e o NavMesh anda sobre ele.
            var floor = Primitive(PrimitiveType.Cube, go.transform, "Piso", new Vector3(0f, 0.01f, 0f),
                new Vector3(size.x - 0.02f, 0.02f, size.y - 0.02f), Mat(kit.FloorFor(s.Kind), Color.white), true);
            var floorRenderer = floor.GetComponent<Renderer>();
            // Camada de luz do espaço; o inicial também pega a luz da varanda (Default).
            floorRenderer.renderingLayerMask = isStart ? (layer | 1u) : layer;

            // Interior: girado para a porta principal ficar no +X local (convenção do FurnitureKit).
            var primary = HouseWorldMap.PrimaryConnection(Layout, i);
            Vector2Int outward = primary != null ? HouseWorldMap.Outward(Layout, i, primary) : new Vector2Int(0, -1);
            bool hasPrefab = s.Def != null && s.Def.InteriorPrefab != null;
            float yaw = hasPrefab ? HouseWorldMap.AuthoredYaw(s, outward) : HouseWorldMap.FurnitureYaw(outward);
            interiorYaw[i] = yaw;
            var interior = new GameObject("Interior").transform;
            interior.SetParent(go.transform, false);
            interior.localRotation = Quaternion.Euler(0f, yaw, 0f);

            GameObject fog = s.Kind == SpaceKind.Room ? BuildMist(go.transform, size) : null;

            var lampGo = new GameObject("Lampada");
            lampGo.transform.SetParent(go.transform, false);
            lampGo.transform.localPosition = new Vector3(0f, kit.lampHeight, 0f);
            var light = lampGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = Mathf.Max(kit.lampRange, Mathf.Max(size.x, size.y) * 0.5f + 2f);
            light.intensity = s.Kind == SpaceKind.Corridor ? kit.corridorLampIntensity : kit.lampIntensity;
            light.color = LampWarm;
            light.shadows = LightShadows.None;
            light.GetUniversalAdditionalLightData().renderingLayers = layer;
            glowsBySpace[i] = BuildLamp(lampGo.transform, layer);

            var anchor = go.AddComponent<RoomAnchor>();
            anchor.Configure(i, size, floorRenderer, interior, fog, light);
            if (primary != null)
            {
                Vector3 door = Map.ToWorld(primary.Position);
                anchor.ConfigureDoor(door - center, HouseWorldMap.OutwardWorld(outward));
            }
            int index = i;
            anchor.InteriorOverride = (a, parent, seed) => BuildInterior(index, parent, seed);
            anchors.Add(anchor);
        }

        /// <summary>
        /// Monta o interior (chamado pelo RoomAnchor.Bind, que já sabe o cômodo e a seed visual).
        /// Prefab do RoomDef > corredor (passadeira) > FurnitureKit pelo estilo do RoomDef.
        /// Depois tira da frente de TODAS as portas/passagens o que estiver no caminho.
        /// </summary>
        private void BuildInterior(int index, Transform interior, int seed)
        {
            if (Layout == null || index < 0 || index >= Layout.Spaces.Count) return;
            var s = Layout.Spaces[index];
            interior.localScale = Vector3.one;
            int before = interior.childCount;
            var created = new List<Transform>();

            if (s.Def != null && s.Def.InteriorPrefab != null)
            {
                var inst = Instantiate(s.Def.InteriorPrefab, interior, false);
                inst.name = s.Def.InteriorPrefab.name;
                if (inst.transform.childCount > 0)
                {
                    foreach (Transform t in inst.transform) created.Add(t);
                }
                else created.Add(inst.transform);
            }
            else
            {
                Vector2 local = HouseWorldMap.LocalSize(s.Rect, interiorYaw[index]);
                if (s.Kind == SpaceKind.Corridor) FurnitureKit.BuildPassage(interior, local, seed);
                else FurnitureKit.Build(s.Def != null ? s.Def.Furniture : FurnitureStyle.None, interior, local, seed);
                for (int c = before; c < interior.childCount; c++) created.Add(interior.GetChild(c));
            }

            if (kit.clearDoorways && s.Kind != SpaceKind.Corridor) ClearDoorways(index, interior, created);
        }

        /// <summary>
        /// Móveis na frente de portas: tenta espelhar o interior em Z (a porta pode estar fora do meio da parede);
        /// fica com o lado que bloqueia menos e remove o que ainda bloquear. Peças rentes ao chão (tapete, fita) ficam.
        /// </summary>
        private void ClearDoorways(int index, Transform interior, List<Transform> items)
        {
            var boxes = new List<Rect>();
            foreach (var c in Layout.ConnectionsOf(index))
            {
                Vector3 g = Map.ToWorld(c.Position);
                Vector3 along = c.HorizontalWall ? Vector3.right : Vector3.forward;
                Vector3 inward = -HouseWorldMap.OutwardWorld(HouseWorldMap.Outward(Layout, index, c));
                float half = c.Width * 0.5f + 0.15f;
                Vector3 a = interior.InverseTransformPoint(g - along * half);
                Vector3 b = interior.InverseTransformPoint(g + along * half + inward * kit.doorwayClearDepth);
                boxes.Add(Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.z, b.z), Mathf.Max(a.x, b.x), Mathf.Max(a.z, b.z)));
            }
            if (boxes.Count == 0) return;

            var blockers = new List<Transform>();
            var bounds = new List<Rect>();
            foreach (var t in items)
            {
                if (t == null || !LocalBounds(interior, t, out Bounds lb) || lb.max.y < 0.12f) continue;
                blockers.Add(t);
                bounds.Add(Rect.MinMaxRect(lb.min.x, lb.min.z, lb.max.x, lb.max.z));
            }

            int normal = 0, mirrored = 0;
            for (int k = 0; k < bounds.Count; k++)
            {
                if (Blocks(bounds[k], boxes, false)) normal++;
                if (Blocks(bounds[k], boxes, true)) mirrored++;
            }
            bool mirror = mirrored < normal;
            if (mirror) interior.localScale = new Vector3(1f, 1f, -1f);

            for (int k = 0; k < bounds.Count; k++)
            {
                if (Blocks(bounds[k], boxes, mirror)) Destroy(blockers[k].gameObject);
            }
        }

        private static bool Blocks(Rect item, List<Rect> boxes, bool mirror)
        {
            if (mirror) item = Rect.MinMaxRect(item.xMin, -item.yMax, item.xMax, -item.yMin);
            foreach (var b in boxes) if (item.Overlaps(b)) return true;
            return false;
        }

        /// <summary>Caixa (no espaço local do interior, sem espelho) que contém todos os renderers do item.</summary>
        private static bool LocalBounds(Transform interior, Transform item, out Bounds local)
        {
            local = default;
            bool any = false;
            foreach (var r in item.GetComponentsInChildren<Renderer>())
            {
                Bounds wb = r.bounds;
                Vector3 mn = wb.min, mx = wb.max;
                for (int k = 0; k < 8; k++)
                {
                    var p = new Vector3((k & 1) == 0 ? mn.x : mx.x, (k & 2) == 0 ? mn.y : mx.y, (k & 4) == 0 ? mn.z : mx.z);
                    Vector3 lp = interior.InverseTransformPoint(p);
                    if (!any) { local = new Bounds(lp, Vector3.zero); any = true; }
                    else local.Encapsulate(lp);
                }
            }
            return any;
        }

        // ================================================================== Névoa e lâmpada

        /// <summary>Cômodo não descoberto: volume de névoa + 2 camadas + fita crepe apagada (igual à casa fixa).</summary>
        private GameObject BuildMist(Transform parent, Vector2 size)
        {
            var volMat = FxMat(kit.fogVolumeMaterial, new Color(0.135f, 0.15f, 0.23f, 0.8f));
            var layerMat = FxMat(kit.fogLayerMaterial, new Color(0.17f, 0.18f, 0.27f, 0.55f));
            var tapeMat = FxMat(kit.fogTapeMaterial, new Color(0.788f, 0.706f, 0.345f, 0.25f));
            if (volMat == null) return null;

            var mist = new GameObject("Nevoa");
            mist.transform.SetParent(parent, false);

            var vol = Primitive(PrimitiveType.Cube, mist.transform, "Nevoa_Volume", new Vector3(0f, 0.75f, 0f),
                new Vector3(size.x - 0.14f, 1.5f, size.y - 0.14f), volMat, false);
            NoShadow(vol);

            float[] heights = { 0.3f, 0.85f };
            for (int i = 0; i < heights.Length; i++)
            {
                var q = Primitive(PrimitiveType.Quad, mist.transform, "Nevoa_Camada", new Vector3(0f, heights[i], 0f),
                    i == 0 ? new Vector3(size.x - 0.2f, size.y - 0.2f, 1f) : new Vector3(size.y - 0.2f, size.x - 0.2f, 1f), layerMat ?? volMat, false);
                q.transform.localRotation = Quaternion.Euler(90f, i * 90f, 0f);
                NoShadow(q);
            }

            if (tapeMat != null)
            {
                float hx = size.x * 0.5f - 0.45f, hz = size.y * 0.5f - 0.45f;
                Tape(mist.transform, new Vector3(0f, 0.025f, -hz), new Vector2(hx * 2f, 0.05f), 0f, tapeMat);
                Tape(mist.transform, new Vector3(0f, 0.025f, hz), new Vector2(hx * 2f, 0.05f), 0f, tapeMat);
                Tape(mist.transform, new Vector3(-hx, 0.025f, 0f), new Vector2(0.05f, hz * 2f), 0f, tapeMat);
                Tape(mist.transform, new Vector3(hx, 0.025f, 0f), new Vector2(0.05f, hz * 2f), 0f, tapeMat);
                Tape(mist.transform, new Vector3(0.2f, 0.026f, 0.1f), new Vector2(0.4f, 0.05f), 45f, tapeMat);
                Tape(mist.transform, new Vector3(0.2f, 0.026f, 0.1f), new Vector2(0.4f, 0.05f), -45f, tapeMat);
            }
            return mist;
        }

        private static void Tape(Transform parent, Vector3 pos, Vector2 size, float yaw, Material mat)
        {
            var q = Primitive(PrimitiveType.Quad, parent, "Fita_Apagada", pos, new Vector3(size.x, size.y, 1f), mat, false);
            q.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(90f, 0f, 0f);
            NoShadow(q);
        }

        /// <summary>Luminária do teto (prefab do kit ou fio + cúpula + bulbo + raio de luz). Devolve os "brilhos".</summary>
        private List<Renderer> BuildLamp(Transform lamp, uint layer)
        {
            var glows = new List<Renderer>();
            if (kit.lampPrefab != null)
            {
                var inst = Instantiate(kit.lampPrefab, lamp, false);
                foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
                {
                    r.renderingLayerMask = layer;
                    glows.Add(r);
                }
                return glows;
            }

            var cord = Primitive(PrimitiveType.Cube, lamp, "Fio", new Vector3(0f, 0.17f, 0f), new Vector3(0.015f, 0.3f, 0.015f),
                Mat(kit.lampCordMaterial, new Color(0.082f, 0.071f, 0.102f)), false);
            var cordR = NoShadow(cord);
            cordR.renderingLayerMask = layer;
            glows.Add(cordR);

            var shade = Primitive(PrimitiveType.Cylinder, lamp, "Cupula", new Vector3(0f, 0.02f, 0f), new Vector3(0.26f, 0.05f, 0.26f),
                Emissive(kit.lampShadeMaterial, new Color(0.541f, 0.416f, 0.251f), new Color(0.3f, 0.18f, 0.07f)), false);
            var shadeR = NoShadow(shade);
            shadeR.renderingLayerMask = layer;
            glows.Add(shadeR);

            var bulb = Primitive(PrimitiveType.Sphere, lamp, "Bulbo", new Vector3(0f, -0.07f, 0f), Vector3.one * 0.13f,
                Emissive(kit.lampBulbMaterial, new Color(1f, 0.886f, 0.69f), new Color(2.4f, 1.7f, 0.9f)), false);
            glows.Add(NoShadow(bulb));

            if (kit.lampShaftMesh != null && kit.lampShaftMaterial != null)
            {
                var shaft = new GameObject("Raio_de_luz");
                shaft.transform.SetParent(lamp, false);
                float h = kit.lampHeight - 0.1f;
                shaft.transform.localPosition = new Vector3(0f, -0.1f - h * 0.5f, 0f);
                shaft.transform.localScale = new Vector3(2.4f, h, 2.4f);
                shaft.AddComponent<MeshFilter>().sharedMesh = kit.lampShaftMesh;
                var sr = shaft.AddComponent<MeshRenderer>();
                sr.sharedMaterial = kit.lampShaftMaterial;
                sr.shadowCastingMode = ShadowCastingMode.Off;
                sr.receiveShadows = false;
                glows.Add(sr);
            }
            return glows;
        }

        // ================================================================== Paredes

        private void BuildWalls()
        {
            for (int w = 0; w < Layout.Walls.Count; w++)
            {
                var wall = Layout.Walls[w];
                float line = Map.WallLineWorld(wall);
                float outSign = 0f;
                if (wall.IsExterior)
                {
                    Vector2Int o = HouseWorldMap.WallOutward(Layout, wall.A, wall);
                    outSign = wall.Horizontal ? o.y : o.x;
                }

                PieceInfo lintel = null;
                foreach (var p in HouseWorldMap.Pieces(Layout, wall, kit.wallHeight, kit.doorHeight, kit.maxWallPiece))
                {
                    var info = CreatePiece(w, wall, line, p, outSign);
                    if (info != null && info.Lintel) lintel = info;
                }

                if (HouseWorldMap.GapOf(Layout, wall, out float g0, out float g1, out ConnectionType type) && type == ConnectionType.Door)
                {
                    float center = Map.AlongWorld(wall.Horizontal, (g0 + g1) * 0.5f);
                    DoorFrame(wall.Horizontal, line, center, g1 - g0, lintel);
                }
            }
        }

        private PieceInfo CreatePiece(int wallIndex, HouseWall wall, float line, WallPiece p, float outSign)
        {
            bool alongX = wall.Horizontal;
            float from = Map.AlongWorld(alongX, p.From), to = Map.AlongWorld(alongX, p.To);
            float length = to - from;
            if (length <= 0.01f) return null;

            float mid = (from + to) * 0.5f;
            float thickness = kit.wallThickness;
            // Paredes inteiras ganham meia espessura em cada ponta para fechar os cantos (igual à casa fixa).
            float extra = p.Lintel ? 0f : thickness;
            Material wallMat = Mat(kit.wallMaterial, WallColor);
            Material mat = p.Lintel ? (kit.lintelMaterial != null ? kit.lintelMaterial : wallMat)
                         : wall.IsExterior && kit.exteriorWallMaterial != null ? kit.exteriorWallMaterial : wallMat;

            var go = Primitive(PrimitiveType.Cube, wallsRoot, p.Lintel ? "Verga" : "Parede",
                alongX ? new Vector3(mid, p.Bottom + p.Height * 0.5f, line) : new Vector3(line, p.Bottom + p.Height * 0.5f, mid),
                alongX ? new Vector3(length + extra, p.Height, thickness) : new Vector3(thickness, p.Height, length + extra),
                mat, !p.Lintel); // verga sem colisor: não atrapalha o NavMesh
            // Externas recebem as luzes de fora (varanda, refletores); internas só as lâmpadas.
            go.GetComponent<Renderer>().renderingLayerMask = wall.IsExterior ? AllLayers : (AllLayers & ~1u);

            var cut = go.AddComponent<WallCutaway>();
            cut.Setup(alongX ? Vector3.forward : Vector3.right, p.Height, p.Bottom, p.Lintel);

            var info = new PieceInfo
            {
                Cut = cut, Wall = wallIndex, AlongX = alongX, Line = line, From = from, To = to,
                Lintel = p.Lintel, Exterior = wall.IsExterior, OutSign = outSign,
            };
            pieces.Add(info);
            return info;
        }

        /// <summary>Batente: prefab do kit (preso à verga) ou dois montantes + travessa (cortam junto com a parede).</summary>
        private void DoorFrame(bool alongX, float line, float center, float doorWidth, PieceInfo lintel)
        {
            float doorH = kit.doorHeight;
            if (kit.doorFramePrefab != null)
            {
                var inst = Instantiate(kit.doorFramePrefab, decorRoot, false);
                inst.transform.SetPositionAndRotation(alongX ? new Vector3(center, 0f, line) : new Vector3(line, 0f, center),
                    Quaternion.Euler(0f, alongX ? 0f : 90f, 0f));
                Vector3 s = inst.transform.localScale;
                inst.transform.localScale = new Vector3(s.x * doorWidth, s.y, s.z);
                lintel?.Cut.AddAttachment(inst);
                return;
            }

            var mat = Mat(kit.trimMaterial, DarkWood);
            const float jamb = 0.1f, depth = 0.2f;
            for (int side = -1; side <= 1; side += 2)
            {
                float along = center + side * (doorWidth * 0.5f + jamb * 0.5f - 0.02f);
                var j = Primitive(PrimitiveType.Cube, wallsRoot, "Batente",
                    alongX ? new Vector3(along, doorH * 0.5f, line) : new Vector3(line, doorH * 0.5f, along),
                    alongX ? new Vector3(jamb, doorH, depth) : new Vector3(depth, doorH, jamb), mat, false);
                j.GetComponent<Renderer>().renderingLayerMask = AllLayers;
                j.AddComponent<WallCutaway>().Setup(alongX ? Vector3.forward : Vector3.right, doorH, 0f, false);
            }
            float w = doorWidth + jamb * 2f - 0.04f;
            var head = Primitive(PrimitiveType.Cube, wallsRoot, "Batente_Topo",
                alongX ? new Vector3(center, doorH + 0.06f, line) : new Vector3(line, doorH + 0.06f, center),
                alongX ? new Vector3(w, 0.12f, depth) : new Vector3(depth, 0.12f, w), mat, false);
            head.GetComponent<Renderer>().renderingLayerMask = AllLayers;
            head.AddComponent<WallCutaway>().Setup(alongX ? Vector3.forward : Vector3.right, 0.12f, doorH, true);
        }

        private PieceInfo FindPiece(bool alongX, float line, float along, bool lintel)
        {
            foreach (var p in pieces)
            {
                if (p.AlongX != alongX || p.Lintel != lintel) continue;
                if (Mathf.Abs(p.Line - line) > 0.01f) continue;
                if (along >= p.From - 0.01f && along <= p.To + 0.01f) return p;
            }
            return null;
        }

        // ================================================================== Fachada

        /// <summary>Janelas nas paredes externas sólidas (≥ 2 m) de Salas e Convivências, sorteadas pela seed da casa.</summary>
        private void BuildWindows()
        {
            var rng = new System.Random(Layout.Seed ^ 0x51D0);
            foreach (var wall in Layout.Walls)
            {
                if (!wall.IsExterior || wall.ConnectionIndex >= 0 || wall.Length < 2) continue;
                var s = Layout.Spaces[wall.A];
                if (s.Kind == SpaceKind.Corridor) continue;
                if (rng.NextDouble() >= kit.windowChance) continue;

                float a0 = wall.Horizontal ? wall.From.x : wall.From.y;
                float a1 = wall.Horizontal ? wall.To.x : wall.To.y;
                Vector2Int o = HouseWorldMap.WallOutward(Layout, wall.A, wall);
                Window(wall.A, wall.Horizontal, Map.WallLineWorld(wall), Map.AlongWorld(wall.Horizontal, (a0 + a1) * 0.5f),
                    wall.Horizontal ? o.y : o.x, rng);
            }
        }

        /// <summary>Janela: moldura + travessas + vidro (o vidro acende com o cômodo) + raio de luz para o quintal.</summary>
        private void Window(int space, bool alongX, float line, float along, float outward, System.Random rng)
        {
            var seg = FindPiece(alongX, line, along, false);
            const float w = 0.9f, h = 1.05f, cy = 1.5f;
            if (!windowsBySpace.TryGetValue(space, out var panes)) windowsBySpace[space] = panes = new List<Renderer>();
            Vector3 pos = alongX ? new Vector3(along, cy, line) : new Vector3(line, cy, along);

            if (kit.windowPrefab != null)
            {
                float yaw = alongX ? (outward > 0f ? 0f : 180f) : (outward > 0f ? 90f : 270f);
                var inst = Instantiate(kit.windowPrefab, decorRoot, false);
                inst.name = $"Janela_{space:00}";
                inst.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
                foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
                {
                    r.renderingLayerMask = AllLayers;
                    if (r.name.Contains("Vidro")) panes.Add(r);
                }
                seg?.Cut.AddAttachment(inst);
                return;
            }

            float tilt = (float)(rng.NextDouble() * 2.0 - 1.0) * 2.2f;
            var frameMat = Mat(kit.windowFrameMaterial, DarkWood);
            var paneMat = Emissive(kit.windowPaneMaterial, new Color(0.118f, 0.165f, 0.267f), new Color(0.02f, 0.025f, 0.05f));

            var win = new GameObject($"Janela_{space:00}");
            win.transform.SetParent(decorRoot, false);
            win.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, alongX ? 0f : 90f, 0f) * Quaternion.Euler(0f, 0f, tilt));

            panes.Add(WindowPart(win.transform, "Vidro", Vector3.zero, new Vector3(w - 0.08f, h - 0.08f, 0.14f), paneMat));
            WindowPart(win.transform, "Moldura_Topo", new Vector3(0f, h * 0.5f, 0f), new Vector3(w + 0.12f, 0.1f, 0.2f), frameMat);
            WindowPart(win.transform, "Peitoril", new Vector3(0f, -h * 0.5f - 0.02f, 0f), new Vector3(w + 0.22f, 0.1f, 0.26f), frameMat);
            WindowPart(win.transform, "Moldura_E", new Vector3(-w * 0.5f, 0f, 0f), new Vector3(0.09f, h, 0.2f), frameMat);
            WindowPart(win.transform, "Moldura_D", new Vector3(w * 0.5f, 0f, 0f), new Vector3(0.09f, h, 0.2f), frameMat);
            WindowPart(win.transform, "Travessa_V", Vector3.zero, new Vector3(0.05f, h - 0.05f, 0.17f), frameMat);
            WindowPart(win.transform, "Travessa_H", new Vector3(0f, 0.08f, 0f), new Vector3(w - 0.05f, 0.05f, 0.17f), frameMat);

            // Raio de luz falso saindo da janela (aditivo). Entra nos brilhos do cômodo: só aparece descoberto.
            if (kit.windowBeamMaterial != null)
            {
                const float theta = 38f * Mathf.Deg2Rad, length = 2.6f;
                Vector3 dirOut = new Vector3(0f, -Mathf.Sin(theta), outward * Mathf.Cos(theta));
                var beam = Primitive(PrimitiveType.Cube, win.transform, "Raio_Janela", dirOut * (length * 0.5f + 0.12f),
                    new Vector3(w - 0.1f, length, h * 0.8f), kit.windowBeamMaterial, false);
                beam.transform.localRotation = Quaternion.FromToRotation(Vector3.up, -dirOut);
                var beamR = NoShadow(beam);
                beamR.receiveShadows = false;
                if (!glowsBySpace.TryGetValue(space, out var glows)) glowsBySpace[space] = glows = new List<Renderer>();
                glows.Add(beamR);
            }
            seg?.Cut.AddAttachment(win);
        }

        private static Renderer WindowPart(Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
        {
            var go = Primitive(PrimitiveType.Cube, parent, name, pos, size, mat, false);
            var r = NoShadow(go);
            r.renderingLayerMask = AllLayers;
            return r;
        }

        /// <summary>Beiral grosso no topo de cada trecho externo (dica de telhado); some com o corte.</summary>
        private void BuildEaves()
        {
            var mat = Mat(kit.eaveMaterial, Shingle);
            var rng = new System.Random(77);
            foreach (var p in pieces.ToArray())
            {
                if (!p.Exterior) continue;
                float len = p.To - p.From + (p.Lintel ? 0f : 0.76f);
                float mid = (p.From + p.To) * 0.5f;
                float y = kit.wallHeight + 0.07f;
                Vector3 pos = p.AlongX ? new Vector3(mid, y, p.Line + p.OutSign * 0.14f) : new Vector3(p.Line + p.OutSign * 0.14f, y, mid);
                Vector3 size = p.AlongX ? new Vector3(len, 0.16f, 0.46f) : new Vector3(0.46f, 0.16f, len);
                float wobble = (float)(rng.NextDouble() * 2.0 - 1.0) * 1.2f;
                var eave = Decor("Beiral", size, 0.04f, 0f, pos, p.AlongX ? new Vector3(0f, 0f, wobble) : new Vector3(wobble, 0f, 0f), mat);
                p.Cut.AddAttachment(eave);
            }
        }

        /// <summary>Varanda na porta da frente (prefab do kit ou deque/degrau/colunas/toldo/luminária) e caminho até o portão.</summary>
        private void BuildPorchAndPath()
        {
            var front = Layout.FrontDoor;
            if (front == null) return;
            Vector3 door = Map.ToWorld(front.Position);
            PieceInfo frontLintel = null;
            foreach (var p in pieces)
            {
                if (p.Lintel && p.Wall >= 0 && Layout.Walls[p.Wall].ConnectionIndex == Layout.FrontDoorIndex) frontLintel = p;
            }

            if (kit.porch)
            {
                var porch = new GameObject("Varanda").transform;
                porch.SetParent(root, false);
                if (kit.porchPrefab != null)
                {
                    var inst = Instantiate(kit.porchPrefab, porch, false);
                    inst.transform.SetPositionAndRotation(door, Quaternion.Euler(0f, 180f, 0f)); // +Z local = rua (-Z)
                    frontLintel?.Cut.AddAttachment(inst);
                }
                else
                {
                    var deck = Mat(kit.deckMaterial, new Color(0.361f, 0.251f, 0.2f));
                    var post = Mat(kit.postMaterial, new Color(0.478f, 0.416f, 0.502f));
                    var roof = Mat(kit.eaveMaterial, Shingle);
                    Place(porch, "Deque", new Vector3(3.4f, 0.08f, 1.7f), 0.02f, 0f, door + new Vector3(0f, 0.02f, -0.91f), Vector3.zero, deck);
                    Place(porch, "Degrau", new Vector3(1.5f, 0.05f, 0.4f), 0.015f, 0f, door + new Vector3(0f, 0f, -1.95f), new Vector3(0f, 2f, 0f), deck);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        var col = Decor("Coluna", new Vector3(0.15f, 2.75f, 0.15f), 0.03f, 0f, door + new Vector3(s * 1.55f, 1.4f, -1.6f),
                            new Vector3(s * 1.2f, 0f, s * 1.8f), post);
                        frontLintel?.Cut.AddAttachment(col);
                    }
                    var awning = Decor("Toldo", new Vector3(3.7f, 0.12f, 2.0f), 0.04f, 0f, door + new Vector3(0f, kit.wallHeight + 0.2f, -0.95f),
                        new Vector3(-13f, 0f, 1.2f), roof);
                    frontLintel?.Cut.AddAttachment(awning);

                    var fixture = Decor("Luminaria_Varanda", new Vector3(0.22f, 0.3f, 0.16f), 0.04f, 0.2f, door + new Vector3(0f, 2.42f, -0.14f),
                        Vector3.zero, Mat(kit.porchFixtureMaterial, new Color(0.227f, 0.165f, 0.133f)));
                    var bulb = Decor("Luminaria_Bulbo", new Vector3(0.14f, 0.16f, 0.1f), 0.04f, 0f, door + new Vector3(0f, 2.27f, -0.17f),
                        Vector3.zero, Emissive(kit.lampBulbMaterial, new Color(1f, 0.886f, 0.69f), new Color(2.4f, 1.7f, 0.9f)));
                    frontLintel?.Cut.AddAttachment(fixture);
                    frontLintel?.Cut.AddAttachment(bulb);

                    if (kit.lampShaftMesh != null && kit.lampShaftMaterial != null)
                    {
                        var shaft = new GameObject("Raio_Varanda");
                        shaft.transform.SetParent(porch, false);
                        shaft.transform.position = door + new Vector3(0f, 1.12f, -0.45f);
                        shaft.transform.localScale = new Vector3(2.6f, 2.2f, 2.6f);
                        shaft.AddComponent<MeshFilter>().sharedMesh = kit.lampShaftMesh;
                        var sr = shaft.AddComponent<MeshRenderer>();
                        sr.sharedMaterial = kit.lampShaftMaterial;
                        sr.shadowCastingMode = ShadowCastingMode.Off;
                    }
                }

                // Luz da varanda: camada Default (pega o piso do espaço inicial e as paredes externas).
                var lightGo = new GameObject("Luz_Varanda");
                lightGo.transform.SetParent(porch, false);
                lightGo.transform.position = door + new Vector3(0f, 2.2f, -0.5f);
                var pl = lightGo.AddComponent<Light>();
                pl.type = LightType.Point;
                pl.range = kit.porchLightRange;
                pl.intensity = kit.porchLightIntensity;
                pl.color = LampWarm;
                pl.shadows = LightShadows.None;
            }

            if (kit.path)
            {
                // Caminho de terra: do degrau da varanda até o portão (pode sair na diagonal).
                Vector3 a = door + new Vector3(0f, 0f, -2.2f);
                Vector3 b = new Vector3(pathEnd.x, 0f, pathEnd.y);
                Vector3 d = b - a;
                if (d.z < -0.5f)
                {
                    var path = Primitive(PrimitiveType.Cube, root, "Caminho", (a + b) * 0.5f + Vector3.up * 0.01f,
                        new Vector3(1.4f, 0.02f, d.magnitude + 0.7f), Mat(kit.pathMaterial, new Color(0.29f, 0.231f, 0.188f)), false);
                    path.transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
                    path.GetComponent<Renderer>().renderingLayerMask = 1u;
                }
            }
        }

        // ================================================================== Peças

        private static GameObject Primitive(PrimitiveType type, Transform parent, string name, Vector3 localPos, Vector3 scale,
                                            Material mat, bool keepCollider)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (!keepCollider)
            {
                var col = go.GetComponent<Collider>();
                if (col != null) DestroyImmediate(col); // imediato: o NavMesh é calculado logo em seguida
            }
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        private static Renderer NoShadow(GameObject go)
        {
            var r = go.GetComponent<Renderer>();
            r.shadowCastingMode = ShadowCastingMode.Off;
            return r;
        }

        /// <summary>Enfeite preso ao corte de parede: fica sob "Enfeites" (NÃO filho da parede, que muda de escala).</summary>
        private GameObject Decor(string name, Vector3 size, float bevel, float taper, Vector3 pos, Vector3 euler, Material mat)
        {
            var go = Place(decorRoot, name, size, bevel, taper, pos, euler, mat);
            go.GetComponent<Renderer>().renderingLayerMask = AllLayers;
            return go;
        }

        private static GameObject Place(Transform parent, string name, Vector3 size, float bevel, float taper, Vector3 pos, Vector3 euler, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(euler));
            go.AddComponent<MeshFilter>().sharedMesh = BevelMesh.Get(size, bevel, taper);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        // ================================================================== Materiais (kit ou padrão por código)

        private static long Key(Color c, int kind) =>
            ((long)ColorUtility.ToHtmlStringRGBA(c).GetHashCode() << 8) ^ (uint)kind;

        /// <summary>Material do kit; se vazio, toon de ambiente com a cor dada (criado uma vez).</summary>
        private Material Mat(Material m, Color fallback)
        {
            if (m != null) return m;
            long key = Key(fallback, 1);
            if (!fallbackMats.TryGetValue(key, out var f) || f == null)
            {
                f = ToonMaterials.NewEnvironment(fallback);
                fallbackMats[key] = f;
            }
            return f;
        }

        private Material Emissive(Material m, Color baseColor, Color emission)
        {
            if (m != null) return m;
            long key = Key(baseColor, 2) ^ ((long)ColorUtility.ToHtmlStringRGB(emission).GetHashCode() << 32);
            if (!fallbackMats.TryGetValue(key, out var f) || f == null)
            {
                f = ToonMaterials.NewEmissive(baseColor, emission);
                fallbackMats[key] = f;
            }
            return f;
        }

        /// <summary>Material de FX (névoa). Sem o do kit: shader HorrorTycoon/FX só com a cor (sem ruído). Null se não houver shader.</summary>
        private Material FxMat(Material m, Color color)
        {
            if (m != null) return m;
            long key = Key(color, 3);
            if (fallbackMats.TryGetValue(key, out var f) && f != null) return f;
            Shader shader = ToonMaterials.Fx != null ? ToonMaterials.Fx : Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) return null;
            f = new Material(shader);
            if (f.HasProperty(ToonMaterials.BaseColorId)) f.SetColor(ToonMaterials.BaseColorId, color);
            fallbackMats[key] = f;
            return f;
        }
    }
}
