using System.Collections.Generic;
using System.Text;
using HorrorTycoon.Core;
using UnityEngine;

namespace HorrorTycoon.Rooms.Generation
{
    /// <summary>
    /// Simulação em lote do gerador (sem cena): gera N casas e resume números para balanceamento.
    /// Usado pelo menu "Horror Tycoon/Geração/Estatísticas" e por um teste que só registra no log.
    /// Custos em PORTAS (ator comum: 1 porta = 1 passo).
    /// </summary>
    public static class HouseGenStats
    {
        public static string Run(HouseGenDef def, IReadOnlyList<RoomDef> roomPool, int houses, int firstSeed = 1)
        {
            int invalid = 0, fallbacks = 0, retries = 0, maxAttempts = 0;
            int roomsMin = int.MaxValue, roomsMax = 0, roomsSum = 0;
            int corridorsSum = 0, socialsSum = 0, deep = 0, roomTotal = 0;
            var roomsHistogram = new SortedDictionary<int, int>();
            var startCost = new Acc();
            var startCostOpen1 = new Acc();
            var pairCost = new Acc();
            var pairCostOpen1 = new Acc();

            for (int h = 0; h < houses; h++)
            {
                var layout = HouseGenerator.Generate(def, roomPool, new GameRandom(firstSeed + h));
                if (!layout.Validate(out _)) invalid++;
                if (layout.UsedFallback) fallbacks++;
                retries += layout.Attempts - 1;
                maxAttempts = Mathf.Max(maxAttempts, layout.Attempts);

                int rooms = layout.CountOf(SpaceKind.Room);
                roomsMin = Mathf.Min(roomsMin, rooms);
                roomsMax = Mathf.Max(roomsMax, rooms);
                roomsSum += rooms;
                roomsHistogram[rooms] = (roomsHistogram.TryGetValue(rooms, out int n) ? n : 0) + 1;
                corridorsSum += layout.CountOf(SpaceKind.Corridor);
                socialsSum += layout.CountOf(SpaceKind.Social) - 1;

                var map = new HouseMap(layout);
                var mapOpen1 = new HouseMap(layout, -1, 1);
                var roomIdx = new List<int>();
                foreach (var s in layout.Spaces)
                {
                    if (s.Kind != SpaceKind.Room) continue;
                    roomIdx.Add(s.Index);
                    roomTotal++;
                    if (s.IsDeep) deep++;
                    startCost.Add(map.Doors(layout.StartSpaceIndex, s.Index));
                    startCostOpen1.Add(mapOpen1.Doors(layout.StartSpaceIndex, s.Index));
                }
                for (int i = 0; i < roomIdx.Count; i++)
                    for (int j = i + 1; j < roomIdx.Count; j++)
                    {
                        pairCost.Add(map.Doors(roomIdx[i], roomIdx[j]));
                        pairCostOpen1.Add(mapOpen1.Doors(roomIdx[i], roomIdx[j]));
                    }
            }

            var sb = new StringBuilder();
            sb.AppendLine($"[HouseGenStats] {houses} casas (seeds {firstSeed}..{firstSeed + houses - 1}), terreno {def.bounds.x}x{def.bounds.y} m");
            sb.AppendLine($"  inválidas: {invalid} | aceitas sem bater metas (fallback): {fallbacks} | re-tentativas: {retries} (máx {maxAttempts} tentativas numa casa)");
            sb.Append($"  salas/casa: mín {roomsMin}, média {(float)roomsSum / houses:0.00}, máx {roomsMax} | distribuição:");
            foreach (var pair in roomsHistogram) sb.Append($" {pair.Key}→{pair.Value}");
            sb.AppendLine();
            sb.AppendLine($"  segmentos de corredor/casa: {(float)corridorsSum / houses:0.00} | convivências extras/casa: {(float)socialsSum / houses:0.00}");
            sb.AppendLine($"  salas fundas: {100f * deep / Mathf.Max(1, roomTotal):0.0}%");
            sb.AppendLine($"  portas início→sala (passagem={def.openingCost}): {startCost} | com passagem=1: {startCostOpen1}");
            sb.AppendLine($"  portas sala→sala   (passagem={def.openingCost}): {pairCost} | com passagem=1: {pairCostOpen1}");
            return sb.ToString();
        }

        private sealed class Acc
        {
            private int count, sum, min = int.MaxValue, max = int.MinValue;

            public void Add(int v)
            {
                count++;
                sum += v;
                if (v < min) min = v;
                if (v > max) max = v;
            }

            public override string ToString() =>
                count == 0 ? "—" : $"média {(float)sum / count:0.00} (mín {min}, máx {max})";
        }
    }
}
