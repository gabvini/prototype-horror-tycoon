using System;
using System.Collections.Generic;
using HorrorTycoon.Core;
using UnityEngine;

namespace HorrorTycoon.Rooms.Generation
{
    /// <summary>
    /// Gera a planta da casa (Proposta_Geracao_Casa §3). C# puro e determinístico pela seed.
    ///
    /// Passos de uma tentativa:
    ///   1. Convivência inicial colada na borda sul, com a PORTA DA FRENTE na parede sul.
    ///   2. Rede de corredores: segmentos de largura fixa crescem a partir dela (reto, virando 90° ou ramificando).
    ///   3. Convivências extras presas a corredores (passagem aberta).
    ///   4. Salas sorteadas do pool, presas numa parede livre de corredor/convivência (porta na parede em comum).
    ///      Com uma chance, a sala abre para dentro de outra sala ("funda").
    ///   5. Validação. Se não bater as metas, nova tentativa com sub-seed. No fim, aceita a melhor
    ///      (sempre estruturalmente válida: sem sobreposição, tudo conectado).
    ///
    /// Para estender: novos tipos de espaço = novo passo no Builder.Build(); regras de posição = filtro em Candidates().
    /// </summary>
    public static class HouseGenerator
    {
        private enum Side { North, East, South, West }

        public static HouseLayout Generate(HouseGenDef def, IReadOnlyList<RoomDef> roomPool, GameRandom rng)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            roomPool = roomPool ?? new List<RoomDef>();

            int baseSeed = rng.Range(0, int.MaxValue - 1);
            int attempts = Mathf.Max(1, def.maxAttempts);
            HouseLayout best = null;
            int bestScore = int.MinValue;

            for (int a = 0; a < attempts; a++)
            {
                int sub = GameRandom.Mix(baseSeed, a);
                var builder = new Builder(def, roomPool, new GameRandom(sub));
                var layout = builder.Build();
                layout.Seed = sub;
                layout.Attempts = a + 1;

                if (!layout.Validate(out _)) continue; // não deveria acontecer; descarta por segurança
                if (builder.MeetsTargets) return layout;

                if (builder.Score > bestScore)
                {
                    bestScore = builder.Score;
                    best = layout;
                }
            }

            if (best == null)
            {
                // Último recurso: só a convivência inicial (sempre válida).
                best = new Builder(def, roomPool, new GameRandom(baseSeed)).BuildStartOnly();
                best.Seed = baseSeed;
            }
            best.Attempts = attempts;
            best.UsedFallback = true;
            return best;
        }

        /// <summary>Quantas salas o pool consegue oferecer (soma de maxPerRun das salas sorteáveis).</summary>
        public static int PoolCapacity(IReadOnlyList<RoomDef> roomPool)
        {
            int cap = 0;
            var seen = new HashSet<RoomDef>();
            foreach (var r in roomPool)
            {
                if (r == null || r.Kind != SpaceKind.Room || !seen.Add(r)) continue;
                if (r.Weight > 0f || r.Required) cap += r.MaxPerRun;
            }
            return cap;
        }

        // ====================================================================== Uma tentativa

        private sealed class Builder
        {
            private struct Candidate
            {
                public int Host;
                public Side Side;
                public RectInt Rect;
                public bool Rotated;
                public int S0, S1; // trecho de parede em comum (ao longo do eixo do lado)
            }

            private readonly HouseGenDef def;
            private readonly IReadOnlyList<RoomDef> pool;
            private readonly GameRandom rng;
            private readonly HouseLayout layout = new HouseLayout();
            private readonly int width, height;
            private readonly int[,] grid; // índice do espaço em cada célula de 1 m (-1 = vazio)
            private readonly List<int> corridors = new List<int>();
            private readonly Dictionary<int, Side> corridorDir = new Dictionary<int, Side>();

            public bool MeetsTargets { get; private set; }
            public int Score { get; private set; }

            public Builder(HouseGenDef def, IReadOnlyList<RoomDef> pool, GameRandom rng)
            {
                this.def = def;
                this.pool = pool;
                this.rng = rng;
                width = Mathf.Max(4, def.bounds.x);
                height = Mathf.Max(4, def.bounds.y);
                layout.Bounds = new Vector2Int(width, height);
                grid = new int[width, height];
                for (int x = 0; x < width; x++)
                    for (int y = 0; y < height; y++)
                        grid[x, y] = -1;
            }

            public HouseLayout BuildStartOnly()
            {
                PlaceStart();
                ComputeWalls();
                return layout;
            }

            public HouseLayout Build()
            {
                PlaceStart();
                int corridorTarget = rng.Range(def.corridorSegments.x, Mathf.Max(def.corridorSegments.x, def.corridorSegments.y));
                GrowCorridors(corridorTarget);
                int socialTarget = rng.Range(def.extraSocials.x, Mathf.Max(def.extraSocials.x, def.extraSocials.y));
                int socialsPlaced = PlaceExtraSocials(socialTarget);
                int roomsWanted, minRooms;
                bool requiredOk;
                int roomsPlaced = PlaceRooms(out roomsWanted, out minRooms, out requiredOk);
                ComputeWalls();

                MeetsTargets = requiredOk
                               && roomsPlaced >= minRooms
                               && (def.corridorDef == null || corridors.Count >= def.corridorSegments.x)
                               && socialsPlaced >= Mathf.Min(def.extraSocials.x, socialTarget);
                // Para escolher a "melhor" quando nenhuma tentativa bate tudo: obrigatórias > salas > o resto.
                Score = (requiredOk ? 10000 : 0) + roomsPlaced * 100 + socialsPlaced * 10 + corridors.Count;
                return layout;
            }

            // ------------------------------------------------------------------ 1. Convivência inicial

            private RoomDef StartDef()
            {
                RoomDef first = null;
                foreach (var s in def.socialPool)
                {
                    if (s == null || s.Kind != SpaceKind.Social) continue;
                    if (s.Required) return s;
                    if (first == null) first = s;
                }
                if (first != null) return first;
                if (def.corridorDef != null) return def.corridorDef; // sem convivência configurada: usa o corredor
                throw new InvalidOperationException("HouseGenDef sem convivência (socialPool) nem corredor.");
            }

            private void PlaceStart()
            {
                var startDef = StartDef();
                Vector2Int size = startDef.Size;
                bool rotated = false;
                if (startDef.AllowRotation && size.x != size.y && rng.Chance(0.5f))
                {
                    size = new Vector2Int(size.y, size.x);
                    rotated = true;
                }
                size.x = Mathf.Min(size.x, width);
                size.y = Mathf.Min(size.y, height);

                // Perto do meio da fachada sul.
                int free = width - size.x;
                int x = rng.Range(free / 4, free - free / 4);
                var rect = new RectInt(x, 0, size.x, size.y);
                int start = AddSpace(SpaceKind.Social, startDef, rect, rotated, -1, false);
                layout.StartSpaceIndex = start;

                // Porta da frente na parede sul (y = 0).
                float center = DoorCenter(rect.xMin, rect.xMax, def.doorWidth, def.doorCornerMargin);
                layout.FrontDoorIndex = AddConnection(start, HouseLayout.Outside, ConnectionType.Door,
                    new Vector2(center, 0f), true, def.doorWidth, def.doorCost);
            }

            // ------------------------------------------------------------------ 2. Corredores

            private void GrowCorridors(int target)
            {
                if (def.corridorDef == null || target <= 0) return;
                int cw = Mathf.Max(1, def.corridorWidth);
                int start = layout.StartSpaceIndex;
                RectInt startRect = layout.spaces[start].Rect;

                // 1º segmento: sai da convivência inicial (preferência para o norte, fundo da casa).
                var sides = new List<Side> { Side.East, Side.West };
                rng.Shuffle(sides);
                sides.Insert(rng.Chance(0.75f) ? 0 : 1, Side.North);
                foreach (var side in sides)
                {
                    int lo = AxisMin(startRect, side), hi = AxisMax(startRect, side) - cw;
                    if (hi < lo) continue;
                    int along = rng.Range(lo, hi);
                    if (TryCorridor(start, side, along)) break;
                }
                if (corridors.Count == 0) return;

                bool lastFailed = false;
                int guard = target * 12;
                while (corridors.Count < target && guard-- > 0)
                {
                    int parent;
                    Side side;
                    int along;
                    bool branch = lastFailed || rng.Chance(def.branchChance);
                    if (branch)
                    {
                        // Ramificação: sai do meio de um segmento qualquer, para um dos lados.
                        parent = corridors[rng.Range(0, corridors.Count - 1)];
                        RectInt pr = layout.spaces[parent].Rect;
                        side = Perpendicular(corridorDir[parent]);
                        int lo = AxisMin(pr, side), hi = AxisMax(pr, side) - cw;
                        along = rng.Range(lo, Mathf.Max(lo, hi));
                    }
                    else
                    {
                        parent = corridors[corridors.Count - 1];
                        RectInt pr = layout.spaces[parent].Rect;
                        Side dir = corridorDir[parent];
                        if (rng.Chance(def.turnChance))
                        {
                            // Vira 90°: sai do bloco final do segmento.
                            side = Perpendicular(dir);
                            along = (dir == Side.North || dir == Side.East) ? AxisMax(pr, side) - cw : AxisMin(pr, side);
                        }
                        else
                        {
                            side = dir; // segue reto
                            along = AxisMin(pr, side);
                        }
                    }
                    lastFailed = !TryCorridor(parent, side, along);
                }
            }

            private Side Perpendicular(Side dir)
            {
                bool vertical = dir == Side.North || dir == Side.South;
                bool first = rng.Chance(0.5f);
                if (vertical) return first ? Side.East : Side.West;
                return first ? Side.North : Side.South;
            }

            private bool TryCorridor(int parent, Side side, int along)
            {
                int cw = Mathf.Max(1, def.corridorWidth);
                int minLen = Mathf.Max(cw, def.segmentLength.x);
                int maxLen = Mathf.Max(minLen, def.segmentLength.y);
                RectInt pr = layout.spaces[parent].Rect;
                for (int len = rng.Range(minLen, maxLen); len >= minLen; len--)
                {
                    RectInt rect = Adjacent(pr, side, along, cw, len);
                    if (!Free(rect) || !CorridorClearance(rect, parent)) continue;

                    int idx = AddSpace(SpaceKind.Corridor, def.corridorDef, rect, false, parent, false);
                    corridors.Add(idx);
                    corridorDir[idx] = side;
                    // Passagem aberta: o vão ocupa toda a parede em comum (largura do corredor).
                    AddLink(parent, idx, side, along, along + cw, ConnectionType.Opening);
                    return true;
                }
                return false;
            }

            /// <summary>Corredores mantêm distância de outros corredores (cabe uma sala entre eles) e não encostam no resto.</summary>
            private bool CorridorClearance(RectInt rect, int parent)
            {
                const int otherClearance = 1;
                int corridorGap = Mathf.Max(1, MinRoomDepth());
                foreach (var s in layout.spaces)
                {
                    if (s.Index == parent) continue;
                    int need = s.Kind == SpaceKind.Corridor ? corridorGap : otherClearance;
                    if (Gap(rect, s.Rect) < need) return false;
                }
                return true;
            }

            private int MinRoomDepth()
            {
                int best = int.MaxValue;
                foreach (var r in pool)
                {
                    if (r == null || r.Kind != SpaceKind.Room) continue;
                    best = Mathf.Min(best, Mathf.Min(r.Size.x, r.Size.y));
                }
                return best == int.MaxValue ? 3 : best;
            }

            // ------------------------------------------------------------------ 3. Convivências extras

            private int PlaceExtraSocials(int target)
            {
                if (target <= 0 || corridors.Count == 0) return 0;
                var startDef = layout.spaces[layout.StartSpaceIndex].Def;
                var options = new List<RoomDef>();
                foreach (var s in def.socialPool)
                {
                    if (s != null && s.Kind == SpaceKind.Social && s != startDef && !options.Contains(s)) options.Add(s);
                }

                var counts = new Dictionary<RoomDef, int>();
                int placed = 0;
                for (int i = 0; i < target; i++)
                {
                    var weights = new List<float>();
                    foreach (var o in options)
                    {
                        int used = counts.TryGetValue(o, out var c) ? c : 0;
                        weights.Add(used < o.MaxPerRun ? o.Weight : 0f);
                    }
                    int pick = rng.PickWeightedIndex(weights);
                    if (pick < 0) break;
                    var socialDef = options[pick];
                    counts[socialDef] = (counts.TryGetValue(socialDef, out var n) ? n : 0) + 1;

                    if (PlaceAttached(socialDef, SpaceKind.Social, corridors, ConnectionType.Opening, false)) placed++;
                }
                return placed;
            }

            // ------------------------------------------------------------------ 4. Salas

            private int PlaceRooms(out int wanted, out int minRooms, out bool requiredOk)
            {
                // Pool sem repetição, só tipo Sala.
                var options = new List<RoomDef>();
                foreach (var r in pool)
                {
                    if (r != null && r.Kind == SpaceKind.Room && !options.Contains(r)) options.Add(r);
                }

                int capacity = PoolCapacity(options);
                minRooms = Mathf.Min(def.roomCount.x, capacity);
                wanted = Mathf.Min(rng.Range(def.roomCount.x, Mathf.Max(def.roomCount.x, def.roomCount.y)), capacity);

                // Obrigatórias primeiro, depois sorteio por peso respeitando maxPerRun.
                var counts = new Dictionary<RoomDef, int>();
                var required = new List<RoomDef>();
                var others = new List<RoomDef>();
                foreach (var r in options)
                {
                    if (!r.Required) continue;
                    required.Add(r);
                    counts[r] = 1;
                }
                while (required.Count + others.Count < wanted)
                {
                    var weights = new List<float>();
                    foreach (var o in options)
                    {
                        int used = counts.TryGetValue(o, out var c) ? c : 0;
                        weights.Add(used < o.MaxPerRun ? o.Weight : 0f);
                    }
                    int pick = rng.PickWeightedIndex(weights);
                    if (pick < 0) break;
                    var chosen = options[pick];
                    counts[chosen] = (counts.TryGetValue(chosen, out var n) ? n : 0) + 1;
                    others.Add(chosen);
                }

                // Maiores primeiro encaixam melhor (ordenação estável).
                var order = new List<int>();
                for (int i = 0; i < others.Count; i++) order.Add(i);
                order.Sort((a, b) =>
                {
                    int areaA = others[a].Size.x * others[a].Size.y, areaB = others[b].Size.x * others[b].Size.y;
                    return areaA != areaB ? areaB.CompareTo(areaA) : a.CompareTo(b);
                });

                var queue = new List<RoomDef>(required);
                foreach (int i in order) queue.Add(others[i]);

                requiredOk = true;
                int placed = 0;
                for (int i = 0; i < queue.Count; i++)
                {
                    bool ok = PlaceRoom(queue[i]);
                    if (ok) placed++;
                    else if (i < required.Count) requiredOk = false;
                }
                return placed;
            }

            private bool PlaceRoom(RoomDef roomDef)
            {
                // Sala "funda": abre para dentro de uma sala comum já colocada.
                if (rng.Chance(def.roomInsideRoomChance))
                {
                    var hosts = new List<int>();
                    foreach (var s in layout.spaces)
                    {
                        if (s.Kind == SpaceKind.Room && !s.IsDeep) hosts.Add(s.Index);
                    }
                    if (hosts.Count > 0 && PlaceAttached(roomDef, SpaceKind.Room, hosts, ConnectionType.Door, true)) return true;
                }

                var normal = new List<int>();
                foreach (var s in layout.spaces)
                {
                    if (s.Kind == SpaceKind.Corridor || s.Kind == SpaceKind.Social) normal.Add(s.Index);
                }
                return PlaceAttached(roomDef, SpaceKind.Room, normal, ConnectionType.Door, false);
            }

            /// <summary>Encaixa o espaço numa parede livre de um dos 'hosts' e cria a ligação na parede em comum.</summary>
            private bool PlaceAttached(RoomDef spaceDef, SpaceKind kind, List<int> hosts, ConnectionType type, bool deep)
            {
                var candidates = Candidates(spaceDef, hosts);
                if (candidates.Count == 0) return false;
                var c = candidates[rng.Range(0, candidates.Count - 1)];
                int idx = AddSpace(kind, spaceDef, c.Rect, c.Rotated, c.Host, deep);
                AddLink(c.Host, idx, c.Side, c.S0, c.S1, type);
                return true;
            }

            private List<Candidate> Candidates(RoomDef spaceDef, List<int> hosts)
            {
                var result = new List<Candidate>();
                Vector2Int size = spaceDef.Size;
                int minShared = Mathf.Max(1, def.minSharedWall);

                foreach (int host in hosts)
                {
                    RectInt hr = layout.spaces[host].Rect;
                    for (int s = 0; s < 4; s++)
                    {
                        var side = (Side)s;
                        for (int rot = 0; rot < 2; rot++)
                        {
                            bool rotated = rot == 1;
                            if (rotated && (!spaceDef.AllowRotation || size.x == size.y)) continue;
                            Vector2Int sz = rotated ? new Vector2Int(size.y, size.x) : size;
                            bool alongX = side == Side.North || side == Side.South;
                            int alongSize = alongX ? sz.x : sz.y;
                            int depth = alongX ? sz.y : sz.x;
                            int hostMin = AxisMin(hr, side), hostMax = AxisMax(hr, side);
                            if (hostMax - hostMin < minShared) continue;

                            for (int along = hostMin - alongSize + minShared; along <= hostMax - minShared; along++)
                            {
                                RectInt rect = Adjacent(hr, side, along, alongSize, depth);
                                if (!Free(rect)) continue;
                                int s0 = Mathf.Max(along, hostMin), s1 = Mathf.Min(along + alongSize, hostMax);
                                if (s1 - s0 < minShared) continue;
                                result.Add(new Candidate { Host = host, Side = side, Rect = rect, Rotated = rotated, S0 = s0, S1 = s1 });
                            }
                        }
                    }
                }
                return result;
            }

            // ------------------------------------------------------------------ 5. Paredes

            /// <summary>
            /// Percorre as bordas de cada espaço metro a metro e junta trechos com o mesmo vizinho.
            /// Parede compartilhada sai uma vez (A &lt; B); parede externa sai com B = Outside.
            /// </summary>
            private void ComputeWalls()
            {
                layout.walls.Clear();
                foreach (var s in layout.spaces)
                {
                    RectInt r = s.Rect;
                    EdgeWalls(s.Index, true, r.yMin, r.xMin, r.xMax, 0, -1);  // sul
                    EdgeWalls(s.Index, true, r.yMax, r.xMin, r.xMax, 0, 0);   // norte
                    EdgeWalls(s.Index, false, r.xMin, r.yMin, r.yMax, -1, 0); // oeste
                    EdgeWalls(s.Index, false, r.xMax, r.yMin, r.yMax, 0, 0);  // leste
                }
            }

            /// <param name="line">Coordenada fixa da borda (y se horizontal, x se vertical).</param>
            /// <param name="offX">/ <paramref name="offY"/>: deslocamento para achar a célula do OUTRO lado.</param>
            private void EdgeWalls(int space, bool horizontal, int line, int from, int to, int offX, int offY)
            {
                int runStart = from;
                int runNeighbor = NeighborAt(horizontal, line, from, offX, offY);
                for (int t = from + 1; t <= to; t++)
                {
                    int nb = t < to ? NeighborAt(horizontal, line, t, offX, offY) : int.MinValue;
                    if (nb == runNeighbor) continue;
                    EmitWall(space, runNeighbor, horizontal, line, runStart, t);
                    runStart = t;
                    runNeighbor = nb;
                }
            }

            private int NeighborAt(bool horizontal, int line, int t, int offX, int offY)
            {
                int x = horizontal ? t : line + offX;
                int y = horizontal ? line + offY : t;
                if (x < 0 || y < 0 || x >= width || y >= height) return HouseLayout.Outside;
                return grid[x, y];
            }

            private void EmitWall(int space, int neighbor, bool horizontal, int line, int a, int b)
            {
                if (neighbor != HouseLayout.Outside && neighbor < space) return; // já emitida pelo outro lado

                var wall = new HouseWall
                {
                    A = space,
                    B = neighbor,
                    Horizontal = horizontal,
                    From = horizontal ? new Vector2Int(a, line) : new Vector2Int(line, a),
                    To = horizontal ? new Vector2Int(b, line) : new Vector2Int(line, b),
                };

                foreach (var c in layout.connections)
                {
                    bool samePair = (c.A == space && c.B == neighbor) || (c.B == space && c.A == neighbor);
                    if (!samePair || c.HorizontalWall != horizontal) continue;
                    float cLine = horizontal ? c.Position.y : c.Position.x;
                    float cAlong = horizontal ? c.Position.x : c.Position.y;
                    if (Mathf.Abs(cLine - line) < 0.001f && cAlong > a && cAlong < b)
                    {
                        wall.ConnectionIndex = c.Index;
                        break;
                    }
                }
                layout.walls.Add(wall);
            }

            // ------------------------------------------------------------------ Utilitários

            private int AddSpace(SpaceKind kind, RoomDef spaceDef, RectInt rect, bool rotated, int parent, bool deep)
            {
                int idx = layout.spaces.Count;
                layout.spaces.Add(new HouseSpace
                {
                    Index = idx, Kind = kind, Def = spaceDef, Rect = rect, Rotated = rotated, ParentIndex = parent, IsDeep = deep,
                });
                for (int x = rect.xMin; x < rect.xMax; x++)
                    for (int y = rect.yMin; y < rect.yMax; y++)
                        grid[x, y] = idx;
                return idx;
            }

            private int AddConnection(int a, int b, ConnectionType type, Vector2 pos, bool horizontal, float w, int cost)
            {
                int idx = layout.connections.Count;
                layout.connections.Add(new HouseConnection
                {
                    Index = idx, A = a, B = b, Type = type, Position = pos, HorizontalWall = horizontal, Width = w, Cost = cost,
                });
                return idx;
            }

            /// <summary>Liga host e filho pelo lado 'side' do host, no trecho [s0, s1] da parede em comum.</summary>
            private void AddLink(int host, int child, Side side, int s0, int s1, ConnectionType type)
            {
                RectInt hr = layout.spaces[host].Rect;
                bool horizontal = side == Side.North || side == Side.South;
                int line = side == Side.North ? hr.yMax : side == Side.South ? hr.yMin : side == Side.East ? hr.xMax : hr.xMin;

                float w;
                float center;
                int cost;
                if (type == ConnectionType.Opening)
                {
                    w = Mathf.Min(s1 - s0, Mathf.Max(1, def.corridorWidth));
                    center = DoorCenter(s0, s1, w, 0f);
                    cost = def.openingCost;
                }
                else
                {
                    w = def.doorWidth;
                    center = DoorCenter(s0, s1, w, def.doorCornerMargin);
                    cost = def.doorCost;
                }
                var pos = horizontal ? new Vector2(center, line) : new Vector2(line, center);
                AddConnection(host, child, type, pos, horizontal, w, cost);
            }

            /// <summary>Centro do vão no trecho [s0, s1], longe dos cantos, em múltiplos de 0,5 m quando possível.</summary>
            private float DoorCenter(int s0, int s1, float doorWidth, float margin)
            {
                float lo = s0 + margin + doorWidth * 0.5f;
                float hi = s1 - margin - doorWidth * 0.5f;
                if (hi < lo) return (s0 + s1) * 0.5f;
                float first = Mathf.Ceil(lo * 2f - 0.0001f) / 2f;
                int steps = Mathf.FloorToInt((hi - first) * 2f + 0.0001f);
                if (steps < 0) return (lo + hi) * 0.5f;
                return first + rng.Range(0, steps) * 0.5f;
            }

            private bool Free(RectInt rect)
            {
                if (rect.xMin < 0 || rect.yMin < 0 || rect.xMax > width || rect.yMax > height) return false;
                for (int x = rect.xMin; x < rect.xMax; x++)
                    for (int y = rect.yMin; y < rect.yMax; y++)
                        if (grid[x, y] != -1) return false;
                return true;
            }

            /// <summary>Distância entre retângulos (0 = encostam pela borda ou canto).</summary>
            private static int Gap(RectInt a, RectInt b)
            {
                int dx = Mathf.Max(0, Mathf.Max(a.xMin - b.xMax, b.xMin - a.xMax));
                int dy = Mathf.Max(0, Mathf.Max(a.yMin - b.yMax, b.yMin - a.yMax));
                return Mathf.Max(dx, dy);
            }

            /// <summary>Início do lado no eixo em que ele corre (x para norte/sul, y para leste/oeste).</summary>
            private static int AxisMin(RectInt r, Side side) => side == Side.North || side == Side.South ? r.xMin : r.yMin;
            private static int AxisMax(RectInt r, Side side) => side == Side.North || side == Side.South ? r.xMax : r.yMax;

            /// <summary>Retângulo colado ao lado 'side' de 'host', começando em 'along' no eixo do lado.</summary>
            private static RectInt Adjacent(RectInt host, Side side, int along, int alongSize, int depth)
            {
                switch (side)
                {
                    case Side.North: return new RectInt(along, host.yMax, alongSize, depth);
                    case Side.South: return new RectInt(along, host.yMin - depth, alongSize, depth);
                    case Side.East: return new RectInt(host.xMax, along, depth, alongSize);
                    default: return new RectInt(host.xMin - depth, along, depth, alongSize);
                }
            }
        }
    }
}
