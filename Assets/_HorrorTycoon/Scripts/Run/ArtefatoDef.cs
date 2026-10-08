using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorTycoon.Run
{
    /// <summary>
    /// Tipos de efeito de artefato (genéricos: o artefato é só uma lista deles).
    /// ATENÇÃO: só acrescente valores NO FIM (o valor é salvo como número nos assets).
    /// </summary>
    public enum ArtefatoEffectType
    {
        /// <summary>× audiência das cenas de dupla (todas, ou só a 'combo' indicada).</summary>
        ComboScoreMult,
        /// <summary>× audiência dos plots cumpridos.</summary>
        PlotRewardMult,
        /// <summary>× audiência dos SUSTOS: Grande Susto do fantasma e cenas dirigidas que não matam.</summary>
        ScareScoreMult,
        /// <summary>× audiência das MORTES (cenas que matam, inclusive a pega do vilão).</summary>
        DeathScoreMult,
        /// <summary>× tensão que o fantasma acumula por cena.</summary>
        GhostTensionGainMult,
        /// <summary>× pavor GANHO por todos (menor que 1 acalma).</summary>
        PavorGainMult,
        /// <summary>× audiência "perto do vilão".</summary>
        NearVillainScoreMult,
        /// <summary>+N cenas em cada ato (valor somado, arredondado).</summary>
        ExtraScenesPerAct,
        /// <summary>× audiência das explorações (encontros).</summary>
        ExploreScoreMult,
    }

    /// <summary>
    /// ARTEFATO (o "Joker" do Balatro; Protótipo 3, v1 simples): item PASSIVO da run que muda as regras.
    /// Vem da escolha entre atos ("1 de 3", grátis) e de plots. Fica com o FILME (não com um ator).
    /// Criar novo: Create > Horror Tycoon > Artefato.
    /// </summary>
    [CreateAssetMenu(menuName = "Horror Tycoon/Artefato", fileName = "Artefato_Novo")]
    public class ArtefatoDef : ScriptableObject
    {
        [Serializable]
        public class Effect
        {
            public ArtefatoEffectType type = ArtefatoEffectType.ComboScoreMult;
            [Tooltip("Multiplicador (ex.: 1,5) ou, para ExtraScenesPerAct, a quantidade somada.")]
            public float value = 1.5f;
            [Tooltip("Só ComboScoreMult: vale só para esta cena de dupla (vazio = todas).")]
            public DuoComboDef combo;
        }

        [SerializeField] private string displayName = "Artefato";
        [Tooltip("Em linguagem de filme, curto. Ex.: 'Sustos rendem +50%'.")]
        [SerializeField, TextArea] private string description = "";
        [SerializeField] private Color color = new Color(0.95f, 0.8f, 0.45f);
        [SerializeField] private List<Effect> effects = new List<Effect>();

        public string DisplayName => displayName;
        public string Description => description;
        public Color Color => color;
        public IReadOnlyList<Effect> Effects => effects;

        public void Setup(string name, string desc, Color c, List<Effect> effectList)
        {
            displayName = name;
            description = desc;
            color = c;
            effects = effectList ?? new List<Effect>();
        }

        public static Effect Fx(ArtefatoEffectType type, float value, DuoComboDef combo = null) =>
            new Effect { type = type, value = value, combo = combo };
    }
}
