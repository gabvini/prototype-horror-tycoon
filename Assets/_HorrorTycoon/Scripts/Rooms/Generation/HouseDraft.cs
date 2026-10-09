using System;
using System.Collections.Generic;
using HorrorTycoon.Core;
using UnityEngine;

namespace HorrorTycoon.Rooms.Generation
{
    /// <summary>Onde uma sala escolhida nasceria, do outro lado de uma porta para o vazio.</summary>
    public struct DraftPlacement
    {
        public RectInt Rect;
        /// <summary>True se o tamanho ficou girado 90° em relação a RoomDef.Size.</summary>
        public bool Rotated;
    }

    /// <summary>
    /// CASA POR ESCOLHA (a casa cresce porta a porta, como Blue Prince, mas sem grade). C# puro.
    ///
    ///   1. Skeleton: da casa gerada inteira fica só o "esqueleto" (convivência inicial, corredores, convivências extras).
    ///      Cada sala que o gerador tinha preso a um corredor/convivência vira uma PORTA PARA O VAZIO (HouseDoorSite)
    ///      na mesma posição: assim sempre há espaço do outro lado.
    ///   2. Ao abrir uma porta, o jogo oferece até N salas que CABEM ali (Placements) e combinam com a zona.
    ///   3. AddRoom: a sala escolhida entra na planta colada à porta. Se ela cobrir outra porta para o vazio,
    ///      essa porta passa a levar à sala nova (ou vira parede, se não encaixar).
    /// </summary>
    public static class HouseDraft
    {
        // ================================================================== 1. Esqueleto

        /// <summary>
        /// Planta inicial da casa por escolha a partir da casa gerada inteira: sem salas, com uma porta para o vazio
        /// onde cada sala presa a um corredor/convivência estava. Índices dos espaços do esqueleto não mudam
        /// (o gerador coloca as salas por último).
        /// </summary>
        public static HouseLayout Skeleton(HouseLayout full)
        {
            if (full == null) throw new ArgumentNullException(nameof(full));
            int k = 0;
            while (k < full.spaces.Count && full.spaces[k].Kind != SpaceKind.Room) k++;

            var layout = new HouseLayout
            {
                Bounds = full.Bounds,
                Seed = full.Seed,
                Attempts = full.Attempts,
                UsedFallback = full.UsedFallback,
                StartSpaceIndex = full.StartSpaceIndex,
                GrowsByDraft = true,
            };
            for (int i = 0; i < k; i++)
            {
                var s = full.spaces[i];
                layout.spaces.Add(new HouseSpace
                {
                    Index = i, Kind = s.Kind, Def = s.Def, Rect = s.Rect, Rotated = s.Rotated, ParentIndex = s.ParentIndex, IsDeep = s.IsDeep,
                });
            }

            foreach (var c in full.connections)
            {
                bool aIn = c.A < k, bIn = c.B < k; // B = Outside (-1) conta como "dentro"
                if (aIn && bIn)
                {
                    int idx = layout.connections.Count;
                    layout.connections.Add(new HouseConnection
                    {
                        Index = idx, A = c.A, B = c.B, Type = c.Type, Position = c.Position, HorizontalWall = c.HorizontalWall,
                        Width = c.Width, Cost = c.Cost,
                    });
                    if (c.Index == full.FrontDoorIndex) layout.FrontDoorIndex = idx;
                    continue;
                }

                // Sala presa ao esqueleto: a porta fica, a sala some (salas "fundas" ligam sala-sala e somem junto).
                int host = aIn ? c.A : bIn ? c.B : -1;
                if (host < 0) continue;
                layout.sites.Add(new HouseDoorSite
                {
                    Index = layout.sites.Count,
                    Host = host,
                    Position = c.Position,
                    HorizontalWall = c.HorizontalWall,
                    Width = c.Width,
                    Outward = OutwardOf(layout.spaces[host].Rect, c.Position, c.HorizontalWall),
                    Zone = ZoneAt(c.Position.y, full.Bounds.y),
                });
            }

            layout.ComputeWalls();
            return layout;
        }

        /// <summary>Direção que sai do retângulo pela parede onde está o ponto.</summary>
        public static Vector2Int OutwardOf(RectInt r, Vector2 doorPos, bool horizontalWall)
        {
            if (horizontalWall) return Mathf.Abs(doorPos.y - r.yMin) < 0.01f ? new Vector2Int(0, -1) : new Vector2Int(0, 1);
            return Mathf.Abs(doorPos.x - r.xMin) < 0.01f ? new Vector2Int(-1, 0) : new Vector2Int(1, 0);
        }

        /// <summary>Zona do terreno pela profundidade: terço da frente = Social, meio = Serviço, fundos = Íntima.</summary>
        public static HouseZone ZoneAt(float y, int depth)
        {
            float t = depth > 0 ? y / depth : 0f;
            if (t < 1f / 3f) return HouseZone.Social;
            if (t < 2f / 3f) return HouseZone.Service;
            return HouseZone.Private;
        }

        // ================================================================== 2. Onde cabe

        /// <summary>
        /// Onde a sala caberia do outro lado da porta: colada à parede do host, cobrindo o vão com as margens,
        /// dentro do terreno e sem encostar por cima de nada. A mais centrada na porta. False = não cabe.
        /// Giro: 0°/180° sempre; 90°/270° só se a sala permite. A sala precisa ter porta (DoorSides) no lado do host.
        /// </summary>
        public static bool TryPlace(HouseLayout layout, HouseDoorSite site, RoomDef def, float cornerMargin, out DraftPlacement placement)
        {
            placement = default;
            if (layout == null || site == null || def == null || site.Host < 0 || site.Host >= layout.spaces.Count) return false;

            RectInt hr = layout.spaces[site.Host].Rect;
            Vector2Int o = site.Outward;
            bool alongX = o.y != 0;
            float door = alongX ? site.Position.x : site.Position.y;
            float half = site.Width * 0.5f + Mathf.Max(0f, cornerMargin);
            Vector2Int baseSize = def.Size;
            DoorSides facing = SideOf(-o); // lado da sala que encosta no host

            bool found = false;
            float bestDist = float.MaxValue;
            for (int rot = 0; rot < 2; rot++)
            {
                bool rotated = rot == 1;
                if (rotated && (!def.AllowRotation || baseSize.x == baseSize.y)) continue;
                if (!HasDoorFacing(def, facing, rotated)) continue;

                Vector2Int sz = rotated ? new Vector2Int(baseSize.y, baseSize.x) : baseSize;
                int alongSize = alongX ? sz.x : sz.y;
                int depth = alongX ? sz.y : sz.x;
                int lo = Mathf.CeilToInt(door + half - alongSize - 0.0001f);
                int hi = Mathf.FloorToInt(door - half + 0.0001f);
                for (int a = lo; a <= hi; a++)
                {
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

        /// <summary>Gira um lado 90° no sentido horário (visto de cima, norte para cima).</summary>
        private static DoorSides Clockwise(DoorSides s)
        {
            switch (s)
            {
                case DoorSides.North: return DoorSides.East;
                case DoorSides.East: return DoorSides.South;
                case DoorSides.South: return DoorSides.West;
                default: return DoorSides.North;
            }
        }

        /// <summary>
        /// Algum giro (0/180 sem trocar o tamanho, 90/270 trocando) leva uma porta da sala (DoorSides, sem giro)
        /// para o lado 'facing'?
        /// </summary>
        private static bool HasDoorFacing(RoomDef def, DoorSides facing, bool rotated)
        {
            DoorSides doors = def.Doors;
            if (doors == DoorSides.All) return true;
            for (int q = rotated ? 1 : 0; q < 4; q += 2)
            {
                // Lado sem giro que, girado q vezes, vira 'facing'.
                DoorSides s = facing;
                for (int i = 0; i < (4 - q) % 4; i++) s = Clockwise(s);
                if ((doors & s) != 0) return true;
            }
            return false;
        }

        // ================================================================== Oferta

        /// <summary>
        /// Salas que podem nascer nesta porta agora (cabem, não passaram do máximo por casa, não estão fora do sorteio).
        /// Prefere as da zona da porta; se nenhuma da zona couber, vale qualquer zona.
        /// </summary>
        public static List<RoomDef> Eligible(HouseLayout layout, HouseDoorSite site, IReadOnlyList<RoomDef> pool,
            float cornerMargin, Func<RoomDef, bool> allowed = null)
        {
            var inZone = new List<RoomDef>();
            var any = new List<RoomDef>();
            if (layout == null || site == null || !site.IsOpen || pool == null) return any;

            var used = new Dictionary<RoomDef, int>();
            foreach (var s in layout.spaces)
            {
                if (s.Def != null) used[s.Def] = (used.TryGetValue(s.Def, out int n) ? n : 0) + 1;
            }

            foreach (var def in pool)
            {
                if (def == null || def.Kind != SpaceKind.Room || def.ExcludeFromDraft || any.Contains(def)) continue;
                if (DraftWeight(def) <= 0f) continue;
                if (used.TryGetValue(def, out int count) && count >= def.MaxPerRun) continue;
                if (allowed != null && !allowed(def)) continue;
                if (!TryPlace(layout, site, def, cornerMargin, out _)) continue;
                any.Add(def);
                if (def.AllowsZone(site.Zone)) inZone.Add(def);
            }
            return inZone.Count > 0 ? inZone : any;
        }

        /// <summary>Peso no sorteio da oferta (sala obrigatória sem peso vale 1).</summary>
        public static float DraftWeight(RoomDef def) => def.Weight > 0f ? def.Weight : def.Required ? 1f : 0f;

        /// <summary>Sorteia até 'count' salas diferentes entre as elegíveis, pelo peso.</summary>
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
                result.Add(left[i]);
                left.RemoveAt(i);
            }
            return result;
        }

        // ================================================================== 3. Sala nova

        /// <summary>
        /// Põe a sala na planta do outro lado da porta (ligação Porta no mesmo vão) e recalcula as paredes.
        /// Outras portas para o vazio cobertas pela sala nova passam a levar a ela (se o vão cabe na parede em comum)
        /// ou viram parede. Devolve o índice do espaço novo.
        /// </summary>
        public static int AddRoom(HouseLayout layout, HouseDoorSite site, RoomDef def, DraftPlacement p, int doorCost)
        {
            if (layout == null || site == null || def == null) throw new ArgumentNullException();
            if (!site.IsOpen) throw new InvalidOperationException("Porta já aberta.");

            int idx = layout.spaces.Count;
            layout.spaces.Add(new HouseSpace
            {
                Index = idx, Kind = SpaceKind.Room, Def = def, Rect = p.Rect, Rotated = p.Rotated, ParentIndex = site.Host, IsDeep = false,
            });
            Connect(layout, site, idx, doorCost);

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
                foreach (var c in layout.connections)
                {
                    if ((c.A == other.Host && c.B == idx) || (c.B == other.Host && c.A == idx)) fits = false;
                }
                if (fits) Connect(layout, other, idx, doorCost);
                else other.State = DoorSiteState.Blocked;
            }

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
        public static RectInt Landing(HouseDoorSite site)
        {
            bool alongX = site.HorizontalWall;
            float along = alongX ? site.Position.x : site.Position.y;
            int a0 = Mathf.FloorToInt(along - site.Width * 0.5f + 0.001f);
            int a1 = Mathf.CeilToInt(along + site.Width * 0.5f - 0.001f);
            int line = Mathf.RoundToInt(alongX ? site.Position.y : site.Position.x);
            Vector2Int o = site.Outward;
            if (alongX) return new RectInt(a0, o.y > 0 ? line : line - 1, a1 - a0, 1);
            return new RectInt(o.x > 0 ? line : line - 1, a0, 1, a1 - a0);
        }
    }
}
