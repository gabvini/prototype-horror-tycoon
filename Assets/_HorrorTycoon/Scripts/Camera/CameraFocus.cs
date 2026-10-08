using UnityEngine;

namespace HorrorTycoon.Cameras
{
    /// <summary>
    /// "Para onde a câmera está olhando agora", compartilhado com o corte de paredes (WallCutaway).
    /// Quem controla a câmera no momento (o CinematicDirector) atualiza este ponto todo frame.
    ///
    /// CutMargin: quanto uma parede pode ficar ALÉM do ponto de foco e ainda ser cortada.
    ///   0.6  = corta tudo entre a câmera e o foco (e um pouco além)   -> planos normais
    ///  -0.3  = só corta paredes claramente na frente do foco          -> plano da porta (a parede da porta fica)
    /// </summary>
    public static class CameraFocus
    {
        public static Vector3 Point { get; private set; }
        public static float CutMargin { get; private set; } = 0.6f;
        public static bool HasValue { get; private set; }

        public static void Set(Vector3 point, float cutMargin)
        {
            Point = point;
            CutMargin = cutMargin;
            HasValue = true;
        }
    }
}
