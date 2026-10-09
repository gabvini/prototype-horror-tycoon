using System.Collections.Generic;
using System.Linq;
using System.Text;
using HorrorTycoon.Actors;
using HorrorTycoon.Cameras;
using HorrorTycoon.Rooms;
using HorrorTycoon.Run;
using HorrorTycoon.Scoring;
using UnityEngine;

namespace HorrorTycoon.UI
{
    /// <summary>
    /// HUD TEMPORÁRIA do P1, feita em IMGUI (OnGUI) por ser rápida de montar e mudar.
    /// Só LÊ o RunPresenter/FilmRun e chama métodos públicos: trocar por UI Toolkit depois
    /// não mexe em nenhuma regra.
    ///
    /// Organização (v2):
    ///   - Sobre as salas: só o NOME (e se já foi explorada). Nada de texto poluindo o mapa.
    ///   - Clicar numa sala abre o CARD DA SALA: o que se sabe dela, em linguagem de jogador,
    ///     e os botões "quem vai?" com o custo em CENAS de cada ator ("grátis" ou "1 cena").
    ///   - Embaixo: o ator selecionado e o que ele pode fazer onde está (dirigir cena, usar ferramenta).
    /// Informação parcial: nunca mostra pontuação antes de agir.
    /// Protótipo 2: ferramentas por ator no Elenco; vilão NPC (posição, anúncio do próximo espaço, "na mira").
    /// Protótipo 3 (Build do Filme): CENAS no lugar de ações; painel ROTEIRO (plots com marcadores de progresso,
    /// cenas de dupla ativas, artefatos); crise/travado no Elenco; card "se ficar aqui por uma cena"; Fantasma
    /// (sala assombrada + barra de tensão); "Gravar cena" e "Segurar a porta"; escolha de 1 de 3 artefatos.
    /// </summary>
    public class RunHud : MonoBehaviour
    {
        [SerializeField] private RunPresenter presenter;
        [SerializeField] private CinematicDirector director;
        [SerializeField] private DirectorMonitor monitor;

        [Tooltip("Altura das faixas pretas de cinema (fração da tela) nos planos de filme.")]
        [SerializeField] private float letterboxHeight = 0.08f;

        private float letterbox; // 0..1, animado

        private GUIStyle box;
        private GUIStyle title;
        private GUIStyle small;
        private GUIStyle big;
        private GUIStyle label;
        private GUIStyle header;
        private Texture2D white;

        private static readonly Color Panel = new Color(0.06f, 0.06f, 0.09f, 0.88f);
        private const float CastRowHeight = 76f;
        private const string GhostPurple = "#b9a8ff";
        private const string VillainRed = "#ff5a4a";

        private void EnsureStyles()
        {
            if (box != null) return;
            white = Texture2D.whiteTexture;
            box = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, padding = new RectOffset(10, 10, 8, 8), richText = true, wordWrap = true, fontSize = 13 };
            title = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 15, richText = true, wordWrap = true };
            small = new GUIStyle(GUI.skin.label) { fontSize = 12, richText = true, wordWrap = true };
            big = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true };
            label = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, richText = true, alignment = TextAnchor.UpperCenter, wordWrap = true };
            header = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, richText = true };
            header.normal.textColor = new Color(0.85f, 0.75f, 0.5f);
        }

        private void Update()
        {
            float target = director != null && director.IsFilmMode ? 1f : 0f;
            letterbox = Mathf.MoveTowards(letterbox, target, Time.unscaledDeltaTime * 2.5f);
        }

        private void OnGUI()
        {
            if (presenter == null || presenter.Run == null) return;
            EnsureStyles();
            HudInputBlocker.BeginFrame();

            var run = presenter.Run;
            DrawLetterbox();
            if (director == null || director.ShowWorldLabels) DrawWorldLabels(run);
            DrawTopPanel(run);
            DrawCast(run);
            DrawScript(run);
            DrawMonitor(run);

            switch (presenter.CurrentPhase)
            {
                case RunPresenter.Phase.Idle:
                    DrawRoomCard(run);
                    DrawSelectedPanel(run);
                    DrawHoverHint();
                    break;
                case RunPresenter.Phase.ShowingResult:
                    DrawResult();
                    break;
                case RunPresenter.Phase.ActBreak:
                    DrawActBreak();
                    break;
                case RunPresenter.Phase.VillainChoice:
                    DrawVillainChoice(run);
                    break;
                case RunPresenter.Phase.ArtefatoChoice:
                    DrawArtefatoChoice(run);
                    break;
                case RunPresenter.Phase.DraftChoice:
                    DrawDraftChoice();
                    break;
                case RunPresenter.Phase.Ended:
                    DrawEnd(run);
                    break;
            }
        }

        // ================================================================== Mapa: só o essencial

        private void DrawWorldLabels(FilmRun run)
        {
            var cam = presenter.MainCamera;
            if (cam == null) return;

            for (int i = 0; i < run.Rooms.Count; i++)
            {
                var anchor = presenter.AnchorOf(i);
                if (anchor == null) continue;
                Vector3 sp = cam.WorldToScreenPoint(anchor.transform.position + Vector3.up * 0.2f);
                if (sp.z < 0f) continue;

                var room = run.Rooms[i];
                if (!ShowsLabel(room)) continue;
                string text = room.Discovered ? room.Def.DisplayName : "<color=#8a8a8a>?</color>";
                if (room.VisitedThisAct) text += "\n<size=10><color=#a0a0a0>explorada</color></size>";
                if (room.Elements.Count > 0) text += $"\n<size=10><color=#ffd966>● {room.Elements.Count}</color></size>";
                if (room.Sealed && room.Discovered) text += "\n<size=10><color=#ff9a6a>trancada</color></size>";
                if (run.PlotsFor(i).Count > 0) text += "\n<size=10><color=#ffd966>◆ plot</color></size>";
                if (IsGhostHere(run, i)) text += $"\n<size=10><color={GhostPurple}>assombrada</color></size>";
                else if (IsVillainNext(run, i)) text += IsGhost(run)
                    ? $"\n<size=10><color={GhostPurple}>próxima assombração</color></size>"
                    : $"\n<size=10><color={VillainRed}>passos se aproximando…</color></size>";

                var rect = new Rect(sp.x - 80, Screen.height - sp.y - 10, 160, 64);
                GUI.Label(Shadow(rect), StripColors(text), label);
                GUI.Label(rect, text, label);
            }

            foreach (var actor in run.Actors)
            {
                var view = presenter.ViewOf(actor);
                if (view == null || !actor.Alive) continue;
                Vector3 sp = cam.WorldToScreenPoint(view.transform.position + Vector3.up * 2.4f);
                if (sp.z < 0f) continue;
                string n = actor == presenter.Selected ? $"<color=#ffd966>▶ {actor.Def.DisplayName}</color>" : actor.Def.DisplayName;
                if (actor.IsLocked) n += " <color=#ff6666>(pânico!)</color>";
                var rect = new Rect(sp.x - 90, Screen.height - sp.y - 10, 180, 20);
                GUI.Label(Shadow(rect), StripColors(n), label);
                GUI.Label(rect, n, label);
            }

            var vv = presenter.VillainViewObject;
            if (vv != null && presenter.RevealVillain && run.VillainNpc != null && run.VillainNpc.InHouse)
            {
                Vector3 sp = cam.WorldToScreenPoint(vv.transform.position + Vector3.up * 2.7f);
                if (sp.z > 0f)
                {
                    string n = $"<color={VillainRed}>{run.Villain.DisplayName}{(run.VillainNpc.IsStunned ? $" (parado {run.VillainNpc.StunBeats})" : "")}</color>";
                    var rect = new Rect(sp.x - 90, Screen.height - sp.y - 10, 180, 20);
                    GUI.Label(Shadow(rect), StripColors(n), label);
                    GUI.Label(rect, n, label);
                }
            }
        }

        // ================================================================== Vilão: consultas para a HUD

        /// <summary>O vilão vai entrar neste espaço na próxima batida?</summary>
        public static bool IsVillainNext(FilmRun run, int room) =>
            run.VillainNpc != null && run.VillainNpc.IsTelegraphing && run.VillainNpc.NextSpace == room;

        private static bool IsGhost(FilmRun run) => run.VillainNpc != null && run.VillainNpc.IsGhost;

        /// <summary>O fantasma assombra este espaço?</summary>
        private static bool IsGhostHere(FilmRun run, int room) =>
            IsGhost(run) && run.VillainNpc.InHouse && run.VillainNpc.Space == room;

        /// <summary>Tensão do fantasma em palavras (informação parcial: sem número).</summary>
        private static string TensionWords(float r) =>
            r >= 0.8f ? "<color=#ff6666>no limite</color>" : r >= 0.5f ? "alta" : r >= 0.2f ? "subindo" : "baixa";

        /// <summary>Marcadores de progresso de plot: ●●○ (cenas cumpridas / necessárias).</summary>
        private static string Pips(int done, int total)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < total; i++) sb.Append(i < done ? "●" : "○");
            return sb.ToString();
        }

        private static string RoleNames(PlotDef def)
        {
            var parts = new List<string>();
            foreach (var r in def.RequiredRoles) parts.Add(RoleName(r));
            if (def.MinActors > 0) parts.Add($"{def.MinActors}+ atores");
            if (def.MaxActors == 1) parts.Add("sozinho(a)");
            return string.Join(" + ", parts);
        }

        private static string RoleName(ActorRole r) => r == ActorRole.FinalGirl ? "Final Girl" : r.ToString();

        /// <summary>O vilão está neste espaço (e a regra deixa mostrar)?</summary>
        private bool IsVillainHere(FilmRun run, int room) =>
            presenter.RevealVillain && run.VillainNpc != null && run.VillainNpc.InHouse && run.VillainNpc.Space == room;

        private static string ToolList(List<ToolDef> tools)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < tools.Count; i++) sb.Append(i > 0 ? ", " : "").Append(tools[i].DisplayName);
            return sb.ToString();
        }

        // ================================================================== Painéis fixos

        private void DrawTopPanel(FilmRun run)
        {
            var rect = new Rect(10, 10, 300, 172);
            HudInputBlocker.Register(rect);
            DrawPanel(rect);

            var act = run.CurrentAct;
            int total = run.ScenesThisAct;
            GUILayout.BeginArea(new Rect(rect.x + 12, rect.y + 8, rect.width - 24, rect.height - 16));
            GUILayout.Label($"<b>{run.Content.FilmFormat.DisplayName}</b> · {act.name} de {run.ActCount}", small);
            GUILayout.Label($"Cenas  <size=24><b>{run.ScenesLeft}</b></size> / {total}", title);
            GUILayout.Label($"Pontos  <b>{run.TotalScore}</b>  <color=#a0a0a0>(meta do ato: {act.goal})</color>", small);
            Rect bar = GUILayoutUtility.GetRect(rect.width - 24, 8);
            DrawBar(bar, act.goal > 0 ? (float)run.TotalScore / act.goal : 1f, new Color(0.9f, 0.75f, 0.3f));
            GUILayout.Space(4);
            GUILayout.Label(run.Villain != null
                ? $"Vilão: <b><color=#{Hex(run.Villain.Color)}>{run.Villain.DisplayName}</color></b>"
                : "Vilão: <color=#a0a0a0>escolhido no fim do Ato 1</color>", small);

            var v = run.VillainNpc;
            if (v != null && v.InHouse && v.IsGhost)
            {
                string next = v.NextSpace >= 0 && v.NextSpace != v.Space ? $" <size=10>→ {run.Rooms[v.NextSpace].Def.DisplayName}</size>" : "";
                string stun = v.IsStunned ? " (se recompondo)" : "";
                GUILayout.Label($"<color={GhostPurple}>Fantasma assombra: <b>{run.Rooms[v.Space].Def.DisplayName}</b>{stun}</color>{next}", small);
                Rect tb = GUILayoutUtility.GetRect(rect.width - 24, 7);
                DrawBar(tb, run.GhostTensionRatio, Color.Lerp(new Color(0.55f, 0.45f, 1f), new Color(1f, 0.3f, 0.5f), run.GhostTensionRatio));
            }
            else if (v != null && v.InHouse)
            {
                string where = presenter.RevealVillain ? run.Rooms[v.Space].Def.DisplayName : "?";
                string next = v.IsStunned ? $"parado ({v.StunBeats} batida(s))"
                    : v.IsTelegraphing ? $"vai para <b>{run.Rooms[v.NextSpace].Def.DisplayName}</b>" : "à espreita";
                string target = v.Target != null ? $" · caça <b>{v.Target.Def.DisplayName}</b>" : "";
                GUILayout.Label($"<color={VillainRed}>Em: {where} → {next}</color>{target}", small);
            }
            else
            {
                GUILayout.Label("<color=#a0a0a0>Andar é grátis. Explorar, dirigir, usar ferramenta ou gravar = 1 cena.</color>", small);
            }
            GUILayout.EndArea();
        }

        private void DrawCast(FilmRun run)
        {
            float rowH = CastRowHeight;
            var rect = new Rect(Screen.width - 260, 10, 250, 30 + run.Actors.Count * rowH);
            HudInputBlocker.Register(rect);
            DrawPanel(rect);

            GUI.Label(new Rect(rect.x + 10, rect.y + 6, 220, 20), "ELENCO", header);
            for (int i = 0; i < run.Actors.Count; i++)
            {
                var actor = run.Actors[i];
                var row = new Rect(rect.x + 6, rect.y + 26 + i * rowH, rect.width - 12, rowH - 4);

                if (actor == presenter.Selected) DrawRect(row, new Color(1f, 0.85f, 0.3f, 0.15f));
                if (actor.Alive && GUI.Button(row, GUIContent.none, GUIStyle.none) && presenter.CurrentPhase == RunPresenter.Phase.Idle)
                {
                    presenter.Select(actor);
                }

                string where = actor.RoomIndex < 0 ? "lá fora" : run.Rooms[actor.RoomIndex].Def.DisplayName;
                string gone = actor.Exit == ExitReason.Fled ? "fugiu do filme" : "fora do filme";
                string status = actor.Alive ? $"<color=#a0a0a0>{where}</color>" : $"<color=#ff6666>{gone}</color>";
                bool hunted = actor.Alive && run.VillainNpc != null && run.VillainNpc.InHouse && run.VillainNpc.Target == actor;
                string mark = hunted ? $"  <color={VillainRed}><b>● na mira</b></color>" : "";
                string role = string.IsNullOrEmpty(actor.Def.RoleTitle) ? "" : $" <size=10><color=#c8b88a>{actor.Def.RoleTitle.ToLowerInvariant()}</color></size>";
                GUI.Label(new Rect(row.x + 4, row.y, row.width - 8, 18), $"<b>{actor.Def.DisplayName}</b>{role}  {status}{mark}", small);

                if (actor.Alive)
                {
                    float ratio = (float)actor.Pavor / run.Content.Rules.pavorLimit;
                    GUI.Label(new Rect(row.x + 4, row.y + 17, 50, 16), "<size=10>pavor</size>", small);
                    DrawBar(new Rect(row.x + 44, row.y + 23, row.width - 52, 5), ratio,
                        Color.Lerp(new Color(0.4f, 0.8f, 0.4f), new Color(0.9f, 0.2f, 0.2f), ratio));

                    var el = new StringBuilder();
                    foreach (var e in actor.Elements) el.Append($"<color=#{Hex(e.Color)}>● {e.DisplayName}</color>  ");
                    GUI.Label(new Rect(row.x + 4, row.y + 29, row.width - 8, 18), $"<size=10>{el}</size>", small);

                    int max = run.Content.Rules.maxToolsPerActor;
                    string tools = actor.Tools.Count == 0 ? "<color=#707070>mãos vazias</color>" : $"<color=#9fd0ff>{ToolList(actor.Tools)}</color>";
                    GUI.Label(new Rect(row.x + 4, row.y + 43, row.width - 8, 16), $"<size=10>ferr.: {tools} <color=#707070>({actor.Tools.Count}/{max})</color></size>", small);

                    // Protótipo 3: estado de crise / determinação.
                    var st = new StringBuilder();
                    if (actor.IsLocked) st.Append($"<color=#ff6666><b>CRISE</b>: em pânico, travado(a) {actor.LockedScenes} cena(s)</color>  ");
                    else if (actor.Crises > 0) st.Append($"<color=#ff9a6a>crises {actor.Crises}/{run.Content.Rules.crisesToLeave}</color>  ");
                    if (actor.Determined) st.Append("<color=#8fdc8f>★ determinada</color>  ");
                    if (actor.Role == ActorRole.Atleta && run.Content.Rules.atletaHoldDoorPerAct > 0)
                        st.Append(actor.DoorHoldsThisAct < run.Content.Rules.atletaHoldDoorPerAct ? "<color=#a0a0a0>porta: disponível</color>" : "<color=#707070>porta: usada no ato</color>");
                    GUI.Label(new Rect(row.x + 4, row.y + 57, row.width - 8, 16), $"<size=10>{st}</size>", small);
                }
            }
        }

        // ================================================================== Card da sala

        /// <summary>Casa por escolha (HUD antiga): card simples da porta para o vazio, com "Quem abre?".</summary>
        private void DrawSiteCard(FilmRun run)
        {
            int site = presenter.CardSite;
            if (run.DoorSite(site) == null) return;
            var alive = new List<ActorRunState>();
            foreach (var a in run.Actors) if (a.Alive) alive.Add(a);
            var rect = new Rect(14, Screen.height * 0.5f - 90, 300, 96 + alive.Count * 30);
            HudInputBlocker.Register(rect);
            DrawPanel(rect);
            GUI.Label(new Rect(rect.x + 10, rect.y + 8, rect.width - 20, 24), "<b>Porta fechada</b>", big);
            bool room = run.SiteHasRoom(site);
            GUI.Label(new Rect(rect.x + 10, rect.y + 38, rect.width - 20, 40),
                room ? $"Do outro lado ainda não há nada. Abra e escolha 1 de {run.DraftOptionCount} salas." : "Nenhuma sala cabe aqui.", small);
            for (int i = 0; i < alive.Count; i++)
            {
                var a = alive[i];
                GUI.enabled = run.CanDraft(a, site);
                if (GUI.Button(new Rect(rect.x + 10, rect.y + 80 + i * 30, rect.width - 20, 26), $"{a.Def.DisplayName} abre")) presenter.OpenDraft(a, site);
                GUI.enabled = true;
            }
        }

        /// <summary>Casa por escolha (HUD antiga): escolher a sala que nasce atrás da porta.</summary>
        private void DrawDraftChoice()
        {
            var offer = presenter.CurrentDraft;
            if (offer == null) return;
            float w = 260f * Mathf.Max(1, offer.Options.Count) + 20f;
            var rect = new Rect((Screen.width - w) * 0.5f, Screen.height * 0.5f - 150, w, 290);
            HudInputBlocker.Register(rect);
            DrawPanel(rect);
            GUI.Label(new Rect(rect.x, rect.y + 10, rect.width, 36), "Escolha a sala", big);
            for (int i = 0; i < offer.Options.Count; i++)
            {
                var r = offer.Options[i];
                var size = offer.Placements[i].Rect;
                var b = new Rect(rect.x + 10 + i * 260, rect.y + 60, 250, 170);
                if (GUI.Button(b, GUIContent.none)) presenter.ChooseDraft(i);
                DrawRect(new Rect(b.x, b.y, b.width, 5), r.FloorColor);
                GUI.Label(new Rect(b.x + 10, b.y + 14, b.width - 20, b.height - 20),
                    $"<size=17><b>{r.DisplayName}</b></size>  {size.width}×{size.height} m\n\n{r.Description}", small);
            }
            if (GUI.Button(new Rect(rect.x + rect.width * 0.5f - 60, rect.yMax - 46, 120, 30), "Voltar")) presenter.ChooseDraft(-1);
        }

        private void DrawRoomCard(FilmRun run)
        {
            if (presenter.CardSite >= 0)
            {
                DrawSiteCard(run);
                return;
            }
            int index = presenter.CardRoom;
            if (index < 0 || index >= run.Rooms.Count) return;
            var room = run.Rooms[index];
            var def = room.Def;

            var lines = new StringBuilder();
            string cardTitle = def.DisplayName;

            // Vilão: aviso no topo do card.
            if (IsGhost(run))
            {
                if (IsGhostHere(run, index)) lines.AppendLine($"<color={GhostPurple}><b>O fantasma assombra esta sala.</b> Tensão {TensionWords(run.GhostTensionRatio)}.</color>\n");
                else if (IsVillainNext(run, index)) lines.AppendLine($"<color={GhostPurple}><b>Próxima assombração:</b> depois do próximo Grande Susto o fantasma vem para cá.</color>\n");
            }
            else
            {
                if (IsVillainHere(run, index)) lines.AppendLine($"<color={VillainRed}><b>O vilão está aqui.</b></color>\n");
                if (IsVillainNext(run, index)) lines.AppendLine($"<color={VillainRed}><b>Passos se aproximando…</b> O vilão entra aqui na próxima cena gravada.</color>\n");
            }
            if (run.IsDoorHeldNext(index)) lines.AppendLine("<color=#8fdc8f><b>Porta segurada:</b> o vilão não entra aqui na próxima cena.</color>\n");
            if (room.FloorTools.Count > 0) lines.AppendLine($"<b>No chão:</b> <color=#9fd0ff>{ToolList(room.FloorTools)}</color> <color=#a0a0a0>(quem entrar com espaço pega)</color>\n");

            if (!room.IsDestination)
            {
                // Casa gerada: corredor é só passagem (ninguém para aqui).
                lines.AppendLine("<b>Passagem</b>");
                lines.AppendLine("Os atores atravessam este corredor a caminho das salas, mas não param aqui.");
                lines.AppendLine("<color=#a0a0a0>Para mandar alguém, clique numa sala ou numa área de convivência.</color>\n");
            }
            else if (room.IsHub)
            {
                if (room.Space == null) lines.AppendLine("<i>O corredor liga a entrada a todos os cômodos.</i>\n");
                else if (!string.IsNullOrEmpty(def.Description)) lines.AppendLine($"<i>{def.Description}</i>\n");
                lines.AppendLine("<b>Área de convivência</b>");
                lines.AppendLine("Os atores passam e se encontram aqui. Não há nada para explorar.\n");
                AppendStayPreview(run, index, lines);
            }
            else if (room.Sealed)
            {
                cardTitle = room.Discovered ? $"{def.DisplayName} (trancada)" : "Porta trancada";
                lines.AppendLine("<b><color=#ff9a6a>Trancada.</color></b> Ninguém entra por enquanto.");
                if (!string.IsNullOrEmpty(def.SealedText)) lines.AppendLine($"<i>{def.SealedText}</i>");
                lines.AppendLine("<color=#a0a0a0>Algum plot do filme pode liberar esta sala.</color>\n");
            }
            else if (!room.Discovered)
            {
                cardTitle = "Porta fechada";
                lines.AppendLine("<i>Ninguém entrou aqui ainda. Não se sabe que cômodo é.</i>\n");
                if (def.Hints.Count > 0)
                {
                    lines.AppendLine("<b>Pela porta…</b>");
                    lines.AppendLine($"• {def.Hints[0]}\n");
                }
                lines.AppendLine("<color=#a0a0a0>Entre para descobrir o cômodo e o que há nele.</color>\n");
            }
            else
            {
                if (!string.IsNullOrEmpty(def.Description)) lines.AppendLine($"<i>{def.Description}</i>\n");

                if (def.Hints.Count > 0)
                {
                    lines.AppendLine("<b>O que se sabe</b>");
                    foreach (var hint in def.Hints) lines.AppendLine($"• {hint}");
                    lines.AppendLine();
                }

                AppendStayPreview(run, index, lines);

                lines.AppendLine("<b>Exploração</b>");
                lines.AppendLine(room.VisitedThisAct
                    ? "<color=#a0a0a0>Já explorada neste ato. Entrar de novo é grátis e não revela nada novo.</color>"
                    : $"Ainda não explorada neste ato. O primeiro ator que entrar descobre o que tem aqui ({Actions(run.Content.Rules.exploreActionCost)}).");
                lines.AppendLine();

                if (def.StagePayoffs.Count > 0)
                {
                    lines.AppendLine("<b>Palco</b> <color=#a0a0a0>(dá para dirigir cenas aqui)</color>");
                    foreach (var p in def.StagePayoffs)
                    {
                        if (p == null) continue;
                        string when = run.ActIndex < p.MinActIndex ? $" <color=#ff9a9a>(a partir do Ato {p.MinActIndex + 1})</color>" : "";
                        lines.AppendLine($"• <b><color=#{Hex(p.Color)}>{p.DisplayName}</color></b>{when}: {p.Description}");
                    }
                    lines.AppendLine($"<color=#a0a0a0>Leve um ator com elementos até aqui e dirija a cena ({Actions(run.Content.Rules.directStepCost)}).</color>");
                    lines.AppendLine();
                }

                if (def.HasLockedSpot && !room.LockUsed)
                {
                    ActorRunState holder = null;
                    foreach (var a in run.Actors) if (a.Alive && a.Tools.Contains(def.Locked.requiredTool)) holder = a;
                    lines.AppendLine($"<b>{def.Locked.name} trancado</b>");
                    if (holder != null)
                        lines.AppendLine($"<color=#8fdc8f>{holder.Def.DisplayName} está com: {def.Locked.requiredTool.DisplayName}. Leve {holder.Def.DisplayName} até aqui (ou alguém junto) para abrir.</color>");
                    else if (room.FloorTools.Contains(def.Locked.requiredTool))
                        lines.AppendLine($"<color=#8fdc8f>No chão daqui: {def.Locked.requiredTool.DisplayName}. Quem entrar com espaço pega.</color>");
                    else
                        lines.AppendLine("Precisa de uma ferramenta para abrir. Talvez esteja em algum lugar da casa.");
                    lines.AppendLine();
                }

                if (room.Elements.Count > 0)
                {
                    lines.AppendLine("<b>Elementos nesta sala</b>");
                    foreach (var e in room.Elements) lines.AppendLine($"<color=#{Hex(e.Color)}>● {e.DisplayName}</color> — {e.Description}");
                    lines.AppendLine();
                }
            }

            // Altura estimada do texto
            float w = 340f;
            float textH = small.CalcHeight(new GUIContent(lines.ToString()), w - 24);
            int alive = 0;
            foreach (var a in run.Actors) if (a.Alive) alive++;
            bool moveButtons = ShowsMoveButtons(room);
            float buttonsH = moveButtons ? 26 + alive * 30 : 0f;
            float h = Mathf.Min(Screen.height - 200, 46 + textH + buttonsH);

            var rect = new Rect(10, 170, w, h);
            HudInputBlocker.Register(rect);
            DrawPanel(rect);
            DrawRect(new Rect(rect.x, rect.y, rect.width, 4), room.Discovered ? def.FloorColor * 1.6f : new Color(0.3f, 0.3f, 0.3f));

            GUI.Label(new Rect(rect.x + 12, rect.y + 10, w - 60, 24), $"<size=18><b>{cardTitle}</b></size>", title);
            if (GUI.Button(new Rect(rect.xMax - 34, rect.y + 10, 24, 22), "X")) presenter.CloseRoomCard();

            float y = rect.y + 40;
            GUI.Label(new Rect(rect.x + 12, y, w - 24, textH), lines.ToString(), small);
            y += textH;

            if (!moveButtons) return; // passagem: sem "Quem vai?"

            GUI.Label(new Rect(rect.x + 12, y, w - 24, 20), "<b>Quem vai?</b>", small);
            y += 22;
            foreach (var actor in run.Actors)
            {
                if (!actor.Alive) continue;
                int cost = run.MoveCost(actor, index);
                string info;
                bool can = run.CanMove(actor, index);
                if (actor.RoomIndex == index) info = "<color=#a0a0a0>já está aqui</color>";
                else if (actor.IsLocked) info = "<color=#ff6666>em pânico — travado(a)</color>";
                else if (room.Sealed) info = "<color=#ff9a6a>trancada</color>";
                else if (cost < 0) info = "<color=#ff6666>sem caminho</color>";
                else if (cost > run.ActionsLeft) info = $"<color=#ff6666>{Actions(cost)} — não dá</color>";
                else if (cost == 0) info = "<color=#8fdc8f>grátis</color>";
                else info = $"<color=#ffd966>{Actions(cost)}</color> <color=#a0a0a0>(explora)</color>";
                if (IsVillainHere(run, index) && actor.RoomIndex != index)
                    info += IsGhost(run) ? $"  <color={GhostPurple}>· assombrada</color>" : $"  <color={VillainRed}>· vilão aqui!</color>";

                var b = new Rect(rect.x + 12, y, w - 24, 26);
                GUI.enabled = can;
                if (GUI.Button(b, GUIContent.none)) presenter.SendActor(actor, index);
                GUI.enabled = true;
                string sel = actor == presenter.Selected ? "▶ " : "";
                GUI.Label(new Rect(b.x + 8, b.y + 4, b.width - 16, 20), $"{sel}<b>{actor.Def.DisplayName}</b>  —  {info}", small);
                y += 30;
            }
        }

        /// <summary>
        /// Protótipo 3: "Se ficar aqui por uma cena: …" — em linguagem de filme, sem números escondidos
        /// (só os marcadores de cenas dos plots). Plots daqui, cenas de dupla ativas, medo do lugar, vilão perto.
        /// </summary>
        private void AppendStayPreview(FilmRun run, int index, StringBuilder lines)
        {
            var room = run.Rooms[index];
            var here = run.ActorsIn(index);
            var items = new List<string>();

            foreach (var p in run.PlotsFor(index))
            {
                int req = run.RequiredScenes(p, index);
                bool ok = run.PlotConditionMet(p, index);
                string state = ok ? "<color=#8fdc8f>(condição cumprida: grave cenas!)</color>" : $"<color=#a0a0a0>(precisa: {RoleNames(p.Def)})</color>";
                items.Add($"<color=#ffd966>◆ Plot <b>{p.Def.DisplayName}</b> {Pips(p.Progress, req)}</color> — {p.Def.Description} {state}");
            }
            foreach (var c in run.ActiveCombosIn(index))
                items.Add($"<color=#ff9ad5>♥ {c.Def.DisplayName}</color> — {c.Def.Description}");
            if (room.Def.PavorPerScene > 0) items.Add("<color=#b0b0b0>Lugar escuro e perigoso: o medo sobe a cada cena.</color>");
            if (here.Count == 1) items.Add($"<color=#b0b0b0>{here[0].Def.DisplayName} está sozinho(a): sente medo (e o vilão gosta disso).</color>");
            if (IsGhostHere(run, index)) items.Add($"<color={GhostPurple}>A tensão do fantasma cresce (mais rápido com gente aqui). No máximo: GRANDE SUSTO em quem estiver aqui — muita audiência, muito medo.</color>");
            if (run.IsNearVillain(index)) items.Add($"<color={VillainRed}>Perto do vilão: rende mais audiência, mas assusta.</color>");

            if (items.Count == 0 && here.Count == 0) return;
            lines.AppendLine("<b>Se ficar aqui por uma cena…</b>");
            if (items.Count == 0) lines.AppendLine("<color=#a0a0a0>Nada de especial: só o tempo passa.</color>");
            foreach (var it in items) lines.AppendLine($"• {it}");
            lines.AppendLine();
        }

        /// <summary>O card mostra os botões "Quem vai?" só onde atores podem parar (corredor gerado = passagem).</summary>
        public static bool ShowsMoveButtons(RoomRunState room) => room != null && room.IsDestination;

        /// <summary>Nome sobre o mapa: segmentos de corredor da casa gerada ficam sem nome (só poluiriam).</summary>
        public static bool ShowsLabel(RoomRunState room) => room != null && !(room.Space != null && room.Kind == SpaceKind.Corridor);

        // ================================================================== Ator selecionado

        private void DrawSelectedPanel(FilmRun run)
        {
            var actor = presenter.Selected;
            var payoffs = actor != null ? run.AvailablePayoffs(actor) : new List<PayoffDef>();
            bool canTool = actor != null && run.CanUseTool(actor);
            bool canHold = actor != null && run.CanHoldDoor(actor);
            bool hasButtons = payoffs.Count > 0 || canTool || canHold;

            float w = 560f;
            float h = hasButtons ? 140f : 70f;
            float px = (Screen.width - w) * 0.5f;
            if (monitor != null && monitor.IsOpen) px = Mathf.Max(10f, Mathf.Min(px, monitor.GuiRect.x - w - 10f));
            var rect = new Rect(px, Screen.height - h - 32f, w, h);
            HudInputBlocker.Register(rect);
            DrawPanel(rect);

            float x = rect.x + 12;
            float y = rect.y + 8;

            if (actor == null)
            {
                GUI.Label(new Rect(x, y, w - 160, 54),
                    "<b>Clique numa sala</b> para ver o que se sabe dela e escolher quem vai.\n" +
                    "<color=#a0a0a0>Clique num ator (ou Tab) para ver o que ele pode fazer onde está.</color>", small);
            }
            else
            {
                var room = run.RoomOf(actor);
                string where = room == null ? "lá fora" : room.Def.DisplayName;
                string tools = actor.Tools.Count > 0 ? $"  <color=#9fd0ff><size=11>· {ToolList(actor.Tools)}</size></color>" : "";
                GUI.Label(new Rect(x, y, w - 160, 22), $"<b>{actor.Def.DisplayName}</b> — {where}{tools}", title);
                y += 26;

                if (!hasButtons)
                {
                    string hint = actor.IsLocked ? "<color=#ff6666>Em pânico: não consegue andar nesta cena.</color> Grave uma cena para ele(a) se recompor."
                        : !string.IsNullOrEmpty(actor.Def.PassiveText) ? $"<color=#c8b88a>Passivo:</color> {actor.Def.PassiveText}"
                        : room != null && room.Def.StagePayoffs.Count > 0
                        ? "Este palco ainda não está liberado neste ato (ou faltam cenas)."
                        : "Nada para fazer aqui. Clique numa sala para mover este ator (andar é grátis).";
                    GUI.Label(new Rect(x, y, w - 160, 40), $"<color=#a0a0a0>{hint}</color>", small);
                }

                float bx = x;
                foreach (var p in payoffs)
                {
                    var counted = run.CountElements(actor, p);
                    var sb = new StringBuilder($"<b>Dirigir {p.DisplayName}</b>  <color=#a0a0a0>({Actions(run.Content.Rules.directStepCost)})</color>\n");
                    if (counted.Count == 0) sb.Append("<color=#ff9a9a>nada preparado — vale pouco</color>");
                    else sb.Append("<size=10>Vai usar: </size>");
                    foreach (var c in counted) sb.Append($"<color=#{Hex(c.Element.Color)}>● {c.Element.DisplayName}</color>{(c.BoostedByVillain ? "★" : "")}  ");
                    if (p.KillsActor) sb.Append("\n<color=#ff6666>o ator sai do filme</color>");

                    var b = new Rect(bx, y, 250, 78);
                    if (GUI.Button(b, GUIContent.none)) presenter.DirectScene(p);
                    GUI.Label(new Rect(b.x + 8, b.y + 4, b.width - 16, b.height - 8), sb.ToString(), small);
                    bx += 258;
                }

                if (canHold)
                {
                    var b = new Rect(bx, y, 250, 78);
                    if (GUI.Button(b, GUIContent.none)) presenter.HoldDoor();
                    GUI.Label(new Rect(b.x + 8, b.y + 4, b.width - 16, b.height - 8),
                        "<b>Segurar a porta</b>  <color=#8fdc8f>(grátis, 1× por ato)</color>\nO vilão não entra aqui na próxima cena.", small);
                    bx += 258;
                }

                if (canTool && room != null)
                {
                    var b = new Rect(bx, y, 250, 78);
                    if (GUI.Button(b, GUIContent.none)) presenter.UseTool();
                    var holder = run.ToolHolderFor(actor, room.Def.Locked.requiredTool);
                    string lent = holder != null && holder != actor ? $"\n<color=#a0a0a0>(de {holder.Def.DisplayName}, que está junto)</color>" : "";
                    GUI.Label(new Rect(b.x + 8, b.y + 4, b.width - 16, b.height - 8),
                        $"<b>Usar {room.Def.Locked.requiredTool.DisplayName}</b>  <color=#a0a0a0>({Actions(run.Content.Rules.toolStepCost)})</color>\nAbrir o {room.Def.Locked.name}{lent}", small);
                }
            }

            if (GUI.Button(new Rect(rect.xMax - 136, rect.y + 8, 124, 24), "Encerrar ato"))
            {
                presenter.EndActEarly();
            }
            GUI.enabled = run.CanRecordScene;
            if (GUI.Button(new Rect(rect.xMax - 136, rect.y + 36, 124, 26),
                    new GUIContent("Gravar cena ▶", "Ninguém se mexe: a casa inteira conta 1 cena (plots avançam, combos pontuam, o vilão age).")))
            {
                presenter.RecordScene();
            }
            GUI.enabled = true;
        }

        private void DrawHoverHint()
        {
            if (presenter.HoveredRoom < 0 || presenter.CardRoom == presenter.HoveredRoom) return;
            Vector2 m = Event.current.mousePosition;
            var rect = new Rect(m.x + 16, m.y + 14, 170, 22);
            DrawPanel(rect);
            GUI.Label(new Rect(rect.x + 8, rect.y + 2, rect.width - 12, 20), "<size=11>Clique para ver a sala</size>", small);
        }

        // ================================================================== Cinema: faixas e monitor

        private void DrawLetterbox()
        {
            if (letterbox <= 0.001f) return;
            float h = Screen.height * letterboxHeight * Mathf.SmoothStep(0f, 1f, letterbox);
            DrawRect(new Rect(0, 0, Screen.width, h), Color.black);
            DrawRect(new Rect(0, Screen.height - h, Screen.width, h), Color.black);
        }

        /// <summary>
        /// Monitor do diretor: a planta da casa vista de cima, no canto inferior direito.
        /// Clicar nela funciona como clicar na casa (o RunPresenter usa o raio da câmera do monitor).
        /// </summary>
        private void DrawMonitor(FilmRun run)
        {
            if (monitor == null) return;

            if (!monitor.IsOpen)
            {
                // Logo abaixo do painel do elenco.
                var b = new Rect(Screen.width - 260, 10 + 30 + run.Actors.Count * CastRowHeight + 6, 250, 26);
                HudInputBlocker.Register(b);
                if (GUI.Button(b, "Monitor do diretor (M)")) monitor.SetOpen(true);
                monitor.GuiRect = Rect.zero;
                return;
            }

            float w = Mathf.Clamp(Screen.width * 0.3f, 260f, 480f);
            float h = w / monitor.Aspect;
            var frame = new Rect(Screen.width - w - 18, Screen.height - h - 58, w + 8, h + 30);
            HudInputBlocker.Register(frame);
            DrawPanel(frame);
            GUI.Label(new Rect(frame.x + 8, frame.y + 4, 200, 18), "MONITOR DO DIRETOR", header);
            if (GUI.Button(new Rect(frame.xMax - 26, frame.y + 3, 22, 18), "X")) monitor.SetOpen(false);

            var content = new Rect(frame.x + 4, frame.y + 26, w, h);
            monitor.GuiRect = content;
            if (monitor.Texture != null) GUI.DrawTexture(content, monitor.Texture, ScaleMode.StretchToFill, false);

            // Nomes das salas e dos atores sobre a planta.
            var tiny = new GUIStyle(label) { fontSize = 10 };
            for (int i = 0; i < run.Rooms.Count; i++)
            {
                var anchor = presenter.AnchorOf(i);
                if (anchor == null || !monitor.WorldToGui(anchor.transform.position, out Vector2 g)) continue;
                var room = run.Rooms[i];
                if (!ShowsLabel(room) && i != presenter.CardRoom) continue;
                string text = room.Discovered ? room.Def.DisplayName : "<color=#8a8a8a>?</color>";
                if (i == presenter.CardRoom) text = $"<color=#ffd966>[ {StripTags(text)} ]</color>";
                if (room.Elements.Count > 0) text += $"\n<color=#ffd966>● {room.Elements.Count}</color>";
                var r = new Rect(g.x - 60, g.y - 8, 120, 30);
                GUI.Label(Shadow(r), StripColors(text), tiny);
                GUI.Label(r, text, tiny);
            }
            foreach (var actor in run.Actors)
            {
                var view = presenter.ViewOf(actor);
                if (view == null || !actor.Alive || !monitor.WorldToGui(view.transform.position, out Vector2 g)) continue;
                string n = actor == presenter.Selected ? $"<color=#ffd966>▶{actor.Def.DisplayName}</color>" : actor.Def.DisplayName;
                var r = new Rect(g.x - 50, g.y + 4, 100, 16);
                GUI.Label(Shadow(r), StripColors(n), tiny);
                GUI.Label(r, n, tiny);
            }

            // Protótipo 3: plots ativos (marcador dourado com o progresso).
            for (int i = 0; i < run.Rooms.Count; i++)
            {
                var plotsHere = run.PlotsFor(i);
                if (plotsHere.Count == 0) continue;
                var anchor = presenter.AnchorOf(i);
                if (anchor == null || !monitor.WorldToGui(anchor.transform.position, out Vector2 pg)) continue;
                var p = plotsHere.OrderByDescending(x => x.Progress).First();
                string t = $"<color=#ffd966>◆ {Pips(p.Progress, run.RequiredScenes(p, i))}</color>";
                var r = new Rect(pg.x - 50, pg.y + 18, 100, 16);
                GUI.Label(Shadow(r), StripColors(t), tiny);
                GUI.Label(r, t, tiny);
            }

            // Vilão: posição (se revelada) e o anúncio do próximo espaço.
            var v = run.VillainNpc;
            if (v != null && v.InHouse && v.IsGhost)
            {
                var hauntA = presenter.AnchorOf(v.Space);
                if (hauntA != null && monitor.WorldToGui(hauntA.transform.position, out Vector2 hg))
                {
                    float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 3f);
                    DrawFrame(new Rect(hg.x - 30, hg.y - 30, 60, 60), new Color(0.65f, 0.5f, 1f, 0.5f + 0.5f * pulse * run.GhostTensionRatio), 2f);
                    DrawBar(new Rect(hg.x - 26, hg.y - 40, 52, 5), run.GhostTensionRatio, new Color(0.75f, 0.6f, 1f));
                    var r = new Rect(hg.x - 60, hg.y - 56, 120, 16);
                    string t = $"<color={GhostPurple}><b>FANTASMA</b></color>";
                    GUI.Label(Shadow(r), StripColors(t), tiny);
                    GUI.Label(r, t, tiny);
                }
                var nextA = v.NextSpace != v.Space ? presenter.AnchorOf(v.NextSpace) : null;
                if (nextA != null && monitor.WorldToGui(nextA.transform.position, out Vector2 ng2))
                {
                    var r = new Rect(ng2.x - 60, ng2.y + 30, 120, 16);
                    string t = $"<color={GhostPurple}>▼ próxima</color>";
                    GUI.Label(Shadow(r), StripColors(t), tiny);
                    GUI.Label(r, t, tiny);
                }
            }
            else if (v != null && v.InHouse)
            {
                var nextAnchor = v.IsTelegraphing ? presenter.AnchorOf(v.NextSpace) : null;
                if (nextAnchor != null && monitor.WorldToGui(nextAnchor.transform.position, out Vector2 ng))
                {
                    float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 6f);
                    var frameRect = new Rect(ng.x - 26, ng.y - 26, 52, 52);
                    DrawFrame(frameRect, new Color(1f, 0.2f, 0.15f, 0.5f + 0.5f * pulse), 2f);
                    var r = new Rect(ng.x - 60, ng.y + 14, 120, 16);
                    string t = $"<color={VillainRed}>▼ passos</color>";
                    GUI.Label(Shadow(r), StripColors(t), tiny);
                    GUI.Label(r, t, tiny);
                }
                var vv = presenter.VillainViewObject;
                if (presenter.RevealVillain && vv != null && monitor.WorldToGui(vv.transform.position, out Vector2 vg))
                {
                    DrawRect(new Rect(vg.x - 6, vg.y - 6, 12, 12), new Color(0.05f, 0.02f, 0.02f, 1f));
                    DrawRect(new Rect(vg.x - 4, vg.y - 4, 8, 8), new Color(1f, 0.2f, 0.15f, 1f));
                    var r = new Rect(vg.x - 50, vg.y - 22, 100, 16);
                    string t = $"<color={VillainRed}><b>VILÃO</b></color>";
                    GUI.Label(Shadow(r), StripColors(t), tiny);
                    GUI.Label(r, t, tiny);
                }
            }
        }

        private void DrawFrame(Rect r, Color c, float t)
        {
            DrawRect(new Rect(r.x, r.y, r.width, t), c);
            DrawRect(new Rect(r.x, r.yMax - t, r.width, t), c);
            DrawRect(new Rect(r.x, r.y, t, r.height), c);
            DrawRect(new Rect(r.xMax - t, r.y, t, r.height), c);
        }

        private static string Actions(int n) => n == 0 ? "grátis" : n == 1 ? "1 cena" : $"{n} cenas";

        private static string StripTags(string s) => System.Text.RegularExpressions.Regex.Replace(s, "<.*?>", "");

        // ================================================================== Momentos

        private void DrawResult()
        {
            var lines = presenter.ResultLines;
            var sb = new StringBuilder($"<size=16><b>{presenter.ResultTitle}</b></size>\n\n");
            foreach (var l in lines) sb.AppendLine(l);
            float w = 440f;
            float h = small.CalcHeight(new GUIContent(sb.ToString()), w - 24) + 40;
            var rect = new Rect(Screen.width * 0.5f - w * 0.5f, 170, w, h);
            HudInputBlocker.Register(rect);
            DrawPanel(rect);
            GUI.Label(new Rect(rect.x + 12, rect.y + 10, rect.width - 24, rect.height - 20), sb.ToString(), small);
            GUI.Label(new Rect(rect.x, rect.yMax - 20, rect.width - 10, 18), "<size=10><color=#a0a0a0>clique para continuar</color></size>",
                new GUIStyle(small) { alignment = TextAnchor.MiddleRight });
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) presenter.SkipResult();
        }

        private void DrawActBreak()
        {
            var act = presenter.LastAct;
            if (act == null) return;

            var rect = new Rect(Screen.width * 0.5f - 220, Screen.height * 0.5f - 90, 440, 180);
            HudInputBlocker.Register(rect);
            DrawPanel(rect);
            string verdict = act.Passed ? "<color=#8fdc8f>meta atingida</color>" : "<color=#ff6666>meta NÃO atingida</color>";
            GUI.Label(new Rect(rect.x, rect.y + 15, rect.width, 40), $"Fim do {act.ActName}", big);
            GUI.Label(new Rect(rect.x + 20, rect.y + 65, rect.width - 40, 40), $"{act.TotalScore} / {act.Goal} — {verdict}",
                new GUIStyle(title) { alignment = TextAnchor.MiddleCenter });
            if (presenter.Run.AwaitingArtefatoChoice)
                GUI.Label(new Rect(rect.x + 20, rect.y + 96, rect.width - 40, 20), "<color=#f2cc73>A seguir: escolha 1 de 3 artefatos para o filme.</color>",
                    new GUIStyle(small) { alignment = TextAnchor.MiddleCenter });
            if (GUI.Button(new Rect(rect.x + rect.width * 0.5f - 70, rect.y + 120, 140, 36), "Continuar"))
            {
                presenter.ContinueAfterAct();
            }
        }

        private void DrawVillainChoice(FilmRun run)
        {
            List<VillainDef> offer = run.VillainOffer();
            float w = 300f * offer.Count + 20f;
            var rect = new Rect((Screen.width - w) * 0.5f, Screen.height * 0.5f - 150, w, 260);
            HudInputBlocker.Register(rect);
            DrawPanel(rect);
            GUI.Label(new Rect(rect.x, rect.y + 10, rect.width, 36), "Quem é o vilão deste filme?", big);
            GUI.Label(new Rect(rect.x + 10, rect.y + 48, rect.width - 20, 20),
                "Os elementos do subgênero dele valem em dobro. A partir do próximo ato ele entra na casa. Perto dele rende mais — e é perigoso.",
                new GUIStyle(small) { alignment = TextAnchor.MiddleCenter });

            for (int i = 0; i < offer.Count; i++)
            {
                var v = offer[i];
                var b = new Rect(rect.x + 10 + i * 300, rect.y + 80, 290, 160);
                if (GUI.Button(b, GUIContent.none)) presenter.ChooseVillain(v);
                DrawRect(new Rect(b.x, b.y, b.width, 5), v.Color);
                string family = v.IsGhost ? $"<color={GhostPurple}>Fantasma: assombra uma sala e dá o Grande Susto</color>"
                    : $"<color={VillainRed}>Slasher: anda e caça quem está sozinho</color>";
                GUI.Label(new Rect(b.x + 10, b.y + 12, b.width - 20, b.height - 20),
                    $"<size=18><b>{v.DisplayName}</b></size>\n<i>{v.Subgenre?.DisplayName}</i> · {family}\n\n{v.Description}", small);
            }
        }

        /// <summary>Protótipo 3: "escolha 1 de 3" artefatos entre atos (grátis, estilo Balatro).</summary>
        private void DrawArtefatoChoice(FilmRun run)
        {
            var offer = run.ArtefatoOffer;
            float w = 260f * Mathf.Max(1, offer.Count) + 20f;
            var rect = new Rect((Screen.width - w) * 0.5f, Screen.height * 0.5f - 150, w, 290);
            HudInputBlocker.Register(rect);
            DrawPanel(rect);
            GUI.Label(new Rect(rect.x, rect.y + 10, rect.width, 36), "Escolha 1 artefato", big);
            GUI.Label(new Rect(rect.x + 10, rect.y + 48, rect.width - 20, 20),
                "Artefatos mudam as regras do filme até o fim. Grátis: escolha o que combina com a sua build.",
                new GUIStyle(small) { alignment = TextAnchor.MiddleCenter });

            for (int i = 0; i < offer.Count; i++)
            {
                var a = offer[i];
                var b = new Rect(rect.x + 10 + i * 260, rect.y + 80, 250, 150);
                if (GUI.Button(b, GUIContent.none)) presenter.ChooseArtefato(a);
                DrawRect(new Rect(b.x, b.y, b.width, 5), a.Color);
                GUI.Label(new Rect(b.x + 10, b.y + 14, b.width - 20, b.height - 20),
                    $"<size=17><b>{a.DisplayName}</b></size>\n\n{a.Description}", small);
            }
            if (GUI.Button(new Rect(rect.x + rect.width * 0.5f - 60, rect.yMax - 46, 120, 30), "Pular")) presenter.ChooseArtefato(null);
        }

        /// <summary>
        /// Protótipo 3: painel ROTEIRO (direita, abaixo do Elenco): plots com marcadores de cenas, cenas de dupla
        /// ativas agora e artefatos do filme.
        /// </summary>
        private void DrawScript(FilmRun run)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<color=#d9c27a><b>ROTEIRO</b></color>");
            int shown = 0;
            foreach (var p in run.Plots)
            {
                if (p.Status == PlotStatus.Failed) continue;
                if (p.Status == PlotStatus.Completed)
                {
                    sb.AppendLine($"<color=#8fdc8f>✓ {p.Def.DisplayName}</color>");
                    shown++;
                    continue;
                }
                int room = run.PlotDisplayRoom(p);
                int req = run.RequiredScenes(p, room);
                string where = room >= 0 && run.Rooms[room].Discovered ? run.Rooms[room].Def.DisplayName : "?";
                sb.AppendLine($"<color=#ffd966>◆ <b>{p.Def.DisplayName}</b> {Pips(p.Progress, req)}</color> <size=10><color=#a0a0a0>({where})</color></size>");
                sb.AppendLine($"<size=10><color=#c0c0c0>   {p.Def.Description}</color></size>");
                shown++;
            }
            if (shown == 0) sb.AppendLine("<size=11><color=#808080>Nenhum plot ainda: explore salas.</color></size>");

            var combos = run.AllActiveCombos();
            sb.AppendLine("<color=#d9c27a><b>CENAS DE DUPLA</b></color>");
            if (combos.Count == 0) sb.AppendLine("<size=11><color=#808080>Ninguém junto numa sala.</color></size>");
            foreach (var c in combos) sb.AppendLine($"<color=#ff9ad5>♥ {c.Def.DisplayName}</color> <size=10><color=#a0a0a0>({run.Rooms[c.Space].Def.DisplayName})</color></size>");

            sb.AppendLine("<color=#d9c27a><b>ARTEFATOS</b></color>");
            if (run.OwnedArtefatos.Count == 0) sb.AppendLine("<size=11><color=#808080>Nenhum (escolha entre atos).</color></size>");
            foreach (var a in run.OwnedArtefatos) sb.AppendLine($"<color=#{Hex(a.Color)}>■ {a.DisplayName}</color> <size=10><color=#a0a0a0>{a.Description}</color></size>");

            float w = 250f;
            float y = 10 + 30 + run.Actors.Count * CastRowHeight + 6 + (monitor != null && !monitor.IsOpen ? 32 : 0);
            float h = small.CalcHeight(new GUIContent(sb.ToString()), w - 20) + 14;
            // Monitor aberto embaixo à direita: o roteiro cabe no espaço que sobra; se não couber, vira uma linha-resumo.
            float free = monitor != null && monitor.IsOpen && monitor.GuiRect.height > 0 ? monitor.GuiRect.y - 34 - y : Screen.height - 40 - y;
            if (h > free)
            {
                int active = run.Plots.Count(p => p.IsActive);
                sb.Length = 0;
                sb.Append($"<color=#d9c27a><b>ROTEIRO</b></color> <size=11>◆ {active} plot(s) · ♥ {combos.Count} dupla(s) · ■ {run.OwnedArtefatos.Count} artefato(s)</size>");
                h = Mathf.Max(24f, Mathf.Min(h, free));
                if (free < 24f) return;
            }
            var rect = new Rect(Screen.width - 260, y, w, h);
            HudInputBlocker.Register(rect);
            DrawPanel(rect);
            GUI.Label(new Rect(rect.x + 10, rect.y + 6, w - 20, h - 8), sb.ToString(), small);
        }

        private void DrawEnd(FilmRun run)
        {
            var rect = new Rect(Screen.width * 0.5f - 230, Screen.height * 0.5f - 110, 460, 220);
            HudInputBlocker.Register(rect);
            DrawPanel(rect);
            bool won = run.Status == RunStatus.Won;
            GUI.Label(new Rect(rect.x, rect.y + 15, rect.width, 40), won ? "Filme aprovado!" : "O filme fracassou", big);

            int alive = 0;
            foreach (var a in run.Actors) if (a.Alive) alive++;
            GUI.Label(new Rect(rect.x + 20, rect.y + 70, rect.width - 40, 70),
                $"Pontuação final: <b>{run.TotalScore}</b>\nSobreviventes: {alive}/{run.Actors.Count} · Vilão: {(run.Villain != null ? run.Villain.DisplayName : "—")}\nSeed: {run.Rng.Seed}", title);

            if (GUI.Button(new Rect(rect.x + 40, rect.y + 160, 170, 36), "Jogar de novo")) presenter.Restart();
            if (GUI.Button(new Rect(rect.x + rect.width - 210, rect.y + 160, 170, 36), "Copiar log da run"))
            {
                GUIUtility.systemCopyBuffer = run.Log.ToJson();
            }
        }

        // ================================================================== Utilidades

        private static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

        private static Rect Shadow(Rect r) => new Rect(r.x + 1, r.y + 1, r.width, r.height);

        /// <summary>Versão preta do texto (para sombra legível sobre o 3D).</summary>
        private static string StripColors(string s)
        {
            var sb = new StringBuilder("<color=#000000>");
            int i = 0;
            while (i < s.Length)
            {
                if (s[i] == '<')
                {
                    int end = s.IndexOf('>', i);
                    if (end < 0) break;
                    string tag = s.Substring(i, end - i + 1);
                    if (!tag.StartsWith("<color") && !tag.StartsWith("</color")) sb.Append(tag);
                    i = end + 1;
                }
                else
                {
                    sb.Append(s[i]);
                    i++;
                }
            }
            sb.Append("</color>");
            return sb.ToString();
        }

        private void DrawPanel(Rect r) => DrawRect(r, Panel);

        private void DrawBar(Rect r, float ratio, Color fill)
        {
            DrawRect(r, new Color(1f, 1f, 1f, 0.12f));
            DrawRect(new Rect(r.x, r.y, r.width * Mathf.Clamp01(ratio), r.height), fill);
        }

        private void DrawRect(Rect r, Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, white);
            GUI.color = old;
        }
    }
}
