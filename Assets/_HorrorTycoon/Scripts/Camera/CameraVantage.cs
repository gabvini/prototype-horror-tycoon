using UnityEngine;

namespace HorrorTycoon.Cameras
{
    /// <summary>
    /// "Esconderijo" de câmera: um ponto no cenário (atrás de um arbusto, entre árvores) de onde
    /// o CinematicDirector pode filmar os atores de LONGE, com zoom de lente (teleobjetiva),
    /// como alguém observando escondido. O objeto fica na posição exata da lente.
    /// Quem monta o cenário espalha estes marcadores; sem nenhum, o diretor gera pontos sozinho.
    /// </summary>
    public class CameraVantage : MonoBehaviour
    {
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, 0.25f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 1.5f);
        }
    }
}
