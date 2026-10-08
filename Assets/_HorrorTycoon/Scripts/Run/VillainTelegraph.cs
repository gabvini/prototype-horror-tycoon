using HorrorTycoon.Art;
using UnityEngine;

namespace HorrorTycoon.Run
{
    /// <summary>
    /// ANÚNCIO do vilão na cena (estilo Into the Breach): para onde ele vai na próxima batida.
    ///   - brilho vermelho pulsando no chão do próximo espaço;
    ///   - pegadas + seta vermelha na porta entre o espaço atual e o próximo;
    ///   - uma ponta de seta flutuando sobre o próximo espaço (lê de longe, por cima das paredes).
    /// Montado por código (primitivas sem colisor). O RunPresenter chama Show/Hide após cada ação.
    /// </summary>
    public class VillainTelegraph : MonoBehaviour
    {
        private static readonly Color Red = new Color(0.95f, 0.12f, 0.1f);
        /// <summary>Cor do pulso (vermelho = Slasher; roxo = Fantasma). Protótipo 3.</summary>
        private Color tint = Red;

        private Transform glow;
        private Transform doorMark;
        private Transform pointer;
        private Vector3 pointerBase;
        private Renderer[] glowRenderers;
        private Renderer[] doorRenderers;
        private Renderer[] pointerRenderers;
        private MaterialPropertyBlock block;

        public static VillainTelegraph Create()
        {
            var go = new GameObject("Vilao_Anuncio");
            var t = go.AddComponent<VillainTelegraph>();
            t.Build();
            t.Hide();
            return t;
        }

        private void Build()
        {
            var glowMat = ToonMaterials.NewEmissive(Red * 0.5f, Red * 2.5f);
            var stepMat = ToonMaterials.NewEmissive(new Color(0.35f, 0.02f, 0.02f), Red * 2.2f);

            glow = new GameObject("BrilhoProximoEspaco").transform;
            glow.SetParent(transform, false);
            // Anel (borda) + miolo fraco: lê como "área marcada" também vista de cima.
            Prim(PrimitiveType.Cylinder, glow, Vector3.zero, new Vector3(1f, 0.01f, 1f), glowMat);
            var ringMat = ToonMaterials.NewEmissive(Red * 0.2f, Red * 0.5f);
            Prim(PrimitiveType.Cylinder, glow, new Vector3(0f, 0.004f, 0f), new Vector3(0.8f, 0.01f, 0.8f), ringMat);
            glowRenderers = glow.GetComponentsInChildren<Renderer>(true);

            doorMark = new GameObject("PegadasNaPorta").transform;
            doorMark.SetParent(transform, false);
            // Pegadas alternadas atravessando a porta (eixo local Z = direção do movimento).
            for (int i = 0; i < 4; i++)
            {
                float side = i % 2 == 0 ? -0.14f : 0.14f;
                Prim(PrimitiveType.Cube, doorMark, new Vector3(side, 0.03f, -0.9f + i * 0.5f), new Vector3(0.16f, 0.02f, 0.32f), stepMat);
            }
            // Seta (haste + ponta feita de 2 barras em V).
            Prim(PrimitiveType.Cube, doorMark, new Vector3(0f, 0.05f, 1.15f), new Vector3(0.12f, 0.03f, 0.7f), stepMat);
            var a = Prim(PrimitiveType.Cube, doorMark, new Vector3(-0.17f, 0.05f, 1.42f), new Vector3(0.12f, 0.03f, 0.5f), stepMat);
            a.localRotation = Quaternion.Euler(0f, 45f, 0f);
            var b = Prim(PrimitiveType.Cube, doorMark, new Vector3(0.17f, 0.05f, 1.42f), new Vector3(0.12f, 0.03f, 0.5f), stepMat);
            b.localRotation = Quaternion.Euler(0f, -45f, 0f);
            doorRenderers = doorMark.GetComponentsInChildren<Renderer>(true);

            // Ponta de seta (pirâmide invertida) flutuando sobre o próximo espaço.
            pointer = new GameObject("SetaFlutuante").transform;
            pointer.SetParent(transform, false);
            var pm = pointer.gameObject.AddComponent<MeshFilter>();
            pm.sharedMesh = PointerMesh(0.32f, 0.6f);
            var pr = pointer.gameObject.AddComponent<MeshRenderer>();
            pr.sharedMaterial = stepMat;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pointerRenderers = new Renderer[] { pr };
        }

        /// <summary>Pirâmide de 4 lados com a ponta para BAIXO (ponta na origem).</summary>
        private static Mesh PointerMesh(float half, float height)
        {
            var tip = Vector3.zero;
            var b0 = new Vector3(-half, height, -half);
            var b1 = new Vector3(half, height, -half);
            var b2 = new Vector3(half, height, half);
            var b3 = new Vector3(-half, height, half);
            var one = new[] { tip, b1, b0, tip, b2, b1, tip, b3, b2, tip, b0, b3, b0, b1, b2, b0, b2, b3 };
            // Dupla face (as duas ordens de vértices): aparece de qualquer ângulo, seja qual for o culling do shader.
            var v = new Vector3[one.Length * 2];
            var tris = new int[v.Length];
            for (int i = 0; i < one.Length; i++)
            {
                v[i] = one[i];
                v[one.Length + i] = one[i];
                tris[i] = i;
            }
            for (int t = 0; t < one.Length; t += 3)
            {
                tris[one.Length + t] = one.Length + t;
                tris[one.Length + t + 1] = one.Length + t + 2;
                tris[one.Length + t + 2] = one.Length + t + 1;
            }
            var mesh = new Mesh { name = "VilaoSeta" };
            mesh.vertices = v;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Transform Prim(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        /// <summary>Mostra o anúncio: brilho no centro do próximo espaço e pegadas na porta, apontando de 'from' para 'to'.</summary>
        /// <summary>Protótipo 3: muda a cor do pulso (emissão). O material base continua; só o brilho muda.</summary>
        public void SetTint(Color c) => tint = c;

        /// <summary>Protótipo 3 (Fantasma): só a área e a seta flutuante sobre a sala, sem pegadas na porta.</summary>
        public void ShowArea(Vector3 center, float size)
        {
            Show(center, size, center, Vector3.forward);
            doorMark.gameObject.SetActive(false);
        }

        public void Show(Vector3 nextCenter, float nextSize, Vector3 door, Vector3 direction)
        {
            gameObject.SetActive(true);
            doorMark.gameObject.SetActive(true);
            glow.position = new Vector3(nextCenter.x, nextCenter.y + 0.02f, nextCenter.z);
            float s = Mathf.Clamp(nextSize * 0.8f, 1.4f, 3.6f);
            glow.localScale = new Vector3(s, 1f, s);
            pointerBase = new Vector3(nextCenter.x, nextCenter.y + 2.6f, nextCenter.z);
            pointer.position = pointerBase;

            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) direction = Vector3.forward;
            doorMark.position = new Vector3(door.x, door.y, door.z);
            doorMark.rotation = Quaternion.LookRotation(direction.normalized);
        }

        public void Hide() => gameObject.SetActive(false);

        private void Update()
        {
            // Pulsa (emissão via MaterialPropertyBlock, sem duplicar material).
            block ??= new MaterialPropertyBlock();
            float k = 0.6f + 0.4f * Mathf.Sin(Time.time * 5f);
            Pulse(glowRenderers, tint * (2.5f * k));
            Pulse(doorRenderers, tint * (2.2f * (0.7f + 0.3f * k)));
            Pulse(pointerRenderers, tint * (2.6f * (0.7f + 0.3f * k)));
            if (pointer != null)
            {
                pointer.position = pointerBase + Vector3.up * (Mathf.Sin(Time.time * 3f) * 0.15f);
                pointer.rotation = Quaternion.Euler(0f, Time.time * 90f, 0f);
            }
        }

        private void Pulse(Renderer[] list, Color emission)
        {
            if (list == null) return;
            foreach (var r in list)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor(ToonMaterials.EmissionColorId, emission);
                r.SetPropertyBlock(block);
            }
        }
    }
}
