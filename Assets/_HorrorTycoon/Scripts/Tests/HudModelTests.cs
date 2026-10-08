using System.Collections.Generic;
using HorrorTycoon.Run;
using HorrorTycoon.UI;
using NUnit.Framework;

namespace HorrorTycoon.Tests
{
    /// <summary>
    /// HUD nova — lógica pura do HudModel: selos de custo, palavras de tensão, encurtar legenda,
    /// beats do relatório (ordem, pavor sem legenda, tamanhos, teto de tempo), sequenciador (pular / resumo /
    /// contador) e prioridade dos 3 plots do Roteiro.
    /// </summary>
    public class HudModelTests
    {
        private static ReportLine L(ReportKind k, string t, int s = 0) => new ReportLine { Kind = k, Text = t, Score = s };

        [Test]
        public void SeloDeCusto_GratisUmaCenaVarias()
        {
            Assert.AreEqual("GRÁTIS", HudText.Cost(0));
            Assert.AreEqual("1 CENA", HudText.Cost(1));
            Assert.AreEqual("2 CENAS", HudText.Cost(2));
        }

        [Test]
        public void Tensao_QuatroPalavras()
        {
            Assert.AreEqual("baixa", HudText.TensionWord(0f));
            Assert.AreEqual("subindo", HudText.TensionWord(0.2f));
            Assert.AreEqual("alta", HudText.TensionWord(0.5f));
            Assert.AreEqual("no limite", HudText.TensionWord(0.8f));
        }

        [Test]
        public void Romanos_DoAto()
        {
            Assert.AreEqual("I", HudText.Roman(0));
            Assert.AreEqual("III", HudText.Roman(2));
        }

        [Test]
        public void Legenda_TiraSimbolosSemGlifoECortaExplicacao()
        {
            string s = HudText.Shorten("★ Plot cumprido: <b>O diário do Sótão</b>", 50);
            Assert.AreEqual("Plot cumprido: <b>O diário do Sótão</b>", s);

            string longo = HudText.Shorten("Novo plot: <b>Casal no Quarto</b> — O casal (Atleta + Popular) fica 1 cena no Quarto e mais um monte de texto.", 50);
            Assert.AreEqual("Novo plot: <b>Casal no Quarto</b>", longo);

            string semTraco = HudText.Shorten("Um texto bem comprido sem travessão que passa muito do limite de caracteres da legenda", 40);
            Assert.LessOrEqual(HudText.VisibleLength(semTraco), 40);
            StringAssert.EndsWith("…", semTraco);
        }

        [Test]
        public void Beats_PavorViraGatilhoSemLegenda_OrdemMantida()
        {
            var lines = new List<ReportLine>
            {
                L(ReportKind.Plot, "Plot <b>A</b>: 1/2 cenas."),
                L(ReportKind.Combo, "Casal em Quarto", 40),
                L(ReportKind.Pavor, "Medo: Nerd (sozinho)"),
                L(ReportKind.Pavor, "A Final Girl acalma: Atleta"),
                L(ReportKind.Crisis, "Cena forçada: Nerd entra em PÂNICO!", 20),
                L(ReportKind.Villain, "O Mascarado entrou na casa (Hall)."),
                L(ReportKind.Info, "   ")
            };
            var beats = ReportBeats.Build(lines, new BeatTiming());
            Assert.AreEqual(5, beats.Count, "2 linhas de pavor = 1 gatilho; linha vazia some");
            Assert.AreEqual(ReportKind.Plot, beats[0].Kind);
            Assert.AreEqual(ReportKind.Combo, beats[1].Kind);
            Assert.IsTrue(beats[2].PavorOnly);
            Assert.AreEqual(ReportKind.Crisis, beats[3].Kind);
            Assert.AreEqual(ReportKind.Villain, beats[4].Kind);
            Assert.AreEqual(BeatSize.Points, beats[1].Size);
            Assert.AreEqual(BeatSize.Big, beats[3].Size, "crise é beat grande");
            Assert.AreEqual(BeatSize.Small, beats[4].Size);
            Assert.AreEqual(60, ReportBeats.TotalScore(beats));
        }

        [Test]
        public void Beats_TetoDeTempo_EncolheMasRespeitaMinimo()
        {
            var t = new BeatTiming { maxTotal = 4f, minBeat = 0.5f };
            var lines = new List<ReportLine>();
            for (int i = 0; i < 10; i++) lines.Add(L(ReportKind.PlotDone, "Plot cumprido: algo bem grande", 100));
            var beats = ReportBeats.Build(lines, t);
            foreach (var b in beats) Assert.GreaterOrEqual(b.Duration, 0.5f);
            float total = 0f;
            foreach (var b in beats) total += b.Duration;
            Assert.LessOrEqual(total, 10 * 0.5f + 0.001f);
        }

        [Test]
        public void Sequenciador_AvancaPulaEResumo()
        {
            var t = new BeatTiming();
            var beats = ReportBeats.Build(new List<ReportLine>
            {
                L(ReportKind.Pavor, "Medo: X"),
                L(ReportKind.Combo, "Casal", 40),
                L(ReportKind.PlotDone, "Plot cumprido", 100),
                L(ReportKind.Info, "Fim")
            }, t);
            var seq = new ReportSequencer();
            seq.Start(beats, 1.2f);
            Assert.AreEqual(1, seq.Index, "o gatilho de pavor passa na hora");
            Assert.IsTrue(seq.PavorReleased);
            Assert.AreEqual(40, seq.ScoreShown, "o contador anda até o beat atual");

            seq.Tick(beats[1].Duration + 0.01f);
            Assert.AreEqual(2, seq.Index);
            Assert.AreEqual(140, seq.ScoreShown);

            seq.Skip(10f);                       // termina o beat atual
            Assert.AreEqual(3, seq.Index);
            seq.Skip(10.2f);                     // 2º clique rápido: resumo
            Assert.IsTrue(seq.InSummary);
            Assert.AreEqual(140, seq.ScoreShown);
            Assert.IsFalse(seq.Done);
            seq.Tick(1.3f);
            Assert.IsTrue(seq.Done);
        }

        [Test]
        public void Sequenciador_SemBeats_VaiDiretoAoResumo()
        {
            var seq = new ReportSequencer();
            seq.Start(new List<HudBeat>(), 1f);
            Assert.IsTrue(seq.InSummary);
            seq.Skip(0f);
            Assert.IsTrue(seq.Done);
        }

        [Test]
        public void Roteiro_Top3_PorPrioridade()
        {
            var all = new List<PlotRowVM>
            {
                new PlotRowVM { Name = "novo1" },
                new PlotRowVM { Name = "andando", Progress = 1, Required = 3 },
                new PlotRowVM { Name = "feito", Completed = true },
                new PlotRowVM { Name = "novo2" },
                new PlotRowVM { Name = "pronto", Ready = true }
            };
            var top = HudModel.TopPlots(all);
            Assert.AreEqual(3, top.Count);
            Assert.AreEqual("pronto", top[0].Name);
            Assert.AreEqual("andando", top[1].Name);
            Assert.AreEqual("novo1", top[2].Name);
        }

        [Test]
        public void Escala_PisoTetoEOpcaoDoJogador()
        {
            Assert.AreEqual(0.75f, HudScaleMath.Compute(602f, 1f), 0.001f);
            Assert.AreEqual(1f, HudScaleMath.Compute(1080f, 1f), 0.001f);
            Assert.AreEqual(2f, HudScaleMath.Compute(4320f, 1f), 0.001f);
            Assert.AreEqual(1.5f, HudScaleMath.Compute(1080f, 1.5f), 0.001f);
        }
    }
}
