using UnityEngine;

namespace HorrorTycoon.Scoring
{
    /// <summary>
    /// Tipo de CENA DIRIGIDA (o "payoff"): Susto, Morte...
    /// Acontece numa sala de cena e transforma os elementos acumulados em pontos.
    /// Pontos = base × multiplicador do ato × (1 + soma dos elementos que contam).
    /// Criar novo: Create > Horror Tycoon > Cena Dirigida.
    /// </summary>
    [CreateAssetMenu(menuName = "Horror Tycoon/Cena Dirigida", fileName = "Payoff_Novo")]
    public class PayoffDef : ScriptableObject
    {
        [SerializeField] private string displayName = "Cena";
        [SerializeField, TextArea] private string description = "";
        [SerializeField] private Color color = Color.white;

        [SerializeField, Min(0)] private int baseScore = 60;

        [Tooltip("Quanto o pavor do ator muda ao fazer esta cena.")]
        [SerializeField] private int pavorDelta = 25;

        [Tooltip("A cena tira o ator do filme (morte)?")]
        [SerializeField] private bool killsActor;

        [Tooltip("Primeiro ato em que esta cena fica disponível (0 = Ato 1, 1 = Ato 2...).")]
        [SerializeField, Min(0)] private int minActIndex;

        [Tooltip("Os elementos que contaram são gastos?")]
        [SerializeField] private bool consumesElements = true;

        public string DisplayName => displayName;
        public string Description => description;
        public Color Color => color;
        public int BaseScore => baseScore;
        public int PavorDelta => pavorDelta;
        public bool KillsActor => killsActor;
        public int MinActIndex => minActIndex;
        public bool ConsumesElements => consumesElements;

        public void Setup(string name, string desc, Color c, int baseValue, int pavor, bool kills, int minAct)
        {
            displayName = name;
            description = desc;
            color = c;
            baseScore = baseValue;
            pavorDelta = pavor;
            killsActor = kills;
            minActIndex = minAct;
        }
    }
}
