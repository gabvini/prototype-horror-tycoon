using HorrorTycoon.Core;
using UnityEngine;

namespace HorrorTycoon.Run
{
    /// <summary>
    /// FERRAMENTA: item que interage com o mapa (ex.: chave de fenda abre o alçapão) ou com o vilão
    /// (ex.: faca repele o Slasher). Protótipo 2: fica com o ATOR que achou (limite em GameRulesDef);
    /// atores no mesmo espaço podem usar a ferramenta um do outro.
    /// Criar nova: Create > Horror Tycoon > Ferramenta.
    /// </summary>
    [CreateAssetMenu(menuName = "Horror Tycoon/Ferramenta", fileName = "Ferramenta_Nova")]
    public class ToolDef : ScriptableObject
    {
        [SerializeField] private string displayName = "Ferramenta";
        [SerializeField, TextArea] private string description = "";

        [Tooltip("Subgênero de vilão que esta ferramenta combate (Faca → Slasher, Crucifixo → Sobrenatural). " +
                 "Vazio = não serve contra o vilão. Ao repelir, a ferramenta é gasta.")]
        [SerializeField] private TagDef counters;

        public string DisplayName => displayName;
        public string Description => description;
        public TagDef Counters => counters;

        /// <summary>Esta ferramenta repele o vilão dado?</summary>
        public bool Repels(VillainDef villain) =>
            villain != null && counters != null && villain.Subgenre == counters;

        public void Setup(string name, string desc, TagDef countersTag = null)
        {
            displayName = name;
            description = desc;
            counters = countersTag;
        }
    }
}
