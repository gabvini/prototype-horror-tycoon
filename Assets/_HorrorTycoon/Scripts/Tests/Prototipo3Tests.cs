using System.Collections.Generic;
using System.Linq;
using HorrorTycoon.Actors;
using HorrorTycoon.Core;
using HorrorTycoon.Rooms;
using HorrorTycoon.Run;
using HorrorTycoon.Scoring;
using NUnit.Framework;
using UnityEngine;

namespace HorrorTycoon.Tests
{
    /// <summary>
    /// Protótipo 3 — "Build do Filme": cena como turno (tique da casa), plots de permanência, classes (passivos),
    /// cenas de dupla, vilão Fantasma, crise de pavor, artefatos e reprodução pelo log.
    /// Mini-casa em estrela (planta fixa): corredor [0] + [1] Quarto (palco Susto/Morte) · [2] Sótão · [3] Porão (LACRADO) ·
    /// [4] Sala (encontro: Crucifixo). Na estrela todas as salas são vizinhas do corredor (sala → sala = 2 espaços).
    /// Encontros valem 0 pontos para a conta dos testes ficar limpa.
    /// </summary>
    public class Prototipo3Tests
    {
        private TagDef slasher, sobrenatural;
        private PayoffDef susto, morte;
        private ToolDef crucifixo;
        private EncounterDef encNada, encCruz, encVinte;
        private ActorDef atleta, nerd, finalGirl, popular;
        private RoomDef hub, quarto, sotao, porao, sala;
        private PlotDef plotCasal, plotDiario, plotMisterio;
        private DuoComboDef casal, investigacao, grupo;
        private VillainDef mascarado, fantasma;
        private GameRulesDef rules;
        private FilmFormatDef format;
        private List<ArtefatoDef> artefatos;

        [SetUp]
        public void SetUp()
        {
            slasher = ScriptableObject.CreateInstance<TagDef>();
            sobrenatural = ScriptableObject.CreateInstance<TagDef>();
            susto = ScriptableObject.CreateInstance<PayoffDef>();
            susto.Setup("Susto", "", Color.white, 60, 0, false, 0);
            morte = ScriptableObject.CreateInstance<PayoffDef>();
            morte.Setup("Morte", "", Color.white, 100, 0, true, 1);

            crucifixo = ScriptableObject.CreateInstance<ToolDef>();
            crucifixo.Setup("Crucifixo", "", sobrenatural);
            encNada = ScriptableObject.CreateInstance<EncounterDef>();
            encNada.Setup("Nada", "", 0, 0, null, null);
            encCruz = ScriptableObject.CreateInstance<EncounterDef>();
            encCruz.Setup("Crucifixo na parede", "", 0, 0, null, crucifixo);
            encVinte = ScriptableObject.CreateInstance<EncounterDef>();
            encVinte.Setup("Barulho", "", 20, 0, null, null);

            hub = Room("Corredor", null);
            quarto = Room("Quarto", encNada, susto, morte);
            sotao = Room("Sótão", encNada);
            porao = Room("Porão", encNada);
            sala = Room("Sala", encCruz);

            plotCasal = ScriptableObject.CreateInstance<PlotDef>();
            plotCasal.Setup("Casal no Quarto", "", new List<ActorRole> { ActorRole.Atleta, ActorRole.Popular }, 1, 120);
            plotDiario = ScriptableObject.CreateInstance<PlotDef>();
            plotDiario.Setup("O diário", "", new List<ActorRole> { ActorRole.FinalGirl }, 2, 100, null, true);
            plotDiario.SetupReward(porao);
            plotMisterio = ScriptableObject.CreateInstance<PlotDef>();
            plotMisterio.Setup("Mistério", "", new List<ActorRole> { ActorRole.Nerd }, 2, 90, new List<RoomDef> { sala }, true);

            quarto.SetupBuild(0, new List<PlotDef> { plotCasal });
            sotao.SetupBuild(0, new List<PlotDef> { plotDiario });
            porao.SetupBuild(0, null, true, "lacrado");

            atleta = Actor("Atleta", ActorRole.Atleta);
            nerd = Actor("Nerd", ActorRole.Nerd, plotMisterio);
            finalGirl = Actor("Final Girl", ActorRole.FinalGirl);
            popular = Actor("Popular", ActorRole.Popular);

            casal = ScriptableObject.CreateInstance<DuoComboDef>();
            casal.Setup("Casal", "", new List<ActorRole> { ActorRole.Atleta, ActorRole.Popular }, 0, true, 40);
            investigacao = ScriptableObject.CreateInstance<DuoComboDef>();
            investigacao.Setup("Investigação", "", new List<ActorRole> { ActorRole.Nerd, ActorRole.FinalGirl }, 0, true, 0, 1);
            grupo = ScriptableObject.CreateInstance<DuoComboDef>();
            grupo.Setup("Grupo", "", new List<ActorRole>(), 3, false, 0, 0, 0.5f, true);

            mascarado = ScriptableObject.CreateInstance<VillainDef>();
            mascarado.Setup("Mascarado", "", Color.red, slasher);
            fantasma = ScriptableObject.CreateInstance<VillainDef>();
            fantasma.Setup("Entidade", "", Color.blue, sobrenatural, VillainLook.Ghost); // família Auto → Fantasma

            artefatos = new List<ArtefatoDef>
            {
                Art("Câmera na mão", ArtefatoEffectType.ComboScoreMult, 2f),
                Art("Hora extra", ArtefatoEffectType.ExtraScenesPerAct, 1f),
                Art("Camomila", ArtefatoEffectType.PavorGainMult, 0.5f),
                Art("Trilha de violinos", ArtefatoEffectType.ScareScoreMult, 1.5f),
            };

            rules = ScriptableObject.CreateInstance<GameRulesDef>();
            format = ScriptableObject.CreateInstance<FilmFormatDef>();
            format.Acts[0].goal = 0;
            format.Acts[1].goal = 0;
            format.Acts[2].goal = 0;
        }

        private RoomDef Room(string name, EncounterDef enc, params PayoffDef[] stage)
        {
            var r = ScriptableObject.CreateInstance<RoomDef>();
            var encs = new List<RoomDef.WeightedEncounter>();
            if (enc != null) encs.Add(new RoomDef.WeightedEncounter { encounter = enc, weight = 1 });
            r.Setup(name, "", Color.gray, new List<string>(), encs, new List<PayoffDef>(stage), null);
            return r;
        }

        private static ActorDef Actor(string name, ActorRole role, PlotDef plot = null)
        {
            var a = ScriptableObject.CreateInstance<ActorDef>();
            a.Setup(name, "", Color.white, new List<TagDef>(), 1, 0);
            a.SetupRole(role, "", "", plot != null ? new List<PlotDef> { plot } : null);
            return a;
        }

        private static ArtefatoDef Art(string name, ArtefatoEffectType type, float v)
        {
            var a = ScriptableObject.CreateInstance<ArtefatoDef>();
            a.Setup(name, "", Color.white, new List<ArtefatoDef.Effect> { ArtefatoDef.Fx(type, v) });
            return a;
        }

        private FilmRun NewRun(VillainDef villain, bool withArtefatos, params ActorDef[] cast)
        {
            var content = ScriptableObject.CreateInstance<GameContentDef>();
            content.Setup(format, rules, new List<ActorDef>(cast), new List<RoomDef> { quarto, sotao, porao, sala },
                hub, false, new List<VillainDef> { villain }, new List<ElementDef>());
            content.SetupBuild(new List<DuoComboDef> { casal, investigacao, grupo }, withArtefatos ? artefatos : new List<ArtefatoDef>());
            var run = new FilmRun(content, 7);
            run.Begin();
            return run;
        }

        private static ActorRunState A(FilmRun run, ActorDef def) => run.Actors.First(a => a.Def == def);

        /// <summary>Fim do Ato 1 → vilão (sem artefatos no conteúdo) → Ato 2.</summary>
        private static void ToAct2(FilmRun run, VillainDef villain)
        {
            run.EndActEarly();
            Assert.IsTrue(run.AwaitingVillainChoice);
            run.ChooseVillain(villain);
            Assert.AreEqual(1, run.ActIndex);
        }

        /// <summary>Desliga o pavor por cena (sozinho/perto do vilão) e a audiência "perto do vilão": contas exatas.</summary>
        private void QuietTick()
        {
            rules.alonePavorPerScene = 0;
            rules.nearVillainPavorPerScene = 0;
            rules.nearVillainScenePoints = 0;
        }

        // ==================================================================== Cena = turno / tique da casa

        [Test]
        public void Cena_AndarGratisNaoConta_GravarCenaGasta1ERodaOTique()
        {
            rules.alonePavorPerScene = 3;
            var run = NewRun(mascarado, false, atleta, popular);
            var a = A(run, atleta);
            run.Move(a, 0);                        // corredor: grátis, sem tique
            Assert.AreEqual(0, run.SceneNumber);
            Assert.AreEqual(8, run.ScenesLeft);
            run.Move(a, 1);                        // explorar: 1 cena
            Assert.AreEqual(1, run.SceneNumber);
            Assert.AreEqual(7, run.ScenesLeft);
            Assert.IsTrue(run.CanRecordScene);
            run.RecordScene();                     // esperar 1 cena
            Assert.AreEqual(2, run.SceneNumber);
            Assert.AreEqual(6, run.ScenesLeft);
            Assert.AreEqual(3 + 3, a.Pavor, "sozinho no quarto por 2 cenas (alonePavorPerScene 3)");
        }

        [Test]
        public void Tique_OrdemFixa_PlotAntesDaCrise_CriseAntesDoVilao()
        {
            rules.nearVillainPavorPerScene = 0;
            rules.alonePavorPerScene = 3;
            var solo = ScriptableObject.CreateInstance<PlotDef>();
            solo.Setup("Solo", "", new List<ActorRole> { ActorRole.Popular }, 1, 50);
            sotao.SetupBuild(5, new List<PlotDef> { solo }); // sótão dá 5 de pavor por cena
            var run = NewRun(mascarado, false, popular, atleta);
            var p = A(run, popular);
            run.Move(p, 2);                         // explora o sótão (plot oferecido, conta da próxima cena)
            ToAct2(run, mascarado);
            var v = run.VillainNpc;
            v.Space = 0;                            // vilão no corredor, anunciado para o sótão
            v.NextSpace = 2;
            v.StunBeats = 0;
            p.Pavor = 92;                           // 92 + 5 (sótão) + 3 (sozinha) = 100 → crise no b4

            int logStart = run.Log.entries.Count;
            run.RecordScene();

            var types = run.Log.entries.Skip(logStart).Select(e => e.type).ToList();
            int iPlot = types.IndexOf("plot-done");
            int iCrisis = types.IndexOf("crisis");
            int iVillain = types.IndexOf("villain-move");
            Assert.GreaterOrEqual(iPlot, 0, "b1: o plot cumpriu");
            Assert.Greater(iCrisis, iPlot, "b3/b4: pavor e crise depois do plot");
            Assert.Greater(iVillain, iCrisis, "(c): o vilão age por último");
            Assert.AreEqual(PlotStatus.Completed, run.Plots.First(x => x.Def == solo).Status);
            Assert.IsFalse(p.Alive, "o vilão entrou no sótão e pegou a Popular sozinha (depois do plot)");
            Assert.AreEqual(ExitReason.Died, p.Exit);
        }

        // ==================================================================== Plots de permanência

        [Test]
        public void Plot_Casal_OferecidoNaExploracao_ContaDaProximaCena_PontuaComCombo()
        {
            QuietTick();
            var run = NewRun(mascarado, false, atleta, popular);
            var a = A(run, atleta);
            var p = A(run, popular);
            run.Move(a, 1);                                    // explora o Quarto: plot aparece
            var plot = run.Plots.Single(x => x.Def == plotCasal);
            Assert.AreEqual(0, plot.Progress, "achado nesta cena: só conta a partir da próxima");
            Assert.AreEqual("Quarto", plot.Source);
            run.Move(p, 1);                                    // grátis (já explorado): casal junto
            Assert.AreEqual(1, run.ActiveCombosIn(1).Count(c => c.Def == casal));
            int before = run.TotalScore;
            run.RecordScene();
            Assert.AreEqual(PlotStatus.Completed, plot.Status);
            // Plot: 120 × 1 (Ato 1) × 1,25 (Popular) = 150. Casal: 40 × 1 × 1,25 = 50.
            Assert.AreEqual(before + 150 + 50, run.TotalScore);
            Assert.IsTrue(run.Report.Lines.Any(l => l.Kind == ReportKind.PlotDone && l.Score == 150));
            Assert.IsTrue(run.Report.Lines.Any(l => l.Kind == ReportKind.Combo && l.Score == 50));
        }

        [Test]
        public void Plot_Diario_QuebraSeSair_RecomecaELiberaSalaLacrada()
        {
            QuietTick();
            var run = NewRun(mascarado, false, finalGirl, atleta);
            var fg = A(run, finalGirl);
            Assert.IsFalse(run.CanMove(fg, 3), "Porão lacrado");
            run.Move(fg, 2);                       // cena 1: plot oferecido
            var plot = run.Plots.Single(x => x.Def == plotDiario);
            run.RecordScene();                     // cena 2: 1/2
            Assert.AreEqual(1, plot.Progress);
            run.Move(fg, 0);                       // sai (grátis): quebra na hora
            Assert.AreEqual(0, plot.Progress);
            Assert.AreEqual(1, plot.TimesBroken);
            Assert.IsTrue(plot.IsActive, "quebrar não falha (failOnBreak = false): dá para recomeçar");
            run.Move(fg, 2);
            run.RecordScene();
            int before = run.TotalScore;
            run.RecordScene();
            Assert.AreEqual(PlotStatus.Completed, plot.Status);
            Assert.AreEqual(before + 100, run.TotalScore);
            Assert.IsFalse(run.Rooms[3].Sealed, "o diário liberou o Porão");
            Assert.IsTrue(run.CanMove(fg, 3));
            Assert.IsTrue(run.Report.Lines.Any(l => l.Kind == ReportKind.Unlock));
        }

        [Test]
        public void Plot_QuebraNaCrise_EFalhaSeQuemFaziaACenaSaiDoFilme()
        {
            QuietTick();
            plotDiario.SetupReward(porao, null, null, false, null, 0, "", true); // failOnBreak
            var run = NewRun(mascarado, false, finalGirl, atleta);
            var fg = A(run, finalGirl);
            run.Move(fg, 2);
            run.RecordScene();
            var plot = run.Plots.Single(x => x.Def == plotDiario);
            Assert.AreEqual(1, plot.Progress);
            fg.Pavor = 100;
            run.Move(A(run, atleta), 0);           // ação grátis: a crise (pavor no máximo) é conferida na próxima cena
            run.RecordScene();                     // b1: completa 2/2 ANTES da crise (b4)
            Assert.AreEqual(PlotStatus.Completed, plot.Status, "b1 vem antes da crise (b4)");
            Assert.AreEqual(1, fg.Crises);

            // Plot em andamento + crise = quebra (failOnBreak → fracassa).
            var lento = ScriptableObject.CreateInstance<PlotDef>();
            lento.Setup("Lento", "", new List<ActorRole> { ActorRole.Atleta }, 3, 30);
            lento.SetupReward(null, null, null, false, null, 0, "", true);
            quarto.SetupBuild(0, new List<PlotDef> { lento });
            var run3 = NewRun(mascarado, false, atleta, finalGirl);
            var at = A(run3, atleta);
            run3.Move(at, 1);
            run3.RecordScene();
            var pl = run3.Plots.Single(x => x.Def == lento);
            Assert.AreEqual(1, pl.Progress);
            at.Pavor = 100;
            run3.RecordScene();                    // b1: 2/3, b4: crise → quebra
            Assert.AreEqual(PlotStatus.Failed, pl.Status);

            // Plot de ator: o Nerd sai do filme → o plot dele falha.
            rules.crisesToLeave = 1;
            var run2 = NewRun(mascarado, false, nerd, atleta);
            var plot2 = run2.Plots.Single(x => x.Def == plotMisterio);
            var n = A(run2, nerd);
            run2.Move(n, 2);                       // longe da Sala
            n.Pavor = 100;
            run2.RecordScene();
            Assert.IsFalse(n.Alive);
            Assert.AreEqual(PlotStatus.Failed, plot2.Status);
        }

        // ==================================================================== Passivos

        [Test]
        public void Nerd_InvestigacaoUmaCenaAMenos_EPavorX1_5()
        {
            QuietTick();
            var run = NewRun(mascarado, false, nerd, atleta);
            var n = A(run, nerd);
            var plot = run.Plots.Single(x => x.Def == plotMisterio);
            Assert.AreEqual("Nerd", plot.Source, "plot vem do ator");
            Assert.AreEqual(2, run.RequiredScenes(plot, 4), "sem o Nerd na sala: 2 cenas");
            run.Move(n, 4);                        // plot de ator conta já na cena da exploração
            Assert.AreEqual(PlotStatus.Completed, plot.Status, "com o Nerd: 2 − 1 = 1 cena");
            Assert.AreEqual(15, run.ModifyPavorGain(n, 10));
            Assert.AreEqual(10, run.ModifyPavorGain(A(run, atleta), 10));
        }

        [Test]
        public void Atleta_AliadoGanhaMetadeDoPavor_ESeguraAPorta()
        {
            rules.nearVillainPavorPerScene = 0;
            var run = NewRun(mascarado, false, atleta, popular);
            var a = A(run, atleta);
            var p = A(run, popular);
            run.Move(a, 1);
            run.Move(p, 1);
            Assert.AreEqual(5, run.ModifyPavorGain(p, 10), "com o Atleta junto: metade");
            Assert.AreEqual(10, run.ModifyPavorGain(a, 10), "ele mesmo não");

            run.Move(p, 2);                         // Popular vai para outra sala
            ToAct2(run, mascarado);
            var v = run.VillainNpc;
            v.Space = 0;
            v.NextSpace = 1;                        // vai entrar no quarto do Atleta
            v.StunBeats = 0;
            Assert.IsTrue(run.CanHoldDoor(a));
            int scenes = run.ScenesLeft;
            run.HoldDoor(a);
            Assert.AreEqual(scenes, run.ScenesLeft, "segurar a porta é grátis");
            Assert.IsFalse(run.CanHoldDoor(a), "1× por ato");
            Assert.IsTrue(run.IsDoorHeldNext(1));
            run.RecordScene();
            Assert.AreEqual(0, v.Space, "barrado: ficou no corredor");
            Assert.IsTrue(a.Alive);
            v.NextSpace = 1;
            run.RecordScene();                      // a porta não está mais segurada
            Assert.AreEqual(1, v.Space);
            Assert.IsFalse(a.Alive, "sozinho: pego");
        }

        [Test]
        public void FinalGirl_AcalmaAliados_ETestemunharMorteZeraPavorEDaBonus()
        {
            QuietTick();
            rules.villainNpcEnabled = false;
            var run = NewRun(mascarado, false, finalGirl, popular, atleta);
            var fg = A(run, finalGirl);
            var p = A(run, popular);
            run.Move(fg, 1);
            run.Move(p, 1);
            p.Pavor = 50;
            run.RecordScene();
            Assert.AreEqual(40, p.Pavor, "−10 por cena com a Final Girl");

            ToAct2(run, mascarado);
            run.Move(fg, 0);                        // corredor: vizinho do quarto
            fg.Pavor = 80;
            int before = run.TotalScore;
            var outcome = run.Direct(p, morte);     // Popular morre no quarto
            Assert.IsTrue(outcome.Killed);
            Assert.AreEqual(0, fg.Pavor);
            Assert.IsTrue(fg.Determined);
            // Morte: 100 × 1,5 × (1 + 0) × 1,25 (Popular) = 187,5 → 188 · testemunha: 60 × 1,5 = 90.
            Assert.AreEqual(188, outcome.Score);
            Assert.AreEqual(before + outcome.Score + 90, run.TotalScore);
            Assert.AreEqual(5, run.ModifyPavorGain(fg, 10), "determinada: metade do pavor");
        }

        [Test]
        public void Popular_RendeMais_EEhAIscaDoVilao()
        {
            quarto.Setup("Quarto", "", Color.gray, new List<string>(),
                new List<RoomDef.WeightedEncounter> { new RoomDef.WeightedEncounter { encounter = encVinte, weight = 1 } },
                new List<PayoffDef>(), null);
            var run = NewRun(mascarado, false, atleta, popular);
            var o = run.Move(A(run, popular), 1);
            Assert.AreEqual(25, o.Points, "20 × 1,25");

            run.Move(A(run, atleta), 2);
            // Os dois sozinhos, mesmo pavor: a Popular é o alvo (isca), mesmo sendo a 2ª do elenco.
            A(run, atleta).Pavor = A(run, popular).Pavor;
            Assert.AreSame(A(run, popular), VillainAgent.ChooseTarget(run.Map, run.Actors, 0, x => x.Role == ActorRole.Popular));
            Assert.AreSame(A(run, atleta), VillainAgent.ChooseTarget(run.Map, run.Actors, 0), "sem isca: ordem do elenco");
        }

        // ==================================================================== Cenas de dupla e perto do vilão

        [Test]
        public void Combos_Investigacao_Acelera_Grupo_ReduzEBloqueiaAtaque()
        {
            QuietTick();
            var lento = ScriptableObject.CreateInstance<PlotDef>();
            lento.Setup("Lento", "", new List<ActorRole> { ActorRole.FinalGirl }, 3, 30);
            sotao.SetupBuild(0, new List<PlotDef> { lento });
            var run = NewRun(mascarado, false, finalGirl, nerd, atleta, popular);
            var fg = A(run, finalGirl);
            run.Move(fg, 2);
            run.Move(A(run, nerd), 2);              // grátis: Investigação ativa
            Assert.IsTrue(run.ActiveCombosIn(2).Any(c => c.Def == investigacao));
            run.RecordScene();
            Assert.AreEqual(2, run.Plots.Single(x => x.Def == lento).Progress, "anda 2 por cena");

            // Grupo: Atleta + Popular + Final Girl no quarto → Casal pela metade.
            run.Move(A(run, atleta), 1);
            run.Move(A(run, popular), 1);
            run.Move(fg, 1);
            Assert.IsTrue(run.ActiveCombosIn(1).Any(c => c.Def == grupo));
            run.RecordScene();
            Assert.IsTrue(run.Report.Lines.Any(l => l.Kind == ReportKind.Combo && l.Score == 25), "40 × 0,5 (grupo) × 1,25 (Popular)");

            ToAct2(run, mascarado);
            var v = run.VillainNpc;
            v.Space = 0;
            v.NextSpace = 1;
            v.StunBeats = 0;
            VillainEncounterOutcome enc = null;
            run.VillainEncountered += e => enc = e;
            int pavor = A(run, atleta).Pavor;
            run.RecordScene();
            Assert.IsNotNull(enc);
            Assert.AreEqual(VillainEncounterKind.GroupScare, enc.Kind);
            Assert.IsTrue(enc.Blocked, "Grupo: o vilão não ataca");
            Assert.AreEqual(pavor, A(run, atleta).Pavor);
        }

        [Test]
        public void PertoDoVilao_RendeAudienciaEPavor()
        {
            rules.alonePavorPerScene = 0;
            rules.nearVillainPavorPerScene = 6;
            rules.nearVillainScenePoints = 10;
            var run = NewRun(mascarado, false, atleta, nerd);
            var a = A(run, atleta);
            var n = A(run, nerd);
            run.Move(a, 1);
            run.Move(n, 1);
            ToAct2(run, mascarado);
            var v = run.VillainNpc;
            v.Space = 0;           // corredor: vizinho do quarto
            v.NextSpace = 0;
            v.StunBeats = 5;       // parado, para medir só o tique (b)
            a.Pavor = 0;
            n.Pavor = 0;
            run.RecordScene();
            var line = run.Report.Lines.Single(l => l.Kind == ReportKind.Score);
            Assert.AreEqual(2 * 15, line.Score, "10 × 1,5 (Ato 2) por ator");
            Assert.AreEqual(6, a.Pavor, "perto do vilão: +6 (o Atleta não reduz o próprio pavor)");
            Assert.AreEqual(4, n.Pavor, "Nerd: 6 × 1,5 (frágil) × 0,5 (junto do Atleta) = 4,5 → 4 (arredonda para o par)");
        }

        // ==================================================================== Fantasma

        [Test]
        public void Fantasma_EncheTensao_GrandeSusto_MudaParaASalaAnunciada()
        {
            QuietTick();
            rules.ghostTensionMax = 10;
            rules.ghostTensionPerScene = 2;
            rules.ghostTensionPerActor = 2;
            rules.ghostScorePerTension = 5;
            rules.ghostScarePavor = 35;
            var run = NewRun(fantasma, false, atleta, popular);
            run.Move(A(run, atleta), 1);
            run.Move(A(run, popular), 1);
            ToAct2(run, fantasma);
            var v = run.VillainNpc;
            Assert.IsTrue(v.IsGhost);
            v.Space = 1;
            v.NextSpace = 2;
            v.Tension = 0;
            GhostScareOutcome scare = null;
            run.GhostScared += o => scare = o;

            run.RecordScene();                       // +2 +2×2 = 6
            Assert.AreEqual(6, v.Tension);
            Assert.IsNull(scare);
            Assert.IsTrue(A(run, atleta).Alive, "fantasma não pega ninguém");

            int before = run.TotalScore;
            run.RecordScene();                       // 12 ≥ 10: Grande Susto
            Assert.IsNotNull(scare);
            Assert.AreEqual(12, scare.Tension);
            // 5 × 12 × 1,5 (Ato 2) × [(1 + 0/100) + (1 + 0/100)] (2 atores) × 1,25 (Popular lá dentro) = 225
            Assert.AreEqual(225, scare.Score);
            Assert.AreEqual(35, A(run, atleta).Pavor, "+35 de pavor (o Atleta não reduz o próprio)");
            Assert.AreEqual(18, A(run, popular).Pavor, "Popular junto do Atleta: 35 × 0,5 = 17,5 → 18");
            Assert.AreEqual(2, v.Space, "foi assombrar a sala anunciada");
            Assert.AreEqual(0, v.Tension);
            Assert.AreNotEqual(2, v.NextSpace, "anuncia a próxima");
            Assert.GreaterOrEqual(run.TotalScore - before, 225);
        }

        [Test]
        public void Fantasma_CrucifixoExorciza_EEntrarNaSalaDeleNaoMata()
        {
            QuietTick();
            var run = NewRun(fantasma, false, atleta, nerd);
            var a = A(run, atleta);
            run.Move(a, 4);                           // acha o Crucifixo
            CollectionAssert.Contains(a.Tools, crucifixo);
            run.Move(A(run, nerd), 2);
            ToAct2(run, fantasma);
            var v = run.VillainNpc;
            v.Space = 1;
            v.NextSpace = 2;
            v.Tension = 8;
            v.StunBeats = 0;
            GhostScareOutcome o = null;
            run.GhostScared += e => o = e;
            run.Move(a, 1);                           // entra na sala assombrada (sala nova no ato: 1 cena)
            Assert.IsTrue(a.Alive, "o fantasma não pega ninguém");
            Assert.IsNotNull(o, "no (c) desta mesma cena o Crucifixo exorciza");
            Assert.IsTrue(o.Exorcised);
            CollectionAssert.DoesNotContain(a.Tools, crucifixo, "gasto");
            Assert.AreEqual(0, v.Tension);
            Assert.AreNotEqual(1, v.Space);
            Assert.AreEqual(Mathf.RoundToInt(rules.repelScore * 1.5f), o.Score);
        }

        // ==================================================================== Crise

        [Test]
        public void Crise_TravaUmaCena_PavorVolta70_SegundaCriseSaiDoFilme()
        {
            rules.nearVillainPavorPerScene = 0;
            rules.alonePavorPerScene = 3;
            rules.crisisResetPavor = 70;
            rules.crisisPanicScore = 20;
            var run = NewRun(mascarado, false, atleta, popular);
            var a = A(run, atleta);
            run.Move(a, 1);
            a.Pavor = 97;
            int before = run.TotalScore;
            run.RecordScene();                        // sozinho +3 → 100 → crise
            Assert.AreEqual(1, a.Crises);
            Assert.AreEqual(70, a.Pavor);
            Assert.IsTrue(a.IsLocked);
            Assert.IsFalse(run.CanMove(a, 0), "travado: não anda");
            Assert.AreEqual(before + 20, run.TotalScore, "cena forçada de pânico: 20 × 1");
            Assert.IsTrue(run.Report.Lines.Any(l => l.Kind == ReportKind.Crisis));

            run.RecordScene();                        // passou 1 cena
            Assert.IsFalse(a.IsLocked);
            Assert.IsTrue(run.CanMove(a, 0));

            a.Pavor = 99;
            run.RecordScene();
            Assert.IsFalse(a.Alive, "2ª crise: sai do filme");
            Assert.AreEqual(ExitReason.Fled, a.Exit);
        }

        [Test]
        public void Crise_Desligada_VoltaARegraAntiga()
        {
            rules.crisisEnabled = false;
            var run = NewRun(mascarado, false, atleta, popular);
            var a = A(run, atleta);
            run.Move(a, 1);
            a.Pavor = 98;
            run.RecordScene();
            Assert.IsFalse(a.Alive);
            Assert.AreEqual(0, a.Crises);
        }

        // ==================================================================== Artefatos

        [Test]
        public void Artefatos_OfertaEntreAtos_AntesDoVilao_EDepoisProximoAto()
        {
            var run = NewRun(mascarado, true, atleta, popular);
            run.EndActEarly();
            Assert.IsTrue(run.AwaitingArtefatoChoice);
            Assert.IsFalse(run.AwaitingVillainChoice, "o artefato vem antes do vilão");
            Assert.IsFalse(run.CanAct);
            Assert.AreEqual(3, run.ArtefatoOffer.Count);
            Assert.AreEqual(3, run.ArtefatoOffer.Distinct().Count());
            var pick = run.ArtefatoOffer[0];
            run.ChooseArtefato(pick);
            CollectionAssert.Contains(run.OwnedArtefatos, pick);
            Assert.IsTrue(run.AwaitingVillainChoice);
            run.ChooseVillain(mascarado);
            Assert.AreEqual(1, run.ActIndex);

            run.EndActEarly();                        // fim do Ato 2: nova oferta (sem o que já tem), sem vilão
            Assert.IsTrue(run.AwaitingArtefatoChoice);
            CollectionAssert.DoesNotContain(run.ArtefatoOffer, pick);
            run.ChooseArtefato(null);                 // pular
            Assert.AreEqual(2, run.ActIndex);
            Assert.AreEqual(1, run.OwnedArtefatos.Count);

            run.EndActEarly();                        // último ato: sem oferta, o filme acaba
            Assert.IsFalse(run.AwaitingArtefatoChoice);
            Assert.AreEqual(RunStatus.Won, run.Status);
        }

        [Test]
        public void Artefatos_EfeitosGenericos()
        {
            QuietTick();
            var run = NewRun(mascarado, true, atleta, popular);
            var a = A(run, atleta);
            var p = A(run, popular);
            run.GiveArtefato(artefatos[0], "teste"); // combos ×2
            run.GiveArtefato(artefatos[2], "teste"); // pavor ×0,5
            run.GiveArtefato(artefatos[1], "teste"); // +1 cena por ato
            run.Move(a, 1);
            run.Move(p, 1);
            run.RecordScene();
            Assert.IsTrue(run.Report.Lines.Any(l => l.Kind == ReportKind.Combo && l.Score == 100), "40 × 2 × 1,25");
            Assert.AreEqual(5, run.ModifyPavorGain(a, 10));
            run.EndActEarly();
            run.ChooseArtefato(null);
            run.ChooseVillain(mascarado);
            Assert.AreEqual(9, run.ScenesLeft, "Hora extra: 8 + 1");
        }

        // ==================================================================== Determinismo

        [Test]
        public void Determinismo_SeedMaisLogReproduzARun()
        {
            FilmRun Play()
            {
                var run = NewRun(fantasma, true, atleta, nerd, finalGirl, popular);
                run.Move(A(run, finalGirl), 2);
                run.Move(A(run, nerd), 2);
                run.Move(A(run, atleta), 1);
                run.Move(A(run, popular), 1);
                run.RecordScene();
                run.Move(A(run, nerd), 4);
                run.RecordScene();
                run.EndActEarly();
                run.ChooseArtefato(run.ArtefatoOffer[1]);
                run.ChooseVillain(fantasma);
                run.RecordScene();
                if (run.CanMove(A(run, finalGirl), 3)) run.Move(A(run, finalGirl), 3);
                run.RecordScene();
                run.RecordScene();
                return run;
            }

            var a = Play();
            var b = Play();
            Assert.AreEqual(a.TotalScore, b.TotalScore);
            Assert.AreEqual(a.VillainNpc.Space, b.VillainNpc.Space);

            // Reproduz só com a seed + a lista de ações do log.
            var c = NewRun(fantasma, true, atleta, nerd, finalGirl, popular);
            int n = RunReplay.Apply(c, a.Log.actions);
            Assert.AreEqual(a.Log.actions.Count, n);
            Assert.AreEqual(a.TotalScore, c.TotalScore);
            Assert.AreEqual(a.SceneNumber, c.SceneNumber);
            CollectionAssert.AreEqual(a.Log.entries.Select(e => e.text).ToList(), c.Log.entries.Select(e => e.text).ToList());
        }
    }
}
