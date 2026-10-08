using System.Collections;
using HorrorTycoon.Art;
using HorrorTycoon.Cameras;
using HorrorTycoon.Run;
using UnityEngine;
using UnityEngine.AI;

namespace HorrorTycoon.Actors
{
    /// <summary>
    /// Boneco PROVISÓRIO do vilão NPC (Protótipo 2): montado por código com primitivas, sem asset.
    /// Mascarado = corpo alto e escuro com máscara branca; Fantasma = lençol claro com olhos pretos.
    /// Anda de espaço em espaço pelo NavMesh (ou desliza, se não houver malha).
    /// Tem SeeThroughSubject com isVillain = true: nunca abre buraco na parede (escondido = tensão).
    /// Trocar pela arte oficial: substituir Build() por um prefab; a lógica não depende disto.
    /// </summary>
    public class VillainView : MonoBehaviour
    {
        private NavMeshAgent agent;
        private Transform body;
        private Renderer[] renderers;
        private bool ghost;
        private float bobSeed;

        public bool IsMoving { get; private set; }

        /// <summary>Cria o vilão na cena (fora da hierarquia da casa).</summary>
        public static VillainView Create(VillainDef def)
        {
            var root = new GameObject($"Vilao_{(def != null ? def.DisplayName : "NPC")}");
            var view = root.AddComponent<VillainView>();
            view.Build(def);
            return view;
        }

        private void Build(VillainDef def)
        {
            // Protótipo 3: família Fantasma também usa o lençol (mesmo com a aparência em Auto).
            ghost = def != null && (def.Look == VillainLook.Ghost || def.IsGhost);
            Color tint = def != null ? def.Color : Color.red;
            bobSeed = Random.value * 10f;

            agent = gameObject.AddComponent<NavMeshAgent>();
            agent.speed = 4.5f;
            agent.angularSpeed = 720f;
            agent.acceleration = 24f;
            agent.radius = 0.3f;
            agent.height = 2.2f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
            agent.autoBraking = true;
            agent.enabled = false;

            var subject = gameObject.AddComponent<SeeThroughSubject>();
            subject.isVillain = true;

            body = new GameObject("Corpo").transform;
            body.SetParent(transform, false);

            if (ghost)
            {
                Color sheet = new Color(0.86f, 0.86f, 0.94f);
                Part(PrimitiveType.Capsule, new Vector3(0f, 1.15f, 0f), new Vector3(0.95f, 1.1f, 0.95f), ToonMaterials.NewCharacter(sheet));
                // Barra do lençol (mais larga embaixo).
                Part(PrimitiveType.Cylinder, new Vector3(0f, 0.25f, 0f), new Vector3(1.05f, 0.22f, 1.05f), ToonMaterials.NewCharacter(sheet));
                var eye = ToonMaterials.NewEmissive(Color.black, Color.black);
                Part(PrimitiveType.Sphere, new Vector3(-0.15f, 1.85f, 0.4f), new Vector3(0.16f, 0.24f, 0.08f), eye);
                Part(PrimitiveType.Sphere, new Vector3(0.15f, 1.85f, 0.4f), new Vector3(0.16f, 0.24f, 0.08f), eye);
                Part(PrimitiveType.Sphere, new Vector3(0f, 1.55f, 0.43f), new Vector3(0.14f, 0.2f, 0.06f), eye);
                // Protótipo 3: brilho fantasmagórico (luz fria em volta do lençol).
                var glowGo = new GameObject("BrilhoFantasma");
                glowGo.transform.SetParent(body, false);
                glowGo.transform.localPosition = new Vector3(0f, 1.3f, 0f);
                var glow = glowGo.AddComponent<Light>();
                glow.type = LightType.Point;
                glow.color = new Color(0.7f, 0.65f, 1f);
                glow.range = 3.2f;
                glow.intensity = 1.6f;
                glow.shadows = LightShadows.None;
            }
            else
            {
                Color dark = Color.Lerp(new Color(0.07f, 0.07f, 0.09f), tint, 0.18f);
                Part(PrimitiveType.Capsule, new Vector3(0f, 1.1f, 0f), new Vector3(0.75f, 1.1f, 0.6f), ToonMaterials.NewCharacter(dark));
                Part(PrimitiveType.Sphere, new Vector3(0f, 2.05f, 0f), new Vector3(0.52f, 0.56f, 0.52f), ToonMaterials.NewCharacter(dark));
                // Máscara branca + olhos vazados.
                Part(PrimitiveType.Sphere, new Vector3(0f, 2.05f, 0.2f), new Vector3(0.4f, 0.48f, 0.2f), ToonMaterials.NewCharacter(new Color(0.93f, 0.92f, 0.86f)));
                var hole = ToonMaterials.NewEmissive(Color.black, Color.black);
                Part(PrimitiveType.Sphere, new Vector3(-0.09f, 2.1f, 0.3f), new Vector3(0.09f, 0.06f, 0.04f), hole);
                Part(PrimitiveType.Sphere, new Vector3(0.09f, 2.1f, 0.3f), new Vector3(0.09f, 0.06f, 0.04f), hole);
                // Faixa na cor do vilão (lê de longe).
                Part(PrimitiveType.Cylinder, new Vector3(0f, 1.6f, 0f), new Vector3(0.8f, 0.06f, 0.66f), ToonMaterials.NewCharacter(tint));
            }

            // Aura no chão na cor do vilão (não gira com o corpo).
            var aura = Part(PrimitiveType.Cylinder, new Vector3(0f, 0.03f, 0f), new Vector3(1.3f, 0.01f, 1.3f),
                ToonMaterials.NewEmissive(tint * 0.4f, tint * 1.6f));
            aura.SetParent(transform, true);

            renderers = GetComponentsInChildren<Renderer>(true);
        }

        private Transform Part(PrimitiveType type, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col); // não atrapalha cliques nem o NavMesh
            go.transform.SetParent(body, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return go.transform;
        }

        public void SetVisible(bool visible)
        {
            if (renderers == null) return;
            foreach (var r in renderers) if (r != null) r.enabled = visible;
        }

        /// <summary>Coloca o vilão no ponto (entrada / teleporte). Liga o agente se houver NavMesh.</summary>
        public void Warp(Vector3 point)
        {
            if (NavMesh.SamplePosition(point, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                point = hit.position;
                if (!agent.enabled)
                {
                    transform.position = point;
                    agent.enabled = true;
                }
                if (agent.isOnNavMesh) agent.Warp(point);
                else transform.position = point;
            }
            else
            {
                if (agent.enabled) agent.enabled = false;
                transform.position = point;
            }
        }

        /// <summary>Corrotina: anda até o ponto (NavMesh) ou desliza; nunca passa de maxSeconds.</summary>
        public IEnumerator MoveTo(Vector3 destination, float maxSeconds)
        {
            IsMoving = true;
            float elapsed = 0f;
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.SetDestination(destination);
                while (elapsed < maxSeconds)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                    if (!agent.pathPending && agent.remainingDistance <= 0.3f) break;
                }
                if (elapsed >= maxSeconds) agent.Warp(destination);
                agent.isStopped = true;
                agent.ResetPath();
            }
            else
            {
                Vector3 from = transform.position;
                float duration = Mathf.Min(0.6f, maxSeconds);
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    transform.position = Vector3.Lerp(from, destination, Mathf.SmoothStep(0f, 1f, elapsed / duration));
                    yield return null;
                }
                transform.position = destination;
            }
            IsMoving = false;
        }

        public void FaceTowards(Vector3 point)
        {
            Vector3 dir = point - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(dir);
        }

        private void Update()
        {
            if (body == null) return;
            // Fantasma flutua; mascarado "respira" e balança ao andar.
            float t = Time.time + bobSeed;
            float y = ghost ? 0.18f + Mathf.Sin(t * 2.2f) * 0.08f : Mathf.Abs(Mathf.Sin(t * (IsMoving ? 9f : 1.3f))) * (IsMoving ? 0.06f : 0.015f);
            body.localPosition = new Vector3(0f, y, 0f);
            body.localRotation = Quaternion.Euler(IsMoving ? 6f : 0f, 0f, ghost ? Mathf.Sin(t * 1.7f) * 4f : 0f);
        }
    }
}
