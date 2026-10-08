using UnityEngine;

namespace HorrorTycoon.Rooms
{
    /// <summary>
    /// Onde está a casa no mundo (compartilhado com câmeras e monitor).
    /// A casa gerada (HouseBuilder) preenche ao montar; a planta fixa do P0 NÃO preenche, e quem lê
    /// usa os valores antigos (constantes) como padrão. Assim o P0 continua idêntico.
    /// Quem precisa reagir a uma casa nova compara 'Version' com o último valor visto.
    /// </summary>
    public static class HouseBounds
    {
        /// <summary>Meia diagonal da casa fixa do P0 (11 × 12 m). Base para escalar distâncias de câmera.</summary>
        public const float P0Radius = 8.1f;

        public static bool HasValue { get; private set; }
        /// <summary>Pegada no plano XZ: x = xMin, y = zMin.</summary>
        public static Rect Footprint { get; private set; }
        /// <summary>Centro da porta da frente (no chão).</summary>
        public static Vector3 FrontDoor { get; private set; }
        /// <summary>Muda a cada Set/Clear.</summary>
        public static int Version { get; private set; }

        public static Vector3 Center => new Vector3(Footprint.center.x, 0f, Footprint.center.y);
        /// <summary>Meia diagonal da pegada.</summary>
        public static float Radius => Footprint.size.magnitude * 0.5f;

        /// <summary>Fator para distâncias de câmera: 1 no P0 (ou sem casa gerada), maior para casas maiores.</summary>
        public static float Scale => HasValue ? Mathf.Max(1f, Radius / P0Radius) : 1f;

        public static void Set(Rect footprint, Vector3 frontDoor)
        {
            Footprint = footprint;
            FrontDoor = frontDoor;
            HasValue = true;
            Version++;
        }

        public static void Clear()
        {
            if (!HasValue) return;
            HasValue = false;
            Version++;
        }

        /// <summary>Com "Enter Play Mode" sem recarregar o domínio, estáticos sobrevivem entre execuções.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            HasValue = false;
            Footprint = default;
            FrontDoor = default;
            Version = 0;
        }
    }
}
