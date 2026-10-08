using System.Collections.Generic;
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
    /// Testes automáticos das REGRAS do P1 (sem abrir cena).
    /// Rodar: Window > General > Test Runner > EditMode > Run All.
    /// Mini-casa de teste: corredor [0] ligado à entrada e às salas [1] (nada), [2] (faca), [3] (palco).
    /// Protótipo 2: andar é grátis; só entrar numa sala ainda não explorada no ato custa 1 ação.
    /// </summary>
    public class RunLogicTests
    {
        private TagDef slasher;
        private PayoffDef susto, morte;
        private ElementDef arma, isolado;
        private EncounterDef encArma, encNada;
        private ToolDef chave;
        private ActorDef lento, rapido;
        private VillainDef mascarado;
        private GameRulesDef rules;
        private FilmFormatDef format;

        [SetUp]
        public void SetUp()
        {
            slasher = ScriptableObject.CreateInstance<TagDef>();

            susto = ScriptableObject.CreateInstance<PayoffDef>();
            susto.Setup("Susto", "", Color.white, 60, 25, false, 0);
            morte = ScriptableObject.CreateInstance<PayoffDef>();
            morte.Setup("Morte", "", Color.white, 120, 0, true, 1);

            arma = ScriptableObject.CreateInstance<ElementDef>();
            arma.Setup("Arma", "", Color.white, slasher, ElementHolder.Actor, 1, new List<PayoffDef> { morte, susto });
            isolado = ScriptableObject.CreateInstance<ElementDef>();
            isolado.Setup("Isolado", "", Color.white, slasher, ElementHolder.Actor, 1, new List<PayoffDef> { morte, susto },
                ElementAutoRule.AloneInRoom);

            chave = ScriptableObject.CreateInstance<ToolDef>();
            encArma = ScriptableObject.CreateInstance<EncounterDef>();
            encArma.Setup("Faca", "", 30, 0, arma, null);
            encNada = ScriptableObject.CreateInstance<EncounterDef>();
            encNada.Setup("Nada", "", 20, 0, null, null);

            lento = ScriptableObject.CreateInstance<ActorDef>();
            lento.Setup("Lento", "", Color.white, new List<TagDef>(), 1, 0);
            rapido = ScriptableObject.CreateInstance<ActorDef>();
            rapido.Setup("Rápido", "", Color.white, new List<TagDef>(), 2, 0);

            mascarado = ScriptableObject.CreateInstance<VillainDef>();
            mascarado.Setup("Mascarado", "", Color.red, slasher);

            rules = ScriptableObject.CreateInstance<GameRulesDef>();
            // ADAPTADO no Protótipo 3: o tique da casa por cena soma pavor (sozinho, perto do vilão) e audiência
            // "perto do vilão". Aqui ficam DESLIGADOS para estes testes medirem só as regras do P1/P2 (números exatos).
            // As regras novas têm testes próprios (Prototipo3Tests).
            rules.alonePavorPerScene = 0;
            rules.nearVillainPavorPerScene = 0;
            rules.nearVillainScenePoints = 0;
            format = ScriptableObject.CreateInstance<FilmFormatDef>(); // padrão: 8 ações, metas 150/1300/3500
        }

        private RoomDef MakeRoom(EncounterDef enc, params PayoffDef[] stage)
        {
            var r = ScriptableObject.CreateInstance<RoomDef>();
            r.Setup("Sala", "", Color.gray, new List<string>(),
                new List<RoomDef.WeightedEncounter> { new RoomDef.WeightedEncounter { encounter = enc, weight = 1 } },
                new List<PayoffDef>(stage), null);
            return r;
        }

        private FilmRun NewRun(params ActorDef[] actorDefs)
        {
            var hub = ScriptableObject.CreateInstance<RoomDef>();
            hub.Setup("Corredor", "", Color.gray, new List<string>(), new List<RoomDef.WeightedEncounter>(), new List<PayoffDef>(), null);
            var content = ScriptableObject.CreateInstance<GameContentDef>();
            content.Setup(format, rules, new List<ActorDef>(actorDefs),
                new List<RoomDef> { MakeRoom(encNada), MakeRoom(encArma), MakeRoom(encNada, susto, morte) },
                hub, false, new List<VillainDef> { mascarado }, new List<ElementDef> { isolado });
            var run = new FilmRun(content, 1);
            run.Begin();
            return run;
        }

        /// <summary>
        /// ADAPTADO no Protótipo 2 (antes: "Passos_CustoPorPorta_EAtorRapidoAndaODobro"). O custo deixou de ser
        /// por porta: andar é grátis e só explorar sala nova custa 1 ação. DoorsPerStep (Atleta) não tem mais efeito.
        /// </summary>
        [Test]
        public void Acoes_AndarGratis_SoSalaNovaCusta1_DoorsPerStepSemEfeito()
        {
            var run = NewRun(lento, rapido);
            var a = run.Actors[0];
            Assert.AreEqual(1, run.MoveCost(a, 3), "sala ainda não explorada no ato");
            Assert.AreEqual(1, run.MoveCost(run.Actors[1], 3), "ator rápido paga igual");
            Assert.AreEqual(0, run.MoveCost(a, 0), "corredor/convivência é grátis");

            run.Move(a, 2);
            Assert.AreEqual(7, run.ActionsLeft);
            run.Move(a, 0);
            Assert.AreEqual(7, run.ActionsLeft, "voltar ao corredor não gasta");
            Assert.AreEqual(0, run.MoveCost(a, 2), "sala já explorada neste ato é grátis");
            run.Move(a, 2);
            Assert.AreEqual(7, run.ActionsLeft);
            Assert.IsFalse(run.CanMove(a, 2), "já está lá");
        }

        [Test]
        public void Encontro_SoNaPrimeiraVisitaDoAto_EDaElemento()
        {
            var run = NewRun(lento);
            var a = run.Actors[0];
            var first = run.Move(a, 2);
            Assert.IsNotNull(first.Encounter);
            Assert.Contains(arma, a.Elements);

            run.Move(a, 0);
            var again = run.Move(a, 2);
            Assert.IsNull(again.Encounter, "segunda visita no mesmo ato não gera encontro");
        }

        [Test]
        public void Susto_PontuaComElementosQueContam()
        {
            var run = NewRun(lento);
            var a = run.Actors[0];
            run.Move(a, 2);            // 1 ação, ganha Arma (+30 do encontro)
            run.Move(a, 3);            // 1 ação (+20 do encontro)
            int before = run.TotalScore;
            var outcome = run.Direct(a, susto); // Arma(1) + Isolado(1): 60 × 1 × (1+2) = 180
            Assert.AreEqual(180, outcome.Score);
            Assert.AreEqual(before + 180, run.TotalScore);
            Assert.IsFalse(a.Elements.Contains(arma), "elementos usados são consumidos");
        }

        [Test]
        public void Morte_SoAPartirDoAto2()
        {
            var run = NewRun(lento);
            var a = run.Actors[0];
            run.Move(a, 3);
            Assert.IsFalse(run.AvailablePayoffs(a).Contains(morte));
            Assert.IsTrue(run.AvailablePayoffs(a).Contains(susto));
        }

        /// <summary>
        /// ADAPTADO no Protótipo 2: o vilão NPC fica DESLIGADO aqui. Este teste verifica a BUILD do vilão
        /// (peso dobrado); com o NPC ligado, ele pegaria o ator 'b' sozinho na sala 3 no meio do teste.
        /// O NPC tem testes próprios (Prototipo2Tests).
        /// </summary>
        [Test]
        public void FimDoAto1_PedeVilao_EVilaoDobraElementosDaTag()
        {
            rules.villainNpcEnabled = false;
            var run = NewRun(lento, rapido);
            var a = run.Actors[0];
            run.Move(a, 2);           // 1 ação, Arma, +30
            run.Move(a, 3);           // 1 ação, +20
            run.Direct(a, susto);     // 1 ação, consome Arma: 180
            run.EndActEarly();        // 230 >= 150 -> passa e pede vilão
            Assert.IsTrue(run.AwaitingVillainChoice);

            run.ChooseVillain(mascarado);
            Assert.AreEqual(1, run.ActIndex);

            var b = run.Actors[1];
            run.Move(b, 3);           // 1 ação (sala nova no Ato 2); 'a' está na sala 3 também -> não isolado
            var counted = run.CountElements(b, morte);
            Assert.AreEqual(0, counted.Count);

            run.Move(a, 2);           // 'a' sai -> 'b' fica isolado; 'a' ganha Arma de novo (novo ato)
            counted = run.CountElements(b, morte);
            Assert.AreEqual(1, counted.Count);
            Assert.AreEqual(2, counted[0].Weight, "Isolado é Slasher: vale em dobro com o Mascarado");
        }

        [Test]
        public void SalasSorteadas_CorredorFicaNoZero_EMesmaSeedMesmaPlanta()
        {
            var hub = ScriptableObject.CreateInstance<RoomDef>();
            var r1 = MakeRoom(encNada); var r2 = MakeRoom(encArma); var r3 = MakeRoom(encNada, susto);
            var content = ScriptableObject.CreateInstance<GameContentDef>();
            content.Setup(format, rules, new List<ActorDef> { lento }, new List<RoomDef> { r1, r2, r3 },
                hub, true, new List<VillainDef>(), new List<ElementDef>());

            var runA = new FilmRun(content, 77);
            var runB = new FilmRun(content, 77);
            Assert.IsTrue(runA.Rooms[0].IsHub);
            for (int i = 0; i < runA.Rooms.Count; i++) Assert.AreSame(runA.Rooms[i].Def, runB.Rooms[i].Def);
        }

        [Test]
        public void MesmaSeed_MesmasAcoes_MesmoResultado()
        {
            int Play()
            {
                var run = NewRun(lento, rapido);
                run.Move(run.Actors[0], 2);
                run.Move(run.Actors[1], 3);
                run.Direct(run.Actors[1], susto);
                return run.TotalScore;
            }
            Assert.AreEqual(Play(), Play());
        }
    }
}
