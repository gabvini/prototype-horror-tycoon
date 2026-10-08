using System.Collections;
using System.Collections.Generic;
using HorrorTycoon.Art;
using HorrorTycoon.Run;
using UnityEngine;
using UnityEngine.AI;

namespace HorrorTycoon.Rooms
{
    /// <summary>
    /// Um "SLOT" físico da planta (um retângulo de cômodo na casa).
    /// Qual cômodo ocupa o slot é decidido a cada run (salas sorteadas): o RunPresenter chama Bind().
    /// Cuida do visual do cômodo: cor do piso, móveis, luz do teto e névoa (escuro até ser descoberto).
    ///
    /// Visual (guia de arte §5/§6):
    ///   - Não descoberto: piso #0E0F17, fita crepe apagada e névoa animada (shader HorrorTycoon/FX).
    ///   - Descoberta: a névoa se dissolve em 0,6 s e a lâmpada acende com um "flicker" de partida.
    ///   - Lâmpada velha (RoomDef.FlickeringLight): ruído 0,85–1,0 a ~10 Hz + "apagões" de 0,05–0,25 s.
    ///   - Janelas da fachada acendem na cor da lâmpada quando o cômodo é descoberto.
    ///   - Camada de luz própria: a lâmpada deste slot só ilumina o piso e os móveis deste slot
    ///     (rendering layer copiada do piso para os móveis em Bind).
    /// </summary>
    public class RoomAnchor : MonoBehaviour
    {
        [Tooltip("Índice do slot na planta. 0 = corredor; 1..N = cômodos.")]
        [SerializeField] private int slotIndex;
        [SerializeField] private Vector2 size = new Vector2(4f, 4f);
        [SerializeField] private Renderer floorRenderer;
        [SerializeField] private Transform interior;
        [SerializeField] private GameObject fogCover;
        [SerializeField] private Light ceilingLight;
        [Tooltip("Centro da porta principal, em coordenadas locais (no chão).")]
        [SerializeField] private Vector3 doorLocal;
        [Tooltip("Direção (mundo) que sai do cômodo pela porta. Ex.: do cômodo para o corredor.")]
        [SerializeField] private Vector3 doorOutward = Vector3.back;
        [SerializeField] private Color highlightTint = new Color(1f, 0.85f, 0.4f);

        [Header("Visual (arte)")]
        [Tooltip("Vidros das janelas deste cômodo (acendem quando ele é descoberto).")]
        [SerializeField] private Renderer[] windowPanes = new Renderer[0];
        [Tooltip("Peças que brilham com a lâmpada (bulbo, cúpula, raio de luz). Piscam junto e somem com a luz apagada.")]
        [SerializeField] private Renderer[] lampGlows = new Renderer[0];
        [Tooltip("Cor do piso enquanto o cômodo não foi descoberto (guia: #0E0F17).")]
        [SerializeField] private Color undiscoveredFloor = new Color(0.055f, 0.059f, 0.09f);
        [Tooltip("Tempo do dissolve da névoa ao descobrir (s).")]
        [SerializeField] private float revealDuration = 0.6f;
        [Tooltip("Brilho das janelas acesas (multiplica a cor da lâmpada, HDR).")]
        [SerializeField] private float windowGlow = 1.4f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private MaterialPropertyBlock block;
        private int standCounter;
        private float baseIntensity;
        private bool flicker;
        private bool discovered = true;
        private bool initialized;
        private Coroutine revealRoutine;

        // Flicker (guia §6)
        private float nextDropout;
        private float dropoutUntil;
        private float noiseSeed;
        private float lightLevel = 1f;   // 0..1 aplicado à lâmpada e aos brilhos
        private float startupLevel = 1f; // flicker de partida (descoberta)

        private readonly List<Color> glowBase = new List<Color>();
        private readonly List<int> glowProperty = new List<int>();

        public int SlotIndex => slotIndex;
        public RoomDef Def { get; private set; }
        public Vector2 Size => size;
        public bool IsDiscovered => discovered;

        /// <summary>Centro da porta principal (no chão), em coordenadas do mundo.</summary>
        public Vector3 DoorPoint => transform.TransformPoint(doorLocal);

        /// <summary>Direção que sai do cômodo pela porta.</summary>
        public Vector3 DoorOutward => doorOutward;

        /// <summary>
        /// Opcional (casa gerada): quem monta o interior no Bind, no lugar do FurnitureKit direto.
        /// Recebe (este anchor, raiz "Interior", seed visual). O HouseBuilder usa para girar os móveis
        /// conforme a porta, usar RoomDef.interiorPrefab e liberar as portas. Null = comportamento do P0.
        /// </summary>
        public System.Action<RoomAnchor, Transform, int> InteriorOverride { get; set; }

        /// <summary>Usado pelo construtor de cena: onde fica a porta deste slot.</summary>
        public void ConfigureDoor(Vector3 localPoint, Vector3 outward)
        {
            doorLocal = localPoint;
            doorOutward = outward.normalized;
        }

        /// <summary>Prende um ponto dentro do retângulo do cômodo (com margem). Mantém a altura.</summary>
        public Vector3 ClampInside(Vector3 world, float margin)
        {
            Vector3 c = transform.position;
            float hx = Mathf.Max(0f, size.x * 0.5f - margin);
            float hz = Mathf.Max(0f, size.y * 0.5f - margin);
            world.x = Mathf.Clamp(world.x, c.x - hx, c.x + hx);
            world.z = Mathf.Clamp(world.z, c.z - hz, c.z + hz);
            return world;
        }

        /// <summary>Usado pelo construtor de cena.</summary>
        public void Configure(int slot, Vector2 roomSize, Renderer floor, Transform interiorRoot, GameObject fog, Light light)
        {
            slotIndex = slot;
            size = roomSize;
            floorRenderer = floor;
            interior = interiorRoot;
            fogCover = fog;
            ceilingLight = light;
        }

        /// <summary>Usado pelo construtor de cena: janelas e peças que brilham com a lâmpada.</summary>
        public void ConfigureVisuals(Renderer[] windows, Renderer[] glows)
        {
            windowPanes = windows ?? new Renderer[0];
            lampGlows = glows ?? new Renderer[0];
        }

        private void Awake()
        {
            if (ceilingLight != null) baseIntensity = ceilingLight.intensity;
            noiseSeed = Random.Range(0f, 100f);
            nextDropout = Time.time + Random.Range(3f, 9f);

            glowBase.Clear();
            glowProperty.Clear();
            foreach (var r in lampGlows)
            {
                Material m = r != null ? r.sharedMaterial : null;
                if (m != null && m.HasProperty(ToonMaterials.EmissionColorId) && ToonMaterials.IsToon(m))
                {
                    glowBase.Add(m.GetColor(ToonMaterials.EmissionColorId));
                    glowProperty.Add(ToonMaterials.EmissionColorId);
                }
                else if (m != null && m.HasProperty(BaseColorId))
                {
                    glowBase.Add(m.GetColor(BaseColorId));
                    glowProperty.Add(BaseColorId);
                }
                else
                {
                    glowBase.Add(Color.black);
                    glowProperty.Add(-1);
                }
            }
        }

        /// <summary>Liga este slot ao cômodo sorteado para ele nesta run e monta o visual.</summary>
        public void Bind(RoomRunState room, int seed)
        {
            Def = room.Def;
            if (interior != null)
            {
                for (int i = interior.childCount - 1; i >= 0; i--) Destroy(interior.GetChild(i).gameObject);
                if (InteriorOverride != null) InteriorOverride(this, interior, seed + slotIndex * 101);
                else FurnitureKit.Build(Def.Furniture, interior, size, seed + slotIndex * 101);

                // Os móveis entram na camada de luz do slot (a lâmpada não vaza para o vizinho).
                if (floorRenderer != null)
                {
                    uint mask = floorRenderer.renderingLayerMask;
                    foreach (var r in interior.GetComponentsInChildren<Renderer>(true)) r.renderingLayerMask = mask;
                }
            }

            if (ceilingLight != null)
            {
                ceilingLight.color = Def.LightColor;
                ceilingLight.intensity = baseIntensity; // desfaz qualquer piscada da run anterior
                flicker = Def.FlickeringLight;
            }

            lightLevel = 1f;
            startupLevel = 1f;
            ApplyGlow();
            ApplyBaseColor(false);
            ApplyWindows();
        }

        /// <summary>Névoa: cômodo ainda não visitado nesta run fica escuro e sem móveis visíveis.</summary>
        public void SetDiscovered(bool value)
        {
            bool animate = value && !discovered && initialized && Application.isPlaying && isActiveAndEnabled
                           && fogCover != null && fogCover.activeSelf;
            discovered = value;
            initialized = true;

            if (revealRoutine != null)
            {
                StopCoroutine(revealRoutine);
                revealRoutine = null;
            }

            if (interior != null) interior.gameObject.SetActive(value);
            ApplyBaseColor(false);
            ApplyWindows();

            if (animate)
            {
                revealRoutine = StartCoroutine(Reveal());
                return;
            }

            if (fogCover != null)
            {
                SetFogDissolve(0f);
                fogCover.SetActive(!value);
            }
            if (ceilingLight != null) ceilingLight.enabled = value;
            SetGlowVisible(value);
            startupLevel = 1f;
        }

        public void SetHighlighted(bool on) => ApplyBaseColor(on);

        /// <summary>Ponto livre perto do centro (espalha atores, e cai sempre no NavMesh).</summary>
        public Vector3 NextStandPoint()
        {
            float r = Mathf.Min(size.x, size.y) * 0.22f;
            float angle = standCounter * 137.5f * Mathf.Deg2Rad;
            standCounter++;
            Vector3 p = transform.position + new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
            if (NavMesh.SamplePosition(p, out NavMeshHit hit, 1.5f, NavMesh.AllAreas)) return hit.position;
            return p;
        }

        // ------------------------------------------------------------------ Descoberta

        private IEnumerator Reveal()
        {
            // 1) A lâmpada "pega no tranco" enquanto a névoa começa a sumir.
            if (ceilingLight != null) ceilingLight.enabled = true;
            SetGlowVisible(true);
            float[] steps = { 0.55f, 0f, 0.8f, 0.05f, 1f };
            float[] times = { 0.06f, 0.09f, 0.05f, 0.12f, 0f };
            float t = 0f;
            int step = 0;
            float stepEnd = times[0];
            while (t < revealDuration || step < steps.Length - 1)
            {
                t += Time.deltaTime;
                if (step < steps.Length - 1 && t >= stepEnd)
                {
                    step++;
                    stepEnd += times[step];
                }
                startupLevel = steps[step];
                SetFogDissolve(Mathf.Clamp01(t / Mathf.Max(0.01f, revealDuration)));
                UpdateLight();
                yield return null;
            }

            startupLevel = 1f;
            UpdateLight();
            SetFogDissolve(0f);
            if (fogCover != null) fogCover.SetActive(false);
            revealRoutine = null;
        }

        private void SetFogDissolve(float amount)
        {
            if (fogCover == null) return;
            block ??= new MaterialPropertyBlock();
            foreach (var r in fogCover.GetComponentsInChildren<Renderer>(true))
            {
                if (amount <= 0f)
                {
                    r.SetPropertyBlock(null);
                    continue;
                }
                r.GetPropertyBlock(block);
                block.SetFloat(ToonMaterials.DissolveId, amount);
                r.SetPropertyBlock(block);
            }
        }

        // ------------------------------------------------------------------ Lâmpada

        private void Update()
        {
            if (ceilingLight == null || !ceilingLight.enabled) return;
            if (!flicker && startupLevel >= 1f && lightLevel >= 1f) return;
            UpdateLight();
        }

        private void UpdateLight()
        {
            float level = 1f;
            if (flicker)
            {
                // Ruído de Perlin 0,85–1,0 a ~10 Hz + "apagão" até 10% a cada 3–9 s.
                float n = Mathf.PerlinNoise(noiseSeed, Time.time * 10f);
                level = Mathf.Lerp(0.85f, 1f, n);
                if (Time.time >= nextDropout)
                {
                    dropoutUntil = Time.time + Random.Range(0.05f, 0.25f);
                    nextDropout = Time.time + Random.Range(3f, 9f);
                }
                if (Time.time < dropoutUntil) level = 0.1f;
            }

            lightLevel = level * startupLevel;
            ceilingLight.intensity = baseIntensity * lightLevel;
            ApplyGlow();
        }

        private void SetGlowVisible(bool on)
        {
            foreach (var r in lampGlows)
            {
                if (r != null) r.enabled = on;
            }
        }

        private void ApplyGlow()
        {
            if (lampGlows.Length == 0 || glowBase.Count != lampGlows.Length) return;
            block ??= new MaterialPropertyBlock();
            Color tint = ceilingLight != null ? ceilingLight.color : Color.white;
            for (int i = 0; i < lampGlows.Length; i++)
            {
                var r = lampGlows[i];
                if (r == null || glowProperty[i] < 0) continue;
                Color c = glowBase[i];
                if (glowProperty[i] == BaseColorId)
                {
                    // Raio de luz (FX): cor da lâmpada, alpha acompanha o brilho.
                    c = new Color(tint.r, tint.g, tint.b, glowBase[i].a * lightLevel);
                }
                else
                {
                    float lum = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                    c = tint * (lum * lightLevel);
                }
                r.GetPropertyBlock(block);
                block.SetColor(glowProperty[i], c);
                r.SetPropertyBlock(block);
            }
        }

        // ------------------------------------------------------------------ Cores

        private void ApplyWindows()
        {
            if (windowPanes == null || windowPanes.Length == 0) return;
            block ??= new MaterialPropertyBlock();
            Color lightColor = Def != null ? Def.LightColor : new Color(1f, 0.7f, 0.36f);
            Color glow = discovered ? lightColor * windowGlow : new Color(0.02f, 0.025f, 0.05f);
            foreach (var r in windowPanes)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor(ToonMaterials.EmissionColorId, glow);
                r.SetPropertyBlock(block);
            }
        }

        private void ApplyBaseColor(bool highlighted)
        {
            if (floorRenderer == null || Def == null) return;
            block ??= new MaterialPropertyBlock();
            floorRenderer.GetPropertyBlock(block);
            Color c = discovered ? Def.FloorColor : undiscoveredFloor;
            if (highlighted) c = Color.Lerp(c, highlightTint, 0.45f);
            block.SetColor(BaseColorId, c);
            floorRenderer.SetPropertyBlock(block);
        }
    }
}
