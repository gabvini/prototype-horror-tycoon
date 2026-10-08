using HorrorTycoon.Scoring;
using UnityEngine;

namespace HorrorTycoon.Core
{
    /// <summary>
    /// Regras globais ajustáveis (números de balanceamento que não pertencem a um conteúdo específico).
    /// Tudo aqui é HIPÓTESE do GDD Vivo — mude no Inspector e jogue de novo.
    /// Os nomes de campo antigos ("Step") ficaram para não perder os valores salvos no asset:
    /// na tela o recurso se chama AÇÕES.
    /// </summary>
    [CreateAssetMenu(menuName = "Horror Tycoon/Regras do Jogo", fileName = "Regras")]
    public class GameRulesDef : ScriptableObject
    {
        [Header("Ações (Protótipo 2)")]
        [Tooltip("Custo em ações para entrar numa SALA ainda não explorada neste ato (gera o encontro). " +
                 "Andar por convivências, corredores e salas já exploradas no ato é grátis.")]
        [Min(0)] public int exploreActionCost = 1;

        [Tooltip("Custo em ações para DIRIGIR uma cena numa sala de cena.")]
        [Min(0)] public int directStepCost = 1;

        [Tooltip("Custo em ações para usar uma ferramenta num ponto trancado.")]
        [Min(0)] public int toolStepCost = 1;

        [Header("Ferramentas (Protótipo 2)")]
        [Tooltip("Quantas ferramentas cada ator carrega. Cheio: a ferramenta achada fica no chão da sala.")]
        [Min(1)] public int maxToolsPerActor = 2;

        [Header("Pavor")]
        [Tooltip("Pavor máximo. Ao chegar aqui: CRISE (Protótipo 3) ou, com a crise desligada, o ator sai do filme.")]
        [Min(1)] public int pavorLimit = 100;

        [Header("Vilão (build)")]
        [Tooltip("Peso extra de cada elemento com a tag do vilão escolhido (2 = conta em dobro).")]
        [Min(1)] public int villainTagWeight = 2;

        [Tooltip("Multiplica a chance de encontros da tag do vilão depois que ele é escolhido.")]
        [Min(1f)] public float villainEncounterBias = 2f;

        [Header("Vilão NPC (Protótipo 2 — experimento)")]
        [Tooltip("Desligado: o vilão volta a ser só a build (como no P1).")]
        public bool villainNpcEnabled = true;

        [Tooltip("revelarVilao: mostra onde o vilão está (boneco, monitor, card). O próximo espaço é sempre anunciado.")]
        public bool revealVillain = true;

        [Tooltip("Primeiro ato em que o vilão anda pela casa (0 = Ato 1, 1 = Ato 2...). Ele só existe depois de escolhido.")]
        [Min(0)] public int villainFirstActIndex = 1;

        [Tooltip("Espaços que o vilão anda a cada batida (cada ação gasta = 1 batida).")]
        [Min(1)] public int villainMovesPerBeat = 1;

        [Tooltip("Repelido (alguém no espaço tem a ferramenta que combate o vilão): pontos de 'sobreviveu' × multiplicador do ato.")]
        [Min(0)] public int repelScore = 80;

        [Tooltip("Repelido: pavor somado a cada ator presente.")]
        public int repelPavor = 10;

        [Tooltip("Repelido: batidas em que o vilão fica parado (depois de reaparecer longe).")]
        [Min(0)] public int repelStunBeats = 2;

        [Tooltip("Grupo (2+ atores): pavor somado a cada ator.")]
        public int groupScarePavor = 30;

        [Tooltip("Grupo: espaços que o vilão recua.")]
        [Min(0)] public int groupScareRetreat = 1;

        [Tooltip("Grupo: batidas em que o vilão fica parado depois de recuar.")]
        [Min(0)] public int groupScareStunBeats = 1;

        [Tooltip("Cena que dispara quando o vilão pega um ator sozinho (vazio = a primeira cena que mata, achada nos palcos).")]
        public PayoffDef villainCatchPayoff;

        // ================================================================== Protótipo 3 — Build do Filme

        [Header("P3 · Cenas (o turno)")]
        [Tooltip("Custo de 'Gravar cena aqui' (esperar 1 cena: a casa inteira conta 1 cena).")]
        [Min(1)] public int recordSceneCost = 1;
        [Tooltip("A pontuação por cena (combos, perto do vilão) usa o multiplicador do ato.")]
        public bool tickScoreUsesActMultiplier = true;

        [Header("P3 · Plots de permanência")]
        [Tooltip("Desliga todos os plots (nenhum é oferecido).")]
        public bool plotsEnabled = true;
        [Tooltip("Plot achado numa exploração só começa a contar na cena SEGUINTE (precisa 'segurar' de verdade).")]
        public bool plotStartsNextScene = true;
        [Tooltip("A audiência do plot cumprido usa o multiplicador do ato.")]
        public bool plotUsesActMultiplier = true;

        [Header("P3 · Pavor por cena")]
        [Tooltip("Pavor por cena de quem está SOZINHO num espaço da casa.")]
        public int alonePavorPerScene = 2;
        [Tooltip("Pavor por cena de quem está perto do vilão (no espaço dele ou vizinho).")]
        public int nearVillainPavorPerScene = 4;

        [Header("P3 · Perto do vilão (os dois lados)")]
        [Tooltip("Vizinhos do espaço do vilão também contam como 'perto'.")]
        public bool nearVillainIncludesAdjacent = true;
        [Tooltip("Audiência por cena de CADA ator perto do vilão (× mult. do ato).")]
        [Min(0)] public int nearVillainScenePoints = 10;
        [Tooltip("Multiplica combos e plots cumpridos perto do vilão.")]
        [Min(1f)] public float nearVillainScoreMult = 1.5f;

        [Header("P3 · Cenas de dupla")]
        [Tooltip("Desliga todos os combos de elenco.")]
        public bool combosEnabled = true;

        [Header("P3 · Passivos dos atores (papéis)")]
        [Tooltip("Desliga todos os passivos.")]
        public bool passivesEnabled = true;
        [Tooltip("Atleta: aliados na mesma sala ganham pavor × este valor.")]
        [Range(0f, 1f)] public float atletaAllyPavorMult = 0.5f;
        [Tooltip("Atleta: 'segurar a porta' (o vilão não entra na sala dele na próxima cena), vezes por ato. 0 = desliga.")]
        [Min(0)] public int atletaHoldDoorPerAct = 1;
        [Tooltip("Nerd: plots de investigação levam N cenas a menos com ele na sala (mínimo 1).")]
        [Min(0)] public int nerdPlotReduction = 1;
        [Tooltip("Nerd: pavor ganho × este valor (frágil).")]
        [Min(0f)] public float nerdPavorMult = 1.5f;
        [Tooltip("Final Girl: pavor que tira de cada aliado na mesma sala, por cena.")]
        [Min(0)] public int finalGirlCalmPerScene = 10;
        [Tooltip("Final Girl: audiência (× ato) ao testemunhar uma morte no espaço dela ou vizinho (o pavor dela zera).")]
        [Min(0)] public int finalGirlWitnessScore = 60;
        [Tooltip("Final Girl determinada (depois de testemunhar): pavor ganho × este valor pelo resto da run.")]
        [Range(0f, 1f)] public float finalGirlDeterminedPavorMult = 0.5f;
        [Tooltip("Popular: cenas com ela rendem × este valor (exploração, cenas dirigidas, combos e plots com ela).")]
        [Min(1f)] public float popularSceneMult = 1.25f;
        [Tooltip("Popular: o vilão a prefere como alvo (depois de 'sozinho').")]
        public bool popularIsBait = true;

        [Header("P3 · Crise de pavor")]
        [Tooltip("Desligado: pavor no limite = sai do filme na hora (regra antiga).")]
        public bool crisisEnabled = true;
        [Tooltip("Depois da crise o pavor volta para este valor.")]
        [Min(0)] public int crisisResetPavor = 60;
        [Tooltip("Cenas que o ator fica travado (não anda).")]
        [Min(0)] public int crisisLockScenes = 1;
        [Tooltip("Na N-ésima crise o ator sai do filme.")]
        [Min(1)] public int crisesToLeave = 2;
        [Tooltip("Audiência (× ato) da cena forçada de pânico.")]
        [Min(0)] public int crisisPanicScore = 20;

        [Header("P3 · Fantasma")]
        [Tooltip("Tensão necessária para o Grande Susto.")]
        [Min(1)] public int ghostTensionMax = 10;
        [Tooltip("Tensão por cena (sempre).")]
        [Min(0)] public int ghostTensionPerScene = 2;
        [Tooltip("Tensão por cena a mais para CADA ator dentro da sala assombrada.")]
        [Min(0)] public int ghostTensionPerActor = 2;
        [Tooltip("Grande Susto: audiência por ponto de tensão (× ato × soma, por ator lá dentro, de (1 + pavor/100)).")]
        [Min(0)] public int ghostScorePerTension = 5;
        [Tooltip("Grande Susto: pavor em cada ator dentro da sala.")]
        public int ghostScarePavor = 25;
        [Tooltip("Exorcizado (ferramenta que combate): cenas em que o fantasma fica parado depois de mudar de sala.")]
        [Min(0)] public int ghostRepelStunScenes = 1;

        [Header("P3 · Artefatos")]
        [Tooltip("Oferta '1 de N' entre atos (grátis).")]
        public bool artefatoOfferEnabled = true;
        [Min(1)] public int artefatoOfferCount = 3;

        [Header("Expressões (só visual)")]
        public int tenseThreshold = 30;
        public int scaredThreshold = 60;
    }
}
