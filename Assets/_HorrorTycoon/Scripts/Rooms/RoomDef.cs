using System;
using System.Collections.Generic;
using HorrorTycoon.Core;
using HorrorTycoon.Run;
using HorrorTycoon.Scoring;
using UnityEngine;

namespace HorrorTycoon.Rooms
{
    /// <summary>
    /// Tipo de espaço da casa. Sala = conta como sala (encontro/exploração).
    /// Convivência = atores param aqui, sem encontro (ex.: Hall, Sala de jantar).
    /// Corredor = só passagem (atores atravessam, não param).
    /// ATENÇÃO: só acrescente valores NO FIM (o valor é salvo como número nos assets).
    /// </summary>
    public enum SpaceKind
    {
        Room,
        Social,
        Corridor
    }

    /// <summary>
    /// Zona do terreno da casa por escolha (Protótipo 2 §4). Cada célula da grade tem UMA zona;
    /// cada RoomDef diz em quais zonas pode aparecer (pode marcar várias).
    /// Porão fica em "Íntima/fundos" por enquanto (andar de baixo = extensão futura).
    /// ATENÇÃO: só acrescente valores NO FIM (bits salvos como número nos assets).
    /// </summary>
    [Flags]
    public enum HouseZone
    {
        None = 0,
        /// <summary>Frente: hall, sala.</summary>
        Social = 1,
        /// <summary>Meio: cozinha, banheiro, corredor.</summary>
        Service = 2,
        /// <summary>Fundos: quarto, sótão, porão.</summary>
        Private = 4,
    }

    /// <summary>
    /// Lados com porta de uma sala, NA SALA SEM GIRO (norte = fundos da casa, sul = frente).
    /// A casa por escolha gira a sala (0/90/180/270) para uma das portas ficar de frente para a porta aberta.
    /// </summary>
    [Flags]
    public enum DoorSides
    {
        None = 0,
        North = 1,
        East = 2,
        South = 4,
        West = 8,
        All = North | East | South | West,
    }

    /// <summary>
    /// Definição de uma sala (Cozinha, Porão...). O lugar físico na cena é o RoomAnchor.
    /// No P1 a sala tem: dicas visíveis (informação parcial), encontros possíveis com pesos,
    /// cenas que podem ser dirigidas nela ("palco") e um ponto trancado opcional (ferramenta).
    /// Criar nova: Create > Horror Tycoon > Sala.
    /// </summary>
    [CreateAssetMenu(menuName = "Horror Tycoon/Sala", fileName = "Sala_Nova")]
    public class RoomDef : ScriptableObject
    {
        [Serializable]
        public class WeightedEncounter
        {
            public EncounterDef encounter;
            [Min(0f)] public float weight = 1f;
        }

        [Serializable]
        public class LockedSpot
        {
            [Tooltip("Nome do ponto (ex.: 'Alçapão'). Vazio = sala sem ponto trancado.")]
            public string name = "";
            public ToolDef requiredTool;
            [Tooltip("O que o ponto dá ao ator que abrir.")]
            public ElementDef reward;
            [TextArea] public string text = "";
        }

        [SerializeField] private string displayName = "Sala";
        [SerializeField, TextArea] private string description = "";
        [SerializeField] private Color floorColor = Color.gray;
        [SerializeField] private List<TagDef> tags = new List<TagDef>();

        [Header("Geração da casa")]
        [Tooltip("Sala (encontro), Convivência (atores param, sem encontro) ou Corredor (só passagem).")]
        [SerializeField] private SpaceKind kind = SpaceKind.Room;
        [Tooltip("Tamanho em metros (largura x profundidade). Pode ser girado pelo gerador.")]
        [SerializeField] private Vector2Int size = new Vector2Int(4, 4);
        [Tooltip("O gerador pode girar a sala 90° para encaixar.")]
        [SerializeField] private bool allowRotation = true;
        [Tooltip("Chance relativa de ser sorteada para a casa (0 = nunca, a não ser que seja obrigatória).")]
        [SerializeField, Min(0f)] private float weight = 1f;
        [Tooltip("Quantas vezes pode aparecer na mesma casa.")]
        [SerializeField, Min(1)] private int maxPerRun = 1;
        [Tooltip("Aparece em toda casa gerada.")]
        [SerializeField] private bool required;
        [Tooltip("Opcional: prefab de interior (arte oficial). Se vazio, os móveis vêm do FurnitureKit. A lógica ignora.")]
        [SerializeField] private GameObject interiorPrefab;

        [Header("Casa por escolha (Protótipo 2 §4 — experimento)")]
        [Tooltip("Zonas do terreno onde a sala pode ser sorteada no draft. Nada marcado = qualquer zona.")]
        [SerializeField] private HouseZone draftZones = HouseZone.None;
        [Tooltip("Lados com porta na sala SEM GIRO (o draft gira a sala para uma porta encaixar). Nada marcado = os 4 lados.")]
        [SerializeField] private DoorSides doorSides = DoorSides.None;
        [Tooltip("Só aparece na casa por escolha (fica fora da casa fixa P0 e da casa gerada P2).")]
        [SerializeField] private bool draftOnly;
        [Tooltip("Nunca é oferecida no draft (ex.: o Hall, que já começa na casa).")]
        [SerializeField] private bool excludeFromDraft;

        [Header("Visual")]
        [Tooltip("Que móveis o cômodo ganha (montados por código no FurnitureKit).")]
        [SerializeField] private FurnitureStyle furniture = FurnitureStyle.None;
        [Tooltip("Cor da luz do teto.")]
        [SerializeField] private Color lightColor = new Color(1f, 0.82f, 0.6f);
        [Tooltip("A luz pisca? (clima)")]
        [SerializeField] private bool flickeringLight;

        [Header("Informação parcial (o que o jogador vê)")]
        [Tooltip("Dicas curtas mostradas sobre a sala. Sem números!")]
        [SerializeField] private List<string> hints = new List<string>();

        [Header("Encontros possíveis (sorteia 1 por ato)")]
        [SerializeField] private List<WeightedEncounter> encounters = new List<WeightedEncounter>();

        [Header("Palco: cenas que podem ser dirigidas aqui")]
        [SerializeField] private List<PayoffDef> stagePayoffs = new List<PayoffDef>();

        [Header("Ponto trancado (opcional)")]
        [SerializeField] private LockedSpot lockedSpot = new LockedSpot();

        [Header("Build do Filme (Protótipo 3)")]
        [Tooltip("Pavor que cada ator aqui ganha a cada CENA gravada (sala escura/perigosa). 0 = nada.")]
        [SerializeField] private int pavorPerScene;
        [Tooltip("Plots de permanência que a sala oferece na 1ª exploração da run.")]
        [SerializeField] private List<PlotDef> plots = new List<PlotDef>();
        [Tooltip("Começa LACRADA: ninguém entra até um plot liberar (PlotDef.unlockRoom).")]
        [SerializeField] private bool startsSealed;
        [Tooltip("Texto do card enquanto lacrada (dica de como abrir).")]
        [SerializeField, TextArea] private string sealedText = "";

        public string DisplayName => displayName;
        public string Description => description;
        public Color FloorColor => floorColor;
        public IReadOnlyList<TagDef> Tags => tags;
        public FurnitureStyle Furniture => furniture;
        public Color LightColor => lightColor;
        public bool FlickeringLight => flickeringLight;
        public IReadOnlyList<string> Hints => hints;
        public IReadOnlyList<WeightedEncounter> Encounters => encounters;
        public IReadOnlyList<PayoffDef> StagePayoffs => stagePayoffs;
        public LockedSpot Locked => lockedSpot;
        public int PavorPerScene => pavorPerScene;
        public IReadOnlyList<PlotDef> Plots => plots;
        public bool StartsSealed => startsSealed;
        public string SealedText => sealedText;
        public bool HasLockedSpot => lockedSpot != null && !string.IsNullOrEmpty(lockedSpot.name) && lockedSpot.requiredTool != null;

        public SpaceKind Kind => kind;
        /// <summary>Tamanho em metros. Valores inválidos (0 ou negativos) caem no padrão 4x4.</summary>
        public Vector2Int Size => new Vector2Int(size.x > 0 ? size.x : 4, size.y > 0 ? size.y : 4);
        public bool AllowRotation => allowRotation;
        public float Weight => weight;
        public int MaxPerRun => Mathf.Max(1, maxPerRun);
        public bool Required => required;
        public GameObject InteriorPrefab => interiorPrefab;

        public bool HasTag(TagDef tag) => tags.Contains(tag);

        /// <summary>Zonas do draft (None no asset = qualquer zona).</summary>
        public HouseZone DraftZones => draftZones == HouseZone.None ? (HouseZone.Social | HouseZone.Service | HouseZone.Private) : draftZones;
        /// <summary>Zonas como estão no asset (None = não configurado).</summary>
        public HouseZone DraftZonesRaw => draftZones;
        /// <summary>Lados com porta sem giro (None no asset = os 4).</summary>
        public DoorSides Doors => doorSides == DoorSides.None ? DoorSides.All : doorSides;
        public DoorSides DoorSidesRaw => doorSides;
        public bool DraftOnly => draftOnly;
        public bool ExcludeFromDraft => excludeFromDraft;
        public bool AllowsZone(HouseZone zone) => (DraftZones & zone) != 0;

        /// <summary>Campos da casa por escolha (ferramentas de editor e testes).</summary>
        public void SetupDraft(HouseZone zones, DoorSides doors, bool onlyInDraft = false, bool neverInDraft = false)
        {
            draftZones = zones;
            doorSides = doors;
            draftOnly = onlyInDraft;
            excludeFromDraft = neverInDraft;
        }

        /// <summary>Campos da geração procedural (ferramentas de editor e testes).</summary>
        public void SetupGen(SpaceKind spaceKind, Vector2Int sizeMeters, float pickWeight = 1f, int maxPer = 1,
            bool isRequired = false, bool canRotate = true)
        {
            kind = spaceKind;
            size = sizeMeters;
            weight = pickWeight;
            maxPerRun = maxPer;
            required = isRequired;
            allowRotation = canRotate;
        }

        /// <summary>Campos do Protótipo 3 (ferramentas de editor e testes).</summary>
        public void SetupBuild(int pavorEachScene, List<PlotDef> plotList = null, bool sealedAtStart = false, string sealedHint = "")
        {
            pavorPerScene = pavorEachScene;
            plots = plotList ?? new List<PlotDef>();
            startsSealed = sealedAtStart;
            sealedText = sealedHint;
        }

        public void SetupVisual(FurnitureStyle style, Color light, bool flicker)
        {
            furniture = style;
            lightColor = light;
            flickeringLight = flicker;
        }

        public void Setup(string name, string desc, Color color, List<string> hintList,
            List<WeightedEncounter> encounterList, List<PayoffDef> payoffs, LockedSpot locked)
        {
            displayName = name;
            description = desc;
            floorColor = color;
            hints = hintList;
            encounters = encounterList;
            stagePayoffs = payoffs;
            lockedSpot = locked ?? new LockedSpot();
        }
    }
}
