using HorrorTycoon.Core;
using UnityEngine;

namespace HorrorTycoon.Run
{
    /// <summary>Aparência provisória do vilão na cena (só visual).</summary>
    public enum VillainLook
    {
        Auto,    // = Mascarado
        Masked,  // corpo escuro + máscara branca
        Ghost    // lençol de fantasma
    }

    /// <summary>
    /// Família do vilão (Protótipo 3, "Build do Filme" §5): define COMO ele age a cada cena.
    /// Auto = deduz da aparência (Fantasma se look = Ghost; senão Slasher).
    /// ATENÇÃO: só acrescente valores NO FIM.
    /// </summary>
    public enum VillainFamily
    {
        Auto,
        /// <summary>Agressivo: anda 1 espaço por cena, anuncia para onde vai e caça quem está sozinho.</summary>
        Slasher,
        /// <summary>Controlado: assombra uma sala, enche a Tensão e dá o Grande Susto em quem estiver lá.</summary>
        Fantasma,
    }

    /// <summary>
    /// VILÃO: escolhido no fim do Ato 1. Define a build (elementos da tag dele contam mais e os
    /// encontros puxam para a tag dele) e, no Protótipo 2, entra na casa como NPC (VillainAgent).
    /// Criar novo: Create > Horror Tycoon > Vilão.
    /// </summary>
    [CreateAssetMenu(menuName = "Horror Tycoon/Vilão", fileName = "Vilao_Novo")]
    public class VillainDef : ScriptableObject
    {
        [SerializeField] private string displayName = "Vilão";
        [SerializeField, TextArea] private string description = "";
        [SerializeField] private Color color = Color.red;
        [Tooltip("Subgênero. Ferramentas com 'counters' igual a esta tag repelem o vilão.")]
        [SerializeField] private TagDef subgenre;
        [Tooltip("Boneco provisório na cena.")]
        [SerializeField] private VillainLook look = VillainLook.Auto;
        [Tooltip("Comportamento por cena (Protótipo 3). Auto: Fantasma se a aparência é Ghost, senão Slasher.")]
        [SerializeField] private VillainFamily family = VillainFamily.Auto;

        public string DisplayName => displayName;
        public string Description => description;
        public Color Color => color;
        public TagDef Subgenre => subgenre;
        public VillainLook Look => look;
        /// <summary>Família efetiva (resolve o Auto).</summary>
        public VillainFamily Family => family != VillainFamily.Auto ? family
            : (look == VillainLook.Ghost ? VillainFamily.Fantasma : VillainFamily.Slasher);
        public VillainFamily FamilyRaw => family;
        public bool IsGhost => Family == VillainFamily.Fantasma;

        public void Setup(string name, string desc, Color c, TagDef tag, VillainLook villainLook = VillainLook.Auto)
        {
            displayName = name;
            description = desc;
            color = c;
            subgenre = tag;
            look = villainLook;
        }

        public void SetupFamily(VillainFamily f) => family = f;
    }
}
