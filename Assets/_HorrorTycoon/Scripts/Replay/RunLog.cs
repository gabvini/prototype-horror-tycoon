using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorTycoon.Replay
{
    /// <summary>
    /// Diário da run. Duas partes:
    ///  - seed + lista de AÇÕES do jogador (ex.: "move Atleta 4", "direct Nerd Morte", "villain Slasher"):
    ///    como toda aleatoriedade sai do GameRandom com a mesma seed, isso basta para REPRODUZIR a run.
    ///    É a base do futuro "filme" exportável.
    ///  - entradas legíveis (texto), para depuração e para a futura pós-produção.
    /// </summary>
    [Serializable]
    public class RunLog
    {
        [Serializable]
        public class Entry
        {
            public int act;
            public string type;
            public string text;
        }

        public int seed;
        public string filmFormat;
        public List<string> actions = new List<string>();
        public List<Entry> entries = new List<Entry>();

        public void Add(int act, string type, string text)
        {
            entries.Add(new Entry { act = act, type = type, text = text });
        }

        public string ToJson() => JsonUtility.ToJson(this, true);
    }
}
