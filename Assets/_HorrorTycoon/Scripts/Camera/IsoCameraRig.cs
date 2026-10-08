using HorrorTycoon.Rooms;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HorrorTycoon.Cameras
{
    /// <summary>
    /// Câmera isométrica do jogo (modo "diretor olhando o set").
    ///
    /// Como funciona:
    ///   CameraRig (este objeto)  -> pivô no chão. Mover = pan, girar no eixo Y = rotação.
    ///   └── CameraArm            -> "braço" inclinado (pitch) e afastado (distância = zoom).
    ///        └── CinemachineCamera (CM_Iso) sem componentes de movimento:
    ///            ela só copia a posição do braço.
    ///
    /// Controles: WASD/setas = mover · botão do meio arrastado = mover
    ///            Q/E = girar 45° · botão direito arrastado = girar livre · scroll = zoom
    ///
    /// Também pode SEGUIR um alvo (o ator selecionado): o pivô desliza até ele.
    /// Mexer na câmera com WASD/botão do meio solta o alvo.
    ///
    /// No P1 esta é a "VISÃO DA CASA": o CinematicDirector entrega a tela para ela quando o jogador
    /// mexe na câmera (LastInputTime) e volta ao modo cinematográfico depois de alguns segundos parado.
    /// </summary>
    public class IsoCameraRig : MonoBehaviour
    {
        [Header("Referências")]
        [SerializeField] private Transform cameraArm;

        [Header("Mover (pan)")]
        [SerializeField] private float panSpeed = 8f;
        [SerializeField] private float mouseDragPanSpeed = 0.02f;
        [SerializeField] private Vector2 panLimitsX = new Vector2(-10f, 10f);
        [SerializeField] private Vector2 panLimitsZ = new Vector2(-12f, 10f);

        [Header("Seguir alvo")]
        [SerializeField] private float followSmoothing = 4f;

        [Header("Girar")]
        [SerializeField] private float rotateStep = 45f;
        [SerializeField] private float mouseDragRotateSpeed = 0.25f;
        [SerializeField] private float rotateSmoothing = 10f;

        [Header("Zoom")]
        [SerializeField] private float pitch = 50f;
        [SerializeField] private float minDistance = 5f;
        [SerializeField] private float maxDistance = 24f;
        [SerializeField] private float startDistance = 17f;
        [SerializeField] private float zoomStep = 1.5f;
        [SerializeField] private float zoomSmoothing = 10f;

        [Header("Visão da casa (deriva automática)")]
        [Tooltip("Graus por segundo que a câmera gira sozinha quando o jogador para de mexer (só com AutoDrift ligado).")]
        [SerializeField] private float driftDegreesPerSecond = 4f;
        [SerializeField] private float driftDelay = 2f;
        [Tooltip("Zoom que 'respira' na visão da casa (metros para mais/menos) e o período em segundos.")]
        [SerializeField] private float breatheAmount = 2.5f;
        [SerializeField] private float breathePeriod = 26f;

        private float targetYaw;
        private float currentYaw;
        private float targetDistance;
        private float currentDistance;

        private Transform followTarget;
        private Vector3? glideGoal;
        private float breatheWeight;
        private int seenHouseVersion;

        // Limites efetivos: os do Inspector (casa fixa do P0) ou ampliados para caber a casa gerada (HouseBounds).
        private Vector2 LimitsX => HouseBounds.HasValue
            ? new Vector2(Mathf.Min(panLimitsX.x, HouseBounds.Footprint.xMin - 4.5f), Mathf.Max(panLimitsX.y, HouseBounds.Footprint.xMax + 4.5f))
            : panLimitsX;
        private Vector2 LimitsZ => HouseBounds.HasValue
            ? new Vector2(Mathf.Min(panLimitsZ.x, HouseBounds.Footprint.yMin - 6f), Mathf.Max(panLimitsZ.y, HouseBounds.Footprint.yMax + 4f))
            : panLimitsZ;
        private float MaxDistance => HouseBounds.HasValue
            ? Mathf.Max(maxDistance, Mathf.Max(HouseBounds.Footprint.width, HouseBounds.Footprint.height) * 1.6f)
            : maxDistance;

        /// <summary>A câmera ativa (usada pelo corte de paredes).</summary>
        public static IsoCameraRig Active { get; private set; }

        /// <summary>Desligado enquanto a câmera de foco está ativa.</summary>
        public bool InputEnabled { get; set; } = true;

        /// <summary>Direção "para onde a câmera olha", no chão (sem a inclinação).</summary>
        public Vector3 FlatForward => transform.forward;

        /// <summary>Faz o pivô seguir este alvo (null = parar de seguir).</summary>
        public void Follow(Transform target) => followTarget = target;

        /// <summary>Momento (Time.unscaledTime) do último comando de câmera do jogador.</summary>
        public float LastInputTime { get; private set; } = -999f;

        /// <summary>Ligado pelo diretor na "visão da casa": gira devagar quando o jogador para de mexer.</summary>
        public bool AutoDrift { get; set; }

        /// <summary>Afasta a câmera até pelo menos esta distância (zoom out), sem aproximar.</summary>
        public void ZoomOutAtLeast(float distance)
        {
            targetDistance = Mathf.Clamp(Mathf.Max(targetDistance, distance), minDistance, MaxDistance);
        }

        /// <summary>Desliza o pivô até um ponto e ajusta o zoom (ex.: card de sala aberto). Mexer cancela.</summary>
        public void GlideTo(Vector3 point, float distance)
        {
            followTarget = null;
            point.y = transform.position.y;
            glideGoal = point;
            targetDistance = Mathf.Clamp(distance, minDistance, MaxDistance);
        }

        private void OnEnable() => Active = this;
        private void OnDisable()
        {
            if (Active == this) Active = null;
        }

        private void Start()
        {
            targetYaw = transform.eulerAngles.y;
            currentYaw = targetYaw;
            targetDistance = Mathf.Clamp(startDistance, minDistance, MaxDistance);
            currentDistance = targetDistance;
            ApplyArm();
        }

        private void Update()
        {
            // Casa gerada nova: o pivô vai para o centro dela (uma vez por casa). P0: nunca acontece.
            if (HouseBounds.Version != seenHouseVersion)
            {
                seenHouseVersion = HouseBounds.Version;
                if (HouseBounds.HasValue && followTarget == null && !glideGoal.HasValue)
                {
                    Vector3 c = HouseBounds.Center;
                    transform.position = new Vector3(c.x, transform.position.y, c.z);
                }
            }

            if (InputEnabled)
            {
                ReadInput();
            }

            bool idleDrift = AutoDrift && Time.unscaledTime - LastInputTime > driftDelay;
            if (idleDrift)
            {
                targetYaw += driftDegreesPerSecond * Time.unscaledDeltaTime;
            }
            // "Respiração" do zoom: entra e sai devagar só quando ninguém mexe.
            breatheWeight = Mathf.MoveTowards(breatheWeight, idleDrift ? 1f : 0f, Time.unscaledDeltaTime * 0.3f);

            if (glideGoal.HasValue)
            {
                float glideT = 1f - Mathf.Exp(-3f * Time.unscaledDeltaTime);
                transform.position = Vector3.Lerp(transform.position, glideGoal.Value, glideT);
                if ((transform.position - glideGoal.Value).sqrMagnitude < 0.0025f) glideGoal = null;
            }

            // Suavização independente de FPS (equivalente a um lerp "por segundo").
            float rotT = 1f - Mathf.Exp(-rotateSmoothing * Time.unscaledDeltaTime);
            float zoomT = 1f - Mathf.Exp(-zoomSmoothing * Time.unscaledDeltaTime);
            currentYaw = Mathf.LerpAngle(currentYaw, targetYaw, rotT);
            currentDistance = Mathf.Lerp(currentDistance, targetDistance, zoomT);

            transform.rotation = Quaternion.Euler(0f, currentYaw, 0f);

            if (followTarget != null)
            {
                float followT = 1f - Mathf.Exp(-followSmoothing * Time.unscaledDeltaTime);
                Vector3 goal = followTarget.position;
                goal.y = transform.position.y;
                transform.position = Vector3.Lerp(transform.position, goal, followT);
            }

            ApplyArm();
        }

        private void ReadInput()
        {
            Keyboard kb = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (kb == null || mouse == null)
            {
                return;
            }

            // --- Pan pelo teclado (relativo para onde a câmera está olhando) ---
            Vector2 move = Vector2.zero;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1f;

            Vector3 forward = transform.forward;
            Vector3 right = transform.right;
            Vector3 delta = (forward * move.y + right * move.x) * panSpeed * Time.unscaledDeltaTime;

            // --- Pan arrastando o botão do meio ---
            if (mouse.middleButton.isPressed)
            {
                Vector2 md = mouse.delta.ReadValue();
                float zoomFactor = currentDistance / 10f;
                delta += (-right * md.x - forward * md.y) * mouseDragPanSpeed * zoomFactor;
            }

            bool anyInput = false;
            if (delta.sqrMagnitude > 0.000001f)
            {
                followTarget = null; // o jogador assumiu a câmera
                glideGoal = null;
                anyInput = true;
            }

            Vector3 pos = transform.position + delta;
            Vector2 limX = LimitsX, limZ = LimitsZ;
            pos.x = Mathf.Clamp(pos.x, limX.x, limX.y);
            pos.z = Mathf.Clamp(pos.z, limZ.x, limZ.y);
            transform.position = pos;

            // --- Rotação ---
            if (kb.qKey.wasPressedThisFrame) { targetYaw -= rotateStep; anyInput = true; }
            if (kb.eKey.wasPressedThisFrame) { targetYaw += rotateStep; anyInput = true; }
            if (mouse.rightButton.isPressed)
            {
                float dx = mouse.delta.ReadValue().x;
                targetYaw += dx * mouseDragRotateSpeed;
                if (Mathf.Abs(dx) > 0.01f) anyInput = true;
            }

            // --- Zoom ---
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f && !HorrorTycoon.UI.HudInputBlocker.IsPointerOverHud(mouse.position.ReadValue())) // rolar o card não dá zoom
            {
                targetDistance = Mathf.Clamp(targetDistance - Mathf.Sign(scroll) * zoomStep, minDistance, MaxDistance);
                anyInput = true;
            }

            if (anyInput) LastInputTime = Time.unscaledTime;
        }

        private void ApplyArm()
        {
            if (cameraArm == null)
            {
                return;
            }

            Quaternion armRotation = Quaternion.Euler(pitch, 0f, 0f);
            cameraArm.localRotation = armRotation;
            float breathe = Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / Mathf.Max(1f, breathePeriod)) * breatheAmount * breatheWeight;
            cameraArm.localPosition = armRotation * new Vector3(0f, 0f, -(currentDistance + breathe));
        }
    }
}
