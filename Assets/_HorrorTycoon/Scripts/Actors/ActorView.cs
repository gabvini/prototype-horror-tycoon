using HorrorTycoon.Art;
using UnityEngine;

namespace HorrorTycoon.Actors
{
    /// <summary>Expressões do rosto (peças soltas: olhos, pupilas, sobrancelhas, 4 bocas).</summary>
    public enum ActorExpression
    {
        Neutral,
        Happy,
        Tense,
        Scared
    }

    /// <summary>
    /// Parte VISUAL de um ator (o "corpo" na cena).
    /// Não guarda regras de jogo: só mostra nome, posição, expressão e destaque.
    /// Os dados ficam no ActorDef (asset) e o estado da run no ActorRunState (C# puro).
    ///
    /// Dois "rigs" de rosto:
    ///   - MODELO do Blender (Art/README_Personagens.md): as peças guardam a pose base e as expressões
    ///     aplicam MULTIPLICADORES (tabela do README §3); as 4 bocas são ligadas uma por vez;
    ///     susto e morte disparam os clipes do Animator (Scared / Dead).
    ///   - CÁPSULA greybox (fallback, sem as bocas): o comportamento antigo, com valores absolutos.
    /// </summary>
    public class ActorView : MonoBehaviour
    {
        [Header("Dados")]
        [Tooltip("Qual ator (asset) este boneco representa.")]
        [SerializeField] private ActorDef def;

        [Header("Identidade (fallback se não houver ActorDef)")]
        [SerializeField] private string displayName = "Ator";

        [Header("Rosto")]
        [Tooltip("Centro do rosto (\"Rosto\" no modelo). A câmera de close enquadra este ponto.")]
        [SerializeField] private Transform faceAnchor;
        [SerializeField] private Transform leftEye;
        [SerializeField] private Transform rightEye;
        [SerializeField] private Transform leftBrow;
        [SerializeField] private Transform rightBrow;
        [Tooltip("Boca única da cápsula greybox (o modelo usa 'Bocas').")]
        [SerializeField] private Transform mouth;
        [Tooltip("Pupilas. Encolhem no susto (guia §4.2).")]
        [SerializeField] private Transform leftPupil;
        [SerializeField] private Transform rightPupil;
        [Tooltip("Modelo: Boca_Neutra, Boca_Feliz, Boca_Tensa, Boca_Grito (nesta ordem). Só uma fica ligada.")]
        [SerializeField] private GameObject[] mouths = new GameObject[0];

        [Header("Corpo e destaque")]
        [Tooltip("Cápsula: o corpo que deita na morte. Modelo: a raiz do modelo (estica no susto).")]
        [SerializeField] private Transform body;
        [Tooltip("Objeto que aparece acima da cabeça quando a carta deste ator está em destaque.")]
        [SerializeField] private GameObject highlightMarker;
        [Tooltip("Renderer do corpo da cápsula (legado).")]
        [SerializeField] private Renderer bodyRenderer;
        [Tooltip("Renderers que ganham o contorno amarelo de 'selecionado' (corpo do modelo; não as peças do rosto).")]
        [SerializeField] private Renderer[] outlineRenderers = new Renderer[0];
        [Tooltip("Animator do modelo (opcional). Sem ele, a cápsula deita na morte.")]
        [SerializeField] private Animator animator;

        [Header("Estado visual")]
        [SerializeField] private ActorExpression expression = ActorExpression.Neutral;
        [Tooltip("Velocidade com que o corpo estica/volta no susto.")]
        [SerializeField] private float squashSpeed = 14f;

        // ---- Pose base do rosto do modelo (capturada no build ou no primeiro uso; salva na cena).
        [System.Serializable]
        private class PartPose
        {
            public Transform t;
            public Vector3 pos;
            public Vector3 scale;
            public Quaternion rot;
        }

        [SerializeField, HideInInspector] private bool faceCaptured;
        [SerializeField, HideInInspector] private PartPose[] poses = new PartPose[0]; // olhoE, olhoD, pupE, pupD, sobE, sobD
        [SerializeField, HideInInspector] private int eyeDepthAxis = 2;
        [SerializeField, HideInInspector] private int eyeHeightAxis = 1;
        [SerializeField, HideInInspector] private Vector3 faceRightLocal = Vector3.right; // no espaço do "Rosto"
        [SerializeField, HideInInspector] private Vector3 faceUpLocal = Vector3.up;
        [SerializeField, HideInInspector] private Vector3 browAxisL = Vector3.forward;   // frente do rosto no espaço da sobrancelha
        [SerializeField, HideInInspector] private Vector3 browAxisR = Vector3.forward;
        [SerializeField, HideInInspector] private float browSideL = -1f;                 // lado (−1 = esquerda da tela do ator)
        [SerializeField, HideInInspector] private float browSideR = 1f;
        [SerializeField, HideInInspector] private Vector3 bodyBaseScale = Vector3.one;

        private static readonly int AfraidId = Animator.StringToHash("Afraid");
        private static readonly int ScaredId = Animator.StringToHash("Scared");
        private static readonly int DeadId = Animator.StringToHash("Dead");

        private Vector3 bodyScaleGoal = Vector3.one;

        public ActorDef Def => def;
        public string DisplayName => def != null ? def.DisplayName : displayName;
        public ActorExpression Expression => expression;
        public bool IsDead { get; private set; }
        public Animator Animator => animator;

        /// <summary>Ponto que a câmera de foco usa para enquadrar o rosto.</summary>
        public Transform FaceAnchor => faceAnchor != null ? faceAnchor : transform;

        /// <summary>Tem rosto de modelo (4 bocas)? Senão, é a cápsula greybox.</summary>
        private bool HasModelFace
        {
            get
            {
                if (mouths == null || mouths.Length < 4) return false;
                foreach (var m in mouths) if (m == null) return false;
                return true;
            }
        }

        private bool AnimatorReady => animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null && Application.isPlaying;

        private void Awake()
        {
            if (HasModelFace && !faceCaptured) CaptureFaceRig();
            if (body != null && HasModelFace) bodyScaleGoal = bodyBaseScale;
            SetHighlighted(false);
        }

        private void Start()
        {
            ApplyExpression();
            SyncAnimator(expression, true);
        }

        public void SetExpression(ActorExpression newExpression)
        {
            ActorExpression previous = expression;
            expression = newExpression;
            ApplyExpression();
            SyncAnimator(previous, false);
        }

        public void SetHighlighted(bool on)
        {
            if (highlightMarker != null)
            {
                highlightMarker.SetActive(on && !IsDead);
            }

            // Contorno de "selecionado" (guia §7): mesma casca do toon, 3,5 px, cor de fita crepe.
            bool any = false;
            if (outlineRenderers != null)
            {
                foreach (var r in outlineRenderers)
                {
                    if (r == null || !ToonMaterials.IsToon(r.sharedMaterial)) continue;
                    ToonMaterials.SetSelectedOutline(r, on && !IsDead);
                    any = true;
                }
            }
            if (!any)
            {
                Renderer r = bodyRenderer != null ? bodyRenderer : (body != null ? body.GetComponent<Renderer>() : null);
                if (r != null && ToonMaterials.IsToon(r.sharedMaterial)) ToonMaterials.SetSelectedOutline(r, on && !IsDead);
            }
        }

        /// <summary>"Saiu do filme": modelo toca o clipe Death (olhos fechados); cápsula deita.</summary>
        public void SetDead()
        {
            IsDead = true;
            SetHighlighted(false);

            if (HasModelFace)
            {
                if (AnimatorReady)
                {
                    animator.SetBool(AfraidId, false);
                    animator.SetTrigger(DeadId);
                }
                ApplyDeadFace();
                return;
            }

            if (body != null)
            {
                body.localRotation = Quaternion.Euler(90f, 0f, 0f);
                body.localPosition = new Vector3(0f, 0.28f, 0f); // deitado: raio da cápsula (0.55 / 2)
            }
            if (faceAnchor != null)
            {
                faceAnchor.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Guarda a pose base de cada peça do rosto e os eixos do rosto (frente/cima/direita) no espaço
        /// local de cada peça. Chamado pelo construtor de cena (modelo em pose T) e, se faltar, no Awake.
        /// </summary>
        public void CaptureFaceRig()
        {
            if (!HasModelFace) return;
            var parts = new[] { leftEye, rightEye, leftPupil, rightPupil, leftBrow, rightBrow };
            poses = new PartPose[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                var t = parts[i];
                poses[i] = t == null ? new PartPose() : new PartPose { t = t, pos = t.localPosition, scale = t.localScale, rot = t.localRotation };
            }

            Transform root = transform;
            if (leftEye != null)
            {
                eyeDepthAxis = Dominant(leftEye.InverseTransformDirection(root.forward), -1);
                eyeHeightAxis = Dominant(leftEye.InverseTransformDirection(root.up), eyeDepthAxis);
            }
            Transform faceSpace = faceAnchor != null ? faceAnchor : root;
            faceRightLocal = faceSpace.InverseTransformDirection(root.right).normalized;
            faceUpLocal = faceSpace.InverseTransformDirection(root.up).normalized;
            if (leftBrow != null)
            {
                browAxisL = leftBrow.InverseTransformDirection(root.forward).normalized;
                browSideL = Mathf.Sign(Vector3.Dot(leftBrow.position - faceSpace.position, root.right));
            }
            if (rightBrow != null)
            {
                browAxisR = rightBrow.InverseTransformDirection(root.forward).normalized;
                browSideR = Mathf.Sign(Vector3.Dot(rightBrow.position - faceSpace.position, root.right));
            }
            if (body != null) bodyBaseScale = body.localScale;
            faceCaptured = true;
        }

        private void LateUpdate()
        {
            // Esticar no susto (guia §4.2: Y 1,15 / XZ 0,9), suave.
            if (body == null || !HasModelFace || !Application.isPlaying) return;
            if ((body.localScale - bodyScaleGoal).sqrMagnitude < 1e-6f) return;
            body.localScale = Vector3.Lerp(body.localScale, bodyScaleGoal, 1f - Mathf.Exp(-squashSpeed * Time.deltaTime));
        }

        // Roda no editor sempre que um valor muda no Inspector:
        // permite trocar a expressão ali e ver o resultado sem dar Play.
        private void OnValidate()
        {
            ApplyExpression();
        }

        private void SyncAnimator(ActorExpression previous, bool initial)
        {
            if (!AnimatorReady || IsDead) return;
            animator.SetBool(AfraidId, expression == ActorExpression.Scared);
            if (!initial && expression == ActorExpression.Scared && previous != ActorExpression.Scared)
            {
                animator.SetTrigger(ScaredId);
            }
        }

        private void ApplyExpression()
        {
            if (HasModelFace)
            {
                if (faceCaptured && !IsDead) ApplyModelExpression();
                return;
            }
            ApplyCapsuleExpression();
        }

        // ------------------------------------------------------------------ Modelo (multiplicadores)

        private void ApplyModelExpression()
        {
            // Tabela do README_Personagens §3 (multiplicadores sobre a pose base).
            float eyeW = 1f, eyeH = 1f, pupil = 1f, look = 0f, browDy = 0f, browTurn = 0f;
            int mouthIndex = 0;
            Vector3 bodyMul = Vector3.one;
            switch (expression)
            {
                case ActorExpression.Happy:
                    eyeH = 0.8f; pupil = 1.1f; browDy = 0.014f; browTurn = 4f; mouthIndex = 1;
                    break;
                case ActorExpression.Tense:
                    pupil = 0.7f; look = 0.013f; browDy = -0.008f; browTurn = -18f; mouthIndex = 2;
                    break;
                case ActorExpression.Scared:
                    eyeW = 1.6f; eyeH = 1.6f; pupil = 0.35f; browDy = 0.03f; browTurn = 16f; mouthIndex = 3;
                    bodyMul = new Vector3(0.9f, 1.15f, 0.9f);
                    break;
            }

            ApplyEye(0, eyeW, eyeH);
            ApplyEye(1, eyeW, eyeH);
            ApplyPupil(2, pupil, look);
            ApplyPupil(3, pupil, look);
            ApplyBrow(4, browDy, browTurn, browAxisL, browSideL);
            ApplyBrow(5, browDy, browTurn, browAxisR, browSideR);
            for (int i = 0; i < mouths.Length; i++) mouths[i].SetActive(i == mouthIndex);

            bodyScaleGoal = Vector3.Scale(bodyBaseScale, bodyMul);
            if (!Application.isPlaying && body != null) body.localScale = bodyBaseScale; // no editor, sem esticar
        }

        private void ApplyDeadFace()
        {
            if (!faceCaptured) return;
            ApplyEye(0, 1f, 0.12f); // olhos fechados (desmaiou)
            ApplyEye(1, 1f, 0.12f);
            for (int i = 2; i <= 3; i++) if (poses[i].t != null) poses[i].t.gameObject.SetActive(false);
            for (int i = 0; i < mouths.Length; i++) mouths[i].SetActive(i == 0);
            bodyScaleGoal = bodyBaseScale;
        }

        private void ApplyEye(int i, float w, float h)
        {
            if (i >= poses.Length || poses[i].t == null) return;
            Vector3 s = poses[i].scale;
            int widthAxis = 3 - eyeDepthAxis - eyeHeightAxis;
            s[widthAxis] *= w;
            s[eyeHeightAxis] *= h;
            poses[i].t.localScale = s;
        }

        private void ApplyPupil(int i, float k, float lookSide)
        {
            if (i >= poses.Length || poses[i].t == null) return;
            var p = poses[i];
            p.t.gameObject.SetActive(true);
            p.t.localScale = p.scale * k;
            p.t.localPosition = p.pos + faceRightLocal * lookSide;
        }

        private void ApplyBrow(int i, float dy, float turn, Vector3 axis, float side)
        {
            if (i >= poses.Length || poses[i].t == null) return;
            var p = poses[i];
            p.t.localPosition = p.pos + faceUpLocal * dy;
            // Giro positivo = ponta de DENTRO sobe (preocupado); espelhado entre os lados.
            p.t.localRotation = p.rot * Quaternion.AngleAxis(-side * turn, axis);
        }

        private static int Dominant(Vector3 v, int exclude)
        {
            int best = -1;
            float bestAbs = -1f;
            for (int a = 0; a < 3; a++)
            {
                if (a == exclude) continue;
                float abs = Mathf.Abs(v[a]);
                if (abs > bestAbs)
                {
                    bestAbs = abs;
                    best = a;
                }
            }
            return best;
        }

        // ------------------------------------------------------------------ Cápsula greybox (legado)

        private void ApplyCapsuleExpression()
        {
            if (leftEye == null || rightEye == null || mouth == null || leftBrow == null || rightBrow == null)
            {
                return;
            }

            // Valores base (rosto neutro), em espaço local do FaceAnchor.
            Vector3 eyeScale = new Vector3(0.07f, 0.07f, 0.03f);
            Vector3 mouthScale = new Vector3(0.14f, 0.025f, 0.03f);
            Vector3 mouthPos = new Vector3(0f, -0.11f, 0.02f);
            float browY = 0.11f;
            float browTilt = 0f;
            float pupil = 1f;          // escala da pupila (guia §4.2)
            float pupilLook = 0f;      // olhar para o lado (tensa)

            switch (expression)
            {
                case ActorExpression.Happy:
                    eyeScale = new Vector3(0.07f, 0.04f, 0.03f);
                    mouthScale = new Vector3(0.18f, 0.045f, 0.03f);
                    browY = 0.13f;
                    browTilt = -8f;
                    pupil = 1.1f;
                    break;
                case ActorExpression.Tense:
                    eyeScale = new Vector3(0.06f, 0.05f, 0.03f);
                    mouthScale = new Vector3(0.12f, 0.015f, 0.03f);
                    browY = 0.095f;
                    browTilt = 22f;
                    pupil = 0.7f;
                    pupilLook = 0.18f;
                    break;
                case ActorExpression.Scared:
                    eyeScale = new Vector3(0.1f, 0.1f, 0.03f);
                    mouthScale = new Vector3(0.08f, 0.1f, 0.03f);
                    mouthPos = new Vector3(0f, -0.13f, 0.02f);
                    browY = 0.16f;
                    browTilt = -15f;
                    pupil = 0.35f;
                    break;
            }

            leftEye.localScale = eyeScale;
            rightEye.localScale = eyeScale;
            mouth.localScale = mouthScale;
            mouth.localPosition = mouthPos;

            leftBrow.localPosition = new Vector3(-0.09f, browY, 0.02f);
            rightBrow.localPosition = new Vector3(0.09f, browY, 0.02f);
            // Sobrancelhas espelhadas: inclinação positiva = "franzido" (tenso).
            leftBrow.localRotation = Quaternion.Euler(0f, 0f, -browTilt);
            rightBrow.localRotation = Quaternion.Euler(0f, 0f, browTilt);

            // Pupila: filha do olho, achatada na frente dele (o olho já é achatado em Z).
            Vector3 pupilScale = new Vector3(0.5f, 0.5f, 0.6f) * pupil;
            if (leftPupil != null)
            {
                leftPupil.localScale = pupilScale;
                leftPupil.localPosition = new Vector3(pupilLook, 0f, 0.42f);
            }
            if (rightPupil != null)
            {
                rightPupil.localScale = pupilScale;
                rightPupil.localPosition = new Vector3(pupilLook, 0f, 0.42f);
            }
        }
    }
}
