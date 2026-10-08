using UnityEngine;

namespace HorrorTycoon.Cameras
{
    /// <summary>
    /// Marca um objeto como alvo (ou NÃO alvo) do buraco de visão. Opcional: atores vivos já entram sozinhos.
    /// Gancho para o vilão: isVillain = true → nunca abre buraco (vilão escondido atrás da parede é tensão, não bug).
    /// </summary>
    public class SeeThroughSubject : MonoBehaviour
    {
        [Tooltip("Vilão: nunca abre buraco (fica escondido).")]
        public bool isVillain;
        [Tooltip("Multiplica o raio do buraco deste alvo.")]
        public float radiusScale = 1f;
        [Tooltip("Altura do centro do buraco acima do pivô (m).")]
        public float centerHeight = 1f;
    }
}
