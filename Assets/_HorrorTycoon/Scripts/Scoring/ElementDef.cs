using System.Collections.Generic;
using HorrorTycoon.Core;
using UnityEngine;

namespace HorrorTycoon.Scoring
{
    /// <summary>Onde um elemento de cena fica preso.</summary>
    public enum ElementHolder
    {
        Actor,
        Room
    }

    /// <summary>
    /// Elementos "automáticos" não são coletados: valem enquanto a condição for verdadeira
    /// no momento em que a cena é dirigida (ex.: ator sozinho na sala, ator apavorado).
    /// </summary>
    public enum ElementAutoRule
    {
        None,
        AloneInRoom,
        PavorAtLeast
    }

    /// <summary>
    /// ELEMENTO DE CENA (o "setup"): a faca, a luz apagada, a presença, o ator isolado...
    /// Acumulam em atores e salas e multiplicam o payoff quando uma cena é dirigida.
    /// Criar novo: Create > Horror Tycoon > Elemento de Cena.
    /// </summary>
    [CreateAssetMenu(menuName = "Horror Tycoon/Elemento de Cena", fileName = "Elemento_Novo")]
    public class ElementDef : ScriptableObject
    {
        [SerializeField] private string displayName = "Elemento";
        [SerializeField, TextArea] private string description = "";
        [SerializeField] private Color color = Color.white;

        [Tooltip("Subgênero que este elemento puxa (Slasher, Sobrenatural...). Vazio = neutro.")]
        [SerializeField] private TagDef subgenre;

        [SerializeField] private ElementHolder holder = ElementHolder.Actor;

        [Tooltip("Quanto este elemento soma no combo (1 = normal, 2 = forte).")]
        [SerializeField, Min(1)] private int strength = 1;

        [Tooltip("Em quais tipos de cena dirigida este elemento conta.")]
        [SerializeField] private List<PayoffDef> validFor = new List<PayoffDef>();

        [Header("Automático (opcional)")]
        [SerializeField] private ElementAutoRule autoRule = ElementAutoRule.None;
        [SerializeField] private int autoValue = 60;

        public string DisplayName => displayName;
        public string Description => description;
        public Color Color => color;
        public TagDef Subgenre => subgenre;
        public ElementHolder Holder => holder;
        public int Strength => strength;
        public ElementAutoRule AutoRule => autoRule;
        public int AutoValue => autoValue;

        public bool CountsFor(PayoffDef payoff) => validFor.Contains(payoff);

        public void Setup(string name, string desc, Color c, TagDef tag, ElementHolder h, int str,
            List<PayoffDef> payoffs, ElementAutoRule rule = ElementAutoRule.None, int ruleValue = 0)
        {
            displayName = name;
            description = desc;
            color = c;
            subgenre = tag;
            holder = h;
            strength = str;
            validFor = payoffs;
            autoRule = rule;
            autoValue = ruleValue;
        }
    }
}
