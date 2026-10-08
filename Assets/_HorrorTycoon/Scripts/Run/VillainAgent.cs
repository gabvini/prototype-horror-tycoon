using System.Collections.Generic;
using HorrorTycoon.Rooms;

namespace HorrorTycoon.Run
{
    /// <summary>
    /// VILÃO NPC (Protótipo 2, estilo Into the Breach). C# puro: estado + decisões de movimento.
    /// Quem manda nele é o FilmRun (batidas, encontros, pontuação); aqui só fica "onde está, para onde vai e quem caça".
    ///
    /// Regras de decisão (determinísticas, sem sorteio):
    ///   - ALVO: ator vivo dentro da casa; primeiro os SOZINHOS no espaço; depois mais pavor;
    ///     depois o mais perto do vilão; depois a ordem do elenco.
    ///   - PRÓXIMO ESPAÇO (o anúncio): primeiro passo do menor caminho (em espaços, não em portas)
    ///     até o espaço do alvo. Empate: o espaço de menor índice. Parado (atordoado) = fica onde está.
    ///   - O anúncio é um COMPROMISSO: é calculado depois de cada batida e a próxima batida vai
    ///     exatamente para lá, mesmo que o alvo tenha saído (o jogador pode desviar).
    /// </summary>
    public class VillainAgent
    {
        public VillainDef Def { get; }
        /// <summary>Espaço atual (-1 = ainda fora da casa).</summary>
        public int Space { get; internal set; } = -1;
        /// <summary>Espaço para onde vai na PRÓXIMA batida (anunciado). Igual a Space = vai ficar parado.</summary>
        public int NextSpace { get; internal set; } = -1;
        public int PreviousSpace { get; internal set; } = -1;
        /// <summary>Ator que ele está caçando (null = ninguém ao alcance).</summary>
        public ActorRunState Target { get; internal set; }
        /// <summary>Batidas que ainda vai ficar parado.</summary>
        public int StunBeats { get; internal set; }

        // ---- Protótipo 3: famílias
        public VillainFamily Family => Def != null ? Def.Family : VillainFamily.Slasher;
        public bool IsGhost => Family == VillainFamily.Fantasma;
        /// <summary>Fantasma: tensão acumulada na sala assombrada (Space). No máximo: Grande Susto.</summary>
        public int Tension { get; internal set; }
        /// <summary>Fantasma: Grandes Sustos dados na run.</summary>
        public int Scares { get; internal set; }

        public bool InHouse => Space >= 0;
        public bool IsStunned => StunBeats > 0;
        /// <summary>Vai mudar de espaço na próxima batida (há marca de passos para mostrar).</summary>
        public bool IsTelegraphing => InHouse && NextSpace >= 0 && NextSpace != Space;

        public VillainAgent(VillainDef def)
        {
            Def = def;
        }

        /// <summary>Recalcula alvo e anúncio. Chamado pelo FilmRun depois de cada batida/mudança do vilão.</summary>
        public void Plan(HouseMap map, IReadOnlyList<ActorRunState> actors, System.Func<ActorRunState, bool> isBait = null)
        {
            if (!InHouse)
            {
                Target = null;
                NextSpace = -1;
                return;
            }

            // Fantasma não caça: a próxima sala assombrada é escolhida pelo FilmRun quando ele muda de sala.
            if (IsGhost)
            {
                Target = null;
                return;
            }

            Target = ChooseTarget(map, actors, Space, isBait);
            if (IsStunned || Target == null || Target.RoomIndex == Space)
            {
                NextSpace = Space;
                return;
            }

            int hop = NextHop(map, Space, Target.RoomIndex);
            NextSpace = hop >= 0 ? hop : Space;
        }

        // ================================================================== Ajudantes (estáticos, testáveis)

        /// <summary>Distância em ESPAÇOS (saltos no grafo) de 'from' até cada espaço. -1 = sem caminho. Nunca passa por "lá fora".</summary>
        public static int[] Hops(HouseMap map, int from)
        {
            var dist = new int[map.RoomCount];
            for (int i = 0; i < dist.Length; i++) dist[i] = -1;
            if (from < 0 || from >= map.RoomCount) return dist;

            var queue = new Queue<int>();
            dist[from] = 0;
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                int c = queue.Dequeue();
                foreach (int n in map.Neighbors(c))
                {
                    if (n < 0 || dist[n] >= 0) continue;
                    dist[n] = dist[c] + 1;
                    queue.Enqueue(n);
                }
            }
            return dist;
        }

        /// <summary>Primeiro passo do menor caminho de 'from' até 'to' (empate: menor índice). -1 = sem caminho.</summary>
        public static int NextHop(HouseMap map, int from, int to)
        {
            if (from < 0 || to < 0) return -1;
            if (from == to) return from;
            int[] distToTarget = Hops(map, to);
            if (distToTarget[from] < 0) return -1;

            int best = -1;
            foreach (int n in map.Neighbors(from))
            {
                if (n < 0 || distToTarget[n] != distToTarget[from] - 1) continue;
                if (best < 0 || n < best) best = n;
            }
            return best;
        }

        /// <summary>
        /// Ator mais vulnerável: sozinho → isca (Popular, Protótipo 3) → mais pavor → mais perto → ordem do elenco.
        /// isBait null = ninguém é isca (regra do Protótipo 2).
        /// </summary>
        public static ActorRunState ChooseTarget(HouseMap map, IReadOnlyList<ActorRunState> actors, int villainSpace,
            System.Func<ActorRunState, bool> isBait = null)
        {
            int[] dist = Hops(map, villainSpace);
            ActorRunState best = null;
            int bestAlone = 0, bestBait = 0, bestPavor = 0, bestDist = 0;

            for (int i = 0; i < actors.Count; i++)
            {
                var a = actors[i];
                if (!a.Alive || a.RoomIndex < 0 || a.RoomIndex >= dist.Length || dist[a.RoomIndex] < 0) continue;

                int together = 0;
                foreach (var o in actors) if (o.Alive && o.RoomIndex == a.RoomIndex) together++;
                int alone = together == 1 ? 1 : 0;
                int bait = isBait != null && isBait(a) ? 1 : 0;
                int d = dist[a.RoomIndex];

                bool better = best == null
                              || alone > bestAlone
                              || (alone == bestAlone && bait > bestBait)
                              || (alone == bestAlone && bait == bestBait && a.Pavor > bestPavor)
                              || (alone == bestAlone && bait == bestBait && a.Pavor == bestPavor && d < bestDist);
                if (!better) continue;
                best = a;
                bestAlone = alone;
                bestBait = bait;
                bestPavor = a.Pavor;
                bestDist = d;
            }
            return best;
        }
    }
}
