using System;
using System.Collections.Generic;
using System.Linq;
using HorrorTycoon.Core;
using HorrorTycoon.Rooms;
using HorrorTycoon.Rooms.Generation;

namespace HorrorTycoon.Run
{
    /// <summary>Salas oferecidas numa porta para o vazio (a escolha "1 de 3").</summary>
    public class DraftOffer
    {
        public int Site;
        public List<RoomDef> Options = new List<RoomDef>();
        /// <summary>Onde cada opção nasceria (mesma ordem de Options).</summary>
        public List<DraftPlacement> Placements = new List<DraftPlacement>();
    }

    /// <summary>
    /// SET EM GRID (HouseGenDef.growByDraft): a casa começa só com o Hall e cresce peça a peça. Um ator abre uma porta
    /// para o vazio (custa como explorar), o jogador escolhe 1 entre até N peças que cabem ali (salas, convivências,
    /// corredores), a peça nasce na planta com portas novas e:
    ///   - Sala: o ator entra nela (encontro, plots... como uma exploração normal);
    ///   - Convivência: o ator vai para lá (a cena passa);
    ///   - Corredor: o ator fica onde está (a cena passa; corredor é só passagem).
    ///
    /// Determinismo/replay: a oferta usa um fluxo aleatório próprio e só é sorteada em OpenDraft (ação "draft-offer"
    /// no log). A escolha é a ação "draft &lt;porta&gt; &lt;opção&gt; &lt;ator&gt;".
    /// </summary>
    public partial class FilmRun
    {
        private const int DraftSalt = 0xD2AF7;

        private GameRandom draftRng;
        private List<RoomDef> draftPool;
        private DraftRules draftRules = new DraftRules();
        private int draftOptionCount = 3;
        private readonly Dictionary<int, DraftOffer> draftOffers = new Dictionary<int, DraftOffer>();
        private readonly HashSet<RoomDef> draftUnlocked = new HashSet<RoomDef>();

        /// <summary>Casa por escolha ligada nesta run.</summary>
        public bool GrowsByDraft => Layout != null && Layout.GrowsByDraft;

        /// <summary>Lado da célula do grid em metros.</summary>
        public int GridCell => draftRules.Cell;

        /// <summary>Quantas peças cada porta oferece.</summary>
        public int DraftOptionCount => draftOptionCount;

        /// <summary>Portas para o vazio (vazio fora da casa por escolha).</summary>
        public IReadOnlyList<HouseDoorSite> DoorSites => GrowsByDraft ? Layout.Sites : Array.Empty<HouseDoorSite>();

        /// <summary>Uma sala nova entrou na planta (índice do espaço). A montagem 3D escuta.</summary>
        public event Action<int> RoomAdded;

        private void InitDraft(HouseGenDef gen, List<RoomDef> pool, int seed)
        {
            draftRng = new GameRandom(GameRandom.Mix(seed, DraftSalt));
            // Peças: salas do pool + convivências + o corredor (sem repetir).
            draftPool = new List<RoomDef>(pool);
            foreach (var social in gen.socialPool) if (social != null && !draftPool.Contains(social)) draftPool.Add(social);
            if (gen.corridorDef != null && !draftPool.Contains(gen.corridorDef)) draftPool.Add(gen.corridorDef);
            draftRules = DraftRules.From(gen);
            draftOptionCount = Math.Max(1, gen.draftOptions);
        }

        /// <summary>A sala pode aparecer na escolha (lacrada só depois que um plot liberar).</summary>
        private bool DraftAllowed(RoomDef def) => !def.StartsSealed || draftUnlocked.Contains(def);

        /// <summary>Plot liberou uma sala lacrada que ainda não está na casa: entra na escolha. True = mudou algo.</summary>
        private bool UnlockForDraft(RoomDef def)
        {
            if (!GrowsByDraft || def == null || !def.StartsSealed) return false;
            if (rooms.Any(r => r.Def == def)) return false;
            return draftUnlocked.Add(def);
        }

        public HouseDoorSite DoorSite(int site) => site >= 0 && site < DoorSites.Count ? DoorSites[site] : null;

        /// <summary>Alguma sala ainda cabe atrás desta porta? (Não sorteia nada.)</summary>
        public bool SiteHasRoom(int site)
        {
            var s = DoorSite(site);
            return s != null && s.IsOpen && HouseDraft.Eligible(Layout, s, draftPool, draftRules, DraftAllowed).Count > 0;
        }

        /// <summary>Custo de abrir a porta (= explorar uma sala nova). -1 = este ator não chega lá.</summary>
        public int DraftCost(ActorRunState actor, int site)
        {
            var s = DoorSite(site);
            if (s == null || actor == null) return -1;
            if (actor.RoomIndex != s.Host && Map.Doors(actor.RoomIndex, s.Host) < 0) return -1;
            return Rules.exploreActionCost;
        }

        /// <summary>O ator pode abrir esta porta agora?</summary>
        public bool CanDraft(ActorRunState actor, int site)
        {
            if (!CanAct || actor == null || !actor.Alive || actor.IsLocked) return false;
            var s = DoorSite(site);
            if (s == null || !s.IsOpen) return false;
            int cost = DraftCost(actor, site);
            return cost >= 0 && cost <= ActionsLeft && SiteHasRoom(site);
        }

        /// <summary>Oferta já sorteada para a porta (null = ainda não abriram).</summary>
        public DraftOffer OfferAt(int site) => draftOffers.TryGetValue(site, out var o) ? o : null;

        /// <summary>
        /// Abre a escolha da porta: sorteia a oferta na 1ª vez (fica guardada: fechar e abrir de novo não troca as salas).
        /// Se alguma sala da oferta deixou de caber (outra sala ocupou o espaço), ela sai; se não sobrar nenhuma, sorteia de novo.
        /// Ação de log ("draft-offer") para o replay sortear igual.
        /// </summary>
        public DraftOffer OpenDraft(int site)
        {
            var s = DoorSite(site);
            if (s == null || !s.IsOpen || !CanAct) return null;

            var eligible = HouseDraft.Eligible(Layout, s, draftPool, draftRules, DraftAllowed);
            if (eligible.Count == 0) return null;

            draftOffers.TryGetValue(site, out var offer);
            var keep = offer != null ? offer.Options.Where(eligible.Contains).ToList() : new List<RoomDef>();
            bool reroll = keep.Count == 0;
            if (reroll) keep = HouseDraft.RollOffer(eligible, draftOptionCount, draftRng);

            offer = new DraftOffer { Site = site };
            foreach (var def in keep)
            {
                if (!HouseDraft.TryPlace(Layout, s, def, draftRules, out var p)) continue;
                offer.Options.Add(def);
                offer.Placements.Add(p);
            }
            draftOffers[site] = offer;
            Log.actions.Add($"draft-offer {site}");
            if (reroll) Log.Add(ActIndex + 1, "draft-offer", $"Porta {site}: {string.Join(", ", offer.Options.Select(o => o.DisplayName))}");
            return offer;
        }

        /// <summary>
        /// O ator abre a porta e a sala escolhida nasce do outro lado; ele entra nela (exploração: gasta cena, encontro, plots).
        /// O MoveOutcome é o da entrada. Dispara RoomAdded ANTES da entrada (a cena monta a sala).
        /// </summary>
        public MoveOutcome Draft(ActorRunState actor, int site, int option)
        {
            if (!CanDraft(actor, site)) throw new InvalidOperationException("Não dá para abrir esta porta.");
            var offer = OfferAt(site);
            if (offer == null || option < 0 || option >= offer.Options.Count) throw new InvalidOperationException("Escolha inválida.");

            var s = DoorSite(site);
            var def = offer.Options[option];
            if (!HouseDraft.TryPlace(Layout, s, def, draftRules, out var placement))
                throw new InvalidOperationException("A sala não cabe mais aqui.");

            Log.actions.Add($"draft {site} {option} {actor.Def.DisplayName}");
            int index = HouseDraft.AddRoom(Layout, s, def, placement, draftRules);
            draftOffers.Remove(site);
            // Outras ofertas guardadas podem ter perdido espaço: OpenDraft filtra na próxima vez.

            var space = Layout.Spaces[index];
            bool isRoom = space.Kind == SpaceKind.Room;
            var room = new RoomRunState(def, index)
            {
                Kind = space.Kind,
                Space = space,
                IsHub = !isRoom,
                // Convivência e corredor não escondem nada.
                Discovered = !isRoom,
            };
            // Peça montada já está "no filme": não fica lacrada (as lacradas só entram depois de liberadas).
            room.Sealed = false;
            room.Encounter = isRoom ? RollEncounter(def) : null;
            rooms.Add(room);
            Map = new HouseMap(Layout);
            Log.Add(ActIndex + 1, "draft", $"{actor.Def.DisplayName} abre uma porta: nasce {def.DisplayName} ({space.Rect.width}×{space.Rect.height} m)");

            RoomAdded?.Invoke(index);
            if (isRoom) return Move(actor, index, false);

            // Convivência / corredor: montar a peça grava a cena (tique da casa). O ator vai para a convivência;
            // no corredor (só passagem) ele fica onde está.
            BeginReport();
            int cost = Rules.exploreActionCost;
            var outcome = new MoveOutcome { Actor = actor, From = actor.RoomIndex, To = actor.RoomIndex, Cost = cost };
            ActionsLeft -= cost;
            if (space.Kind == SpaceKind.Social)
            {
                actor.RoomIndex = index;
                outcome.To = index;
            }
            Scenes(cost);
            AfterAction();
            return outcome;
        }
    }
}
