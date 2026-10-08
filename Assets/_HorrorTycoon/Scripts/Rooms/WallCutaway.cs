using HorrorTycoon.Cameras;
using UnityEngine;

namespace HorrorTycoon.Rooms
{
    /// <summary>
    /// LEGADO (desligado por padrão): o jogo agora usa o BURACO DE VISÃO (SeeThroughTargets + keyword _SEETHROUGH
    /// do HT_Toon): as paredes ficam SEMPRE inteiras. O componente continua em cada parede porque os construtores e
    /// a câmera usam ele como "marcador de parede" (raycast de oclusão, clique através do buraco, planos de cinema)
    /// e as APIs (Setup, AddAttachment) seguem valendo. Para voltar ao corte antigo: WallCutaway.LegacyLowering = true.
    ///
    /// Corte de parede estilo "The Sims" (legado): paredes que ficam ENTRE a câmera e o ponto que ela olha
    /// abaixam (viram rodapé), para dá para ver dentro dos cômodos. As do fundo ficam inteiras.
    ///
    /// O "ponto que ela olha" vem do CameraFocus (atualizado por quem controla a câmera:
    /// plano cinematográfico, visão da casa ou zoom na porta).
    ///
    /// Cada pedaço de parede tem este componente. 'normal' é a direção para onde a face da parede aponta
    /// (para paredes internas, qualquer um dos dois lados serve: usamos o valor absoluto).
    /// </summary>
    public class WallCutaway : MonoBehaviour
    {
        [SerializeField] private Vector3 normal = Vector3.forward;
        [SerializeField] private float fullHeight = 2.7f;
        [SerializeField] private float cutHeight = 0.35f;
        [SerializeField] private float baseY = 0f;
        [Tooltip("Peças acima de portas (verga): somem quando a parede é cortada.")]
        [SerializeField] private bool isLintel;
        [SerializeField] private float speed = 8f;
        [Tooltip("Enfeites presos a este trecho (janela, beiral, chaminé...): somem quando a parede é cortada.")]
        [SerializeField] private System.Collections.Generic.List<GameObject> attachments = new System.Collections.Generic.List<GameObject>();

        /// <summary>Liga o corte antigo (paredes abaixam). Padrão: false (paredes inteiras, buraco de visão).</summary>
        public static bool LegacyLowering = false;

        private float currentHeight;
        private bool attachmentsVisible = true;

        /// <summary>Usado pelo construtor de cena: prende um enfeite que some com o corte.</summary>
        public void AddAttachment(GameObject go)
        {
            if (go != null && !attachments.Contains(go)) attachments.Add(go);
        }

        public void Setup(Vector3 faceNormal, float height, float bottom, bool lintel)
        {
            normal = faceNormal.normalized;
            fullHeight = height;
            baseY = bottom;
            isLintel = lintel;
            currentHeight = height;
            Apply();
        }

        private void Start()
        {
            currentHeight = fullHeight;
        }

        private void LateUpdate()
        {
            if (!LegacyLowering)
            {
                // Paredes inteiras (se o legado foi desligado no meio do jogo, volta à altura cheia).
                if (Mathf.Abs(currentHeight - fullHeight) > 0.001f)
                {
                    currentHeight = fullHeight;
                    Apply();
                }
                return;
            }

            Camera cam = Camera.main;
            if (cam == null || !CameraFocus.HasValue) return;

            Vector3 focus = CameraFocus.Point;

            // Direção da câmera até o foco, no chão. Se a câmera estiver bem em cima, usa a direção dela.
            Vector3 fwd = focus - cam.transform.position;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f)
            {
                fwd = cam.transform.forward;
                fwd.y = 0f;
            }
            fwd.Normalize();

            Vector3 toWall = transform.position - focus;
            toWall.y = 0f;

            // Corta se a parede está virada para a câmera (não paralela à direção do olhar)
            // e fica do lado da câmera em relação ao foco.
            bool facing = Mathf.Abs(Vector3.Dot(normal, fwd)) > 0.35f;
            bool inFront = Vector3.Dot(toWall, fwd) < CameraFocus.CutMargin;
            bool cut = facing && inFront;

            float target = cut ? (isLintel ? 0f : cutHeight) : fullHeight;
            float t = 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime);
            float next = Mathf.Lerp(currentHeight, target, t);
            if (Mathf.Abs(next - currentHeight) > 0.001f)
            {
                currentHeight = next;
                Apply();
            }
        }

        private void Apply()
        {
            Vector3 s = transform.localScale;
            s.y = Mathf.Max(0.001f, currentHeight);
            transform.localScale = s;
            Vector3 p = transform.position;
            p.y = baseY + s.y * 0.5f;
            transform.position = p;

            // Enfeites: aparecem só com a parede (quase) inteira.
            bool show = currentHeight > fullHeight * 0.85f;
            if (show != attachmentsVisible)
            {
                attachmentsVisible = show;
                foreach (var go in attachments)
                {
                    if (go != null) go.SetActive(show);
                }
            }
        }
    }
}
