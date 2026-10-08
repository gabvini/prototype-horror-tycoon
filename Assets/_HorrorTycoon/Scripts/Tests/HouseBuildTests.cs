using System.Collections.Generic;
using System.Linq;
using HorrorTycoon.Actors;
using HorrorTycoon.Core;
using HorrorTycoon.Rooms;
using HorrorTycoon.Rooms.Building;
using HorrorTycoon.Rooms.Generation;
using HorrorTycoon.Run;
using HorrorTycoon.Scoring;
using HorrorTycoon.UI;
using NUnit.Framework;
using UnityEngine;

namespace HorrorTycoon.Tests
{
    /// <summary>
    /// Testes da MONTAGEM 3D sem cena: a régua planta → mundo (HouseWorldMap), vãos nas paredes,
    /// porta principal/giro do interior, camadas de luz e o card do corredor.
    /// </summary>
    public class HouseBuildTests
    {
        private const float FrontZ = -10f;
        private const float Eps = 0.001f;

        private EncounterDef encNada;
        private ActorDef ator;
        private GameRulesDef rules;
        private FilmFormatDef format;
        private RoomDef corridor, hall, jantar;
        private List<RoomDef> pool;

        [SetUp]
        public void SetUp()
        {
            encNada = ScriptableObject.CreateInstance<EncounterDef>();
            encNada.Setup("Nada", "", 20, 0, null, null);
            ator = ScriptableObject.CreateInstance<ActorDef>();
            ator.Setup("Ator", "", Color.white, new List<TagDef>(), 1, 0);
            rules = ScriptableObject.CreateInstance<GameRulesDef>();
            format = ScriptableObject.CreateInstance<FilmFormatDef>();

            corridor = MakeDef("Corredor", SpaceKind.Corridor, new Vector2Int(2, 2), false);
            hall = MakeDef("Hall", SpaceKind.Social, new Vector2Int(5, 4), true);
            jantar = MakeDef("Jantar", SpaceKind.Social, new Vector2Int(4, 4), false);
            pool = new List<RoomDef>
            {
                MakeDef("A", SpaceKind.Room, new Vector2Int(4, 5), true),
                MakeDef("B", SpaceKind.Room, new Vector2Int(4, 4), false),
                MakeDef("C", SpaceKind.Room, new Vector2Int(3, 3), false),
                MakeDef("D", SpaceKind.Room, new Vector2Int(5, 4), false),
                MakeDef("E", SpaceKind.Room, new Vector2Int(4, 5), false),
                MakeDef("F", SpaceKind.Room, new Vector2Int(3, 4), false),
                MakeDef("G", SpaceKind.Room, new Vector2Int(4, 4), false),
            };
        }

        private RoomDef MakeDef(string name, SpaceKind kind, Vector2Int size, bool required)
        {
            var r = ScriptableObject.CreateInstance<RoomDef>();
            var encounters = new List<RoomDef.WeightedEncounter> { new RoomDef.WeightedEncounter { encounter = encNada, weight = 1 } };
            r.Setup(name, "", Color.gray, new List<string>(), encounters, new List<PayoffDef>(), null);
            r.SetupGen(kind, size, 1f, 1, required);
            return r;
        }

        private HouseGenDef MakeGen()
        {
            var g = ScriptableObject.CreateInstance<HouseGenDef>();
            g.corridorDef = corridor;
            g.socialPool = new List<RoomDef> { hall, jantar };
            g.extraSocials = new Vector2Int(0, 1);
            return g;
        }

        private HouseLayout Gen(int seed) => HouseGenerator.Generate(MakeGen(), pool, new GameRandom(seed));

        // ==================================================================== Régua planta → mundo

        [Test]
        public void Mapa_FachadaFixa_PegadaCentradaEmX_DentroDoRetanguloMaximo()
        {
            for (int seed = 1; seed <= 100; seed++)
            {
                var l = Gen(seed);
                var map = new HouseWorldMap(l, FrontZ, true);
                Rect fp = map.FootprintWorld;

                Assert.AreEqual(0f, fp.center.x, Eps, $"seed {seed}: pegada centrada em x = 0");
                Assert.AreEqual(FrontZ, map.ToWorld(l.FrontDoor.Position).z, Eps, $"seed {seed}: porta da frente na fachada");
                Assert.AreEqual(FrontZ, fp.yMin, Eps, $"seed {seed}: nada na frente da fachada");

                // Cabe no retângulo máximo centrado (é o que o cenário evita).
                float half = l.Bounds.x * 0.5f;
                Assert.GreaterOrEqual(fp.xMin, -half - Eps, $"seed {seed}");
                Assert.LessOrEqual(fp.xMax, half + Eps, $"seed {seed}");
                Assert.LessOrEqual(fp.yMax, FrontZ + l.Bounds.y + Eps, $"seed {seed}");
            }
        }

        // ==================================================================== Paredes e vãos

        [Test]
        public void Paredes_CadaLigacaoViraVao_PortaComVerga_PassagemSemVerga()
        {
            const float wallH = 2.7f, doorH = 2.1f, maxPiece = 4f;
            for (int seed = 1; seed <= 100; seed++)
            {
                var l = Gen(seed);
                var map = new HouseWorldMap(l, FrontZ, true);
                var withGap = new HashSet<int>();

                foreach (var w in l.Walls)
                {
                    var pieces = HouseWorldMap.Pieces(l, w, wallH, doorH, maxPiece);
                    float solid = pieces.Where(p => !p.Lintel).Sum(p => p.Length);
                    foreach (var p in pieces.Where(p => !p.Lintel))
                    {
                        Assert.LessOrEqual(p.Length, maxPiece + Eps, $"seed {seed}: pedaço maior que o máximo");
                        Assert.AreEqual(0f, p.Bottom, Eps);
                        Assert.AreEqual(wallH, p.Height, Eps);
                    }

                    if (w.ConnectionIndex < 0)
                    {
                        Assert.AreEqual(w.Length, solid, Eps, $"seed {seed}: parede sólida inteira");
                        Assert.IsFalse(pieces.Any(p => p.Lintel));
                        continue;
                    }

                    var c = l.Connections[w.ConnectionIndex];
                    withGap.Add(c.Index);
                    Assert.IsTrue(HouseWorldMap.GapOf(l, w, out float g0, out float g1, out ConnectionType type), $"seed {seed}");
                    Assert.AreEqual(c.Width, g1 - g0, Eps, $"seed {seed}: vão do tamanho da ligação {c.Index}");
                    Assert.AreEqual(w.Length, solid + (g1 - g0), Eps, $"seed {seed}: sólido + vão = parede");

                    // Nada sólido no chão dentro do vão.
                    foreach (var p in pieces.Where(p => !p.Lintel))
                    {
                        Assert.IsTrue(p.To <= g0 + Eps || p.From >= g1 - Eps, $"seed {seed}: parede tapando o vão {c.Index}");
                    }

                    var lintels = pieces.Where(p => p.Lintel).ToList();
                    if (type == ConnectionType.Door)
                    {
                        Assert.AreEqual(1, lintels.Count, $"seed {seed}: porta {c.Index} sem verga");
                        Assert.AreEqual(g0, lintels[0].From, Eps);
                        Assert.AreEqual(g1, lintels[0].To, Eps);
                        Assert.AreEqual(doorH, lintels[0].Bottom, Eps);
                        Assert.AreEqual(wallH - doorH, lintels[0].Height, Eps);
                    }
                    else
                    {
                        Assert.AreEqual(0, lintels.Count, $"seed {seed}: passagem {c.Index} é vão inteiro");
                    }

                    // No mundo, o centro do vão cai sobre a linha da parede.
                    Vector3 gap = map.WallPoint(w, (g0 + g1) * 0.5f);
                    Vector3 expected = map.ToWorld(c.Position);
                    Assert.AreEqual(expected.x, gap.x, Eps, $"seed {seed}");
                    Assert.AreEqual(expected.z, gap.z, Eps, $"seed {seed}");
                    Assert.AreEqual(map.WallLineWorld(w), w.Horizontal ? expected.z : expected.x, Eps, $"seed {seed}");
                }

                Assert.AreEqual(l.Connections.Count, withGap.Count, $"seed {seed}: toda ligação tem vão");
            }
        }

        // ==================================================================== Porta principal e interior

        [Test]
        public void PortaPrincipal_DirecaoParaFora_EGiroDoInterior()
        {
            for (int seed = 1; seed <= 100; seed++)
            {
                var l = Gen(seed);
                var map = new HouseWorldMap(l, FrontZ, true);
                for (int i = 0; i < l.Spaces.Count; i++)
                {
                    var s = l.Spaces[i];
                    var c = HouseWorldMap.PrimaryConnection(l, i);
                    Assert.IsNotNull(c, $"seed {seed}: espaço {i} sem porta");

                    if (i == l.StartSpaceIndex) Assert.AreSame(l.FrontDoor, c, "inicial: porta da frente");
                    else if (s.ParentIndex >= 0) Assert.AreEqual(s.ParentIndex, c.Other(i), $"seed {seed}: porta do espaço {i} = ligação com o pai");

                    // A porta fica na borda do espaço e a direção "para fora" aponta do centro para ela.
                    Vector2Int o = HouseWorldMap.Outward(l, i, c);
                    Assert.AreEqual(1, Mathf.Abs(o.x) + Mathf.Abs(o.y));
                    if (i == l.StartSpaceIndex) Assert.AreEqual(new Vector2Int(0, -1), o, "porta da frente sai para o sul");
                    Vector3 toDoor = map.ToWorld(c.Position) - map.SpaceCenter(i);
                    Vector3 outWorld = HouseWorldMap.OutwardWorld(o);
                    float half = c.HorizontalWall ? s.Rect.height * 0.5f : s.Rect.width * 0.5f;
                    Assert.AreEqual(half, Vector3.Dot(toDoor, outWorld), Eps, $"seed {seed}: porta do espaço {i} fora da borda");

                    // FurnitureKit: porta no +X local.
                    float yaw = HouseWorldMap.FurnitureYaw(o);
                    Vector3 localX = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
                    Assert.AreEqual(outWorld.x, localX.x, 1e-4f, $"seed {seed}");
                    Assert.AreEqual(outWorld.z, localX.z, 1e-4f, $"seed {seed}");
                    Vector2 local = HouseWorldMap.LocalSize(s.Rect, yaw);
                    Assert.AreEqual(s.Rect.width * s.Rect.height, local.x * local.y, Eps);

                    // Prefab de interior: tamanho local = tamanho do RoomDef (quando o retângulo é ele, girado ou não).
                    var size = new Vector2Int(s.Rect.width, s.Rect.height);
                    var def = s.Def.Size;
                    if (size == def || size == new Vector2Int(def.y, def.x))
                    {
                        Vector2 authored = HouseWorldMap.LocalSize(s.Rect, HouseWorldMap.AuthoredYaw(s, o));
                        Assert.AreEqual((Vector2)def, authored, $"seed {seed}: prefab do espaço {i} giraria errado");
                    }
                }
            }
        }

        // ==================================================================== Camadas de luz

        [Test]
        public void CamadasDeLuz_EspacosEncostadosTemGruposDiferentesQuandoPossivel()
        {
            const int groups = HouseBuilder.LightGroupCount;
            for (int seed = 1; seed <= 100; seed++)
            {
                var l = Gen(seed);
                int[] g = HouseWorldMap.LightGroups(l, 5.5f, groups);
                Assert.AreEqual(l.Spaces.Count, g.Length);
                Assert.IsTrue(g.All(x => x >= 0 && x < groups));

                for (int j = 0; j < l.Spaces.Count; j++)
                {
                    for (int i = 0; i < j; i++)
                    {
                        if (g[i] != g[j] || HouseWorldMap.Gap(l.Spaces[i].Rect, l.Spaces[j].Rect) > 0f) continue;
                        // Só aceitável se TODOS os grupos já estavam nos vizinhos encostados anteriores.
                        var used = new HashSet<int>();
                        for (int k = 0; k < j; k++)
                        {
                            if (HouseWorldMap.Gap(l.Spaces[k].Rect, l.Spaces[j].Rect) <= 0f) used.Add(g[k]);
                        }
                        Assert.AreEqual(groups, used.Count, $"seed {seed}: espaços {i} e {j} encostados com a mesma luz");
                    }
                }
                Assert.AreEqual(g, HouseWorldMap.LightGroups(l, 5.5f, groups), "determinístico");
            }
        }

        // ==================================================================== Card da sala

        [Test]
        public void Card_CorredorSemBotoes_SalaEConvivenciaComBotoes()
        {
            var content = ScriptableObject.CreateInstance<GameContentDef>();
            content.Setup(format, rules, new List<ActorDef> { ator }, new List<RoomDef>(pool), null, true,
                new List<VillainDef>(), new List<ElementDef>());
            bool sawCorridor = false;
            for (int seed = 1; seed <= 20; seed++)
            {
                var run = new FilmRun(content, seed, MakeGen());
                run.Begin();
                foreach (var room in run.Rooms)
                {
                    bool corridorSpace = room.Kind == SpaceKind.Corridor;
                    sawCorridor |= corridorSpace;
                    Assert.AreEqual(!corridorSpace, RunHud.ShowsMoveButtons(room), $"seed {seed}: {room.Kind}");
                    Assert.AreEqual(!corridorSpace, RunHud.ShowsLabel(room));
                    if (corridorSpace) Assert.IsFalse(run.CanMove(run.Actors[0], room.Index), "ninguém é mandado para o corredor");
                }
            }
            Assert.IsTrue(sawCorridor, "esperava corredores nas casas geradas");
        }

        [Test]
        public void Card_PlantaFixa_HubContinuaComBotoes()
        {
            var hub = MakeDef("Corredor P0", SpaceKind.Room, new Vector2Int(2, 2), false);
            var content = ScriptableObject.CreateInstance<GameContentDef>();
            content.Setup(format, rules, new List<ActorDef> { ator }, new List<RoomDef> { pool[1], pool[2] }, hub, true,
                new List<VillainDef>(), new List<ElementDef>());
            var run = new FilmRun(content, 3);
            run.Begin();
            Assert.IsTrue(run.Rooms.All(RunHud.ShowsMoveButtons), "P0: todo lugar tem 'Quem vai?'");
            Assert.IsTrue(run.Rooms.All(RunHud.ShowsLabel), "P0: todos com nome");
        }
    }
}
