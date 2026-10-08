using UnityEngine;
using UnityEngine.AI;

namespace HorrorTycoon.Actors
{
    /// <summary>
    /// Liga o NavMeshAgent ao Animator do modelo: "Speed" = velocidade real (m/s) e
    /// "MoveRate" = ritmo da passada (o clipe Walk anda ~1,4 m/s; o agente anda mais rápido,
    /// então a passada acelera em vez de o pé "patinar"). Só visual.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class ActorAnimDriver : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [Tooltip("Velocidade (m/s) em que o clipe Walk casa com o chão.")]
        [SerializeField] private float walkClipSpeed = 1.4f;
        [SerializeField] private Vector2 moveRateRange = new Vector2(0.6f, 2.0f);
        [SerializeField] private float speedDamp = 0.08f;

        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int MoveRateId = Animator.StringToHash("MoveRate");

        private NavMeshAgent agent;
        private ActorView view;

        public void Configure(Animator a) => animator = a;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            view = GetComponent<ActorView>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        private void Update()
        {
            if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null) return;

            float speed = 0f;
            if (agent != null && agent.enabled && (view == null || !view.IsDead))
            {
                Vector3 v = agent.velocity;
                v.y = 0f;
                speed = v.magnitude;
            }
            animator.SetFloat(SpeedId, speed, speedDamp, Time.deltaTime);
            animator.SetFloat(MoveRateId, Mathf.Clamp(speed / Mathf.Max(0.1f, walkClipSpeed), moveRateRange.x, moveRateRange.y));
        }
    }
}
