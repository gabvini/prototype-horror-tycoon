using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace HorrorTycoon.Actors
{
    /// <summary>
    /// Faz o ator andar pela casa usando o NavMesh (a "malha de caminhada" calculada sobre o cenário).
    /// O NavMeshAgent desvia de paredes e passa pelas portas sozinho.
    /// O agente começa DESLIGADO porque o NavMesh só é construído quando a cena começa.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class ActorMover : MonoBehaviour
    {
        [SerializeField] private float arriveDistance = 0.25f;
        [SerializeField] private float timeoutSeconds = 10f;

        private NavMeshAgent agent;

        public bool IsMoving { get; private set; }

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            agent.enabled = false;
        }

        /// <summary>Chamar depois que o NavMesh existir.</summary>
        public void EnableAgent()
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                transform.position = hit.position;
            }
            agent.enabled = true;
        }

        /// <summary>Corrotina: anda até o destino e termina ao chegar (ou no timeout).</summary>
        public IEnumerator MoveTo(Vector3 destination)
        {
            if (!agent.enabled || !agent.isOnNavMesh)
            {
                transform.position = destination; // fallback: teleporta
                yield break;
            }

            // Calcula o caminho antes. Se não houver caminho completo, teleporta (o jogo nunca trava).
            var path = new NavMeshPath();
            if (!agent.CalculatePath(destination, path) || path.status != NavMeshPathStatus.PathComplete)
            {
                agent.Warp(destination);
                yield break;
            }

            IsMoving = true;
            agent.isStopped = false;
            agent.SetPath(path);

            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                elapsed += Time.deltaTime;
                yield return null; // espera o próximo frame

                // Só confia em remainingDistance depois que o caminho existe
                // (antes disso ele vale 0 e o ator "chegaria" sem sair do lugar).
                bool arrived = (transform.position - destination).sqrMagnitude <= arriveDistance * arriveDistance * 4f;
                if (!agent.pathPending && agent.hasPath && agent.remainingDistance <= arriveDistance) arrived = true;
                if (arrived) break;
            }

            if (elapsed >= timeoutSeconds) agent.Warp(destination); // garantia contra ficar preso

            agent.isStopped = true;
            agent.ResetPath();
            IsMoving = false;
        }

        /// <summary>Vira o ator para olhar um ponto (ex.: a câmera ou o centro da sala).</summary>
        public void FaceTowards(Vector3 point)
        {
            Vector3 dir = point - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(dir);
            }
        }

        public void Stop()
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
            }
        }
    }
}
