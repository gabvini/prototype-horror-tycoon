using System.Collections.Generic;
using UnityEngine;

namespace HorrorTycoon.Art
{
    /// <summary>
    /// Caixa CHANFRADA ("de massinha") gerada por código: 6 faces + 12 chanfros + 8 cantos,
    /// normais chapadas por grupo (o toon desenha uma faixa de luz em cada quina).
    /// Opcional: afunilamento (topo menor que a base, guia §5: 5–10% em armários e cadeiras).
    /// As malhas ficam em cache por tamanho, então dá para chamar à vontade.
    /// Centro da malha = centro da caixa (igual ao cubo da Unity, mas sem escala no Transform).
    /// </summary>
    public static class BevelMesh
    {
        private static readonly Dictionary<long, Mesh> cache = new Dictionary<long, Mesh>();

        public static Mesh Get(Vector3 size, float bevel, float taper = 0f)
        {
            size = new Vector3(Mathf.Max(size.x, 0.002f), Mathf.Max(size.y, 0.002f), Mathf.Max(size.z, 0.002f));
            float minHalf = Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.5f;
            bevel = Mathf.Clamp(bevel, 0f, minHalf * 0.45f);

            long key = Quant(size.x) * 73856093L ^ Quant(size.y) * 19349663L ^ Quant(size.z) * 83492791L
                       ^ Quant(bevel) * 2654435761L ^ Quant(taper) * 97L;
            Mesh mesh;
            if (cache.TryGetValue(key, out mesh) && mesh != null) return mesh;
            mesh = Build(size, bevel, taper);
            cache[key] = mesh;
            return mesh;
        }

        private static long Quant(float v) => (long)Mathf.Round(v * 1000f);

        /// <summary>Monta a malha (também usada pelo editor para salvar como asset).</summary>
        public static Mesh Build(Vector3 size, float bevel, float taper)
        {
            Vector3 h = size * 0.5f;
            var verts = new List<Vector3>(96);
            var norms = new List<Vector3>(96);
            var tris = new List<int>(132);

            // Faces: para cada eixo e sinal.
            for (int axis = 0; axis < 3; axis++)
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    int u = (axis + 1) % 3, v = (axis + 2) % 3;
                    var q = new Vector3[4];
                    int k = 0;
                    for (int a = -1; a <= 1; a += 2)
                    {
                        for (int b = -1; b <= 1; b += 2)
                        {
                            Vector3 p = Vector3.zero;
                            p[axis] = s * h[axis];
                            p[u] = a * (h[u] - bevel);
                            p[v] = b * (h[v] - bevel);
                            q[k++] = p;
                        }
                    }
                    // ordem do quad: (-,-) (-,+) (+,+) (+,-)
                    AddQuad(verts, norms, tris, q[0], q[1], q[3], q[2], taper, h.y);
                }
            }

            // Chanfros das arestas: aresta paralela ao eixo 'w', entre as faces 'a1' e 'a2'.
            for (int w = 0; w < 3; w++)
            {
                int a1 = (w + 1) % 3, a2 = (w + 2) % 3;
                for (int s1 = -1; s1 <= 1; s1 += 2)
                {
                    for (int s2 = -1; s2 <= 1; s2 += 2)
                    {
                        Vector3 p0 = Vector3.zero, p1 = Vector3.zero, p2 = Vector3.zero, p3 = Vector3.zero;
                        // Lado da face a1.
                        p0[a1] = s1 * h[a1]; p0[a2] = s2 * (h[a2] - bevel); p0[w] = -(h[w] - bevel);
                        p1[a1] = s1 * h[a1]; p1[a2] = s2 * (h[a2] - bevel); p1[w] = h[w] - bevel;
                        // Lado da face a2.
                        p2[a1] = s1 * (h[a1] - bevel); p2[a2] = s2 * h[a2]; p2[w] = h[w] - bevel;
                        p3[a1] = s1 * (h[a1] - bevel); p3[a2] = s2 * h[a2]; p3[w] = -(h[w] - bevel);
                        AddQuad(verts, norms, tris, p0, p1, p2, p3, taper, h.y);
                    }
                }
            }

            // Cantos.
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        var px = new Vector3(sx * h.x, sy * (h.y - bevel), sz * (h.z - bevel));
                        var py = new Vector3(sx * (h.x - bevel), sy * h.y, sz * (h.z - bevel));
                        var pz = new Vector3(sx * (h.x - bevel), sy * (h.y - bevel), sz * h.z);
                        AddTri(verts, norms, tris, px, py, pz, taper, h.y);
                    }
                }
            }

            var mesh = new Mesh { name = $"HT_Bevel_{size.x:0.##}x{size.y:0.##}x{size.z:0.##}" };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3 Taper(Vector3 p, float taper, float hy)
        {
            if (taper <= 0f) return p;
            float t = Mathf.InverseLerp(-hy, hy, p.y);
            float k = 1f - taper * t;
            return new Vector3(p.x * k, p.y, p.z * k);
        }

        private static void AddQuad(List<Vector3> v, List<Vector3> n, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d, float taper, float hy)
        {
            if ((a - c).sqrMagnitude < 1e-10f) return;
            AddTri(v, n, t, a, b, c, taper, hy);
            AddTri(v, n, t, a, c, d, taper, hy);
        }

        private static void AddTri(List<Vector3> v, List<Vector3> n, List<int> t, Vector3 a, Vector3 b, Vector3 c, float taper, float hy)
        {
            // Normal pelo centróide (a caixa é convexa e centrada na origem): vira para fora.
            Vector3 centroid = (a + b + c) / 3f;
            a = Taper(a, taper, hy);
            b = Taper(b, taper, hy);
            c = Taper(c, taper, hy);
            Vector3 normal = Vector3.Cross(b - a, c - a);
            if (normal.sqrMagnitude < 1e-14f) return;
            if (Vector3.Dot(normal, centroid) < 0f)
            {
                Vector3 tmp = b; b = c; c = tmp;
                normal = -normal;
            }
            normal.Normalize();
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c);
            n.Add(normal); n.Add(normal); n.Add(normal);
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
        }
    }
}
