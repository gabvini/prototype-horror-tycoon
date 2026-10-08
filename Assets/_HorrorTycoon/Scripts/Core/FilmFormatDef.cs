using UnityEngine;

namespace HorrorTycoon.Core
{
    /// <summary>
    /// Formato do filme (Curta, Média, Grande...). Define tamanho e dificuldade da run.
    /// Criar formato novo: Create > Horror Tycoon > Formato de Filme.
    /// </summary>
    [CreateAssetMenu(menuName = "Horror Tycoon/Formato de Filme", fileName = "Formato_Novo")]
    public class FilmFormatDef : ScriptableObject
    {
        [System.Serializable]
        public class ActSettings
        {
            [Tooltip("Nome mostrado na tela.")]
            public string name = "Ato";

            [Tooltip("CENAS deste ato (Protótipo 3; antes 'ações'/'passos'). Cada cena gravada é um turno da casa inteira.")]
            [Min(1)] public int steps = 8;

            [Tooltip("Pontuação ACUMULADA do filme que precisa ser atingida no fim deste ato.")]
            [Min(0)] public int goal = 150;

            [Tooltip("Multiplicador das cenas dirigidas (payoffs) neste ato. O clímax vale mais.")]
            [Min(0f)] public float payoffMultiplier = 1f;

            [Tooltip("Se falhar a meta deste ato, a run acaba?")]
            public bool failIsFatal = true;
        }

        [SerializeField] private string displayName = "Curta";
        [SerializeField] private ActSettings[] acts =
        {
            new ActSettings { name = "Ato 1", steps = 8, goal = 150, payoffMultiplier = 1f },
            new ActSettings { name = "Ato 2", steps = 8, goal = 1300, payoffMultiplier = 1.5f },
            new ActSettings { name = "Ato 3", steps = 8, goal = 3500, payoffMultiplier = 2.5f },
        };

        public string DisplayName => displayName;
        public ActSettings[] Acts => acts;
    }
}
