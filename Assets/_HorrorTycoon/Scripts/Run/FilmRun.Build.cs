using System;
using System.Collections.Generic;
using System.Linq;
using HorrorTycoon.Actors;
using HorrorTycoon.Scoring;
using UnityEngine;

namespace HorrorTycoon.Run
{
    /// <summary>
    /// PROTÓTIPO 3 — "Build do Filme" (Build_do_Filme_v1.md). C# puro.
    ///
    /// A CENA é o turno. Toda ação que gasta cena (explorar sala nova, dirigir, usar ferramenta, "Gravar cena aqui")
    /// roda, para CADA cena gasta, o TIQUE DA CASA, sempre nesta ordem:
    ///   (a) a cena escolhida acontece (já resolvida pelo método da ação: encontro, payoff, ferramenta...);
    ///   (b) cada ator vivo conta 1 cena onde está:
    ///       b1. plots de permanência avançam (ou quebram) e, se completos, dão a recompensa;
    ///       b2. cenas de dupla (combos) pontuam; atores perto do vilão rendem audiência;
    ///       b3. pavor do lugar (sala escura/perigosa, sozinho, perto do vilão) e a calma da Final Girl;
    ///       b4. crise de pavor (quem chegou no máximo);
    ///   (c) o vilão age pela família dele (Slasher: anda 1 espaço anunciado; Fantasma: enche a tensão / Grande Susto);
    ///   fim: travas de crise e "segurar a porta" vencem.
    /// Andar grátis NÃO roda o tique (é só posicionamento para a próxima cena).
    /// </summary>
    public partial class FilmRun
    {
        // ================================================================== Estado

        private readonly List<PlotRunState> plots = new List<PlotRunState>();
        private readonly List<ArtefatoDef> owned = new List<ArtefatoDef>();
        private readonly List<ArtefatoDef> artefatoOffer = new List<ArtefatoDef>();
        private bool villainChoiceAfterArtefato;
        private bool deferCrisis;

        // "Segurar a porta" (Atleta): espaço barrado no tique 'barredTick'.
        private int barredSpace = -1;
        private int barredTick = -1;
        private ActorRunState barredBy;

        /// <summary>Plots da run (ativos, cumpridos e falhados), na ordem em que apareceram.</summary>
        public IReadOnlyList<PlotRunState> Plots => plots;
        /// <summary>Artefatos do filme.</summary>
        public IReadOnlyList<ArtefatoDef> OwnedArtefatos => owned;
        /// <summary>Oferta atual "1 de N" (só enquanto AwaitingArtefatoChoice).</summary>
        public IReadOnlyList<ArtefatoDef> ArtefatoOffer => artefatoOffer;
        public bool AwaitingArtefatoChoice { get; private set; }
        /// <summary>Cenas gravadas no filme todo (tiques da casa).</summary>
        public int SceneNumber { get; private set; }
        /// <summary>Cenas que o ato atual começou (formato + artefatos de cena extra pegos ANTES do ato).</summary>
        public int ScenesThisAct { get; private set; }
        /// <summary>Cenas que sobram neste ato (alias de ActionsLeft).</summary>
        public int ScenesLeft => ActionsLeft;
        /// <summary>Relatório da última ação (o que o tique da casa fez). Recriado a cada ação.</summary>
        public SceneReport Report { get; private set; } = new SceneReport();

        /// <summary>Grande Susto (ou exorcismo) do fantasma.</summary>
        public event Action<GhostScareOutcome> GhostScared;
        /// <summary>Crise de pavor.</summary>
        public event Action<CrisisEvent> CrisisHappened;

        private float ActMult => CurrentAct.payoffMultiplier;
        private float TickActMult => Rules.tickScoreUsesActMultiplier ? CurrentAct.payoffMultiplier : 1f;

        private void BeginReport() => Report = new SceneReport { FirstScene = SceneNumber + 1 };

        // ================================================================== Ações novas

        public bool CanRecordScene => CanAct && ActionsLeft >= Rules.recordSceneCost && actors.Any(a => a.Alive);

        /// <summary>"Gravar cena aqui" / "Esperar 1 cena": ninguém se mexe; a casa inteira conta a cena (plots avançam).</summary>
        public void RecordScene()
        {
            if (!CanRecordScene) throw new InvalidOperationException("Não dá para gravar cena agora.");
            BeginReport();
            ActionsLeft -= Rules.recordSceneCost;
            Log.actions.Add("hold");
            Log.Add(ActIndex + 1, "hold", "Cena gravada: o elenco segura a posição.");
            Scenes(Rules.recordSceneCost);
            AfterAction();
        }

        /// <summary>Atleta pode "segurar a porta" agora? (grátis, 1× por ato, só com o vilão na casa)</summary>
        public bool CanHoldDoor(ActorRunState actor)
        {
            if (!CanAct || actor == null || !actor.Alive || !Rules.passivesEnabled) return false;
            if (actor.Role != ActorRole.Atleta || Rules.atletaHoldDoorPerAct <= 0) return false;
            if (actor.RoomIndex < 0 || !rooms[actor.RoomIndex].IsDestination) return false;
            if (VillainNpc == null || !VillainNpc.InHouse) return false;
            if (IsDoorHeldNext(actor.RoomIndex)) return false;
            return actor.DoorHoldsThisAct < Rules.atletaHoldDoorPerAct;
        }

        /// <summary>O Atleta segura a porta: na PRÓXIMA cena o vilão não entra no espaço dele. Não gasta cena.</summary>
        public void HoldDoor(ActorRunState actor)
        {
            if (!CanHoldDoor(actor)) throw new InvalidOperationException("Não dá para segurar a porta agora.");
            BeginReport();
            barredSpace = actor.RoomIndex;
            barredTick = SceneNumber + 1;
            barredBy = actor;
            actor.DoorHoldsThisAct++;
            actor.DoorHeldAct = ActIndex;
            Log.actions.Add($"holddoor {actor.Def.DisplayName}");
            Log.Add(ActIndex + 1, "holddoor", $"{actor.Def.DisplayName} segura a porta de {Name(barredSpace)}");
            Report.Add(ReportKind.Info, $"{actor.Def.DisplayName} segura a porta de {Name(barredSpace)}: o vilão não entra aqui na próxima cena.");
        }

        /// <summary>O espaço está com a porta segurada para a próxima cena (HUD).</summary>
        public bool IsDoorHeldNext(int space) => barredSpace == space && barredTick == SceneNumber + 1 && BarrerStillThere();

        private bool IsBarred(int space) => barredSpace == space && barredTick == SceneNumber && BarrerStillThere();

        private bool BarrerStillThere() => barredBy != null && barredBy.Alive && barredBy.RoomIndex == barredSpace;

        private string BarredBy(int space) => barredBy != null ? barredBy.Def.DisplayName : "Alguém";

        /// <summary>Escolha entre atos ("1 de N"). null = pular. Continua para o vilão ou o próximo ato.</summary>
        public void ChooseArtefato(ArtefatoDef artefato)
        {
            if (!AwaitingArtefatoChoice) return;
            BeginReport();
            if (artefato != null && artefatoOffer.Contains(artefato))
            {
                GiveArtefato(artefato, "escolha entre atos");
                Log.actions.Add($"artefato {artefato.DisplayName}");
            }
            else
            {
                Log.actions.Add("artefato -");
                Log.Add(ActIndex + 1, "artefato", "Nenhum artefato escolhido.");
            }

            AwaitingArtefatoChoice = false;
            artefatoOffer.Clear();
            if (villainChoiceAfterArtefato)
            {
                villainChoiceAfterArtefato = false;
                AwaitingVillainChoice = true;
                return;
            }
            ActIndex++;
            StartAct();
        }

        // ================================================================== Consultas (HUD e testes)

        /// <summary>Espaço do vilão ou vizinho (os "dois lados": rende mais, assusta mais).</summary>
        public bool IsNearVillain(int space)
        {
            var v = VillainNpc;
            if (v == null || !v.InHouse || space < 0) return false;
            if (space == v.Space) return true;
            return Rules.nearVillainIncludesAdjacent && Map.Neighbors(v.Space).Contains(space);
        }

        /// <summary>Cenas de dupla ativas num espaço agora.</summary>
        public List<ActiveCombo> ActiveCombosIn(int space)
        {
            var list = new List<ActiveCombo>();
            if (!Rules.combosEnabled || space < 0 || space >= rooms.Count) return list;
            var present = ActorsIn(space);
            if (present.Count == 0) return list;
            foreach (var def in Content.DuoCombos)
            {
                if (def == null) continue;
                if (def.RequiresRoom && !rooms[space].IsExplorable) continue;
                if (present.Count < Mathf.Max(1, def.MinActors)) continue;
                if (!HasRoles(present, def.RequiredRoles)) continue;
                list.Add(new ActiveCombo { Def = def, Space = space, Actors = present });
            }
            return list;
        }

        /// <summary>Todas as cenas de dupla ativas na casa agora.</summary>
        public List<ActiveCombo> AllActiveCombos()
        {
            var list = new List<ActiveCombo>();
            for (int i = 0; i < rooms.Count; i++) list.AddRange(ActiveCombosIn(i));
            return list;
        }

        /// <summary>Plots ativos que valem nesta sala (para o card e o monitor).</summary>
        public List<PlotRunState> PlotsFor(int room)
        {
            var list = new List<PlotRunState>();
            if (room < 0 || room >= rooms.Count) return list;
            foreach (var p in plots) if (p.IsActive && PlotRoomAllowed(p, room)) list.Add(p);
            return list;
        }

        /// <summary>Sala que a HUD usa para mostrar o plot (a fixa, a que está avançando, ou a 1ª que serve).</summary>
        public int PlotDisplayRoom(PlotRunState p)
        {
            if (p.RoomIndex >= 0) return p.RoomIndex;
            if (p.ActiveRoom >= 0) return p.ActiveRoom;
            for (int i = 0; i < rooms.Count; i++) if (PlotRoomAllowed(p, i)) return i;
            return -1;
        }

        /// <summary>Cenas necessárias agora (com o Nerd na sala, plots de investigação levam menos).</summary>
        public int RequiredScenes(PlotRunState p, int room)
        {
            int s = p.Def.Scenes;
            if (Rules.passivesEnabled && p.Def.Investigation && room >= 0 && ActorsIn(room).Any(a => a.Role == ActorRole.Nerd))
                s -= Rules.nerdPlotReduction;
            return Mathf.Max(1, s);
        }

        /// <summary>A condição do plot está cumprida nesta sala agora?</summary>
        public bool PlotConditionMet(PlotRunState p, int room)
        {
            if (room < 0 || room >= rooms.Count || !PlotRoomAllowed(p, room)) return false;
            var present = ActorsIn(room);
            if (present.Count == 0) return false;
            if (present.Count < p.Def.MinActors) return false;
            if (p.Def.MaxActors > 0 && present.Count > p.Def.MaxActors) return false;
            return HasRoles(present, p.Def.RequiredRoles);
        }

        public float GhostTensionRatio => VillainNpc != null && VillainNpc.IsGhost
            ? Mathf.Clamp01((float)VillainNpc.Tension / Mathf.Max(1, Rules.ghostTensionMax)) : 0f;

        private bool PlotRoomAllowed(PlotRunState p, int room)
        {
            var r = rooms[room];
            if (!r.IsDestination || r.Sealed) return false;
            if (p.RoomIndex >= 0) return room == p.RoomIndex;
            if (p.Def.Rooms.Count > 0) return p.Def.Rooms.Contains(r.Def);
            return r.IsExplorable;
        }

        private static bool HasRoles(List<ActorRunState> present, IReadOnlyList<ActorRole> roles)
        {
            if (roles == null || roles.Count == 0) return true;
            // Papel repetido na lista = precisa de dois atores com ele.
            var used = new List<ActorRunState>();
            foreach (var role in roles)
            {
                if (role == ActorRole.None) continue;
                var a = present.FirstOrDefault(x => x.Role == role && !used.Contains(x));
                if (a == null) return false;
                used.Add(a);
            }
            return true;
        }

        // ================================================================== Multiplicadores

        /// <summary>Produto dos multiplicadores dos artefatos deste tipo (combo: só os que valem para ela).</summary>
        public float ArtMult(ArtefatoEffectType type, DuoComboDef combo = null)
        {
            float m = 1f;
            foreach (var art in owned)
                foreach (var e in art.Effects)
                    if (e != null && e.type == type && (type != ArtefatoEffectType.ComboScoreMult || e.combo == null || e.combo == combo))
                        m *= e.value;
            return m;
        }

        /// <summary>Soma dos valores dos artefatos deste tipo (ex.: cenas extras por ato).</summary>
        public float ArtSum(ArtefatoEffectType type)
        {
            float s = 0f;
            foreach (var art in owned)
                foreach (var e in art.Effects)
                    if (e != null && e.type == type) s += e.value;
            return s;
        }

        private float PopularMult(ActorRunState actor) =>
            Rules.passivesEnabled && actor != null && actor.Role == ActorRole.Popular ? Rules.popularSceneMult : 1f;

        private float PopularMult(IEnumerable<ActorRunState> group) =>
            Rules.passivesEnabled && group.Any(a => a.Role == ActorRole.Popular) ? Rules.popularSceneMult : 1f;

        private bool IsBait(ActorRunState a) => Rules.passivesEnabled && Rules.popularIsBait && a.Role == ActorRole.Popular;

        // ================================================================== Tique da casa

        /// <summary>Roda 'count' cenas (tiques da casa). Cada cena = 1 turno para a casa inteira.</summary>
        private void Scenes(int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (Status != RunStatus.Playing || !actors.Any(a => a.Alive)) return;
                HouseTick();
            }
        }

        private void HouseTick()
        {
            SceneNumber++;
            Report.Scenes++;
            int before = TotalScore;

            // (b) cada ator conta 1 cena onde está
            TickPlots();
            TickCombos();
            TickPavor();
            foreach (var a in actors) CheckCrisis(a);

            // (c) o vilão age pela família dele
            var v = VillainNpc;
            if (v != null && v.InHouse && actors.Any(a => a.Alive))
            {
                if (v.IsGhost) GhostTick();
                else Beat();
            }

            // fim: "segurar a porta" vence; plots revalidados (o vilão pode ter tirado alguém)
            if (barredTick >= 0 && barredTick <= SceneNumber) { barredSpace = -1; barredTick = -1; barredBy = null; }
            RecheckPlots();
            foreach (var a in actors)
            {
                if (a.LockedScenes > 0 && !a.LockFresh) a.LockedScenes--;
            }

            Log.Add(ActIndex + 1, "scene", $"Cena {SceneNumber}: +{TotalScore - before}");
        }

        // ---------------------------------------------------------------- b1. Plots

        private void OfferActorPlots()
        {
            if (!Rules.plotsEnabled) return;
            foreach (var a in actors)
            {
                if (!a.Alive) continue;
                foreach (var def in a.Def.Plots) AddPlot(def, -1, a.Def.DisplayName, 0);
            }
        }

        private void OfferRoomPlots(RoomRunState room)
        {
            room.PlotsOffered = true;
            if (!Rules.plotsEnabled) return;
            int at = Rules.plotStartsNextScene ? SceneNumber + 1 : SceneNumber;
            foreach (var def in room.Def.Plots)
            {
                if (def == null) continue;
                AddPlot(def, def.Rooms.Count == 0 ? room.Index : -1, room.Def.DisplayName, at);
            }
        }

        private void AddPlot(PlotDef def, int roomIndex, string source, int offeredAt)
        {
            if (def == null || plots.Any(p => p.Def == def)) return;
            // Papel que não existe no elenco: o plot nem aparece.
            foreach (var role in def.RequiredRoles)
                if (role != ActorRole.None && !actors.Any(a => a.Alive && a.Role == role)) return;

            var p = new PlotRunState(def, roomIndex) { OfferedAtScene = offeredAt, Source = source };
            plots.Add(p);
            Report.Add(ReportKind.Plot, $"Novo plot: <b>{def.DisplayName}</b> — {def.Description}");
            Log.Add(ActIndex + 1, "plot", $"Novo plot ({source}): {def.DisplayName}");
        }

        private void TickPlots()
        {
            foreach (var p in plots.ToList())
            {
                if (!p.IsActive || SceneNumber <= p.OfferedAtScene) continue;

                int room = -1;
                if (p.ActiveRoom >= 0 && PlotConditionMet(p, p.ActiveRoom)) room = p.ActiveRoom;
                else for (int i = 0; i < rooms.Count && room < 0; i++) if (PlotConditionMet(p, i)) room = i;

                if (room < 0)
                {
                    if (p.Progress > 0) BreakPlot(p, "a cena ficou sem quem precisava");
                    continue;
                }
                if (p.ActiveRoom >= 0 && p.ActiveRoom != room && p.Progress > 0) BreakPlot(p, "mudou de sala");

                p.ActiveRoom = room;
                int step = 1 + ActiveCombosIn(room).Sum(c => c.Def.PlotSpeedBonus);
                p.Progress += step;
                int req = RequiredScenes(p, room);
                if (p.Progress >= req) CompletePlot(p, room);
                else Report.Add(ReportKind.Plot, $"Plot <b>{p.Def.DisplayName}</b>: {p.Progress}/{req} cenas em {Name(room)}.");
            }
        }

        private void CompletePlot(PlotRunState p, int room)
        {
            p.Status = PlotStatus.Completed;
            p.Progress = RequiredScenes(p, room);
            var present = ActorsIn(room);
            var participants = present.Where(a => p.Def.RequiredRoles.Count == 0 || p.Def.RequiredRoles.Contains(a.Role)).ToList();
            if (participants.Count == 0) participants = present;

            float mult = (Rules.plotUsesActMultiplier ? ActMult : 1f) * ArtMult(ArtefatoEffectType.PlotRewardMult)
                         * (IsNearVillain(room) ? Rules.nearVillainScoreMult : 1f) * PopularMult(participants);
            int pts = Mathf.RoundToInt(p.Def.RewardPoints * mult);
            TotalScore += pts;
            Report.Add(ReportKind.PlotDone, $"★ Plot cumprido: <b>{p.Def.DisplayName}</b>", pts);
            if (!string.IsNullOrEmpty(p.Def.RewardText)) Report.Add(ReportKind.PlotDone, p.Def.RewardText);
            Log.Add(ActIndex + 1, "plot-done", $"{p.Def.DisplayName} em {Name(room)} +{pts}");

            // Liberar sala lacrada ("ações liberam salas").
            if (p.Def.UnlockRoom != null)
            {
                foreach (var r in rooms)
                {
                    if (r.Def != p.Def.UnlockRoom || !r.Sealed) continue;
                    r.Sealed = false;
                    Report.Add(ReportKind.Unlock, $"<b>{r.Def.DisplayName}</b> liberado(a)! Agora dá para entrar.");
                    Log.Add(ActIndex + 1, "unlock", $"{r.Def.DisplayName} liberada pelo plot {p.Def.DisplayName}");
                }
                // Casa por escolha: a sala lacrada ainda não foi montada? Passa a poder ser escolhida numa porta.
                if (UnlockForDraft(p.Def.UnlockRoom))
                {
                    Report.Add(ReportKind.Unlock, $"<b>{p.Def.UnlockRoom.DisplayName}</b> liberado(a)! Pode aparecer ao abrir uma porta.");
                    Log.Add(ActIndex + 1, "unlock", $"{p.Def.UnlockRoom.DisplayName} entra na escolha de salas (plot {p.Def.DisplayName})");
                }
            }

            if (p.Def.RewardTool != null && !ToolInPlay(p.Def.RewardTool))
            {
                var taker = participants.FirstOrDefault(a => a.Tools.Count < Rules.maxToolsPerActor);
                if (taker != null) taker.Tools.Add(p.Def.RewardTool);
                else rooms[room].FloorTools.Add(p.Def.RewardTool);
                Report.Add(ReportKind.Unlock, $"Ferramenta: <b>{p.Def.RewardTool.DisplayName}</b>{(taker != null ? $" (com {taker.Def.DisplayName})" : " (no chão)")}");
            }

            if (p.Def.RewardArtefato != null && !owned.Contains(p.Def.RewardArtefato)) GiveArtefato(p.Def.RewardArtefato, p.Def.DisplayName);
            if (p.Def.RewardRandomArtefato)
            {
                var pool = Content.Artefatos.Where(a => a != null && !owned.Contains(a)).Distinct().ToList();
                if (pool.Count > 0) GiveArtefato(pool[artefatoRng.Range(0, pool.Count - 1)], p.Def.DisplayName);
            }

            if (p.Def.RewardElement != null)
            {
                if (p.Def.RewardElement.Holder == ElementHolder.Actor) participants[0].Elements.Add(p.Def.RewardElement);
                else rooms[room].Elements.Add(p.Def.RewardElement);
                Report.Add(ReportKind.Unlock, $"Elemento: <b>{p.Def.RewardElement.DisplayName}</b>");
            }

            if (p.Def.RewardPavor != 0)
            {
                foreach (var a in participants) ChangePavor(a, p.Def.RewardPavor);
            }
        }

        private void BreakPlot(PlotRunState p, string reason)
        {
            p.TimesBroken++;
            p.ActiveRoom = -1;
            if (p.Def.FailOnBreak)
            {
                p.Status = PlotStatus.Failed;
                p.Progress = 0;
                Report.Add(ReportKind.PlotBroken, $"Plot <b>{p.Def.DisplayName}</b> fracassou ({reason}).");
            }
            else
            {
                p.Progress = 0;
                Report.Add(ReportKind.PlotBroken, $"Plot <b>{p.Def.DisplayName}</b> quebrou ({reason}). Dá para recomeçar.");
            }
            Log.Add(ActIndex + 1, "plot-break", $"{p.Def.DisplayName}: {reason}");
        }

        /// <summary>Plots em andamento cuja condição deixou de valer quebram agora; plots sem atores possíveis falham.</summary>
        private void RecheckPlots()
        {
            foreach (var p in plots)
            {
                if (!p.IsActive) continue;
                bool impossible = p.Def.RequiredRoles.Any(r => r != ActorRole.None && !actors.Any(a => a.Alive && a.Role == r));
                if (impossible)
                {
                    p.Status = PlotStatus.Failed;
                    p.Progress = 0;
                    Report.Add(ReportKind.PlotBroken, $"Plot <b>{p.Def.DisplayName}</b> fracassou (falta quem fazia a cena).");
                    Log.Add(ActIndex + 1, "plot-fail", p.Def.DisplayName);
                    continue;
                }
                if (p.Progress > 0 && !PlotConditionMet(p, p.ActiveRoom)) BreakPlot(p, "alguém saiu da sala");
            }
        }

        /// <summary>Crise ou pega: o plot em andamento em que este ator participa quebra.</summary>
        private void BreakPlotsWith(ActorRunState actor, string reason)
        {
            foreach (var p in plots)
            {
                if (!p.IsActive || p.Progress <= 0 || p.ActiveRoom < 0 || p.ActiveRoom != actor.RoomIndex) continue;
                if (p.Def.RequiredRoles.Count > 0 && !p.Def.RequiredRoles.Contains(actor.Role)) continue;
                BreakPlot(p, reason);
            }
        }

        // ---------------------------------------------------------------- b2. Combos e "perto do vilão"

        private void TickCombos()
        {
            for (int s = 0; s < rooms.Count; s++)
            {
                var present = ActorsIn(s);
                if (present.Count == 0) continue;
                var combos = ActiveCombosIn(s);
                float sceneMult = 1f;
                foreach (var c in combos) sceneMult *= c.Def.SceneScoreMultiplier;
                bool near = IsNearVillain(s);
                float nearMult = near ? Rules.nearVillainScoreMult : 1f;

                foreach (var c in combos)
                {
                    if (c.Def.ScorePerScene <= 0) continue;
                    int pts = Mathf.RoundToInt(c.Def.ScorePerScene * TickActMult * sceneMult * nearMult * PopularMult(present)
                                               * ArtMult(ArtefatoEffectType.ComboScoreMult, c.Def));
                    if (pts <= 0) continue;
                    TotalScore += pts;
                    Report.Add(ReportKind.Combo, $"{c.Def.DisplayName} em {Name(s)}{(near ? " (perto do vilão!)" : "")}", pts);
                }

                if (near && Rules.nearVillainScenePoints > 0)
                {
                    int total = 0;
                    foreach (var a in present)
                        total += Mathf.RoundToInt(Rules.nearVillainScenePoints * TickActMult * sceneMult * PopularMult(a)
                                                  * ArtMult(ArtefatoEffectType.NearVillainScoreMult));
                    if (total > 0)
                    {
                        TotalScore += total;
                        Report.Add(ReportKind.Score, $"Cena perto do vilão em {Name(s)}", total);
                    }
                }
            }
        }

        // ---------------------------------------------------------------- b3. Pavor por cena

        private void TickPavor()
        {
            deferCrisis = true;
            var scared = new List<string>();
            foreach (var a in actors)
            {
                if (!a.Alive || a.RoomIndex < 0) continue;
                var room = rooms[a.RoomIndex];
                int gain = room.Def.PavorPerScene;
                var why = new List<string>();
                if (room.Def.PavorPerScene > 0) why.Add(room.Def.DisplayName);
                if (ActorsIn(a.RoomIndex).Count == 1 && Rules.alonePavorPerScene != 0) { gain += Rules.alonePavorPerScene; why.Add("sozinho(a)"); }
                if (IsNearVillain(a.RoomIndex) && Rules.nearVillainPavorPerScene != 0) { gain += Rules.nearVillainPavorPerScene; why.Add("vilão perto"); }
                if (gain == 0) continue;
                ChangePavor(a, gain);
                if (gain > 0) scared.Add($"{a.Def.DisplayName} ({string.Join(", ", why)})");
            }
            if (scared.Count > 0) Report.Add(ReportKind.Pavor, $"Medo: {string.Join(" · ", scared)}");

            if (Rules.passivesEnabled && Rules.finalGirlCalmPerScene > 0)
            {
                var calmed = new List<string>();
                foreach (var fg in actors.Where(x => x.Alive && x.RoomIndex >= 0 && x.Role == ActorRole.FinalGirl).ToList())
                {
                    foreach (var ally in ActorsIn(fg.RoomIndex))
                    {
                        if (ally == fg || ally.Pavor <= 0) continue;
                        ChangePavor(ally, -Rules.finalGirlCalmPerScene);
                        calmed.Add(ally.Def.DisplayName);
                    }
                }
                if (calmed.Count > 0) Report.Add(ReportKind.Pavor, $"A Final Girl acalma: {string.Join(", ", calmed)}");
            }
            deferCrisis = false;
        }

        // ---------------------------------------------------------------- Pavor e crise

        /// <summary>
        /// Muda o pavor. Ganhos passam pelos passivos (Nerd ×1,5, perto do Atleta ×0,5, Final Girl determinada)
        /// e pelos artefatos. Devolve true se o ator SAIU do filme agora (2ª crise / regra antiga).
        /// No passo b3 do tique a crise espera o b4 (deferCrisis).
        /// </summary>
        private bool ChangePavor(ActorRunState actor, int delta)
        {
            if (!actor.Alive) return false;
            if (delta > 0) delta = ModifyPavorGain(actor, delta);
            actor.Pavor = Mathf.Clamp(actor.Pavor + delta, 0, Rules.pavorLimit);
            if (deferCrisis) return false;
            return CheckCrisis(actor);
        }

        /// <summary>Pavor ganho depois dos passivos e artefatos (só ganhos; calma não muda).</summary>
        public int ModifyPavorGain(ActorRunState actor, int delta)
        {
            float f = ArtMult(ArtefatoEffectType.PavorGainMult);
            if (Rules.passivesEnabled)
            {
                if (actor.Role == ActorRole.Nerd) f *= Rules.nerdPavorMult;
                if (actor.Determined) f *= Rules.finalGirlDeterminedPavorMult;
                if (actor.RoomIndex >= 0 && actors.Any(o => o != actor && o.Alive && o.RoomIndex == actor.RoomIndex && o.Role == ActorRole.Atleta))
                    f *= Rules.atletaAllyPavorMult;
            }
            return Mathf.Max(0, Mathf.RoundToInt(delta * f));
        }

        /// <summary>Pavor no máximo: CRISE (trava, cena de pânico, pavor volta) ou sai do filme (2ª crise / regra antiga).</summary>
        private bool CheckCrisis(ActorRunState actor)
        {
            if (!actor.Alive || actor.Pavor < Rules.pavorLimit) return false;

            if (!Rules.crisisEnabled)
            {
                RemoveFromFilm(actor, ExitReason.Fled);
                Report.Add(ReportKind.Crisis, $"{actor.Def.DisplayName} não aguentou o medo e saiu do filme.");
                CrisisHappened?.Invoke(new CrisisEvent { Actor = actor, LeftFilm = true });
                return true;
            }

            actor.Crises++;
            if (actor.Crises >= Rules.crisesToLeave)
            {
                RemoveFromFilm(actor, ExitReason.Fled);
                Report.Add(ReportKind.Crisis, $"{actor.Def.DisplayName} tem a {actor.Crises}ª crise e FOGE do filme!");
                Log.Add(ActIndex + 1, "crisis", $"{actor.Def.DisplayName}: crise {actor.Crises} — saiu do filme");
                CrisisHappened?.Invoke(new CrisisEvent { Actor = actor, LeftFilm = true });
                return true;
            }

            actor.Pavor = Mathf.Min(Rules.crisisResetPavor, Rules.pavorLimit - 1);
            actor.LockedScenes = Rules.crisisLockScenes;
            actor.LockFresh = true;
            int pts = Mathf.RoundToInt(Rules.crisisPanicScore * ActMult * PopularMult(actor));
            TotalScore += pts;
            Report.Add(ReportKind.Crisis,
                $"Cena forçada: {actor.Def.DisplayName} entra em PÂNICO! Travado(a) por {Rules.crisisLockScenes} cena(s).", pts);
            Log.Add(ActIndex + 1, "crisis", $"{actor.Def.DisplayName}: crise {actor.Crises} (+{pts})");
            BreakPlotsWith(actor, $"{actor.Def.DisplayName} entrou em pânico");
            CrisisHappened?.Invoke(new CrisisEvent { Actor = actor, Score = pts });
            return false;
        }

        /// <summary>Ator sai do filme: ferramentas no chão; morte = a Final Girl por perto testemunha.</summary>
        private void RemoveFromFilm(ActorRunState actor, ExitReason reason)
        {
            BreakPlotsWith(actor, reason == ExitReason.Died ? $"{actor.Def.DisplayName} morreu" : $"{actor.Def.DisplayName} saiu do filme");
            actor.Alive = false;
            actor.Exit = reason;
            actor.LockedScenes = 0;
            if (actor.RoomIndex >= 0) rooms[actor.RoomIndex].FloorTools.AddRange(actor.Tools);
            actor.Tools.Clear();
            if (reason == ExitReason.Died) FinalGirlWitness(actor.RoomIndex, actor);
        }

        private void FinalGirlWitness(int space, ActorRunState victim)
        {
            if (!Rules.passivesEnabled || space < 0) return;
            foreach (var fg in actors)
            {
                if (!fg.Alive || fg.Role != ActorRole.FinalGirl || fg.RoomIndex < 0) continue;
                if (fg.RoomIndex != space && !Map.Neighbors(space).Contains(fg.RoomIndex)) continue;
                fg.Pavor = 0;
                fg.Determined = true;
                int pts = Mathf.RoundToInt(Rules.finalGirlWitnessScore * ActMult);
                TotalScore += pts;
                Report.Add(ReportKind.Score, $"{fg.Def.DisplayName} testemunha a morte de {victim.Def.DisplayName} e fica DETERMINADA (pavor zera).", pts);
                Log.Add(ActIndex + 1, "witness", $"{fg.Def.DisplayName} testemunhou {victim.Def.DisplayName} (+{pts})");
            }
        }

        // ---------------------------------------------------------------- (c) Fantasma

        private void GhostTick()
        {
            var v = VillainNpc;
            if (v.StunBeats > 0)
            {
                v.StunBeats--;
                Report.Add(ReportKind.Ghost, $"{v.Def.DisplayName} se recompõe em {Name(v.Space)}.");
                return;
            }

            int s = v.Space;
            var inside = ActorsIn(s);

            // Exorcismo: alguém lá dentro tem a ferramenta que combate o fantasma (Crucifixo).
            foreach (var a in inside)
            {
                var tool = a.Tools.FirstOrDefault(t => t.Repels(v.Def));
                if (tool == null) continue;
                a.Tools.Remove(tool);
                int pts = Mathf.RoundToInt(Rules.repelScore * ActMult);
                TotalScore += pts;
                var ex = new GhostScareOutcome { Space = s, Tension = v.Tension, Actors = inside, Exorcised = true, ToolHolder = a, ToolUsed = tool, Score = pts };
                v.Tension = 0;
                Report.Add(ReportKind.Ghost, $"{a.Def.DisplayName} ergue o <b>{tool.DisplayName}</b>: {v.Def.DisplayName} é expulso(a) de {Name(s)}! (ferramenta gasta)", pts);
                Log.Add(ActIndex + 1, "ghost", $"exorcizado com {tool.DisplayName} de {a.Def.DisplayName} +{pts}");
                GhostScared?.Invoke(ex);
                GhostRelocate();
                v.StunBeats = Rules.ghostRepelStunScenes;
                return;
            }

            int gain = Mathf.RoundToInt((Rules.ghostTensionPerScene + Rules.ghostTensionPerActor * inside.Count)
                                        * ArtMult(ArtefatoEffectType.GhostTensionGainMult));
            v.Tension += gain;
            if (v.Tension < Rules.ghostTensionMax)
            {
                Report.Add(ReportKind.Ghost, inside.Count > 0
                    ? $"A tensão cresce em {Name(s)}: {v.Def.DisplayName} se alimenta de quem está lá."
                    : $"A tensão cresce em {Name(s)}.");
                return;
            }

            // GRANDE SUSTO em quem estiver na sala.
            var o = new GhostScareOutcome { Space = s, Tension = v.Tension, Actors = inside };
            if (inside.Count > 0)
            {
                // Cada ator lá dentro vale (1 + pavor/100): mais gente e mais medo = susto maior.
                float people = inside.Sum(a => 1f + a.Pavor / 100f);
                o.Score = Mathf.RoundToInt(Rules.ghostScorePerTension * v.Tension * ActMult * people
                                           * ArtMult(ArtefatoEffectType.ScareScoreMult) * PopularMult(inside));
                TotalScore += o.Score;
                Report.Add(ReportKind.Ghost, $"<b>GRANDE SUSTO</b> em {Name(s)}! {string.Join(", ", inside.Select(a => a.Def.DisplayName))} gritam.", o.Score);
                foreach (var a in inside)
                {
                    int crisesBefore = a.Crises;
                    ChangePavor(a, Rules.ghostScarePavor);
                    if (a.Crises != crisesBefore || !a.Alive) o.Crisis.Add(a);
                }
            }
            else
            {
                Report.Add(ReportKind.Ghost, $"O Grande Susto em {Name(s)} assusta só a poeira: não havia ninguém lá.");
            }
            Log.Add(ActIndex + 1, "ghost", $"Grande Susto em {Name(s)} (tensão {v.Tension}, {inside.Count} ator(es)) +{o.Score}");
            v.Scares++;
            v.Tension = 0;
            GhostScared?.Invoke(o);
            GhostRelocate();
        }

        /// <summary>Fantasma vai assombrar a sala anunciada (ou outra, se ela estiver barrada) e anuncia a próxima.</summary>
        private void GhostRelocate()
        {
            var v = VillainNpc;
            int to = v.NextSpace;
            if (to < 0 || to == v.Space || IsBarred(to) || rooms[to].Sealed) to = ChooseGhostRoom(v.Space);
            if (to >= 0 && to != v.Space)
            {
                MoveVillain(to, VillainMoveReason.Haunt);
                Report.Add(ReportKind.Ghost, $"{v.Def.DisplayName} agora assombra {Name(to)}.");
            }
            PlanGhostNext();
        }

        /// <summary>Anuncia a próxima sala que o fantasma vai assombrar (depois do próximo susto).</summary>
        private void PlanGhostNext()
        {
            var v = VillainNpc;
            if (v == null || !v.IsGhost) return;
            int next = ChooseGhostRoom(v.Space);
            v.NextSpace = next >= 0 ? next : v.Space;
        }

        /// <summary>
        /// Próxima sala assombrada: SORTEADA (fluxo do vilão) entre as salas não lacradas e não barradas, menos a atual.
        /// O fantasma não persegue ninguém: é o jogador que decide quem leva para lá (a sala é anunciada antes).
        /// </summary>
        private int ChooseGhostRoom(int exclude)
        {
            var options = new List<int>();
            for (int i = 0; i < rooms.Count; i++)
            {
                if (i == exclude || !rooms[i].IsExplorable || !rooms[i].IsDestination || rooms[i].Sealed || IsBarred(i)) continue;
                options.Add(i);
            }
            return options.Count == 0 ? -1 : options[villainRng.Range(0, options.Count - 1)];
        }

        // ---------------------------------------------------------------- Artefatos

        /// <summary>Monta a oferta "1 de N" (fluxo próprio da seed). false = nada a oferecer.</summary>
        private bool PrepareArtefatoOffer()
        {
            artefatoOffer.Clear();
            if (!Rules.artefatoOfferEnabled) return false;
            var pool = Content.Artefatos.Where(a => a != null && !owned.Contains(a)).Distinct().ToList();
            if (pool.Count == 0) return false;
            artefatoRng.Shuffle(pool);
            for (int i = 0; i < pool.Count && i < Rules.artefatoOfferCount; i++) artefatoOffer.Add(pool[i]);
            Log.Add(ActIndex + 1, "artefato-offer", string.Join(", ", artefatoOffer.Select(a => a.DisplayName)));
            return true;
        }

        internal void GiveArtefato(ArtefatoDef art, string from)
        {
            if (art == null || owned.Contains(art)) return;
            owned.Add(art);
            Report.Add(ReportKind.Artefato, $"Artefato: <b>{art.DisplayName}</b> — {art.Description}");
            Log.Add(ActIndex + 1, "artefato", $"{art.DisplayName} ({from})");
        }

        // ---------------------------------------------------------------- Relatório de encontros do Slasher

        private void ReportVillainEncounter(VillainEncounterOutcome o)
        {
            // As linhas do encontro são montadas pela apresentação (RunPresenter); aqui só a soma do relatório.
            Report.ScoreGained += o.Score;
        }
    }
}
