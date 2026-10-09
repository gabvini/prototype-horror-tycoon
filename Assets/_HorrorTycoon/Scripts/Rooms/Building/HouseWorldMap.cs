using System.Collections.Generic;
using HorrorTycoon.Rooms.Generation;
using UnityEngine;

namespace HorrorTycoon.Rooms.Building
{
    /// <summary>Um pedaço reto de parede ao longo do eixo da parede (metros da grade da casa).</summary>
    public struct WallPiece
    {
        /// <summary>Início e fim ao longo do eixo da parede (x se horizontal, y se vertical).</summary>
        public float From, To;
        /// <summary>Base e altura (m). Parede inteira: 0 e pé-direito. Verga: altura da porta e o resto.</summary>
        public float Bottom, Height;
        /// <summary>Verga (pedaço acima de uma porta): sem colisor, some inteira no corte.</summary>
        public bool Lintel;

        public float Length => To - From;
    }

    /// <summary>
    /// "Régua" entre a planta gerada (HouseLayout, metros, x = leste, y = norte) e o mundo 3D.
    /// C# puro (sem cena): a montagem (HouseBuilder) e os testes usam as mesmas contas.
    ///
    /// Mundo: (x, 0, y) + origem. A fachada sul da planta (y = 0, porta da frente) fica em z = FrontZ.
    /// Em X a pegada REAL da casa (união dos espaços) fica centrada em x = 0 (ou o retângulo máximo, se pedido).
    /// </summary>
    public sealed class HouseWorldMap
    {
        public HouseLayout Layout { get; }
        /// <summary>z do mundo da linha y = 0 da planta (fachada da frente).</summary>
        public float FrontZ { get; }
        /// <summary>x do mundo = x da planta + OriginX.</summary>
        public float OriginX { get; }
        /// <summary>Retângulo que contém todos os espaços (metros da planta).</summary>
        public RectInt FootprintGrid { get; }

        public HouseWorldMap(HouseLayout layout, float frontZ, bool centerFootprintX)
        {
            Layout = layout;
            FrontZ = frontZ;

            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
            foreach (var s in layout.Spaces)
            {
                x0 = Mathf.Min(x0, s.Rect.xMin);
                y0 = Mathf.Min(y0, s.Rect.yMin);
                x1 = Mathf.Max(x1, s.Rect.xMax);
                y1 = Mathf.Max(y1, s.Rect.yMax);
            }
            // Casa por escolha: a pegada cresce a cada sala, mas o mundo não pode andar. Vale o terreno inteiro.
            if (x0 > x1 || layout.GrowsByDraft) { x0 = y0 = 0; x1 = layout.Bounds.x; y1 = layout.Bounds.y; }
            FootprintGrid = new RectInt(x0, y0, x1 - x0, y1 - y0);
            OriginX = centerFootprintX ? -(x0 + x1) * 0.5f : -layout.Bounds.x * 0.5f;
        }

        public Vector3 ToWorld(float x, float y) => new Vector3(x + OriginX, 0f, y + FrontZ);
        public Vector3 ToWorld(Vector2 p) => ToWorld(p.x, p.y);
        public Vector3 SpaceCenter(int space) => ToWorld(Layout.Spaces[space].Center);

        /// <summary>Pegada da casa no mundo: x = xMin, y = zMin (Rect no plano XZ).</summary>
        public Rect FootprintWorld => new Rect(FootprintGrid.xMin + OriginX, FootprintGrid.yMin + FrontZ,
                                               FootprintGrid.width, FootprintGrid.height);

        /// <summary>Coordenada fixa da parede no mundo (z se horizontal, x se vertical).</summary>
        public float WallLineWorld(HouseWall w) => w.Horizontal ? w.From.y + FrontZ : w.From.x + OriginX;

        /// <summary>Converte uma posição AO LONGO da parede (metros da planta) para o mundo.</summary>
        public float AlongWorld(bool horizontal, float along) => horizontal ? along + OriginX : along + FrontZ;

        /// <summary>Ponto do mundo (no chão) sobre a linha da parede, na posição 'along' da planta.</summary>
        public Vector3 WallPoint(HouseWall w, float along) =>
            w.Horizontal ? ToWorld(along, w.From.y) : ToWorld(w.From.x, along);

        // ================================================================== Porta de cada espaço

        /// <summary>
        /// Porta "principal" do espaço (a do card / plano da porta / convenção dos móveis):
        /// espaço inicial = porta da frente; senão a ligação com o espaço-pai (ParentIndex); senão a primeira ligação.
        /// </summary>
        public static HouseConnection PrimaryConnection(HouseLayout layout, int space)
        {
            if (space == layout.StartSpaceIndex && layout.FrontDoor != null) return layout.FrontDoor;
            int parent = layout.Spaces[space].ParentIndex;
            HouseConnection first = null;
            foreach (var c in layout.ConnectionsOf(space))
            {
                if (first == null) first = c;
                if (parent >= 0 && c.Other(space) == parent) return c;
            }
            return first;
        }

        /// <summary>Direção (na grade, unitária) que SAI do espaço pela parede da ligação: (±1,0) ou (0,±1).</summary>
        public static Vector2Int Outward(HouseLayout layout, int space, HouseConnection c)
        {
            RectInt r = layout.Spaces[space].Rect;
            if (c.HorizontalWall)
            {
                float line = c.Position.y;
                return Mathf.Abs(line - r.yMin) < 0.01f ? new Vector2Int(0, -1) : new Vector2Int(0, 1);
            }
            else
            {
                float line = c.Position.x;
                return Mathf.Abs(line - r.xMin) < 0.01f ? new Vector2Int(-1, 0) : new Vector2Int(1, 0);
            }
        }

        public static Vector3 OutwardWorld(Vector2Int d) => new Vector3(d.x, 0f, d.y);

        /// <summary>Lado da parede (para fora do espaço) de um trecho de parede do espaço.</summary>
        public static Vector2Int WallOutward(HouseLayout layout, int space, HouseWall w)
        {
            RectInt r = layout.Spaces[space].Rect;
            if (w.Horizontal) return w.From.y == r.yMin ? new Vector2Int(0, -1) : new Vector2Int(0, 1);
            return w.From.x == r.xMin ? new Vector2Int(-1, 0) : new Vector2Int(1, 0);
        }

        // ================================================================== Referencial do interior

        /// <summary>
        /// Giro (graus em Y) do "Interior" para a convenção do FurnitureKit: porta principal no +X local.
        /// Leste 0, norte -90 (=270), oeste 180, sul 90.
        /// </summary>
        public static float FurnitureYaw(Vector2Int outward)
        {
            if (outward.x > 0) return 0f;
            if (outward.x < 0) return 180f;
            return outward.y > 0 ? 270f : 90f;
        }

        /// <summary>
        /// Giro para PREFAB de interior (RoomDef.interiorPrefab): o tamanho local fica IGUAL ao RoomDef.Size
        /// (o prefab é feito naquele tamanho). Entre os dois giros possíveis, a porta principal vai para +X local;
        /// se ela cair num lado comprido/curto que não dá, vai para +Z local.
        /// </summary>
        public static float AuthoredYaw(HouseSpace space, Vector2Int outward)
        {
            Vector2Int def = space.Def != null ? space.Def.Size : new Vector2Int(space.Rect.width, space.Rect.height);
            if (def.x == def.y) return FurnitureYaw(outward); // quadrada: qualquer giro serve
            // Giros que mantêm o tamanho: 0/180 se a sala não foi girada, 90/270 se foi.
            bool sameAxes = new Vector2Int(space.Rect.width, space.Rect.height) == def;
            float yaw = FurnitureYaw(outward);
            bool yawKeepsAxes = Mathf.Approximately(yaw, 0f) || Mathf.Approximately(yaw, 180f);
            if (yawKeepsAxes == sameAxes) return yaw;
            // Porta perpendicular: +Z local aponta para a porta. +Z local com giro θ = (sen θ, cos θ).
            if (outward.y > 0) return 0f;
            if (outward.y < 0) return 180f;
            return outward.x > 0 ? 90f : 270f;
        }

        /// <summary>Largura (X local) × profundidade (Z local) do interior com o giro dado.</summary>
        public static Vector2 LocalSize(RectInt rect, float yaw)
        {
            int q = Mathf.RoundToInt(yaw / 90f) & 3;
            return q % 2 == 0 ? new Vector2(rect.width, rect.height) : new Vector2(rect.height, rect.width);
        }

        // ================================================================== Paredes

        /// <summary>Um vão num trecho de parede (ao longo do eixo da parede, metros da planta).</summary>
        public struct WallGap
        {
            public float From, To;
            public ConnectionType Type;
            /// <summary>Porta para o vazio (casa por escolha) deste vão; -1 = ligação normal.</summary>
            public int Site;
        }

        /// <summary>Vãos do trecho, em ordem: o da ligação (no máximo um) ou as portas para o vazio (casa por escolha).</summary>
        public static List<WallGap> Gaps(HouseLayout layout, HouseWall wall)
        {
            var list = new List<WallGap>();
            float a0 = wall.Horizontal ? wall.From.x : wall.From.y;
            float a1 = wall.Horizontal ? wall.To.x : wall.To.y;

            void Add(Vector2 pos, float width, ConnectionType type, int site)
            {
                float along = wall.Horizontal ? pos.x : pos.y;
                float g0 = Mathf.Max(a0, along - width * 0.5f), g1 = Mathf.Min(a1, along + width * 0.5f);
                if (g1 > g0) list.Add(new WallGap { From = g0, To = g1, Type = type, Site = site });
            }

            if (wall.ConnectionIndex >= 0 && wall.ConnectionIndex < layout.Connections.Count)
            {
                var c = layout.Connections[wall.ConnectionIndex];
                Add(c.Position, c.Width, c.Type, -1);
            }
            foreach (int si in wall.SiteIndices)
            {
                if (si < 0 || si >= layout.Sites.Count) continue;
                var site = layout.Sites[si];
                // Porta para o vazio: vão de porta, fechado por uma folha no HouseBuilder.
                Add(site.Position, site.Width, ConnectionType.Door, si);
            }
            list.Sort((x, y) => x.From.CompareTo(y.From));
            return list;
        }

        /// <summary>Primeiro vão do trecho (ao longo do eixo). False = trecho sólido.</summary>
        public static bool GapOf(HouseLayout layout, HouseWall wall, out float g0, out float g1, out ConnectionType type)
        {
            var gaps = Gaps(layout, wall);
            g0 = g1 = 0f;
            type = ConnectionType.Door;
            if (gaps.Count == 0) return false;
            g0 = gaps[0].From;
            g1 = gaps[0].To;
            type = gaps[0].Type;
            return true;
        }

        /// <summary>
        /// Pedaços de parede de um trecho: sólidos entre os vãos (divididos em pedaços de até 'maxPiece' m,
        /// para o corte "Sims" funcionar por pedaço) e, em cada PORTA, a verga acima do vão.
        /// PASSAGEM (Opening) = vão de altura inteira, sem verga.
        /// </summary>
        public static List<WallPiece> Pieces(HouseLayout layout, HouseWall wall, float wallHeight, float doorHeight, float maxPiece)
        {
            var list = new List<WallPiece>();
            float a0 = wall.Horizontal ? wall.From.x : wall.From.y;
            float a1 = wall.Horizontal ? wall.To.x : wall.To.y;

            float cursor = a0;
            foreach (var gap in Gaps(layout, wall))
            {
                AddSolid(list, cursor, gap.From, wallHeight, maxPiece);
                if (gap.Type == ConnectionType.Door && wallHeight - doorHeight > 0.01f)
                {
                    list.Add(new WallPiece { From = gap.From, To = gap.To, Bottom = doorHeight, Height = wallHeight - doorHeight, Lintel = true });
                }
                cursor = Mathf.Max(cursor, gap.To);
            }
            AddSolid(list, cursor, a1, wallHeight, maxPiece);
            return list;
        }

        private static void AddSolid(List<WallPiece> list, float from, float to, float height, float maxPiece)
        {
            float len = to - from;
            if (len <= 0.01f) return;
            int n = Mathf.Max(1, Mathf.CeilToInt(len / Mathf.Max(0.5f, maxPiece) - 0.001f));
            float step = len / n;
            for (int i = 0; i < n; i++)
            {
                float f = from + step * i;
                float t = i == n - 1 ? to : f + step;
                list.Add(new WallPiece { From = f, To = t, Bottom = 0f, Height = height });
            }
        }

        // ================================================================== Camadas de luz

        /// <summary>Distância entre dois retângulos (0 = encostam pela borda ou pelo canto).</summary>
        public static float Gap(RectInt a, RectInt b)
        {
            int dx = Mathf.Max(0, Mathf.Max(a.xMin - b.xMax, b.xMin - a.xMax));
            int dy = Mathf.Max(0, Mathf.Max(a.yMin - b.yMax, b.yMin - a.yMax));
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Grupo de luz (0..groups-1) de cada espaço. As lâmpadas não têm sombra, então cada espaço usa uma
        /// rendering layer e só ilumina o próprio piso/móveis. Com poucas camadas, espaços que se TOCAM
        /// recebem grupos diferentes sempre que possível; depois, evita repetir entre espaços a menos de 'range' m.
        /// Guloso, na ordem dos índices (determinístico).
        /// </summary>
        public static int[] LightGroups(HouseLayout layout, float range, int groups)
        {
            int n = layout.Spaces.Count;
            var result = new int[n];
            var penalty = new float[Mathf.Max(1, groups)];
            for (int i = 0; i < n; i++)
            {
                System.Array.Clear(penalty, 0, penalty.Length);
                RectInt ri = layout.Spaces[i].Rect;
                for (int j = 0; j < i; j++)
                {
                    float gap = Gap(ri, layout.Spaces[j].Rect);
                    if (gap <= 0f) penalty[result[j]] += 1000f;
                    else if (gap < range) penalty[result[j]] += range - gap;
                }
                int best = 0;
                for (int g = 1; g < penalty.Length; g++)
                {
                    if (penalty[g] < penalty[best]) best = g;
                }
                result[i] = best;
            }
            return result;
        }
    }
}
