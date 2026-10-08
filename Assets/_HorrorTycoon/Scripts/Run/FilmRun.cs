using System;
using System.Collections.Generic;
using System.Linq;
using HorrorTycoon.Core;
using HorrorTycoon.Replay;
using HorrorTycoon.Rooms;
using HorrorTycoon.Rooms.Generation;
using HorrorTycoon.Scoring;
using UnityEngine;

namespace HorrorTycoon.Run
{
    /// <summary>
    /// A RUN inteira (um filme), em C# puro: não sabe nada de 3D, câmera ou UI.
    ///
    /// Protótipo 3 ("Build do Filme"): o recurso do ato são CENAS (o nome interno continua ActionsLeft).
    /// Toda ação que gasta cena roda o TIQUE DA CASA (FilmRun.Build.cs): (a) a cena escolhida acontece;
    /// (b) cada ator conta 1 cena onde está (plots, combos, pavor, crise); (c) o vilão age pela família dele.
    ///
    /// Protótipo 2 — fluxo de um ato:
    ///   - o ato começa com N AÇÕES e cada sala sorteia 1 encontro escondido;
    ///   - Move(): leva um ator a um espaço. GRÁTIS por convivências, corredores e salas já exploradas no ato;
    ///     custa 1 ação (exploreActionCost) só ao entrar numa sala ainda não explorada no ato (resolve o encontro);
    ///   - ferramentas ficam com o ator que achou (limite por ator); mãos cheias = fica no chão da sala;
    ///   - UseTool(): abre um ponto trancado se ALGUM ator no mesmo espaço tem a ferramenta certa;
    ///   - Direct(): numa sala-palco, dirige uma cena (Susto/Morte) e transforma os elementos em pontos;
    ///   - cada ação gasta = 1 BATIDA: o vilão NPC (a partir do Ato 2) anda 1 espaço para onde tinha anunciado;
    ///   - ações acabaram (ou EndActEarly) -> confere a meta. Após o Ato 1: escolher o vilão.
    ///
    /// Duas plantas:
    ///   - FIXA (sem HouseGenDef): corredor no índice 0 + salas do catálogo sorteadas nos slots; atores começam lá fora.
    ///   - GERADA (com HouseGenDef): HouseGenerator monta a casa; índice de sala = índice do espaço no Layout;
    ///     atores começam na convivência inicial. Só espaços do tipo Sala têm encontro; Corredor é só passagem.
    /// </summary>
    public partial class FilmRun
    {
        public GameContentDef Content { get; }
        public GameRandom Rng { get; }
        public HouseMap Map { get; }
        /// <summary>Planta gerada (null na planta fixa). Contrato com a montagem 3D.</summary>
        public HouseLayout Layout { get; }
        public RunStatus Status { get; private set; } = RunStatus.Playing;
        public int ActIndex { get; private set; }
        /// <summary>Ações que sobram neste ato (antigos "passos").</summary>
        public int ActionsLeft { get; private set; }
        public int TotalScore { get; private set; }
        public VillainDef Villain { get; private set; }
        public bool AwaitingVillainChoice { get; private set; }
        /// <summary>O vilão andando pela casa (null antes do Ato 2 / com o experimento desligado).</summary>
        public VillainAgent VillainNpc { get; private set; }

        private readonly List<ActorRunState> actors = new List<ActorRunState>();
        private readonly List<RoomRunState> rooms = new List<RoomRunState>();
        /// <summary>Fluxo aleatório só do vilão (não mexe na sequência dos encontros).</summary>
        private readonly GameRandom villainRng;
        /// <summary>Fluxo aleatório só dos artefatos (oferta entre atos, plot com artefato sorteado).</summary>
        private readonly GameRandom artefatoRng;

        public IReadOnlyList<ActorRunState> Actors => actors;
        public IReadOnlyList<RoomRunState> Rooms => rooms;
        public RunLog Log { get; } = new RunLog();

        public FilmFormatDef.ActSettings CurrentAct => Content.FilmFormat.Acts[ActIndex];
        public int ActCount => Content.FilmFormat.Acts.Length;
        private GameRulesDef Rules => Content.Rules;

        // Eventos (o visual e o log escutam).
        public event Action<int> ActStarted;
        public event Action<ActOutcome> ActEnded;
        public event Action<RunStatus> RunEnded;
        /// <summary>O vilão mudou de espaço (entrada, batida, recuo, teleporte).</summary>
        public event Action<VillainMoveEvent> VillainMoved;
        /// <summary>O vilão encontrou atores (repelido, susto em grupo, pegou alguém).</summary>
        public event Action<VillainEncounterOutcome> VillainEncountered;

        /// <summary>"Sal" do fluxo aleatório da geração da casa (separado dos sorteios da run).</summary>
        private const int LayoutSalt = 0x4A0753;
        private const int VillainSalt = 0x71A1;
        private const int ArtefatoSalt = 0xA27E;

        /// <summary>Planta fixa (P0/P1).</summary>
        public FilmRun(GameContentDef content, int seed) : this(content, seed, null)
        {
        }

        /// <summary>houseGen != null: casa gerada pela seed. null: planta fixa (comportamento antigo).</summary>
        public FilmRun(GameContentDef content, int seed, HouseGenDef houseGen)
        {
            Content = content ?? throw new ArgumentNullException(nameof(content));
            Rng = new GameRandom(seed);
            villainRng = new GameRandom(GameRandom.Mix(seed, VillainSalt));
            artefatoRng = new GameRandom(GameRandom.Mix(seed, ArtefatoSalt));
            foreach (var def in content.Actors) actors.Add(new ActorRunState(def));

            if (houseGen == null)
            {
                // Planta: índice 0 = corredor (se houver); depois os cômodos, em ordem SORTEADA pela seed.
                // O índice é o "slot" físico da planta; o sorteio decide qual cômodo fica em cada slot.
                // Só RoomDefs do tipo Sala viram cômodo (convivências do catálogo ficam de fora).
                var defs = content.Rooms.Where(r => r != null && r.Kind == SpaceKind.Room).ToList();
                if (content.ShuffleRooms) Rng.Shuffle(defs);

                int hubIndex = -1;
                if (content.HubRoom != null)
                {
                    hubIndex = 0;
                    rooms.Add(new RoomRunState(content.HubRoom, 0) { IsHub = true, Discovered = true, Kind = SpaceKind.Social });
                }
                foreach (var def in defs) rooms.Add(new RoomRunState(def, rooms.Count));
                Map = new HouseMap(rooms.Count, hubIndex);
            }
            else
            {
                // Pool de salas = salas do catálogo + extras do HouseGenDef (só tipo Sala, sem repetir).
                var pool = content.Rooms.Concat(houseGen.extraRoomPool)
                    .Where(r => r != null && r.Kind == SpaceKind.Room).Distinct().ToList();
                // Fluxo próprio: gerar a casa não muda a sequência dos encontros da run.
                Layout = HouseGenerator.Generate(houseGen, pool, new GameRandom(GameRandom.Mix(seed, LayoutSalt)));

                foreach (var space in Layout.Spaces)
                {
                    rooms.Add(new RoomRunState(space.Def, space.Index)
                    {
                        Kind = space.Kind,
                        Space = space,
                        IsHub = space.Kind != SpaceKind.Room,
                        // Convivências e corredores não escondem nada: já começam visíveis.
                        Discovered = space.Kind != SpaceKind.Room,
                    });
                }
                Map = new HouseMap(Layout);

                if (houseGen.startInside)
                {
                    foreach (var actor in actors) actor.RoomIndex = Layout.StartSpaceIndex;
                }
            }

            Log.seed = seed;
            Log.filmFormat = content.FilmFormat.DisplayName;
            Log.Add(0, "start", $"Filme '{content.FilmFormat.DisplayName}' — seed {seed}" +
                (Layout != null ? $" | casa gerada: {Layout.CountOf(SpaceKind.Room)} salas, {Layout.Attempts} tentativa(s)" : ""));
        }

        /// <summary>Chamar uma vez depois de assinar os eventos.</summary>
        public void Begin()
        {
            BeginReport();
            OfferActorPlots();
            StartAct();
        }

        // ================================================================== Consultas

        /// <summary>Entrar neste espaço agora dispara algo (sala ainda não explorada neste ato)?</summary>
        public bool TriggersExploration(int roomIndex) =>
            roomIndex >= 0 && roomIndex < rooms.Count && rooms[roomIndex].IsExplorable && !rooms[roomIndex].VisitedThisAct;

        /// <summary>
        /// Ações para ir até o lugar: exploreActionCost se for uma sala ainda não explorada neste ato, senão 0 (grátis).
        /// 0 também quando já está lá (ver CanMove). -1 = sem caminho / índice inválido.
        /// A habilidade de andar (ActorDef.DoorsPerStep) fica guardada, sem efeito no Protótipo 2.
        /// </summary>
        public int MoveCost(ActorRunState actor, int roomIndex)
        {
            if (roomIndex < 0 || roomIndex >= rooms.Count) return -1;
            if (actor.RoomIndex == roomIndex) return 0;
            if (Map.Doors(actor.RoomIndex, roomIndex) < 0) return -1;
            return TriggersExploration(roomIndex) ? Rules.exploreActionCost : 0;
        }

        public bool CanAct => Status == RunStatus.Playing && !AwaitingVillainChoice && !AwaitingArtefatoChoice;

        public bool CanMove(ActorRunState actor, int roomIndex)
        {
            if (!CanAct || !actor.Alive || actor.IsLocked) return false;
            if (roomIndex < 0 || roomIndex >= rooms.Count || !rooms[roomIndex].IsDestination || rooms[roomIndex].Sealed) return false;
            if (actor.RoomIndex == roomIndex) return false;
            int cost = MoveCost(actor, roomIndex);
            return cost >= 0 && cost <= ActionsLeft;
        }

        public RoomRunState RoomOf(ActorRunState actor) =>
            actor.RoomIndex >= 0 ? rooms[actor.RoomIndex] : null;

        /// <summary>Atores vivos num espaço (ordem do elenco).</summary>
        public List<ActorRunState> ActorsIn(int roomIndex) =>
            actors.Where(a => a.Alive && a.RoomIndex == roomIndex && roomIndex >= 0).ToList();

        /// <summary>Cenas que este ator pode dirigir agora (está num palco, ato certo, ações suficientes).</summary>
        public List<PayoffDef> AvailablePayoffs(ActorRunState actor)
        {
            var list = new List<PayoffDef>();
            var room = RoomOf(actor);
            if (!CanAct || !actor.Alive || room == null || ActionsLeft < Rules.directStepCost) return list;
            foreach (var p in room.Def.StagePayoffs)
            {
                if (p != null && ActIndex >= p.MinActIndex) list.Add(p);
            }
            return list;
        }

        // ------------------------------------------------------------------ Ferramentas

        /// <summary>Quem pode emprestar a ferramenta para este ator: ele mesmo ou outro ator vivo no MESMO espaço. null = ninguém.</summary>
        public ActorRunState ToolHolderFor(ActorRunState actor, ToolDef tool)
        {
            if (tool == null || actor == null || !actor.Alive) return null;
            if (actor.Tools.Contains(tool)) return actor;
            if (actor.RoomIndex < 0) return null;
            return actors.FirstOrDefault(a => a.Alive && a != actor && a.RoomIndex == actor.RoomIndex && a.Tools.Contains(tool));
        }

        /// <summary>Ferramenta existe na run agora (com algum ator vivo ou no chão de alguma sala)?</summary>
        public bool ToolInPlay(ToolDef tool) =>
            tool != null && (actors.Any(a => a.Alive && a.Tools.Contains(tool)) || rooms.Any(r => r.FloorTools.Contains(tool)));

        /// <summary>Todas as ferramentas em jogo (com atores vivos e no chão).</summary>
        public List<ToolDef> ToolsInPlay()
        {
            var list = new List<ToolDef>();
            foreach (var a in actors) if (a.Alive) list.AddRange(a.Tools);
            foreach (var r in rooms) list.AddRange(r.FloorTools);
            return list;
        }

        public bool CanUseTool(ActorRunState actor)
        {
            var room = RoomOf(actor);
            return CanAct && actor.Alive && room != null && room.Def.HasLockedSpot && !room.LockUsed
                   && ToolHolderFor(actor, room.Def.Locked.requiredTool) != null && ActionsLeft >= Rules.toolStepCost;
        }

        /// <summary>
        /// Quais elementos contariam se o ator dirigisse esta cena agora (inclui automáticos).
        /// A UI mostra ESTA lista (ícones/nomes), não o número final: informação parcial.
        /// </summary>
        public List<CountedElement> CountElements(ActorRunState actor, PayoffDef payoff)
        {
            var result = new List<CountedElement>();
            var room = RoomOf(actor);

            void Add(ElementDef e, bool fromRoom, bool auto)
            {
                if (e == null || !e.CountsFor(payoff)) return;
                bool boosted = Villain != null && e.Subgenre != null && e.Subgenre == Villain.Subgenre;
                result.Add(new CountedElement
                {
                    Element = e,
                    Weight = e.Strength * (boosted ? Rules.villainTagWeight : 1),
                    FromRoom = fromRoom,
                    IsAuto = auto,
                    BoostedByVillain = boosted,
                });
            }

            foreach (var e in actor.Elements) Add(e, false, false);
            if (room != null) foreach (var e in room.Elements) Add(e, true, false);

            foreach (var auto in Content.AutoElements)
            {
                if (auto == null) continue;
                bool active = false;
                switch (auto.AutoRule)
                {
                    case ElementAutoRule.AloneInRoom:
                        active = room != null && actors.Count(a => a.Alive && a.RoomIndex == room.Index) == 1;
                        break;
                    case ElementAutoRule.PavorAtLeast:
                        active = actor.Pavor >= auto.AutoValue;
                        break;
                }
                if (active) Add(auto, false, true);
            }

            return result;
        }

        // ================================================================== Ações

        public MoveOutcome Move(ActorRunState actor, int roomIndex)
        {
            if (!CanMove(actor, roomIndex)) throw new InvalidOperationException("Movimento inválido.");

            int cost = MoveCost(actor, roomIndex);
            var outcome = new MoveOutcome { Actor = actor, From = actor.RoomIndex, To = roomIndex, Cost = cost };
            BeginReport();

            ActionsLeft -= cost;
            actor.RoomIndex = roomIndex;
            Log.actions.Add($"move {actor.Def.DisplayName} {roomIndex}");

            var room = rooms[roomIndex];
            room.Discovered = true;
            PickUpFloorTools(actor, room, outcome);

            if (!room.VisitedThisAct)
            {
                room.VisitedThisAct = true;
                var enc = room.Encounter;
                outcome.Encounter = enc;
                if (enc != null)
                {
                    int pts = Mathf.RoundToInt(enc.Points * ArtMult(ArtefatoEffectType.ExploreScoreMult) * PopularMult(actor));
                    outcome.Points = pts;
                    TotalScore += pts;
                    outcome.ActorBroke = ChangePavor(actor, enc.PavorDelta);

                    if (enc.Element != null && actor.Alive)
                    {
                        if (enc.Element.Holder == ElementHolder.Actor) actor.Elements.Add(enc.Element);
                        else room.Elements.Add(enc.Element);
                        outcome.ElementGained = enc.Element;
                    }

                    if (enc.Tool != null && !ToolInPlay(enc.Tool))
                    {
                        if (actor.Alive && actor.Tools.Count < Rules.maxToolsPerActor)
                        {
                            actor.Tools.Add(enc.Tool);
                            outcome.ToolGained = enc.Tool;
                        }
                        else
                        {
                            room.FloorTools.Add(enc.Tool);
                            outcome.ToolLeftOnFloor = enc.Tool;
                        }
                    }
                }
            }

            // Protótipo 3: a 1ª exploração da sala na run oferece os plots dela.
            if (room.IsExplorable && !room.PlotsOffered) OfferRoomPlots(room);

            Log.Add(ActIndex + 1, "move",
                $"{actor.Def.DisplayName} → {room.Def.DisplayName} ({(cost == 0 ? "grátis" : cost + " ação(ões)")})" +
                (outcome.Encounter != null ? $" | {outcome.Encounter.DisplayName}" : "") +
                (outcome.ElementGained != null ? $" | +{outcome.ElementGained.DisplayName}" : "") +
                (outcome.ToolGained != null ? $" | achou {outcome.ToolGained.DisplayName}" : "") +
                (outcome.ToolLeftOnFloor != null ? $" | {outcome.ToolLeftOnFloor.DisplayName} ficou no chão (mãos cheias)" : "") +
                (outcome.ToolsPickedUp.Count > 0 ? $" | pegou do chão: {string.Join(", ", outcome.ToolsPickedUp.Select(t => t.DisplayName))}" : "") +
                (outcome.ActorBroke ? " | NÃO AGUENTOU" : ""));

            // Entrou no espaço do vilão: encontro na hora (só o Slasher; o fantasma não pega ninguém).
            if (actor.Alive && VillainNpc != null && !VillainNpc.IsGhost && VillainNpc.Space == roomIndex)
            {
                ResolveVillainEncounter(roomIndex, true);
            }

            RecheckPlots();
            Scenes(cost);
            AfterAction();
            return outcome;
        }

        public ToolOutcome UseTool(ActorRunState actor)
        {
            if (!CanUseTool(actor)) throw new InvalidOperationException("Não dá para usar ferramenta aqui.");

            var room = RoomOf(actor);
            var tool = room.Def.Locked.requiredTool;
            var holder = ToolHolderFor(actor, tool);
            BeginReport();
            ActionsLeft -= Rules.toolStepCost;
            room.LockUsed = true;

            var reward = room.Def.Locked.reward;
            if (reward != null)
            {
                if (reward.Holder == ElementHolder.Actor) actor.Elements.Add(reward);
                else room.Elements.Add(reward);
            }

            Log.actions.Add($"tool {actor.Def.DisplayName}");
            Log.Add(ActIndex + 1, "tool", $"{actor.Def.DisplayName} abriu {room.Def.Locked.name}" +
                (holder != actor ? $" com {tool.DisplayName} de {holder.Def.DisplayName}" : "") + $" → {reward?.DisplayName}");

            var outcome = new ToolOutcome { Actor = actor, Holder = holder, Room = room, Tool = tool, Reward = reward };
            Scenes(Rules.toolStepCost);
            AfterAction();
            return outcome;
        }

        public PayoffOutcome Direct(ActorRunState actor, PayoffDef payoff)
        {
            if (!AvailablePayoffs(actor).Contains(payoff)) throw new InvalidOperationException("Cena indisponível.");

            var room = RoomOf(actor);
            BeginReport();
            ActionsLeft -= Rules.directStepCost;
            var outcome = ScorePayoff(actor, room, payoff, out int sum, out float mult);

            if (payoff.KillsActor)
            {
                RemoveFromFilm(actor, ExitReason.Died);
                outcome.Killed = true;
            }
            else
            {
                outcome.ActorBroke = ChangePavor(actor, payoff.PavorDelta);
            }

            Log.actions.Add($"direct {actor.Def.DisplayName} {payoff.DisplayName}");
            Log.Add(ActIndex + 1, "direct",
                $"{payoff.DisplayName}: {actor.Def.DisplayName} em {room.Def.DisplayName} | " +
                $"{payoff.BaseScore} × {mult:0.##} × (1 + {sum}) = {outcome.Score}" +
                (outcome.Killed ? " | MORREU" : "") + (outcome.ActorBroke ? " | NÃO AGUENTOU" : ""));

            RecheckPlots();
            Scenes(Rules.directStepCost);
            AfterAction();
            return outcome;
        }

        /// <summary>Encerra o ato mesmo com ações sobrando (não gera batida).</summary>
        public void EndActEarly()
        {
            if (!CanAct) return;
            BeginReport();
            Log.actions.Add("end-act");
            ActionsLeft = 0;
            AfterAction();
        }

        /// <summary>Escolha do vilão (no fim do Ato 1). Começa o próximo ato (e o vilão entra na casa).</summary>
        public void ChooseVillain(VillainDef villain)
        {
            if (!AwaitingVillainChoice) return;
            BeginReport();
            Villain = villain;
            AwaitingVillainChoice = false;
            Log.actions.Add($"villain {villain.DisplayName}");
            Log.Add(ActIndex + 1, "villain", $"Vilão escolhido: {villain.DisplayName}");
            ActIndex++;
            StartAct();
        }

        /// <summary>Vilões oferecidos: os mais compatíveis com os elementos coletados primeiro.</summary>
        public List<VillainDef> VillainOffer()
        {
            int Score(VillainDef v) =>
                actors.Sum(a => a.Elements.Count(e => e.Subgenre == v.Subgenre)) +
                rooms.Sum(r => r.Elements.Count(e => e.Subgenre == v.Subgenre));
            return Content.Villains.Where(v => v != null).OrderByDescending(Score).ToList();
        }

        // ================================================================== Vilão NPC

        /// <summary>Cena que dispara quando o vilão pega alguém sozinho.</summary>
        public PayoffDef VillainCatchPayoff()
        {
            if (Rules.villainCatchPayoff != null) return Rules.villainCatchPayoff;
            foreach (var r in rooms)
                foreach (var p in r.Def.StagePayoffs)
                    if (p != null && p.KillsActor) return p;
            return null;
        }

        /// <summary>Slasher: 1 batida (anda para o espaço anunciado). Chamado no passo (c) do tique da casa.</summary>
        private void Beat()
        {
            var v = VillainNpc;
            if (v == null || !v.InHouse) return;

            if (v.StunBeats > 0)
            {
                v.StunBeats--;
                PlanVillain();
                return;
            }

            for (int m = 0; m < Rules.villainMovesPerBeat; m++)
            {
                int to = v.NextSpace;
                if (to >= 0 && to != v.Space && IsBarred(to))
                {
                    // Atleta segurando a porta: o vilão não entra nesta cena.
                    Report.Add(ReportKind.Villain, $"{BarredBy(to)} segura a porta de {Name(to)}: {v.Def.DisplayName} fica do lado de fora.");
                    Log.Add(ActIndex + 1, "villain", $"{v.Def.DisplayName} barrado na porta de {Name(to)}");
                    break;
                }
                if (to >= 0 && to != v.Space) MoveVillain(to, VillainMoveReason.Beat);

                // Chegou (ou ficou) num espaço com atores: encontro.
                if (ActorsIn(v.Space).Count > 0)
                {
                    ResolveVillainEncounter(v.Space, false);
                    break;
                }
                PlanVillain();
                if (v.NextSpace == v.Space) break;
            }
            PlanVillain();
        }

        private void PlanVillain() => VillainNpc?.Plan(Map, actors, IsBait);

        private void SpawnVillain()
        {
            VillainNpc = new VillainAgent(Villain);
            int space = FarthestFreeSpace(-1, true);
            if (space < 0) space = FarthestFreeSpace(-1, false);
            if (space < 0) space = Map.Hub >= 0 ? Map.Hub : 0;
            MoveVillain(space, VillainMoveReason.Spawn);
            if (VillainNpc.IsGhost) PlanGhostNext();
            PlanVillain();
            Report.Add(ReportKind.Villain, VillainNpc.IsGhost
                ? $"{Villain.DisplayName} passa a assombrar {Name(space)}."
                : $"{Villain.DisplayName} entrou na casa ({Name(space)}).");
        }

        private void MoveVillain(int to, VillainMoveReason reason)
        {
            var v = VillainNpc;
            int from = v.Space;
            v.PreviousSpace = from;
            v.Space = to;
            Log.Add(ActIndex + 1, "villain-move", $"{v.Def.DisplayName}: {Name(from)} → {Name(to)} ({reason})");
            VillainMoved?.Invoke(new VillainMoveEvent { From = from, To = to, Reason = reason });
        }

        private string Name(int space) => space >= 0 && space < rooms.Count ? rooms[space].Def.DisplayName : "lá fora";

        /// <summary>Vilão e atores no mesmo espaço: repelido (ferramenta), susto em grupo (2+) ou pega (sozinho).</summary>
        private void ResolveVillainEncounter(int space, bool walkedIn)
        {
            var v = VillainNpc;
            var present = ActorsIn(space);
            if (v == null || present.Count == 0) return;

            var o = new VillainEncounterOutcome { Space = space, ActorWalkedIn = walkedIn, Actors = present };
            int moveTo = -1;
            VillainMoveReason moveReason = VillainMoveReason.Teleport;

            // 1) Alguém ali tem a ferramenta que combate este vilão?
            foreach (var a in present)
            {
                var tool = a.Tools.FirstOrDefault(t => t.Repels(v.Def));
                if (tool == null) continue;
                o.ToolHolder = a;
                o.ToolUsed = tool;
                break;
            }

            if (o.ToolUsed != null)
            {
                o.Kind = VillainEncounterKind.Repelled;
                o.ToolHolder.Tools.Remove(o.ToolUsed);
                o.Score = Mathf.RoundToInt(Rules.repelScore * CurrentAct.payoffMultiplier);
                TotalScore += o.Score;
                o.PavorDelta = Rules.repelPavor;
                foreach (var a in present) if (ChangePavor(a, Rules.repelPavor)) o.Broke.Add(a);
                moveTo = FarthestFreeSpace(space, true);
                if (moveTo < 0) moveTo = FarthestFreeSpace(space, false);
                o.StunBeats = Rules.repelStunBeats;
            }
            else if (present.Count >= 2)
            {
                o.Kind = VillainEncounterKind.GroupScare;
                // Protótipo 3: combo que bloqueia o ataque (Grupo) = o vilão recua sem dar pavor.
                o.Blocked = ActiveCombosIn(space).Any(c => c.Def.BlocksVillainAttack);
                o.PavorDelta = o.Blocked ? 0 : Rules.groupScarePavor;
                if (o.PavorDelta != 0) foreach (var a in present) if (ChangePavor(a, o.PavorDelta)) o.Broke.Add(a);
                int at = space;
                for (int i = 0; i < Rules.groupScareRetreat; i++)
                {
                    int step = RetreatStep(at, i == 0 ? v.PreviousSpace : -1);
                    if (step < 0) break;
                    at = step;
                }
                if (at != space) { moveTo = at; moveReason = VillainMoveReason.Retreat; }
                o.StunBeats = Rules.groupScareStunBeats;
            }
            else
            {
                o.Kind = VillainEncounterKind.Caught;
                var victim = present[0];
                o.Victim = victim;
                var payoff = VillainCatchPayoff();
                if (payoff != null && ActIndex >= payoff.MinActIndex)
                {
                    o.Payoff = ScorePayoff(victim, rooms[space], payoff, out _, out _);
                    o.Payoff.Killed = true;
                    o.Score = o.Payoff.Score;
                }
                RemoveFromFilm(victim, ExitReason.Died);
            }

            if (moveTo == space) moveTo = -1;
            o.VillainMovedTo = moveTo;
            v.StunBeats = Mathf.Max(v.StunBeats, o.StunBeats);

            string who = string.Join(", ", present.Select(a => a.Def.DisplayName));
            Log.Add(ActIndex + 1, "villain",
                $"{v.Def.DisplayName} × {who} em {Name(space)}{(walkedIn ? " (entraram no espaço dele)" : "")}: " +
                (o.Kind == VillainEncounterKind.Repelled ? $"REPELIDO com {o.ToolUsed.DisplayName} de {o.ToolHolder.Def.DisplayName}, +{o.Score}" :
                 o.Kind == VillainEncounterKind.GroupScare ? $"susto no grupo, +{o.PavorDelta} de pavor" :
                 $"PEGOU {o.Victim.Def.DisplayName}" + (o.Payoff != null ? $" | {o.Payoff.Payoff.DisplayName} +{o.Score}" : "")) +
                (o.Broke.Count > 0 ? $" | não aguentaram: {string.Join(", ", o.Broke.Select(a => a.Def.DisplayName))}" : ""));

            ReportVillainEncounter(o);
            // A apresentação recebe primeiro o encontro, depois o deslocamento (sumir/recuar).
            VillainEncountered?.Invoke(o);
            if (moveTo >= 0) MoveVillain(moveTo, moveReason);
            PlanVillain();
        }

        /// <summary>
        /// Espaço sem atores mais longe (em saltos) do ator mais próximo. Empate: sorteio do fluxo do vilão.
        /// onlyRooms: só Salas (senão qualquer espaço onde se pode parar). -1 = nenhum.
        /// </summary>
        private int FarthestFreeSpace(int exclude, bool onlyRooms)
        {
            var occupied = new HashSet<int>();
            foreach (var a in actors)
            {
                if (!a.Alive) continue;
                occupied.Add(a.RoomIndex >= 0 ? a.RoomIndex : Map.EntranceIndex);
            }
            var distances = occupied.Where(s => s >= 0).Select(s => VillainAgent.Hops(Map, s)).ToList();

            var best = new List<int>();
            int bestScore = int.MinValue;
            for (int i = 0; i < rooms.Count; i++)
            {
                if (i == exclude || occupied.Contains(i) || !rooms[i].IsDestination || rooms[i].Sealed) continue;
                if (onlyRooms && !rooms[i].IsExplorable) continue;
                int score = 1000;
                foreach (var d in distances) if (d[i] >= 0) score = Mathf.Min(score, d[i]);
                if (score > bestScore) { bestScore = score; best.Clear(); }
                if (score == bestScore) best.Add(i);
            }
            return best.Count == 0 ? -1 : best[villainRng.Range(0, best.Count - 1)];
        }

        /// <summary>Um espaço para recuar: vizinho sem atores (de preferência de onde veio; senão o de menor índice).</summary>
        private int RetreatStep(int from, int prefer)
        {
            int best = -1;
            foreach (int n in Map.Neighbors(from))
            {
                if (n < 0 || ActorsIn(n).Count > 0) continue;
                if (n == prefer) return n;
                if (best < 0 || n < best) best = n;
            }
            return best;
        }

        // ================================================================== Internos

        private void StartAct()
        {
            ActionsLeft = CurrentAct.steps + Mathf.RoundToInt(ArtSum(ArtefatoEffectType.ExtraScenesPerAct));
            ScenesThisAct = ActionsLeft;
            foreach (var a in actors) a.DoorHoldsThisAct = 0;

            foreach (var room in rooms)
            {
                room.VisitedThisAct = false;
                // Só Salas sorteiam encontro (convivência e corredor nunca; não gasta sorteio).
                room.Encounter = room.IsExplorable ? RollEncounter(room.Def) : null;
            }

            Log.Add(ActIndex + 1, "act", $"{CurrentAct.name} começa — {ActionsLeft} cenas");

            if (VillainNpc == null && Villain != null && Rules.villainNpcEnabled && ActIndex >= Rules.villainFirstActIndex)
            {
                SpawnVillain();
            }
            else
            {
                PlanVillain();
            }

            ActStarted?.Invoke(ActIndex);
        }

        private EncounterDef RollEncounter(RoomDef def)
        {
            var options = new List<EncounterDef>();
            var weights = new List<float>();
            foreach (var we in def.Encounters)
            {
                if (we?.encounter == null || we.weight <= 0f) continue;
                // Encontro que SÓ dá uma ferramenta que já está em jogo: fica de fora.
                // (Com elemento junto, ex.: Faca + Arma, continua valendo; só não dá a ferramenta repetida.)
                if (we.encounter.Tool != null && we.encounter.Element == null && ToolInPlay(we.encounter.Tool)) continue;

                float w = we.weight;
                var el = we.encounter.Element;
                if (Villain != null && el != null && el.Subgenre != null && el.Subgenre == Villain.Subgenre)
                {
                    w *= Rules.villainEncounterBias;
                }
                options.Add(we.encounter);
                weights.Add(w);
            }

            int i = Rng.PickWeightedIndex(weights);
            return i >= 0 ? options[i] : null;
        }

        /// <summary>Pontua uma cena (dirigida ou disparada pelo vilão): base × mult. do ato × (1 + soma). Gasta os elementos.</summary>
        private PayoffOutcome ScorePayoff(ActorRunState actor, RoomRunState room, PayoffDef payoff, out int sum, out float mult)
        {
            var counted = CountElements(actor, payoff);
            sum = counted.Sum(c => c.Weight);
            mult = CurrentAct.payoffMultiplier;
            // Protótipo 3: Popular rende mais; artefatos de susto/morte. (1 = regra do Protótipo 2.)
            float extra = PopularMult(actor) * ArtMult(payoff.KillsActor ? ArtefatoEffectType.DeathScoreMult : ArtefatoEffectType.ScareScoreMult);
            int score = Mathf.RoundToInt(payoff.BaseScore * mult * (1 + sum) * extra);
            TotalScore += score;

            if (payoff.ConsumesElements)
            {
                foreach (var c in counted)
                {
                    if (c.IsAuto) continue;
                    if (c.FromRoom) room.Elements.Remove(c.Element);
                    else actor.Elements.Remove(c.Element);
                }
            }

            return new PayoffOutcome { Actor = actor, Room = room, Payoff = payoff, Counted = counted, Score = score };
        }

        /// <summary>Ator pega o que couber das ferramentas no chão ao entrar (grátis).</summary>
        private void PickUpFloorTools(ActorRunState actor, RoomRunState room, MoveOutcome outcome)
        {
            while (actor.Alive && room.FloorTools.Count > 0 && actor.Tools.Count < Rules.maxToolsPerActor)
            {
                var t = room.FloorTools[0];
                room.FloorTools.RemoveAt(0);
                actor.Tools.Add(t);
                outcome.ToolsPickedUp.Add(t);
            }
        }


        private void AfterAction()
        {
            // Protótipo 3: travas de crise criadas NESTA ação passam a contar a partir da próxima cena.
            foreach (var a in actors) a.LockFresh = false;
            if (Status != RunStatus.Playing) return;
            RecheckPlots();
            if (!actors.Any(a => a.Alive))
            {
                Log.Add(ActIndex + 1, "end", "Todo o elenco saiu do filme.");
                End(RunStatus.Lost);
                return;
            }

            if (ActionsLeft <= 0) EndAct();
        }

        private void EndAct()
        {
            var act = CurrentAct;
            bool lastAct = ActIndex >= ActCount - 1;
            var result = new ActOutcome
            {
                ActIndex = ActIndex,
                ActName = act.name,
                Goal = act.goal,
                TotalScore = TotalScore,
                Passed = TotalScore >= act.goal,
                Fatal = act.failIsFatal,
            };

            bool continues = (result.Passed || !result.Fatal) && !lastAct;
            result.OffersVillain = continues && Villain == null && Content.Villains.Count > 0;
            result.OffersArtefato = continues && PrepareArtefatoOffer();

            Log.Add(ActIndex + 1, "act-end", $"{act.name}: {TotalScore}/{act.goal} — {(result.Passed ? "passou" : "falhou")}");
            ActEnded?.Invoke(result);

            if (!result.Passed && (result.Fatal || lastAct))
            {
                End(RunStatus.Lost);
                return;
            }

            if (lastAct)
            {
                End(RunStatus.Won);
                return;
            }

            if (result.OffersArtefato)
            {
                // Escolha de artefato vem ANTES do vilão / do próximo ato (ChooseArtefato continua o fluxo).
                AwaitingArtefatoChoice = true;
                villainChoiceAfterArtefato = result.OffersVillain;
                return;
            }

            if (result.OffersVillain)
            {
                AwaitingVillainChoice = true;
                return;
            }

            ActIndex++;
            StartAct();
        }

        private void End(RunStatus status)
        {
            Status = status;
            Log.Add(ActIndex + 1, "end", status == RunStatus.Won ? "Filme aprovado!" : "Filme fracassou.");
            RunEnded?.Invoke(status);
        }
    }
}
