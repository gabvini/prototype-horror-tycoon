using System.Collections.Generic;
using UnityEngine;

namespace HorrorTycoon.Rooms.Generation
{
    /// <summary>Como dois espaços se ligam.</summary>
    public enum ConnectionType
    {
        /// <summary>Porta de verdade (batente, folha). Custo = HouseGenDef.doorCost.</summary>
        Door,
        /// <summary>Vão aberto, sem porta (corredor↔corredor, corredor↔convivência). Custo = HouseGenDef.openingCost.</summary>
        Opening
    }

    /// <summary>
    /// Um espaço da casa gerada (sala, convivência ou segmento de corredor).
    /// Coordenadas em METROS na grade da casa: x = leste, y = norte (no mundo 3D, y vira Z).
    /// O índice é o MESMO índice de FilmRun.Rooms.
    /// </summary>
    public sealed class HouseSpace
    {
        public int Index { get; internal set; }
        public SpaceKind Kind { get; internal set; }
        public RoomDef Def { get; internal set; }
        /// <summary>Retângulo ocupado (metros). xMin/yMin = canto sudoeste.</summary>
        public RectInt Rect { get; internal set; }
        /// <summary>True se o tamanho foi girado 90° em relação a Def.Size.</summary>
        public bool Rotated { get; internal set; }
        /// <summary>Espaço ao qual ele foi preso na geração (-1 = o inicial).</summary>
        public int ParentIndex { get; internal set; } = -1;
        /// <summary>Sala que abre para dentro de outra sala (só se chega passando pela sala-mãe).</summary>
        public bool IsDeep { get; internal set; }

        public Vector2 Center => Rect.center;
    }

    /// <summary>
    /// Ligação entre dois espaços (ou entre o inicial e Outside = porta da frente).
    /// A porta fica SOBRE a linha da parede em comum, centrada em Position, com largura Width.
    /// </summary>
    public sealed class HouseConnection
    {
        public int Index { get; internal set; }
        public int A { get; internal set; }
        /// <summary>Outro lado. HouseLayout.Outside (-1) = lado de fora (porta da frente).</summary>
        public int B { get; internal set; }
        public ConnectionType Type { get; internal set; }
        /// <summary>Centro do vão, em metros, exatamente sobre a linha da parede.</summary>
        public Vector2 Position { get; internal set; }
        /// <summary>True = a parede corre ao longo de X (a porta "olha" para ±Y). False = parede ao longo de Y.</summary>
        public bool HorizontalWall { get; internal set; }
        /// <summary>Largura do vão em metros.</summary>
        public float Width { get; internal set; }
        /// <summary>Custo em portas (já com o custo de porta/passagem do HouseGenDef).</summary>
        public int Cost { get; internal set; }

        public int Other(int space) => space == A ? B : A;
    }

    /// <summary>
    /// Trecho de parede reto. Cada borda de cada espaço aparece em EXATAMENTE um trecho:
    /// parede compartilhada entre A e B (listada uma vez, A &lt; B) ou parede externa (B = Outside).
    /// Se ConnectionIndex >= 0, o trecho tem o vão daquela ligação (o resto do trecho é parede sólida).
    /// </summary>
    public sealed class HouseWall
    {
        public int A { get; internal set; }
        public int B { get; internal set; }
        public Vector2Int From { get; internal set; }
        public Vector2Int To { get; internal set; }
        /// <summary>True = corre ao longo de X (From.y == To.y).</summary>
        public bool Horizontal { get; internal set; }
        public int ConnectionIndex { get; internal set; } = -1;
        internal readonly List<int> siteIndices = new List<int>();
        /// <summary>Casa por escolha: portas para o vazio (HouseDoorSite ainda abertas) neste trecho externo, em ordem ao longo dele.</summary>
        public IReadOnlyList<int> SiteIndices => siteIndices;
        /// <summary>Primeira porta para o vazio do trecho (-1 = nenhuma).</summary>
        public int SiteIndex => siteIndices.Count > 0 ? siteIndices[0] : -1;

        public bool IsExterior => B == HouseLayout.Outside;
        public int Length => Horizontal ? To.x - From.x : To.y - From.y;
    }

    /// <summary>Estado de uma porta para o vazio (casa por escolha).</summary>
    public enum DoorSiteState
    {
        /// <summary>Porta fechada: do outro lado ainda não há nada. Abrir = escolher 1 de 3 salas.</summary>
        Open,
        /// <summary>Já leva a uma sala montada (Space).</summary>
        Built,
        /// <summary>Uma sala montada ocupou o lado de fora sem encaixar na porta: virou parede.</summary>
        Blocked
    }

    /// <summary>
    /// CASA POR ESCOLHA: porta numa parede EXTERNA de um corredor/convivência que dá para o vazio.
    /// Ao abrir, o jogador escolhe a sala que nasce ali (HouseDraft). Coordenadas como HouseConnection.
    /// </summary>
    public sealed class HouseDoorSite
    {
        public int Index { get; internal set; }
        /// <summary>Espaço que tem a porta (corredor ou convivência).</summary>
        public int Host { get; internal set; }
        /// <summary>Centro do vão, sobre a linha da parede do host.</summary>
        public Vector2 Position { get; internal set; }
        public bool HorizontalWall { get; internal set; }
        public float Width { get; internal set; }
        /// <summary>Direção (grade) que sai do host pela porta: (±1,0) ou (0,±1).</summary>
        public Vector2Int Outward { get; internal set; }
        /// <summary>Zona do terreno (frente/meio/fundos) onde a porta fica: filtra as salas oferecidas.</summary>
        public HouseZone Zone { get; internal set; }
        public DoorSiteState State { get; internal set; } = DoorSiteState.Open;
        /// <summary>Sala montada do outro lado (-1 = nenhuma).</summary>
        public int Space { get; internal set; } = -1;

        public bool IsOpen => State == DoorSiteState.Open;
    }

    /// <summary>
    /// Resultado da geração: a planta da casa em C# puro (sem cena). Contrato para a montagem 3D e para a lógica.
    /// </summary>
    public sealed class HouseLayout
    {
        public const int Outside = -1;

        internal readonly List<HouseSpace> spaces = new List<HouseSpace>();
        internal readonly List<HouseConnection> connections = new List<HouseConnection>();
        internal readonly List<HouseWall> walls = new List<HouseWall>();
        internal readonly List<HouseDoorSite> sites = new List<HouseDoorSite>();

        public IReadOnlyList<HouseSpace> Spaces => spaces;
        /// <summary>Casa por escolha: portas para o vazio (vazio na casa gerada inteira).</summary>
        public IReadOnlyList<HouseDoorSite> Sites => sites;
        /// <summary>True = casa por escolha (cresce a cada sala escolhida).</summary>
        public bool GrowsByDraft { get; internal set; }
        /// <summary>Set em grid: lado da célula em metros (1 = sem grid).</summary>
        public int GridCell { get; internal set; } = 1;
        public IReadOnlyList<HouseConnection> Connections => connections;
        public IReadOnlyList<HouseWall> Walls => walls;

        /// <summary>Retângulo máximo (metros), começando em (0,0). A porta da frente está na borda sul.</summary>
        public Vector2Int Bounds { get; internal set; }
        /// <summary>Convivência inicial: atores começam aqui.</summary>
        public int StartSpaceIndex { get; internal set; }
        /// <summary>Índice (em Connections) da porta da frente (StartSpace ↔ Outside).</summary>
        public int FrontDoorIndex { get; internal set; } = -1;
        public HouseConnection FrontDoor => FrontDoorIndex >= 0 ? connections[FrontDoorIndex] : null;

        /// <summary>Seed usada nesta tentativa (sub-seed) e quantas tentativas foram feitas.</summary>
        public int Seed { get; internal set; }
        public int Attempts { get; internal set; }
        /// <summary>True se nenhuma tentativa passou na validação completa e a melhor casa foi aceita.</summary>
        public bool UsedFallback { get; internal set; }

        public int CountOf(SpaceKind kind)
        {
            int n = 0;
            foreach (var s in spaces) if (s.Kind == kind) n++;
            return n;
        }

        public IEnumerable<HouseConnection> ConnectionsOf(int space)
        {
            foreach (var c in connections) if (c.A == space || c.B == space) yield return c;
        }

        public IEnumerable<HouseWall> WallsOf(int space)
        {
            foreach (var w in walls) if (w.A == space || w.B == space) yield return w;
        }

        /// <summary>
        /// Parede em comum entre dois retângulos que se tocam pela borda (não só pelo canto).
        /// Retorna o trecho (From → To, crescente em X ou Y). Útil também para a montagem 3D.
        /// </summary>
        public static bool TryGetSharedWall(RectInt a, RectInt b, out Vector2Int from, out Vector2Int to)
        {
            from = to = Vector2Int.zero;
            if (a.xMax == b.xMin || b.xMax == a.xMin)
            {
                int x = a.xMax == b.xMin ? a.xMax : a.xMin;
                int y0 = Mathf.Max(a.yMin, b.yMin), y1 = Mathf.Min(a.yMax, b.yMax);
                if (y1 <= y0) return false;
                from = new Vector2Int(x, y0);
                to = new Vector2Int(x, y1);
                return true;
            }
            if (a.yMax == b.yMin || b.yMax == a.yMin)
            {
                int y = a.yMax == b.yMin ? a.yMax : a.yMin;
                int x0 = Mathf.Max(a.xMin, b.xMin), x1 = Mathf.Min(a.xMax, b.xMax);
                if (x1 <= x0) return false;
                from = new Vector2Int(x0, y);
                to = new Vector2Int(x1, y);
                return true;
            }
            return false;
        }

        // ================================================================== Paredes

        /// <summary>Índice do espaço em cada célula de 1 m (-1 = vazio).</summary>
        internal int[,] Grid()
        {
            int w = Mathf.Max(1, Bounds.x), h = Mathf.Max(1, Bounds.y);
            var grid = new int[w, h];
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    grid[x, y] = -1;
            foreach (var s in spaces)
            {
                RectInt r = s.Rect;
                for (int x = Mathf.Max(0, r.xMin); x < Mathf.Min(w, r.xMax); x++)
                    for (int y = Mathf.Max(0, r.yMin); y < Mathf.Min(h, r.yMax); y++)
                        grid[x, y] = s.Index;
            }
            return grid;
        }

        /// <summary>Retângulo dentro do terreno e sem nenhum espaço.</summary>
        public bool IsFree(RectInt rect)
        {
            if (rect.xMin < 0 || rect.yMin < 0 || rect.xMax > Bounds.x || rect.yMax > Bounds.y) return false;
            foreach (var s in spaces) if (s.Rect.Overlaps(rect)) return false;
            return true;
        }

        /// <summary>
        /// Recalcula as paredes: percorre as bordas de cada espaço metro a metro e junta trechos com o mesmo vizinho.
        /// Parede compartilhada sai uma vez (A &lt; B); parede externa sai com B = Outside.
        /// Casa por escolha: trechos externos com porta para o vazio ganham SiteIndices.
        /// </summary>
        internal void ComputeWalls()
        {
            walls.Clear();
            var grid = Grid();
            foreach (var s in spaces)
            {
                RectInt r = s.Rect;
                EdgeWalls(grid, s.Index, true, r.yMin, r.xMin, r.xMax, 0, -1);  // sul
                EdgeWalls(grid, s.Index, true, r.yMax, r.xMin, r.xMax, 0, 0);   // norte
                EdgeWalls(grid, s.Index, false, r.xMin, r.yMin, r.yMax, -1, 0); // oeste
                EdgeWalls(grid, s.Index, false, r.xMax, r.yMin, r.yMax, 0, 0);  // leste
            }
        }

        private void EdgeWalls(int[,] grid, int space, bool horizontal, int line, int from, int to, int offX, int offY)
        {
            int runStart = from;
            int runNeighbor = NeighborAt(grid, horizontal, line, from, offX, offY);
            for (int t = from + 1; t <= to; t++)
            {
                int nb = t < to ? NeighborAt(grid, horizontal, line, t, offX, offY) : int.MinValue;
                if (nb == runNeighbor) continue;
                EmitWall(space, runNeighbor, horizontal, line, runStart, t);
                runStart = t;
                runNeighbor = nb;
            }
        }

        private int NeighborAt(int[,] grid, bool horizontal, int line, int t, int offX, int offY)
        {
            int x = horizontal ? t : line + offX;
            int y = horizontal ? line + offY : t;
            if (x < 0 || y < 0 || x >= grid.GetLength(0) || y >= grid.GetLength(1)) return Outside;
            return grid[x, y];
        }

        private void EmitWall(int space, int neighbor, bool horizontal, int line, int a, int b)
        {
            if (neighbor != Outside && neighbor < space) return; // já emitida pelo outro lado

            var wall = new HouseWall
            {
                A = space,
                B = neighbor,
                Horizontal = horizontal,
                From = horizontal ? new Vector2Int(a, line) : new Vector2Int(line, a),
                To = horizontal ? new Vector2Int(b, line) : new Vector2Int(line, b),
            };

            foreach (var c in connections)
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

            if (neighbor == Outside && wall.ConnectionIndex < 0)
            {
                foreach (var site in sites)
                {
                    if (!site.IsOpen || site.Host != space || site.HorizontalWall != horizontal) continue;
                    float sLine = horizontal ? site.Position.y : site.Position.x;
                    float sAlong = horizontal ? site.Position.x : site.Position.y;
                    if (Mathf.Abs(sLine - line) < 0.001f && sAlong - site.Width * 0.5f >= a - 0.001f && sAlong + site.Width * 0.5f <= b + 0.001f)
                    {
                        wall.siteIndices.Add(site.Index);
                    }
                }
                wall.siteIndices.Sort((x, y) =>
                {
                    var sx = sites[x].Position;
                    var sy = sites[y].Position;
                    return horizontal ? sx.x.CompareTo(sy.x) : sx.y.CompareTo(sy.y);
                });
            }
            walls.Add(wall);
        }

        /// <summary>Confere as regras estruturais. Usado pelo gerador e pelos testes.</summary>
        public bool Validate(out string error)
        {
            var bounds = new RectInt(0, 0, Bounds.x, Bounds.y);
            for (int i = 0; i < spaces.Count; i++)
            {
                var r = spaces[i].Rect;
                if (spaces[i].Index != i) { error = $"índice errado em {i}"; return false; }
                if (r.width <= 0 || r.height <= 0) { error = $"espaço {i} vazio"; return false; }
                if (r.xMin < bounds.xMin || r.yMin < bounds.yMin || r.xMax > bounds.xMax || r.yMax > bounds.yMax)
                {
                    error = $"espaço {i} fora dos limites"; return false;
                }
                for (int j = i + 1; j < spaces.Count; j++)
                {
                    if (r.Overlaps(spaces[j].Rect)) { error = $"espaços {i} e {j} se sobrepõem"; return false; }
                }
            }

            if (StartSpaceIndex < 0 || StartSpaceIndex >= spaces.Count) { error = "sem espaço inicial"; return false; }
            if (FrontDoor == null || FrontDoor.A != StartSpaceIndex || FrontDoor.B != Outside)
            {
                error = "porta da frente inválida"; return false;
            }

            // Tudo conectado a partir do inicial.
            var seen = new bool[spaces.Count];
            var stack = new Stack<int>();
            stack.Push(StartSpaceIndex);
            seen[StartSpaceIndex] = true;
            int count = 1;
            while (stack.Count > 0)
            {
                int n = stack.Pop();
                foreach (var c in ConnectionsOf(n))
                {
                    int m = c.Other(n);
                    if (m < 0 || seen[m]) continue;
                    seen[m] = true;
                    count++;
                    stack.Push(m);
                }
            }
            if (count != spaces.Count) { error = "há espaços desconectados"; return false; }

            // Cada vão fica sobre a parede em comum, inteiro dentro dela.
            foreach (var c in connections)
            {
                RectInt a = spaces[c.A].Rect;
                Vector2Int from, to;
                if (c.B == Outside)
                {
                    // Porta da frente: na parede sul do espaço inicial (borda sul do terreno).
                    from = new Vector2Int(a.xMin, a.yMin);
                    to = new Vector2Int(a.xMax, a.yMin);
                }
                else if (!TryGetSharedWall(a, spaces[c.B].Rect, out from, out to))
                {
                    error = $"ligação {c.Index} entre espaços que não se tocam"; return false;
                }

                bool horizontal = from.y == to.y;
                float along = horizontal ? c.Position.x : c.Position.y;
                float line = horizontal ? c.Position.y : c.Position.x;
                float lo = horizontal ? from.x : from.y;
                float hi = horizontal ? to.x : to.y;
                if (horizontal != c.HorizontalWall || Mathf.Abs(line - (horizontal ? from.y : from.x)) > 0.001f
                    || along - c.Width * 0.5f < lo - 0.001f || along + c.Width * 0.5f > hi + 0.001f)
                {
                    error = $"vão da ligação {c.Index} fora da parede em comum"; return false;
                }
            }

            error = null;
            return true;
        }
    }
}
