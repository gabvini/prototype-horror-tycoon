using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace HorrorTycoon.Actors
{
    /// <summary>
    /// "Vida de set": o que o ator faz enquanto o jogador pensa. Só visual, não mexe nas regras.
    ///   - Com alguém perto: vira para a pessoa e "conversa" (modelo: clipe Talk pelo bool "Talking";
    ///     cápsula greybox: balança o "Corpo").
    ///   - Sozinho: às vezes anda um pouco pelo lugar, às vezes olha em volta.
    /// O RunPresenter define a "casa" do ator (centro + raio onde ele pode andar) e pausa este
    /// componente quando o ator está encenando (andando para outra sala, close de reação).
    /// Usa UnityEngine.Random de propósito: é visual e não pode mexer na seed da run.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class ActorIdle : MonoBehaviour
    {
        private static readonly List<ActorIdle> all = new List<ActorIdle>();

        [SerializeField] private float talkDistance = 2.6f;
        [SerializeField] private Vector2 thinkInterval = new Vector2(3f, 7f);
        [SerializeField] private float turnSpeed = 4f;
        [SerializeField] private float talkBob = 0.025f;

        private NavMeshAgent agent;
        private ActorView view;
        private Transform body;
        private Vector3 bodyBasePos;
        private Quaternion bodyBaseRot;
        private Animator animator;
        private static readonly int TalkingId = Animator.StringToHash("Talking");

        private Vector3 home;
        private float homeRadius = 0.6f;
        private bool paused = true;
        private float timer;
        private Quaternion? lookGoal;
        private ActorIdle talkingTo;
        private bool wandering;

        /// <summary>Com quem está conversando agora (null = ninguém). Usado pela câmera.</summary>
        public ActorIdle TalkingTo => talkingTo;

        public bool Paused
        {
            get => paused;
            set
            {
                paused = value;
                if (paused) StopIdle();
            }
        }

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            view = GetComponent<ActorView>();
            animator = view != null && view.Animator != null ? view.Animator : GetComponentInChildren<Animator>();
            Transform b = transform.Find("Corpo");
            if (b != null)
            {
                body = b;
                bodyBasePos = b.localPosition;
                bodyBaseRot = b.localRotation;
            }
            home = transform.position;
        }

        private void OnEnable() => all.Add(this);
        private void OnDisable() => all.Remove(this);

        /// <summary>Onde o ator "mora" agora (centro do cômodo) e quanto pode se afastar.</summary>
        public void SetHome(Vector3 center, float radius)
        {
            home = center;
            homeRadius = radius;
            timer = Random.Range(0.5f, 2f);
        }

        private bool IsDead => view != null && view.IsDead;

        private void Update()
        {
            if (paused || IsDead || agent == null || !agent.enabled || !agent.isOnNavMesh) return;

            // Terminou a caminhadinha?
            if (wandering && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.05f)
            {
                wandering = false;
                agent.ResetPath();
            }

            // Conversa: mantém virado para o colega e balança o corpo.
            if (talkingTo != null)
            {
                if (talkingTo.IsDead || talkingTo.paused || Flat(talkingTo.transform.position - transform.position).magnitude > talkDistance)
                {
                    talkingTo = null;
                    ResetBody();
                }
                else
                {
                    lookGoal = Quaternion.LookRotation(Flat(talkingTo.transform.position - transform.position));
                    SetTalking(true);
                    if (body != null && !HasAnimator)
                    {
                        float t = Time.time * 5f + GetInstanceID();
                        body.localPosition = bodyBasePos + Vector3.up * (Mathf.Abs(Mathf.Sin(t)) * talkBob);
                        body.localRotation = bodyBaseRot * Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.5f) * 3f);
                    }
                }
            }

            if (lookGoal.HasValue && !wandering)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, lookGoal.Value, 1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
            }

            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = Random.Range(thinkInterval.x, thinkInterval.y);
            Think();
        }

        private void Think()
        {
            // Alguém por perto? Conversa (às vezes).
            ActorIdle mate = NearestMate();
            if (mate != null && Random.value < 0.7f)
            {
                talkingTo = mate;
                return;
            }

            talkingTo = null;
            ResetBody();

            if (Random.value < 0.5f)
            {
                // Anda um pouco dentro da "casa".
                Vector2 r = Random.insideUnitCircle * homeRadius;
                Vector3 goal = home + new Vector3(r.x, 0f, r.y);
                if (NavMesh.SamplePosition(goal, out NavMeshHit hit, 1f, NavMesh.AllAreas))
                {
                    agent.isStopped = false;
                    agent.SetDestination(hit.position);
                    wandering = true;
                    lookGoal = null;
                }
            }
            else
            {
                // Olha em volta.
                lookGoal = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            }
        }

        private ActorIdle NearestMate()
        {
            ActorIdle best = null;
            float bestD = talkDistance;
            foreach (var other in all)
            {
                if (other == this || other.IsDead || other.paused) continue;
                float d = Flat(other.transform.position - transform.position).magnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = other;
                }
            }
            return best;
        }

        private void StopIdle()
        {
            talkingTo = null;
            lookGoal = null;
            ResetBody();
            if (wandering && agent != null && agent.enabled && agent.isOnNavMesh) agent.ResetPath();
            wandering = false;
        }

        private bool HasAnimator => animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null;

        private void SetTalking(bool on)
        {
            if (HasAnimator) animator.SetBool(TalkingId, on && !IsDead);
        }

        private void ResetBody()
        {
            SetTalking(false);
            if (body == null || IsDead) return;
            body.localPosition = bodyBasePos;
            body.localRotation = bodyBaseRot;
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude < 0.0001f ? Vector3.forward : v;
        }
    }
}
