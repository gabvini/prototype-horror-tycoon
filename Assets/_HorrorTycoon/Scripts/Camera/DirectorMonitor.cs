using HorrorTycoon.Rooms;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HorrorTycoon.Cameras
{
    /// <summary>
    /// MONITOR DO DIRETOR: uma segunda câmera, vista de cima, que desenha a planta da casa numa
    /// textura (RenderTexture). A HUD mostra essa textura num canto da tela, e o jogador clica
    /// nela para escolher salas e atores enquanto a cena continua rolando na tela principal.
    ///
    /// Tecla M (ou o botão da HUD) abre e fecha. Fechado, a câmera do monitor nem renderiza.
    /// Enquadramento: o da cena (casa fixa do P0); com casa gerada (HouseBounds), reenquadra para caber a pegada.
    /// </summary>
    public class DirectorMonitor : MonoBehaviour
    {
        [SerializeField] private Camera monitorCamera;
        [SerializeField] private Vector2Int resolution = new Vector2Int(640, 480);
        [SerializeField] private bool startOpen = false;

        private RenderTexture texture;
        private int seenHouseVersion;

        public bool IsOpen { get; private set; }
        public Texture Texture => texture;
        public Camera Camera => monitorCamera;

        /// <summary>Área (coordenadas de GUI, Y para baixo) onde a HUD desenhou a imagem neste frame.</summary>
        public Rect GuiRect { get; set; }

        public float Aspect => (float)resolution.x / resolution.y;

        private void Awake()
        {
            if (monitorCamera == null) return;
            texture = new RenderTexture(resolution.x, resolution.y, 16) { name = "RT_MonitorDoDiretor" };
            monitorCamera.targetTexture = texture;
            SetOpen(startOpen);
        }

        private void OnDestroy()
        {
            if (monitorCamera != null) monitorCamera.targetTexture = null;
            if (texture != null) texture.Release();
        }

        private void Update()
        {
            if (HouseBounds.Version != seenHouseVersion)
            {
                seenHouseVersion = HouseBounds.Version;
                if (HouseBounds.HasValue) FitToHouse(HouseBounds.Footprint);
            }

            var kb = Keyboard.current;
            if (kb != null && kb.mKey.wasPressedThisFrame) Toggle();
        }

        public void Toggle() => SetOpen(!IsOpen);

        /// <summary>
        /// Câmera ortográfica de cima centrada na casa, com folga para a varanda/quintal da frente.
        /// Mesma regra que dá os valores da cena do P0 (casa 11 × 12: tamanho 8,5, z = -1,5).
        /// </summary>
        public void FitToHouse(Rect footprint)
        {
            if (monitorCamera == null) return;
            Vector2 c = footprint.center;
            float size = Mathf.Max(footprint.height * 0.5f + 2.5f, (footprint.width * 0.5f + 1.5f) / Aspect);
            monitorCamera.orthographicSize = size;
            Vector3 p = monitorCamera.transform.position;
            monitorCamera.transform.position = new Vector3(c.x, p.y, c.y - 1.5f);
        }

        public void SetOpen(bool open)
        {
            IsOpen = open && monitorCamera != null;
            if (monitorCamera != null) monitorCamera.enabled = IsOpen;
        }

        /// <summary>
        /// Se o mouse (posição do Input System, Y para cima) está sobre a imagem do monitor,
        /// devolve o raio "da câmera do monitor" que passa por aquele ponto.
        /// </summary>
        public bool TryGetRay(Vector2 screenPos, out Ray ray)
        {
            ray = default;
            if (!IsOpen || monitorCamera == null) return false;
            var gui = new Vector2(screenPos.x, Screen.height - screenPos.y);
            Rect r = GuiRect;
            if (r.width <= 0f || !r.Contains(gui)) return false;
            var viewport = new Vector2((gui.x - r.x) / r.width, 1f - (gui.y - r.y) / r.height);
            ray = monitorCamera.ViewportPointToRay(viewport);
            return true;
        }

        /// <summary>Converte um ponto do mundo para a posição dentro da imagem do monitor (GUI).</summary>
        public bool WorldToGui(Vector3 world, out Vector2 gui)
        {
            gui = default;
            if (monitorCamera == null) return false;
            Vector3 v = monitorCamera.WorldToViewportPoint(world);
            if (v.z < 0f || v.x < 0f || v.x > 1f || v.y < 0f || v.y > 1f) return false;
            Rect r = GuiRect;
            gui = new Vector2(r.x + v.x * r.width, r.y + (1f - v.y) * r.height);
            return true;
        }
    }
}
