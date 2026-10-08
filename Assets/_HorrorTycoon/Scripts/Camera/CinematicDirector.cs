using System.Collections.Generic;
using HorrorTycoon.Actors;
using HorrorTycoon.Rooms;
using HorrorTycoon.Run;
using Unity.Cinemachine;
using UnityEngine;

namespace HorrorTycoon.Cameras
{
    /// <summary>
    /// DIRETOR DE CÂMERA: decide o que a tela principal mostra.
    ///
    /// Regra do Gabriel:
    ///   - SEM o monitor do diretor: câmera LONGE, vendo a casa de cima (IsoCameraRig), girando
    ///     devagar e "respirando" o zoom. O jogador sempre vê o mapa.
    ///   - COM o monitor aberto (o mapa está no monitor): a tela principal vira FILME.
    ///     Planos feitos de LONGE com zoom de lente (teleobjetiva), como alguém observando
    ///     escondido atrás de arbustos (CameraVantage), com zooms lentos e cortes secos.
    ///
    /// Modos:
    ///   Overview   - visão da casa (sem monitor, ou o jogador mexeu na câmera).
    ///   Cinematic  - planos de filme (monitor aberto e ninguém mexendo).
    ///   Track      - monitor aberto e um ator encenando: o "observador" acompanha de longe.
    ///   DoorFocus  - card de sala aberto: com monitor, plano de lente na porta;
    ///                sem monitor, a visão da casa desliza até a porta.
    /// O close de reação (ActorFocusController, prioridade 20) passa por cima de todos.
    ///
    /// Zoom de lente: a câmera fica longe e o FOV é calculado para "caber" uma altura de quadro
    /// (ex.: 1,8 m = plano médio de um ator). Quanto mais longe, menor o FOV.
    /// Tudo aqui é visual: usa UnityEngine.Random (nunca a seed da run).
    /// </summary>
    public class CinematicDirector : MonoBehaviour
    {
        public enum Mode { Cinematic, Track, Overview, DoorFocus }

        [Header("Referências")]
        [SerializeField] private RunPresenter presenter;
        [SerializeField] private IsoCameraRig rig;
        [SerializeField] private CinemachineCamera cineCamera;
        [SerializeField] private CinemachineCamera doorCamera;
        [SerializeField] private ActorFocusController focusController;
        [SerializeField] private DirectorMonitor monitor;

        [Header("Ritmo")]
        [Tooltip("Segundos sem mexer na câmera até voltar aos planos de filme (com monitor aberto).")]
        [SerializeField] private float returnToCinematicAfter = 7f;
        [SerializeField] private Vector2 shotDuration = new Vector2(6f, 9f);
        [Range(0f, 1f)] [SerializeField] private float establishingChance = 0.15f;
        [Range(0f, 1f)] [SerializeField] private float creepZoomChance = 0.25f;

        [Header("Visão da casa (sem monitor)")]
        [SerializeField] private float overviewDistance = 22f;
        [SerializeField] private float doorGlideDistance = 12f;

        [Header("Observador (planos de lente)")]
        [Tooltip("Distância da câmera até o ator nos planos de filme (metros).")]
        [SerializeField] private Vector2 watchDistance = new Vector2(11f, 20f);
        [SerializeField] private Vector2 watchHeight = new Vector2(2.2f, 4.5f);
        [Tooltip("Altura do quadro num plano médio (metros que cabem na tela na vertical).")]
        [SerializeField] private float mediumFrame = 1.8f;
        [Tooltip("Primeiros metros do raio que podem ter obstáculo (o arbusto na frente da lente).")]
        [SerializeField] private float foregroundAllowance = 3f;

        [Header("Prioridades (Iso = 10, close do ator = 20)")]
        [SerializeField] private int activePriority = 15;
        [SerializeField] private int doorPriority = 17;

        [Header("Câmera na mão")]
        [SerializeField] private float shakePosition = 0.04f;
        [SerializeField] private float shakeDegrees = 0.35f;

        /// <summary>Centro da casa fixa do P0 (padrão quando não há casa gerada).</summary>
        private static readonly Vector3 HouseCenter = new Vector3(0f, 0f, -0.5f);

        /// <summary>Centro da casa: o da casa gerada (HouseBounds) ou o do P0.</summary>
        private static Vector3 Center => HouseBounds.HasValue ? HouseBounds.Center : HouseCenter;

        private Mode mode = Mode.Overview;
        private bool started;
        private bool firstShot = true;
        private bool preferSelected;
        private int doorRoom = -1;
        private int lastCardRoom = -1;
        private float cardOpenedAt;
        private bool lastMonitorOpen;

        // Plano atual
        private Vector3 camFrom, camTo;
        private Transform lookTarget;
        private Vector3 lookOffset;
        private Vector3 lookFixed;
        private float frameFrom, frameTo;
        private float shotTime, shotLength;
        private float cutMargin = 0.6f;
        private Vector3? fixedFocus;
        private float noiseSeed;

        // Track
        private Transform tracked;
        private Vector3 trackLook;

        private readonly List<CameraVantage> vantages = new List<CameraVantage>();

        public Mode CurrentMode => mode;

        private bool MonitorOpen => monitor != null && monitor.IsOpen;

        /// <summary>Planos de "filme" (a HUD põe as faixas pretas e esconde os nomes no mapa).</summary>
        public bool IsFilmMode => mode == Mode.Cinematic || mode == Mode.Track || (focusController != null && focusController.CurrentActor != null);

        /// <summary>Mostrar nomes das salas e atores sobre a imagem principal?</summary>
        public bool ShowWorldLabels => mode == Mode.Overview && (focusController == null || focusController.CurrentActor == null);

        private void Start()
        {
            noiseSeed = Random.value * 100f;
            vantages.AddRange(FindObjectsByType<CameraVantage>(FindObjectsSortMode.None));
        }

        private void LateUpdate()
        {
            if (presenter == null || presenter.Run == null) return;

            Mode wanted = DecideMode();
            bool cardChanged = wanted == Mode.DoorFocus && presenter.CardRoom != doorRoom;
            bool monitorChanged = MonitorOpen != lastMonitorOpen;
            lastMonitorOpen = MonitorOpen;

            if (!started || wanted != mode || cardChanged || (wanted == Mode.DoorFocus && monitorChanged))
            {
                started = true;
                EnterMode(wanted);
            }

            switch (mode)
            {
                case Mode.Cinematic: UpdateShot(cineCamera, true); break;
                case Mode.DoorFocus:
                    if (MonitorOpen) UpdateShot(doorCamera, false);
                    else CameraFocus.Set(rig != null ? rig.transform.position : Center, 0.6f);
                    break;
                case Mode.Track: UpdateTrack(); break;
                case Mode.Overview: CameraFocus.Set(rig != null ? rig.transform.position : Center, 0.6f); break;
            }

            // Close do ator por cima de tudo: o corte de paredes olha para ele.
            if (focusController != null && focusController.CurrentActor != null)
            {
                CameraFocus.Set(focusController.CurrentActor.transform.position, 0.6f);
            }
        }

        // ================================================================== Estados

        private Mode DecideMode()
        {
            var phase = presenter.CurrentPhase;

            if (presenter.CardRoom != lastCardRoom)
            {
                lastCardRoom = presenter.CardRoom;
                cardOpenedAt = Time.unscaledTime;
            }

            float lastInput = rig != null ? rig.LastInputTime : -999f;
            bool recentInput = Time.unscaledTime - lastInput < returnToCinematicAfter;

            if (phase == RunPresenter.Phase.Busy && presenter.Selected != null)
            {
                return MonitorOpen ? Mode.Track : Mode.Overview;
            }

            if (phase == RunPresenter.Phase.Idle && presenter.CardRoom >= 0)
            {
                // Abriu o card e depois mexeu na câmera: o jogador quer olhar a casa.
                if (recentInput && lastInput > cardOpenedAt) return Mode.Overview;
                return Mode.DoorFocus;
            }

            if (!MonitorOpen) return Mode.Overview;
            return recentInput ? Mode.Overview : Mode.Cinematic;
        }

        private void EnterMode(Mode next)
        {
            Mode previous = mode;
            mode = next;
            bool doorWithMonitor = next == Mode.DoorFocus && MonitorOpen;

            if (cineCamera != null) cineCamera.Priority = (next == Mode.Cinematic || next == Mode.Track) ? activePriority : 0;
            if (doorCamera != null) doorCamera.Priority = doorWithMonitor ? doorPriority : 0;
            if (rig != null) rig.AutoDrift = next == Mode.Overview;

            switch (next)
            {
                case Mode.Cinematic:
                    if (previous == Mode.Track) preferSelected = true; // mostra quem acabou de agir
                    NextShot();
                    break;
                case Mode.Track:
                    tracked = presenter.ViewOf(presenter.Selected)?.transform;
                    StartTrack();
                    break;
                case Mode.Overview:
                    if (rig != null) rig.ZoomOutAtLeast(overviewDistance * HouseBounds.Scale); // casa maior: mais longe
                    break;
                case Mode.DoorFocus:
                    doorRoom = presenter.CardRoom;
                    var anchor = presenter.AnchorOf(doorRoom);
                    if (anchor == null) break;
                    if (doorWithMonitor) DoorShot(anchor, presenter.Run.Rooms[doorRoom].IsHub);
                    else if (rig != null) rig.GlideTo(anchor.DoorPoint, doorGlideDistance);
                    break;
            }
            if (next != Mode.DoorFocus) doorRoom = -1;
        }

        // ================================================================== Planos de filme (monitor aberto)

        private void NextShot()
        {
            var alive = new List<ActorRunState>();
            foreach (var a in presenter.Run.Actors)
            {
                if (a.Alive && presenter.ViewOf(a) != null) alive.Add(a);
            }

            if (alive.Count == 0 || firstShot || Random.value < establishingChance)
            {
                firstShot = false;
                EstablishingShot();
                return;
            }

            ActorRunState subject = alive[Random.Range(0, alive.Count)];
            if (presenter.Selected != null && presenter.Selected.Alive && (preferSelected || Random.value < 0.5f))
            {
                subject = presenter.Selected;
            }
            preferSelected = false;

            var group = new List<ActorView>();
            foreach (var a in alive)
            {
                if (a.RoomIndex == subject.RoomIndex) group.Add(presenter.ViewOf(a));
            }

            ActorView view = presenter.ViewOf(subject);
            if (group.Count >= 2 && Random.value < 0.5f) GroupShot(group);
            else if (Random.value < creepZoomChance) CreepZoomShot(view);
            else WatchShot(view);
        }

        /// <summary>A casa inteira vista de longe, com um zoom lento.</summary>
        private void EstablishingShot()
        {
            // Casa gerada maior que a do P0: órbita e quadro crescem na mesma proporção (P0: fator 1).
            float k = HouseBounds.Scale;
            Vector3 center = Center;
            float angle = Random.Range(0f, 360f);
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.back;
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            camFrom = center + dir * (Random.Range(24f, 30f) * k) + Vector3.up * (Random.Range(6f, 9f) * k);
            camTo = camFrom + side * Random.Range(-2.5f, 2.5f);
            SetLook(null, Vector3.zero, center + Vector3.up * 0.8f);
            BeginShot(cineCamera, Random.Range(shotDuration.x, shotDuration.y), 13f * k, 10f * k, 0.6f, null);
        }

        /// <summary>Observando um ator de longe (plano médio pela lente), com zoom bem lento.</summary>
        private void WatchShot(ActorView view)
        {
            Vector3 target = view.transform.position + Vector3.up * 1.3f;
            camFrom = PickVantage(target, null);
            camTo = camFrom + Drift();
            SetLook(view.transform, Vector3.up * 1.3f, Vector3.zero);
            BeginShot(cineCamera, Random.Range(shotDuration.x, shotDuration.y), mediumFrame * 1.3f, mediumFrame, 0.6f, null);
        }

        /// <summary>Zoom lento "assustador": começa aberto e fecha no ator.</summary>
        private void CreepZoomShot(ActorView view)
        {
            Vector3 target = view.transform.position + Vector3.up * 1.4f;
            camFrom = PickVantage(target, null);
            camTo = camFrom;
            SetLook(view.transform, Vector3.up * 1.45f, Vector3.zero);
            BeginShot(cineCamera, Random.Range(7f, 9f), 7f, 0.9f, 0.6f, null);
        }

        /// <summary>Vários atores juntos: quadro do tamanho do grupo.</summary>
        private void GroupShot(List<ActorView> group)
        {
            Vector3 mid = Vector3.zero;
            foreach (var v in group) mid += v.transform.position;
            mid /= group.Count;
            float spread = 0f;
            foreach (var v in group) spread = Mathf.Max(spread, (v.transform.position - mid).magnitude);

            Vector3 target = mid + Vector3.up * 1.2f;
            camFrom = PickVantage(target, null);
            camTo = camFrom + Drift();
            SetLook(null, Vector3.zero, target);
            float frame = Mathf.Max(2.6f, spread * 2f + 1.6f);
            BeginShot(cineCamera, Random.Range(shotDuration.x, shotDuration.y), frame * 1.15f, frame, 0.6f, null);
        }

        /// <summary>Card aberto com monitor: plano de lente na porta da sala, visto do lado do corredor.</summary>
        private void DoorShot(RoomAnchor anchor, bool isHub)
        {
            Vector3 door = anchor.DoorPoint;
            Vector3 outward = anchor.DoorOutward;
            Vector3 look = door + Vector3.up * 1.15f - outward * 0.5f;
            camFrom = PickVantage(look, outward);
            camTo = camFrom + Drift() * 0.5f;
            SetLook(null, Vector3.zero, look);
            BeginShot(doorCamera, 6f, isHub ? 4.5f : 3.6f, isHub ? 3.6f : 2.8f, -0.3f, door);
        }

        private Vector3 Drift()
        {
            Vector2 r = Random.insideUnitCircle * 0.8f;
            return new Vector3(r.x, Random.Range(-0.2f, 0.2f), r.y);
        }

        // ================================================================== Onde pôr a câmera

        /// <summary>
        /// Escolhe um "esconderijo" de onde filmar o alvo: marcadores CameraVantage do cenário
        /// ou pontos gerados em volta. Rejeita pontos com algo no caminho (árvore, etc.), mas deixa
        /// os primeiros metros livres para ter um arbusto na frente da lente (efeito de "espiando").
        /// Paredes não reprovam o ponto (o buraco de visão é a rede de segurança), mas um ponto com parede
        /// escondendo o alvo perde pontos: o diretor prefere planos pela porta/janela, com o ator à vista.
        /// </summary>
        private Vector3 PickVantage(Vector3 target, Vector3? preferredDir)
        {
            var candidates = new List<Vector3>();
            foreach (var v in vantages)
            {
                if (v != null) candidates.Add(v.transform.position);
            }
            for (int i = 0; i < 10; i++)
            {
                Vector3 dir = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
                if (preferredDir.HasValue && i < 6)
                {
                    dir = Quaternion.Euler(0f, Random.Range(-35f, 35f), 0f) * preferredDir.Value;
                }
                Vector3 p = target + dir * Random.Range(watchDistance.x, watchDistance.y);
                p.y = Random.Range(watchHeight.x, watchHeight.y);
                candidates.Add(p);
            }

            Vector3 best = target + new Vector3(0f, 3f, -14f);
            float bestScore = float.MinValue;
            foreach (var c in candidates)
            {
                float d = Vector3.Distance(c, target);
                if (d < watchDistance.x * 0.8f || d > watchDistance.y * 1.4f) continue;
                if (!ClearLine(c, target, out bool wallBlocked)) continue;
                float score = Random.value;
                if (wallBlocked) score -= 2f;
                if (preferredDir.HasValue)
                {
                    Vector3 dir = c - target;
                    dir.y = 0f;
                    score += Vector3.Dot(dir.normalized, preferredDir.Value) * 2f;
                }
                if (score > bestScore)
                {
                    bestScore = score;
                    best = c;
                }
            }
            return best;
        }

        private bool ClearLine(Vector3 from, Vector3 to, out bool wallBlocked)
        {
            wallBlocked = false;
            Vector3 dir = to - from;
            float dist = dir.magnitude;
            if (dist < 0.1f) return true;
            var hits = Physics.RaycastAll(from, dir / dist, dist - 0.6f);
            foreach (var h in hits)
            {
                if (h.distance < foregroundAllowance) continue;                     // arbusto na frente da lente: pode
                if (h.collider.GetComponentInParent<WallCutaway>() != null) { wallBlocked = true; continue; } // parede: penaliza (buraco de visão)
                if (h.collider.GetComponentInParent<ActorView>() != null) continue;
                if (h.collider.GetComponentInParent<RoomAnchor>() != null) continue;  // piso
                return false;
            }
            return true;
        }

        // ================================================================== Execução do plano

        private void SetLook(Transform target, Vector3 offset, Vector3 fixedPoint)
        {
            lookTarget = target;
            lookOffset = offset;
            lookFixed = fixedPoint;
        }

        private void BeginShot(CinemachineCamera cam, float length, float fromFrame, float toFrame, float margin, Vector3? focus)
        {
            shotTime = 0f;
            shotLength = length;
            frameFrom = fromFrame;
            frameTo = toFrame;
            cutMargin = margin;
            fixedFocus = focus;
            if (cam != null) PlaceCamera(cam, 0f);
        }

        private void UpdateShot(CinemachineCamera cam, bool autoCut)
        {
            if (cam == null) return;
            shotTime += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(shotTime / Mathf.Max(0.1f, shotLength));
            PlaceCamera(cam, t);
            if (autoCut && shotTime >= shotLength) NextShot(); // corte seco para o próximo plano
        }

        private Vector3 CurrentLookPoint() => lookTarget != null ? lookTarget.position + lookOffset : lookFixed;

        /// <summary>FOV para que 'frameHeight' metros caibam na vertical a 'distance' metros (zoom de lente).</summary>
        public static float FovFor(float frameHeight, float distance)
        {
            return Mathf.Clamp(2f * Mathf.Atan(frameHeight * 0.5f / Mathf.Max(0.5f, distance)) * Mathf.Rad2Deg, 2.5f, 60f);
        }

        private void PlaceCamera(CinemachineCamera cam, float t)
        {
            float eased = Mathf.SmoothStep(0f, 1f, t);
            Vector3 pos = Vector3.Lerp(camFrom, camTo, eased);
            Vector3 look = CurrentLookPoint();
            float fov = FovFor(Mathf.Lerp(frameFrom, frameTo, eased), Vector3.Distance(pos, look));
            Shake(ref pos, look, fov, out Quaternion rot);
            cam.Lens.FieldOfView = fov;
            cam.transform.SetPositionAndRotation(pos, rot);

            Vector3 focus = fixedFocus ?? (lookTarget != null ? lookTarget.position : new Vector3(look.x, 0f, look.z));
            CameraFocus.Set(focus, cutMargin);
        }

        /// <summary>Câmera na mão: tremor suave. Com zoom alto (FOV pequeno) o tremor angular diminui.</summary>
        private void Shake(ref Vector3 pos, Vector3 look, float fov, out Quaternion rot)
        {
            float n = Time.unscaledTime * 0.35f;
            pos += new Vector3(Mathf.PerlinNoise(n, noiseSeed) - 0.5f, Mathf.PerlinNoise(noiseSeed, n) - 0.5f, Mathf.PerlinNoise(n + 7f, noiseSeed) - 0.5f) * (shakePosition * 2f);
            float deg = shakeDegrees * Mathf.Clamp(fov / 25f, 0.15f, 1f);
            rot = Quaternion.LookRotation(look - pos, Vector3.up)
                  * Quaternion.Euler((Mathf.PerlinNoise(n * 1.3f, noiseSeed + 3f) - 0.5f) * deg * 2f,
                                     (Mathf.PerlinNoise(noiseSeed + 5f, n * 1.3f) - 0.5f) * deg * 2f, 0f);
        }

        // ================================================================== Acompanhar o ator (de longe)

        private void StartTrack()
        {
            if (tracked == null || cineCamera == null) return;
            trackLook = tracked.position + Vector3.up * 1.3f;
            camFrom = PickVantage(trackLook, null);
            camTo = camFrom;
            frameFrom = frameTo = mediumFrame * 1.4f;
            PlaceTrack(1f);
        }

        private void UpdateTrack()
        {
            if (tracked == null || cineCamera == null) return;
            PlaceTrack(1f - Mathf.Exp(-3f * Time.unscaledDeltaTime));
        }

        /// <summary>O "observador" fica parado no esconderijo e gira a câmera seguindo o ator (panorâmica).</summary>
        private void PlaceTrack(float k)
        {
            trackLook = Vector3.Lerp(trackLook, tracked.position + Vector3.up * 1.3f, k);
            Vector3 pos = camFrom;
            float fov = FovFor(frameFrom, Vector3.Distance(pos, trackLook));
            Shake(ref pos, trackLook, fov, out Quaternion rot);
            cineCamera.Lens.FieldOfView = fov;
            cineCamera.transform.SetPositionAndRotation(pos, rot);
            CameraFocus.Set(tracked.position, 0.6f);
        }
    }
}
