using System.Collections.Generic;
using HorrorTycoon.Rooms.Generation;

namespace HorrorTycoon.Rooms
{
    /// <summary>
    /// Grafo da casa: quais lugares se ligam e quanto custa atravessar cada ligação (em "portas").
    ///
    /// Planta fixa do P0/P1 (estrela): o CORREDOR (índice 'hub') liga a porta de entrada (-1 = lá fora)
    /// a todos os cômodos. Ir de um cômodo a outro = sair para o corredor + entrar no outro = 2 portas.
    ///
    /// Casa gerada (HouseLayout): cada ligação tem custo próprio (porta = 1, passagem aberta = 0 por padrão).
    /// Doors() = menor caminho com pesos (Dijkstra), então qualquer planta funciona igual.
    /// </summary>
    public class HouseMap
    {
        public const int Outside = -1;

        private readonly int roomCount;
        private readonly Dictionary<int, List<int>> neighbors = new Dictionary<int, List<int>>();
        private readonly Dictionary<long, int> edgeCost = new Dictionary<long, int>();

        /// <summary>
        /// Espaço "central" de convivência. Planta fixa: o corredor. Casa gerada: a convivência inicial
        /// (é onde fica a porta da frente; a apresentação usa Hub para o plano da entrada). Alias de StartIndex.
        /// </summary>
        public int Hub { get; }
        /// <summary>Onde os atores começam (casa gerada) / o corredor (planta fixa).</summary>
        public int StartIndex { get; }
        /// <summary>Espaço ligado ao lado de fora pela porta da frente.</summary>
        public int EntranceIndex { get; }

        /// <param name="roomCount">Total de lugares (inclui o corredor).</param>
        /// <param name="hubIndex">Índice do corredor. -1 = sem corredor (todos se ligam a todos; entrada no 0).</param>
        public HouseMap(int roomCount, int hubIndex)
        {
            this.roomCount = roomCount;
            Hub = hubIndex;
            StartIndex = hubIndex;
            EntranceIndex = hubIndex >= 0 && hubIndex < roomCount ? hubIndex : (roomCount > 0 ? 0 : Outside);
            Init();

            if (hubIndex >= 0 && hubIndex < roomCount)
            {
                Link(Outside, hubIndex, 1);
                for (int i = 0; i < roomCount; i++)
                {
                    if (i != hubIndex) Link(hubIndex, i, 1);
                }
            }
            else if (roomCount > 0)
            {
                Link(Outside, 0, 1);
                for (int i = 0; i < roomCount; i++)
                    for (int j = i + 1; j < roomCount; j++)
                        Link(i, j, 1);
            }
        }

        /// <summary>Grafo a partir da casa gerada, com o custo de cada ligação (HouseConnection.Cost).</summary>
        public HouseMap(HouseLayout layout) : this(layout, -1, -1)
        {
        }

        /// <summary>
        /// Igual ao anterior, mas sobrescreve os custos (>= 0) de porta e/ou passagem.
        /// Útil para simular "e se a passagem custasse 1?" sem gerar outra casa.
        /// </summary>
        public HouseMap(HouseLayout layout, int doorCostOverride, int openingCostOverride)
        {
            roomCount = layout.Spaces.Count;
            Hub = layout.StartSpaceIndex;
            StartIndex = layout.StartSpaceIndex;
            EntranceIndex = layout.FrontDoor != null ? layout.FrontDoor.A : layout.StartSpaceIndex;
            Init();

            foreach (var c in layout.Connections)
            {
                int cost = c.Cost;
                if (c.Type == ConnectionType.Door && doorCostOverride >= 0) cost = doorCostOverride;
                if (c.Type == ConnectionType.Opening && openingCostOverride >= 0) cost = openingCostOverride;
                Link(c.A, c.B, cost);
            }
        }

        private void Init()
        {
            neighbors[Outside] = new List<int>();
            for (int i = 0; i < roomCount; i++) neighbors[i] = new List<int>();
        }

        private static long Key(int a, int b)
        {
            if (a > b) { int t = a; a = b; b = t; }
            return ((long)a << 32) ^ (uint)b;
        }

        private void Link(int a, int b, int cost)
        {
            long key = Key(a, b);
            if (edgeCost.TryGetValue(key, out int old))
            {
                if (cost < old) edgeCost[key] = cost; // ligação dupla: vale a mais barata
                return;
            }
            edgeCost[key] = cost;
            neighbors[a].Add(b);
            neighbors[b].Add(a);
        }

        public IReadOnlyList<int> Neighbors(int room) => neighbors[room];

        /// <summary>Custo da ligação direta entre dois vizinhos (-1 se não forem vizinhos).</summary>
        public int EdgeCost(int a, int b) => edgeCost.TryGetValue(Key(a, b), out int c) ? c : -1;

        /// <summary>Custo em portas do caminho mais barato entre dois lugares (-1 se não houver caminho).</summary>
        public int Doors(int from, int to)
        {
            if (from == to) return 0;
            if (!neighbors.ContainsKey(from) || !neighbors.ContainsKey(to)) return -1;

            // Dijkstra simples (casas pequenas: busca linear pelo menor em aberto basta).
            var dist = new Dictionary<int, int> { [from] = 0 };
            var done = new HashSet<int>();
            while (true)
            {
                int current = int.MinValue, best = int.MaxValue;
                foreach (var pair in dist)
                {
                    if (done.Contains(pair.Key) || pair.Value >= best) continue;
                    best = pair.Value;
                    current = pair.Key;
                }
                if (current == int.MinValue) return -1;
                if (current == to) return best;
                done.Add(current);

                foreach (int m in neighbors[current])
                {
                    if (done.Contains(m)) continue;
                    int nd = best + edgeCost[Key(current, m)];
                    if (!dist.TryGetValue(m, out int d) || nd < d) dist[m] = nd;
                }
            }
        }

        public int RoomCount => roomCount;
    }
}
