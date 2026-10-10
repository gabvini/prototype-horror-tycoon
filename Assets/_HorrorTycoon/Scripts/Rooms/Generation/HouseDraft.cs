using System;
using System.Collections.Generic;
using HorrorTycoon.Core;
using UnityEngine;

namespace HorrorTycoon.Rooms.Generation
{
    /// <summary>Onde uma peça escolhida nasceria, do outro lado de uma porta para o vazio.</summary>
    public struct DraftPlacement
    {
        public RectInt Rect;
        /// <summary>True se o tamanho ficou girado 90° em relação a RoomDef.Size.</summary>
        public bool Rotated;
    }

    /// <summary>Medidas do set em grid (vêm do HouseGenDef).</summary>
    public sealed class DraftRules
    {
        /// <summary>Lado da célula do grid em metros (o "módulo de set").</summary>
        public int Cell = 2;
        public float DoorWidth = 1.2f;
        /// <summary>Distância mínima do vão até o canto (limitada pelo que cabe numa célula).</summary>
        public float CornerMargin = 0.4f;
        public int DoorCost = 1;

        public float Margin => Mathf.Min(CornerMargin, Mathf.Max(0f, (Cell - DoorWidth) * 0.5f));

        public static DraftRules From(HouseGenDef def) => new DraftRules
        {
            Cell = Mathf.Max(1, def.gridCell),
            DoorWidth = def.doorWidth,
            CornerMargin = def.doorCornerMargin,
            DoorCost = def.doorCost,
        };
    }

    /// <summary>
    /// SET EM GRID (casa por escolha): a casa cresce peça a peça, como Blue Prince, num terreno dividido em células
    /// (HouseGenDef.gridCell, 2 m). C# puro.
    ///
    ///   1. StartLayout: só a convivência inicial (o Hall), colada na frente do terreno, com a porta da frente e uma
    ///      PORTA PARA O VAZIO (HouseDoorSite) em cada lado livre.
    ///   2. Ao abrir uma porta, o jogo oferece peças que CABEM ali (TryPlace / Eligible): salas, convivências e corredores.
    ///   3. AddRoom: a peça entra na planta colada à porta e ganha portas novas nos lados dela (AddSites). Portas que
    ///      dariam em algo já montado viram ligação direta; portas para o vazio cobertas pela peça nova levam a ela.
    /// Tudo alinhado ao grid: retângulos começam em múltiplos da célula e as portas ficam no meio de uma célula.
    /// </summary>
    public static class HouseDraft
    {
        // ================================================================== 1. Começo

        /// <summary>Planta inicial: a convivência inicial na frente do terreno, porta da frente e portas para o vazio.</summary>
        public static HouseLayout StartLayout(HouseGenDef def, GameRandom rng)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            var rules = DraftRules.From(def);
            int cell = rules.Cell;
            var layout = new HouseLayout
            {
                Bounds = new Vector2Int(Snap(Mathf.Max(cell * 4, def.bounds.x), cell), Snap(Mathf.Max(cell * 4, def.bounds.y), cell)),
                Seed = rng.Range(0, int.MaxValue - 1),
                Attempts = 1,
                GrowsByDraft = true,
                GridCell = cell,
            };

            RoomDef startDef = StartDef(def);
            Vector2Int size = new Vector2Int(Snap(startDef.Size.x, cell), Snap(startDef.Size.y, cell));
            size.x = Mathf.Min(size.x, layout.Bounds.x);
            size.y = Mathf.Min(size.y, layout.Bounds.y);
            int x = Snap((layout.Bounds.x - size.x) / 2, cell, false);
            var rect = new RectInt(x, 0, size.x, size.y);
            layout.spaces.Add(new HouseSpace { Index = 0, Kind = SpaceKind.Social, Def = startDef, Rect = rect, ParentIndex = -1 });
            layout.StartSpaceIndex = 0;

            // Porta da frente: numa célula do meio da parede sul.
            float doorX = CellCenters(rect.xMin, rect.xMax, cell)[0];
            layout.FrontDoorIndex = layout.connections.Count;
            layout.connections.Add(new HouseConnection
            {
                Index = 0, A = 0, B = HouseLayout.Outside, Type = ConnectionType.Door, Position = new Vector2(doorX, 0f),
                HorizontalWall = true, Width = rules.DoorWidth, Cost = rules.DoorCost,
            });

            AddSites(layout, 0, rules);
            layout.ComputeWalls();
            return layout;
        }

        private static RoomDef StartDef(HouseGenDef def)
        {
            RoomDef first = null;
            foreach (var s in def.socialPool)
            {
                if (s == null || s.Kind != SpaceKind.Social) continue;
                if (s.Required) return s;
                if (first == null) first = s;
            }
            if (first != null) return first;
            if (def.corridorDef != null) return def.corridorDef;
            throw new InvalidOperationException("HouseGenDef sem convivência (socialPool) nem corredor.");
        }

        /// <summary>Arredonda para múltiplo da célula (para cima por padrão; mínimo 1 célula).</summary>
        public static int Snap(int v, int cell, bool up = true)
        {
            if (cell <= 1) return Mathf.Max(1, v);
            int n = up ? (v + cell - 1) / cell : v / cell;
            return Mathf.Max(cell, n * cell);
        }

        /// <summary>Centros das células do trecho [a, b), do meio para as pontas (a porta prefere o meio da parede).</summary>
        public static List<float> CellCenters(int a, int b, int cell)
        {
            var list = new List<float>();
            int n = Mathf.Max(1, (b - a) / Mathf.Max(1, cell));
            float mid = (n - 1) * 0.5f;
            var order = new List<int>();
            for (int i = 0; i < n; i++) order.Add(i);
            order.Sort((i, j) =>
            {
                int c = Mathf.Abs(i - mid).CompareTo(Mathf.Abs(j - mid));
                return c != 0 ? c : i.CompareTo(j);
            });
            foreach (int i in order) list.Add(a + cell * i + cell * 0.5f);
            return list;
        }

        /// <summary>Zona do terreno pela profundidade: terço da frente = Social, meio = Serviço, fundos = Íntima.</summary>
        public static HouseZone ZoneAt(float y, int depth)
        {
            float t = depth > 0 ? y / depth : 0f;
            if (t < 1f / 3f) return HouseZone.Social;
            if (t < 2f / 3f) return HouseZone.Service;
            return HouseZone.Private;
        }

        // ================================================================== Portas de uma peça

        private static readonly Vector2Int[] Sides = { new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(-1, 0) };

        /// <summary>
        /// Uma porta por lado da peça (nos lados com porta no RoomDef, já com o giro), na célula mais ao meio que dá:
        /// para o vazio vira porta para o vazio; para algo já montado vira ligação direta (se ainda não houver ligação
        /// entre os dois). O lado por onde a peça foi aberta e o lado da porta da frente ficam de fora.
        /// </summary>
        public static void AddSites(HouseLayout layout, int space, DraftRules rules)
        {
            var s = layout.spaces[space];
            RectInt r = s.Rect;
            int cell = rules.Cell;
            foreach (var o in Sides)
            {
                if (s.Def != null && !SideHasDoor(s.Def, o, s.Rotated)) continue;
                if (HasConnectionOnSide(layout, space, o)) continue;

                bool alongX = o.y != 0;
                int lo = alongX ? r.xMin : r.yMin, hi = alongX ? r.xMax : r.yMax;
                int line = o.y > 0 ? r.yMax : o.y < 0 ? r.yMin : o.x > 0 ? r.xMax : r.xMin;
                foreach (float along in CellCenters(lo, hi, cell))
                {
                    var pos = alongX ? new Vector2(along, line) : new Vector2(line, along);
                    var landing = LandingRect(pos, alongX, o, cell);
                    if (landing.xMin < 0 || landing.yMin < 0 || landing.xMax > layout.Bounds.x || landing.yMax > layout.Bounds.y) continue;

                    int neighbor = OccupantOf(layout, landing);
                    if (neighbor == space) continue;
                    if (neighbor >= 0)
                    {
                        if (Connected(layout, space, neighbor)) break;
                        int c = layout.connections.Count;
                        layout.connections.Add(new HouseConnection
                        {
                            Index = c, A = Mathf.Min(space, neighbor), B = Mathf.Max(space, neighbor), Type = ConnectionType.Door,
                            Position = pos, HorizontalWall = alongX, Width = rules.DoorWidth, Cost = rules.DoorCost,
                        });
                        break;
                    }

                    if (SiteAt(layout, pos, alongX)) break;
                    layout.sites.Add(new HouseDoorSite
                    {
                        Index = layout.sites.Count, Host = space, Position = pos, HorizontalWall = alongX, Width = rules.DoorWidth,
                        Outward = o, Zone = ZoneAt(pos.y, layout.Bounds.y),
                    });
                    break;
                }
            }
        }

        /// <summary>A peça tem porta deste lado (DoorSides do RoomDef, girando 90° no sentido horário se 'rotated')?</summary>
        private static bool SideHasDoor(RoomDef def, Vector2Int worldSide, bool rotated)
        {
            DoorSides doors = def.Doors;
            if (doors == DoorSides.All) return true;
            DoorSides side = SideOf(worldSide);
            if (rotated) side = CounterClockwise(side); // lado sem giro que, girado 90° horário, vira 'worldSide'
            return (doors & side) != 0;
        }

        private static bool HasConnectionOnSide(HouseLayout layout, int space, Vector2Int o)
        {
            RectInt r = layout.spaces[space].Rect;
            foreach (var c in layout.connections)
            {
                if (c.A != space && c.B != space) continue;
                if (OutwardOf(r, c.Position, c.HorizontalWall) == o && OnEdge(r, c.Position, c.HorizontalWall)) return true;
            }
            foreach (var site in layout.sites)
            {
                if (site.Host == space && site.Outward == o && site.State != DoorSiteState.Blocked) return true;
            }
            return false;
        }

        private static bool OnEdge(RectInt r, Vector2 p, bool horizontal) =>
            horizontal ? Mathf.Abs(p.y - r.yMin) < 0.01f || Mathf.Abs(p.y - r.yMax) < 0.01f
                       : Mathf.Abs(p.x - r.xMin) < 0.01f || Mathf.Abs(p.x - r.xMax) < 0.01f;

        private static bool Connected(HouseLayout layout, int a, int b)
        {
            foreach (var c in layout.connections)
            {
                if ((c.A == a && c.B == b) || (c.A == b && c.B == a)) return true;
            }
            return false;
        }

        private static bool SiteAt(HouseLayout layout, Vector2 pos, bool horizontal)
        {
            foreach (var s in layout.sites)
            {
                if (s.HorizontalWall == horizontal && (s.Position - pos).sqrMagnitude < 0.01f) return true;
            }
            return false;
        }

        private static int OccupantOf(HouseLayout layout, RectInt rect)
        {
            foreach (var s in layout.spaces) if (s.Rect.Overlaps(rect)) return s.Index;
            return -1;
        }

        /// <summary>Direção que sai do retângulo pela parede onde está o ponto.</summary>
        public static Vector2Int OutwardOf(RectInt r, Vector2 doorPos, bool horizontalWall)
        {
            if (horizontalWall) return Mathf.Abs(doorPos.y - r.yMin) < 0.01f ? new Vector2Int(0, -1) : new Vector2Int(0, 1);
            return Mathf.Abs(doorPos.x - r.xMin) < 0.01f ? new Vector2Int(-1, 0) : new Vector2Int(1, 0);
        }

        // ================================================================== 2. Onde cabe

        /// <summary>
        /// Onde a peça caberia do outro lado da porta: colada à parede do host, cobrindo o vão com as margens,
        /// alinhada ao grid, dentro do terreno e sem passar por cima de nada. A mais centrada na porta. False = não cabe.
        /// Giro de 90° só se a peça permite; ela precisa ter porta (DoorSides) do lado que encosta no host.
        /// </summary>
        public static bool TryPlace(HouseLayout layout, HouseDoorSite site, RoomDef def, DraftRules rules, out DraftPlacement placement)
        {
            placement = default;
            if (layout == null || site == null || def == null || site.Host < 0 || site.Host >= layout.spaces.Count) return false;

            int cell = Mathf.Max(1, rules.Cell);
            RectInt hr = layout.spaces[site.Host].Rect;
            Vector2Int o = site.Outward;
            bool alongX = o.y != 0;
            float door = alongX ? site.Position.x : site.Position.y;
            float half = site.Width * 0.5f + rules.Margin;
            Vector2Int baseSize = new Vector2Int(Snap(def.Size.x, cell), Snap(def.Size.y, cell));

            bool found = false;
            float bestDist = float.MaxValue;
            for (int rot = 0; rot < 2; rot++)
            {
                bool rotated = rot == 1;
                if (rotated && (!def.AllowRotation || baseSize.x == baseSize.y)) continue;
                if (!SideHasDoor(def, -o, rotated)) continue;

                Vector2Int sz = rotated ? new Vector2Int(baseSize.y, baseSize.x) : baseSize;
                int alongSize = alongX ? sz.x : sz.y;
                int depth = alongX ? sz.y : sz.x;
                int lo = Mathf.CeilToInt(door + half - alongSize - 0.0001f);
                int hi = Mathf.FloorToInt(door - half + 0.0001f);
                for (int a = lo; a <= hi; a++)
                {
                    if (((a % cell) + cell) % cell != 0) continue; // alinhado ao grid
                    RectInt rect;
                    if (o.y > 0) rect = new RectInt(a, hr.yMax, alongSize, depth);
                    else if (o.y < 0) rect = new RectInt(a, hr.yMin - depth, alongSize, depth);
                    else if (o.x > 0) rect = new RectInt(hr.xMax, a, depth, alongSize);
                    else rect = new RectInt(hr.xMin - depth, a, depth, alongSize);
                    if (!layout.IsFree(rect)) continue;

                    float dist = Mathf.Abs(a + alongSize * 0.5f - door);
                    if (dist < bestDist - 0.001f)
                    {
                        bestDist = dist;
                        placement = new DraftPlacement { Rect = rect, Rotated = rotated };
                        found = true;
                    }
                }
            }
            return found;
        }

        private static DoorSides SideOf(Vector2Int d)
        {
            if (d.y > 0) return DoorSides.North;
            if (d.y < 0) return DoorSides.South;
            return d.x > 0 ? DoorSides.East : DoorSides.West;
        }

        private static DoorSides CounterClockwise(DoorSides s)
        {
            switch (s)
            {
                case DoorSides.North: return DoorSides.West;
                case DoorSides.West: return DoorSides.South;
                case DoorSides.South: return DoorSides.East;
                default: return DoorSides.North;
            }
        }

        // ================================================================== Oferta

        /// <summary>A peça entra na escolha? Salas, convivências e corredores (corredor sem limite por casa).</summary>
        public static bool IsDraftable(RoomDef def) => def != null && !def.ExcludeFromDraft && DraftWeight(def) > 0f;

        /// <summary>
        /// Peças que podem nascer nesta porta agora (cabem, não passaram do máximo por casa, não estão fora do sorteio).
        /// Salas preferem a zona da porta; se nenhuma sala da zona couber, vale qualquer zona.
        /// </summary>
        public static List<RoomDef> Eligible(HouseLayout layout, HouseDoorSite site, IReadOnlyList<RoomDef> pool,
            DraftRules rules, Func<RoomDef, bool> allowed = null)
        {
            var result = new List<RoomDef>();
            if (layout == null || site == null || !site.IsOpen || pool == null) return result;

            var used = new Dictionary<RoomDef, int>();
            foreach (var s in layout.spaces)
            {
                if (s.Def != null) used[s.Def] = (used.TryGetValue(s.Def, out int n) ? n : 0) + 1;
            }

            var rooms = new List<RoomDef>();
            var roomsInZone = new List<RoomDef>();
            foreach (var def in pool)
            {
                if (!IsDraftable(def) || result.Contains(def) || rooms.Contains(def)) continue;
                if (def.Kind != SpaceKind.Corridor && used.TryGetValue(def, out int count) && count >= def.MaxPerRun) continue;
                if (allowed != null && !allowed(def)) continue;
                if (!TryPlace(layout, site, def, rules, out _)) continue;
                if (def.Kind == SpaceKind.Room)
                {
                    rooms.Add(def);
                    if (def.AllowsZone(site.Zone)) roomsInZone.Add(def);
                }
                else result.Add(def);
            }
            result.AddRange(roomsInZone.Count > 0 ? roomsInZone : rooms);
            return result;
        }

        /// <summary>Peso no sorteio da oferta (peça obrigatória sem peso vale 1).</summary>
        public static float DraftWeight(RoomDef def) => def.Weight > 0f ? def.Weight : def.Required ? 1f : 0f;

        /// <summary>Sorteia até 'count' peças diferentes entre as elegíveis, pelo peso. No máximo 1 corredor por oferta.</summary>
        public static List<RoomDef> RollOffer(List<RoomDef> eligible, int count, GameRandom rng)
        {
            var result = new List<RoomDef>();
            var left = new List<RoomDef>(eligible);
            while (result.Count < count && left.Count > 0)
            {
                var weights = new List<float>();
                foreach (var d in left) weights.Add(DraftWeight(d));
                int i = rng.PickWeightedIndex(weights);
                if (i < 0) break;
                var pick = left[i];
                result.Add(pick);
                left.RemoveAt(i);
                if (pick.Kind == SpaceKind.Corridor) left.RemoveAll(d => d.Kind == SpaceKind.Corridor);
            }
            return result;
        }

        // ================================================================== 3. Peça nova

        /// <summary>
        /// Põe a peça na planta do outro lado da porta (ligação Porta no mesmo vão), dá portas novas a ela e recalcula as
        /// paredes. Outras portas para o vazio cobertas pela peça passam a levar a ela (se o vão cabe na parede em comum
        /// e ainda não há porta entre os dois) ou viram parede. Devolve o índice do espaço novo.
        /// </summary>
        public static int AddRoom(HouseLayout layout, HouseDoorSite site, RoomDef def, DraftPlacement p, DraftRules rules)
        {
            if (layout == null || site == null || def == null) throw new ArgumentNullException();
            if (!site.IsOpen) throw new InvalidOperationException("Porta já aberta.");

            int idx = layout.spaces.Count;
            layout.spaces.Add(new HouseSpace
            {
                Index = idx, Kind = def.Kind, Def = def, Rect = p.Rect, Rotated = p.Rotated, ParentIndex = site.Host, IsDeep = false,
            });
            Connect(layout, site, idx, rules.DoorCost);

            foreach (var other in layout.sites)
            {
                if (!other.IsOpen || !Landing(other).Overlaps(p.Rect)) continue;
                RectInt hr = layout.spaces[other.Host].Rect;
                bool fits = HouseLayout.TryGetSharedWall(hr, p.Rect, out Vector2Int from, out Vector2Int to);
                if (fits)
                {
                    float along = other.HorizontalWall ? other.Position.x : other.Position.y;
                    float lo = other.HorizontalWall ? from.x : from.y;
                    float hi = other.HorizontalWall ? to.x : to.y;
                    float line = other.HorizontalWall ? from.y : from.x;
                    float sLine = other.HorizontalWall ? other.Position.y : other.Position.x;
                    fits = other.HorizontalWall == (from.y == to.y) && Mathf.Abs(line - sLine) < 0.001f
                           && along - other.Width * 0.5f >= lo - 0.001f && along + other.Width * 0.5f <= hi + 0.001f;
                }
                // Uma porta só entre os mesmos dois espaços (a parede em comum tem um vão só).
                if (fits && Connected(layout, other.Host, idx)) fits = false;
                if (fits) Connect(layout, other, idx, rules.DoorCost);
                else other.State = DoorSiteState.Blocked;
            }

            AddSites(layout, idx, rules);
            layout.ComputeWalls();
            return idx;
        }

        private static void Connect(HouseLayout layout, HouseDoorSite site, int space, int doorCost)
        {
            int c = layout.connections.Count;
            layout.connections.Add(new HouseConnection
            {
                Index = c, A = site.Host, B = space, Type = ConnectionType.Door, Position = site.Position,
                HorizontalWall = site.HorizontalWall, Width = site.Width, Cost = doorCost,
            });
            site.State = DoorSiteState.Built;
            site.Space = space;
        }

        /// <summary>Faixa de 1 m logo do lado de fora do vão (o "patamar" da porta).</summary>
        public static RectInt Landing(HouseDoorSite site) => LandingRect(site.Position, site.HorizontalWall, site.Outward, 1);

        /// <summary>Retângulo de 'depth' m do lado de fora do vão, cobrindo a largura da célula da porta.</summary>
        private static RectInt LandingRect(Vector2 pos, bool alongX, Vector2Int o, int depth)
        {
            float along = alongX ? pos.x : pos.y;
            int a0 = Mathf.FloorToInt(along - 0.5f + 0.001f);
            int a1 = Mathf.CeilToInt(along + 0.5f - 0.001f);
            int line = Mathf.RoundToInt(alongX ? pos.y : pos.x);
            if (alongX) return new RectInt(a0, o.y > 0 ? line : line - depth, Mathf.Max(1, a1 - a0), depth);
            return new RectInt(o.x > 0 ? line : line - depth, a0, depth, Mathf.Max(1, a1 - a0));
        }
    }
}
