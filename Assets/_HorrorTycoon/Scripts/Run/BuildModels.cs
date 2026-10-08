using System.Collections.Generic;

namespace HorrorTycoon.Run
{
    // ====================================================================== Protótipo 3 — Build do Filme (estado e relatório)

    public enum PlotStatus
    {
        Active,
        Completed,
        Failed
    }

    /// <summary>Um plot de permanência DURANTE a run.</summary>
    public class PlotRunState
    {
        public PlotDef Def { get; }
        /// <summary>Sala fixa do plot (plot de sala sem lista de salas). -1 = qualquer sala da lista do PlotDef.</summary>
        public int RoomIndex { get; internal set; }
        /// <summary>Sala onde está avançando agora (-1 = parado).</summary>
        public int ActiveRoom { get; internal set; } = -1;
        public int Progress { get; internal set; }
        public PlotStatus Status { get; internal set; } = PlotStatus.Active;
        /// <summary>Só avança em tiques com número MAIOR que este (Regras.plotStartsNextScene).</summary>
        public int OfferedAtScene { get; internal set; }
        /// <summary>De onde veio (sala ou ator), para a HUD.</summary>
        public string Source { get; internal set; } = "";
        public int TimesBroken { get; internal set; }

        public bool IsActive => Status == PlotStatus.Active;

        public PlotRunState(PlotDef def, int roomIndex)
        {
            Def = def;
            RoomIndex = roomIndex;
        }
    }

    /// <summary>Tipo de linha do relatório da cena (a HUD pinta por tipo).</summary>
    public enum ReportKind
    {
        Info,
        Score,
        Plot,
        PlotDone,
        PlotBroken,
        Combo,
        Pavor,
        Crisis,
        Villain,
        Ghost,
        Unlock,
        Artefato
    }

    public class ReportLine
    {
        public ReportKind Kind;
        public string Text;
        public int Score;
    }

    /// <summary>
    /// RELATÓRIO de uma ação que gastou cena(s): o que o tique da casa fez (plots, combos, pavor, crises, vilão).
    /// Também recebe eventos fora do tique da mesma ação (plot oferecido na exploração, crise por encontro...).
    /// </summary>
    public class SceneReport
    {
        public int FirstScene;
        public int Scenes;
        public int ScoreGained;
        public List<ReportLine> Lines { get; } = new List<ReportLine>();

        public void Add(ReportKind kind, string text, int score = 0)
        {
            Lines.Add(new ReportLine { Kind = kind, Text = text, Score = score });
            ScoreGained += score;
        }

        public bool IsEmpty => Lines.Count == 0;
    }

    /// <summary>Grande Susto do fantasma (a apresentação encena: close em quem estava na sala).</summary>
    public class GhostScareOutcome
    {
        public int Space;
        public int Tension;
        public List<ActorRunState> Actors = new List<ActorRunState>();
        public List<ActorRunState> Crisis = new List<ActorRunState>();
        public int Score;
        /// <summary>true = exorcizado com ferramenta (sem susto).</summary>
        public bool Exorcised;
        public ActorRunState ToolHolder;
        public ToolDef ToolUsed;
    }

    /// <summary>Crise de pavor (a apresentação mostra o ator em pânico).</summary>
    public class CrisisEvent
    {
        public ActorRunState Actor;
        public bool LeftFilm;
        public int Score;
    }

    /// <summary>Cena de dupla ativa num espaço (para a HUD).</summary>
    public class ActiveCombo
    {
        public DuoComboDef Def;
        public int Space;
        public List<ActorRunState> Actors = new List<ActorRunState>();
    }
}
