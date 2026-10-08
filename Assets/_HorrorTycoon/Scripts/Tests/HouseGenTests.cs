using System.Collections.Generic;
using System.Linq;
using HorrorTycoon.Actors;
using HorrorTycoon.Core;
using HorrorTycoon.Rooms;
using HorrorTycoon.Rooms.Generation;
using HorrorTycoon.Run;
using HorrorTycoon.Scoring;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HorrorTycoon.Tests
{
    /// <summary>
    /// Testes da casa gerada (HouseGenerator + HouseMap com pesos + FilmRun com HouseGenDef). Sem cena.
    /// </summary>
    public class HouseGenTests
    {
        private EncounterDef encNada;
        private ActorDef lento, rapido;
        private GameRulesDef rules;
        private FilmFormatDef format;
        private RoomDef corridor, hall, jantar, requiredRoom;
        private List<RoomDef> pool;

        [SetUp]
        public void SetUp()
        {
            encNada = ScriptableObject.CreateInstance<EncounterDef>();
            encNada.Setup("Nada", "", 20, 0, null, null);
            lento = ScriptableObject.CreateInstance<ActorDef>();
            lento.Setup("Lento", "", Color.white, new List<TagDef>(), 1, 0);
            rapido = ScriptableObject.CreateInstance<ActorDef>();
            rapido.Setup("Rápido", "", Color.white, new List<TagDef>(), 2, 0);
            rules = ScriptableObject.CreateInstance<GameRulesDef>();
            format = ScriptableObject.CreateInstance<FilmFormatDef>();

            // Convivência e corredor COM encontros de propósito: o jogo deve ignorá-los.
            corridor = MakeDef("Corredor", SpaceKind.Corridor, new Vector2Int(2, 2), false, true);
            hall = MakeDef("Hall", SpaceKind.Social, new Vector2Int(5, 4), true, true);
            jantar = MakeDef("Jantar", SpaceKind.Social, new Vector2Int(4, 4), false, true);

            requiredRoom = MakeDef("Obrigatória", SpaceKind.Room, new Vector2Int(4, 5), true, true);
            pool = new List<RoomDef>
            {
                requiredRoom,
                MakeDef("A", SpaceKind.Room, new Vector2Int(4, 4), false, true),
                MakeDef("B", SpaceKind.Room, new Vector2Int(3, 3), false, true),
                MakeDef("C", SpaceKind.Room, new Vector2Int(4, 5), false, true),
                MakeDef("D", SpaceKind.Room, new Vector2Int(5, 4), false, true),
                MakeDef("E", SpaceKind.Room, new Vector2Int(4, 4), false, true),
                MakeDef("F", SpaceKind.Room, new Vector2Int(3, 4), false, true),
                MakeDef("G", SpaceKind.Room, new Vector2Int(4, 4), false, true),
            };
        }

        private RoomDef MakeDef(string name, SpaceKind kind, Vector2Int size, bool required, bool withEncounter)
        {
            var r = ScriptableObject.CreateInstance<RoomDef>();
            var encounters = new List<RoomDef.WeightedEncounter>();
            if (withEncounter) encounters.Add(new RoomDef.WeightedEncounter { encounter = encNada, weight = 1 });
            r.Setup(name, "", Color.gray, new List<string>(), encounters, new List<PayoffDef>(), null);
            r.SetupGen(kind, size, 1f, 1, required);
            return r;
        }

        private HouseGenDef MakeGen(int openingCost = 0)
        {
            var g = ScriptableObject.CreateInstance<HouseGenDef>();
            g.corridorDef = corridor;
            g.socialPool = new List<RoomDef> { hall, jantar };
            g.openingCost = openingCost;
            return g;
        }

        private FilmRun NewRun(HouseGenDef gen, int seed, params ActorDef[] actorDefs)
        {
            var content = ScriptableObject.CreateInstance<GameContentDef>();
            content.Setup(format, rules, new List<ActorDef>(actorDefs), new List<RoomDef>(pool), null, true,
                new List<VillainDef>(), new List<ElementDef>());
            var run = new FilmRun(content, seed, gen);
            run.Begin();
            return run;
        }

        private static HouseLayout Gen(HouseGenDef g, IReadOnlyList<RoomDef> p, int seed) =>
            HouseGenerator.Generate(g, p, new GameRandom(seed));

        private static string Signature(HouseLayout l) =>
            string.Join("|", l.Spaces.Select(s => $"{s.Kind}:{s.Def.name}:{s.Rect}")) + "#" +
            string.Join("|", l.Connections.Select(c => $"{c.A}-{c.B}:{c.Type}:{c.Position}"));

        // ==================================================================== Gerador

        [Test]
        public void Gerador_MesmaSeedMesmaCasa_SeedsDiferentesCasasDiferentes()
        {
            var g = MakeGen();
            Assert.AreEqual(Signature(Gen(g, pool, 42)), Signature(Gen(g, pool, 42)));

            var signatures = new HashSet<string>();
            for (int seed = 1; seed <= 20; seed++) signatures.Add(Signature(Gen(g, pool, seed)));
            Assert.Greater(signatures.Count, 15, "seeds diferentes devem gerar casas diferentes");
        }

        [Test]
        public void Gerador_SemSobreposicao_DentroDosLimites_TudoConectado_PortasNasParedes()
        {
            var g = MakeGen();
            for (int seed = 1; seed <= 200; seed++)
            {
                var l = Gen(g, pool, seed);
                Assert.IsTrue(l.Validate(out string error), $"seed {seed}: {error}");

                // Cada ligação aparece em exatamente 1 trecho de parede (a montagem 3D abre o vão ali).
                foreach (var c in l.Connections)
                {
                    Assert.AreEqual(1, l.Walls.Count(w => w.ConnectionIndex == c.Index), $"seed {seed}: ligação {c.Index}");
                }
                // A porta da frente está na borda sul, numa parede externa.
                Assert.AreEqual(0f, l.FrontDoor.Position.y);
                Assert.IsTrue(l.Walls.Single(w => w.ConnectionIndex == l.FrontDoorIndex).IsExterior);
            }
        }

        [Test]
        public void Gerador_QuantidadeDeSalas_EObrigatoriasPresentes()
        {
            var g = MakeGen();
            for (int seed = 1; seed <= 200; seed++)
            {
                var l = Gen(g, pool, seed);
                int rooms = l.CountOf(SpaceKind.Room);
                Assert.That(rooms, Is.InRange(g.roomCount.x, g.roomCount.y), $"seed {seed}");
                Assert.IsTrue(l.Spaces.Any(s => s.Def == requiredRoom), $"seed {seed}: falta sala obrigatória");
                Assert.AreEqual(SpaceKind.Social, l.Spaces[l.StartSpaceIndex].Kind);
                Assert.AreSame(hall, l.Spaces[l.StartSpaceIndex].Def, "a convivência obrigatória é a inicial");
            }
        }

        [Test]
        public void Gerador_PoolPequeno_LimitaQuantidadeSemFalhar()
        {
            var g = MakeGen();
            var small = pool.Take(3).ToList();
            var l = Gen(g, small, 7);
            Assert.AreEqual(3, l.CountOf(SpaceKind.Room));
            Assert.IsFalse(l.UsedFallback);
        }

        // ==================================================================== HouseMap com pesos

        [Test]
        public void HouseMap_PassagemCusto0ou1_PortaCusto1()
        {
            var g = MakeGen();
            bool sawLonger = false;
            for (int seed = 1; seed <= 50; seed++)
            {
                var l = Gen(g, pool, seed);
                var map0 = new HouseMap(l);
                var map1 = new HouseMap(l, -1, 1);
                Assert.AreEqual(1, map0.Doors(HouseMap.Outside, l.StartSpaceIndex), "porta da frente = 1");
                Assert.AreEqual(l.StartSpaceIndex, map0.Hub);
                foreach (var s in l.Spaces)
                {
                    if (s.Kind != SpaceKind.Room) continue;
                    int d0 = map0.Doors(l.StartSpaceIndex, s.Index);
                    int d1 = map1.Doors(l.StartSpaceIndex, s.Index);
                    // Passagem grátis: só contam as portas (1 para a sala, +1 se for funda).
                    Assert.AreEqual(s.IsDeep ? 2 : 1, d0, $"seed {seed}, sala {s.Index}");
                    Assert.GreaterOrEqual(d1, d0);
                    if (d1 > d0) sawLonger = true;
                }
            }
            Assert.IsTrue(sawLonger, "com passagem = 1, salas longe do início custam mais");
        }

        // ==================================================================== FilmRun com casa gerada

        [Test]
        public void CasaGerada_AtoresComecamNaConvivenciaInicial()
        {
            var run = NewRun(MakeGen(), 3, lento, rapido);
            int start = run.Layout.StartSpaceIndex;
            Assert.AreEqual(SpaceKind.Social, run.Rooms[start].Kind);
            Assert.IsTrue(run.Rooms[start].Discovered);
            Assert.IsTrue(run.Rooms[start].IsHub);
            Assert.AreEqual(start, run.Map.Hub);
            Assert.AreEqual(start, run.Map.StartIndex);
            foreach (var a in run.Actors) Assert.AreEqual(start, a.RoomIndex);
            Assert.AreEqual(run.Layout.Spaces.Count, run.Rooms.Count);
        }

        [Test]
        public void CasaGerada_ConvivenciaECorredorNuncaTemEncontro()
        {
            for (int seed = 1; seed <= 20; seed++)
            {
                var run = NewRun(MakeGen(), seed, lento);
                foreach (var room in run.Rooms)
                {
                    if (room.Kind == SpaceKind.Room) Assert.IsNotNull(room.Encounter);
                    else
                    {
                        Assert.IsNull(room.Encounter, $"seed {seed}: {room.Kind} com encontro");
                        Assert.IsTrue(room.IsHub);
                        Assert.IsFalse(room.IsExplorable);
                    }
                }
            }
        }

        /// <summary>
        /// ADAPTADO no Protótipo 2 (antes: "..._MudarCustaNoMinimo1"): ir para outra convivência agora é GRÁTIS
        /// (não dispara nada). Sala nova continua custando 1.
        /// </summary>
        [Test]
        public void CasaGerada_CorredorNaoEDestino_SalaESim_ConvivenciaGratis()
        {
            var gen = MakeGen();
            gen.extraSocials = new Vector2Int(1, 1);
            var run = NewRun(gen, 11, lento);
            var a = run.Actors[0];

            int corridorIdx = run.Rooms.First(r => r.Kind == SpaceKind.Corridor).Index;
            Assert.IsFalse(run.CanMove(a, corridorIdx), "corredor é só passagem");

            var room = run.Rooms.First(r => r.Kind == SpaceKind.Room && !r.Space.IsDeep);
            Assert.AreEqual(1, run.MoveCost(a, room.Index));
            Assert.IsTrue(run.CanMove(a, room.Index));

            // Outra convivência: não dispara nada → grátis.
            var other = run.Rooms.FirstOrDefault(r => r.Kind == SpaceKind.Social && r.Index != run.Layout.StartSpaceIndex);
            Assert.IsNotNull(other, "esperava uma convivência extra nesta seed");
            Assert.AreEqual(0, run.Map.Doors(a.RoomIndex, other.Index));
            Assert.AreEqual(0, run.MoveCost(a, other.Index));
            Assert.IsTrue(run.CanMove(a, other.Index));
            int before = run.ActionsLeft;
            var outcome = run.Move(a, other.Index);
            Assert.IsNull(outcome.Encounter, "convivência não tem encontro");
            Assert.AreEqual(before, run.ActionsLeft);
        }

        /// <summary>
        /// ADAPTADO no Protótipo 2 (antes: "CasaGerada_AtletaArredondaParaCima"): o custo não depende mais
        /// da distância nem do DoorsPerStep. Sala nova longe (3+ portas) custa 1 para os dois atores.
        /// </summary>
        [Test]
        public void CasaGerada_SalaNovaLongeCusta1_IndependenteDaDistancia()
        {
            var gen = MakeGen(openingCost: 1);
            for (int seed = 1; seed <= 50; seed++)
            {
                var run = NewRun(gen, seed, lento, rapido);
                foreach (var room in run.Rooms)
                {
                    if (room.Kind != SpaceKind.Room) continue;
                    int doors = run.Map.Doors(run.Layout.StartSpaceIndex, room.Index);
                    if (doors < 3) continue;
                    Assert.AreEqual(1, run.MoveCost(run.Actors[0], room.Index));
                    Assert.AreEqual(1, run.MoveCost(run.Actors[1], room.Index));
                    return;
                }
            }
            Assert.Fail("nenhuma sala a 3+ portas encontrada");
        }

        [Test]
        public void PlantaFixa_IgnoraConvivenciasDoCatalogo()
        {
            var hub = MakeDef("Corredor", SpaceKind.Room, new Vector2Int(2, 2), false, false);
            var content = ScriptableObject.CreateInstance<GameContentDef>();
            content.Setup(format, rules, new List<ActorDef> { lento }, new List<RoomDef> { pool[1], jantar, pool[2] },
                hub, true, new List<VillainDef>(), new List<ElementDef>());
            var run = new FilmRun(content, 5);
            run.Begin();
            Assert.AreEqual(3, run.Rooms.Count, "corredor + 2 salas; a convivência fica de fora");
            Assert.IsFalse(run.Rooms.Any(r => r.Def == jantar));
            Assert.IsNull(run.Layout);
            Assert.AreEqual(HouseMap.Outside, run.Actors[0].RoomIndex, "planta fixa: começa lá fora");
            Assert.IsTrue(run.CanMove(run.Actors[0], 0), "planta fixa: o corredor central continua sendo destino");
        }

        // ==================================================================== Estatísticas (não verifica nada)

        [Test]
        public void Estatisticas_500Casas_ComConteudoReal()
        {
            var gen = AssetDatabase.LoadAssetAtPath<HouseGenDef>("Assets/_HorrorTycoon/Data/P1/Geracao/Casa_Padrao.asset");
            var content = AssetDatabase.LoadAssetAtPath<GameContentDef>("Assets/_HorrorTycoon/Data/P1/Conteudo_P1.asset");
            if (gen == null || content == null) Assert.Ignore("Conteúdo real não encontrado (rode 'Criar Conteúdo P1').");

            var roomPool = content.Rooms.Concat(gen.extraRoomPool)
                .Where(r => r != null && r.Kind == SpaceKind.Room).Distinct().ToList();
            Debug.Log(HouseGenStats.Run(gen, roomPool, 500));
            Debug.Log("[HouseGenStats] pool de 8 salas (teste):\n" + HouseGenStats.Run(MakeGen(), pool, 500));
        }
    }
}
