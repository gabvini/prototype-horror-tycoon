using System.Collections.Generic;
using HorrorTycoon.Actors;
using HorrorTycoon.Art;
using HorrorTycoon.Cameras;
using HorrorTycoon.Core;
using HorrorTycoon.Rooms;
using HorrorTycoon.Rooms.Building;
using HorrorTycoon.Rooms.Generation;
using HorrorTycoon.Run;
using HorrorTycoon.UI;
using Unity.AI.Navigation;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HorrorTycoon.EditorTools
{
    /// <summary>
    /// Ferramenta de EDITOR (não vai para o jogo final): monta a cena greybox do P0
    /// do zero, pelo menu "Horror Tycoon > Construir Cena Greybox".
    ///
    /// Por que um script em vez de montar à mão?
    ///   - Dá para reconstruir a cena a qualquer momento (é só rodar de novo).
    ///   - Mudanças no layout ficam registradas no Git como código legível.
    /// A cena é montada a partir do Catálogo de Conteúdo (atores e salas vêm dos assets em Data/).
    /// Depois de construída, a cena é normal: dá para editar à mão (mas rodar o menu de novo apaga as edições).
    ///
    /// Duas cenas:
    ///   - P0_Greybox ("Construir Cena Greybox"): casa FIXA montada aqui no editor (slots sorteados por run).
    ///   - P2_CasaProcedural ("Construir Cena Casa Procedural"): mesmo terreno/céu/cenário/atores/câmeras/sistemas,
    ///     SEM casa: um HouseBuilder monta a casa gerada em Play (RunPresenter.houseGen = Geracao/Casa_Padrao).
    /// </summary>
    public static class GreyboxSceneBuilder
    {
        private const string Root = "Assets/_HorrorTycoon";
        private const string ScenePath = Root + "/Scenes/P0_Greybox.unity";
        private const string ProceduralScenePath = Root + "/Scenes/P2_CasaProcedural.unity";
        private const string MaterialsPath = Root + "/Materials";

        // ---- Planta em ESCALA REAL (metros). Pé-direito 2,7 m; portas internas 0,9 m; atores com 1,75 m.
        // Corredor central (slot 0) de 2 m de largura; 3 cômodos de cada lado (4,5 × 4 m).
        //
        //   z=+6  ┌──────────┬──┬──────────┐
        //         │ slot 3   │  │ slot 6   │
        //         ├──────────┤  ├──────────┤
        //         │ slot 2   │C │ slot 5   │     C = corredor (slot 0)
        //         ├──────────┤  ├──────────┤
        //         │ slot 1   │  │ slot 4   │
        //   z=-6  └──────────┴▯▯┴──────────┘  ← porta da frente (x = 0)
        //       x=-5,5     -1  +1        +5,5
        //
        // Quais cômodos ocupam os slots é sorteado a cada run (FilmRun + RoomAnchor.Bind).
        private const float WallHeight = 2.7f;
        private const float DoorHeight = 2.1f;
        private const float WallThickness = 0.12f;
        private const float DoorWidth = 0.9f;
        private const float FrontDoorWidth = 1.0f;
        private const float HallHalfWidth = 1f;
        private const float HouseHalfWidth = 5.5f;
        private const float HouseHalfDepth = 6f;
        private const float RoomDepth = 4f;
        private static readonly float[] RowCenters = { -4f, 0f, 4f };

        private const float AgentRadius = 0.25f;
        private const float AgentHeight = 1.75f;

        // ---- Terreno da casa (preenchido no início do Build). P0: casa fixa 11 × 12, fachada em z = -6.
        // Casa procedural: retângulo MÁXIMO do HouseGenDef centrado em x = 0, fachada em z = -profundidade/2.
        private static bool procedural;
        private static float siteHalfWidth = HouseHalfWidth;
        private static float siteFrontZ = -HouseHalfDepth;
        private static float siteBackZ = HouseHalfDepth;
        private static float siteFenceZ = -15f;

        [MenuItem("Horror Tycoon/Construir Cena Greybox")]
        public static void Build() => Build(false);

        /// <summary>Cena da casa gerada (P2): sem casa fixa; o HouseBuilder monta a casa em Play.</summary>
        [MenuItem("Horror Tycoon/Construir Cena Casa Procedural")]
        public static void BuildProcedural() => Build(true);

        private static void Build(bool proceduralHouse)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            ContentBuilder.EnsureP1Content();
            HudAssetsSetup.EnsureHudAssets(); // HUD nova (UI Toolkit): fontes, PanelSettings, ícones
            if (proceduralHouse)
            {
                HouseArtKitSetup.EnsureDefaultKit();
                if (AssetDatabase.LoadAssetAtPath<HouseGenDef>(ContentBuilder.HouseGenPath) == null)
                {
                    Debug.LogError($"[HorrorTycoon] Falta {ContentBuilder.HouseGenPath} (rode 'Criar Conteúdo P1').");
                    return;
                }
            }
            EnsureAgentSettings();
            HTVisualSetup.EnsureRenderer(); // contorno de ambiente + camadas de luz por cômodo
            anchorsBySlot.Clear();
            glowsBySlot.Clear();
            windowsBySlot.Clear();
            wallSegments.Clear();

            procedural = proceduralHouse;
            siteHalfWidth = HouseHalfWidth;
            siteFrontZ = -HouseHalfDepth;
            siteBackZ = HouseHalfDepth;
            siteFenceZ = -15f;
            string path = procedural ? ProceduralScenePath : ScenePath;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Recarrega DEPOIS de criar a cena nova: trocar de cena descarrega assets da memória,
            // e referências antigas virariam "null".
            var content = AssetDatabase.LoadAssetAtPath<GameContentDef>(ContentBuilder.ContentPath);
            HouseGenDef houseGen = null;
            HouseArtKit kit = null;
            if (procedural)
            {
                houseGen = AssetDatabase.LoadAssetAtPath<HouseGenDef>(ContentBuilder.HouseGenPath);
                kit = AssetDatabase.LoadAssetAtPath<HouseArtKit>(HouseArtKitSetup.KitPath);
                if (houseGen == null)
                {
                    Debug.LogError($"[HorrorTycoon] Falta {ContentBuilder.HouseGenPath} (rode 'Criar Conteúdo P1').");
                    procedural = false;
                    return;
                }
                // Mesma régua do HouseBuilder: fachada em z = -profundidade/2 (casa máxima centrada na origem).
                siteHalfWidth = houseGen.bounds.x * 0.5f;
                siteFrontZ = -houseGen.bounds.y * 0.5f;
                siteBackZ = siteFrontZ + houseGen.bounds.y;
                siteFenceZ = siteFrontZ - 9f;
            }

            BuildLighting();
            BuildGround();
            HouseBuilder houseBuilder = null;
            if (procedural) houseBuilder = BuildHouseBuilder(kit);
            else BuildHouse();
            BuildAtmosphere();
            BuildFilmSet();
            var navSurface = BuildNavMeshSurface();
            ActorView[] actors = BuildActors(content);
            ActorFocusController focus = BuildCameras();
            BuildPostProcessing();
            BuildSystems(content, navSurface, focus, houseGen, houseBuilder);
            HTVisualSetup.ApplySeeThroughFlags(); // paredes/batentes/janelas/beiral: buraco de visão (keyword no material)

            EditorSceneManager.SaveScene(scene, path);
            AddSceneToBuildSettings(path);
            Debug.Log($"[HorrorTycoon] Cena {(procedural ? "da casa procedural" : "greybox")} criada em {path} com {actors.Length} atores.");
            procedural = false;
        }

        /// <summary>Casa procedural: só o HouseBuilder (com o kit de arte). A casa nasce em Play, a cada run.</summary>
        private static HouseBuilder BuildHouseBuilder(HouseArtKit kit)
        {
            var go = new GameObject("Casa (montada em Play)");
            var builder = go.AddComponent<HouseBuilder>();
            builder.Configure(kit, siteFrontZ, new Vector2(0f, siteFenceZ));
            EditorUtility.SetDirty(builder);
            if (kit == null) Debug.LogWarning("[HorrorTycoon] HouseBuilder sem kit de arte: usa materiais padrão por código.");
            return builder;
        }

        /// <summary>
        /// O NavMesh usa o "tipo de agente" Humanoid das Project Settings (Navigation).
        /// Em escala real o agente precisa ser mais fino (raio 0,25) para passar em portas de 0,9 m.
        /// </summary>
        private static void EnsureAgentSettings()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset");
            if (assets == null || assets.Length == 0) return;
            var so = new SerializedObject(assets[0]);
            var list = so.FindProperty("m_Settings");
            if (list == null || list.arraySize == 0) return;
            var humanoid = list.GetArrayElementAtIndex(0);
            humanoid.FindPropertyRelative("agentRadius").floatValue = AgentRadius;
            humanoid.FindPropertyRelative("agentHeight").floatValue = AgentHeight;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ Luz

        private static void BuildLighting()
        {
            // Lua: fraca e azulada. A luz "de verdade" vem das lâmpadas de cada cômodo.
            // Guia de arte §6: lua #8CA6FF a 0,5; ambiente em gradiente; névoa exp² CLARA (#2B3550, 0,015)
            // para recortar a silhueta da casa e da floresta; céu de desenho com lua e estrelas.
            var lightGo = new GameObject("Lua");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = HTVisualSetup.MoonIntensity;
            light.color = HTVisualSetup.Moon;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 1f;
            lightGo.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

            HTVisualSetup.ApplyEnvironmentLighting(light);
        }

        // ------------------------------------------------------------------ Chão

        private static void BuildGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground_Floresta";
            ground.transform.localScale = new Vector3(9f, 1f, 9f); // Plane = 10x10 -> 90x90
            ground.GetComponent<Renderer>().sharedMaterial = HTVisualSetup.Environment("M_Ground", HTVisualSetup.Hex(0x22302A), 0f);

            // Caminho de terra até a porta da frente (casa procedural: o HouseBuilder faz, a porta muda por run).
            if (!procedural)
            {
                var path = GameObject.CreatePrimitive(PrimitiveType.Cube);
                path.name = "Caminho";
                path.transform.position = new Vector3(0f, 0.01f, -HouseHalfDepth - 3.5f);
                path.transform.localScale = new Vector3(1.4f, 0.02f, 7f);
                path.GetComponent<Renderer>().sharedMaterial = HTVisualSetup.Environment("M_Caminho", HTVisualSetup.Hex(0x4A3B30), 0f);
            }

            // Cenário em volta da casa (árvores, arbustos, cerca, cemitério, esconderijos de câmera).
            // Montado por código pelo SceneryBuilder, sempre igual (seed fixa). Casa procedural: em volta do retângulo máximo.
            var scenery = new GameObject("Cenario_Raiz");
            if (procedural)
            {
                SceneryBuilder.Build(scenery.transform,
                    SceneryBuilder.SiteLayout.ForHouse(siteHalfWidth, siteFrontZ, siteBackZ - siteFrontZ));
            }
            else
            {
                SceneryBuilder.Build(scenery.transform);
            }
        }

        // ------------------------------------------------------------------ Casa

        // Estado temporário da montagem (preenchido em BuildHouse, usado por janelas/enfeites).
        private sealed class WallInfo
        {
            public WallCutaway Cut;
            public bool AlongX;
            public float Fixed, From, To;
            public bool Lintel;
            public bool Exterior;
        }

        private static readonly Dictionary<int, RoomAnchor> anchorsBySlot = new Dictionary<int, RoomAnchor>();
        private static readonly Dictionary<int, List<Renderer>> glowsBySlot = new Dictionary<int, List<Renderer>>();
        private static readonly Dictionary<int, List<Renderer>> windowsBySlot = new Dictionary<int, List<Renderer>>();
        private static readonly List<WallInfo> wallSegments = new List<WallInfo>();
        private static Transform decorRoot;

        // Paleta do guia (§3.1)
        private static readonly Color DarkWoodColor = HTVisualSetup.Hex(0x4A3328);
        private static readonly Color DeckWoodColor = HTVisualSetup.Hex(0x5C4033);
        private static readonly Color ShingleColor = HTVisualSetup.Hex(0x2A2236);
        private static readonly Color BrickColor = HTVisualSetup.Hex(0x6B3B35);

        private static void BuildHouse()
        {
            var house = new GameObject("Casa");
            decorRoot = new GameObject("Enfeites (somem com o corte)").transform;
            decorRoot.SetParent(house.transform);

            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = "Fundacao";
            slab.transform.SetParent(house.transform);
            slab.transform.position = new Vector3(0f, -0.1f, 0f);
            slab.transform.localScale = new Vector3(HouseHalfWidth * 2f + 0.4f, 0.2f, HouseHalfDepth * 2f + 0.4f);
            slab.GetComponent<Renderer>().sharedMaterial = HTVisualSetup.Environment("M_Fundacao", HTVisualSetup.Hex(0x3A3440), 0.25f);

            // Slot 0: corredor (sem névoa — é sempre conhecido).
            BuildSlot(house.transform, 0, new Vector3(0f, 0f, 0f), new Vector2(HallHalfWidth * 2f, HouseHalfDepth * 2f), 0f, false);

            // Slots 1-3 (oeste) e 4-6 (leste). O Interior do lado leste gira 180° para que a porta
            // fique sempre no lado +X local (convenção do FurnitureKit).
            float roomWidth = HouseHalfWidth - HallHalfWidth;
            float westX = -(HallHalfWidth + roomWidth * 0.5f);
            float eastX = HallHalfWidth + roomWidth * 0.5f;
            for (int i = 0; i < RowCenters.Length; i++)
            {
                BuildSlot(house.transform, 1 + i, new Vector3(westX, 0f, RowCenters[i]), new Vector2(roomWidth, RoomDepth), 0f, true);
                BuildSlot(house.transform, 4 + i, new Vector3(eastX, 0f, RowCenters[i]), new Vector2(roomWidth, RoomDepth), 180f, true);
            }

            BuildWalls(house.transform);
            BuildFacade(house.transform, westX, eastX);
            BuildPorch(house.transform);

            // Liga janelas e brilhos de lâmpada a cada cômodo (RoomAnchor acende/apaga/pisca).
            foreach (var pair in anchorsBySlot)
            {
                List<Renderer> windows, glows;
                windowsBySlot.TryGetValue(pair.Key, out windows);
                glowsBySlot.TryGetValue(pair.Key, out glows);
                pair.Value.ConfigureVisuals(windows != null ? windows.ToArray() : null, glows != null ? glows.ToArray() : null);
                EditorUtility.SetDirty(pair.Value);
            }
        }

        private static void BuildSlot(Transform parent, int slot, Vector3 center, Vector2 size, float interiorYaw, bool withFog)
        {
            var room = new GameObject(slot == 0 ? "Slot_0 (Corredor)" : $"Slot_{slot}");
            room.transform.SetParent(parent);
            room.transform.position = center;
            uint layer = HTVisualSetup.SlotLayer(slot);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Piso";
            floor.transform.SetParent(room.transform, false);
            floor.transform.localPosition = new Vector3(0f, 0.01f, 0f);
            floor.transform.localScale = new Vector3(size.x - 0.02f, 0.02f, size.y - 0.02f);
            var floorRenderer = floor.GetComponent<Renderer>();
            floorRenderer.sharedMaterial = HTVisualSetup.Environment("M_Piso", Color.white, 0f); // cor vem do RoomDef (MaterialPropertyBlock)
            // Camada de luz do slot: só a lâmpada DESTE cômodo ilumina o piso (o corredor também pega a varanda).
            floorRenderer.renderingLayerMask = slot == 0 ? (layer | 1u) : layer;

            var interior = new GameObject("Interior").transform;
            interior.SetParent(room.transform, false);
            interior.localRotation = Quaternion.Euler(0f, interiorYaw, 0f);

            GameObject fog = withFog ? BuildRoomMist(room.transform, size) : null;

            var lightGo = new GameObject("Lampada");
            lightGo.transform.SetParent(room.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 2.45f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            // Guia §6: 2–3 de intensidade, alcance 5,5 m, sem sombra. O corredor (12 m) ganha alcance maior.
            light.range = slot == 0 ? 6.5f : 5.5f;
            light.intensity = slot == 0 ? 2.2f : 2.6f;
            light.color = HTVisualSetup.LampWarm;
            light.shadows = LightShadows.None;
            light.GetUniversalAdditionalLightData().renderingLayers = layer;

            glowsBySlot[slot] = BuildHangingLamp(lightGo.transform, layer);

            var anchor = room.AddComponent<RoomAnchor>();
            anchor.Configure(slot, size, floorRenderer, interior, fog, light);
            anchorsBySlot[slot] = anchor;

            // Porta principal: corredor -> porta da frente (-Z); cômodos -> parede do lado do corredor.
            if (slot == 0)
            {
                anchor.ConfigureDoor(new Vector3(0f, 0f, -size.y * 0.5f), Vector3.back);
            }
            else
            {
                float towardHall = -Mathf.Sign(center.x);
                anchor.ConfigureDoor(new Vector3(towardHall * size.x * 0.5f, 0f, 0f), new Vector3(towardHall, 0f, 0f));
            }
            EditorUtility.SetDirty(anchor);
        }

        /// <summary>
        /// Cômodo não descoberto (guia §5): névoa animada (#1A1D2B) em volume + 2 camadas internas,
        /// e marcações de fita crepe apagadas (25%) no chão — "o set ainda não foi montado".
        /// Sem colisor (o clique atravessa e acerta o piso). Some com dissolve ao descobrir (RoomAnchor).
        /// </summary>
        private static GameObject BuildRoomMist(Transform room, Vector2 size)
        {
            var root = new GameObject("Nevoa");
            root.transform.SetParent(room, false);

            // Um pouco mais clara que o #1A1D2B do guia: sobre o piso #0E0F17 a névoa precisa "aparecer" como nuvem.
            var volMat = HTVisualSetup.Fx("M_FX_NevoaSala", new Color(0.135f, 0.15f, 0.23f, 0.8f),
                HTVisualSetup.FxBlend.Alpha, 1.1f, new Vector3(0.06f, 0.03f, 0.04f), 0.75f, 0.25f, 1f, 0.45f);
            var layerMat = HTVisualSetup.Fx("M_FX_NevoaCamada", new Color(0.17f, 0.18f, 0.27f, 0.55f),
                HTVisualSetup.FxBlend.Alpha, 0.8f, new Vector3(-0.05f, 0.02f, 0.06f), 0.9f, 0.2f);
            var tapeMat = HTVisualSetup.Fx("M_FX_FitaApagada", new Color(HTVisualSetup.Tape.r, HTVisualSetup.Tape.g, HTVisualSetup.Tape.b, 0.25f),
                HTVisualSetup.FxBlend.Alpha, 1f, Vector3.zero, 0f, 0f);

            var vol = GameObject.CreatePrimitive(PrimitiveType.Cube);
            vol.name = "Nevoa_Volume";
            Object.DestroyImmediate(vol.GetComponent<Collider>());
            vol.transform.SetParent(root.transform, false);
            vol.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            vol.transform.localScale = new Vector3(size.x - 0.14f, 1.5f, size.y - 0.14f);
            NoShadow(vol, volMat);

            float[] heights = { 0.3f, 0.85f };
            for (int i = 0; i < heights.Length; i++)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q.name = "Nevoa_Camada";
                Object.DestroyImmediate(q.GetComponent<Collider>());
                q.transform.SetParent(root.transform, false);
                q.transform.localPosition = new Vector3(0f, heights[i], 0f);
                q.transform.localRotation = Quaternion.Euler(90f, i * 90f, 0f);
                q.transform.localScale = i == 0 ? new Vector3(size.x - 0.2f, size.y - 0.2f, 1f) : new Vector3(size.y - 0.2f, size.x - 0.2f, 1f);
                NoShadow(q, layerMat);
            }

            // Fita crepe apagada: retângulo do "set" + um X de marca.
            float hx = size.x * 0.5f - 0.45f, hz = size.y * 0.5f - 0.45f;
            TapeStrip(root.transform, new Vector3(0f, 0.025f, -hz), new Vector3(hx * 2f, 1f, 0.05f), 0f, tapeMat);
            TapeStrip(root.transform, new Vector3(0f, 0.025f, hz), new Vector3(hx * 2f, 1f, 0.05f), 0f, tapeMat);
            TapeStrip(root.transform, new Vector3(-hx, 0.025f, 0f), new Vector3(0.05f, 1f, hz * 2f), 0f, tapeMat);
            TapeStrip(root.transform, new Vector3(hx, 0.025f, 0f), new Vector3(0.05f, 1f, hz * 2f), 0f, tapeMat);
            TapeStrip(root.transform, new Vector3(0.2f, 0.026f, 0.1f), new Vector3(0.4f, 1f, 0.05f), 45f, tapeMat);
            TapeStrip(root.transform, new Vector3(0.2f, 0.026f, 0.1f), new Vector3(0.4f, 1f, 0.05f), -45f, tapeMat);
            return root;
        }

        private static void TapeStrip(Transform parent, Vector3 pos, Vector3 size, float yaw, Material mat)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "Fita_Apagada";
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(parent, false);
            q.transform.localPosition = pos;
            q.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(90f, 0f, 0f);
            q.transform.localScale = new Vector3(size.x, size.z, 1f);
            NoShadow(q, mat);
        }

        /// <summary>Lâmpada pendurada: fio + cúpula + bulbo que brilha + raio de luz falso (cone aditivo).</summary>
        private static List<Renderer> BuildHangingLamp(Transform lamp, uint layer)
        {
            var glows = new List<Renderer>();
            var shadeMat = HTVisualSetup.Emissive("M_Cupula", HTVisualSetup.Hex(0x8A6A40), new Color(0.3f, 0.18f, 0.07f));
            var bulbMat = HTVisualSetup.Emissive("M_Bulbo", HTVisualSetup.Hex(0xFFE2B0), new Color(2.4f, 1.7f, 0.9f));
            var shaftMat = HTVisualSetup.Fx("M_FX_RaioLampada", new Color(1f, 0.7f, 0.36f, 0.075f), HTVisualSetup.FxBlend.Additive,
                0.9f, new Vector3(0f, -0.05f, 0f), 0.25f, 0.4f, 0f, 1f, 1.4f, true);

            var cord = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cord.name = "Fio";
            Object.DestroyImmediate(cord.GetComponent<Collider>());
            cord.transform.SetParent(lamp, false);
            cord.transform.localPosition = new Vector3(0f, 0.17f, 0f);
            cord.transform.localScale = new Vector3(0.015f, 0.3f, 0.015f);
            var cordR = NoShadow(cord, HTVisualSetup.Environment("M_Fio", HTVisualSetup.Hex(0x15121A), 0f));
            cordR.renderingLayerMask = layer;
            glows.Add(cordR); // some junto com a lâmpada (cômodo não descoberto = sem luminária)

            var shade = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shade.name = "Cupula";
            Object.DestroyImmediate(shade.GetComponent<Collider>());
            shade.transform.SetParent(lamp, false);
            shade.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            shade.transform.localScale = new Vector3(0.26f, 0.05f, 0.26f);
            var shadeR = NoShadow(shade, shadeMat);
            shadeR.renderingLayerMask = layer;
            glows.Add(shadeR);

            var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bulb.name = "Bulbo";
            Object.DestroyImmediate(bulb.GetComponent<Collider>());
            bulb.transform.SetParent(lamp, false);
            bulb.transform.localPosition = new Vector3(0f, -0.07f, 0f);
            bulb.transform.localScale = Vector3.one * 0.13f;
            glows.Add(NoShadow(bulb, bulbMat));

            var shaft = new GameObject("Raio_de_luz");
            shaft.transform.SetParent(lamp, false);
            float h = lamp.localPosition.y - 0.1f;
            shaft.transform.localPosition = new Vector3(0f, -0.1f - h * 0.5f, 0f);
            shaft.transform.localScale = new Vector3(2.4f, h, 2.4f);
            shaft.AddComponent<MeshFilter>().sharedMesh = HTVisualSetup.ConeMesh();
            var sr = shaft.AddComponent<MeshRenderer>();
            sr.sharedMaterial = shaftMat;
            sr.shadowCastingMode = ShadowCastingMode.Off;
            sr.receiveShadows = false;
            glows.Add(sr);
            return glows;
        }

        private static Renderer NoShadow(GameObject go, Material mat)
        {
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return r;
        }

        private static void BuildWalls(Transform parent)
        {
            var walls = new GameObject("Paredes").transform;
            walls.SetParent(parent);
            float hw = HouseHalfWidth, hd = HouseHalfDepth, hall = HallHalfWidth;
            var none = new float[0];

            // Externas. Divididas em trechos de ~4 m: o corte "Sims" funciona por pedaço.
            WallWithDoors(walls, new Vector3(-hw, 0, -hd), new Vector3(-hall, 0, -hd), none, DoorWidth, true);
            WallWithDoors(walls, new Vector3(-hall, 0, -hd), new Vector3(hall, 0, -hd), new[] { 0f }, FrontDoorWidth, true);
            WallWithDoors(walls, new Vector3(hall, 0, -hd), new Vector3(hw, 0, -hd), none, DoorWidth, true);
            WallWithDoors(walls, new Vector3(-hw, 0, hd), new Vector3(-hall, 0, hd), none, DoorWidth, true);
            WallWithDoors(walls, new Vector3(-hall, 0, hd), new Vector3(hall, 0, hd), none, DoorWidth, true);
            WallWithDoors(walls, new Vector3(hall, 0, hd), new Vector3(hw, 0, hd), none, DoorWidth, true);
            foreach (float z in RowCenters)
            {
                float z0 = z - RoomDepth * 0.5f, z1 = z + RoomDepth * 0.5f;
                WallWithDoors(walls, new Vector3(-hw, 0, z0), new Vector3(-hw, 0, z1), none, DoorWidth, true);
                WallWithDoors(walls, new Vector3(hw, 0, z0), new Vector3(hw, 0, z1), none, DoorWidth, true);

                // Paredes do corredor: uma porta por cômodo, no meio dele.
                WallWithDoors(walls, new Vector3(-hall, 0, z0), new Vector3(-hall, 0, z1), new[] { z }, DoorWidth, false);
                WallWithDoors(walls, new Vector3(hall, 0, z0), new Vector3(hall, 0, z1), new[] { z }, DoorWidth, false);
            }

            // Divisórias entre cômodos do mesmo lado (sem porta: tudo passa pelo corredor).
            for (int i = 0; i < RowCenters.Length - 1; i++)
            {
                float z = (RowCenters[i] + RowCenters[i + 1]) * 0.5f;
                WallWithDoors(walls, new Vector3(-hw, 0, z), new Vector3(-hall, 0, z), none, DoorWidth, false);
                WallWithDoors(walls, new Vector3(hall, 0, z), new Vector3(hw, 0, z), none, DoorWidth, false);
            }
        }

        /// <summary>
        /// Parede reta de 'a' até 'b' (alinhada a X ou Z), com vãos de porta centrados em 'doorCenters'
        /// (coordenada ao longo da parede). Acima de cada vão vai uma verga (pedaço de parede de 2,1 a 2,7 m).
        /// Cada vão ganha batente de madeira escura (guia §5: "batente visível").
        /// </summary>
        private static void WallWithDoors(Transform parent, Vector3 a, Vector3 b, float[] doorCenters, float doorWidth, bool exterior)
        {
            bool alongX = Mathf.Abs(b.x - a.x) > Mathf.Abs(b.z - a.z);
            float start = alongX ? Mathf.Min(a.x, b.x) : Mathf.Min(a.z, b.z);
            float end = alongX ? Mathf.Max(a.x, b.x) : Mathf.Max(a.z, b.z);
            float fixedCoord = alongX ? a.z : a.x;

            System.Array.Sort(doorCenters);
            float cursor = start;
            foreach (float door in doorCenters)
            {
                float d0 = door - doorWidth * 0.5f, d1 = door + doorWidth * 0.5f;
                CreateWallSegment(parent, alongX, fixedCoord, cursor, d0, 0f, WallHeight, false, exterior);
                CreateWallSegment(parent, alongX, fixedCoord, d0, d1, DoorHeight, WallHeight - DoorHeight, true, exterior);
                DoorFrame(parent, alongX, fixedCoord, door, doorWidth);
                cursor = d1;
            }
            CreateWallSegment(parent, alongX, fixedCoord, cursor, end, 0f, WallHeight, false, exterior);
        }

        private static void CreateWallSegment(Transform parent, bool alongX, float fixedCoord, float from, float to,
                                              float bottom, float height, bool lintel, bool exterior)
        {
            float length = to - from;
            if (length <= 0.01f)
            {
                return;
            }

            float mid = (from + to) * 0.5f;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = lintel ? "Verga" : "Parede";
            wall.transform.SetParent(parent);
            wall.transform.position = alongX
                ? new Vector3(mid, bottom + height * 0.5f, fixedCoord)
                : new Vector3(fixedCoord, bottom + height * 0.5f, mid);
            // Paredes ganham meia espessura em cada ponta para fechar os cantos (o vão livre da porta fica ~0,8 m).
            float extra = lintel ? 0f : WallThickness;
            wall.transform.localScale = alongX
                ? new Vector3(length + extra, height, WallThickness)
                : new Vector3(WallThickness, height, length + extra);
            var r = wall.GetComponent<Renderer>();
            r.sharedMaterial = HTVisualSetup.Wall("M_Parede");
            // Externas recebem as luzes de fora (varanda, refletores); internas só as lâmpadas dos cômodos.
            r.renderingLayerMask = exterior ? HTVisualSetup.AllLayers : (HTVisualSetup.AllLayers & ~1u);
            if (lintel) Object.DestroyImmediate(wall.GetComponent<Collider>()); // não atrapalha o NavMesh

            var cut = wall.AddComponent<WallCutaway>();
            cut.Setup(alongX ? Vector3.forward : Vector3.right, height, bottom, lintel);
            wallSegments.Add(new WallInfo { Cut = cut, AlongX = alongX, Fixed = fixedCoord, From = from, To = to, Lintel = lintel, Exterior = exterior });
        }

        /// <summary>Batente: dois montantes + travessa (cortam junto com a parede).</summary>
        private static void DoorFrame(Transform parent, bool alongX, float fixedCoord, float center, float doorWidth)
        {
            var mat = HTVisualSetup.Environment("M_Batente", DarkWoodColor, 0.1f);
            const float jamb = 0.1f, depth = 0.2f;
            for (int side = -1; side <= 1; side += 2)
            {
                float along = center + side * (doorWidth * 0.5f + jamb * 0.5f - 0.02f);
                var j = GameObject.CreatePrimitive(PrimitiveType.Cube);
                j.name = "Batente";
                Object.DestroyImmediate(j.GetComponent<Collider>());
                j.transform.SetParent(parent);
                j.transform.position = alongX ? new Vector3(along, DoorHeight * 0.5f, fixedCoord) : new Vector3(fixedCoord, DoorHeight * 0.5f, along);
                j.transform.localScale = alongX ? new Vector3(jamb, DoorHeight, depth) : new Vector3(depth, DoorHeight, jamb);
                var jr = j.GetComponent<Renderer>();
                jr.sharedMaterial = mat;
                jr.renderingLayerMask = HTVisualSetup.AllLayers;
                j.AddComponent<WallCutaway>().Setup(alongX ? Vector3.forward : Vector3.right, DoorHeight, 0f, false);
            }
            var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            head.name = "Batente_Topo";
            Object.DestroyImmediate(head.GetComponent<Collider>());
            head.transform.SetParent(parent);
            float w = doorWidth + jamb * 2f - 0.04f;
            head.transform.position = alongX ? new Vector3(center, DoorHeight + 0.06f, fixedCoord) : new Vector3(fixedCoord, DoorHeight + 0.06f, center);
            head.transform.localScale = alongX ? new Vector3(w, 0.12f, depth) : new Vector3(depth, 0.12f, w);
            var hr = head.GetComponent<Renderer>();
            hr.sharedMaterial = mat;
            hr.renderingLayerMask = HTVisualSetup.AllLayers;
            head.AddComponent<WallCutaway>().Setup(alongX ? Vector3.forward : Vector3.right, 0.12f, DoorHeight, true);
        }

        /// <summary>Trecho de parede que contém o ponto (para prender enfeites ao corte certo).</summary>
        private static WallCutaway FindSegment(bool alongX, float fixedCoord, float along, bool lintel)
        {
            foreach (var w in wallSegments)
            {
                if (w.AlongX != alongX || w.Lintel != lintel) continue;
                if (Mathf.Abs(w.Fixed - fixedCoord) > 0.01f) continue;
                if (along >= w.From - 0.01f && along <= w.To + 0.01f) return w.Cut;
            }
            return null;
        }

        // ------------------------------------------------------------------ Fachada (janelas, beiral, chaminé)

        /// <summary>
        /// "A casa como personagem" (guia §1/§5): janelas que acendem quando o cômodo é descoberto,
        /// beiral grosso e escuro no topo das paredes externas (dica de telhado), oitão com janelinha
        /// redonda sobre a porta e chaminé torta. Tudo some quando o trecho de parede é cortado.
        /// </summary>
        private static void BuildFacade(Transform house, float westX, float eastX)
        {
            float hw = HouseHalfWidth, hd = HouseHalfDepth;

            // Janelas: laterais de todos os cômodos, frente (1 e 4), fundo (3 e 6) e fim do corredor.
            for (int i = 0; i < RowCenters.Length; i++)
            {
                Window(1 + i, false, -hw, RowCenters[i], -1f, 0f);
                Window(4 + i, false, hw, RowCenters[i], 1f, 0f);
            }
            Window(1, true, -hd, westX, -1f, -0.5f);
            Window(4, true, -hd, eastX, -1f, 0.5f);
            Window(3, true, hd, westX, 1f, 0f);
            Window(6, true, hd, eastX, 1f, 0f);
            Window(0, true, hd, 0f, 1f, 0f);

            // Beiral: em todos os trechos externos (inclusive vergas).
            var eaveMat = HTVisualSetup.Environment("M_Beiral", ShingleColor, 0f);
            var rng = new System.Random(77);
            foreach (var w in wallSegments.ToArray())
            {
                if (!w.Exterior) continue;
                float len = w.To - w.From + (w.Lintel ? 0f : 0.76f);
                float outward = w.AlongX ? Mathf.Sign(w.Fixed) : Mathf.Sign(w.Fixed);
                float mid = (w.From + w.To) * 0.5f;
                Vector3 pos = w.AlongX ? new Vector3(mid, WallHeight + 0.07f, w.Fixed + outward * 0.14f)
                                       : new Vector3(w.Fixed + outward * 0.14f, WallHeight + 0.07f, mid);
                Vector3 size = w.AlongX ? new Vector3(len, 0.16f, 0.46f) : new Vector3(0.46f, 0.16f, len);
                float wobble = (float)(rng.NextDouble() * 2.0 - 1.0) * 1.2f;
                var eave = Decor("Beiral", BevelObject(size, 0.04f, 0f), pos, w.AlongX ? new Vector3(0f, 0f, wobble) : new Vector3(wobble, 0f, 0f), eaveMat);
                w.Cut.AddAttachment(eave);
            }

            // Oitão (empena) sobre a porta da frente, com janelinha redonda.
            var frontLintel = FindSegment(true, -hd, 0f, true);
            var wallMat = HTVisualSetup.Wall("M_Parede");
            var gable = Decor("Oitao", GableMesh(2.8f, 1.15f, 0.14f), new Vector3(0f, WallHeight + 0.15f, -hd), Vector3.zero, wallMat);
            frontLintel?.AddAttachment(gable);
            for (int s = -1; s <= 1; s += 2)
            {
                float ang = Mathf.Atan2(1.15f, 1.4f) * Mathf.Rad2Deg;
                var slope = Decor("Oitao_Beiral", BevelObject(new Vector3(1.95f, 0.14f, 0.36f), 0.04f, 0f),
                    new Vector3(s * 0.68f, WallHeight + 0.15f + 0.6f, -hd - 0.08f), new Vector3(0f, 0f, -s * ang), eaveMat);
                frontLintel?.AddAttachment(slope);
            }
            var round = Decor("Janela_Redonda", CylinderMesh(), new Vector3(0f, WallHeight + 0.55f, -hd - 0.05f), new Vector3(90f, 0f, 0f),
                HTVisualSetup.Emissive("M_Janela_Sotao", HTVisualSetup.Hex(0x2A3352), new Color(0.9f, 0.6f, 0.25f)));
            round.transform.localScale = new Vector3(0.42f, 0.05f, 0.42f);
            frontLintel?.AddAttachment(round);

            // Chaminé torta de tijolo na lateral leste.
            var eastMid = FindSegment(false, hw, 1.4f, false);
            var brick = HTVisualSetup.Environment("M_Tijolo", BrickColor, 0.25f);
            var chimney = Decor("Chamine", BevelObject(new Vector3(0.8f, 4.4f, 0.75f), 0.05f, 0.08f), new Vector3(hw + 0.47f, 2.2f, 1.4f), new Vector3(2f, 0f, -2.5f), brick);
            var cap = Decor("Chamine_Topo", BevelObject(new Vector3(0.95f, 0.2f, 0.9f), 0.04f, 0f), new Vector3(hw + 0.57f, 4.42f, 1.48f), new Vector3(2f, 0f, -2.5f),
                HTVisualSetup.Environment("M_Chamine_Topo", HTVisualSetup.Hex(0x2B2530), 0f));
            eastMid?.AddAttachment(chimney);
            eastMid?.AddAttachment(cap);
        }

        /// <summary>Janela na parede externa: moldura + travessas + vidro (o vidro acende com o cômodo).</summary>
        private static void Window(int slot, bool alongX, float fixedCoord, float along, float outward, float offset)
        {
            float c = along + offset;
            var seg = FindSegment(alongX, fixedCoord, c, false);
            var frameMat = HTVisualSetup.Environment("M_Janela_Moldura", DarkWoodColor, 0f);
            var paneMat = HTVisualSetup.Emissive("M_Janela_Vidro", HTVisualSetup.Hex(0x1E2A44), new Color(0.02f, 0.025f, 0.05f));
            const float w = 0.9f, h = 1.05f, cy = 1.5f;
            var rng = new System.Random(slot * 31 + (alongX ? 7 : 3) + Mathf.RoundToInt(c * 10f));
            float tilt = (float)(rng.NextDouble() * 2.0 - 1.0) * 2.2f;

            var root = new GameObject($"Janela_Slot{slot}");
            root.transform.SetParent(decorRoot, false);
            root.transform.position = alongX ? new Vector3(c, cy, fixedCoord) : new Vector3(fixedCoord, cy, c);
            root.transform.rotation = Quaternion.Euler(0f, alongX ? 0f : 90f, 0f) * Quaternion.Euler(0f, 0f, tilt);

            var pane = WindowPart(root.transform, "Vidro", Vector3.zero, new Vector3(w - 0.08f, h - 0.08f, 0.14f), paneMat);
            WindowPart(root.transform, "Moldura_Topo", new Vector3(0f, h * 0.5f, 0f), new Vector3(w + 0.12f, 0.1f, 0.2f), frameMat);
            WindowPart(root.transform, "Peitoril", new Vector3(0f, -h * 0.5f - 0.02f, 0f), new Vector3(w + 0.22f, 0.1f, 0.26f), frameMat);
            WindowPart(root.transform, "Moldura_E", new Vector3(-w * 0.5f, 0f, 0f), new Vector3(0.09f, h, 0.2f), frameMat);
            WindowPart(root.transform, "Moldura_D", new Vector3(w * 0.5f, 0f, 0f), new Vector3(0.09f, h, 0.2f), frameMat);
            WindowPart(root.transform, "Travessa_V", Vector3.zero, new Vector3(0.05f, h - 0.05f, 0.17f), frameMat);
            WindowPart(root.transform, "Travessa_H", new Vector3(0f, 0.08f, 0f), new Vector3(w - 0.05f, 0.05f, 0.17f), frameMat);

            if (!windowsBySlot.TryGetValue(slot, out var list)) windowsBySlot[slot] = list = new List<Renderer>();
            list.Add(pane);

            // Raio de luz falso saindo da janela para o quintal (guia §6): aditivo, some com a distância.
            // Entra nos "brilhos" do cômodo: só aparece com o cômodo descoberto e pisca junto com a lâmpada.
            const float theta = 38f * Mathf.Deg2Rad, length = 2.6f;
            Vector3 dirOut = new Vector3(0f, -Mathf.Sin(theta), outward * Mathf.Cos(theta));
            var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beam.name = "Raio_Janela";
            Object.DestroyImmediate(beam.GetComponent<Collider>());
            beam.transform.SetParent(root.transform, false);
            beam.transform.localPosition = dirOut * (length * 0.5f + 0.12f);
            beam.transform.localRotation = Quaternion.FromToRotation(Vector3.up, -dirOut);
            beam.transform.localScale = new Vector3(w - 0.1f, length, h * 0.8f);
            var beamR = NoShadow(beam, HTVisualSetup.Fx("M_FX_RaioJanela", new Color(1f, 0.7f, 0.36f, 0.07f), HTVisualSetup.FxBlend.Additive,
                0.8f, new Vector3(0.02f, -0.03f, 0f), 0.3f, 0.35f, 0f, 1f, 0.9f, true));
            beamR.receiveShadows = false;
            if (glowsBySlot.TryGetValue(slot, out var glows)) glows.Add(beamR);
            seg?.AddAttachment(root);
        }

        private static Renderer WindowPart(Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.renderingLayerMask = HTVisualSetup.AllLayers;
            return r;
        }

        // ------------------------------------------------------------------ Varanda

        /// <summary>Varanda: deque, degrau, colunas tortas, toldo inclinado, porta aberta e luminária.</summary>
        private static void BuildPorch(Transform house)
        {
            float hd = HouseHalfDepth;
            var deck = HTVisualSetup.Environment("M_Deque", DeckWoodColor, 0.1f);
            var post = HTVisualSetup.Environment("M_Coluna", HTVisualSetup.Hex(0x7A6A80), 0.2f);
            var roof = HTVisualSetup.Environment("M_Beiral", ShingleColor, 0f);
            var frontLintel = FindSegment(true, -hd, 0f, true);

            var porch = new GameObject("Varanda").transform;
            porch.SetParent(house);
            Place(porch, "Deque", BevelObject(new Vector3(3.4f, 0.08f, 1.7f), 0.02f, 0f), new Vector3(0f, 0.02f, -hd - 0.91f), Vector3.zero, deck);
            Place(porch, "Degrau", BevelObject(new Vector3(1.5f, 0.05f, 0.4f), 0.015f, 0f), new Vector3(0f, 0.0f, -hd - 1.95f), new Vector3(0f, 2f, 0f), deck);
            for (int s = -1; s <= 1; s += 2)
            {
                var p = Decor("Coluna", BevelObject(new Vector3(0.15f, 2.75f, 0.15f), 0.03f, 0f), new Vector3(s * 1.55f, 1.4f, -hd - 1.6f), new Vector3(s * 1.2f, 0f, s * 1.8f), post);
                frontLintel?.AddAttachment(p);
            }
            var awning = Decor("Toldo", BevelObject(new Vector3(3.7f, 0.12f, 2.0f), 0.04f, 0f), new Vector3(0f, 2.9f, -hd - 0.95f), new Vector3(-13f, 0f, 1.2f), roof);
            frontLintel?.AddAttachment(awning);

            // Porta da frente aberta, encostada na fachada (vinho, maçaneta de latão).
            var leafMat = HTVisualSetup.Environment("M_Porta", HTVisualSetup.Hex(0x7A3B47), 0.15f);
            var leaf = Decor("Porta_Aberta", BevelObject(new Vector3(0.95f, 2.05f, 0.07f), 0.03f, 0f), new Vector3(1.06f, 1.03f, -hd - 0.11f), new Vector3(0f, 0f, 0.8f), leafMat);
            var knob = Decor("Macaneta", BevelObject(new Vector3(0.08f, 0.08f, 0.08f), 0.03f, 0f), new Vector3(1.44f, 1.0f, -hd - 0.17f), Vector3.zero,
                HTVisualSetup.Environment("M_Latao", HTVisualSetup.Hex(0xB08A3E), 0f));
            foreach (var seg in new[] { FindSegment(true, -hd, 0.75f, false), FindSegment(true, -hd, 2f, false) })
            {
                seg?.AddAttachment(leaf);
                seg?.AddAttachment(knob);
            }

            // Luminária da varanda (a luz em si fica logo abaixo).
            var fixture = Decor("Luminaria_Varanda", BevelObject(new Vector3(0.22f, 0.3f, 0.16f), 0.04f, 0.2f), new Vector3(0f, 2.42f, -hd - 0.14f), Vector3.zero,
                HTVisualSetup.Environment("M_Luminaria", HTVisualSetup.Hex(0x3A2A22), 0f));
            var bulb = Decor("Luminaria_Bulbo", BevelObject(new Vector3(0.14f, 0.16f, 0.1f), 0.04f, 0f), new Vector3(0f, 2.27f, -hd - 0.17f), Vector3.zero,
                HTVisualSetup.Emissive("M_Bulbo", HTVisualSetup.Hex(0xFFE2B0), new Color(2.4f, 1.7f, 0.9f)));
            frontLintel?.AddAttachment(fixture);
            frontLintel?.AddAttachment(bulb);

            var lightGo = new GameObject("Luz_Varanda");
            lightGo.transform.SetParent(house);
            lightGo.transform.position = new Vector3(0f, 2.2f, -hd - 0.5f);
            var pl = lightGo.AddComponent<Light>();
            pl.type = LightType.Point;
            pl.range = 5.5f;
            pl.intensity = 2.4f;
            pl.color = HTVisualSetup.LampWarm;
            pl.shadows = LightShadows.None;

            // Raio de luz falso da varanda.
            var shaft = new GameObject("Raio_Varanda");
            shaft.transform.SetParent(porch, false);
            shaft.transform.position = new Vector3(0f, 1.12f, -hd - 0.45f);
            shaft.transform.localScale = new Vector3(2.6f, 2.2f, 2.6f);
            shaft.AddComponent<MeshFilter>().sharedMesh = HTVisualSetup.ConeMesh();
            var sr = shaft.AddComponent<MeshRenderer>();
            sr.sharedMaterial = HTVisualSetup.Fx("M_FX_RaioVaranda", new Color(1f, 0.7f, 0.36f, 0.08f), HTVisualSetup.FxBlend.Additive,
                0.9f, new Vector3(0f, -0.05f, 0f), 0.25f, 0.4f, 0f, 1f, 1.4f, true);
            sr.shadowCastingMode = ShadowCastingMode.Off;
        }

        // ------------------------------------------------------------------ Atmosfera

        /// <summary>
        /// Névoa rasteira (guia §6): 3 planos grandes de 0,15 a 0,6 m com noise rolando a ~0,03 m/s,
        /// alpha ~0,22, soft particles e um "buraco" na pegada da casa (não entra nos cômodos).
        /// </summary>
        private static void BuildAtmosphere()
        {
            var root = new GameObject("Atmosfera").transform;
            float[] heights = { 0.14f, 0.34f, 0.6f };
            float[] alphas = { 0.26f, 0.2f, 0.13f };
            for (int i = 0; i < heights.Length; i++)
            {
                var mat = HTVisualSetup.Fx($"M_FX_NevoaRasteira_{i + 1}", new Color(HTVisualSetup.GroundMist.r, HTVisualSetup.GroundMist.g, HTVisualSetup.GroundMist.b, alphas[i]),
                    HTVisualSetup.FxBlend.Alpha, 0.22f + i * 0.07f, new Vector3(0.03f + i * 0.01f, 0.01f, (i % 2 == 0 ? 0.02f : -0.025f)), 0.85f, 0.5f);
                mat.SetVector("_HoleRect", new Vector4(-siteHalfWidth - 0.15f, siteFrontZ - 0.15f, siteHalfWidth + 0.15f, siteBackZ + 0.15f));
                mat.SetFloat("_HoleSoft", 0.9f);
                mat.SetFloat("_CameraFade", 2.5f);
                var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
                plane.name = $"Nevoa_Rasteira_{i + 1}";
                Object.DestroyImmediate(plane.GetComponent<Collider>());
                plane.transform.SetParent(root, false);
                plane.transform.position = new Vector3(0f, heights[i], 0f);
                plane.transform.localScale = new Vector3(9f, 1f, 9f);
                var r = plane.GetComponent<Renderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        // ------------------------------------------------------------------ Set de filmagem

        /// <summary>
        /// Detalhes de "set" (guia §5): refletores de cena atrás da casa (spots de verdade, sem sombra,
        /// só na camada Default: não vazam para dentro dos cômodos) e uma câmera de cinema no quintal.
        /// </summary>
        private static void BuildFilmSet()
        {
            var root = new GameObject("Set_de_Filmagem").transform;
            if (procedural)
            {
                // Fora do retângulo máximo da casa gerada (cantos da frente/fundo) e longe do caminho.
                float hw = siteHalfWidth;
                FilmLight(root, new Vector3(-hw - 2.4f, 0f, siteFrontZ - 2.4f), new Vector3(-hw * 0.6f, 1.2f, siteFrontZ + 2f), HTVisualSetup.LampWarm, 5f);
                FilmLight(root, new Vector3(hw + 2.6f, 0f, siteBackZ + 2.6f), new Vector3(hw * 0.55f, 1.0f, siteBackZ - 2.5f), HTVisualSetup.Hex(0x8CA6FF), 6f);
                FilmCamera(root, new Vector3(-6.5f, 0f, siteFrontZ - 6.5f), new Vector3(0f, 1.2f, siteFrontZ));
                return;
            }
            FilmLight(root, new Vector3(-7.4f, 0f, -7.9f), new Vector3(-3.5f, 1.2f, -4f), HTVisualSetup.LampWarm, 5f);
            FilmLight(root, new Vector3(7.6f, 0f, 7.6f), new Vector3(3f, 1.0f, 3.5f), HTVisualSetup.Hex(0x8CA6FF), 6f);
            FilmCamera(root, new Vector3(-3.4f, 0f, -11.0f), new Vector3(0f, 1.2f, -6f));
        }

        private static void FilmLight(Transform root, Vector3 at, Vector3 aim, Color color, float intensity)
        {
            var metal = HTVisualSetup.Environment("M_Set_Metal", HTVisualSetup.Hex(0x2B2B33), 0f);
            var lensMat = HTVisualSetup.Emissive("M_Set_Lente", HTVisualSetup.Hex(0xFFE2B0), new Color(color.r, color.g, color.b) * 2.2f);
            var stand = new GameObject("Refletor").transform;
            stand.SetParent(root, false);
            stand.position = at;
            Vector3 flatAim = new Vector3(aim.x - at.x, 0f, aim.z - at.z);
            stand.rotation = Quaternion.LookRotation(flatAim.normalized, Vector3.up);

            for (int k = 0; k < 3; k++)
            {
                float a = k * 120f;
                Place(stand, "Perna", BevelObject(new Vector3(0.05f, 1.25f, 0.05f), 0.01f, 0f),
                    stand.position + Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0.55f, 0.28f), new Vector3(-25f, a, 0f), metal);
            }
            float headY = 2.3f;
            Place(stand, "Haste", BevelObject(new Vector3(0.05f, headY, 0.05f), 0.01f, 0f), stand.position + Vector3.up * (headY * 0.5f), Vector3.zero, metal);

            Vector3 headPos = stand.position + Vector3.up * headY;
            Quaternion look = Quaternion.LookRotation((aim - headPos).normalized, Vector3.up);
            var head = new GameObject("Cabeca").transform;
            head.SetParent(stand, false);
            head.SetPositionAndRotation(headPos, look);
            Place(head, "Corpo", BevelObject(new Vector3(0.42f, 0.42f, 0.5f), 0.06f, 0f), headPos, look.eulerAngles, metal);
            Place(head, "Lente", BevelObject(new Vector3(0.34f, 0.34f, 0.04f), 0.04f, 0f), headPos + look * new Vector3(0f, 0f, 0.26f), look.eulerAngles, lensMat);
            for (int s = -1; s <= 1; s += 2)
            {
                Place(head, "Aba", BevelObject(new Vector3(0.03f, 0.42f, 0.24f), 0.01f, 0f), headPos + look * new Vector3(s * 0.26f, 0f, 0.36f), (look * Quaternion.Euler(0f, s * 25f, 0f)).eulerAngles, metal);
            }

            var lightGo = new GameObject("Luz_Refletor");
            lightGo.transform.SetParent(head, false);
            lightGo.transform.SetPositionAndRotation(headPos + look * new Vector3(0f, 0f, 0.3f), look);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Spot;
            l.spotAngle = 55f;
            l.innerSpotAngle = 40f;
            l.range = 11f;
            l.intensity = intensity;
            l.color = color;
            l.shadows = LightShadows.None;
            l.GetUniversalAdditionalLightData().renderingLayers = 1u; // Default: chão, cenário, paredes externas, atores
        }

        private static void FilmCamera(Transform root, Vector3 at, Vector3 aim)
        {
            var metal = HTVisualSetup.Environment("M_Set_Metal", HTVisualSetup.Hex(0x2B2B33), 0f);
            var body = HTVisualSetup.Environment("M_Set_Camera", HTVisualSetup.Hex(0x3A3F55), 0.1f);
            var cam = new GameObject("Camera_de_Cinema").transform;
            cam.SetParent(root, false);
            cam.position = at;
            Vector3 flat = new Vector3(aim.x - at.x, 0f, aim.z - at.z);
            cam.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
            for (int k = 0; k < 3; k++)
            {
                float a = k * 120f + 60f;
                Place(cam, "Perna", BevelObject(new Vector3(0.05f, 1.3f, 0.05f), 0.01f, 0f),
                    at + cam.rotation * (Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0.6f, 0.3f)), (cam.rotation * Quaternion.Euler(-25f, a, 0f)).eulerAngles, metal);
            }
            Vector3 c = at + Vector3.up * 1.32f;
            Vector3 e = cam.rotation.eulerAngles + new Vector3(-6f, 0f, 0f);
            Quaternion q = Quaternion.Euler(e);
            Place(cam, "Corpo", BevelObject(new Vector3(0.36f, 0.4f, 0.6f), 0.06f, 0f), c, e, body);
            Place(cam, "Objetiva", CylinderMesh(), c + q * new Vector3(0f, 0f, 0.42f), (q * Quaternion.Euler(90f, 0f, 0f)).eulerAngles, metal).transform.localScale = new Vector3(0.2f, 0.14f, 0.2f);
            for (int s = 0; s < 2; s++)
            {
                var reel = Place(cam, "Rolo", CylinderMesh(), c + q * new Vector3(0f, 0.36f, s == 0 ? 0.16f : -0.2f), (q * Quaternion.Euler(0f, 0f, 90f)).eulerAngles, body);
                reel.transform.localScale = new Vector3(0.34f, 0.04f, 0.34f);
            }
            var tally = Place(cam, "Luz_REC", BevelObject(new Vector3(0.05f, 0.05f, 0.05f), 0.02f, 0f), c + q * new Vector3(0.12f, 0.22f, 0.22f), e,
                HTVisualSetup.Emissive("M_Set_REC", HTVisualSetup.Hex(0xFF3B3B), new Color(3f, 0.25f, 0.2f)));
            tally.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        // ------------------------------------------------------------------ Peças de cenário (helpers)

        private static Mesh BevelObject(Vector3 size, float bevel, float taper) => HTVisualSetup.BevelAsset(size, bevel, taper);

        private static Mesh cylinderMesh;
        private static Mesh CylinderMesh()
        {
            if (cylinderMesh == null)
            {
                var tmp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                cylinderMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
                Object.DestroyImmediate(tmp);
            }
            return cylinderMesh;
        }

        /// <summary>Enfeite preso ao corte de parede: fica sob "Enfeites" (NÃO filho da parede, que muda de escala).</summary>
        private static GameObject Decor(string name, Mesh mesh, Vector3 pos, Vector3 euler, Material mat)
        {
            var go = Place(decorRoot, name, mesh, pos, euler, mat);
            go.GetComponent<Renderer>().renderingLayerMask = HTVisualSetup.AllLayers;
            return go;
        }

        private static GameObject Place(Transform parent, string name, Mesh mesh, Vector3 pos, Vector3 euler, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(euler));
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            return go;
        }

        /// <summary>Empena triangular (prisma) salva como asset: base 'width', altura 'height', espessura 'depth'.</summary>
        private static Mesh GableMesh(float width, float height, float depth)
        {
            HTVisualSetup.EnsureFolder("Assets/_HorrorTycoon/Art", "Meshes");
            string path = HTVisualSetup.MeshFolder + "/HT_Oitao.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh { name = "HT_Oitao" };
            float hw = width * 0.5f, hz = depth * 0.5f;
            Vector3[] f = { new Vector3(-hw, 0f, -hz), new Vector3(hw, 0f, -hz), new Vector3(0f, height, -hz) };
            Vector3[] b = { new Vector3(-hw, 0f, hz), new Vector3(hw, 0f, hz), new Vector3(0f, height, hz) };
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var tris = new List<int>();
            System.Action<Vector3, Vector3, Vector3> tri = (p0, p1, p2) =>
            {
                Vector3 n = Vector3.Cross(p1 - p0, p2 - p0).normalized;
                Vector3 centroid = (p0 + p1 + p2) / 3f - new Vector3(0f, height / 3f, 0f);
                if (Vector3.Dot(n, centroid) < 0f) { var t = p1; p1 = p2; p2 = t; n = -n; }
                int i = verts.Count;
                verts.Add(p0); verts.Add(p1); verts.Add(p2);
                norms.Add(n); norms.Add(n); norms.Add(n);
                tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            };
            tri(f[0], f[1], f[2]);
            tri(b[0], b[2], b[1]);
            for (int k = 0; k < 3; k++)
            {
                int j = (k + 1) % 3;
                tri(f[k], b[k], b[j]);
                tri(f[k], b[j], f[j]);
            }
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            if (isNew) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        // ------------------------------------------------------------------ NavMesh

        private static NavMeshSurface BuildNavMeshSurface()
        {
            // A malha de caminhada é calculada quando a cena COMEÇA (RunPresenter.Start),
            // assim ela sempre bate com a casa atual, mesmo depois de mudanças.
            var go = new GameObject("NavMesh");
            var surface = go.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            return surface;
        }

        // ------------------------------------------------------------------ Atores

        private static ActorView[] BuildActors(GameContentDef content)
        {
            var group = new GameObject("Atores");
            var result = new List<ActorView>();
            ActorDef[] defs = ReadList<ActorDef>(content, "actors");
            int count = defs.Length;
            bool haveModels = HTCharacterSetup.Setup(); // importadores + controller (se os FBX existirem)
            var controller = haveModels ? AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(HTCharacterSetup.ControllerPath) : null;

            for (int i = 0; i < count; i++)
            {
                ActorDef def = defs[i];
                if (def == null) continue;

                float x = (i - (count - 1) * 0.5f) * 1.0f;

                // Raiz do ator fica nos pés; olha para -Z (para a câmera inicial).
                var root = new GameObject(def.DisplayName);
                root.transform.SetParent(group.transform);
                root.transform.position = new Vector3(x, 0f, siteFrontZ - 2.5f); // no caminho, em frente à casa (casa gerada: o RunPresenter leva para dentro)
                root.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

                // Marcador de destaque (aparece quando a carta do ator está sob o mouse).
                var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marker.name = "Marcador";
                Object.DestroyImmediate(marker.GetComponent<Collider>());
                marker.transform.SetParent(root.transform, false);
                marker.transform.localPosition = new Vector3(0f, 2.2f, 0f);
                marker.transform.localScale = Vector3.one * 0.2f;
                marker.GetComponent<Renderer>().sharedMaterial = HTVisualSetup.Emissive("M_Marcador", HTVisualSetup.Hex(0xF5C542), new Color(1.6f, 1.25f, 0.4f));
                marker.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

                // Caminhada: NavMeshAgent salvo DESLIGADO (o NavMesh só existe quando a cena começa).
                var agent = root.AddComponent<NavMeshAgent>();
                agent.radius = AgentRadius;
                agent.height = AgentHeight;
                agent.speed = 2.6f; // passo de caminhada
                agent.angularSpeed = 720f;
                agent.acceleration = 20f;
                agent.stoppingDistance = 0.1f;
                agent.enabled = false;
                root.AddComponent<ActorMover>();

                var view = root.AddComponent<ActorView>();
                var so = new SerializedObject(view);
                so.FindProperty("def").objectReferenceValue = def;
                so.FindProperty("displayName").stringValue = def.DisplayName;
                so.FindProperty("highlightMarker").objectReferenceValue = marker;

                GameObject model = haveModels ? HTCharacterSetup.ModelFor(def.DisplayName) : null;
                if (model != null) BuildModelRig(root, view, so, model, controller);
                else BuildCapsuleRig(root, so, i, def);
                so.ApplyModifiedPropertiesWithoutUndo();

                root.AddComponent<ActorIdle>(); // "vida de set" enquanto o jogador decide (depois do ActorView: lê o Animator)
                view.CaptureFaceRig();          // pose base do rosto (modelo em pose T); a cápsula ignora
                view.SetExpression(ActorExpression.Neutral);
                marker.SetActive(false);

                // Atores recebem TODAS as luzes (lâmpadas dos cômodos, varanda, refletores).
                foreach (var r in root.GetComponentsInChildren<Renderer>(true)) r.renderingLayerMask = HTVisualSetup.AllLayers;

                result.Add(view);
            }

            return result.ToArray();
        }

        /// <summary>
        /// Ator = modelo do Blender (Art/Characters/HT_*.fbx): Animator Humanoid com o controller único,
        /// toon de personagem com a paleta (cores já vêm do modelo — o ActorDef.Color não tinge),
        /// olhos brancos que brilham, colisor de cápsula na raiz para o clique.
        /// </summary>
        private static void BuildModelRig(GameObject root, ActorView view, SerializedObject so, GameObject modelAsset, RuntimeAnimatorController controller)
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            model.name = "Modelo";
            model.transform.SetParent(root.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            // Clique: o RunPresenter faz raycast e usa GetComponentInParent<ActorView>.
            var col = root.AddComponent<CapsuleCollider>();
            col.radius = 0.3f;
            col.height = AgentHeight;
            col.center = new Vector3(0f, AgentHeight * 0.5f, 0f);

            var animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.avatar = HTCharacterSetup.AvatarOf(AssetDatabase.GetAssetPath(modelAsset));
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; // o close lê o "Rosto" mesmo fora da tela
            root.AddComponent<ActorAnimDriver>().Configure(animator);

            string key = modelAsset.name.Replace("HT_", "");
            var bodyMat = HTCharacterSetup.ActorMaterial(key);
            var faceMat = HTCharacterSetup.FaceMaterial();
            var eyeMat = HTCharacterSetup.EyeMaterial();
            var outline = new List<Renderer>();
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                if (r is SkinnedMeshRenderer smr)
                {
                    smr.sharedMaterial = bodyMat;
                    smr.updateWhenOffscreen = true; // animação de morte/pulo sai da caixa da pose T
                    outline.Add(smr);
                }
                else
                {
                    r.sharedMaterial = r.name.StartsWith("Olho_") ? eyeMat : faceMat;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                }
            }

            Transform Find(string n) => FindDeep(model.transform, n);
            so.FindProperty("faceAnchor").objectReferenceValue = Find("Rosto");
            so.FindProperty("leftEye").objectReferenceValue = Find("Olho_E");
            so.FindProperty("rightEye").objectReferenceValue = Find("Olho_D");
            so.FindProperty("leftPupil").objectReferenceValue = Find("Pupila_E");
            so.FindProperty("rightPupil").objectReferenceValue = Find("Pupila_D");
            so.FindProperty("leftBrow").objectReferenceValue = Find("Sobrancelha_E");
            so.FindProperty("rightBrow").objectReferenceValue = Find("Sobrancelha_D");
            so.FindProperty("mouth").objectReferenceValue = null;
            var mouths = so.FindProperty("mouths");
            string[] mouthNames = { "Boca_Neutra", "Boca_Feliz", "Boca_Tensa", "Boca_Grito" };
            mouths.arraySize = mouthNames.Length;
            for (int m = 0; m < mouthNames.Length; m++)
            {
                Transform t = Find(mouthNames[m]);
                mouths.GetArrayElementAtIndex(m).objectReferenceValue = t != null ? t.gameObject : null;
            }
            so.FindProperty("body").objectReferenceValue = model.transform;
            so.FindProperty("animator").objectReferenceValue = animator;
            var orr = so.FindProperty("outlineRenderers");
            orr.arraySize = outline.Count;
            for (int k = 0; k < outline.Count; k++) orr.GetArrayElementAtIndex(k).objectReferenceValue = outline[k];
        }

        private static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                var f = FindDeep(c, name);
                if (f != null) return f;
            }
            return null;
        }

        /// <summary>Fallback: cápsula greybox com rosto de primitivas (se o FBX do ator não existir).</summary>
        private static void BuildCapsuleRig(GameObject root, SerializedObject so, int i, ActorDef def)
        {
            // Corpo: cápsula escalada. Escala real: 1,75 m de altura, 0,55 m de largura.
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Corpo";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, AgentHeight * 0.5f, 0f);
            body.transform.localScale = new Vector3(0.55f, AgentHeight * 0.5f, 0.5f);
            // Toon de PERSONAGEM (guia §7): rim de lua + casca colorida de 2,5 px.
            var bodyRenderer = body.GetComponent<Renderer>();
            bodyRenderer.sharedMaterial = HTVisualSetup.Character($"M_Ator_{i + 1}", def.Color);

            // Rosto: um "anchor" vazio na frente da cabeça + peças pretas sem colisão.
            var face = new GameObject("Rosto");
            face.transform.SetParent(root.transform, false);
            face.transform.localPosition = new Vector3(0f, 1.5f, 0.25f);

            // Olhos brancos levemente emissivos (brilham no escuro, gag de Scooby-Doo) com pupila preta.
            Transform leftEye = FacePart(face.transform, "Olho_E", PrimitiveType.Sphere, new Vector3(-0.09f, 0.03f, 0.02f));
            Transform rightEye = FacePart(face.transform, "Olho_D", PrimitiveType.Sphere, new Vector3(0.09f, 0.03f, 0.02f));
            var eyeMat = HTVisualSetup.Emissive("M_Olho", HTVisualSetup.Hex(0xF2EEE6), new Color(0.75f, 0.74f, 0.7f));
            leftEye.GetComponent<Renderer>().sharedMaterial = eyeMat;
            rightEye.GetComponent<Renderer>().sharedMaterial = eyeMat;
            Transform leftPupil = FacePart(leftEye, "Pupila", PrimitiveType.Sphere, new Vector3(0f, 0f, 0.42f));
            Transform rightPupil = FacePart(rightEye, "Pupila", PrimitiveType.Sphere, new Vector3(0f, 0f, 0.42f));
            Transform leftBrow = FacePart(face.transform, "Sobrancelha_E", PrimitiveType.Cube, new Vector3(-0.09f, 0.11f, 0.02f));
            Transform rightBrow = FacePart(face.transform, "Sobrancelha_D", PrimitiveType.Cube, new Vector3(0.09f, 0.11f, 0.02f));
            Transform mouth = FacePart(face.transform, "Boca", PrimitiveType.Cube, new Vector3(0f, -0.11f, 0.02f));
            leftBrow.localScale = new Vector3(0.1f, 0.02f, 0.02f);
            rightBrow.localScale = new Vector3(0.1f, 0.02f, 0.02f);

            so.FindProperty("faceAnchor").objectReferenceValue = face.transform;
            so.FindProperty("leftEye").objectReferenceValue = leftEye;
            so.FindProperty("rightEye").objectReferenceValue = rightEye;
            so.FindProperty("leftBrow").objectReferenceValue = leftBrow;
            so.FindProperty("rightBrow").objectReferenceValue = rightBrow;
            so.FindProperty("mouth").objectReferenceValue = mouth;
            so.FindProperty("body").objectReferenceValue = body.transform;
            so.FindProperty("leftPupil").objectReferenceValue = leftPupil;
            so.FindProperty("rightPupil").objectReferenceValue = rightPupil;
            so.FindProperty("bodyRenderer").objectReferenceValue = bodyRenderer;
        }

        private static Transform FacePart(Transform parent, string name, PrimitiveType type, Vector3 localPos)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            Object.DestroyImmediate(part.GetComponent<Collider>()); // só o corpo recebe clique
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPos;
            part.GetComponent<Renderer>().sharedMaterial = HTVisualSetup.Environment("M_Rosto", HTVisualSetup.Hex(0x15121A), 0f);
            part.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            return part.transform;
        }

        // ------------------------------------------------------------------ Câmeras

        private static ActorFocusController BuildCameras()
        {
            // Câmera "real" (a que renderiza). O Brain escolhe qual câmera virtual ela segue.
            var mainGo = new GameObject("Main Camera");
            mainGo.tag = "MainCamera";
            var cam = mainGo.AddComponent<Camera>();
            cam.fieldOfView = 30f;
            cam.clearFlags = CameraClearFlags.Skybox; // céu de desenho (HorrorTycoon/Sky)
            cam.backgroundColor = HTVisualSetup.FogColor;
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            mainGo.AddComponent<AudioListener>();
            var brain = mainGo.AddComponent<CinemachineBrain>();
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 0.6f);

            // Rig isométrico: pivô -> braço -> câmera virtual.
            var rigGo = new GameObject("CameraRig");
            rigGo.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
            var arm = new GameObject("CameraArm");
            arm.transform.SetParent(rigGo.transform, false);

            var isoGo = new GameObject("CM_Iso");
            isoGo.transform.SetParent(arm.transform, false);
            var iso = isoGo.AddComponent<CinemachineCamera>();
            iso.Priority = 10;
            iso.Lens = MakeLens(30f);

            var rig = rigGo.AddComponent<IsoCameraRig>();
            var rigSo = new SerializedObject(rig);
            rigSo.FindProperty("cameraArm").objectReferenceValue = arm.transform;
            rigSo.ApplyModifiedPropertiesWithoutUndo();

            // Câmera de close no ator.
            var focusGo = new GameObject("CM_Focus");
            var focus = focusGo.AddComponent<CinemachineCamera>();
            focus.Priority = 0;
            focus.Lens = MakeLens(35f);

            // Câmeras do DIRETOR: planos de cinema e zoom na porta (o CinematicDirector move as duas).
            var cineGo = new GameObject("CM_Cine");
            var cine = cineGo.AddComponent<CinemachineCamera>();
            cine.Priority = 0; // o diretor liga quando o monitor abre
            cine.Lens = MakeLens(45f);
            cineGo.transform.SetPositionAndRotation(new Vector3(0f, 7f, -16f), Quaternion.Euler(20f, 0f, 0f));

            var doorGo = new GameObject("CM_Porta");
            var door = doorGo.AddComponent<CinemachineCamera>();
            door.Priority = 0;
            door.Lens = MakeLens(55f);

            // Monitor do diretor: câmera comum (não Cinemachine), ortográfica, olhando a planta de cima.
            var monGo = new GameObject("MonitorCam");
            monGo.transform.SetPositionAndRotation(new Vector3(0f, 16f, -1.5f), Quaternion.Euler(90f, 0f, 0f));
            var monCam = monGo.AddComponent<Camera>();
            monCam.orthographic = true;
            monCam.orthographicSize = 8.5f;
            monCam.nearClipPlane = 0.3f;
            monCam.farClipPlane = 40f;
            monCam.clearFlags = CameraClearFlags.SolidColor;
            monCam.backgroundColor = new Color(0.03f, 0.04f, 0.06f);
            monCam.enabled = false;

            // Controlador de foco (fica em GameSystems).
            var systems = new GameObject("GameSystems");
            var focusCtrl = systems.AddComponent<ActorFocusController>();
            var fcSo = new SerializedObject(focusCtrl);
            fcSo.FindProperty("mainCamera").objectReferenceValue = cam;
            fcSo.FindProperty("focusCamera").objectReferenceValue = focus;
            fcSo.FindProperty("isoRig").objectReferenceValue = rig;
            fcSo.ApplyModifiedPropertiesWithoutUndo();

            var monitor = systems.AddComponent<DirectorMonitor>();
            var mSo = new SerializedObject(monitor);
            mSo.FindProperty("monitorCamera").objectReferenceValue = monCam;
            mSo.ApplyModifiedPropertiesWithoutUndo();

            var director = systems.AddComponent<CinematicDirector>();
            var dSo = new SerializedObject(director);
            dSo.FindProperty("rig").objectReferenceValue = rig;
            dSo.FindProperty("cineCamera").objectReferenceValue = cine;
            dSo.FindProperty("doorCamera").objectReferenceValue = door;
            dSo.FindProperty("focusController").objectReferenceValue = focusCtrl;
            dSo.FindProperty("monitor").objectReferenceValue = monitor;
            dSo.ApplyModifiedPropertiesWithoutUndo();

            // Buraco de visão estilo BG3 (substitui o corte de paredes): alvos = atores escondidos, só na câmera principal.
            var seeThrough = systems.AddComponent<SeeThroughTargets>();
            var stSo = new SerializedObject(seeThrough);
            stSo.FindProperty("focusController").objectReferenceValue = focusCtrl;
            stSo.FindProperty("mainCamera").objectReferenceValue = cam;
            stSo.ApplyModifiedPropertiesWithoutUndo();

            return focusCtrl;
        }

        // ------------------------------------------------------------------ Pós-processamento

        private const string PostFxPath = Root + "/Settings/PP_FilmeDeTerror.asset";

        /// <summary>
        /// "Cara de filme" do guia de arte §6: tonemapping Neutral, saturação −10, contraste +15, split toning
        /// (sombras azul-violeta, realces laranja), vinheta, granulação fina e bloom para lâmpadas e olhos.
        /// O perfil é um asset; os valores do guia são reaplicados a cada reconstrução (HTVisualSetup.ApplyPostProcessing).
        /// </summary>
        private static void BuildPostProcessing()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostFxPath);
            if (profile == null)
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PostFxPath));
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, PostFxPath);

                var vignette = profile.Add<Vignette>(true);
                vignette.intensity.Override(0.4f);
                vignette.smoothness.Override(0.45f);

                var grain = profile.Add<FilmGrain>(true);
                grain.intensity.Override(0.35f);

                var color = profile.Add<ColorAdjustments>(true);
                color.contrast.Override(18f);
                color.saturation.Override(-30f);

                var tone = profile.Add<Tonemapping>(true);
                tone.mode.Override(TonemappingMode.ACES);

                var bloom = profile.Add<Bloom>(true);
                bloom.intensity.Override(0.4f);
                bloom.threshold.Override(1f);

                // Cada efeito é um "sub-asset" guardado dentro do arquivo do perfil.
                foreach (var component in profile.components)
                {
                    AssetDatabase.AddObjectToAsset(component, profile);
                }
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
            }
            HTVisualSetup.ApplyPostProcessing(profile);

            var go = new GameObject("PostFX");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        private static void BuildSystems(GameContentDef content, NavMeshSurface surface, ActorFocusController focus,
                                         HouseGenDef houseGen, HouseBuilder houseBuilder)
        {
            GameObject systems = focus.gameObject;

            var presenter = systems.AddComponent<RunPresenter>();
            var pSo = new SerializedObject(presenter);
            pSo.FindProperty("content").objectReferenceValue = content;
            pSo.FindProperty("navMeshSurface").objectReferenceValue = surface;
            pSo.FindProperty("focusController").objectReferenceValue = focus;
            var camGo = GameObject.FindWithTag("MainCamera");
            pSo.FindProperty("mainCamera").objectReferenceValue = camGo != null ? camGo.GetComponent<Camera>() : null;
            var monitor = systems.GetComponent<DirectorMonitor>();
            var director = systems.GetComponent<CinematicDirector>();
            pSo.FindProperty("monitor").objectReferenceValue = monitor;
            // Casa gerada (P2). Na cena do P0 ficam vazios: planta fixa, comportamento antigo.
            pSo.FindProperty("houseGen").objectReferenceValue = houseGen;
            pSo.FindProperty("houseBuilder").objectReferenceValue = houseBuilder;
            pSo.ApplyModifiedPropertiesWithoutUndo();

            var dSo = new SerializedObject(director);
            dSo.FindProperty("presenter").objectReferenceValue = presenter;
            dSo.ApplyModifiedPropertiesWithoutUndo();

            var hud = systems.AddComponent<RunHud>();
            var hSo = new SerializedObject(hud);
            hSo.FindProperty("presenter").objectReferenceValue = presenter;
            hSo.FindProperty("director").objectReferenceValue = director;
            hSo.FindProperty("monitor").objectReferenceValue = monitor;
            hSo.ApplyModifiedPropertiesWithoutUndo();

            // HUD nova (UI Toolkit). A antiga (RunHud) fica no objeto: F1 alterna entre as duas.
            var hudRoot = systems.AddComponent<HudRoot>();
            var rSo = new SerializedObject(hudRoot);
            rSo.FindProperty("presenter").objectReferenceValue = presenter;
            rSo.FindProperty("director").objectReferenceValue = director;
            rSo.FindProperty("monitor").objectReferenceValue = monitor;
            rSo.FindProperty("legacyHud").objectReferenceValue = hud;
            rSo.FindProperty("panelSettings").objectReferenceValue = HudAssetsSetup.LoadPanelSettings();
            rSo.FindProperty("compositeShader").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Shader>(HudAssetsSetup.CompositeShaderPath);
            var sheets = HudAssetsSetup.LoadStyleSheets();
            var sheetsProp = rSo.FindProperty("styleSheets");
            sheetsProp.arraySize = sheets.Length;
            for (int i = 0; i < sheets.Length; i++) sheetsProp.GetArrayElementAtIndex(i).objectReferenceValue = sheets[i];
            rSo.ApplyModifiedPropertiesWithoutUndo();
            if (HudAssetsSetup.LoadPanelSettings() == null) Debug.LogWarning("[HorrorTycoon] HUD nova sem PanelSettings: rode 'Horror Tycoon/HUD/Criar assets da HUD'.");
        }

        private static LensSettings MakeLens(float fov)
        {
            LensSettings lens = LensSettings.Default;
            lens.FieldOfView = fov;
            lens.NearClipPlane = 0.1f;
            lens.FarClipPlane = 200f;
            return lens;
        }

        // ------------------------------------------------------------------ Utilidades

        /// <summary>
        /// Lê uma lista de referências do asset pelo SerializedObject (o mesmo caminho do Inspector).
        /// Mais seguro que ler a lista em C# logo após trocar de cena: o Unity pode ter descarregado
        /// os assets da memória, e a lista em C# ficaria com referências "mortas".
        /// </summary>
        private static T[] ReadList<T>(Object owner, string fieldName) where T : Object
        {
            var so = new SerializedObject(owner);
            var prop = so.FindProperty(fieldName);
            var result = new T[prop.arraySize];
            for (int i = 0; i < prop.arraySize; i++)
            {
                result[i] = prop.GetArrayElementAtIndex(i).objectReferenceValue as T;
            }
            return result;
        }

        private static void AddSceneToBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes;
            foreach (var s in scenes)
            {
                if (s.path == path)
                {
                    return;
                }
            }

            var list = new List<EditorBuildSettingsScene>(scenes)
            {
                new EditorBuildSettingsScene(path, true)
            };
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
