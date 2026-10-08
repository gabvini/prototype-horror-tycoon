using HorrorTycoon.Actors;
using HorrorTycoon.UI;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HorrorTycoon.Cameras
{
    /// <summary>
    /// Clique em um ator -> a câmera faz um close no rosto dele.
    ///
    /// Usa DUAS câmeras Cinemachine:
    ///   CM_Iso   (prioridade 10) -> visão geral isométrica
    ///   CM_Focus (prioridade 0 / 20 quando ativa) -> close no rosto
    /// O Cinemachine Brain (na Main Camera) sempre mostra a de maior prioridade
    /// e faz a transição suave (blend) sozinho. Nós só trocamos a prioridade.
    ///
    /// Também é usado pelo RunPresenter para o close automático de reação após cada cena.
    ///
    /// Controles: clique esquerdo no ator = foco · Esc = voltar
    ///            1-4 (com foco) = testar expressões
    /// </summary>
    public class ActorFocusController : MonoBehaviour
    {
        [Header("Referências")]
        [SerializeField] private Camera mainCamera;
        [SerializeField] private CinemachineCamera focusCamera;
        [SerializeField] private IsoCameraRig isoRig;

        [Header("Enquadramento do close (de LONGE, com zoom de lente)")]
        [Tooltip("Distância da câmera à frente do rosto. Longe = 'alguém observando escondido'.")]
        [SerializeField] private float faceDistance = 9f;
        [SerializeField] private float heightOffset = 1.2f;
        [SerializeField] private float sideOffset = 2.5f;
        [Tooltip("Quantos metros cabem na vertical do quadro (1,1 = cabeça grande + ombros dos modelos do Blender).")]
        [SerializeField] private float frameHeight = 1.1f;

        [Header("Prioridades")]
        [SerializeField] private int focusPriority = 20;
        [SerializeField] private int idlePriority = 0;

        [Header("Entrada")]
        [Tooltip("Clique esquerdo num ator faz o close (desligado no P1: o clique seleciona; use F).")]
        [SerializeField] private bool clickToFocus = false;

        private ActorView currentActor;

        public ActorView CurrentActor => currentActor;

        /// <summary>Desligado enquanto uma cena está sendo "filmada" (o jogo controla a câmera).</summary>
        public bool AllowPlayerInput { get; set; } = true;

        private void Awake()
        {
            if (focusCamera != null)
            {
                focusCamera.Priority = idlePriority;
            }
        }

        private void Update()
        {
            if (!AllowPlayerInput)
            {
                return;
            }

            Mouse mouse = Mouse.current;
            Keyboard kb = Keyboard.current;
            if (mouse == null || kb == null)
            {
                return;
            }

            // P1: clique esquerdo agora SELECIONA o ator (RunPresenter). O close é pela tecla F.
            // Para voltar ao clique-para-close, ligue 'clickToFocus' no Inspector.
            if (clickToFocus && mouse.leftButton.wasPressedThisFrame)
            {
                Vector2 pos = mouse.position.ReadValue();
                if (!HudInputBlocker.IsPointerOverHud(pos))
                {
                    TryFocusUnderMouse(pos);
                }
            }

            if (currentActor != null)
            {
                if (kb.escapeKey.wasPressedThisFrame)
                {
                    Unfocus();
                    return;
                }

                // Teclas de teste de expressão (só para o greybox).
                if (kb.digit1Key.wasPressedThisFrame) currentActor.SetExpression(ActorExpression.Neutral);
                if (kb.digit2Key.wasPressedThisFrame) currentActor.SetExpression(ActorExpression.Happy);
                if (kb.digit3Key.wasPressedThisFrame) currentActor.SetExpression(ActorExpression.Tense);
                if (kb.digit4Key.wasPressedThisFrame) currentActor.SetExpression(ActorExpression.Scared);
            }
        }

        // LateUpdate roda depois de todos os Update: o ator já se moveu neste frame,
        // então posicionamos a câmera no lugar certo (evita "tremedeira").
        private void LateUpdate()
        {
            if (currentActor != null)
            {
                PlaceFocusCamera(currentActor);
            }
        }

        private void TryFocusUnderMouse(Vector2 screenPosition)
        {
            if (mainCamera == null)
            {
                return;
            }

            Ray ray = mainCamera.ScreenPointToRay(screenPosition);
            if (SeeThroughTargets.Raycast(ray, out RaycastHit hit, 200f))
            {
                ActorView actor = hit.collider.GetComponentInParent<ActorView>();
                if (actor != null)
                {
                    Focus(actor);
                }
            }
        }

        public void Focus(ActorView actor)
        {
            currentActor = actor;
            PlaceFocusCamera(actor);
            focusCamera.Priority = focusPriority;
            if (isoRig != null)
            {
                isoRig.InputEnabled = false;
            }
        }

        public void Unfocus()
        {
            currentActor = null;
            focusCamera.Priority = idlePriority;
            if (isoRig != null)
            {
                isoRig.InputEnabled = true;
            }
        }

        private void PlaceFocusCamera(ActorView actor)
        {
            Transform face = actor.FaceAnchor;
            Transform root = actor.transform;

            // Fica longe, na frente do rosto e de lado (3/4), um pouco acima.
            Vector3 position = face.position
                               + root.forward * faceDistance
                               + root.right * sideOffset
                               + Vector3.up * heightOffset;

            focusCamera.transform.SetPositionAndRotation(
                position,
                Quaternion.LookRotation(face.position - position, Vector3.up));

            // Zoom de lente: FOV pequeno para o rosto caber no quadro mesmo de longe.
            focusCamera.Lens.FieldOfView = CinematicDirector.FovFor(frameHeight, Vector3.Distance(position, face.position));
        }

        /// <summary>Linha de ajuda IMGUI na base da tela. A HUD nova desliga (a ajuda vai para o menu ≡).</summary>
        public static bool ShowHelpLine = true;

        private void OnGUI()
        {
            if (!ShowHelpLine) return;
            // Ajuda temporária (IMGUI), no canto inferior esquerdo.
            const string help =
                "Câmera: WASD/setas · Q/E ou botão direito: girar · scroll: zoom · botão do meio: arrastar (parado = modo filme)  |  " +
                "M: monitor do diretor · Clique na sala: card · Clique no ator: selecionar · Tab: próximo ator · F: close · Esc: fechar";
            GUI.Label(new Rect(10, Screen.height - 24, 1200, 20), help);
        }
    }
}
