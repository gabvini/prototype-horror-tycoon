using UnityEngine;

namespace HorrorTycoon.Core
{
    /// <summary>
    /// Uma etiqueta (tag) usada para sinergias: "Atleta", "Susto", "Escuro"...
    /// É um asset (e não um texto solto) para evitar erro de digitação e para
    /// poder renomear sem quebrar nada. Criar tag nova: Create > Horror Tycoon > Tag.
    /// </summary>
    [CreateAssetMenu(menuName = "Horror Tycoon/Tag", fileName = "Tag_Nova")]
    public class TagDef : ScriptableObject
    {
        [SerializeField] private string displayName = "Tag";
        [SerializeField] private Color color = Color.white;

        public string DisplayName => displayName;
        public Color Color => color;
    }
}
