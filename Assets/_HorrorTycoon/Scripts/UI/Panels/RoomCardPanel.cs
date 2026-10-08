using System.Collections.Generic;
using System.Text;
using HorrorTycoon.Run;
using UnityEngine;
using UnityEngine.UIElements;

namespace HorrorTycoon.UI
{
    /// <summary>
    /// Card da sala v2 (Z7, gaveta lateral esq.): divulgação progressiva (HUD_Spec §3.4).
    /// Ordem: título + chips de estado + clima → SE FICAR AQUI POR UMA CENA (a seção que decide) →
    /// PALCO → NA SALA → rodapé fixo QUEM VAI? (retratos-botão com selo de custo).
    /// Itens de 1 linha; a explicação longa vai para o tooltip do item.
    /// </summary>
    public class RoomCardPanel
    {
        public readonly VisualElement Root;
        private readonly VisualElement colorBar, chips, footer, footerRow;
        private readonly Label title, mood;
        private readonly ScrollView scroll;
        private string sig;
        private int shownRoom = -1;

        public RoomCardPanel(VisualElement parent, HudContext c)
        {
            Root = Ui.El("roomcard panel col fadeable", parent, "Z7-Card");
            colorBar = Ui.El("roomcard-bar", Root);
            var head = Ui.El("row roomcard-head", Root);
            title = Ui.Lbl("", "t-h2 roomcard-title", head);
            Ui.El("grow", head);
            var close = Ui.OnClick(Ui.El("icon-btn", head), () => c.Presenter.CloseRoomCard());
            Ui.Icon("close", "ic-20", close);
            chips = Ui.El("row chips", Root);
            mood = Ui.Lbl("", "t-small t-italic roomcard-mood", Root);

            scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("roomcard-scroll");
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            Root.Add(scroll);

            footer = Ui.El("roomcard-footer", Root);
            Ui.Lbl("QUEM VAI?", "t-label section-title", footer);
            footerRow = Ui.El("row who-row", footer);
        }

        public void Update(HudContext c)
        {
            var run = c.Run;
            int index = c.Presenter.CardRoom;
            bool visible = index >= 0 && index < run.Rooms.Count && c.Phase == RunPresenter.Phase.Idle;
            if (visible && !run.Rooms[index].IsDestination)
            {
                // Corredor/passagem: o card não abre (o hover do mundo diz "Passagem").
                c.Presenter.CloseRoomCard();
                visible = false;
            }
            if (!visible)
            {
                shownRoom = -1;
                return;
            }

            string s = Signature(run, index);
            if (s == sig && shownRoom == index) return;
            bool slide = shownRoom != index;
            sig = s;
            shownRoom = index;
            Rebuild(c, run, index);
            if (slide)
            {
                // Abre em 0,18 s: desliza 24 px + fade.
                Root.style.opacity = 0f;
                c.Tweens.Add(0.18f, k =>
                {
                    Root.style.opacity = k;
                    Root.style.translate = new Translate(Mathf.Lerp(-24f, 0f, HudTweens.EaseOut(k)), 0);
                }, () =>
                {
                    // Devolve o controle ao USS (o fade de saída usa a classe is-off).
                    Root.style.opacity = StyleKeyword.Null;
                    Root.style.translate = StyleKeyword.Null;
                }, 0f, Root);
            }
        }

        private static string Signature(FilmRun run, int index)
        {
            var r = run.Rooms[index];
            var sb = new StringBuilder();
            sb.Append(index).Append(r.Discovered).Append(r.VisitedThisAct).Append(r.Sealed).Append(r.LockUsed)
              .Append(r.Elements.Count).Append(r.FloorTools.Count).Append(run.ScenesLeft).Append(run.ActIndex);
            foreach (var a in run.Actors) sb.Append(a.RoomIndex).Append(a.Alive).Append(a.IsLocked).Append(a.Tools.Count);
            var v = run.VillainNpc;
            if (v != null) sb.Append(v.Space).Append(v.NextSpace).Append(v.StunBeats).Append(Mathf.RoundToInt(run.GhostTensionRatio * 10));
            foreach (var p in run.PlotsFor(index)) sb.Append(p.Def.DisplayName).Append(p.Progress);
            sb.Append(run.IsDoorHeldNext(index));
            return sb.ToString();
        }

        private void Chip(string icon, string tint, string text, string cls, HudContext c, string tip = null)
        {
            var chip = Ui.El("row chip " + cls, chips);
            if (icon != null) Ui.Icon(icon, "ic-16 tint-" + tint, chip);
            Ui.Lbl(text, "t-label", chip);
            if (tip != null) c.Tooltip.Attach(chip, tip);
        }

        private static VisualElement Section(VisualElement parent, string titleText)
        {
            var sec = Ui.El("section", parent);
            Ui.Lbl(titleText, "t-label section-title", sec);
            return sec;
        }

        private static void Item(HudContext c, VisualElement sec, string icon, string tint, string text, string tip, string cls = null)
        {
            var row = Ui.El("row card-item" + (cls != null ? " " + cls : ""), sec);
            Ui.Icon(icon, "ic-20 tint-" + tint, row);
            Ui.Lbl(text, "t-body card-item-text", row);
            if (!string.IsNullOrEmpty(tip)) c.Tooltip.Attach(row, tip);
        }

        private void Rebuild(HudContext c, FilmRun run, int index)
        {
            var room = run.Rooms[index];
            var def = room.Def;
            var v = run.VillainNpc;
            bool ghost = v != null && v.IsGhost;
            bool ghostHere = ghost && v.InHouse && v.Space == index;
            bool villainHere = !ghost && c.Presenter.RevealVillain && v != null && v.InHouse && v.Space == index;
            bool villainNext = v != null && v.IsTelegraphing && v.NextSpace == index;

            chips.Clear();
            scroll.Clear();
            var content = scroll.contentContainer;

            colorBar.style.backgroundColor = room.Discovered ? def.FloorColor * 1.6f : new Color(0.3f, 0.3f, 0.3f);

            // ---------------- título e chips de estado
            if (room.Sealed)
            {
                title.text = room.Discovered ? def.DisplayName.ToUpperInvariant() : "PORTA TRANCADA";
                Chip("lock", "perigo", "LACRADA", "chip-danger", c);
            }
            else if (!room.Discovered)
            {
                title.text = "PORTA FECHADA";
                Chip("door", "dim", "DESCONHECIDA", "chip-dim", c);
            }
            else
            {
                title.text = def.DisplayName.ToUpperInvariant();
                if (room.IsHub) Chip("group", "dim", "CONVIVÊNCIA", "chip-dim", c, "Área de convivência: os atores passam e se encontram aqui. Não há nada para explorar.");
                else if (room.VisitedThisAct) Chip("eye", "dim", "EXPLORADA", "chip-dim", c, "Já explorada neste ato: entrar de novo é grátis e não revela nada novo.");
                else Chip("eye", "papel", "NÃO EXPLORADA", "chip-plain", c, $"O 1º ator que entrar explora ({HudText.Cost(run.Content.Rules.exploreActionCost)}).");
                if (def.StagePayoffs.Count > 0) Chip("spotlight", "papel", "PALCO", "chip-plain", c, "Palco: dá para dirigir cenas aqui.");
                if (run.PlotsFor(index).Count > 0) Chip("diamond", "plot", "PLOT", "chip-plot", c);
            }
            if (villainNext && !ghost) Chip("footprints", "perigo", "PASSOS", "chip-danger", c, "O vilão entra aqui na próxima cena gravada.");
            if (villainHere) Chip("knife", "perigo", "VILÃO AQUI", "chip-danger", c);
            if (ghostHere) Chip("ghost", "fantasma", "ASSOMBRADA", "chip-ghost", c, $"O fantasma assombra esta sala. Tensão {HudText.TensionWord(run.GhostTensionRatio)}.");
            else if (ghost && villainNext) Chip("ghost", "fantasma", "PRÓXIMA", "chip-ghost", c, "Depois do próximo Grande Susto o fantasma vem para cá.");
            if (run.IsDoorHeldNext(index)) Chip("doorhold", "ok", "PORTA SEGURADA", "chip-ok", c, "O vilão não entra aqui na próxima cena.");

            // ---------------- clima (1 linha)
            string moodText = room.Sealed ? def.SealedText
                : !room.Discovered ? "Ninguém entrou aqui ainda."
                : room.IsHub && room.Space == null ? "O corredor liga a entrada a todos os cômodos."
                : def.Description;
            mood.text = string.IsNullOrEmpty(moodText) ? "" : moodText;
            Ui.Display(mood, !string.IsNullOrEmpty(moodText));

            // ---------------- lacrada / porta fechada
            if (room.Sealed)
            {
                var sec = Section(content, "COMO ABRIR");
                Item(c, sec, "unlock", "dim", "Um plot do filme pode abrir", "Cumpra o plot que libera esta sala (veja o Roteiro).");
            }
            else if (!room.Discovered)
            {
                if (def.Hints.Count > 0)
                {
                    var sec = Section(content, "PELA PORTA…");
                    Item(c, sec, "eye", "dim", HudText.Clip(def.Hints[0], 42), def.Hints[0]);
                }
                var ex = Section(content, "EXPLORAR");
                Item(c, ex, "door", "papel", $"Entrar descobre a sala ({HudText.Cost(run.Content.Rules.exploreActionCost).ToLowerInvariant()})", "Entre para descobrir o cômodo e o que há nele.");
            }
            else
            {
                StayPreview(c, run, index, content, ghostHere, villainHere, villainNext && !ghost);

                if (def.StagePayoffs.Count > 0)
                {
                    var sec = Section(content, "PALCO");
                    var row = Ui.El("row chips-wrap", sec);
                    foreach (var p in def.StagePayoffs)
                    {
                        if (p == null) continue;
                        bool locked = run.ActIndex < p.MinActIndex;
                        var chip = Ui.El("row chip chip-plain" + (locked ? " is-locked" : ""), row);
                        Ui.Icon(p.KillsActor ? "skull" : "scare", "ic-16 tint-" + (p.KillsActor ? "perigo" : "papel"), chip);
                        Ui.Lbl(p.DisplayName + (locked ? $" (Ato {p.MinActIndex + 1}+)" : ""), "t-label", chip);
                        c.Tooltip.Attach(chip, $"<b>{p.DisplayName}</b>: {p.Description}\n<color={HudColors.Texto2}>Leve um ator com elementos e dirija a cena ({HudText.Cost(run.Content.Rules.directStepCost).ToLowerInvariant()}).</color>");
                    }
                }

                bool hasLock = def.HasLockedSpot && !room.LockUsed;
                if (room.Elements.Count > 0 || room.FloorTools.Count > 0 || hasLock)
                {
                    var sec = Section(content, "NA SALA");
                    var row = Ui.El("row chips-wrap", sec);
                    foreach (var e in room.Elements)
                    {
                        var chip = Ui.El("row chip chip-plain", row);
                        var dot = Ui.El("el-dot", chip);
                        dot.style.backgroundColor = e.Color;
                        Ui.Lbl(e.DisplayName, "t-label", chip);
                        c.Tooltip.Attach(chip, $"<b>{e.DisplayName}</b>: {e.Description}");
                    }
                    foreach (var t in room.FloorTools)
                    {
                        var chip = Ui.El("row chip chip-plain", row);
                        Ui.Icon("key", "ic-16 tint-papel", chip);
                        Ui.Lbl(t.DisplayName + " (no chão)", "t-label", chip);
                        c.Tooltip.Attach(chip, "Quem entrar com espaço na mochila pega.");
                    }
                    if (hasLock)
                    {
                        var chip = Ui.El("row chip chip-plain", row);
                        Ui.Icon("lock", "ic-16 tint-alerta", chip);
                        Ui.Lbl(def.Locked.name, "t-label", chip);
                        c.Tooltip.Attach(chip, LockTip(run, def));
                    }
                }
            }

            // ---------------- QUEM VAI?
            footerRow.Clear();
            bool any = false;
            foreach (var actor in run.Actors)
            {
                if (!actor.Alive) continue;
                any = true;
                var a = actor;
                int cost = run.MoveCost(a, index);
                bool can = run.CanMove(a, index);
                string badge, badgeCls, why;
                if (a.RoomIndex == index) { badge = "AQUI"; badgeCls = "badge-here"; why = "Já está aqui."; }
                else if (a.IsLocked) { badge = "PÂNICO"; badgeCls = "badge-no"; why = "Em pânico: travado(a) nesta cena."; }
                else if (room.Sealed) { badge = "LACRADA"; badgeCls = "badge-no"; why = "Lacrada: ninguém entra."; }
                else if (cost < 0) { badge = "SEM CAMINHO"; badgeCls = "badge-no"; why = "Não há caminho até aqui."; }
                else if (cost > run.ScenesLeft) { badge = "SEM CENAS"; badgeCls = "badge-no"; why = $"Custa {HudText.Cost(cost).ToLowerInvariant()} e não há cenas."; }
                else if (cost == 0) { badge = "GRÁTIS"; badgeCls = "badge-free"; why = "Andar é grátis."; }
                else { badge = HudText.Cost(cost); badgeCls = "badge-cost"; why = $"{HudText.Cost(cost)}: explora a sala."; }
                if (villainHere && a.RoomIndex != index) why += $"\n<color={HudColors.Perigo}>O vilão está aqui!</color>";
                if (ghostHere && a.RoomIndex != index) why += $"\n<color={HudColors.Fantasma}>Sala assombrada.</color>";

                var btn = Ui.El("who-btn col" + (can ? "" : " is-disabled") + (a == c.Presenter.Selected ? " is-selected" : ""), footerRow);
                var face = Ui.El("who-face", btn);
                face.style.backgroundColor = a.Def.Color;
                Ui.Lbl(a.Def.DisplayName.Substring(0, 1).ToUpperInvariant(), "t-display who-initial", face);
                Ui.Lbl(HudText.Clip(a.Def.DisplayName, 10), "t-label who-name", btn);
                var b = Ui.El("badge " + badgeCls, btn);
                Ui.Lbl(badge, "t-label", b);
                c.Tooltip.Attach(btn, $"<b>{a.Def.DisplayName}</b>\n{why}");
                if (can) Ui.OnClick(btn, () => c.Presenter.SendActor(a, index));
            }
            Ui.Display(footer, any);
        }

        private static string LockTip(FilmRun run, Rooms.RoomDef def)
        {
            ActorRunState holder = null;
            foreach (var a in run.Actors) if (a.Alive && a.Tools.Contains(def.Locked.requiredTool)) holder = a;
            string how = holder != null ? $"{holder.Def.DisplayName} está com {def.Locked.requiredTool.DisplayName}: leve até aqui para abrir."
                : "Precisa de uma ferramenta para abrir. Talvez esteja em algum lugar da casa.";
            return $"<b>{def.Locked.name} trancado</b>\n{how}";
        }

        /// <summary>"Se ficar aqui por uma cena…" — linguagem de filme, sem números (§3.4).</summary>
        private static void StayPreview(HudContext c, FilmRun run, int index, VisualElement content, bool ghostHere, bool villainHere, bool villainNext)
        {
            var room = run.Rooms[index];
            var here = run.ActorsIn(index);
            var sec = Section(content, "SE FICAR AQUI POR UMA CENA");
            sec.AddToClassList("section-key");
            int n = 0;

            foreach (var p in run.PlotsFor(index))
            {
                int req = run.RequiredScenes(p, index);
                bool ok = run.PlotConditionMet(p, index);
                var row = Ui.El("row card-item", sec);
                Ui.Icon("diamond", "ic-20 tint-plot", row);
                Ui.Lbl(HudText.Clip(p.Def.DisplayName, 24), "t-body t-bold card-item-text", row);
                var pips = Ui.El("row plot-pips plot-pips-dark", row);
                Pips.Set(pips, p.Progress, req, "ppip");
                Ui.El("grow", row);
                if (ok)
                {
                    var chip = Ui.El("row chip chip-ok", row);
                    Ui.Icon("check", "ic-16 tint-tinta", chip);
                    Ui.Lbl("pronto", "t-label", chip);
                }
                else Ui.Lbl("falta gente", "t-small fg-alerta", row);
                c.Tooltip.Attach(row, $"<b>{p.Def.DisplayName}</b>: {p.Def.Description}\n" +
                                      (ok ? $"<color={HudColors.Ok}>Condição cumprida: grave cenas!</color>" : $"<color={HudColors.Texto2}>Precisa: {HudText.RoleNames(p.Def)}</color>"));
                n++;
            }
            foreach (var cb in run.ActiveCombosIn(index))
            {
                Item(c, sec, "heart", "dupla", HudText.Clip($"{cb.Def.DisplayName}: mais audiência", 42), $"<b>{cb.Def.DisplayName}</b>: {cb.Def.Description}");
                n++;
            }
            if (villainNext) { Item(c, sec, "knife", "perigo", "Vilão entra aqui na próxima cena", "Os passos anunciam: na próxima cena gravada o vilão entra aqui. Sozinho = pego; em grupo, ele recua.", "is-danger"); n++; }
            if (villainHere) { Item(c, sec, "knife", "perigo", "O vilão está aqui", "Quem ficar sozinho com ele é pego.", "is-danger"); n++; }
            if (ghostHere)
            {
                Item(c, sec, "ghost", "fantasma", $"Tensão do fantasma sobe ({HudText.TensionWord(run.GhostTensionRatio)})",
                    "A tensão do fantasma cresce a cada cena (mais rápido com gente aqui). No máximo: GRANDE SUSTO em quem estiver aqui: muita audiência, muito medo.");
                n++;
            }
            if (run.IsNearVillain(index) && !villainHere) { Item(c, sec, "target", "alerta", "Perto do vilão: + audiência, + medo", "Perto do vilão rende mais audiência, mas assusta."); n++; }
            if (run.IsDoorHeldNext(index)) { Item(c, sec, "doorhold", "ok", "Porta segurada: vilão não entra", null); n++; }
            if (room.Def.PavorPerScene > 0) { Item(c, sec, "drop", "pavor", "Lugar escuro: o medo sobe", "Lugar escuro e perigoso: o medo sobe a cada cena."); n++; }
            if (here.Count == 1) { Item(c, sec, "eye", "pavor", HudText.Clip($"{here[0].Def.DisplayName} sozinho(a): o medo sobe", 42), "Sozinho(a) sente medo (e o vilão gosta disso)."); n++; }
            if (n == 0)
            {
                if (here.Count == 0) Item(c, sec, "dot", "dim", "Ninguém aqui ainda", "Mande alguém pelo QUEM VAI? abaixo.");
                else Item(c, sec, "dot", "dim", "Nada de especial: só o tempo passa", null);
            }
        }
    }
}
