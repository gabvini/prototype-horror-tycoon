using System.Collections.Generic;
using System.Text;
using HorrorTycoon.Actors;
using HorrorTycoon.Run;
using UnityEngine;
using UnityEngine.UIElements;

namespace HorrorTycoon.UI
{
    /// <summary>Ações da HUD que os painéis pedem ao HudRoot (menu ≡, modais, opções).</summary>
    public interface IHudActions
    {
        void OpenReportLog();
        void OpenHelp();
        void OpenScriptList();
        void ConfirmEndAct();
        void ToggleLegacy();
        void SettingsChanged();
    }

    // ================================================================================== Z1 Claquete

    /// <summary>Claquete (sup. esq.): ato, cena n/m com pips, audiência × meta com barra. SEMPRE.</summary>
    public class ClaquetePanel
    {
        public readonly VisualElement Root;
        public VisualElement ScoreAnchor => scorePill;

        private readonly Label act, sceneNum, left, score, goal, compact;
        private readonly VisualElement pips, barFill, goalMark, check, scorePill, full, bar;
        private int lastTarget = int.MinValue;
        private float shown;
        private bool goalFlashed;
        private int goalActIndex = -1;

        public ClaquetePanel(VisualElement parent, HudContext c)
        {
            Root = Ui.El("claquete paper fadeable", parent, "Z1-Claquete");
            Root.Add(new StripeElement { name = "stripes" });
            Root[0].AddToClassList("clap-stripes");
            var body = Ui.El("clap-body", Root);

            var row1 = Ui.El("row clap-row1", body);
            act = Ui.Lbl("ATO I", "t-marker clap-act", row1);
            compact = Ui.Lbl("", "t-display clap-compact", row1);
            Ui.El("grow", row1);
            Ui.Lbl("CENA", "t-label clap-cap", row1);
            sceneNum = Ui.Lbl("1/8", "t-num clap-scene", row1);

            full = Ui.El("clap-full", body);
            var row2 = Ui.El("row clap-row2", full);
            pips = Ui.El("row clap-pips", row2);
            Ui.El("grow", row2);
            left = Ui.Lbl("", "t-small clap-left", row2);

            var row3 = Ui.El("row clap-row3", full);
            Ui.Lbl("AUDIÊNCIA", "t-label clap-cap", row3);
            Ui.El("grow", row3);
            scorePill = Ui.El("row clap-score-pill", row3);
            Ui.Icon("ticket", "ic-24 tint-audiencia", scorePill);
            score = Ui.Lbl("0", "t-display clap-score", scorePill);
            goal = Ui.Lbl("/ 0", "t-display clap-goal", row3);
            check = Ui.Icon("check", "ic-24 tint-ok clap-check", row3);

            bar = Ui.El("clap-bar", full);
            barFill = Ui.El("clap-bar-fill", bar);
            goalMark = Ui.El("clap-bar-goal", bar);

            c.Tooltip.Attach(Root, () =>
            {
                var run = c.Run;
                if (run == null) return "";
                return $"<b>{run.Content.FilmFormat.DisplayName}</b> · {run.CurrentAct.name} de {run.ActCount}\n" +
                       $"Restam <b>{run.ScenesLeft}</b> de {run.ScenesThisAct} cenas neste ato.\n" +
                       $"<color={HudColors.Texto2}>Gastam cena: explorar sala nova, gravar, dirigir, usar ferramenta. Andar é grátis.</color>\n" +
                       $"Audiência: {run.TotalScore} · meta do ato: {run.CurrentAct.goal}";
            });
        }

        /// <summary>targetScore = para onde o contador anda (durante o relatório, sobe beat a beat).</summary>
        public void Update(HudContext c, int targetScore)
        {
            var run = c.Run;
            int total = Mathf.Max(1, run.ScenesThisAct);
            int current = Mathf.Clamp(c.SceneShown, 1, total);
            act.text = "ATO " + HudText.Roman(run.ActIndex);
            sceneNum.text = $"{current}/{total}";
            compact.text = c.FilmMode ? $"· CENA {current}/{total}" : "";
            Pips.Set(pips, run.ScenesLeft, total, "pip");
            left.text = run.ScenesLeft == 1 ? "última cena" : $"restam {run.ScenesLeft}";
            bool last = run.ScenesLeft <= 1 && run.Status == RunStatus.Playing;
            pips.EnableInClassList("is-last", last);
            if (last) pips.style.opacity = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * Mathf.PI * 1.5f));
            else pips.style.opacity = 1f;
            Root.EnableInClassList("is-film", c.FilmMode);

            // Contador: rola em 0,4 s até o alvo, com um "pop".
            if (lastTarget == int.MinValue) shown = targetScore;
            else if (targetScore != lastTarget)
            {
                float from = shown;
                float to = targetScore;
                c.Tweens.Add(0.4f, k => shown = Mathf.Lerp(from, to, HudTweens.EaseOut(k)), () => shown = to, 0f, this);
                if (to > from)
                    c.Tweens.Add(0.25f, k => scorePill.style.scale = new Scale(Vector2.one * Mathf.Lerp(1.18f, 1f, k)), null, 0f, scorePill);
            }
            lastTarget = targetScore;
            c.DisplayedScore = shown;
            int shownInt = Mathf.RoundToInt(shown);
            score.text = shownInt.ToString();

            int g = run.CurrentAct.goal;
            goal.text = "/ " + g;
            if (goalActIndex != run.ActIndex)
            {
                goalActIndex = run.ActIndex;
                goalFlashed = false;
            }
            bool reached = g > 0 && shownInt >= g;
            Ui.Display(check, reached);
            float max = g > 0 ? g / 0.8f : Mathf.Max(1, shownInt);
            barFill.style.width = Length.Percent(Mathf.Clamp01(shown / max) * 100f);
            goalMark.style.left = Length.Percent(80f);
            barFill.EnableInClassList("is-goal", reached);
            if (reached && !goalFlashed)
            {
                goalFlashed = true;
                c.Tweens.Add(0.6f, k => bar.style.scale = new Scale(new Vector2(1f, 1f + 0.6f * Mathf.Sin(k * Mathf.PI))), null, 0f, bar);
            }
        }
    }

    // ================================================================================== Z2 Chip do vilão

    /// <summary>Chip do vilão (sup. centro): família + nome + intenção em 1 linha. Ato 1: "???".</summary>
    public class VillainChip
    {
        public readonly VisualElement Root;
        private readonly VisualElement icon, meter, meterFill;
        private readonly Label name, intent, meterWord;
        private string sig;

        public VillainChip(VisualElement parent, HudContext c)
        {
            Root = Ui.El("villain-chip panel row fadeable", parent, "Z2-Vilao");
            icon = Ui.Icon("knife", "villain-icon", Root);
            var col = Ui.El("col villain-text", Root);
            name = Ui.Lbl("", "t-btn villain-name", col);
            intent = Ui.Lbl("", "t-small villain-intent", col);
            meter = Ui.El("col villain-meter", Root);
            var track = Ui.El("meter-track", meter);
            meterFill = Ui.El("meter-fill", track);
            meterWord = Ui.Lbl("", "t-small villain-meter-word", meter);

            Ui.OnClick(Root, () =>
            {
                var v = c.Run?.VillainNpc;
                if (v == null || !v.InHouse || c.Phase != RunPresenter.Phase.Idle) return;
                int room = v.IsGhost ? v.Space : (v.IsTelegraphing ? v.NextSpace : v.Space);
                if (room >= 0) c.Presenter.OpenRoomCard(room);
            });
            c.Tooltip.Attach(Root, () =>
            {
                var run = c.Run;
                if (run?.Villain == null) return "O vilão é escolhido no fim do Ato 1.\nEle entra na casa no começo do Ato 2.";
                string how = run.Villain.IsGhost
                    ? "<b>Fantasma</b>: assombra uma sala. A tensão sobe a cada cena (mais rápido com gente lá). No limite: GRANDE SUSTO em quem estiver na sala."
                    : "<b>Slasher</b>: anda 1 sala por cena até quem está sozinho. As pegadas mostram para onde vai. Pega quem está só; em grupo, recua.";
                return how + $"\n<color={HudColors.Texto2}>Clique: a câmera vai até a sala.</color>";
            });
        }

        public void Update(HudContext c)
        {
            var run = c.Run;
            var v = run.VillainNpc;
            bool known = run.Villain != null;
            bool inHouse = v != null && v.InHouse;
            bool ghost = known && run.Villain.IsGhost;
            string intentText = inHouse ? HudModel.VillainIntent(run, c.Presenter.RevealVillain) : known ? "entra na casa no próximo ato" : "revelado no fim do ato";
            float tension = ghost ? run.GhostTensionRatio : 0f;
            string s = $"{known}|{inHouse}|{ghost}|{intentText}|{(v != null && v.IsStunned)}|{Mathf.RoundToInt(tension * 20)}";
            if (s == sig) return;
            sig = s;

            Root.EnableInClassList("is-unknown", !known);
            Root.EnableInClassList("is-ghost", ghost);
            Root.EnableInClassList("is-slasher", known && !ghost);
            Root.EnableInClassList("is-stunned", v != null && v.IsStunned);
            name.text = known ? run.Villain.DisplayName.ToUpperInvariant() : "VILÃO ???";
            intent.text = intentText;
            Ui.SetIcon(icon, !known ? "help" : v != null && v.IsStunned ? "zzz" : ghost ? "ghost" : "knife");
            Ui.SetTint(icon, !known || (v != null && v.IsStunned) ? "dim" : ghost ? "fantasma" : "perigo");
            Ui.Display(meter, ghost && inHouse);
            meterFill.style.width = Length.Percent(tension * 100f);
            meterFill.EnableInClassList("is-max", tension >= 0.8f);
            meterWord.text = HudText.TensionWord(tension);
        }

        /// <summary>No modo filme o chip só fica se o vilão vai agir na próxima cena.</summary>
        public bool ActsNextScene(HudContext c)
        {
            var v = c.Run.VillainNpc;
            if (v == null || !v.InHouse) return false;
            return v.IsGhost ? c.Run.GhostTensionRatio >= 0.8f : v.IsTelegraphing;
        }
    }

    // ================================================================================== Z2b Toasts

    /// <summary>Toasts (sob o chip do vilão): eventos que não exigem decisão. 2,5 s, máx. 3; hover pausa.</summary>
    public class ToastStack
    {
        public readonly VisualElement Root;
        private class Item
        {
            public VisualElement El;
            public float Until;
            public bool Hover;
        }
        private readonly List<Item> items = new List<Item>();
        public float Lifetime = 2.5f;

        public ToastStack(VisualElement parent)
        {
            Root = Ui.El("toasts", parent, "Z2b-Toasts");
            Root.pickingMode = PickingMode.Ignore;
        }

        public void Push(HudContext c, string icon, string kind, string text)
        {
            var t = Ui.El("toast panel row toast-" + kind, Root);
            Ui.Icon(icon, "ic-24 tint-" + kind, t);
            Ui.Lbl(text, "t-body toast-text", t);
            var item = new Item { El = t, Until = Time.unscaledTime + Lifetime };
            t.RegisterCallback<PointerEnterEvent>(_ => item.Hover = true);
            t.RegisterCallback<PointerLeaveEvent>(_ => { item.Hover = false; item.Until = Mathf.Max(item.Until, Time.unscaledTime + 1f); });
            items.Add(item);
            t.style.opacity = 0f;
            c.Tweens.Add(0.2f, k =>
            {
                t.style.opacity = k;
                t.style.translate = new Translate(0, Mathf.Lerp(-12f, 0f, HudTweens.EaseOut(k)));
            });
            while (items.Count > 3) Remove(items[0]);
        }

        private void Remove(Item i)
        {
            items.Remove(i);
            i.El.RemoveFromHierarchy();
        }

        public void Update()
        {
            for (int i = items.Count - 1; i >= 0; i--)
            {
                var it = items[i];
                if (it.Hover) it.Until = Mathf.Max(it.Until, Time.unscaledTime + 0.5f);
                if (Time.unscaledTime > it.Until) Remove(it);
                else if (Time.unscaledTime > it.Until - 0.3f) it.El.style.opacity = (it.Until - Time.unscaledTime) / 0.3f;
            }
        }
    }

    // ================================================================================== Z3 Roteiro

    /// <summary>Roteiro (sup. dir.): ≤ 3 plots, 1 linha de duplas, artefatos. Recolhe para 1 linha com o monitor.</summary>
    public class ScriptTracker
    {
        public readonly VisualElement Root;
        private readonly VisualElement body, plotsBox, duoRow, artRow, collapsedRow;
        private readonly Label duoText, nPlots, nDuos, nArts, toggle;
        private string sig;
        private bool userCollapsed;
        private readonly Dictionary<string, float> doneAt = new Dictionary<string, float>();
        private readonly Dictionary<string, int> brokenCount = new Dictionary<string, int>();
        private readonly Dictionary<string, float> brokenAt = new Dictionary<string, float>();
        private bool firstFrame = true;

        public ScriptTracker(VisualElement parent, HudContext c, IHudActions actions)
        {
            Root = Ui.El("script paper fadeable", parent, "Z3-Roteiro");
            var header = Ui.El("row script-header", Root);
            Ui.Lbl("ROTEIRO", "t-label script-title", header);
            collapsedRow = Ui.El("row script-counts", header);
            Ui.Icon("diamond", "ic-16 tint-plot-papel", collapsedRow);
            nPlots = Ui.Lbl("0", "t-bold t-small", collapsedRow);
            Ui.Icon("heart", "ic-16 tint-dupla-papel", collapsedRow);
            nDuos = Ui.Lbl("0", "t-bold t-small", collapsedRow);
            Ui.Icon("artefact", "ic-16 tint-tinta", collapsedRow);
            nArts = Ui.Lbl("0", "t-bold t-small", collapsedRow);
            Ui.El("grow", header);
            Ui.OnClick(Ui.Lbl("ver tudo", "t-small script-link", header), actions.OpenScriptList);
            toggle = Ui.OnClick(Ui.Lbl("–", "t-display script-toggle", header), () =>
            {
                userCollapsed = !userCollapsed;
                sig = null;
            });

            body = Ui.El("script-body", Root);
            plotsBox = Ui.El("col", body);
            duoRow = Ui.El("row script-duo", body);
            Ui.Icon("heart", "ic-20 tint-dupla-papel", duoRow);
            duoText = Ui.Lbl("", "t-small script-duo-text", duoRow);
            c.Tooltip.Attach(duoRow, () => duoRow.userData as string);
            artRow = Ui.El("row script-arts", body);
        }

        public void Update(HudContext c)
        {
            var run = c.Run;
            float now = Time.unscaledTime;

            // Plots recém-cumpridos ficam 1,5 s riscados; quebrados tremem 1 s.
            foreach (var p in run.Plots)
            {
                string key = p.Def.DisplayName;
                if (p.Status == PlotStatus.Completed && !doneAt.ContainsKey(key)) doneAt[key] = firstFrame ? -99f : now;
                brokenCount.TryGetValue(key, out int prev);
                if (p.TimesBroken > prev && !firstFrame) brokenAt[key] = now;
                brokenCount[key] = p.TimesBroken;
            }
            firstFrame = false;

            bool collapsed = userCollapsed || c.MonitorVisible;
            var rows = HudModel.PlotRows(run, true);
            var recentDone = rows.FindAll(r => r.Completed && doneAt.TryGetValue(r.Name, out float t) && now - t < 1.5f);
            var top = HudModel.TopPlots(rows);
            top.InsertRange(0, recentDone);
            var combos = run.AllActiveCombos();
            int activePlots = rows.FindAll(r => !r.Completed).Count;

            var sb = new StringBuilder();
            sb.Append(collapsed).Append('|').Append(activePlots).Append('|').Append(combos.Count).Append('|').Append(run.OwnedArtefatos.Count).Append('|');
            foreach (var r in top)
            {
                bool broken = brokenAt.TryGetValue(r.Name, out float bt) && now - bt < 1f;
                sb.Append(r.Name).Append(r.Progress).Append(r.Required).Append(r.Ready).Append(r.Completed).Append(r.Room).Append(broken).Append(';');
            }
            foreach (var cb in combos) sb.Append(cb.Def.DisplayName).Append(cb.Space).Append(';');
            string s = sb.ToString();
            if (s == sig) return;
            sig = s;

            Root.EnableInClassList("is-collapsed", collapsed);
            Ui.Display(body, !collapsed);
            Ui.Display(collapsedRow, collapsed);
            toggle.text = collapsed ? "+" : "–";
            nPlots.text = activePlots.ToString();
            nDuos.text = combos.Count.ToString();
            nArts.text = run.OwnedArtefatos.Count.ToString();
            if (collapsed) return;

            plotsBox.Clear();
            if (top.Count == 0) Ui.Lbl("Nenhum plot ainda: explore salas.", "t-small script-empty", plotsBox);
            foreach (var r in top)
            {
                var row = Ui.El("row script-plot", plotsBox);
                Ui.Icon(r.Completed ? "check" : "diamond", "ic-20 " + (r.Completed ? "tint-ok-papel" : "tint-plot-papel"), row);
                var name = Ui.Lbl(r.Completed ? $"<s>{HudText.Clip(r.Name, 22)}</s>" : HudText.Clip(r.Name, 22), "t-bold script-plot-name", row);
                if (r.Completed) name.AddToClassList("is-done");
                var pipRow = Ui.El("row plot-pips", row);
                Pips.Set(pipRow, r.Progress, r.Required, "ppip");
                Ui.El("grow", row);
                if (r.Ready && !r.Completed)
                {
                    var ready = Ui.El("row chip chip-ok", row);
                    Ui.Lbl("pronto", "t-label", ready);
                }
                else Ui.Lbl(HudText.Clip(r.Room, 12), "t-small script-room", row);
                if (brokenAt.TryGetValue(r.Name, out float bt) && now - bt < 1f)
                {
                    row.AddToClassList("is-broken");
                    c.Tweens.Add(0.5f, k => row.style.translate = new Translate(Mathf.Sin(k * 40f) * 6f * (1f - k), 0));
                }
                string tip = r.Tooltip;
                c.Tooltip.Attach(row, tip);
            }

            Ui.Display(duoRow, combos.Count > 0);
            if (combos.Count > 0)
            {
                var parts = new List<string>();
                foreach (var cb in combos) parts.Add($"{cb.Def.DisplayName} · {run.Rooms[cb.Space].Def.DisplayName}");
                duoText.text = string.Join("   ", parts);
                string tip = "";
                foreach (var cb in combos) tip += $"<b>{cb.Def.DisplayName}</b> ({run.Rooms[cb.Space].Def.DisplayName}): {cb.Def.Description}\n";
                duoRow.userData = tip.TrimEnd();
            }

            artRow.Clear();
            Ui.Display(artRow, run.OwnedArtefatos.Count > 0);
            int n = 0;
            foreach (var a in run.OwnedArtefatos)
            {
                if (n++ >= 8) break;
                var slot = Ui.El("art-slot", artRow);
                var ic = Ui.Icon("artefact", "ic-24", slot);
                ic.style.unityBackgroundImageTintColor = a.Color;
                c.Tooltip.Attach(slot, $"<b>{a.DisplayName}</b>\n{a.Description}");
            }
        }
    }

    // ================================================================================== Z4 Folha de elenco

    /// <summary>Elenco (inf. esq.): retrato + anel de pavor + até 3 ícones de estado. Hover = detalhes.</summary>
    public class CastStrip
    {
        public readonly VisualElement Root;

        private class Card
        {
            public ActorRunState Actor;
            public VisualElement El, Portrait, Strike, Stamp, States, Drop;
            public RingElement Ring;
            public Label Initial, Name, StampSub, Out;
            public float Shown = -1f;
            public string Sig;
        }

        private readonly List<Card> cards = new List<Card>();

        public CastStrip(VisualElement parent)
        {
            Root = Ui.El("cast row", parent, "Z4-Elenco");
            Root.pickingMode = PickingMode.Ignore;
        }

        private void Build(HudContext c)
        {
            Root.Clear();
            cards.Clear();
            foreach (var actor in c.Run.Actors)
            {
                var card = new Card { Actor = actor };
                card.El = Ui.El("cast-card fadeable", Root);
                var pw = Ui.El("portrait-wrap", card.El);
                card.Ring = new RingElement();
                card.Ring.AddToClassList("pavor-ring");
                pw.Add(card.Ring);
                card.Portrait = Ui.El("portrait", pw);
                card.Portrait.style.backgroundColor = actor.Def.Color;
                card.Initial = Ui.Lbl(actor.Def.DisplayName.Substring(0, 1).ToUpperInvariant(), "t-display portrait-initial", card.Portrait);
                card.Strike = Ui.El("portrait-strike", pw);
                card.Drop = Ui.Icon("drop", "ic-24 tint-pavor portrait-drop", pw);
                card.Stamp = Ui.El("stamp", pw);
                Ui.Lbl("PÂNICO", "t-marker stamp-text", card.Stamp);
                card.StampSub = Ui.Lbl("", "t-label stamp-sub", card.Stamp);
                card.Name = Ui.Lbl(actor.Def.DisplayName, "t-bold cast-name", card.El);
                card.States = Ui.El("row cast-states", card.El);
                card.Out = Ui.Lbl("", "t-label cast-out", card.El);

                var a = actor;
                card.El.RegisterCallback<ClickEvent>(ev =>
                {
                    if (!a.Alive || c.Phase != RunPresenter.Phase.Idle) return;
                    c.Presenter.Select(a);
                    if (ev.clickCount >= 2 && c.Focus != null)
                    {
                        var view = c.Presenter.ViewOf(a);
                        if (view != null) c.Focus.Focus(view);
                    }
                });
                c.Tooltip.Attach(card.El, () => Tooltip(c, a));
                cards.Add(card);
            }
        }

        private static string Tooltip(HudContext c, ActorRunState a)
        {
            var run = c.Run;
            var rules = run.Content.Rules;
            var sb = new StringBuilder($"<b>{a.Def.DisplayName}</b>");
            if (!string.IsNullOrEmpty(a.Def.RoleTitle)) sb.Append($" · {a.Def.RoleTitle}");
            if (!string.IsNullOrEmpty(a.Def.PassiveText)) sb.Append($"\n<color={HudColors.Texto2}>{a.Def.PassiveText}</color>");
            if (!a.Alive)
            {
                sb.Append($"\n<color={HudColors.Perigo}>{(a.Exit == ExitReason.Fled ? "Fugiu do filme." : "Fora do filme.")}</color>");
                return sb.ToString();
            }
            sb.Append($"\nOnde: {(a.RoomIndex < 0 ? "lá fora" : run.Rooms[a.RoomIndex].Def.DisplayName)}");
            sb.Append($"\nPavor: {PavorWord((float)a.Pavor / rules.pavorLimit)}");
            sb.Append($"\nFerramentas ({a.Tools.Count}/{rules.maxToolsPerActor}): ");
            sb.Append(a.Tools.Count == 0 ? "mãos vazias" : string.Join(", ", a.Tools.ConvertAll(t => t.DisplayName)));
            if (a.Elements.Count > 0)
                sb.Append("\nElementos: " + string.Join(", ", a.Elements.ConvertAll(e => $"<color={HudText.Hex(e.Color)}>{e.DisplayName}</color>")));
            if (rules.crisisEnabled) sb.Append($"\nCrises: {a.Crises}/{rules.crisesToLeave}");
            if (a.IsLocked) sb.Append($"\n<color={HudColors.Perigo}>Em pânico: travado(a) {a.LockedScenes} cena(s).</color>");
            if (a.Determined) sb.Append($"\n<color={HudColors.Ok}>Determinada: sente menos medo.</color>");
            if (a.Role == ActorRole.Atleta && rules.atletaHoldDoorPerAct > 0)
                sb.Append(a.DoorHoldsThisAct < rules.atletaHoldDoorPerAct ? "\nSegurar a porta: disponível" : "\nSegurar a porta: usada neste ato");
            var v = run.VillainNpc;
            if (v != null && v.InHouse && v.Target == a) sb.Append($"\n<color={HudColors.Perigo}>Na mira do vilão!</color>");
            sb.Append($"\n<color={HudColors.Texto2}>Clique: selecionar · 2 cliques: close</color>");
            return sb.ToString();
        }

        public static string PavorWord(float r) => r >= 0.75f ? "no limite" : r >= 0.5f ? "alto" : r >= 0.25f ? "médio" : "baixo";

        public void Update(HudContext c)
        {
            var run = c.Run;
            if (cards.Count != run.Actors.Count || (cards.Count > 0 && cards[0].Actor != run.Actors[0])) Build(c);
            float limit = Mathf.Max(1, run.Content.Rules.pavorLimit);
            var v = run.VillainNpc;
            float dt = Time.unscaledDeltaTime;

            foreach (var card in cards)
            {
                var a = card.Actor;
                bool hunted = a.Alive && v != null && v.InHouse && v.Target == a;
                bool selected = a == c.Presenter.Selected;

                // Pavor: anima até o valor real (no relatório, espera o beat de pavor).
                float actual = a.Pavor / limit;
                if (card.Shown < 0f) card.Shown = actual;
                if (!c.PavorHeld)
                {
                    float before = card.Shown;
                    card.Shown = Mathf.MoveTowards(card.Shown, actual, dt / 0.6f);
                    if (card.Shown > before + 0.0001f) card.Drop.style.opacity = 1f;
                }
                card.Drop.style.opacity = Mathf.MoveTowards(card.Drop.resolvedStyle.opacity, 0f, dt * 1.2f);
                bool high = card.Shown >= 0.75f && a.Alive;
                Color fill = high ? new Color(1f, 0.541f, 0.239f) : new Color(0.486f, 0.361f, 1f);
                card.Ring.Set(a.Alive ? card.Shown : 0f, fill);
                card.Ring.style.opacity = high ? 0.65f + 0.35f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2f)) : 1f;

                string s = $"{a.Alive}|{selected}|{hunted}|{a.IsLocked}|{a.LockedScenes}|{a.Crises}|{a.Determined}|{a.Tools.Count}|{a.DoorHoldsThisAct}|{a.Exit}";
                if (s == card.Sig) continue;
                card.Sig = s;

                card.El.EnableInClassList("is-selected", selected);
                card.El.EnableInClassList("is-out", !a.Alive);
                card.El.EnableInClassList("is-hunted", hunted);
                card.El.EnableInClassList("is-panic", a.IsLocked);
                Ui.Display(card.Stamp, a.IsLocked && a.Alive);
                card.StampSub.text = a.LockedScenes == 1 ? "travado 1 cena" : $"travado {a.LockedScenes} cenas";
                Ui.Display(card.Strike, !a.Alive);
                card.Portrait.style.backgroundColor = a.Alive ? a.Def.Color : new Color(0.35f, 0.33f, 0.37f);
                Ui.Display(card.Out, !a.Alive);
                card.Out.text = a.Exit == ExitReason.Fled ? "FUGIU" : "FORA";
                Ui.Display(card.States, a.Alive);

                card.States.Clear();
                if (!a.Alive) continue;
                var rules = run.Content.Rules;
                int n = 0;
                void Add(string icon, string tint) { if (n++ < 3) Ui.Icon(icon, "ic-20 tint-" + tint, card.States); }
                if (hunted) Add("target", "perigo");
                if (a.IsLocked) Add("panic", "perigo");
                else if (a.Crises > 0) Add("panic", "alerta");
                if (a.Determined) Add("star", "ok");
                if (a.Tools.Count > 0) Add("key", "papel");
                if (a.Role == ActorRole.Atleta && rules.atletaHoldDoorPerAct > 0 && a.DoorHoldsThisAct < rules.atletaHoldDoorPerAct && v != null && v.InHouse)
                    Add("doorhold", "dim");
            }
        }

        /// <summary>No modo filme só o cartão de quem está na mira fica.</summary>
        public void ApplyFilmMode(HudContext c, bool film)
        {
            var v = c.Run.VillainNpc;
            foreach (var card in cards)
            {
                bool hunted = card.Actor.Alive && v != null && v.InHouse && v.Target == card.Actor;
                Ui.Fade(card.El, !film || hunted);
            }
        }

        /// <summary>Centro (painel) do cartão de um ator, para efeitos.</summary>
        public Rect CardRect(ActorRunState a)
        {
            foreach (var card in cards) if (card.Actor == a) return card.El.worldBound;
            return Rect.zero;
        }
    }

    // ================================================================================== Z5 Barra de ações

    /// <summary>Barra de ações (inf. centro): o que o ator selecionado pode fazer onde está. CONTEXTO.</summary>
    public class ActionBar
    {
        public readonly VisualElement Root;
        private readonly Label header;
        private readonly VisualElement row;
        private readonly Label empty;
        private string sig;
        private int highlighted = -1;

        public ActionBar(VisualElement parent)
        {
            Root = Ui.El("actionbar fadeable", parent, "Z5-Acoes");
            Root.pickingMode = PickingMode.Ignore;
            header = Ui.Lbl("", "t-label actionbar-header", Root);
            row = Ui.El("row actionbar-row", Root);
            row.pickingMode = PickingMode.Ignore;
            empty = Ui.Lbl("", "t-small actionbar-empty panel", Root);
        }

        public void Update(HudContext c)
        {
            var run = c.Run;
            var actor = c.Presenter.Selected;
            var actions = HudModel.Actions(run, actor);
            var sb = new StringBuilder();
            if (actor != null)
            {
                sb.Append(actor.Def.DisplayName).Append(actor.RoomIndex).Append(actor.IsLocked).Append(actor.LockedScenes).Append(run.ScenesLeft).Append(run.ActIndex);
                foreach (var a in actions) sb.Append(a.Verb).Append(a.Enabled).Append(a.Line).Append(a.Dots.Count).Append('|');
            }
            string s = sb.ToString();
            if (s == sig) return;
            sig = s;
            SetHighlight(c, -1);
            row.Clear();
            if (actor == null) return;

            var room = run.RoomOf(actor);
            string role = string.IsNullOrEmpty(actor.Def.RoleTitle) ? "" : " · " + actor.Def.RoleTitle.ToLowerInvariant();
            header.text = $"{actor.Def.DisplayName.ToUpperInvariant()}{role} · {(room != null ? room.Def.DisplayName : "lá fora")}";

            Ui.Display(empty, actions.Count == 0);
            empty.text = HudModel.NoActionLine(run, actor);
            int roomIndex = actor.RoomIndex;
            foreach (var a in actions)
            {
                var card = Ui.El("action-card", row);
                card.EnableInClassList("is-disabled", !a.Enabled);
                card.EnableInClassList("is-danger", a.Danger);
                var top = Ui.El("row action-top", card);
                Ui.Icon(a.Icon, "ic-32 " + (a.Danger ? "tint-perigo" : "tint-papel"), top);
                Ui.Lbl(a.Verb, "t-btn action-verb", top);
                var bottom = Ui.El("row action-bottom", card);
                var badge = Ui.El("badge " + (a.Cost == 0 ? "badge-free" : "badge-cost"), bottom);
                Ui.Lbl(HudText.Cost(a.Cost), "t-label", badge);
                if (a.Danger) Ui.Icon("warning", "ic-16 tint-perigo", bottom);
                Ui.Lbl(a.Line, "t-small action-line" + (a.Danger ? " fg-perigo" : ""), bottom);
                if (a.Dots.Count > 0)
                {
                    var dots = Ui.El("row action-dots", bottom);
                    foreach (var col in a.Dots)
                    {
                        var d = Ui.El("el-dot", dots);
                        d.style.backgroundColor = col;
                    }
                }
                var act = a;
                c.Tooltip.Attach(card, act.Tooltip);
                card.RegisterCallback<PointerEnterEvent>(_ => SetHighlight(c, roomIndex));
                card.RegisterCallback<PointerLeaveEvent>(_ => SetHighlight(c, -1));
                if (a.Enabled) Ui.OnClick(card, () => Invoke(c, act));
            }
        }

        private static void Invoke(HudContext c, ActionVM a)
        {
            var p = c.Presenter;
            if (p.Selected == null || p.CurrentPhase != RunPresenter.Phase.Idle) return;
            if (a.Kind == "hold") p.HoldDoor();
            else if (a.Kind == "tool") p.UseTool();
            else if (a.Kind == "direct" && a.Payoff != null) p.DirectScene(a.Payoff);
        }

        /// <summary>Prévia no mundo: a sala do ator acende no contorno enquanto o mouse está no cartão.</summary>
        private void SetHighlight(HudContext c, int room)
        {
            if (highlighted == room) return;
            if (highlighted >= 0) c.Presenter.AnchorOf(highlighted)?.SetHighlighted(false);
            highlighted = room;
            if (room >= 0) c.Presenter.AnchorOf(room)?.SetHighlighted(true);
        }

        public void Clear(HudContext c) => SetHighlight(c, -1);
    }

    // ================================================================================== Z6 Gravar + monitor + menu

    /// <summary>Gravar cena (inf. dir.) + botão do Monitor (M) + menu ≡ (encerrar ato, relatório, ajuda, opções).</summary>
    public class RecordCluster
    {
        public readonly VisualElement Root;
        public readonly VisualElement Menu;
        private readonly VisualElement record, recDot, holdFill, monitorBtn, menuBtn;
        private readonly Label[] speedBtns = new Label[3];
        private readonly Label[] scaleBtns = new Label[4];
        private static readonly float[] Scales = { 0.8f, 1f, 1.2f, 1.5f };
        private float holdStart = -1f;
        public const float HoldSeconds = 0.3f;

        public bool MenuOpen => Menu.resolvedStyle.display == DisplayStyle.Flex;

        public RecordCluster(VisualElement parent, VisualElement topLayer, HudContext c, IHudActions actions)
        {
            Root = Ui.El("record-cluster row fadeable", parent, "Z6-Gravar");
            Root.pickingMode = PickingMode.Ignore;

            monitorBtn = Ui.OnClick(Ui.El("square-btn", Root), () => { if (c.Monitor != null) c.Monitor.Toggle(); });
            Ui.Icon("monitor", "ic-32", monitorBtn);
            c.Tooltip.Attach(monitorBtn, "<b>Monitor do diretor</b> (M)\nA planta da casa, clicável. Com ele aberto, a tela vira filme.");

            menuBtn = Ui.OnClick(Ui.El("square-btn", Root), () => ShowMenu(!MenuOpen));
            Ui.Icon("menu", "ic-32", menuBtn);
            c.Tooltip.Attach(menuBtn, "Menu: encerrar ato, relatório, ajuda, opções");

            record = Ui.El("record-btn row", Root);
            recDot = Ui.Icon("rec", "ic-24 tint-rec rec-dot", record);
            Ui.Lbl("GRAVAR CENA", "t-display record-text", record);
            holdFill = Ui.El("record-hold", record);
            holdFill.pickingMode = PickingMode.Ignore;
            Ui.OnClick(record, () =>
            {
                if (c.Phase == RunPresenter.Phase.Idle && c.Run.CanRecordScene) c.Presenter.RecordScene();
            });
            c.Tooltip.Attach(record, () => c.Run.CanRecordScene
                ? "<b>Gravar cena</b> (segure Espaço)\nNinguém se mexe: a casa inteira conta 1 cena (plots avançam, duplas pontuam, o vilão age)."
                : "Sem cenas neste ato. Encerre o ato pelo menu ≡.");

            // ---- menu ≡
            Menu = Ui.El("menu panel col", topLayer, "Menu");
            Ui.Display(Menu, false);
            MenuItem(Menu, "list", "Relatório das cenas  (R)", () => { ShowMenu(false); actions.OpenReportLog(); });
            MenuItem(Menu, "help", "Ajuda e controles", () => { ShowMenu(false); actions.OpenHelp(); });
            var sp = Ui.El("row menu-row", Menu);
            Ui.Icon("fast", "ic-24", sp);
            Ui.Lbl("Relatório", "t-body menu-label", sp);
            for (int i = 0; i < 3; i++)
            {
                int k = i + 1;
                speedBtns[i] = Ui.OnClick(Ui.Lbl($"{k}×", "t-btn seg-btn", sp), () =>
                {
                    c.Settings.ReportSpeed = k;
                    actions.SettingsChanged();
                });
            }
            var sc = Ui.El("row menu-row", Menu);
            Ui.Icon("eye", "ic-24", sc);
            Ui.Lbl("Tamanho", "t-body menu-label", sc);
            for (int i = 0; i < Scales.Length; i++)
            {
                float v = Scales[i];
                scaleBtns[i] = Ui.OnClick(Ui.Lbl($"{Mathf.RoundToInt(v * 100)}%", "t-btn seg-btn", sc), () =>
                {
                    c.Settings.PlayerScale = v;
                    actions.SettingsChanged();
                });
            }
            MenuItem(Menu, "clapper", "HUD antiga  (F1)", () => { ShowMenu(false); actions.ToggleLegacy(); });
            Ui.El("menu-sep", Menu);
            var end = MenuItem(Menu, "warning", "Encerrar ato…", () => { ShowMenu(false); actions.ConfirmEndAct(); });
            end.AddToClassList("menu-danger");
        }

        private static VisualElement MenuItem(VisualElement menu, string icon, string text, System.Action onClick)
        {
            var row = Ui.OnClick(Ui.El("row menu-row menu-item", menu), onClick);
            Ui.Icon(icon, "ic-24", row);
            Ui.Lbl(text, "t-body", row);
            return row;
        }

        public void ShowMenu(bool open) => Ui.Display(Menu, open);

        public void Update(HudContext c, bool spaceDown, bool spacePressed)
        {
            bool can = c.Phase == RunPresenter.Phase.Idle && c.Run.CanRecordScene && !c.OverlayOpen;
            record.EnableInClassList("is-disabled", !can);
            monitorBtn.EnableInClassList("is-active", c.Monitor != null && c.Monitor.IsOpen);
            for (int i = 0; i < 3; i++) speedBtns[i].EnableInClassList("is-active", c.Settings.ReportSpeed == i + 1);
            for (int i = 0; i < Scales.Length; i++) scaleBtns[i].EnableInClassList("is-active", Mathf.Approximately(c.Settings.PlayerScale, Scales[i]));

            // Espaço: segurar 0,3 s para gravar (evita gravar por acidente).
            if (can && spacePressed) holdStart = Time.unscaledTime;
            if (!spaceDown || !can) holdStart = -1f;
            float k = holdStart >= 0f ? Mathf.Clamp01((Time.unscaledTime - holdStart) / HoldSeconds) : 0f;
            holdFill.style.width = Length.Percent(k * 100f);
            if (k >= 1f)
            {
                holdStart = -1f;
                c.Presenter.RecordScene();
            }
            recDot.style.opacity = can ? 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f) : 0.4f;
        }
    }
}
