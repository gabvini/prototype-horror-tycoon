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
    /// Casa por escolha (HouseGenDef.growByDraft): esqueleto + portas para o vazio, oferta de salas, sala nova na planta,
    /// FilmRun.Draft e replay. Sem cena. Conteúdo com as medidas da escala de cinema (×1,5).
    /// </summary>
    public class HouseDraftTests
    {
        private EncounterDef encNada;
        private GameRulesDef rules;
        private FilmFormatDef format;
        private RoomDef corridor, hall, jantar, tv, porao;
        private List<RoomDef> pool;
        private List<ActorDef> actors;

        [SetUp]
        public void SetUp()
        {
            encNada = ScriptableObject.CreateInstance<EncounterDef>();
            encNada.Setup("Nada", "", 20, 0, null, null);
            rules = ScriptableObject.CreateInstance<GameRulesDef>();
            format = ScriptableObject.CreateInstance<FilmFormatDef>();

            corridor = MakeDef("Corredor", SpaceKind.Corridor, new Vector2Int(3, 3), false);
            hall = MakeDef("Hall", SpaceKind.Social, new Vector2Int(8, 6), true);
            jantar = MakeDef("Jantar", SpaceKind.Social, new Vector2Int(6, 6), false);
            tv = MakeDef("TV", SpaceKind.Social, new Vector2Int(8, 6), false);
            porao = MakeDef("Porão", SpaceKind.Room, new Vector2Int(6, 8), false);
            porao.SetupBuild(0, null, true, "Trancado.");
            pool = new List<RoomDef>
            {
                MakeDef("Cozinha", SpaceKind.Room, new Vector2Int(6, 8), false),
                MakeDef("Sala de estar", SpaceKind.Room, new Vector2Int(8, 6), false),
                MakeDef("Banheiro", SpaceKind.Room, new Vector2Int(5, 5), false),
                MakeDef("Quarto", SpaceKind.Room, new Vector2Int(6, 6), false),
                porao,
                MakeDef("Sótão", SpaceKind.Room, new Vector2Int(6, 6), false),
            };

            actors = new List<ActorDef>();
            foreach (var n in new[] { "Ana", "Beto", "Final Girl" })
            {
                var a = ScriptableObject.CreateInstance<ActorDef>();
                a.Setup(n, "", Color.white, new List<TagDef>(), 1, 0);
                actors.Add(a);
            }
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
            g.socialPool = new List<RoomDef> { hall, jantar, tv };
            g.growByDraft = true;
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

        // ==================================================================== Esqueleto

        [Test]
        public void Esqueleto_SemSalas_ComPortasParaOVazioEmParedesExternas()
        {
            for (int seed = 1; seed <= 150; seed++)
            {
                var run = NewRun(seed);
                var l = run.Layout;
                Assert.IsTrue(l.GrowsByDraft);
                Assert.IsTrue(l.Validate(out string error), $"seed {seed}: {error}");
                Assert.AreEqual(0, l.CountOf(SpaceKind.Room), $"seed {seed}: o esqueleto não tem salas");
                Assert.Greater(l.Sites.Count, 0, $"seed {seed}: sem portas para o vazio");

                foreach (var s in l.Sites)
                {
                    var walls = l.Walls.Where(w => w.SiteIndices.Contains(s.Index)).ToList();
                    Assert.AreEqual(1, walls.Count, $"seed {seed}: porta {s.Index} deve cair em 1 trecho de parede");
                    Assert.IsTrue(walls[0].IsExterior && walls[0].A == s.Host, $"seed {seed}: porta {s.Index} fora da parede externa do host");
                    Assert.IsTrue(HouseWorldMap.Gaps(l, walls[0]).Any(g => g.Site == s.Index && Mathf.Abs(g.To - g.From - s.Width) < 0.01f),
                        $"seed {seed}: vão da porta {s.Index}");
                    Assert.AreNotEqual(SpaceKind.Room, l.Spaces[s.Host].Kind);
                    Assert.IsTrue(run.SiteHasRoom(s.Index), $"seed {seed}: porta {s.Index} sem sala que caiba no começo");
                }
            }
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
                foreach (var def in pool)
                {
                    if (!HouseDraft.TryPlace(l, s, def, 0.45f, out var p)) continue;
                    RectInt host = l.Spaces[s.Host].Rect;
                    Assert.IsTrue(HouseLayout.TryGetSharedWall(host, p.Rect, out var from, out var to), "a sala encosta no host");
                    float along = s.HorizontalWall ? s.Position.x : s.Position.y;
                    float lo = s.HorizontalWall ? from.x : from.y, hi = s.HorizontalWall ? to.x : to.y;
                    Assert.LessOrEqual(lo, along - s.Width * 0.5f - 0.45f + 0.001f);
                    Assert.GreaterOrEqual(hi, along + s.Width * 0.5f + 0.45f - 0.001f);
                    Vector2Int size = p.Rotated ? new Vector2Int(def.Size.y, def.Size.x) : def.Size;
                    Assert.AreEqual(size, new Vector2Int(p.Rect.width, p.Rect.height));
                }
            }
        }

        // ==================================================================== Abrir a porta

        [Test]
        public void Draft_SalaNasce_AtorEntra_GastaCena_PlantaContinuaValida()
        {
            for (int seed = 1; seed <= 150; seed++)
            {
                var run = NewRun(seed);
                var l = run.Layout;
                int guard = 0;
                while (guard++ < 10 && run.Status == RunStatus.Playing)
                {
                    var actor = run.Actors.First(a => a.Alive);
                    int site = FirstDraftable(run, actor);
                    if (site < 0) break;
                    var offer = run.OpenDraft(site);
                    int before = run.Rooms.Count, scenes = run.ActionsLeft, act = run.ActIndex;

                    var outcome = run.Draft(actor, site, (seed + guard) % offer.Options.Count);

                    Assert.AreEqual(before + 1, run.Rooms.Count, $"seed {seed}");
                    Assert.AreEqual(before, actor.RoomIndex, "o ator entra na sala nova");
                    Assert.AreSame(offer.Options[(seed + guard) % offer.Options.Count], run.Rooms[before].Def);
                    Assert.IsNotNull(outcome.Encounter, "abrir a porta é explorar: tem encontro");
                    if (run.ActIndex == act && run.Status == RunStatus.Playing)
                        Assert.AreEqual(scenes - rules.exploreActionCost, run.ActionsLeft, "custa como explorar");
                    Assert.IsTrue(l.Validate(out string error), $"seed {seed}: {error}");
                    Assert.Greater(run.Map.Doors(l.StartSpaceIndex, before), 0, "sala nova ligada à casa");
                    Assert.IsFalse(l.Sites[site].IsOpen);
                    Assert.AreEqual(before, l.Sites[site].Space);
                    foreach (var c in l.Connections)
                        Assert.AreEqual(1, l.Walls.Count(w => w.ConnectionIndex == c.Index), $"seed {seed}: ligação {c.Index}");

                    if (run.CanMove(actor, l.StartSpaceIndex)) run.Move(actor, l.StartSpaceIndex);
                    if (run.AwaitingArtefatoChoice) run.ChooseArtefato(null);
                }
                Assert.Greater(l.CountOf(SpaceKind.Room), 0, $"seed {seed}: nenhuma sala nasceu");
            }
        }

        [Test]
        public void Draft_SalaUsadaNaoVoltaNaOferta()
        {
            var run = NewRun(11);
            var actor = run.Actors[0];
            int site = FirstDraftable(run, actor);
            var chosen = run.OpenDraft(site).Options[0];
            run.Draft(actor, site, 0);
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
