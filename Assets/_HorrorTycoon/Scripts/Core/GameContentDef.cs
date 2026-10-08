using System.Collections.Generic;
using HorrorTycoon.Actors;
using HorrorTycoon.Rooms;
using HorrorTycoon.Run;
using HorrorTycoon.Scoring;
using UnityEngine;

namespace HorrorTycoon.Core
{
    /// <summary>
    /// "Catálogo" de tudo que existe numa run: formato, regras, atores, salas (o mapa) e vilões.
    /// Encontros, elementos, cenas dirigidas e ferramentas são referenciados pelas salas.
    /// Para adicionar conteúdo: crie o asset e ligue-o aqui ou numa sala.
    /// </summary>
    [CreateAssetMenu(menuName = "Horror Tycoon/Catálogo de Conteúdo", fileName = "Conteudo")]
    public class GameContentDef : ScriptableObject
    {
        [SerializeField] private FilmFormatDef filmFormat;
        [SerializeField] private GameRulesDef rules;
        [SerializeField] private List<ActorDef> actors = new List<ActorDef>();

        [Header("Mapa (casa)")]
        [Tooltip("Corredor central: liga a entrada a todos os cômodos. Área de convivência (sem encontro).")]
        [SerializeField] private RoomDef hubRoom;
        [Tooltip("Cômodos da casa. Cada um ocupa um 'slot' da planta.")]
        [SerializeField] private List<RoomDef> rooms = new List<RoomDef>();
        [Tooltip("Sortear em qual slot da planta cada cômodo fica, a cada run.")]
        [SerializeField] private bool shuffleRooms = true;

        [Header("Vilões oferecidos no fim do Ato 1")]
        [SerializeField] private List<VillainDef> villains = new List<VillainDef>();

        [Header("Elementos automáticos (valem quando a condição é verdadeira)")]
        [Tooltip("Ex.: Isolado (ator sozinho na sala), Apavorado (pavor alto).")]
        [SerializeField] private List<ElementDef> autoElements = new List<ElementDef>();

        [Header("Build do Filme (Protótipo 3)")]
        [Tooltip("Cenas de dupla / combos de elenco (Casal, Investigação, Grupo...). Vazio = sem combos.")]
        [SerializeField] private List<DuoComboDef> duoCombos = new List<DuoComboDef>();
        [Tooltip("Artefatos que podem ser oferecidos entre atos ('1 de 3') e por plots. Vazio = sem oferta.")]
        [SerializeField] private List<ArtefatoDef> artefatos = new List<ArtefatoDef>();

        public FilmFormatDef FilmFormat => filmFormat;
        public GameRulesDef Rules => rules;
        public IReadOnlyList<ActorDef> Actors => actors;
        public IReadOnlyList<RoomDef> Rooms => rooms;
        public RoomDef HubRoom => hubRoom;
        public bool ShuffleRooms => shuffleRooms;
        public IReadOnlyList<VillainDef> Villains => villains;
        public IReadOnlyList<ElementDef> AutoElements => autoElements;
        public IReadOnlyList<DuoComboDef> DuoCombos => duoCombos;
        public IReadOnlyList<ArtefatoDef> Artefatos => artefatos;

        /// <summary>Conteúdo do Protótipo 3 (testes e ferramentas de editor).</summary>
        public void SetupBuild(List<DuoComboDef> combos, List<ArtefatoDef> artefatoPool)
        {
            duoCombos = combos ?? new List<DuoComboDef>();
            artefatos = artefatoPool ?? new List<ArtefatoDef>();
        }

        /// <summary>Usado por ferramentas de editor e testes para montar o catálogo por código.</summary>
        public void Setup(FilmFormatDef format, GameRulesDef gameRules, List<ActorDef> actorList,
            List<RoomDef> roomList, RoomDef hub, bool shuffle, List<VillainDef> villainList,
            List<ElementDef> autoList)
        {
            filmFormat = format;
            rules = gameRules;
            actors = actorList;
            rooms = roomList;
            hubRoom = hub;
            shuffleRooms = shuffle;
            villains = villainList;
            autoElements = autoList;
        }
    }
}
