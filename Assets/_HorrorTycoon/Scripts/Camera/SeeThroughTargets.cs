using System.Collections.Generic;
using HorrorTycoon.Actors;
using HorrorTycoon.Rooms;
using HorrorTycoon.Run;
using UnityEngine;
using UnityEngine.Rendering;

namespace HorrorTycoon.Cameras
{
    /// <summary>
    /// BURACO DE VISÃO estilo Baldur's Gate 3: as paredes ficam inteiras e só abre um círculo com borda de pincel
    /// em volta do ator quando ele está ESCONDIDO atrás de parede (ou móvel alto), visto pela câmera principal.
    ///
    ///   - Alvos: atores vivos (o selecionado e o do close ganham buraco maior) + SeeThroughSubject (vilão nunca).
    ///   - Oclusão: a cada 0,1 s, raios da câmera até 3 pontos do ator (pés, peito, cabeça) contra paredes
    ///     (objetos com WallCutaway). Escondido se ≥ 2 pontos bloqueados. Histerese: segura 0,25 s antes de fechar.
    ///   - Íris: o RAIO anima (abre 0,15 s, fecha 0,35 s). O shader (HT_Toon, keyword _SEETHROUGH) faz o resto.
    ///   - Por câmera: só a câmera principal recorta. Monitor do diretor e Scene View veem a casa inteira.
    ///   - Clique: SeeThroughTargets.Raycast ignora acertos em parede dentro de um buraco aberto.
    /// Docs/Tecnico/SeeThrough_Oclusao.md.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class SeeThroughTargets : MonoBehaviour
    {
        public const int MaxTargets = 8;

        /// <summary>Desliga o efeito no jogo inteiro (todas as câmeras veem paredes inteiras).</summary>
        public static bool GlobalEnabled = true;
        public static SeeThroughTargets Instance { get; private set; }

        [Header("Referências (opcionais: acha sozinho)")]
        [SerializeField] private RunPresenter presenter;
        [SerializeField] private ActorFocusController focusController;
        [SerializeField] private Camera mainCamera;

        [Header("Raios (m)")]
        [SerializeField] private float radius = 1.1f;
        [SerializeField] private float selectedRadius = 1.4f;
        [Tooltip("Ator no close (ActorFocusController).")]
        [SerializeField] private float closeRadius = 1.6f;
        [Tooltip("Centro do buraco acima dos pés (peito).")]
        [SerializeField] private float centerHeight = 1.0f;

        [Header("Íris (s)")]
        [SerializeField] private float openTime = 0.15f;
        [SerializeField] private float closeTime = 0.35f;
        [Tooltip("Segura aberto depois que o ator deixa de estar escondido (histerese).")]
        [SerializeField] private float holdTime = 0.25f;

        [Header("Oclusão")]
        [SerializeField] private float checkInterval = 0.1f;
        [Tooltip("Alturas testadas (pés, peito, cabeça).")]
        [SerializeField] private float[] checkHeights = { 0.35f, 1.0f, 1.55f };
        [SerializeField] private int minBlockedPoints = 2;

        [Header("Shader")]
        [Tooltip("Só recorta o que estiver pelo menos isto NA FRENTE do alvo (m).")]
        [SerializeField] private float depthBias = 0.3f;
        [Tooltip("Paredes: acima desta altura (m) nunca recorta (faixa fina sob o topo = linha da planta). Casa de 2,7 m → 2,62.")]
        [SerializeField] private float keepAboveY = 2.62f;
        [Tooltip("A faixa do topo só fica de pé com a câmera ALTA (visão da casa). Em planos baixos (cinema, close) ela " +
                 "flutuaria no quadro como uma barra solta, então o buraco atravessa a parede inteira.")]
        [SerializeField] private float keepTopMinCameraY = 6f;
        [Tooltip("Ruído da borda (fração do raio).")]
        [Range(0f, 0.4f)] [SerializeField] private float edgeNoise = 0.12f;
        [Tooltip("Ciclos de ruído por raio.")]
        [SerializeField] private float noiseScale = 3.5f;
        [SerializeField] private Color inkColor = new Color(0.102f, 0.078f, 0.125f, 1f);
        [Tooltip("Largura da tinta (px a 1080p).")]
        [SerializeField] private float inkWidthPx = 3f;
        [Tooltip("Largura do anel escuro em volta (px a 1080p).")]
        [SerializeField] private float darkWidthPx = 28f;
        [Range(0f, 1f)] [SerializeField] private float darkStrength = 0.35f;
        [Tooltip("Raio mínimo na tela (px a 1080p), para a câmera de longe.")]
        [SerializeField] private float minRadiusPx = 60f;

        private sealed class Slot
        {
            public Transform T;
            public ActorView View;
            public float Amount;
            public float Hold;
            public bool Occluded;
            public float Radius;
            public float Center;
            public bool Seen;
        }

        private readonly Dictionary<Transform, Slot> slots = new Dictionary<Transform, Slot>();
        private readonly List<Slot> active = new List<Slot>();
        private readonly Vector4[] buffer = new Vector4[MaxTargets];
        private readonly RaycastHit[] hits = new RaycastHit[16];
        private readonly RaycastHit[] pickHits = new RaycastHit[32];
        private readonly Dictionary<int, bool> wallCache = new Dictionary<int, bool>();
        private float nextCheck;
        private float effectiveKeepY = 2.62f;
        private float nextGather;
        private readonly List<ActorView> views = new List<ActorView>();
        private readonly List<SeeThroughSubject> subjects = new List<SeeThroughSubject>();

        private static readonly int TargetsId = Shader.PropertyToID("_HT_SeeThroughTargets");
        private static readonly int CountId = Shader.PropertyToID("_HT_SeeThroughCount");
        private static readonly int OnId = Shader.PropertyToID("_HT_SeeThroughOn");
        private static readonly int ParamsId = Shader.PropertyToID("_HT_SeeThroughParams");
        private static readonly int Params2Id = Shader.PropertyToID("_HT_SeeThroughParams2");
        private static readonly int InkId = Shader.PropertyToID("_HT_SeeThroughInk");

        /// <summary>Algum buraco aberto agora?</summary>
        public bool HasOpenHoles => active.Count > 0;

        private void OnEnable()
        {
            Instance = this;
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            Upload();
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            if (Instance == this) Instance = null;
            Shader.SetGlobalFloat(CountId, 0f);
            Shader.SetGlobalFloat(OnId, 0f);
        }

        private void Start()
        {
            if (presenter == null) presenter = FindFirstObjectByType<RunPresenter>();
            if (focusController == null) focusController = FindFirstObjectByType<ActorFocusController>();
        }

        private Camera MainCam
        {
            get
            {
                if (mainCamera == null || !mainCamera.isActiveAndEnabled) mainCamera = Camera.main;
                return mainCamera;
            }
        }

        /// <summary>Só a câmera principal do jogo recorta (monitor do diretor, Scene View e previews: paredes inteiras).</summary>
        private void OnBeginCamera(ScriptableRenderContext ctx, Camera cam)
        {
            bool on = GlobalEnabled && cam != null && cam == MainCam && cam.cameraType == CameraType.Game;
            Shader.SetGlobalFloat(OnId, on ? 1f : 0f);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (Time.unscaledTime >= nextGather)
            {
                nextGather = Time.unscaledTime + 1f;
                views.Clear();
                views.AddRange(FindObjectsByType<ActorView>(FindObjectsSortMode.None));
                subjects.Clear();
                subjects.AddRange(FindObjectsByType<SeeThroughSubject>(FindObjectsSortMode.None));
            }

            bool check = Time.unscaledTime >= nextCheck;
            if (check) nextCheck = Time.unscaledTime + checkInterval;

            foreach (var s in slots.Values) s.Seen = false;
            ActorView selected = presenter != null && presenter.Selected != null ? presenter.ViewOf(presenter.Selected) : null;
            ActorView close = focusController != null ? focusController.CurrentActor : null;

            foreach (var v in views)
            {
                if (v == null || v.IsDead || !v.isActiveAndEnabled) continue;
                var subject = v.GetComponent<SeeThroughSubject>();
                if (subject != null && subject.isVillain) continue;
                float r = v == close ? closeRadius : v == selected ? selectedRadius : radius;
                Touch(v.transform, v, r * (subject != null ? subject.radiusScale : 1f), subject != null ? subject.centerHeight : centerHeight);
            }
            foreach (var su in subjects)
            {
                if (su == null || su.isVillain || !su.isActiveAndEnabled || su.GetComponent<ActorView>() != null) continue;
                Touch(su.transform, null, radius * su.radiusScale, su.centerHeight);
            }

            Camera cam = MainCam;
            active.Clear();
            var dead = new List<Transform>();
            foreach (var pair in slots)
            {
                Slot s = pair.Value;
                if (!s.Seen || s.T == null)
                {
                    s.Occluded = false;
                    s.Hold = 0f;
                }
                else if (check && cam != null)
                {
                    s.Occluded = IsOccluded(cam.transform.position, s.T.position);
                }

                if (s.Occluded)
                {
                    s.Hold = holdTime;
                    s.Amount = Mathf.MoveTowards(s.Amount, 1f, dt / Mathf.Max(0.01f, openTime));
                }
                else
                {
                    s.Hold -= dt;
                    if (s.Hold <= 0f) s.Amount = Mathf.MoveTowards(s.Amount, 0f, dt / Mathf.Max(0.01f, closeTime));
                }

                if (s.Amount > 0.001f && s.T != null) active.Add(s);
                else if (!s.Seen) dead.Add(pair.Key);
            }
            foreach (var k in dead) slots.Remove(k);

            // Mais importantes primeiro (o array tem 8 posições).
            active.Sort((a, b) => b.Radius.CompareTo(a.Radius));
            Upload();
        }

        private void Touch(Transform t, ActorView v, float r, float center)
        {
            if (!slots.TryGetValue(t, out Slot s))
            {
                s = new Slot { T = t, View = v };
                slots[t] = s;
            }
            s.Radius = r;
            s.Center = center;
            s.Seen = true;
        }

        private void Upload()
        {
            int n = Mathf.Min(active.Count, MaxTargets);
            for (int i = 0; i < MaxTargets; i++)
            {
                if (i < n)
                {
                    Slot s = active[i];
                    float open = EaseOutBack(s.Amount);
                    Vector3 c = s.T.position + Vector3.up * s.Center;
                    buffer[i] = new Vector4(c.x, c.y, c.z, s.Radius * open);
                }
                else buffer[i] = Vector4.zero;
            }
            Shader.SetGlobalVectorArray(TargetsId, buffer); // sempre 8 entradas
            Shader.SetGlobalFloat(CountId, n);
            bool highCamera = mainCamera != null && mainCamera.transform.position.y >= keepTopMinCameraY
                              && (focusController == null || focusController.CurrentActor == null);
            effectiveKeepY = highCamera ? keepAboveY : 1e4f; // 1e4 = nunca mantém o topo
            Shader.SetGlobalVector(ParamsId, new Vector4(depthBias, effectiveKeepY, edgeNoise, noiseScale));
            Shader.SetGlobalVector(Params2Id, new Vector4(darkWidthPx, darkStrength, minRadiusPx, 0f));
            Shader.SetGlobalVector(InkId, new Vector4(inkColor.linear.r, inkColor.linear.g, inkColor.linear.b, inkWidthPx));
        }

        /// <summary>Íris de desenho animado: abre passando um pouco do ponto e assenta.</summary>
        private static float EaseOutBack(float x)
        {
            x = Mathf.Clamp01(x);
            const float c1 = 1.4f, c3 = c1 + 1f;
            float t = x - 1f;
            return Mathf.Max(0f, 1f + c3 * t * t * t + c1 * t * t);
        }

        // ================================================================== Oclusão

        private bool IsOccluded(Vector3 camPos, Vector3 feet)
        {
            int blocked = 0;
            foreach (float h in checkHeights)
            {
                Vector3 p = feet + Vector3.up * h;
                Vector3 d = p - camPos;
                float dist = d.magnitude;
                if (dist < 0.1f) continue;
                int n = Physics.RaycastNonAlloc(camPos, d / dist, hits, dist - 0.05f, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < n; i++)
                {
                    if (IsWall(hits[i].collider))
                    {
                        blocked++;
                        break;
                    }
                }
            }
            return blocked >= minBlockedPoints;
        }

        /// <summary>Parede = qualquer peça com WallCutaway (paredes, vergas, batentes das duas casas).</summary>
        public bool IsWall(Collider c)
        {
            if (c == null) return false;
            int id = c.GetInstanceID();
            if (!wallCache.TryGetValue(id, out bool w))
            {
                w = c.GetComponentInParent<WallCutaway>() != null;
                wallCache[id] = w;
            }
            return w;
        }

        // ================================================================== Clique através do buraco

        /// <summary>
        /// Igual a Physics.Raycast, mas um raio da CÂMERA PRINCIPAL atravessa paredes dentro de um buraco aberto
        /// (dá para clicar no ator/sala que aparece pelo buraco). Raios de outras câmeras (monitor): normal.
        /// </summary>
        public static bool Raycast(Ray ray, out RaycastHit hit, float maxDistance)
        {
            var inst = Instance;
            if (inst == null || !GlobalEnabled || !inst.HasOpenHoles || !inst.IsMainCameraRay(ray))
            {
                return Physics.Raycast(ray, out hit, maxDistance);
            }
            return inst.PickThroughHoles(ray, out hit, maxDistance);
        }

        private bool IsMainCameraRay(Ray ray)
        {
            Camera cam = MainCam;
            if (cam == null) return false;
            return (ray.origin - cam.transform.position).sqrMagnitude < 4f && Vector3.Dot(ray.direction, cam.transform.forward) > 0f;
        }

        private bool PickThroughHoles(Ray ray, out RaycastHit hit, float maxDistance)
        {
            int n = Physics.RaycastNonAlloc(ray, pickHits, maxDistance);
            System.Array.Sort(pickHits, 0, n, HitComparer.Instance);
            for (int i = 0; i < n; i++)
            {
                RaycastHit h = pickHits[i];
                if (IsWall(h.collider) && InsideHole(h.point, h.normal)) continue; // parede recortada: o clique passa
                hit = h;
                return true;
            }
            hit = default;
            return false;
        }

        /// <summary>Mesma conta do shader (sem o ruído da borda): o ponto está dentro de um buraco aberto?</summary>
        public bool InsideHole(Vector3 point, Vector3 normal)
        {
            Camera cam = MainCam;
            if (cam == null || active.Count == 0) return false;
            if (point.y < 0.06f || point.y > effectiveKeepY) return false; // mesma regra das paredes no shader
            Transform ct = cam.transform;
            float pDepth = Vector3.Dot(point - ct.position, ct.forward);
            Vector3 ps = cam.WorldToScreenPoint(point);
            float p11 = Mathf.Abs(cam.projectionMatrix[1, 1]);
            float halfH = cam.pixelHeight * 0.5f;
            float minPx = minRadiusPx * cam.pixelHeight / 1080f;
            int count = Mathf.Min(active.Count, MaxTargets);
            for (int i = 0; i < count; i++)
            {
                Vector4 b = buffer[i];
                if (b.w <= 0.001f) continue;
                var c = new Vector3(b.x, b.y, b.z);
                float tDepth = Vector3.Dot(c - ct.position, ct.forward);
                if (tDepth <= 0.05f || pDepth > tDepth - depthBias) continue;
                float rPx = b.w * p11 / (cam.orthographic ? 1f : tDepth) * halfH;
                rPx = Mathf.Max(rPx, minPx * Mathf.Clamp01(b.w * 4f));
                Vector3 cs = cam.WorldToScreenPoint(c);
                if (Vector2.Distance(new Vector2(ps.x, ps.y), new Vector2(cs.x, cs.y)) < rPx) return true;
            }
            return false;
        }

        private sealed class HitComparer : IComparer<RaycastHit>
        {
            public static readonly HitComparer Instance = new HitComparer();
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }
    }
}
