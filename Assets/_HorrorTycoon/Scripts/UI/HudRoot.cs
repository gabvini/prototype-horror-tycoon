using System.Collections.Generic;
using HorrorTycoon.Cameras;
using HorrorTycoon.Run;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace HorrorTycoon.UI
{
    /// <summary>
    /// HUD NOVA (UI Toolkit, runtime). Monta a árvore em C# dentro de um UIDocument, aplica a escala
    /// (HudScaleMath: clamp(altura/1080, 0,75, 2) × opção do jogador) e decide o que aparece em cada momento:
    ///
    ///   SEMPRE   claquete (Z1), chip do vilão (Z2), roteiro (Z3), elenco (Z4), gravar/monitor/menu (Z6)
    ///   CONTEXTO card da sala (Z7), barra de ações do ator (Z5), monitor (Z9)
    ///   HOVER    tooltips
    ///   SEQUÊNCIA legendas do relatório (Z8) — só claquete + elenco + legendas ficam
    ///   FASE     modais (fim de rolo, artefato, vilão, estreia) — a HUD inteira some
    ///   FILME    idle cinematográfico: faixas, cantos de visor, ● REC; o resto some
    ///
    /// Só LÊ o RunPresenter/FilmRun e chama os métodos públicos. A HUD antiga (RunHud, IMGUI) continua
    /// no projeto: F1 alterna entre as duas (comparação até o playtest).
    /// Doc: Docs/Tecnico/HUD_UIToolkit.md
    /// </summary>
    public class HudRoot : MonoBehaviour, IHudActions
    {
        [Header("Referências")]
        [SerializeField] private RunPresenter presenter;
        [SerializeField] private CinematicDirector director;
        [SerializeField] private DirectorMonitor monitor;
        [Tooltip("HUD antiga (IMGUI). F1 alterna. Vazio = sem alternância.")]
        [SerializeField] private RunHud legacyHud;

        [Header("UI Toolkit")]
        [Tooltip("Asset de PanelSettings (Scale With Screen Size, 1920×1080, match altura). Uma cópia é usada em Play.")]
        [SerializeField] private PanelSettings panelSettings;
        [Tooltip("Folhas de estilo: HudTheme.uss (tokens) e Hud.uss (componentes).")]
        [SerializeField] private StyleSheet[] styleSheets;

        [Header("Escala")]
        [Tooltip("Aplica o piso de 0,75 e a opção de tamanho do jogador (§4.1). Desligado = só o Scale With Screen Size do asset.")]
        [SerializeField] private bool clampScale = true;

        [Header("Ritmo")]
        [SerializeField] private BeatTiming timing = new BeatTiming();
        [Tooltip("Segundos sem nenhum input até o modo filme (com o diretor em planos de cinema).")]
        [SerializeField] private float filmIdleSeconds = 7f;

        public enum HdrFix { Auto, Ligado, Desligado }

        [Header("Cor (URP + HDR)")]
        [Tooltip("Com projeto em Linear e câmera HDR, o URP 17 desenha o UI Toolkit lendo as cores do USS como lineares " +
                 "(tons escuros ficam bem mais claros: #15121A vira ~#51495A). Auto/Ligado: o painel é desenhado numa " +
                 "RenderTexture sRGB e composto na tela por IMGUI (cores certas). Desligado: direto na tela.")]
        [SerializeField] private HdrFix hdrColorFix = HdrFix.Auto;
        [Tooltip("Shader que compõe a textura do painel na tela (Art/UI/HudComposite.shader).")]
        [SerializeField] private Shader compositeShader;

        [Header("Comparação")]
        [Tooltip("Começa com a HUD antiga (IMGUI).")]
        [SerializeField] private bool startWithLegacy;

        private UIDocument document;
        private bool useTexture;
        private RenderTexture panelTexture;   // onde o painel desenha (Linear: guarda os bytes como o painel escreveu)
        private Material compositeMaterial;
        private static readonly int LinearizeId = Shader.PropertyToID("_Linearize");
        /// <summary>Diagnóstico: o último desenho da HUD converteu para linear (destino sRGB com escrita sRGB)?</summary>
        public bool CompositeLinearized { get; private set; }
        /// <summary>Diagnóstico: destino do último desenho da HUD.</summary>
        public string CompositeTarget { get; private set; } = "";
        private PanelSettings runtimeSettings;
        private readonly HudContext ctx = new HudContext();
        private bool built;
        private bool legacy;

        private ClaquetePanel claquete;
        private VillainChip villainChip;
        private ToastStack toasts;
        private ScriptTracker script;
        private CastStrip cast;
        private ActionBar actionBar;
        private RecordCluster record;
        private RoomCardPanel roomCard;
        private SubtitleReport subtitles;
        private MonitorPanel monitorPanel;
        private FilmFrame filmFrame;
        private WorldMarkers world;
        private ModalHost modals;
        private Label hint;

        private RunPresenter.Phase lastPhase = (RunPresenter.Phase)(-1);
        private int frozenScore;
        private float lastInputTime;
        private bool hintDismissed;
        private int lastAct = -1;
        private bool villainAnnounced;
        private int goalToastAct = -1;
        private readonly List<SceneLogEntry> history = new List<SceneLogEntry>();

        public bool LegacyActive => legacy;

        // ================================================================== Ciclo de vida

        private void Awake()
        {
            ctx.Settings = new HudSettings();
            ctx.Settings.Load();
            ctx.Timing = timing;
            ctx.Tweens = new HudTweens();
            if (presenter == null) presenter = FindFirstObjectByType<RunPresenter>();
            if (director == null) director = FindFirstObjectByType<CinematicDirector>();
            if (monitor == null) monitor = FindFirstObjectByType<DirectorMonitor>();
            if (legacyHud == null) legacyHud = GetComponent<RunHud>();
            ctx.Presenter = presenter;
            ctx.Director = director;
            ctx.Monitor = monitor;
            ctx.Focus = FindFirstObjectByType<ActorFocusController>();

            if (panelSettings == null)
            {
                Debug.LogWarning("[HorrorTycoon] HudRoot sem PanelSettings: usando a HUD antiga.");
                startWithLegacy = true;
            }
            else
            {
                runtimeSettings = Instantiate(panelSettings);
                runtimeSettings.name = panelSettings.name + " (Play)";
                // Objeto filho criado desligado: o UIDocument já nasce com o PanelSettings.
                var go = new GameObject("HUD (UI Toolkit)");
                go.SetActive(false);
                go.transform.SetParent(transform, false);
                document = go.AddComponent<UIDocument>();
                useTexture = NeedsColorFix();
                if (useTexture)
                {
                    if (compositeShader == null) compositeShader = Shader.Find("Hidden/HorrorTycoon/HudComposite");
                    if (compositeShader != null && compositeShader.isSupported) compositeMaterial = new Material(compositeShader) { hideFlags = HideFlags.HideAndDontSave };
                    else useTexture = false;
                }
                if (useTexture)
                {
                    runtimeSettings.clearColor = true;
                    runtimeSettings.colorClearValue = Color.clear;
                    runtimeSettings.clearDepthStencil = true;
                    // A textura cobre a tela inteira, no mesmo tamanho: posição de tela = posição no painel.
                    runtimeSettings.SetScreenToPanelSpaceFunction(p => p);
                    EnsurePanelTexture();
                }
                document.panelSettings = runtimeSettings;
                go.SetActive(true);
            }
            SetLegacy(startWithLegacy);
        }

        private void OnDisable()
        {
            if (HudInputBlocker.Picker == (System.Func<Vector2, bool>)Pick) HudInputBlocker.Picker = null;
            if (presenter != null) presenter.HudControlsResultTiming = false;
            ActorFocusController.ShowHelpLine = true;
        }

        private void OnEnable()
        {
            if (built) SetLegacy(legacy);
        }

        private void OnDestroy()
        {
            if (runtimeSettings != null) Destroy(runtimeSettings);
            ReleaseTexture(ref panelTexture);
            if (compositeMaterial != null) Destroy(compositeMaterial);
        }

        // ================================================================== Correção de cor (URP + HDR)

        private bool NeedsColorFix()
        {
            if (hdrColorFix == HdrFix.Desligado) return false;
            if (hdrColorFix == HdrFix.Ligado) return true;
            var cam = presenter != null && presenter.MainCamera != null ? presenter.MainCamera : Camera.main;
            return QualitySettings.activeColorSpace == ColorSpace.Linear && cam != null && cam.allowHDR
                   && UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null;
        }

        /// <summary>
        /// RenderTexture do tamanho da tela (recriada quando a resolução muda). Ela é Linear: guarda os bytes
        /// exatamente como o painel escreveu (já em espaço gamma). No OnGUI o shader HudComposite põe a imagem na
        /// tela: se o destino converte para sRGB na escrita, ele devolve as cores para linear antes; senão, cruas.
        /// </summary>
        private void EnsurePanelTexture()
        {
            int w = Mathf.Max(1, Screen.width), h = Mathf.Max(1, Screen.height);
            if (panelTexture != null && panelTexture.width == w && panelTexture.height == h) return;
            ReleaseTexture(ref panelTexture);
            panelTexture = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear) { name = "RT_HUD" };
            panelTexture.Create();
            runtimeSettings.targetTexture = panelTexture;
        }

        private static void ReleaseTexture(ref RenderTexture rt)
        {
            if (rt == null) return;
            rt.Release();
            Destroy(rt);
            rt = null;
        }

        private void OnGUI()
        {
            // O painel em textura entra na tela por IMGUI (depois de todas as câmeras, cores certas).
            if (!useTexture || legacy || panelTexture == null || Event.current.type != EventType.Repaint) return;
            // O destino converte para sRGB na escrita? (Game view do editor com/sem escala, player, etc.)
            var target = RenderTexture.active;
            bool encodes = GL.sRGBWrite && (target != null ? target.sRGB : QualitySettings.activeColorSpace == ColorSpace.Linear);
            CompositeLinearized = encodes;
            CompositeTarget = (target != null ? target.name + (target.sRGB ? " (sRGB)" : " (Linear)") : "tela") + (GL.sRGBWrite ? " · sRGBWrite" : "");
            compositeMaterial.SetFloat(LinearizeId, encodes ? 1f : 0f);
            Graphics.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), panelTexture, compositeMaterial);
        }

        private void SetLegacy(bool on)
        {
            legacy = on || document == null;
            if (legacyHud != null) legacyHud.enabled = legacy;
            if (presenter != null) presenter.HudControlsResultTiming = !legacy;
            ActorFocusController.ShowHelpLine = legacy;
            HudInputBlocker.Picker = legacy ? null : (System.Func<Vector2, bool>)Pick;
            if (ctx.Root != null) Ui.Display(ctx.Root, !legacy);
            if (!legacy) lastPhase = (RunPresenter.Phase)(-1); // reabre o modal da fase atual
            else
            {
                modals?.Close();
                subtitles?.Cancel();
                record?.ShowMenu(false);
            }
        }

        /// <summary>HUD nova: o ponto de tela (Y para cima) está sobre um elemento clicável?</summary>
        private bool Pick(Vector2 screenYUp)
        {
            if (legacy || ctx.Root == null || ctx.Root.panel == null) return false;
            var p = RuntimePanelUtils.ScreenToPanel(ctx.Root.panel, new Vector2(screenYUp.x, Screen.height - screenYUp.y));
            return ctx.Root.panel.Pick(p) != null;
        }

        // ================================================================== Montagem

        private void Build()
        {
            var docRoot = document.rootVisualElement;
            docRoot.pickingMode = PickingMode.Ignore;
            var root = new VisualElement { name = "HudRoot" };
            root.AddToClassList("ht-root");
            root.pickingMode = PickingMode.Ignore;
            if (styleSheets != null)
                foreach (var s in styleSheets)
                    if (s != null) root.styleSheets.Add(s);
            docRoot.Add(root);

            ctx.Root = root;
            ctx.World = Layer("layer-world");
            ctx.Film = Layer("layer-film");
            ctx.Hud = Layer("layer-hud");
            ctx.Modal = Layer("layer-modal");
            ctx.Top = Layer("layer-top");
            ctx.Tooltip = new HudTooltip(ctx.Top);

            world = new WorldMarkers(ctx.World);
            filmFrame = new FilmFrame(ctx.Film);
            claquete = new ClaquetePanel(ctx.Hud, ctx);
            villainChip = new VillainChip(ctx.Hud, ctx);
            toasts = new ToastStack(ctx.Hud);
            script = new ScriptTracker(ctx.Hud, ctx, this);
            monitorPanel = new MonitorPanel(ctx.Hud, ctx);
            roomCard = new RoomCardPanel(ctx.Hud, ctx);
            cast = new CastStrip(ctx.Hud);
            actionBar = new ActionBar(ctx.Hud);
            hint = Ui.Lbl("Clique numa sala ou num ator", "t-body hint-line fadeable", ctx.Hud);
            hint.pickingMode = PickingMode.Ignore;
            record = new RecordCluster(ctx.Hud, ctx.Top, ctx, this);
            subtitles = new SubtitleReport(ctx.Hud, ctx.Top);
            modals = new ModalHost(ctx.Modal);
            built = true;
            Ui.Display(root, !legacy);
        }

        private VisualElement Layer(string cls)
        {
            var e = Ui.El("layer " + cls, ctx.Root);
            e.pickingMode = PickingMode.Ignore;
            return e;
        }

        // ================================================================== Escala

        private void ApplyScale()
        {
            if (runtimeSettings == null) return;
            float raw = Screen.height / 1080f;
            float player = ctx.Settings.PlayerScale;
            bool needClamp = clampScale && (raw < HudScaleMath.MinScale || raw > HudScaleMath.MaxScale || !Mathf.Approximately(player, 1f));
            if (needClamp)
            {
                float s = HudScaleMath.Compute(Screen.height, player);
                if (runtimeSettings.scaleMode != PanelScaleMode.ConstantPixelSize) runtimeSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
                if (!Mathf.Approximately(runtimeSettings.scale, s)) runtimeSettings.scale = s;
            }
            else if (runtimeSettings.scaleMode != panelSettings.scaleMode)
            {
                runtimeSettings.scaleMode = panelSettings.scaleMode;
                runtimeSettings.scale = panelSettings.scale;
            }
        }

        // ================================================================== Quadro a quadro

        private void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb != null && kb.f1Key.wasPressedThisFrame && document != null) SetLegacy(!legacy);
            if (presenter == null || presenter.Run == null || document == null) return;
            if (!built)
            {
                if (document.rootVisualElement == null) return;
                Build();
            }
            if (legacy) return;

            if (useTexture) EnsurePanelTexture();
            ApplyScale();
            var run = presenter.Run;
            var phase = presenter.CurrentPhase;
            float now = Time.unscaledTime;

            // ---------------- input
            bool anyInput = false;
            bool click = false, spacePressed = false, spaceDown = false, shift = false;
            if (mouse != null)
            {
                click = mouse.leftButton.wasPressedThisFrame;
                anyInput |= click || mouse.rightButton.isPressed || mouse.middleButton.isPressed
                            || mouse.delta.ReadValue().sqrMagnitude > 0.5f || Mathf.Abs(mouse.scroll.ReadValue().y) > 0.01f;
            }
            if (kb != null)
            {
                spacePressed = kb.spaceKey.wasPressedThisFrame;
                spaceDown = kb.spaceKey.isPressed;
                shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
                anyInput |= kb.anyKey.isPressed;
                if (kb.escapeKey.wasPressedThisFrame)
                {
                    if (record.MenuOpen) record.ShowMenu(false);
                    else if (modals.IsOverlay) modals.Close();
                }
                if (kb.rKey.wasPressedThisFrame && phase == RunPresenter.Phase.Idle)
                {
                    if (modals.Current == "log") modals.Close();
                    else OpenReportLog();
                }
            }
            if (anyInput) lastInputTime = now;

            // ---------------- trocas de fase
            if (phase != lastPhase)
            {
                OnPhaseChanged(lastPhase, phase);
                lastPhase = phase;
            }
            if (phase == RunPresenter.Phase.Idle && !subtitles.Active)
            {
                frozenScore = run.TotalScore;
                ctx.SceneShown = Mathf.Clamp(run.ScenesThisAct - run.ScenesLeft + 1, 1, Mathf.Max(1, run.ScenesThisAct));
            }

            // ---------------- estado
            ctx.Sequence = phase == RunPresenter.Phase.Busy || phase == RunPresenter.Phase.ShowingResult;
            ctx.PhaseModal = phase == RunPresenter.Phase.ActBreak || phase == RunPresenter.Phase.VillainChoice
                             || phase == RunPresenter.Phase.ArtefatoChoice || phase == RunPresenter.Phase.Ended
                             || phase == RunPresenter.Phase.DraftChoice;
            if (phase != RunPresenter.Phase.Idle && record.MenuOpen) record.ShowMenu(false);
            if (phase != RunPresenter.Phase.Idle && modals.IsOverlay) modals.Close();
            ctx.OverlayOpen = modals.IsOverlay || record.MenuOpen;
            bool cinematic = director != null && director.CurrentMode == CinematicDirector.Mode.Cinematic;
            ctx.FilmMode = phase == RunPresenter.Phase.Idle && cinematic && now - lastInputTime >= filmIdleSeconds
                           && !ctx.OverlayOpen && !presenter.HasCard;
            ctx.MonitorVisible = monitor != null && monitor.IsOpen && !ctx.PhaseModal && !ctx.Sequence && !ctx.FilmMode;
            ctx.PavorHeld = phase == RunPresenter.Phase.Busy || subtitles.PavorHeld;
            if (!hintDismissed && (presenter.Selected != null || presenter.HasCard)) hintDismissed = true;

            // ---------------- relatório (antes do resto: pode devolver a fase para Idle)
            bool spaceForSkip = spacePressed && phase == RunPresenter.Phase.ShowingResult;
            subtitles.Update(ctx, (click || spaceForSkip) && phase == RunPresenter.Phase.ShowingResult, shift, claquete.ScoreAnchor, ctx.Hud);
            if (spaceForSkip) spacePressed = false;
            phase = presenter.CurrentPhase;

            // ---------------- painéis
            bool showHud = !ctx.PhaseModal;
            bool calm = showHud && !ctx.Sequence && !ctx.FilmMode;
            bool idle = phase == RunPresenter.Phase.Idle;

            int target = phase == RunPresenter.Phase.Busy ? frozenScore : subtitles.Active ? subtitles.TargetScore(ctx) : run.TotalScore;
            claquete.Update(ctx, target);
            Ui.Fade(claquete.Root, showHud);

            villainChip.Update(ctx);
            Ui.Fade(villainChip.Root, showHud && !ctx.Sequence && (!ctx.FilmMode || villainChip.ActsNextScene(ctx)));

            toasts.Update();
            Ui.Display(toasts.Root, showHud);

            script.Update(ctx);
            Ui.Fade(script.Root, calm);

            cast.Update(ctx);
            Ui.Display(cast.Root, showHud);
            cast.ApplyFilmMode(ctx, ctx.FilmMode);

            bool showActions = calm && idle && presenter.Selected != null;
            if (showActions) actionBar.Update(ctx);
            else actionBar.Clear(ctx);
            Ui.Fade(actionBar.Root, showActions, 0.15f);
            float monitorRight = ctx.MonitorVisible ? 24f + MonitorPanel.Size(ctx).x + 4f + 16f : 408f;
            actionBar.Root.style.right = monitorRight;
            hint.style.right = monitorRight;

            Ui.Fade(hint, calm && idle && !hintDismissed);

            record.Update(ctx, spaceDown, spacePressed);
            Ui.Fade(record.Root, calm);

            // Card (Z7): começa 16 px abaixo da claquete e vai no máximo até 16 px acima do elenco (Z4).
            float clapBottom = claquete.Root.layout.yMax;
            float cardTop = float.IsNaN(clapBottom) || clapBottom < 1f ? 156f : clapBottom + 16f;
            roomCard.Root.style.top = cardTop;
            float panelH = ctx.Root.layout.height;
            if (!float.IsNaN(panelH) && panelH > 1f) roomCard.Root.style.maxHeight = panelH - cardTop - (24f + 136f + 12f + 16f);
            roomCard.Update(ctx);
            Ui.Fade(roomCard.Root, calm && idle && presenter.HasCard, 0.15f);

            monitorPanel.Update(ctx, ctx.MonitorVisible, !ctx.OverlayOpen);
            Ui.Fade(monitorPanel.Root, ctx.MonitorVisible, 0.2f);

            filmFrame.Update(ctx, showHud && (ctx.Sequence || (director != null && director.IsFilmMode)), ctx.FilmMode);
            world.Update(ctx, !ctx.PhaseModal);

            // ---------------- toasts de contexto
            Toasts(run);

            ctx.Tweens.Update(Time.unscaledDeltaTime);
            ctx.Tooltip.Update(ctx.Root);
        }

        private void OnPhaseChanged(RunPresenter.Phase from, RunPresenter.Phase to)
        {
            ctx.Tooltip?.Hide();
            if (!modals.IsOverlay) modals.Close();
            switch (to)
            {
                case RunPresenter.Phase.ShowingResult:
                    subtitles.Begin(ctx, frozenScore, history);
                    break;
                case RunPresenter.Phase.ActBreak:
                    modals.ShowActBreak(ctx);
                    break;
                case RunPresenter.Phase.ArtefatoChoice:
                    modals.ShowArtefato(ctx);
                    break;
                case RunPresenter.Phase.DraftChoice:
                    modals.ShowDraft(ctx);
                    break;
                case RunPresenter.Phase.VillainChoice:
                    modals.ShowVillain(ctx);
                    break;
                case RunPresenter.Phase.Ended:
                    modals.ShowEnd(ctx, () => toasts.Push(ctx, "list", "ok", "Log da run copiado."));
                    break;
            }
        }

        private void Toasts(FilmRun run)
        {
            if (ctx.PhaseModal) return;
            if (lastAct != run.ActIndex)
            {
                if (lastAct >= 0) toasts.Push(ctx, "clapper", "papel", $"ATO {HudText.Roman(run.ActIndex)} · meta {run.CurrentAct.goal} de audiência");
                lastAct = run.ActIndex;
            }
            var v = run.VillainNpc;
            if (!villainAnnounced && v != null && v.InHouse && !ctx.Sequence)
            {
                villainAnnounced = true;
                toasts.Push(ctx, v.IsGhost ? "ghost" : "knife", v.IsGhost ? "fantasma" : "perigo",
                    v.IsGhost ? $"{run.Villain.DisplayName} assombra a casa" : $"{run.Villain.DisplayName} entrou na casa");
            }
            if (goalToastAct != run.ActIndex && run.CurrentAct.goal > 0 && ctx.DisplayedScore >= run.CurrentAct.goal - 0.5f)
            {
                goalToastAct = run.ActIndex;
                toasts.Push(ctx, "check", "ok", "Meta do ato batida!");
            }
        }

        // ================================================================== IHudActions (menu ≡)

        public void OpenReportLog() => modals.ShowReportLog(ctx, history);
        public void OpenHelp() => modals.ShowHelp(ctx);
        public void OpenScriptList() => modals.ShowScriptList(ctx);

        public void ConfirmEndAct()
        {
            if (presenter.CurrentPhase == RunPresenter.Phase.Idle) modals.ShowConfirmEndAct(ctx);
        }

        public void ToggleLegacy() => SetLegacy(!legacy);

        public void SettingsChanged()
        {
            ctx.Settings.Save();
            ApplyScale();
        }

        // ================================================================== Testes por reflexão

        /// <summary>Para testes/capturas: pula o beat atual do relatório (como um clique).</summary>
        public void DebugSkipBeat() => subtitles?.Sequencer.Skip(Time.unscaledTime);
        /// <summary>Para testes/capturas: força o modo filme agora (como 7 s sem input).</summary>
        public void DebugForceIdle() => lastInputTime = -999f;
        public void DebugOpenMenu() => record?.ShowMenu(true);
        /// <summary>Para capturas: congela o relatório no beat atual (true) ou solta (false).</summary>
        public void DebugFreezeReport(bool on) => ctx.DebugFreeze = on;
    }
}
