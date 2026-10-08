using System.Collections.Generic;
using HorrorTycoon.Actors;
using HorrorTycoon.Rooms;
using HorrorTycoon.Scoring;
using UnityEngine;

namespace HorrorTycoon.Run
{
    /// <summary>
    /// PLOT DE PERMANÊNCIA (Protótipo 3, "Build do Filme" §3): "segure a posição".
    /// CONDIÇÃO: certos atores (por papel) numa certa sala, por N cenas seguidas.
    /// RECOMPENSA: audiência + liberar algo (sala lacrada, ferramenta, artefato, elemento).
    /// QUEBRA: alguém necessário sai da sala, é pego pelo vilão ou entra em crise.
    ///
    /// De onde vem: RoomDef.plots (oferecido na 1ª exploração da sala na run) ou ActorDef.plots (começo da run).
    /// Cada plot só pode ser cumprido uma vez por run.
    /// Criar novo: Create > Horror Tycoon > Plot.
    /// </summary>
    [CreateAssetMenu(menuName = "Horror Tycoon/Plot", fileName = "Plot_Novo")]
    public class PlotDef : ScriptableObject
    {
        [SerializeField] private string displayName = "Plot";
        [Tooltip("Em linguagem de filme: o que precisa acontecer. Ex.: 'O casal fica 1 cena no Quarto.'")]
        [SerializeField, TextArea] private string description = "";
        [SerializeField] private Color color = new Color(1f, 0.85f, 0.4f);

        [Header("Condição")]
        [Tooltip("Papéis que precisam estar JUNTOS na sala (todos). Vazio = qualquer ator.")]
        [SerializeField] private List<ActorRole> requiredRoles = new List<ActorRole>();
        [Tooltip("Mínimo de atores na sala (0 = só os papéis).")]
        [SerializeField, Min(0)] private int minActors;
        [Tooltip("Máximo de atores na sala (0 = sem limite; 1 = 'sozinho').")]
        [SerializeField, Min(0)] private int maxActors;
        [Tooltip("Salas onde vale (qualquer uma). Vazio = a sala que ofereceu o plot (plot de sala) ou qualquer sala (plot de ator).")]
        [SerializeField] private List<RoomDef> rooms = new List<RoomDef>();
        [Tooltip("Cenas seguidas que os atores precisam ficar.")]
        [SerializeField, Min(1)] private int scenes = 1;
        [Tooltip("Plot de investigação: com o Nerd na sala leva menos cenas (Regras > nerdPlotReduction).")]
        [SerializeField] private bool investigation;
        [Tooltip("Se quebrar: false = o progresso volta a zero e dá para tentar de novo; true = o plot falha de vez.")]
        [SerializeField] private bool failOnBreak;

        [Header("Recompensa")]
        [Tooltip("Audiência ao cumprir (× multiplicador do ato, se Regras > plotUsesActMultiplier).")]
        [SerializeField, Min(0)] private int rewardPoints = 100;
        [Tooltip("Pavor somado a quem cumpriu (negativo acalma).")]
        [SerializeField] private int rewardPavor;
        [Tooltip("Sala LACRADA que este plot libera (todas as salas com este RoomDef).")]
        [SerializeField] private RoomDef unlockRoom;
        [Tooltip("Ferramenta dada a quem cumpriu (se já está em jogo, não dá).")]
        [SerializeField] private ToolDef rewardTool;
        [Tooltip("Artefato dado ao cumprir.")]
        [SerializeField] private ArtefatoDef rewardArtefato;
        [Tooltip("Dá um artefato SORTEADO do catálogo (que o jogador ainda não tem).")]
        [SerializeField] private bool rewardRandomArtefato;
        [Tooltip("Elemento de cena dado ao cumprir (no 1º ator participante, ou na sala).")]
        [SerializeField] private ElementDef rewardElement;
        [Tooltip("Texto da recompensa, em linguagem de filme (popup).")]
        [SerializeField, TextArea] private string rewardText = "";

        public string DisplayName => displayName;
        public string Description => description;
        public Color Color => color;
        public IReadOnlyList<ActorRole> RequiredRoles => requiredRoles;
        public int MinActors => minActors;
        public int MaxActors => maxActors;
        public IReadOnlyList<RoomDef> Rooms => rooms;
        public int Scenes => Mathf.Max(1, scenes);
        public bool Investigation => investigation;
        public bool FailOnBreak => failOnBreak;
        public int RewardPoints => rewardPoints;
        public int RewardPavor => rewardPavor;
        public RoomDef UnlockRoom => unlockRoom;
        public ToolDef RewardTool => rewardTool;
        public ArtefatoDef RewardArtefato => rewardArtefato;
        public bool RewardRandomArtefato => rewardRandomArtefato;
        public ElementDef RewardElement => rewardElement;
        public string RewardText => rewardText;

        public void Setup(string name, string desc, List<ActorRole> roles, int sceneCount, int points,
            List<RoomDef> roomList = null, bool isInvestigation = false, int min = 0, int max = 0)
        {
            displayName = name;
            description = desc;
            requiredRoles = roles ?? new List<ActorRole>();
            scenes = sceneCount;
            rewardPoints = points;
            rooms = roomList ?? new List<RoomDef>();
            investigation = isInvestigation;
            minActors = min;
            maxActors = max;
        }

        public void SetupReward(RoomDef unlock = null, ToolDef tool = null, ArtefatoDef artefato = null,
            bool randomArtefato = false, ElementDef element = null, int pavor = 0, string text = "", bool failsOnBreak = false)
        {
            unlockRoom = unlock;
            rewardTool = tool;
            rewardArtefato = artefato;
            rewardRandomArtefato = randomArtefato;
            rewardElement = element;
            rewardPavor = pavor;
            rewardText = text;
            failOnBreak = failsOnBreak;
        }
    }
}
