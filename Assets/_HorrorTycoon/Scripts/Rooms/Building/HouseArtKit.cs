using UnityEngine;

namespace HorrorTycoon.Rooms.Building
{
    /// <summary>
    /// KIT DE ARTE da casa gerada: tudo o que o HouseBuilder desenha vem daqui (ou do FurnitureKit / RoomDef.interiorPrefab).
    /// Para trocar a arte provisória pela oficial, troque os materiais/prefabs deste asset (ou crie outro kit e
    /// ligue no HouseBuilder da cena). Nenhuma regra de jogo depende deste asset.
    ///
    /// Campos vazios caem num padrão por código (material toon de cor chapada), então um kit "em branco" também funciona.
    /// Convenções dos prefabs: ver Docs/Tecnico/Geracao_Casa_Montagem3D.md §4.
    /// Criar: menu Horror Tycoon > Visual > Criar kit de arte da casa (não sobrescreve), ou Create > Horror Tycoon > Kit de Arte da Casa.
    /// </summary>
    [CreateAssetMenu(menuName = "Horror Tycoon/Kit de Arte da Casa", fileName = "HouseArtKit_Novo")]
    public class HouseArtKit : ScriptableObject
    {
        [Header("Medidas (metros)")]
        [Tooltip("Pé-direito (altura das paredes).")]
        [Min(1f)] public float wallHeight = 3f;
        [Min(0.02f)] public float wallThickness = 0.12f;
        [Tooltip("Altura do vão das portas (acima vai a verga).")]
        [Min(0.5f)] public float doorHeight = 2.3f;
        [Tooltip("Paredes compridas são divididas em pedaços de até este tamanho (o corte 'Sims' é por pedaço).")]
        [Min(0.5f)] public float maxWallPiece = 4f;

        [Header("Materiais da casa")]
        [Tooltip("Paredes internas (e externas, se o campo de baixo estiver vazio). O corte reescala a parede em Y.")]
        public Material wallMaterial;
        [Tooltip("Opcional: paredes externas (fachada).")]
        public Material exteriorWallMaterial;
        [Tooltip("Opcional: vergas (acima das portas). Vazio = material da parede.")]
        public Material lintelMaterial;
        [Tooltip("Piso. A COR vem do RoomDef (MaterialPropertyBlock _BaseColor): deixe o material claro.")]
        public Material floorMaterial;
        [Tooltip("Opcional: piso por tipo de espaço (vazio = 'floorMaterial').")]
        public Material roomFloorMaterial;
        public Material socialFloorMaterial;
        public Material corridorFloorMaterial;
        [Tooltip("Fundação (placa sob cada espaço).")]
        public Material foundationMaterial;
        [Tooltip("Batentes das portas.")]
        public Material trimMaterial;
        [Tooltip("Beiral no topo das paredes externas e toldo da varanda.")]
        public Material eaveMaterial;

        [Header("Névoa do cômodo não descoberto (só Salas)")]
        public Material fogVolumeMaterial;
        public Material fogLayerMaterial;
        public Material fogTapeMaterial;

        [Header("Lâmpada do teto")]
        [Min(0f)] public float lampIntensity = 2.6f;
        [Tooltip("Corredores (passagem) usam uma lâmpada um pouco mais fraca.")]
        [Min(0f)] public float corridorLampIntensity = 2.2f;
        [Tooltip("Alcance mínimo. Espaços compridos ganham alcance maior automaticamente.")]
        [Min(0.5f)] public float lampRange = 5.5f;
        [Min(0.5f)] public float lampHeight = 2.45f;
        public Material lampCordMaterial;
        public Material lampShadeMaterial;
        public Material lampBulbMaterial;
        public Material lampShaftMaterial;
        [Tooltip("Malha do raio de luz falso (cone aberto, ápice em y = +0,5).")]
        public Mesh lampShaftMesh;

        [Header("Janelas (paredes externas de Salas e Convivências)")]
        [Range(0f, 1f)] public float windowChance = 0.6f;
        public Material windowFrameMaterial;
        [Tooltip("Vidro: o RoomAnchor acende a emissão (_EmissionColor) quando o cômodo é descoberto.")]
        public Material windowPaneMaterial;
        public Material windowBeamMaterial;

        [Header("Fachada")]
        public bool eaves = true;
        public bool porch = true;
        public bool path = true;
        public Material deckMaterial;
        public Material postMaterial;
        public Material pathMaterial;
        public Material porchFixtureMaterial;
        [Min(0f)] public float porchLightIntensity = 2.4f;
        [Min(0.5f)] public float porchLightRange = 5.5f;

        [Header("Prefabs opcionais (arte oficial) — vazio = peça por código")]
        [Tooltip("Batente: origem no chão, no centro do vão; X local ao longo da parede; feito para vão de 1 m (escala X = largura).")]
        public GameObject doorFramePrefab;
        [Tooltip("Janela: origem no centro da janela; +Z local aponta para FORA da casa. Renderers com 'Vidro' no nome acendem.")]
        public GameObject windowPrefab;
        [Tooltip("Luminária do teto: origem no ponto da lâmpada. Todos os renderers somem com o cômodo escuro e piscam junto.")]
        public GameObject lampPrefab;
        [Tooltip("Varanda: origem no chão, no centro da porta da frente; +Z local aponta para a rua.")]
        public GameObject porchPrefab;

        [Header("Interiores")]
        [Tooltip("Remove móveis que fiquem na frente de qualquer porta/passagem (vale para FurnitureKit e prefabs).")]
        public bool clearDoorways = true;
        [Tooltip("Quanto da porta para dentro precisa ficar livre (m).")]
        [Min(0.2f)] public float doorwayClearDepth = 1.0f;

        public Material FloorFor(SpaceKind kind)
        {
            Material m = null;
            switch (kind)
            {
                case SpaceKind.Room: m = roomFloorMaterial; break;
                case SpaceKind.Social: m = socialFloorMaterial; break;
                case SpaceKind.Corridor: m = corridorFloorMaterial; break;
            }
            return m != null ? m : floorMaterial;
        }

        private void OnValidate()
        {
            if (doorHeight > wallHeight - 0.05f) doorHeight = wallHeight - 0.05f;
        }
    }
}
