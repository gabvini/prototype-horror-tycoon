using System.Collections.Generic;
using UnityEngine;

namespace HorrorTycoon.UI
{
    /// <summary>
    /// Diz se o mouse está sobre a HUD, para que um clique numa carta não seja tratado também
    /// como clique no mundo 3D (ex.: focar um ator atrás dela).
    ///
    /// Duas fontes (a API pública não mudou):
    ///   - HUD antiga (IMGUI): registra as áreas a cada frame em coordenadas de GUI (Y para baixo).
    ///     Áreas de mais de 1 frame atrás são ignoradas (a HUD antiga pode ter sido desligada pelo F1).
    ///   - HUD nova (UI Toolkit): o HudRoot põe um "Picker" que pergunta ao painel (panel.Pick)
    ///     se há um elemento clicável naquele ponto.
    /// </summary>
    public static class HudInputBlocker
    {
        private static readonly List<Rect> rects = new List<Rect>();
        private static int frame = -1;

        /// <summary>HUD nova: devolve true se a posição de tela (Input System, Y para cima) está sobre um painel.</summary>
        public static System.Func<Vector2, bool> Picker;

        public static void BeginFrame()
        {
            if (frame == Time.frameCount) return;
            frame = Time.frameCount;
            rects.Clear();
        }

        public static void Register(Rect guiRect)
        {
            BeginFrame();
            rects.Add(guiRect);
        }

        /// <summary>screenPos vem do Input System (Y para cima) e é convertido para GUI (Y para baixo).</summary>
        public static bool IsPointerOverHud(Vector2 screenPos)
        {
            if (Picker != null && Picker(screenPos)) return true;
            if (frame < Time.frameCount - 1) return false; // áreas velhas: a HUD antiga não desenhou
            var guiPos = new Vector2(screenPos.x, Screen.height - screenPos.y);
            foreach (var r in rects)
            {
                if (r.Contains(guiPos)) return true;
            }
            return false;
        }
    }
}
