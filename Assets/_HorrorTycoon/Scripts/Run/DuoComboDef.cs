using System.Collections.Generic;
using HorrorTycoon.Actors;
using UnityEngine;

namespace HorrorTycoon.Run
{
    /// <summary>
    /// CENA DE DUPLA / COMBO DE ELENCO (Protótipo 3, "Build do Filme" §4).
    /// Ativa num espaço quando os papéis pedidos estão juntos ali (e/ou há atores suficientes).
    /// Vale a cada CENA gravada (tique da casa), para quem estiver no espaço.
    ///   - Casal (Atleta + Popular): + audiência por cena.
    ///   - Investigação (Nerd + Final Girl): plots daquela sala andam mais rápido.
    ///   - Grupo (3+): o vilão não ataca o grupo, mas a audiência por cena cai ("cena parada").
    /// Criar novo: Create > Horror Tycoon > Cena de Dupla.
    /// </summary>
    [CreateAssetMenu(menuName = "Horror Tycoon/Cena de Dupla", fileName = "Combo_Novo")]
    public class DuoComboDef : ScriptableObject
    {
        [SerializeField] private string displayName = "Combo";
        [SerializeField, TextArea] private string description = "";
        [SerializeField] private Color color = new Color(1f, 0.6f, 0.8f);

        [Header("Condição")]
        [Tooltip("Papéis que precisam estar juntos no mesmo espaço (todos).")]
        [SerializeField] private List<ActorRole> requiredRoles = new List<ActorRole>();
        [Tooltip("Mínimo de atores no espaço (ex.: Grupo = 3).")]
        [SerializeField, Min(0)] private int minActors;
        [Tooltip("Só vale em SALAS (não em convivência/corredor).")]
        [SerializeField] private bool requiresRoom = true;

        [Header("Efeito por cena")]
        [Tooltip("Audiência por cena gravada (× mult. do ato, perto do vilão, Popular, artefatos).")]
        [SerializeField, Min(0)] private int scorePerScene;
        [Tooltip("Plots desta sala avançam esta quantidade de cenas A MAIS por cena.")]
        [SerializeField, Min(0)] private int plotSpeedBonus;
        [Tooltip("Multiplica TODA a audiência por cena deste espaço (combos e 'perto do vilão'). Grupo = 0,5.")]
        [SerializeField, Min(0f)] private float sceneScoreMultiplier = 1f;
        [Tooltip("O vilão não ataca quem está aqui: o susto em grupo do Slasher não dá pavor.")]
        [SerializeField] private bool blocksVillainAttack;

        public string DisplayName => displayName;
        public string Description => description;
        public Color Color => color;
        public IReadOnlyList<ActorRole> RequiredRoles => requiredRoles;
        public int MinActors => minActors;
        public bool RequiresRoom => requiresRoom;
        public int ScorePerScene => scorePerScene;
        public int PlotSpeedBonus => plotSpeedBonus;
        public float SceneScoreMultiplier => sceneScoreMultiplier;
        public bool BlocksVillainAttack => blocksVillainAttack;

        public void Setup(string name, string desc, List<ActorRole> roles, int min, bool onlyRooms,
            int score, int plotBonus = 0, float sceneMult = 1f, bool blocksAttack = false)
        {
            displayName = name;
            description = desc;
            requiredRoles = roles ?? new List<ActorRole>();
            minActors = min;
            requiresRoom = onlyRooms;
            scorePerScene = score;
            plotSpeedBonus = plotBonus;
            sceneScoreMultiplier = sceneMult;
            blocksVillainAttack = blocksAttack;
        }
    }
}
