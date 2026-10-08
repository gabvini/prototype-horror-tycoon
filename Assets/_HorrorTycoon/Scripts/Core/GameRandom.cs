using System.Collections.Generic;

namespace HorrorTycoon.Core
{
    /// <summary>
    /// Gerador aleatório com SEED. Mesma seed + mesmas escolhas = mesma run.
    /// Toda aleatoriedade da lógica do jogo deve passar por aqui (nunca UnityEngine.Random),
    /// senão o replay e os testes deixam de ser reproduzíveis.
    /// </summary>
    public class GameRandom
    {
        private readonly System.Random random;

        public int Seed { get; }

        public GameRandom(int seed)
        {
            Seed = seed;
            random = new System.Random(seed);
        }

        /// <summary>
        /// Deriva uma seed nova e estável de (seed, sal). Usado para "fluxos" separados
        /// (ex.: a geração da casa não altera a sequência dos sorteios de encontros).
        /// </summary>
        public static int Mix(int seed, int salt)
        {
            unchecked
            {
                uint h = (uint)seed * 0x9E3779B1u ^ (uint)salt * 0x85EBCA77u;
                h ^= h >> 15; h *= 0x2C1B3C6Du;
                h ^= h >> 12; h *= 0x297A2D39u;
                h ^= h >> 15;
                return (int)(h & 0x7FFFFFFF);
            }
        }

        /// <summary>Inteiro entre min e max, INCLUSIVO nos dois lados.</summary>
        public int Range(int minInclusive, int maxInclusive)
        {
            return random.Next(minInclusive, maxInclusive + 1);
        }

        /// <summary>Número entre 0 e 1.</summary>
        public float Value()
        {
            return (float)random.NextDouble();
        }

        /// <summary>True com a probabilidade dada (0 a 1).</summary>
        public bool Chance(float probability)
        {
            return Value() < probability;
        }

        /// <summary>Embaralha a lista no lugar (Fisher-Yates), usando a seed.</summary>
        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        public T Pick<T>(IReadOnlyList<T> list)
        {
            return list[random.Next(list.Count)];
        }

        /// <summary>Escolhe um índice respeitando pesos (peso 0 = nunca).</summary>
        public int PickWeightedIndex(IReadOnlyList<float> weights)
        {
            float total = 0f;
            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i] > 0f) total += weights[i];
            }

            if (total <= 0f)
            {
                return -1;
            }

            float roll = Value() * total;
            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i] <= 0f) continue;
                roll -= weights[i];
                if (roll < 0f) return i;
            }

            return weights.Count - 1;
        }
    }
}
