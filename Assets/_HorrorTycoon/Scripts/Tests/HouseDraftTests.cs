using System.Collections.Generic;
using System.Linq;
using HorrorTycoon.Actors;
using HorrorTycoon.Core;
using HorrorTycoon.Rooms;
using HorrorTycoon.Rooms.Building;
using HorrorTycoon.Rooms.Generation;
using HorrorTycoon.Run;
using HorrorTycoon.Scoring;
using NUnit.Framework;
using UnityEngine;

namespace HorrorTycoon.Tests
{
    /// <summary>
    /// Set em grid (HouseGenDef.growByDraft): começo só com o Hall + portas para o vazio, oferta de peças, peça nova na planta
    /// (1 célula = 1 cômodo de 6 m, girada para encaixar, com as portas dela), FilmRun.Draft e replay. Sem cena.
    /// </summary>
    public class HouseDraftTests
    {
        private EncounterDef encNada;
        private GameRulesDef rules;
        private FilmFormatDef format;
        private RoomDef corridor, corridorL, corridorT, cross, hall, jantar, tv, porao, banheiro;
        private List<RoomDef> pool;
        private List<ActorDef> actors;

        [SetUp]
        public void SetUp()
        {
            encNada = ScriptableObject.CreateInstance<EncounterDef>();
            encNada.Setup("Nada", "", 20, 0, null, null);
            rules = ScriptableObject.CreateInstance<GameRulesDef>();
            format = ScriptableObject.CreateInstance<FilmFormatDef>();

            // N = 1, L = 2, S = 4, O = 8 (sem giro; "sul" = a entrada). 0 = os 4 lados.
            corridor = MakeDef("Corredor", SpaceKind.Corridor, new Vector2Int(6, 6), false, DoorSides.North | DoorSides.South);
            corridorL = MakeDef("Corredor em L", SpaceKind.Corridor, new Vector2Int(6, 6), false, DoorSides.South | DoorSides.East);
            corridorT = MakeDef("Corredor em T", SpaceKind.Corridor, new Vector2Int(6, 6), false, DoorSides.South | DoorSides.East | DoorSides.West);
            cross = MakeDef("Cruzamento", SpaceKind.Corridor, new Vector2Int(6, 6), false);
            hall = MakeDef("Hall", SpaceKind.Social, new Vector2Int(6, 6), true);
            jantar = MakeDef("Jantar", SpaceKind.Social, new Vector2Int(6, 6), false, DoorSides.South | DoorSides.East | DoorSides.West);
            tv = MakeDef("TV", SpaceKind.Social, new Vector2Int(12, 6), false);
            porao = MakeDef("Porão", SpaceKind.Room, new Vector2Int(6, 6), false, DoorSides.South);
            porao.SetupBuild(0, null, true, "Trancado.");
            banheiro = MakeDef("Banheiro", SpaceKind.Room, new Vector2Int(6, 6), false, DoorSides.South);
            pool = new List<RoomDef>
            {
                MakeDef("Cozinha", SpaceKind.Room, new Vector2Int(6, 6), false, DoorSides.South | DoorSides.North | DoorSides.East),
                MakeDef("Sala de estar", SpaceKind.Room, new Vector2Int(12, 6), false),
                banheiro,
                MakeDef("Quarto", SpaceKind.Room, new Vector2Int(6, 6), false, DoorSides.South | DoorSides.East),
                porao,
                MakeDef("Sótão", SpaceKind.Room, new Vector2Int(6, 6), false, DoorSides.South | DoorSides.North),
            };

            actors = new List<ActorDef>();
            foreach (var n in new[] { "Ana", "Beto", "Final Girl" })
            {
                var a = ScriptableObject.CreateInstance<ActorDef>();
                a.Setup(n, "", Color.white, new List<TagDef>(), 1, 0);
                actors.Add(a);
            }
        }

        private RoomDef MakeDef(string name, SpaceKind kind, Vector2Int size, bool required, DoorSides doors = DoorSides.None)
        {
            var r = ScriptableObject.CreateInstance<RoomDef>();
            var encounters = new List<RoomDef.WeightedEncounter> { new RoomDef.WeightedEncounter { encounter = encNada, weight = 1 } };
            r.Setup(name, "", Color.gray, new List<string>(), encounters, new List<PayoffDef>(), null);
            r.SetupGen(kind, size, 1f, 1, required);
            r.SetupDraft(HouseZone.None, doors);
            return r;
        }

        private HouseGenDef MakeGen()
        {
            var g = ScriptableObject.CreateInstance<HouseGenDef>();
            g.corridorDef = corridor;
            g.socialPool = new List<RoomDef> { hall, jantar, tv };
            g.growByDraft = true;
            g.gridCell = 6;
            g.bounds = new Vector2Int(42, 36);
            g.corridorPieces = new List<RoomDef> { corridorL, corridorT, cross };
            g.doorCornerMargin = 0.4f;
            return g;
        }

        private FilmRun NewRun(int seed)
        {
            var content = ScriptableObject.CreateInstance<GameContentDef>();
            content.Setup(format, rules, actors, pool, null, true, new List<VillainDef>(), new List<ElementDef>());
            var run = new FilmRun(content, seed, MakeGen());
            run.Begin();
            return run;
        }

        private static int FirstDraftable(FilmRun run, ActorRunState actor)
        {
            for (int i = 0; i < run.DoorSites.Count; i++) if (run.CanDraft(actor, i)) return i;
            return -1;
        }

        private static string Signature(HouseLayout l) =>
            string.Join("|", l.Spaces.Select(s => $"{s.Def.DisplayName}:{s.Rect}"));

        // ==================================================================== Começo

        [Test]
        public void Comeco_SoOHall_NaFrente_ComPortasParaOVazio()
        {
            for (int seed = 1; seed <= 50; seed++)
            {
                var run = NewRun(seed);
                var l = run.Layout;
                Assert.IsTrue(l.GrowsByDraft);
                Assert.IsTrue(l.Validate(out string error), $"seed {seed}: {error}");
                Assert.AreEqual(1, l.Spaces.Count, "começa só com o Hall");
                Assert.AreSame(hall, l.Spaces[0].Def);
                Assert.AreEqual(new RectInt(18, 0, 6, 6), l.Spaces[0].Rect, "o Hall ocupa a célula do meio da fileira da frente");
                Assert.AreEqual(3, l.Sites.Count, "portas para o vazio nos lados norte, leste e oeste");
                Assert.IsTrue(run.Actors.All(a => a.RoomIndex == 0), "o elenco começa no Hall");
                foreach (var s in l.Sites) AssertSiteOnWall(l, s, seed);
            }
        }

        private static void AssertSiteOnWall(HouseLayout l, HouseDoorSite s, int seed)
        {
            var walls = l.Walls.Where(w => w.SiteIndices.Contains(s.Index)).ToList();
            Assert.AreEqual(1, walls.Count, $"seed {seed}: porta {s.Index} deve cair em 1 trecho de parede");
            Assert.IsTrue(walls[0].IsExterior && walls[0].A == s.Host, $"seed {seed}: porta {s.Index} fora da parede externa do host");
            Assert.IsTrue(HouseWorldMap.Gaps(l, walls[0]).Any(g => g.Site == s.Index && Mathf.Abs(g.To - g.From - s.Width) < 0.01f),
                $"seed {seed}: vão da porta {s.Index}");
            float along = s.HorizontalWall ? s.Position.x : s.Position.y;
            Assert.AreEqual(3f, Mathf.Repeat(along, 6f), 0.001f, $"seed {seed}: porta {s.Index} fora do meio da célula");
        }

        // ==================================================================== Oferta

        [Test]
        public void Oferta_AteTresSalasDiferentes_QueCabem_SemLacradas_EGuardada()
        {
            for (int seed = 1; seed <= 100; seed++)
            {
                var run = NewRun(seed);
                var actor = run.Actors[0];
                int site = FirstDraftable(run, actor);
                var offer = run.OpenDraft(site);
                Assert.IsNotNull(offer);
                Assert.That(offer.Options.Count, Is.InRange(1, 3));
                Assert.AreEqual(offer.Options.Count, offer.Options.Distinct().Count(), "sem repetir sala na oferta");
                Assert.IsFalse(offer.Options.Contains(porao), "sala lacrada só depois de liberada");
                for (int i = 0; i < offer.Options.Count; i++)
                {
                    Assert.IsTrue(run.Layout.IsFree(offer.Placements[i].Rect), $"seed {seed}: opção {i} não cabe");
                }

                // Fechar e abrir de novo não troca as salas.
                var again = run.OpenDraft(site);
                CollectionAssert.AreEqual(offer.Options, again.Options);
            }
        }

        [Test]
        public void TryPlace_SalaColadaAoHost_CobrindoOVaoComMargens()
        {
            var run = NewRun(3);
            var l = run.Layout;
            foreach (var s in l.Sites)
            {
                foreach (var def in pool.Concat(new[] { corridor, corridorL, corridorT, cross, jantar, tv }))
                {
                    if (!HouseDraft.TryPlace(l, s, def, new DraftRules(), out var p)) continue;
                    RectInt host = l.Spaces[s.Host].Rect;
                    Assert.IsTrue(HouseLayout.TryGetSharedWall(host, p.Rect, out var from, out var to), "a sala encosta no host");
                    float along = s.HorizontalWall ? s.Position.x : s.Position.y;
                    float lo = s.HorizontalWall ? from.x : from.y, hi = s.HorizontalWall ? to.x : to.y;
                    Assert.LessOrEqual(lo, along - s.Width * 0.5f - 0.4f + 0.001f);
                    Assert.GreaterOrEqual(hi, along + s.Width * 0.5f + 0.4f - 0.001f);
                    Assert.AreEqual(0, p.Rect.xMin % 6, "alinhada ao grid");
                    Assert.AreEqual(0, p.Rect.yMin % 6, "alinhada ao grid");
                    Assert.IsTrue(HouseDraft.SideHasDoor(def, -s.Outward, p.Quarter), "girada para ter porta do lado da entrada");
                    Vector2Int size = p.Rotated ? new Vector2Int(def.Size.y, def.Size.x) : def.Size;
                    Assert.AreEqual(size, new Vector2Int(p.Rect.width, p.Rect.height));
                }
            }
        }

        // ==================================================================== Abrir a porta

        [Test]
        public void Draft_PecaNasce_AlinhadaAoGrid_ComPortasNovas_PlantaContinuaValida()
        {
            int corridors = 0, socials = 0, roomsBuilt = 0;
            for (int seed = 1; seed <= 150; seed++)
            {
                var run = NewRun(seed);
                var l = run.Layout;
                int guard = 0;
                while (guard++ < 14 && run.Status == RunStatus.Playing)
                {
                    var actor = run.Actors.First(a => a.Alive);
                    int site = FirstDraftable(run, actor);
                    if (site < 0) break;
                    var offer = run.OpenDraft(site);
                    Assert.LessOrEqual(offer.Options.Count(o => o.Kind == SpaceKind.Corridor), 1, "no máximo 1 corredor por oferta");
                    int before = run.Rooms.Count, scenes = run.ActionsLeft, act = run.ActIndex;
                    int option = (seed + guard) % offer.Options.Count;
                    var def = offer.Options[option];
                    int from = actor.RoomIndex;

                    var outcome = run.Draft(actor, site, option);

                    Assert.AreEqual(before + 1, run.Rooms.Count, $"seed {seed}");
                    Assert.AreSame(def, run.Rooms[before].Def);
                    var rect = l.Spaces[before].Rect;
                    Assert.IsTrue(rect.xMin % 6 == 0 && rect.yMin % 6 == 0 && rect.width % 6 == 0 && rect.height % 6 == 0, $"seed {seed}: fora do grid {rect}");
                    if (def.Kind == SpaceKind.Room)
                    {
                        roomsBuilt++;
                        Assert.AreEqual(before, actor.RoomIndex, "o ator entra na sala nova");
                        Assert.IsNotNull(outcome.Encounter, "abrir a porta de uma sala é explorar");
                    }
                    else if (def.Kind == SpaceKind.Social) { socials++; Assert.AreEqual(before, actor.RoomIndex); }
                    else { corridors++; Assert.AreEqual(from, actor.RoomIndex, "corredor é só passagem: o ator fica"); }
                    if (run.ActIndex == act && run.Status == RunStatus.Playing)
                        Assert.AreEqual(scenes - rules.exploreActionCost, run.ActionsLeft, "custa uma cena");
                    Assert.IsTrue(l.Validate(out string error), $"seed {seed}: {error}");
                    Assert.Greater(run.Map.Doors(l.StartSpaceIndex, before), 0, "peça nova ligada à casa");
                    Assert.IsFalse(l.Sites[site].IsOpen);
                    foreach (var c in l.Connections)
                        Assert.AreEqual(1, l.Walls.Count(w => w.ConnectionIndex == c.Index), $"seed {seed}: ligação {c.Index}");
                    foreach (var s in l.Sites.Where(x => x.IsOpen)) AssertSiteOnWall(l, s, seed);

                    if (run.CanMove(actor, l.StartSpaceIndex)) run.Move(actor, l.StartSpaceIndex);
                    if (run.AwaitingArtefatoChoice) run.ChooseArtefato(null);
                }
                Assert.Greater(l.Spaces.Count, 1, $"seed {seed}: nenhuma peça nasceu");
            }
            Assert.Greater(roomsBuilt, 0);
            Assert.Greater(corridors, 0, "corredores aparecem na escolha");
            Assert.Greater(socials, 0, "convivências aparecem na escolha");
        }

        [Test]
        public void Draft_SalaUsadaNaoVoltaNaOferta()
        {
            var run = NewRun(11);
            var actor = run.Actors[0];
            int site = FirstDraftable(run, actor);
            var offer = run.OpenDraft(site);
            int option = offer.Options.FindIndex(o => o.Kind == SpaceKind.Room);
            Assert.GreaterOrEqual(option, 0, "a 1ª oferta tem alguma sala");
            var chosen = offer.Options[option];
            run.Draft(actor, site, option);
            run.Move(actor, run.Layout.StartSpaceIndex);
            for (int i = 0; i < run.DoorSites.Count; i++)
            {
                if (!run.CanDraft(actor, i)) continue;
                Assert.IsFalse(run.OpenDraft(i).Options.Contains(chosen), "máximo 1 por casa");
            }
        }

        [Test]
        public void Replay_MesmaSeedMesmasAcoes_MesmaCasa()
        {
            for (int seed = 1; seed <= 40; seed++)
            {
                var run = NewRun(seed);
                for (int k = 0; k < 4; k++)
                {
                    var actor = run.Actors[k % run.Actors.Count];
                    if (!actor.Alive) continue;
                    int site = FirstDraftable(run, actor);
                    if (site < 0) break;
                    var offer = run.OpenDraft(site);
                    run.Draft(actor, site, k % offer.Options.Count);
                    if (run.AwaitingArtefatoChoice) run.ChooseArtefato(null);
                }

                var again = NewRun(seed);
                RunReplay.Apply(again, run.Log.actions);
                Assert.AreEqual(Signature(run.Layout), Signature(again.Layout), $"seed {seed}");
                Assert.AreEqual(run.TotalScore, again.TotalScore, $"seed {seed}");
            }
        }

        [Test]
        public void Portas_SoNosLadosDaPeca_GiradaParaEncaixar()
        {
            // Banheiro só tem porta no "sul" (a entrada): girado para a porta dar no host e sem portas novas.
            for (int seed = 1; seed <= 60; seed++)
            {
                var run = NewRun(seed);
                var l = run.Layout;
                var hallRect = l.Spaces[0].Rect;
                int site = l.Sites.First(x => x.Outward == new Vector2Int(1, 0)).Index; // porta leste do Hall
                Assert.IsTrue(HouseDraft.TryPlace(l, l.Sites[site], banheiro, new DraftRules(), out var p));
                Assert.AreEqual(new RectInt(hallRect.xMax, 0, 6, 6), p.Rect);
                Assert.AreEqual(1, p.Quarter, "sul girado 90° horário = oeste (encosta no Hall)");
                int before = l.Sites.Count;
                HouseDraft.AddRoom(l, l.Sites[site], banheiro, p, new DraftRules());
                Assert.AreEqual(before, l.Sites.Count, "banheiro é beco sem saída: nenhuma porta nova");
            }
        }

        [Test]
        public void Portas_DeVizinhas_ViramPassagemOuParede()
        {
            // Hall no meio; Cruzamento ao leste e ao norte. O cruzamento do norte tem porta para o sul (o Hall) e leste;
            // a porta para o vazio leste dele cai na célula acima do cruzamento do leste, que fica livre.
            var run = NewRun(4);
            var l = run.Layout;
            var rules = new DraftRules();
            var east = l.Sites.First(x => x.Outward == new Vector2Int(1, 0));
            HouseDraft.TryPlace(l, east, cross, rules, out var pe);
            int iEast = HouseDraft.AddRoom(l, east, cross, pe, rules);
            var north = l.Sites.First(x => x.Host == 0 && x.Outward == new Vector2Int(0, 1));
            HouseDraft.TryPlace(l, north, cross, rules, out var pn);
            int iNorth = HouseDraft.AddRoom(l, north, cross, pn, rules);
            // Porta norte do cruzamento leste e porta leste do cruzamento norte dão na mesma célula (diagonal do Hall):
            // as duas são portas para o vazio abertas.
            var a = l.Sites.First(x => x.Host == iEast && x.Outward == new Vector2Int(0, 1));
            var b = l.Sites.First(x => x.Host == iNorth && x.Outward == new Vector2Int(1, 0));
            Assert.IsTrue(a.IsOpen && b.IsOpen);
            // Um Cruzamento ali liga os dois lados (vira passagem dos dois).
            HouseDraft.TryPlace(l, a, cross, rules, out var pc);
            int iCorner = HouseDraft.AddRoom(l, a, cross, pc, rules);
            Assert.AreEqual(DoorSiteState.Built, b.State);
            Assert.AreEqual(iCorner, b.Space);
            Assert.IsTrue(l.Validate(out string error), error);
        }

        [Test]
        public void SemDraft_CasaGeradaContinuaIgual()
        {
            var gen = MakeGen();
            gen.growByDraft = false;
            var content = ScriptableObject.CreateInstance<GameContentDef>();
            content.Setup(format, rules, actors, pool, null, true, new List<VillainDef>(), new List<ElementDef>());
            var run = new FilmRun(content, 5, gen);
            Assert.IsFalse(run.GrowsByDraft);
            Assert.AreEqual(0, run.DoorSites.Count);
            Assert.Greater(run.Layout.CountOf(SpaceKind.Room), 0);
            Assert.IsTrue(run.Layout.Walls.All(w => w.SiteIndices.Count == 0));
        }
    }
}
