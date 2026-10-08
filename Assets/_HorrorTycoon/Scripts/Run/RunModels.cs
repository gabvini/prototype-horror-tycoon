using System.Collections.Generic;
using HorrorTycoon.Actors;
using HorrorTycoon.Rooms;
using HorrorTycoon.Rooms.Generation;
using HorrorTycoon.Scoring;

namespace HorrorTycoon.Run
{
    public enum RunStatus
    {
        Playing,
        Won,
        Lost
    }

    /// <summary>Estado de um ator DURANTE a run (muda a cada ação). C# puro, sem Unity.</summary>
    public class ActorRunState
    {
        /// <summary>Índice de sala que significa "do lado de fora da casa".</summary>
        public const int Outside = -1;

        public ActorDef Def { get; }
        public int Pavor { get; internal set; }
        public bool Alive { get; internal set; } = true;
        public int RoomIndex { get; internal set; } = Outside;
        public List<ElementDef> Elements { get; } = new List<ElementDef>();
        /// <summary>Ferramentas que ESTE ator carrega (limite: GameRulesDef.maxToolsPerActor).</summary>
        public List<ToolDef> Tools { get; } = new List<ToolDef>();

        // ---- Protótipo 3
        /// <summary>Crises de pavor que o ator já teve nesta run (na N-ésima ele sai do filme).</summary>
        public int Crises { get; internal set; }
        /// <summary>Cenas que ainda fica travado (não anda) por causa da crise.</summary>
        public int LockedScenes { get; internal set; }
        /// <summary>Travou DURANTE o tique atual (o tique não desconta esta trava).</summary>
        internal bool LockFresh;
        public bool IsLocked => LockedScenes > 0;
        /// <summary>Final Girl que testemunhou uma morte: ganha menos pavor pelo resto da run.</summary>
        public bool Determined { get; internal set; }
        /// <summary>Ato em que o Atleta já segurou a porta (-1 = ainda não).</summary>
        public int DoorHeldAct { get; internal set; } = -1;
        public int DoorHoldsThisAct { get; internal set; }
        /// <summary>Por que saiu do filme (morte, fugiu de medo).</summary>
        public ExitReason Exit { get; internal set; }
        public ActorRole Role => Def != null ? Def.Role : ActorRole.None;

        public ActorRunState(ActorDef def)
        {
            Def = def;
            Pavor = def.StartingPavor;
        }
    }

    /// <summary>Por que um ator saiu do filme.</summary>
    public enum ExitReason
    {
        None,
        Died,   // morte (cena dirigida ou pega do vilão): conta como "testemunhar uma morte"
        Fled    // pavor: saiu do filme (2ª crise ou regra antiga)
    }

    /// <summary>Estado de uma sala DURANTE a run.</summary>
    public class RoomRunState
    {
        public RoomDef Def { get; }
        public int Index { get; }
        public bool VisitedThisAct { get; internal set; }
        /// <summary>Já foi visitada alguma vez nesta run (para a névoa: sala desconhecida fica no escuro).</summary>
        public bool Discovered { get; internal set; }
        /// <summary>
        /// Área de convivência/passagem (sem encontro). Planta fixa: o corredor. Casa gerada: todo espaço
        /// que NÃO é Sala (convivências e corredores). HUD e câmera usam isto.
        /// </summary>
        public bool IsHub { get; internal set; }
        /// <summary>Tipo do espaço. Planta fixa: o corredor central conta como Convivência (atores param nele).</summary>
        public SpaceKind Kind { get; internal set; } = SpaceKind.Room;
        /// <summary>Espaço da casa gerada (retângulo, etc.). null na planta fixa.</summary>
        public HouseSpace Space { get; internal set; }
        /// <summary>Só Salas têm encontro/exploração.</summary>
        public bool IsExplorable => Kind == SpaceKind.Room;
        /// <summary>Atores podem PARAR aqui (corredor é só passagem).</summary>
        public bool IsDestination => Kind != SpaceKind.Corridor;
        public EncounterDef Encounter { get; internal set; }
        public bool LockUsed { get; internal set; }
        public List<ElementDef> Elements { get; } = new List<ElementDef>();
        /// <summary>Ferramentas no chão (achadas por ator de mãos cheias, ou largadas por quem saiu do filme).</summary>
        public List<ToolDef> FloorTools { get; } = new List<ToolDef>();
        /// <summary>Lacrada (Protótipo 3): ninguém entra até um plot liberar.</summary>
        public bool Sealed { get; internal set; }
        /// <summary>Os plots desta sala já foram oferecidos (1ª exploração da run).</summary>
        public bool PlotsOffered { get; internal set; }

        public RoomRunState(RoomDef def, int index)
        {
            Def = def;
            Index = index;
            Sealed = def != null && def.StartsSealed;
        }
    }

    /// <summary>Um elemento que vai contar num payoff, e quanto ele vale.</summary>
    public class CountedElement
    {
        public ElementDef Element;
        public int Weight;
        public bool FromRoom;
        public bool IsAuto;
        public bool BoostedByVillain;
    }

    public class MoveOutcome
    {
        public ActorRunState Actor;
        public int From;
        public int To;
        public int Cost;
        public EncounterDef Encounter;   // null = sala já visitada neste ato (ou espaço sem encontro)
        public int Points;               // audiência do encontro (com Popular/artefatos)
        public ElementDef ElementGained;
        public ToolDef ToolGained;       // foi para a mão do ator
        public ToolDef ToolLeftOnFloor;  // achou, mas estava de mãos cheias
        public List<ToolDef> ToolsPickedUp = new List<ToolDef>(); // pegou do chão ao entrar
        public bool ActorBroke;          // pavor chegou ao limite
    }

    public class PayoffOutcome
    {
        public ActorRunState Actor;
        public RoomRunState Room;
        public PayoffDef Payoff;
        public List<CountedElement> Counted = new List<CountedElement>();
        public int Score;
        public bool Killed;
        public bool ActorBroke;
    }

    public class ToolOutcome
    {
        public ActorRunState Actor;
        /// <summary>Quem carrega a ferramenta (pode ser outro ator no mesmo espaço).</summary>
        public ActorRunState Holder;
        public RoomRunState Room;
        public ToolDef Tool;
        public ElementDef Reward;
    }

    /// <summary>Resultado do fim de um ato.</summary>
    public class ActOutcome
    {
        public int ActIndex;
        public string ActName;
        public int Goal;
        public int TotalScore;
        public bool Passed;
        public bool Fatal;
        public bool OffersVillain;
        /// <summary>Protótipo 3: depois desta tela vem a escolha de 1 de 3 artefatos.</summary>
        public bool OffersArtefato;
    }

    // ====================================================================== Vilão NPC

    public enum VillainMoveReason
    {
        Spawn,     // entrou na casa (começo do Ato 2)
        Beat,      // andou 1 espaço numa batida
        Retreat,   // recuou depois de assustar um grupo
        Teleport,  // repelido: sumiu e reapareceu longe
        Haunt      // fantasma: passou a assombrar outra sala
    }

    /// <summary>O vilão mudou de espaço (a apresentação anima o boneco).</summary>
    public class VillainMoveEvent
    {
        public int From;
        public int To;
        public VillainMoveReason Reason;
    }

    public enum VillainEncounterKind
    {
        Repelled,   // alguém tinha a ferramenta que combate o vilão
        GroupScare, // 2+ atores juntos
        Caught      // ator sozinho: sai do filme e a Morte dispara
    }

    /// <summary>O vilão e atores ficaram no mesmo espaço.</summary>
    public class VillainEncounterOutcome
    {
        public VillainEncounterKind Kind;
        public int Space;
        /// <summary>true = um ator entrou no espaço do vilão; false = o vilão chegou.</summary>
        public bool ActorWalkedIn;
        public List<ActorRunState> Actors = new List<ActorRunState>();
        public List<ActorRunState> Broke = new List<ActorRunState>();
        public int PavorDelta;
        public int Score;
        /// <summary>Susto em grupo bloqueado por um combo (Grupo): sem pavor.</summary>
        public bool Blocked;

        // Repelido
        public ActorRunState ToolHolder;
        public ToolDef ToolUsed;

        // Pego
        public ActorRunState Victim;
        public PayoffOutcome Payoff; // null = sem cena (ato antes do MinActIndex da Morte)

        /// <summary>Para onde o vilão foi depois (repelido/recuo). -1 = ficou.</summary>
        public int VillainMovedTo = -1;
        public int StunBeats;
    }
}
