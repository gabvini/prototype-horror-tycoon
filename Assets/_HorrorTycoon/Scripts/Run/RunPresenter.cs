using System.Collections;
using System.Collections.Generic;
using HorrorTycoon.Actors;
using HorrorTycoon.Cameras;
using HorrorTycoon.Core;
using HorrorTycoon.Rooms;
using HorrorTycoon.Rooms.Building;
using HorrorTycoon.Rooms.Generation;
using HorrorTycoon.Scoring;
using HorrorTycoon.UI;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace HorrorTycoon.Run
{
    /// <summary>
    /// "Cola" entre a lógica (FilmRun, C# puro) e a cena 3D.
    /// Lê o mouse (selecionar ator, clicar em sala), manda a lógica resolver e então ENCENA:
    /// ator anda até a sala, close de reação, popup de resultado, telas de fim de ato e vilão.
    /// A HUD só lê o estado daqui e chama os métodos públicos.
    /// </summary>
    public class RunPresenter : MonoBehaviour
    {
        public enum Phase
        {
            Idle,          // jogador decide
            Busy,          // encenando (andando / close)
            ShowingResult, // popup do que aconteceu
            ActBreak,      // fim de ato
            VillainChoice, // escolher vilão
            Ended,
            ArtefatoChoice, // Protótipo 3: escolher 1 de 3 artefatos entre atos
            DraftChoice     // Casa por escolha: escolher a sala que nasce atrás da porta aberta
        }

        [Header("Dados")]
        [SerializeField] private GameContentDef content;
        [Tooltip("0 = seed aleatória a cada run. Outro valor = run reproduzível.")]
        [SerializeField] private int seed = 0;

        [Header("Cena")]
        [SerializeField] private NavMeshSurface navMeshSurface;
        [SerializeField] private ActorFocusController focusController;
        [SerializeField] private Camera mainCamera;
        [Tooltip("Monitor do diretor (planta vista de cima, clicável). Opcional.")]
        [SerializeField] private DirectorMonitor monitor;

        [Header("Casa gerada (opcional)")]
        [Tooltip("Vazio = planta fixa do P0 (slots da cena). Com asset: a casa é gerada pela seed e montada pelo HouseBuilder.")]
        [SerializeField] private HouseGenDef houseGen;
        [Tooltip("Monta a casa gerada na cena. Vazio = procura um na cena.")]
        [SerializeField] private HouseBuilder houseBuilder;

        [Header("Ritmo da encenação (segundos)")]
        [SerializeField] private float reactionSeconds = 1.4f;
        [SerializeField] private float resultSeconds = 2.2f;
        [Tooltip("Tempo extra do popup quando o vilão fez algo na mesma ação.")]
        [SerializeField] private float villainResultExtraSeconds = 1.5f;
        [Tooltip("Tempo máximo do vilão andando de um espaço ao outro numa batida.")]
        [SerializeField] private float villainMoveSeconds = 2.2f;
        [Tooltip("HUD nova: teto de segurança do relatório (a HUD controla o tempo e chama SkipResult).")]
        [SerializeField] private float resultSafetySeconds = 30f;

        private readonly Dictionary<ActorRunState, ActorView> views = new Dictionary<ActorRunState, ActorView>();
        private readonly Dictionary<int, RoomAnchor> anchors = new Dictionary<int, RoomAnchor>();

        private ActOutcome pendingAct;
        private IsoCameraRig rig;
        private bool runEnded;
        private float resultTimer;
        private RoomAnchor hoveredAnchor;
        private int runSeedUsed;

        // Vilão NPC: eventos da lógica guardados para encenar depois da ação, boneco e anúncio.
        private readonly List<object> villainEvents = new List<object>();
        private VillainView villainView;
        private VillainTelegraph telegraph;
        private bool villainLinesAdded;

        public FilmRun Run { get; private set; }
        public Phase CurrentPhase { get; private set; } = Phase.Idle;
        public ActorRunState Selected { get; private set; }
        public int HoveredRoom { get; private set; } = -1;

        /// <summary>Sala cujo card está aberto (-1 = nenhum).</summary>
        public int CardRoom { get; private set; } = -1;
        /// <summary>Casa por escolha: porta para o vazio cujo card está aberto (-1 = nenhuma).</summary>
        public int CardSite { get; private set; } = -1;
        /// <summary>Algum card (sala ou porta para o vazio) aberto.</summary>
        public bool HasCard => CardRoom >= 0 || CardSite >= 0;
        /// <summary>Porta para o vazio sob o mouse (-1 = nenhuma).</summary>
        public int HoveredSite { get; private set; } = -1;
        /// <summary>Escolha de sala aberta (fase DraftChoice): a oferta, a porta e quem vai abrir.</summary>
        public DraftOffer CurrentDraft { get; private set; }
        public ActorRunState DraftActor { get; private set; }
        public HouseBuilder Builder => houseBuilder;
        /// <summary>Casa por escolha: centro da sala que está se montando agora (a câmera faz o plano de grua). null = nenhuma.</summary>
        public Vector3? AssemblyPoint { get; private set; }
        /// <summary>Maior lado (m) da sala que está se montando.</summary>
        public float AssemblySize { get; private set; }
        public ActOutcome LastAct { get; private set; }
        public string ResultTitle { get; private set; } = "";
        public List<string> ResultLines { get; } = new List<string>();
        /// <summary>
        /// HUD nova: o mesmo relatório, ESTRUTURADO (um item por beat, na ordem da encenação e do tique).
        /// ResultLines continua igual para a HUD antiga (IMGUI).
        /// </summary>
        public List<ReportLine> ResultBeats { get; } = new List<ReportLine>();
        /// <summary>HUD nova ligada: ela controla o tempo do relatório; o resultTimer vira só teto de segurança.</summary>
        public bool HudControlsResultTiming { get; set; }
        public Camera MainCamera => mainCamera;

        public RoomAnchor AnchorOf(int roomIndex) => anchors.TryGetValue(roomIndex, out var a) ? a : null;

        /// <summary>Vilão NPC da run (null antes de entrar na casa).</summary>
        public VillainAgent Villain => Run?.VillainNpc;
        /// <summary>Boneco do vilão na cena (null antes de entrar).</summary>
        public VillainView VillainViewObject => villainView;
        /// <summary>Mostrar onde o vilão está (regra revealVillain). O anúncio do próximo espaço aparece sempre.</summary>
        public bool RevealVillain => content != null && content.Rules.revealVillain;
        public ActorView ViewOf(ActorRunState actor) => views.TryGetValue(actor, out var v) ? v : null;

        private void Start()
        {
            if (content == null)
            {
                Debug.LogError("[HorrorTycoon] RunPresenter sem Catálogo de Conteúdo.");
                enabled = false;
                return;
            }

            if (mainCamera == null) mainCamera = Camera.main;
            rig = FindFirstObjectByType<IsoCameraRig>();

            int runSeed = seed != 0 ? seed : System.Environment.TickCount;
            runSeedUsed = runSeed;
            Run = houseGen != null ? new FilmRun(content, runSeed, houseGen) : new FilmRun(content, runSeed);
            Run.ActEnded += act => pendingAct = act;
            Run.RunEnded += _ => runEnded = true;
            Run.VillainMoved += e => villainEvents.Add(e);
            Run.VillainEncountered += e => villainEvents.Add(e);
            Run.GhostScared += e => villainEvents.Add(e);
            Run.CrisisHappened += e => villainEvents.Add(e);
            Run.RoomAdded += OnRoomAdded;

            // Casa gerada: monta a casa ANTES de ligar os slots e de calcular o NavMesh.
            if (Run.Layout != null)
            {
                if (houseBuilder == null) houseBuilder = FindFirstObjectByType<HouseBuilder>();
                if (houseBuilder != null) houseBuilder.Build(Run.Layout);
                else Debug.LogError("[HorrorTycoon] RunPresenter com HouseGenDef mas sem HouseBuilder na cena.");
            }

            // Liga dados <-> objetos da cena.
            var sceneViews = FindObjectsByType<ActorView>(FindObjectsSortMode.None);
            foreach (var actor in Run.Actors)
            {
                foreach (var view in sceneViews)
                {
                    if (view.Def == actor.Def) views[actor] = view;
                }
            }

            // Cada slot da planta recebe o cômodo sorteado para ele nesta run (móveis, luz, névoa).
            foreach (var anchor in FindObjectsByType<RoomAnchor>(FindObjectsSortMode.None))
            {
                int slot = anchor.SlotIndex;
                if (slot < 0 || slot >= Run.Rooms.Count) continue;
                anchors[slot] = anchor;
                anchor.Bind(Run.Rooms[slot], runSeed);
                anchor.SetDiscovered(Run.Rooms[slot].Discovered);
            }

            // Malha de caminhada: calculada depois de montar a casa.
            if (navMeshSurface != null) navMeshSurface.BuildNavMesh();
            foreach (var pair in views)
            {
                // Casa gerada: os atores começam DENTRO (FilmRun já pôs o RoomIndex no espaço inicial).
                RoomAnchor home = Run.Layout != null ? AnchorOf(pair.Key.RoomIndex) : null;
                if (home != null) pair.Value.transform.position = home.NextStandPoint();

                pair.Value.GetComponent<ActorMover>()?.EnableAgent();
                var idle = pair.Value.GetComponent<ActorIdle>();
                if (idle != null)
                {
                    if (home != null) idle.SetHome(home.transform.position, Run.Rooms[pair.Key.RoomIndex].IsHub ? 0.5f : 1.0f);
                    else idle.SetHome(pair.Value.transform.position, 0.6f);
                    idle.Paused = false;
                }
            }

            Run.Begin();
            RefreshSites();
            Debug.Log($"[HorrorTycoon] Run iniciada. Seed {runSeed} (coloque no RunPresenter para repetir).");
            RefreshExpressions();
        }

        // ================================================================== Entrada (mouse/teclado)

        private void Update()
        {
            if (Run == null) return;

            if (CurrentPhase == Phase.ShowingResult)
            {
                resultTimer -= Time.deltaTime;
                if (resultTimer <= 0f) FinishResult();
                return;
            }

            UpdateHover();

            if (CurrentPhase != Phase.Idle) return;

            Mouse mouse = Mouse.current;
            Keyboard kb = Keyboard.current;
            if (mouse == null || kb == null) return;

            if (mouse.leftButton.wasPressedThisFrame && TryPointerRay(out Ray clickRay))
            {
                HandleClick(clickRay);
            }

            if (kb.fKey.wasPressedThisFrame && Selected != null && focusController != null)
            {
                var v = ViewOf(Selected);
                if (v != null) focusController.Focus(v);
            }

            if (kb.tabKey.wasPressedThisFrame) SelectNextActor();
            if (kb.escapeKey.wasPressedThisFrame) CloseRoomCard();
        }

        /// <summary>Porta para o vazio atingida pelo raio: a folha fechada ou a marcação de fita no chão.</summary>
        private int SiteFromHit(RaycastHit hit)
        {
            if (houseBuilder == null || !Run.GrowsByDraft) return -1;
            var marker = hit.collider.GetComponentInParent<DraftSiteMarker>();
            if (marker != null) return marker.SiteIndex;
            if (hit.collider.GetComponentInParent<RoomAnchor>() != null) return -1;
            return houseBuilder.SiteAt(hit.point);
        }

        /// <summary>
        /// Raio do mouse para o mundo: pelo MONITOR DO DIRETOR se o mouse estiver sobre ele,
        /// senão pela câmera principal (desde que não esteja sobre outro painel da HUD).
        /// </summary>
        private bool TryPointerRay(out Ray ray)
        {
            ray = default;
            Mouse mouse = Mouse.current;
            if (mouse == null) return false;
            Vector2 p = mouse.position.ReadValue();
            if (monitor != null && monitor.TryGetRay(p, out ray)) return true;
            if (mainCamera == null || HudInputBlocker.IsPointerOverHud(p)) return false;
            ray = mainCamera.ScreenPointToRay(p);
            return true;
        }

        private void HandleClick(Ray ray)
        {
            if (!SeeThroughTargets.Raycast(ray, out RaycastHit hit, 300f)) return; // atravessa parede dentro do buraco de visão

            var view = hit.collider.GetComponentInParent<ActorView>();
            if (view != null)
            {
                foreach (var pair in views)
                {
                    if (pair.Value == view && pair.Key.Alive) Select(pair.Key);
                }
                return;
            }

            // Casa por escolha: porta para o vazio (folha fechada ou fita no chão) abre o card da porta.
            int site = SiteFromHit(hit);
            if (site >= 0 && Run.DoorSite(site) != null && Run.DoorSite(site).IsOpen)
            {
                OpenSiteCard(site);
                return;
            }

            // Clicar numa sala ABRE O CARD da sala (o jogador decide quem vai a partir dele).
            var anchor = hit.collider.GetComponentInParent<RoomAnchor>();
            if (anchor != null)
            {
                OpenRoomCard(IndexOf(anchor));
            }
            else
            {
                CloseRoomCard();
            }
        }

        private void UpdateHover()
        {
            RoomAnchor newHover = null;
            int newSite = -1;
            if (TryPointerRay(out Ray ray))
            {
                if (SeeThroughTargets.Raycast(ray, out RaycastHit hit, 300f))
                {
                    newHover = hit.collider.GetComponentInParent<RoomAnchor>();
                    newSite = SiteFromHit(hit);
                }
            }

            if (newSite != HoveredSite)
            {
                if (HoveredSite >= 0 && HoveredSite != CardSite) houseBuilder?.SetSiteHighlight(HoveredSite, false);
                HoveredSite = newSite;
                if (HoveredSite >= 0) houseBuilder?.SetSiteHighlight(HoveredSite, true);
            }

            if (newHover == hoveredAnchor) return;
            if (hoveredAnchor != null) hoveredAnchor.SetHighlighted(false);
            hoveredAnchor = newHover;
            if (hoveredAnchor != null) hoveredAnchor.SetHighlighted(true);
            HoveredRoom = hoveredAnchor != null ? IndexOf(hoveredAnchor) : -1;
        }

        private int IndexOf(RoomAnchor anchor)
        {
            foreach (var pair in anchors)
            {
                if (pair.Value == anchor) return pair.Key;
            }
            return -1;
        }

        // ================================================================== API para a HUD

        public void OpenRoomCard(int roomIndex)
        {
            SetCardSite(-1);
            CardRoom = roomIndex;
        }

        public void CloseRoomCard()
        {
            CardRoom = -1;
            SetCardSite(-1);
        }

        /// <summary>Casa por escolha: abre o card da porta para o vazio ("Quem abre?").</summary>
        public void OpenSiteCard(int site)
        {
            CardRoom = -1;
            SetCardSite(site);
        }

        private void SetCardSite(int site)
        {
            if (CardSite == site) return;
            if (CardSite >= 0 && CardSite != HoveredSite) houseBuilder?.SetSiteHighlight(CardSite, false);
            CardSite = site;
            if (CardSite >= 0) houseBuilder?.SetSiteHighlight(CardSite, true);
        }

        /// <summary>
        /// Card da porta: o ator vai abrir. Sorteia (ou recupera) a oferta e abre a escolha de sala (fase DraftChoice).
        /// </summary>
        public void OpenDraft(ActorRunState actor, int site)
        {
            if (CurrentPhase != Phase.Idle || actor == null || !Run.CanDraft(actor, site)) return;
            var offer = Run.OpenDraft(site);
            if (offer == null || offer.Options.Count == 0) return;
            Select(actor);
            DraftActor = actor;
            CurrentDraft = offer;
            CurrentPhase = Phase.DraftChoice;
        }

        /// <summary>Escolha de sala: a opção 'option' nasce atrás da porta e o ator entra. -1 = desistir (volta ao card).</summary>
        public void ChooseDraft(int option)
        {
            if (CurrentPhase != Phase.DraftChoice || CurrentDraft == null) return;
            var actor = DraftActor;
            int site = CurrentDraft.Site;
            CurrentDraft = null;
            DraftActor = null;
            CurrentPhase = Phase.Idle;
            if (option < 0 || actor == null || !Run.CanDraft(actor, site)) return;
            CloseRoomCard();
            StartCoroutine(DraftSequence(actor, site, option));
        }

        /// <summary>Fita apagada nas portas onde nenhuma sala cabe mais.</summary>
        private void RefreshSites()
        {
            if (houseBuilder == null || !Run.GrowsByDraft) return;
            for (int i = 0; i < Run.DoorSites.Count; i++)
            {
                if (Run.DoorSites[i].IsOpen) houseBuilder.SetSiteAvailable(i, Run.SiteHasRoom(i));
            }
        }

        /// <summary>A lógica pôs uma sala nova na planta: monta na cena (a animação roda no DraftSequence).</summary>
        private void OnRoomAdded(int index)
        {
            if (houseBuilder == null) return;
            var anchor = houseBuilder.AddRoom(index);
            if (anchor == null) return;
            anchors[index] = anchor;
            anchor.Bind(Run.Rooms[index], runSeedUsed);
            anchor.SetDiscovered(true);
            if (navMeshSurface != null) navMeshSurface.BuildNavMesh();
        }

        /// <summary>Botão "Ir" do card: manda o ator para a sala.</summary>
        public void SendActor(ActorRunState actor, int roomIndex)
        {
            if (CurrentPhase != Phase.Idle || actor == null || !Run.CanMove(actor, roomIndex)) return;
            Select(actor);
            CloseRoomCard();
            StartCoroutine(MoveSequence(actor, roomIndex));
        }

        public void Select(ActorRunState actor)
        {
            if (Selected != null && ViewOf(Selected) != null) ViewOf(Selected).SetHighlighted(false);
            Selected = actor != null && actor.Alive ? actor : null;
            if (Selected != null && ViewOf(Selected) != null)
            {
                ViewOf(Selected).SetHighlighted(true);
                if (rig != null) rig.Follow(ViewOf(Selected).transform); // câmera acompanha o ator
            }
        }

        public void SelectNextActor()
        {
            var alive = new List<ActorRunState>();
            foreach (var a in Run.Actors) if (a.Alive) alive.Add(a);
            if (alive.Count == 0) return;
            int i = Selected == null ? -1 : alive.IndexOf(Selected);
            Select(alive[(i + 1) % alive.Count]);
        }

        public void DirectScene(PayoffDef payoff)
        {
            if (CurrentPhase != Phase.Idle || Selected == null) return;
            StartCoroutine(DirectSequence(Selected, payoff));
        }

        public void UseTool()
        {
            if (CurrentPhase != Phase.Idle || Selected == null || !Run.CanUseTool(Selected)) return;
            StartCoroutine(ToolSequence(Selected));
        }

        public void EndActEarly()
        {
            if (CurrentPhase != Phase.Idle) return;
            Run.EndActEarly();
            AfterSequence();
        }

        public void SkipResult()
        {
            if (CurrentPhase == Phase.ShowingResult) FinishResult();
        }

        public void ContinueAfterAct()
        {
            if (CurrentPhase != Phase.ActBreak) return;
            pendingAct = null;
            if (runEnded) CurrentPhase = Phase.Ended;
            else if (Run.AwaitingArtefatoChoice) CurrentPhase = Phase.ArtefatoChoice;
            else if (Run.AwaitingVillainChoice) CurrentPhase = Phase.VillainChoice;
            else CurrentPhase = Phase.Idle;
        }

        /// <summary>Protótipo 3: escolha "1 de 3" entre atos (null = pular). Depois: vilão (fim do Ato 1) ou o próximo ato.</summary>
        public void ChooseArtefato(ArtefatoDef artefato)
        {
            if (CurrentPhase != Phase.ArtefatoChoice) return;
            Run.ChooseArtefato(artefato);
            if (Run.AwaitingVillainChoice)
            {
                CurrentPhase = Phase.VillainChoice;
                return;
            }
            CurrentPhase = Phase.Idle;
            StartCoroutine(VillainAfterChoice());
        }

        /// <summary>Protótipo 3: "Gravar cena aqui" — ninguém se mexe, a casa inteira conta 1 cena.</summary>
        public void RecordScene()
        {
            if (CurrentPhase != Phase.Idle || !Run.CanRecordScene) return;
            CloseRoomCard();
            StartCoroutine(RecordSequence());
        }

        /// <summary>Protótipo 3: o Atleta selecionado segura a porta (grátis, 1× por ato).</summary>
        public void HoldDoor()
        {
            if (CurrentPhase != Phase.Idle || Selected == null || !Run.CanHoldDoor(Selected)) return;
            BeginBusy();
            Run.HoldDoor(Selected);
            ResultTitle = $"{Selected.Def.DisplayName} segura a porta!";
            ResultLines.Clear();
            ResultBeats.Clear();
            AppendReport(false);
            var view = ViewOf(Selected);
            if (view != null) view.SetExpression(ActorExpression.Tense);
            UpdateTelegraph();
            FinishSequence();
        }

        public void ChooseVillain(VillainDef villain)
        {
            if (CurrentPhase != Phase.VillainChoice) return;
            Run.ChooseVillain(villain);
            CurrentPhase = Phase.Idle;
            StartCoroutine(VillainAfterChoice());
        }

        /// <summary>O vilão entra na casa: aparece direto no espaço sorteado (sem animação de caminhada).</summary>
        private IEnumerator VillainAfterChoice()
        {
            CurrentPhase = Phase.Busy;
            yield return PlayVillainEvents();
            CurrentPhase = runEnded ? Phase.Ended : Phase.Idle;
        }

        public void Restart() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

        // ================================================================== Encenação

        /// <summary>
        /// Casa por escolha: o ator abre a porta, a sala escolhida nasce (OnRoomAdded monta), a câmera olha a sala
        /// se montando e o ator entra (o resto é a encenação normal de uma exploração).
        /// </summary>
        private IEnumerator DraftSequence(ActorRunState actor, int site, int option)
        {
            BeginBusy();
            MoveOutcome outcome = Run.Draft(actor, site, option);
            int roomIndex = outcome.To;
            var anchor = AnchorOf(roomIndex);
            if (anchor != null && houseBuilder != null)
            {
                AssemblySize = Mathf.Max(anchor.Size.x, anchor.Size.y);
                AssemblyPoint = anchor.transform.position;
                yield return houseBuilder.Assemble(roomIndex);
                AssemblyPoint = null;
            }
            RefreshSites();
            yield return MoveSequence(actor, roomIndex, outcome);
        }

        private IEnumerator MoveSequence(ActorRunState actor, int roomIndex) => MoveSequence(actor, roomIndex, null);

        /// <param name="done">Movimento já resolvido pela lógica (abrir porta na casa por escolha). null = resolve aqui.</param>
        private IEnumerator MoveSequence(ActorRunState actor, int roomIndex, MoveOutcome done)
        {
            if (done == null) BeginBusy();
            MoveOutcome outcome = done ?? Run.Move(actor, roomIndex);
            var view = ViewOf(actor);
            var anchor = AnchorOf(roomIndex);

            if (anchor != null && done == null) anchor.SetDiscovered(true); // a névoa sai quando alguém entra
            if (view != null && rig != null) rig.Follow(view.transform);
            var idle = view != null ? view.GetComponent<ActorIdle>() : null;
            if (idle != null && anchor != null)
            {
                bool hub = Run.Rooms[roomIndex].IsHub;
                idle.SetHome(anchor.transform.position, hub ? 0.5f : 1.0f);
            }

            if (view != null && anchor != null)
            {
                var mover = view.GetComponent<ActorMover>();
                if (mover != null)
                {
                    yield return mover.MoveTo(anchor.NextStandPoint());
                    mover.FaceTowards(view.transform.position + new Vector3(-1f, 0f, -1f));
                }
            }

            ResultLines.Clear();
            ResultBeats.Clear();
            ResultTitle = "";
            if (outcome.Encounter != null)
            {
                ResultTitle = $"{actor.Def.DisplayName} — {outcome.Encounter.DisplayName}";
                if (!string.IsNullOrEmpty(outcome.Encounter.Description)) ResultLines.Add(outcome.Encounter.Description);
                if (outcome.Encounter.Points > 0) ResultLines.Add($"+{outcome.Encounter.Points} pontos");
                if (outcome.ElementGained != null)
                    ResultLines.Add($"Elemento: <b>{outcome.ElementGained.DisplayName}</b> ({(outcome.ElementGained.Holder == ElementHolder.Actor ? "no ator" : "na sala")})");
                int encPts = outcome.Points > 0 ? outcome.Points : outcome.Encounter.Points;
                Beat(encPts > 0 ? ReportKind.Score : ReportKind.Info, $"<b>{outcome.Encounter.DisplayName}</b>", encPts);
                if (outcome.ElementGained != null) Beat(ReportKind.Info, $"Elemento: <b>{outcome.ElementGained.DisplayName}</b> ({(outcome.ElementGained.Holder == ElementHolder.Actor ? "com o ator" : "na sala")})");
            }
            if (outcome.ToolGained != null) ResultLines.Add($"Ferramenta: <b>{outcome.ToolGained.DisplayName}</b> (com {actor.Def.DisplayName})");
            if (outcome.ToolLeftOnFloor != null)
                ResultLines.Add($"Ferramenta: <b>{outcome.ToolLeftOnFloor.DisplayName}</b> — {actor.Def.DisplayName} está de mãos cheias; ficou no chão.");
            foreach (var t in outcome.ToolsPickedUp) ResultLines.Add($"{actor.Def.DisplayName} pegou do chão: <b>{t.DisplayName}</b>");
            if (outcome.ToolGained != null) Beat(ReportKind.Info, $"Ferramenta: <b>{outcome.ToolGained.DisplayName}</b> (com {actor.Def.DisplayName})");
            if (outcome.ToolLeftOnFloor != null) Beat(ReportKind.Info, $"<b>{outcome.ToolLeftOnFloor.DisplayName}</b> ficou no chão (mãos cheias)");
            foreach (var t in outcome.ToolsPickedUp) Beat(ReportKind.Info, $"{actor.Def.DisplayName} pegou: <b>{t.DisplayName}</b>");
            // (Crise/saída por pavor vem no relatório da cena.)
            if (ResultLines.Count > 0 && ResultTitle == "") ResultTitle = $"{actor.Def.DisplayName} — {Run.Rooms[roomIndex].Def.DisplayName}";

            if (outcome.Encounter != null && view != null)
            {
                view.SetExpression(outcome.Encounter.PavorDelta < 0 ? ActorExpression.Happy : ExpressionFor(actor.Pavor));
                focusController?.Focus(view);
                yield return new WaitForSeconds(reactionSeconds);
                if (outcome.ActorBroke) view.SetDead();
            }

            yield return PlayVillainEvents();
            AppendReport(outcome.Cost > 0);
            if (ResultLines.Count > 0 && ResultTitle == "") ResultTitle = $"{actor.Def.DisplayName} — {Run.Rooms[roomIndex].Def.DisplayName}";
            FinishSequence();
        }

        private IEnumerator RecordSequence()
        {
            BeginBusy();
            var sel = Selected;
            Run.RecordScene();
            var room = sel != null && sel.Alive ? Run.RoomOf(sel) : null;
            ResultTitle = room != null ? $"Cena gravada — {room.Def.DisplayName}" : "Cena gravada";
            ResultLines.Clear();
            ResultBeats.Clear();
            if (sel != null && ViewOf(sel) != null) focusController?.Focus(ViewOf(sel));
            yield return new WaitForSeconds(reactionSeconds * 0.5f);
            yield return PlayVillainEvents();
            AppendReport(true);
            if (ResultLines.Count == 0) ResultLines.Add("<i>O elenco segura a posição. Nada de mais aconteceu.</i>");
            if (ResultBeats.Count == 0) Beat(ReportKind.Info, "O elenco segura a posição. Nada de mais.");
            FinishSequence();
        }

        /// <summary>
        /// Protótipo 3: acrescenta ao popup o RELATÓRIO DA CENA (o que o tique da casa fez), curto e colorido por tipo.
        /// header = mostra o título "Relatório da cena" (só quando gastou cena).
        /// </summary>
        private void AppendReport(bool header)
        {
            var rep = Run.Report;
            if (rep == null || rep.IsEmpty) return;
            if (ResultLines.Count > 0) ResultLines.Add("");
            if (header) ResultLines.Add($"<b><color=#d9c27a>— Relatório da cena{(rep.Scenes > 1 ? $" ({rep.Scenes} cenas)" : "")} —</color></b>");
            foreach (var l in rep.Lines)
            {
                string c = ReportColor(l.Kind);
                string pts = l.Score > 0 ? $"  <b>+{l.Score}</b>" : "";
                ResultLines.Add($"<color={c}>{l.Text}</color>{pts}");
                ResultBeats.Add(l);
            }
        }

        /// <summary>HUD nova: acrescenta um beat estruturado (a HUD antiga usa só ResultLines).</summary>
        private void Beat(ReportKind kind, string text, int score = 0) =>
            ResultBeats.Add(new ReportLine { Kind = kind, Text = text, Score = score });

        public static string ReportColor(ReportKind k)
        {
            switch (k)
            {
                case ReportKind.PlotDone: return "#ffd966";
                case ReportKind.Plot: return "#e8d9a8";
                case ReportKind.PlotBroken: return "#ff9a6a";
                case ReportKind.Combo: return "#ff9ad5";
                case ReportKind.Pavor: return "#b0b0b0";
                case ReportKind.Crisis: return "#ff6666";
                case ReportKind.Villain: return "#ff5a4a";
                case ReportKind.Ghost: return "#b9a8ff";
                case ReportKind.Unlock: return "#8fdc8f";
                case ReportKind.Artefato: return "#f2cc73";
                case ReportKind.Score: return "#ffe9a0";
                default: return "#e0e0e0";
            }
        }

        /// <summary>Fim de toda encenação: popup se houver o que contar, senão volta ao jogador.</summary>
        private void FinishSequence()
        {
            if (ResultLines.Count > 0) ShowResult();
            else AfterSequence();
        }

        private IEnumerator DirectSequence(ActorRunState actor, PayoffDef payoff)
        {
            BeginBusy();
            PayoffOutcome outcome = Run.Direct(actor, payoff);
            var view = ViewOf(actor);

            ResultTitle = $"{payoff.DisplayName}! {actor.Def.DisplayName} em {outcome.Room.Def.DisplayName}";
            ResultLines.Clear();
            ResultBeats.Clear();
            if (outcome.Counted.Count == 0) ResultLines.Add("<i>Nenhum elemento preparado.</i>");
            foreach (var c in outcome.Counted)
            {
                ResultLines.Add($"• {c.Element.DisplayName}  +{c.Weight}{(c.BoostedByVillain ? "  (vilão ×)" : "")}");
            }
            ResultLines.Add($"<size=20><b>+{outcome.Score}</b></size>");
            if (outcome.Killed) ResultLines.Add($"<color=#ff6666>{actor.Def.DisplayName} saiu do filme.</color>");
            Beat(ReportKind.Info, outcome.Counted.Count == 0 ? "Nenhum elemento preparado." : DescribeCounted(outcome.Counted));
            Beat(outcome.Killed ? ReportKind.Crisis : ReportKind.Score, $"<b>{payoff.DisplayName.ToUpperInvariant()}!</b> {actor.Def.DisplayName}", outcome.Score);
            if (outcome.Killed) Beat(ReportKind.Crisis, $"{actor.Def.DisplayName} sai do filme.");

            if (view != null)
            {
                view.SetExpression(ActorExpression.Scared);
                focusController?.Focus(view);
                yield return new WaitForSeconds(reactionSeconds);
                if (outcome.Killed || outcome.ActorBroke) view.SetDead();
            }

            if (!actor.Alive) Select(null);
            yield return PlayVillainEvents();
            AppendReport(true);
            FinishSequence();
        }

        /// <summary>"Faca + Isolado + Apavorado" (com ★ quando o vilão aumenta o peso).</summary>
        private static string DescribeCounted(List<CountedElement> counted)
        {
            var names = new List<string>();
            foreach (var c in counted) names.Add($"<color=#{ColorUtility.ToHtmlStringRGB(c.Element.Color)}>{c.Element.DisplayName}</color>");
            return string.Join(" + ", names);
        }

        private IEnumerator ToolSequence(ActorRunState actor)
        {
            BeginBusy();
            ToolOutcome outcome = Run.UseTool(actor);
            var view = ViewOf(actor);

            ResultTitle = $"{actor.Def.DisplayName} usa {outcome.Tool.DisplayName}";
            ResultLines.Clear();
            ResultBeats.Clear();
            if (outcome.Holder != null && outcome.Holder != actor) ResultLines.Add($"<color=#a0a0a0>(emprestada por {outcome.Holder.Def.DisplayName})</color>");
            if (!string.IsNullOrEmpty(outcome.Room.Def.Locked.text)) ResultLines.Add(outcome.Room.Def.Locked.text);
            if (outcome.Reward != null) ResultLines.Add($"Elemento: <b>{outcome.Reward.DisplayName}</b>");
            Beat(ReportKind.Unlock, $"<b>{outcome.Room.Def.Locked.name}</b> aberto com {outcome.Tool.DisplayName}");
            if (outcome.Reward != null) Beat(ReportKind.Info, $"Elemento: <b>{outcome.Reward.DisplayName}</b>");

            if (view != null)
            {
                view.SetExpression(ActorExpression.Tense);
                focusController?.Focus(view);
                yield return new WaitForSeconds(reactionSeconds);
            }
            yield return PlayVillainEvents();
            AppendReport(true);
            FinishSequence();
        }

        // ================================================================== Vilão NPC (encenação)

        /// <summary>
        /// Encena, na ordem, o que o vilão fez durante a última ação (andar, encontro, sumir/recuar)
        /// e acrescenta as linhas no popup de resultado.
        /// </summary>
        private IEnumerator PlayVillainEvents()
        {
            if (villainEvents.Count == 0)
            {
                UpdateTelegraph();
                yield break;
            }

            var list = new List<object>(villainEvents);
            villainEvents.Clear();
            if (telegraph != null) telegraph.Hide(); // durante a encenação o anúncio antigo some

            foreach (var ev in list)
            {
                if (ev is VillainMoveEvent move) yield return AnimateVillainMove(move);
                else if (ev is VillainEncounterOutcome enc) yield return StageVillainEncounter(enc);
                else if (ev is GhostScareOutcome scare) yield return StageGhostScare(scare);
                else if (ev is CrisisEvent crisis) StageCrisis(crisis);
            }
            RefreshExpressions();
            UpdateTelegraph();
        }

        private void EnsureVillainObjects()
        {
            if (Run.Villain == null) return;
            if (villainView == null) villainView = VillainView.Create(Run.Villain);
            if (telegraph == null) telegraph = VillainTelegraph.Create();
            villainView.SetVisible(RevealVillain);
        }

        private IEnumerator AnimateVillainMove(VillainMoveEvent move)
        {
            EnsureVillainObjects();
            if (villainView == null) yield break;
            var anchor = AnchorOf(move.To);
            Vector3 point = anchor != null ? anchor.NextStandPoint() : villainView.transform.position;

            if (move.Reason == VillainMoveReason.Spawn || move.Reason == VillainMoveReason.Teleport
                || move.Reason == VillainMoveReason.Haunt || !RevealVillain)
            {
                villainView.Warp(point);
                if (anchor != null) villainView.FaceTowards(anchor.DoorPoint);
                yield break;
            }

            // Anda pela porta (NavMesh). Câmera acompanha de leve se o monitor não estiver aberto.
            yield return villainView.MoveTo(point, villainMoveSeconds);
        }

        private IEnumerator StageVillainEncounter(VillainEncounterOutcome enc)
        {
            EnsureVillainObjects();
            string vName = Run.Villain != null ? $"<color=#{ColorUtility.ToHtmlStringRGB(Run.Villain.Color)}><b>{Run.Villain.DisplayName}</b></color>" : "O vilão";
            string names = string.Join(", ", enc.Actors.ConvertAll(a => a.Def.DisplayName));
            string place = Run.Rooms[enc.Space].Def.DisplayName;

            if (ResultLines.Count > 0) ResultLines.Add("");
            if (string.IsNullOrEmpty(ResultTitle) || ResultLines.Count == 0)
            {
                ResultTitle = enc.Kind == VillainEncounterKind.Repelled ? "O vilão foi repelido!"
                    : enc.Kind == VillainEncounterKind.GroupScare ? "O vilão ataca o grupo!"
                    : $"O vilão pegou {enc.Victim.Def.DisplayName}!";
            }
            villainLinesAdded = true;

            ResultLines.Add(enc.ActorWalkedIn
                ? $"<b>— {names} deu de cara com {vName} em {place} —</b>"
                : $"<b>— {vName} chega em {place} —</b>");

            string vPlain = Run.Villain != null ? Run.Villain.DisplayName : "O vilão";
            Beat(ReportKind.Villain, enc.ActorWalkedIn ? $"{names} dá de cara com <b>{vPlain}</b>" : $"<b>{vPlain}</b> chega em {place}");
            switch (enc.Kind)
            {
                case VillainEncounterKind.Repelled:
                    Beat(ReportKind.Villain, $"{enc.ToolHolder.Def.DisplayName} usa <b>{enc.ToolUsed.DisplayName}</b>: o vilão some!", enc.Score);
                    ResultLines.Add($"{enc.ToolHolder.Def.DisplayName} usa: <b>{enc.ToolUsed.DisplayName}</b>! O vilão some e fica parado {enc.StunBeats} batida(s).");
                    ResultLines.Add($"Sobreviveram: <b>+{enc.Score}</b>  <color=#a0a0a0>(+{enc.PavorDelta} de pavor; ferramenta gasta: {enc.ToolUsed.DisplayName})</color>");
                    break;
                case VillainEncounterKind.GroupScare:
                    Beat(ReportKind.Villain, enc.Blocked ? "Em grupo, ninguém se assusta. O vilão recua." : "Juntos, resistem. O vilão recua.");
                    ResultLines.Add(enc.Blocked
                        ? $"Em grupo, {names} encaram o vilão. <color=#8fdc8f>Ninguém se assusta.</color>"
                        : $"Juntos, {names} resistem. <color=#ff9a9a>Todos se assustam (pavor sobe).</color>");
                    ResultLines.Add($"<color=#a0a0a0>O vilão recua e fica parado {enc.StunBeats} batida(s).</color>");
                    break;
                case VillainEncounterKind.Caught:
                    ResultLines.Add($"<color=#ff6666>{enc.Victim.Def.DisplayName} estava sozinho(a). Pego!</color>");
                    Beat(ReportKind.Villain, $"<b>{enc.Victim.Def.DisplayName}</b> estava sozinho(a). Pego!", enc.Payoff != null ? enc.Score : 0);
                    Beat(ReportKind.Crisis, $"{enc.Victim.Def.DisplayName} sai do filme.");
                    if (enc.Payoff != null)
                    {
                        ResultLines.Add($"Cena disparada: <b>{enc.Payoff.Payoff.DisplayName}</b>");
                        if (enc.Payoff.Counted.Count == 0) ResultLines.Add("<i>Nenhum elemento preparado.</i>");
                        foreach (var c in enc.Payoff.Counted)
                            ResultLines.Add($"• {c.Element.DisplayName}  +{c.Weight}{(c.BoostedByVillain ? "  (vilão ×)" : "")}");
                        ResultLines.Add($"<size=20><b>+{enc.Score}</b></size>");
                    }
                    ResultLines.Add($"<color=#ff6666>{enc.Victim.Def.DisplayName} saiu do filme.</color>");
                    break;
            }

            // Reação: close em quem estava lá; o vilão olha para a vítima.
            ActorView focus = null;
            foreach (var a in enc.Actors)
            {
                var v = ViewOf(a);
                if (v == null) continue;
                if (focus == null || a == enc.Victim) focus = v;
                v.SetExpression(ActorExpression.Scared);
                var idle = v.GetComponent<ActorIdle>();
                if (idle != null) idle.Paused = true;
            }
            if (focus != null)
            {
                if (villainView != null) villainView.FaceTowards(focus.transform.position);
                focusController?.Focus(focus);
                yield return new WaitForSeconds(reactionSeconds);
            }
            if (enc.Victim != null) ViewOf(enc.Victim)?.SetDead();
            foreach (var b in enc.Broke) ViewOf(b)?.SetDead();
            if (Selected != null && !Selected.Alive) Select(null);
        }

        /// <summary>Grande Susto (ou exorcismo) do fantasma: close em quem estava na sala; o fantasma olha para eles.</summary>
        private IEnumerator StageGhostScare(GhostScareOutcome o)
        {
            EnsureVillainObjects();
            villainLinesAdded = true;
            if (string.IsNullOrEmpty(ResultTitle) || ResultLines.Count == 0)
                ResultTitle = o.Exorcised ? "O fantasma foi expulso!" : "GRANDE SUSTO!";
            ActorView focus = null;
            foreach (var a in o.Actors)
            {
                var v = ViewOf(a);
                if (v == null) continue;
                focus ??= v;
                v.SetExpression(o.Exorcised ? ActorExpression.Tense : ActorExpression.Scared);
                var idle = v.GetComponent<ActorIdle>();
                if (idle != null) idle.Paused = true;
            }
            if (focus != null)
            {
                if (villainView != null) villainView.FaceTowards(focus.transform.position);
                focusController?.Focus(focus);
                yield return new WaitForSeconds(reactionSeconds);
            }
            foreach (var a in o.Actors) if (!a.Alive) ViewOf(a)?.SetDead();
        }

        /// <summary>Crise de pavor: cara de pânico; quem saiu do filme cai.</summary>
        private void StageCrisis(CrisisEvent c)
        {
            var v = ViewOf(c.Actor);
            if (v == null) return;
            if (c.LeftFilm) v.SetDead();
            else v.SetExpression(ActorExpression.Scared);
            villainLinesAdded = true; // mais tempo de leitura no popup
        }

        /// <summary>Atualiza as marcas do anúncio (próximo espaço do vilão) e a visibilidade do boneco.</summary>
        public void UpdateTelegraph()
        {
            var v = Villain;
            if (v == null || !v.InHouse)
            {
                if (telegraph != null) telegraph.Hide();
                return;
            }
            EnsureVillainObjects();
            if (!v.IsTelegraphing || Run.Status != RunStatus.Playing)
            {
                telegraph.Hide();
                return;
            }

            var next = AnchorOf(v.NextSpace);
            var cur = AnchorOf(v.Space);
            if (next == null)
            {
                telegraph.Hide();
                return;
            }
            if (v.IsGhost)
            {
                // Fantasma: só a área da PRÓXIMA sala assombrada (sem pegadas: ele não anda, reaparece).
                telegraph.SetTint(new Color(0.65f, 0.5f, 1f));
                telegraph.ShowArea(next.transform.position, Mathf.Min(next.Size.x, next.Size.y));
                return;
            }
            telegraph.SetTint(new Color(0.95f, 0.12f, 0.1f));
            Vector3 from = cur != null ? cur.transform.position : next.transform.position;
            Vector3 door = DoorBetween(v.Space, v.NextSpace);
            float size = Mathf.Min(next.Size.x, next.Size.y);
            telegraph.Show(next.transform.position, size, door, next.transform.position - from);
        }

        /// <summary>
        /// Ponto (no chão) da passagem entre dois espaços vizinhos.
        /// Casa gerada: a ligação do HouseLayout. Planta fixa (estrela): a porta do cômodo para o corredor.
        /// </summary>
        public Vector3 DoorBetween(int a, int b)
        {
            if (Run.Layout != null && houseBuilder != null && houseBuilder.Map != null)
            {
                foreach (var c in Run.Layout.Connections)
                {
                    if ((c.A == a && c.B == b) || (c.A == b && c.B == a)) return houseBuilder.Map.ToWorld(c.Position);
                }
            }
            var ra = AnchorOf(a);
            var rb = AnchorOf(b);
            if (Run.Layout == null)
            {
                if (b == Run.Map.Hub && ra != null) return ra.DoorPoint;
                if (a == Run.Map.Hub && rb != null) return rb.DoorPoint;
            }
            if (ra != null && rb != null) return (ra.transform.position + rb.transform.position) * 0.5f;
            return rb != null ? rb.transform.position : (ra != null ? ra.transform.position : Vector3.zero);
        }

        private void BeginBusy()
        {
            CurrentPhase = Phase.Busy;
            villainLinesAdded = false;
            SetIdlePaused(Selected, true); // o ator que vai agir para de "fazer hora"
            if (focusController != null)
            {
                focusController.Unfocus();
                focusController.AllowPlayerInput = false;
            }
        }

        private void ShowResult()
        {
            CurrentPhase = Phase.ShowingResult;
            resultTimer = HudControlsResultTiming ? resultSafetySeconds : resultSeconds + (villainLinesAdded ? villainResultExtraSeconds : 0f);
            villainLinesAdded = false;
        }

        private void FinishResult()
        {
            if (focusController != null) focusController.Unfocus();
            AfterSequence();
        }

        private void AfterSequence()
        {
            UpdateTelegraph();
            if (focusController != null) focusController.AllowPlayerInput = true;
            foreach (var a in Run.Actors) SetIdlePaused(a, !a.Alive);
            if (Selected != null && !Selected.Alive) Select(null);
            RefreshExpressions();

            if (pendingAct != null)
            {
                LastAct = pendingAct;
                CurrentPhase = Phase.ActBreak;
            }
            else
            {
                CurrentPhase = runEnded ? Phase.Ended : Phase.Idle;
            }
        }

        private void SetIdlePaused(ActorRunState actor, bool paused)
        {
            var view = actor != null ? ViewOf(actor) : null;
            var idle = view != null ? view.GetComponent<ActorIdle>() : null;
            if (idle != null) idle.Paused = paused;
        }

        private ActorExpression ExpressionFor(int pavor)
        {
            var rules = content.Rules;
            if (pavor >= rules.scaredThreshold) return ActorExpression.Scared;
            if (pavor >= rules.tenseThreshold) return ActorExpression.Tense;
            return ActorExpression.Neutral;
        }

        private void RefreshExpressions()
        {
            foreach (var pair in views)
            {
                if (pair.Key.Alive) pair.Value.SetExpression(ExpressionFor(pair.Key.Pavor));
            }
        }
    }
}
