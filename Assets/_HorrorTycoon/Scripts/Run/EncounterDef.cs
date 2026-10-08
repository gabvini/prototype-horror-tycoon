using HorrorTycoon.Scoring;
using UnityEngine;

namespace HorrorTycoon.Run
{
    /// <summary>
    /// ENCONTRO: o que acontece quando um ator entra numa sala pela primeira vez no ato.
    /// Pode dar pontos pequenos, mudar o pavor, deixar um elemento de cena e/ou uma ferramenta.
    /// Cada sala tem uma lista de encontros possíveis com pesos (RoomDef).
    /// Criar novo: Create > Horror Tycoon > Encontro.
    /// </summary>
    [CreateAssetMenu(menuName = "Horror Tycoon/Encontro", fileName = "Encontro_Novo")]
    public class EncounterDef : ScriptableObject
    {
        [SerializeField] private string displayName = "Encontro";
        [SerializeField, TextArea] private string description = "";
        [SerializeField] private int points = 20;
        [SerializeField] private int pavorDelta;
        [SerializeField] private ElementDef element;
        [SerializeField] private ToolDef tool;

        public string DisplayName => displayName;
        public string Description => description;
        public int Points => points;
        public int PavorDelta => pavorDelta;
        public ElementDef Element => element;
        public ToolDef Tool => tool;

        public void Setup(string name, string desc, int pts, int pavor, ElementDef el, ToolDef t)
        {
            displayName = name;
            description = desc;
            points = pts;
            pavorDelta = pavor;
            element = el;
            tool = t;
        }
    }
}
