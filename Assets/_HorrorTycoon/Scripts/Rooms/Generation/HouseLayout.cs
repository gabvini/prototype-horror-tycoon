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

        public bool IsExterior => B == HouseLayout.Outside;
        public int Length => Horizontal ? To.x - From.x : To.y - From.y;
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

        public IReadOnlyList<HouseSpace> Spaces => spaces;
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
