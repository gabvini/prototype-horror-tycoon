using System.Collections.Generic;
using HorrorTycoon.Core;
using HorrorTycoon.Run;
using UnityEngine;

namespace HorrorTycoon.Actors
{
    /// <summary>
    /// Papel (classe) do ator no Protótipo 3 ("Build do Filme" §4). Cada papel tem UM passivo,
    /// com os números em GameRulesDef (seção "Passivos"). None = sem passivo (atores antigos/testes).
    /// ATENÇÃO: só acrescente valores NO FIM (o valor é salvo como número nos assets).
    /// </summary>
    public enum ActorRole
    {
        None,
        /// <summary>Tanque: aliados na mesma sala ganham metade do pavor; "segurar a porta" 1× por ato.</summary>
        Atleta,
        /// <summary>Mago: plots de investigação levam 1 cena a menos; ganha +50% de pavor.</summary>
        Nerd,
        /// <summary>Suporte: acalma quem está com ela; testemunhar uma morte a deixa determinada.</summary>
        FinalGirl,
        /// <summary>Bardo/isca: cenas com ela rendem mais audiência; alvo preferido do vilão.</summary>
        Popular,
    }

    /// <summary>
    /// Definição de um ator/arquétipo (Atleta, Nerd...). São DADOS, não o boneco na cena.
    /// O boneco na cena é o ActorView; o estado durante a run (pavor, posição, elementos) é o ActorRunState.
    /// Criar novo: Create > Horror Tycoon > Ator.
    /// </summary>
    [CreateAssetMenu(menuName = "Horror Tycoon/Ator", fileName = "Ator_Novo")]
    public class ActorDef : ScriptableObject
    {
        [SerializeField] private string displayName = "Ator";
        [SerializeField, TextArea] private string description = "";
        [SerializeField] private Color color = Color.gray;
        [SerializeField] private List<TagDef> tags = new List<TagDef>();

        [Header("Habilidades (P1)")]
        [Tooltip("Quantas portas o ator atravessa por passo. 2 = anda o dobro (ex.: Atleta). Sem efeito desde o Protótipo 2.")]
        [SerializeField, Min(1)] private int doorsPerStep = 1;

        [Tooltip("Pavor inicial do ator na run.")]
        [SerializeField, Min(0)] private int startingPavor;

        [Header("Classe (Protótipo 3 — Build do Filme)")]
        [Tooltip("Papel do ator. Define o passivo (números em Regras > Passivos) e as cenas de dupla (DuoComboDef).")]
        [SerializeField] private ActorRole role = ActorRole.None;
        [Tooltip("Nome do papel em linguagem de RPG/cinema (ex.: 'Tanque'). Só texto.")]
        [SerializeField] private string roleTitle = "";
        [Tooltip("Descrição curta do passivo, mostrada na HUD. Só texto (o efeito vem do papel).")]
        [SerializeField, TextArea] private string passiveText = "";
        [Tooltip("Plots que este ator traz para o filme (oferecidos no começo da run). Ex.: o mistério do Nerd.")]
        [SerializeField] private List<PlotDef> plots = new List<PlotDef>();

        public string DisplayName => displayName;
        public string Description => description;
        public Color Color => color;
        public IReadOnlyList<TagDef> Tags => tags;
        public int DoorsPerStep => doorsPerStep;
        public int StartingPavor => startingPavor;
        public ActorRole Role => role;
        public string RoleTitle => roleTitle;
        public string PassiveText => passiveText;
        public IReadOnlyList<PlotDef> Plots => plots;

        public bool HasTag(TagDef tag) => tags.Contains(tag);

        public void Setup(string name, string desc, Color c, List<TagDef> tagList, int doors, int pavor)
        {
            displayName = name;
            description = desc;
            color = c;
            tags = tagList;
            doorsPerStep = doors;
            startingPavor = pavor;
        }

        /// <summary>Classe do ator (ferramentas de editor e testes).</summary>
        public void SetupRole(ActorRole actorRole, string title = "", string passive = "", List<PlotDef> plotList = null)
        {
            role = actorRole;
            roleTitle = title;
            passiveText = passive;
            plots = plotList ?? new List<PlotDef>();
        }
    }
}
