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
    /// Protótipo 2: ações (andar grátis), ferramentas por ator e vilão NPC que anuncia a jogada.
    /// Mini-casa em estrela (planta fixa): corredor [0] ligado à entrada e às salas [1], [2], [3].
    ///   [1] caixa de ferramentas (Chave) · [2] faca na gaveta (Faca + Arma) · [3] palco (Susto leve, Morte) + alçapão (Chave).
    /// Na estrela, sala → sala = 2 espaços (passa pelo corredor): o vilão leva 2 batidas.
    /// </summary>
    public class Prototipo2Tests
    {
        private TagDef slasher, sobrenatural;
        private PayoffDef susto, morte;
        private ElementDef arma, isolado, machado;
        private ToolDef chave, faca, crucifixo;
        private EncounterDef encChave, encFaca, encNada;
        private ActorDef ana, beto;
        private VillainDef mascarado, entidade;
        private GameRulesDef rules;
        private FilmFormatDef format;

        [SetUp]
        public void SetUp()
        {
            slasher = ScriptableObject.CreateInstance<TagDef>();
            sobrenatural = ScriptableObject.CreateInstance<TagDef>();

            susto = ScriptableObject.CreateInstance<PayoffDef>();
            susto.Setup("Susto", "", Color.white, 60, 0, false, 0); // pavor 0: só para gastar ações sem mexer no resto
            morte = ScriptableObject.CreateInstance<PayoffDef>();
            morte.Setup("Morte", "", Color.white, 120, 0, true, 1);

            arma = ScriptableObject.CreateInstance<ElementDef>();
            arma.Setup("Arma", "", Color.white, slasher, ElementHolder.Actor, 1, new List<PayoffDef> { morte });
            machado = ScriptableObject.CreateInstance<ElementDef>();
            machado.Setup("Machado", "", Color.white, slasher, ElementHolder.Actor, 2, new List<PayoffDef> { morte });
            isolado = ScriptableObject.CreateInstance<ElementDef>();
            isolado.Setup("Isolado", "", Color.white, slasher, ElementHolder.Actor, 1, new List<PayoffDef> { morte },
                ElementAutoRule.AloneInRoom);

            chave = ScriptableObject.CreateInstance<ToolDef>();
            chave.Setup("Chave", "");
            faca = ScriptableObject.CreateInstance<ToolDef>();
            faca.Setup("Faca", "", slasher);
            crucifixo = ScriptableObject.CreateInstance<ToolDef>();
            crucifixo.Setup("Crucifixo", "", sobrenatural);

            encChave = ScriptableObject.CreateInstance<EncounterDef>();
            encChave.Setup("Caixa", "", 20, 0, null, chave);
            encFaca = ScriptableObject.CreateInstance<EncounterDef>();
            encFaca.Setup("Faca na gaveta", "", 30, 0, arma, faca);
            encNada = ScriptableObject.CreateInstance<EncounterDef>();
            encNada.Setup("Nada", "", 20, 0, null, null);

            ana = ScriptableObject.CreateInstance<ActorDef>();
            ana.Setup("Ana", "", Color.white, new List<TagDef>(), 1, 0);
            beto = ScriptableObject.CreateInstance<ActorDef>();
            beto.Setup("Beto", "", Color.white, new List<TagDef>(), 2, 0);

            mascarado = ScriptableObject.CreateInstance<VillainDef>();
            mascarado.Setup("Mascarado", "", Color.red, slasher);
            entidade = ScriptableObject.CreateInstance<VillainDef>();
            entidade.Setup("Entidade", "", Color.blue, sobrenatural);

            rules = ScriptableObject.CreateInstance<GameRulesDef>();
            // ADAPTADO no Protótipo 3: o tique da casa por cena soma pavor (sozinho, perto do vilão) e audiência
            // "perto do vilão". Aqui ficam DESLIGADOS para estes testes medirem só as regras do P1/P2 (números exatos).
            // As regras novas têm testes próprios (Prototipo3Tests).
            rules.alonePavorPerScene = 0;
            rules.nearVillainPavorPerScene = 0;
            rules.nearVillainScenePoints = 0;
            format = ScriptableObject.CreateInstance<FilmFormatDef>();
            format.Acts[0].goal = 0; // o Ato 1 sempre passa: os testes chegam rápido ao vilão
        }

        private RoomDef MakeRoom(EncounterDef enc, RoomDef.LockedSpot locked, params PayoffDef[] stage)
        {
            var r = ScriptableObject.CreateInstance<RoomDef>();
            r.Setup("Sala", "", Color.gray, new List<string>(),
                new List<RoomDef.WeightedEncounter> { new RoomDef.WeightedEncounter { encounter = enc, weight = 1 } },
                new List<PayoffDef>(stage), locked);
            return r;
        }

        private FilmRun NewRun(VillainDef villain, params ActorDef[] actorDefs)
        {
            var hub = ScriptableObject.CreateInstance<RoomDef>();
            hub.Setup("Corredor", "", Color.gray, new List<string>(), new List<RoomDef.WeightedEncounter>(), new List<PayoffDef>(), null);
            var locked = new RoomDef.LockedSpot { name = "Alçapão", requiredTool = chave, reward = machado, text = "" };
            var content = ScriptableObject.CreateInstance<GameContentDef>();
            content.Setup(format, rules, new List<ActorDef>(actorDefs),
                new List<RoomDef> { MakeRoom(encChave, null), MakeRoom(encFaca, null), MakeRoom(encNada, locked, susto, morte) },
                hub, false, new List<VillainDef> { villain }, new List<ElementDef> { isolado });
            var run = new FilmRun(content, 1);
            run.Begin();
            return run;
        }

        /// <summary>Encerra o Ato 1 (meta 0) e escolhe o vilão: começa o Ato 2 com o vilão na casa.</summary>
        private static void ToAct2(FilmRun run, VillainDef villain)
        {
            run.EndActEarly();
            Assert.IsTrue(run.AwaitingVillainChoice);
            run.ChooseVillain(villain);
            Assert.AreEqual(1, run.ActIndex);
        }

        // ==================================================================== Ações

        [Test]
        public void Acoes_MoverGratisOuPago_EAtoAcabaQuandoZera()
        {
            var run = NewRun(mascarado, ana);
            var a = run.Actors[0];
            var paid = run.Move(a, 1);
            Assert.AreEqual(1, paid.Cost);
            var free = run.Move(a, 0);
            Assert.AreEqual(0, free.Cost, "corredor é grátis");
            Assert.AreEqual(0, run.Move(a, 1).Cost, "sala já explorada no ato é grátis");
            Assert.AreEqual(7, run.ActionsLeft);

            run.Move(a, 2);             // 6
            run.Move(a, 3);             // 5
            for (int i = 0; i < 5; i++) run.Direct(a, susto); // 0 → fim do ato
            Assert.IsTrue(run.AwaitingVillainChoice, "ações zeraram depois da ação: o ato acabou");
        }

        // ==================================================================== Ferramentas

        [Test]
        public void Ferramentas_FicamComQuemAchou_LimiteChaoEProximoPega()
        {
            rules.maxToolsPerActor = 1;
            var run = NewRun(mascarado, ana, beto);
            var a = run.Actors[0];
            var b = run.Actors[1];

            var o1 = run.Move(a, 1);
            Assert.AreSame(chave, o1.ToolGained);
            CollectionAssert.AreEqual(new[] { chave }, a.Tools);
            Assert.AreEqual(0, b.Tools.Count, "a ferramenta é do ator, não da run");

            var o2 = run.Move(a, 2); // mãos cheias: a Faca fica no chão (o elemento Arma vem normal)
            Assert.IsNull(o2.ToolGained);
            Assert.AreSame(faca, o2.ToolLeftOnFloor);
            Assert.Contains(arma, a.Elements);
            CollectionAssert.AreEqual(new[] { faca }, run.Rooms[2].FloorTools);

            int before = run.ActionsLeft;
            var o3 = run.Move(b, 2); // sala já explorada: grátis, e pega do chão
            Assert.AreEqual(before, run.ActionsLeft);
            CollectionAssert.AreEqual(new[] { faca }, o3.ToolsPickedUp);
            CollectionAssert.AreEqual(new[] { faca }, b.Tools);
            Assert.AreEqual(0, run.Rooms[2].FloorTools.Count);
        }

        [Test]
        public void Ferramentas_AtoresJuntosCompartilham_AlcapaoComChaveDeOutro()
        {
            var run = NewRun(mascarado, ana, beto);
            var a = run.Actors[0];
            var b = run.Actors[1];
            run.Move(a, 1);         // Ana acha a Chave
            run.Move(b, 3);         // Beto no porão (alçapão), sem chave
            Assert.IsFalse(run.CanUseTool(b), "sozinho e sem chave");

            run.Move(a, 3);         // Ana chega junto (sala explorada: grátis)
            Assert.IsTrue(run.CanUseTool(b), "Ana está junto com a Chave");
            Assert.AreSame(a, run.ToolHolderFor(b, chave));
            var outcome = run.UseTool(b);
            Assert.AreSame(a, outcome.Holder);
            Assert.Contains(machado, b.Elements, "quem usou recebe o prêmio");
            CollectionAssert.AreEqual(new[] { chave }, a.Tools, "chave não é gasta no alçapão");
        }

        // ==================================================================== Vilão NPC

        [Test]
        public void Vilao_NaoAgeNoAto1_EntraNoAto2NumEspacoSemAtores()
        {
            var run = NewRun(mascarado, ana, beto);
            run.Move(run.Actors[0], 2);
            run.Move(run.Actors[1], 3);
            run.Direct(run.Actors[1], susto);
            Assert.IsNull(run.VillainNpc, "Ato 1: o vilão nem existe na casa");

            ToAct2(run, mascarado);
            var v = run.VillainNpc;
            Assert.IsNotNull(v);
            Assert.IsTrue(v.InHouse);
            Assert.IsFalse(run.Actors.Any(x => x.Alive && x.RoomIndex == v.Space), "entra num espaço sem atores");
            Assert.IsTrue(run.Rooms[v.Space].IsExplorable, "prefere uma sala");
        }

        [Test]
        public void Vilao_PrimeiroAtoConfiguravel()
        {
            rules.villainFirstActIndex = 2;
            var run = NewRun(mascarado, ana);
            ToAct2(run, mascarado);
            Assert.IsNull(run.VillainNpc, "configurado para entrar só no Ato 3");
        }

        [Test]
        public void Vilao_AnuncioIgualAoMovimento_MesmoSeOAlvoSair()
        {
            var run = NewRun(mascarado, ana);
            var a = run.Actors[0];
            run.Move(a, 3);
            ToAct2(run, mascarado);
            var v = run.VillainNpc;

            var beats = new List<VillainMoveEvent>();
            run.VillainMoved += e => { if (e.Reason == VillainMoveReason.Beat) beats.Add(e); };

            Assert.AreSame(a, v.Target);
            Assert.IsTrue(v.IsTelegraphing);
            int announced = v.NextSpace;
            Assert.AreEqual(0, announced, "estrela: o primeiro passo é o corredor");
            run.Direct(a, susto);                    // 1 ação = 1 batida
            Assert.AreEqual(1, beats.Count);
            Assert.AreEqual(announced, beats[0].To);
            Assert.AreEqual(announced, v.Space);

            announced = v.NextSpace;
            Assert.AreEqual(3, announced, "agora vai para onde a Ana está");
            run.Move(a, 2);                          // Ana foge para uma sala nova (paga 1 → batida)
            Assert.AreEqual(2, beats.Count);
            Assert.AreEqual(announced, beats[1].To, "o anúncio é compromisso: vai para lá mesmo com a Ana fora");
            Assert.IsTrue(a.Alive);
        }

        [Test]
        public void Vilao_AndarGratisNaoGeraBatida()
        {
            var run = NewRun(mascarado, ana);
            var a = run.Actors[0];
            run.Move(a, 3);
            ToAct2(run, mascarado);
            int space = run.VillainNpc.Space;
            run.Move(a, 0); // corredor: grátis
            Assert.AreEqual(space, run.VillainNpc.Space);
        }

        [Test]
        public void Vilao_RepelidoComFerramentaQueCombate_AoEntrarNoEspacoDele()
        {
            var run = NewRun(mascarado, ana);
            var a = run.Actors[0];
            run.Move(a, 2); // Faca (ferramenta) + Arma
            CollectionAssert.Contains(a.Tools, faca);
            ToAct2(run, mascarado);
            var v = run.VillainNpc;

            VillainEncounterOutcome enc = null;
            run.VillainEncountered += e => enc = e;
            int villainRoom = v.Space;
            int scoreBefore = run.TotalScore;
            run.Move(a, villainRoom); // Ana entra no espaço do vilão (sala nova no ato: 1 ação, +20 do encontro)

            Assert.IsNotNull(enc);
            Assert.AreEqual(VillainEncounterKind.Repelled, enc.Kind);
            Assert.IsTrue(enc.ActorWalkedIn);
            Assert.AreSame(faca, enc.ToolUsed);
            CollectionAssert.DoesNotContain(a.Tools, faca, "a ferramenta é gasta");
            Assert.AreEqual(Mathf.RoundToInt(rules.repelScore * format.Acts[1].payoffMultiplier), enc.Score);
            Assert.AreEqual(scoreBefore + 20 + enc.Score, run.TotalScore);
            Assert.AreEqual(rules.repelPavor, a.Pavor);
            Assert.IsTrue(a.Alive);
            Assert.AreNotEqual(villainRoom, v.Space, "sumiu e reapareceu longe");
            Assert.AreEqual(rules.repelStunBeats, enc.StunBeats);
            Assert.AreEqual(rules.repelStunBeats - 1, v.StunBeats, "a batida desta mesma ação já conta como parado");
            Assert.IsFalse(v.IsTelegraphing, "parado: sem anúncio de movimento");
        }

        [Test]
        public void Vilao_FerramentaErradaNaoRepele()
        {
            var run = NewRun(entidade, ana);
            var a = run.Actors[0];
            run.Move(a, 2); // Faca: combate Slasher, não Sobrenatural
            ToAct2(run, entidade);
            VillainEncounterOutcome enc = null;
            run.VillainEncountered += e => enc = e;
            run.Move(a, run.VillainNpc.Space);
            Assert.AreEqual(VillainEncounterKind.Caught, enc.Kind);
            Assert.IsFalse(a.Alive);
        }

        [Test]
        public void Vilao_GrupoLevaSusto_VilaoRecuaEFicaParado()
        {
            var run = NewRun(mascarado, ana, beto);
            var a = run.Actors[0];
            var b = run.Actors[1];
            run.Move(a, 3);
            run.Move(b, 3); // juntos no palco
            ToAct2(run, mascarado);
            var v = run.VillainNpc;

            VillainEncounterOutcome enc = null;
            run.VillainEncountered += e => enc = e;
            run.Direct(a, susto); // batida 1: vilão → corredor
            Assert.AreEqual(0, v.Space);
            Assert.AreEqual(3, v.NextSpace);
            run.Direct(a, susto); // batida 2: vilão entra no palco com os dois

            Assert.IsNotNull(enc);
            Assert.AreEqual(VillainEncounterKind.GroupScare, enc.Kind);
            Assert.AreEqual(rules.groupScarePavor, a.Pavor);
            Assert.AreEqual(rules.groupScarePavor, b.Pavor);
            Assert.IsTrue(a.Alive && b.Alive);
            Assert.AreEqual(0, v.Space, "recuou 1 espaço (de volta ao corredor)");
            Assert.AreEqual(rules.groupScareStunBeats, v.StunBeats);
        }

        [Test]
        public void Vilao_PegaAtorSozinho_MorteDisparaComOsElementosDele()
        {
            // Entidade: a Faca que a Ana acha na sala 2 não repele o sobrenatural (com o Mascarado ela se salvaria).
            var run = NewRun(entidade, ana);
            var a = run.Actors[0];
            run.Move(a, 2); // Arma
            run.Move(a, 3); // palco, sozinha
            ToAct2(run, entidade);
            var v = run.VillainNpc;

            VillainEncounterOutcome enc = null;
            run.VillainEncountered += e => enc = e;
            run.Direct(a, susto); // vilão → corredor
            int before = run.TotalScore;
            run.Direct(a, susto); // vilão → palco: pega a Ana

            Assert.IsNotNull(enc);
            Assert.AreEqual(VillainEncounterKind.Caught, enc.Kind);
            Assert.AreSame(a, enc.Victim);
            Assert.IsFalse(a.Alive);
            Assert.IsNotNull(enc.Payoff, "a Morte dispara (Ato 2 >= MinActIndex)");
            Assert.AreSame(morte, enc.Payoff.Payoff);
            // Arma (1) + Isolado (1) (Slasher: sem bônus com a Entidade): 120 × 1,5 × (1 + 2) = 540.
            Assert.AreEqual(540, enc.Score);
            Assert.AreEqual(before + 90 + 540, run.TotalScore, "susto desta ação (60 × 1,5) + a Morte disparada pelo vilão");
            CollectionAssert.DoesNotContain(a.Elements, arma, "elementos usados na cena");
            Assert.AreEqual(RunStatus.Lost, run.Status, "era a única atriz");
        }

        [Test]
        public void Vilao_AtorEntraSozinhoNoEspacoDoVilao_EPego_FerramentasFicamNoChao()
        {
            // Entidade: a Faca que a Ana acha ao entrar na sala dela não serve contra o sobrenatural.
            var run = NewRun(entidade, ana, beto);
            var a = run.Actors[0];
            var b = run.Actors[1];
            run.Move(a, 1);   // Chave
            run.Move(b, 3);
            ToAct2(run, entidade);
            var v = run.VillainNpc;
            int villainRoom = v.Space;
            Assert.AreNotEqual(a.RoomIndex, villainRoom);

            VillainEncounterOutcome enc = null;
            run.VillainEncountered += e => { if (enc == null) enc = e; };
            run.Move(a, villainRoom);
            Assert.IsNotNull(enc);
            Assert.IsTrue(enc.ActorWalkedIn);
            Assert.AreEqual(VillainEncounterKind.Caught, enc.Kind);
            Assert.IsFalse(a.Alive);
            CollectionAssert.Contains(run.Rooms[villainRoom].FloorTools, chave, "a chave da Ana ficou no chão");
        }

        [Test]
        public void Vilao_AlvoPrefereSozinho_DepoisMaisPavor()
        {
            var carla = ScriptableObject.CreateInstance<ActorDef>();
            carla.Setup("Carla", "", Color.white, new List<TagDef>(), 1, 0);
            susto.Setup("Susto", "", Color.white, 60, 20, false, 0); // aqui o susto dá pavor
            var run = NewRun(mascarado, ana, beto, carla);
            var a = run.Actors[0];
            var b = run.Actors[1];
            var c = run.Actors[2];

            run.Move(a, 3);
            run.Move(b, 3);
            run.Move(c, 1);
            Assert.AreSame(c, VillainAgent.ChooseTarget(run.Map, run.Actors, 2), "sozinha vence o grupo, mesmo sendo a última do elenco");

            run.Move(b, 2);       // agora os três estão sozinhos, pavor 0: vence o mais perto, depois a ordem do elenco
            Assert.AreSame(b, VillainAgent.ChooseTarget(run.Map, run.Actors, 2), "Beto está no espaço do vilão (distância 0)");
            Assert.AreSame(a, VillainAgent.ChooseTarget(run.Map, run.Actors, 0), "todos a 1 espaço: ordem do elenco");

            run.Direct(a, susto); // Ana: +20 de pavor
            Assert.AreEqual(20, a.Pavor);
            run.Move(b, 0);       // Beto sai da frente
            Assert.AreSame(a, VillainAgent.ChooseTarget(run.Map, run.Actors, 1), "entre os sozinhos, mais pavor");
        }
    }
}
