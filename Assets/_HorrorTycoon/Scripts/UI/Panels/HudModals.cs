using System.Collections.Generic;
using System.Text.RegularExpressions;
using HorrorTycoon.Run;
using UnityEngine;
using UnityEngine.UIElements;

namespace HorrorTycoon.UI
{
    /// <summary>
    /// Modais (Z10): fim de rolo, artefato, vilão, estreia (fases que já pausam) e as janelas do menu ≡
    /// (confirmar encerrar ato, relatório das cenas, ajuda, roteiro completo).
    /// Entrada em 0,25 s; botões só respondem depois de 0,3 s (evita clique que vinha da sequência).
    /// </summary>
    public class ModalHost
    {
        public const float ButtonDelay = 0.3f;

        private readonly VisualElement layer;
        private VisualElement scrim, dialog;
        private float openedAt;
        public string Current { get; private set; } = "";
        public bool IsOpen => scrim != null;
        /// <summary>Janela do menu (não é fase do jogo).</summary>
        public bool IsOverlay { get; private set; }

        public ModalHost(VisualElement layer)
        {
            this.layer = layer;
        }

        public void Close()
        {
            scrim?.RemoveFromHierarchy();
            scrim = null;
            dialog = null;
            Current = "";
            IsOverlay = false;
        }

        private VisualElement Open(HudContext c, string id, string cls, bool overlay)
        {
            Close();
            Current = id;
            IsOverlay = overlay;
            openedAt = Time.unscaledTime;
            scrim = Ui.El("scrim" + (overlay ? " scrim-light" : ""), layer);
            if (overlay) Ui.OnClick(scrim, () => { if (Ready) Close(); });
            dialog = Ui.El("modal " + cls, scrim);
            dialog.RegisterCallback<ClickEvent>(e => e.StopPropagation());
            dialog.style.opacity = 0f;
            c.Tweens.Add(0.25f, k =>
            {
                dialog.style.opacity = k;
                dialog.style.scale = new Scale(Vector2.one * Mathf.Lerp(0.94f, 1f, HudTweens.EaseOut(k)));
            }, null, 0f, dialog);
            return dialog;
        }

        private bool Ready => Time.unscaledTime - openedAt >= ButtonDelay;

        private VisualElement Button(VisualElement parent, string text, string cls, System.Action onClick, string icon = null)
        {
            var b = Ui.El("btn row " + cls, parent);
            if (icon != null) Ui.Icon(icon, "ic-24", b);
            Ui.Lbl(text, "t-btn", b);
            Ui.OnClick(b, () => { if (Ready) onClick(); });
            return b;
        }

        // ---------------------------------------------------------------- Fim de ato

        public void ShowActBreak(HudContext c)
        {
            var act = c.Presenter.LastAct;
            if (act == null) return;
            var run = c.Run;
            var d = Open(c, "act", "modal-act paper col", false);
            Ui.Lbl($"FIM DO ROLO {HudText.Roman(act.ActIndex)}", "t-h1 modal-title", d);
            var scoreRow = Ui.El("row act-score", d);
            Ui.Lbl("AUDIÊNCIA", "t-label act-cap", scoreRow);
            var num = Ui.Lbl("0", "t-display act-num", scoreRow);
            Ui.Lbl($"/ {act.Goal}", "t-display act-goal", scoreRow);
            var stamp = Ui.El("act-stamp " + (act.Passed ? "stamp-ok" : "stamp-bad"), scoreRow);
            Ui.Lbl(act.Passed ? "APROVADO" : "REPROVADO", "t-marker", stamp);
            stamp.style.opacity = 0f;

            int plots = 0, crises = 0, deaths = 0, fled = 0;
            foreach (var p in run.Plots) if (p.Status == PlotStatus.Completed) plots++;
            foreach (var a in run.Actors)
            {
                crises += a.Crises;
                if (!a.Alive && a.Exit == ExitReason.Died) deaths++;
                if (!a.Alive && a.Exit == ExitReason.Fled) fled++;
            }
            var stats = Ui.El("row act-stats", d);
            Stat(stats, "diamond", "tint-plot-papel", $"{plots} plot{(plots == 1 ? "" : "s")}");
            Stat(stats, "skull", "tint-tinta", $"{deaths} morte{(deaths == 1 ? "" : "s")}");
            Stat(stats, "panic", "tint-perigo-papel", $"{crises} crise{(crises == 1 ? "" : "s")}");
            if (fled > 0) Stat(stats, "door", "tint-tinta", $"{fled} fugiu");
            Stat(stats, "artefact", "tint-tinta", $"{run.OwnedArtefatos.Count} artefato{(run.OwnedArtefatos.Count == 1 ? "" : "s")}");

            string next = act.Fatal && !act.Passed ? "O filme não segura o público. É o fim."
                : run.AwaitingArtefatoChoice ? "A seguir: escolha 1 artefato para o filme."
                : run.AwaitingVillainChoice ? "A seguir: quem é o vilão deste filme?" : "";
            if (next.Length > 0) Ui.Lbl(next, "t-body act-next", d);
            var row = Ui.El("row modal-buttons", d);
            Button(row, "CONTINUAR", "btn-primary", () => c.Presenter.ContinueAfterAct(), "play");

            int target = act.TotalScore;
            c.Tweens.Add(0.8f, k => num.text = Mathf.RoundToInt(target * HudTweens.EaseOut(k)).ToString(), null, 0.2f);
            c.Tweens.Add(0.3f, k =>
            {
                stamp.style.opacity = k;
                stamp.style.scale = new Scale(Vector2.one * Mathf.Lerp(1.8f, 1f, HudTweens.EaseOut(k)));
            }, null, 1.0f);
        }

        private static void Stat(VisualElement parent, string icon, string tint, string text)
        {
            var s = Ui.El("row act-stat", parent);
            Ui.Icon(icon, "ic-24 " + tint, s);
            Ui.Lbl(text, "t-body t-bold", s);
        }

        // ---------------------------------------------------------------- Artefato

        public void ShowArtefato(HudContext c)
        {
            var run = c.Run;
            var d = Open(c, "artefato", "modal-pick col", false);
            Ui.Lbl("ESCOLHA 1 ARTEFATO", "t-h1 modal-title fg-papel", d);
            Ui.Lbl("Artefatos mudam as regras do filme até o fim. Grátis.", "t-body t-dim modal-sub", d);
            var cards = Ui.El("row pick-cards", d);
            foreach (var a in run.ArtefatoOffer)
            {
                var art = a;
                var card = Ui.El("pick-card paper col", cards);
                var band = Ui.El("pick-band", card);
                band.style.backgroundColor = a.Color;
                var iconWrap = Ui.El("pick-icon-wrap", card);
                var ic = Ui.Icon("artefact", "ic-96", iconWrap);
                ic.style.unityBackgroundImageTintColor = a.Color;
                Ui.Lbl(a.DisplayName, "t-display pick-name", card);
                Ui.Lbl(Highlight(a.Description), "t-body pick-desc", card);
                Ui.OnClick(card, () => { if (Ready) c.Presenter.ChooseArtefato(art); });
            }
            var skip = Ui.Lbl("pular  >", "t-body pick-skip", d);
            Ui.OnClick(skip, () => { if (Ready) c.Presenter.ChooseArtefato(null); });
        }

        /// <summary>Palavras-chave do efeito ganham a cor do significado ("Duplas rendem mais").</summary>
        public static string Highlight(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = HudText.StripSymbols(s);
            s = Regex.Replace(s, @"\b(duplas?|Duplas?|combos?|Combos?)\b", m => $"<color=#C2357F><b>{m.Value}</b></color>");
            s = Regex.Replace(s, @"\b(plots?|Plots?)\b", m => $"<color=#3F5BD0><b>{m.Value}</b></color>");
            s = Regex.Replace(s, @"\b(pavor|medo|Pavor|Medo)\b", m => $"<color=#5A3FD6><b>{m.Value}</b></color>");
            s = Regex.Replace(s, @"\b(sustos?|Sustos?|mortes?|Mortes?)\b", m => $"<color=#B3242C><b>{m.Value}</b></color>");
            s = Regex.Replace(s, @"\b(cenas?|Cenas?)\b", m => $"<b>{m.Value}</b>");
            return s;
        }

        // ---------------------------------------------------------------- Vilão

        public void ShowVillain(HudContext c)
        {
            var run = c.Run;
            var d = Open(c, "villain", "modal-pick col", false);
            Ui.Lbl("QUEM É O VILÃO?", "t-h1 modal-title fg-papel", d);
            Ui.Lbl("Ele entra na casa no próximo ato. Os elementos do subgênero dele valem em dobro.", "t-body t-dim modal-sub", d);
            var cards = Ui.El("row pick-cards", d);
            foreach (var v in run.VillainOffer())
            {
                var vil = v;
                bool ghost = v.IsGhost;
                var card = Ui.El("poster col " + (ghost ? "poster-ghost" : "poster-slasher"), cards);
                var band = Ui.El("poster-band row", card);
                Ui.Lbl(ghost ? "FANTASMA" : "SLASHER", "t-label poster-family", band);
                var art = Ui.El("poster-art", card);
                Ui.Icon(ghost ? "ghost" : "knife", "ic-96 " + (ghost ? "tint-fantasma" : "tint-perigo"), art);
                Ui.Lbl(v.DisplayName.ToUpperInvariant(), "t-display poster-name", card);
                Ui.Lbl(ghost ? "Assombra uma sala e dá o Grande Susto." : "Anda pela casa e caça quem está sozinho.", "t-body t-bold poster-how", card);
                if (v.Subgenre != null) Ui.Lbl(v.Subgenre.DisplayName, "t-small t-italic poster-genre", card);
                Ui.Lbl(HudText.StripSymbols(v.Description), "t-small poster-desc", card);
                Ui.OnClick(card, () => { if (Ready) c.Presenter.ChooseVillain(vil); });
            }
        }

        // ---------------------------------------------------------------- Estreia (fim)

        public void ShowEnd(HudContext c, System.Action onCopied)
        {
            var run = c.Run;
            bool won = run.Status == RunStatus.Won;
            var d = Open(c, "end", "modal-end paper col", false);
            Ui.Lbl("ESTREIA", "t-h1 modal-title", d);
            var stamp = Ui.El("act-stamp end-stamp " + (won ? "stamp-ok" : "stamp-bad"), d);
            Ui.Lbl(won ? "FILME APROVADO" : "O FILME FRACASSOU", "t-marker", stamp);
            var scoreRow = Ui.El("row act-score", d);
            Ui.Lbl("AUDIÊNCIA FINAL", "t-label act-cap", scoreRow);
            var num = Ui.Lbl("0", "t-display act-num", scoreRow);
            int target = run.TotalScore;
            c.Tweens.Add(0.9f, k => num.text = Mathf.RoundToInt(target * HudTweens.EaseOut(k)).ToString(), null, 0.2f);

            Ui.Lbl("ELENCO", "t-label end-cap", d);
            var cast = Ui.El("row end-cast", d);
            foreach (var a in run.Actors)
            {
                var col = Ui.El("col end-actor" + (a.Alive ? "" : " is-out"), cast);
                var face = Ui.El("end-face", col);
                face.style.backgroundColor = a.Alive ? a.Def.Color : new Color(0.45f, 0.43f, 0.47f);
                Ui.Lbl(a.Def.DisplayName.Substring(0, 1).ToUpperInvariant(), "t-display end-initial", face);
                if (!a.Alive) Ui.El("portrait-strike", face);
                Ui.Lbl(a.Def.DisplayName, "t-small t-bold", col);
                Ui.Lbl(a.Alive ? "sobreviveu" : a.Exit == ExitReason.Fled ? "fugiu" : "morreu", "t-small end-fate", col);
            }
            Ui.Lbl($"Vilão: {(run.Villain != null ? run.Villain.DisplayName : "—")}   ·   seed {run.Rng.Seed}", "t-small end-seed", d);
            var row = Ui.El("row modal-buttons", d);
            Button(row, "JOGAR DE NOVO", "btn-primary", () => c.Presenter.Restart(), "play");
            Button(row, "COPIAR LOG", "btn-secondary", () =>
            {
                GUIUtility.systemCopyBuffer = run.Log.ToJson();
                onCopied?.Invoke();
            }, "list");
        }

        // ---------------------------------------------------------------- Janelas do menu

        public void ShowConfirmEndAct(HudContext c)
        {
            var run = c.Run;
            var d = Open(c, "confirm", "modal-small panel col", true);
            Ui.Lbl("Encerrar o ato?", "t-h2 modal-title fg-papel", d);
            Ui.Lbl($"Encerrar o Ato {HudText.Roman(run.ActIndex)} com {run.ScenesLeft} cena{(run.ScenesLeft == 1 ? "" : "s")} sobrando?\n" +
                   $"<color={HudColors.Texto2}>Audiência {run.TotalScore} de {run.CurrentAct.goal}.</color>", "t-body modal-sub", d);
            var row = Ui.El("row modal-buttons", d);
            Button(row, "VOLTAR", "btn-secondary", Close);
            Button(row, "ENCERRAR", "btn-danger", () =>
            {
                Close();
                c.Presenter.EndActEarly();
            });
        }

        public void ShowReportLog(HudContext c, List<SceneLogEntry> history)
        {
            var d = Open(c, "log", "modal-wide panel col", true);
            var head = Ui.El("row", d);
            Ui.Lbl("RELATÓRIO DAS CENAS", "t-h2 modal-title fg-papel", head);
            Ui.El("grow", head);
            Ui.Icon("close", "ic-24", Ui.OnClick(Ui.El("icon-btn", head), Close));
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("log-scroll");
            d.Add(scroll);
            if (history.Count == 0) Ui.Lbl("Nenhuma cena gravada ainda.", "t-body t-dim", scroll);
            for (int i = history.Count - 1; i >= 0; i--)
            {
                var e = history[i];
                var block = Ui.El("log-block", scroll);
                Ui.Lbl($"ATO {HudText.Roman(e.Act)} · CENA {e.Scene} — {e.Title}" + (e.Gain > 0 ? $"   <color={HudColors.Audiencia}>+{e.Gain}</color>" : ""), "t-bold log-title", block);
                foreach (var l in e.Lines) Ui.Lbl(l, "t-small log-line", block);
            }
        }

        public void ShowHelp(HudContext c)
        {
            var d = Open(c, "help", "modal-wide panel col", true);
            var head = Ui.El("row", d);
            Ui.Lbl("AJUDA", "t-h2 modal-title fg-papel", head);
            Ui.El("grow", head);
            Ui.Icon("close", "ic-24", Ui.OnClick(Ui.El("icon-btn", head), Close));
            string[,] rows =
            {
                { "Clique numa sala", "abre o card: o que se sabe e QUEM VAI?" },
                { "Clique num ator / Tab", "seleciona; a barra de ações mostra o que ele pode fazer" },
                { "Gravar cena (segure Espaço)", "ninguém se mexe: a casa inteira conta 1 cena" },
                { "Andar", "é grátis. Gastam cena: explorar, gravar, dirigir, usar ferramenta" },
                { "M", "monitor do diretor (a tela vira filme)" },
                { "R", "relatório das últimas cenas" },
                { "F", "close no ator selecionado" },
                { "Esc", "fecha o card / menu" },
                { "Câmera", "WASD/setas · Q/E ou botão direito: girar · scroll: zoom · botão do meio: arrastar" },
                { "Relatório", "clique ou Espaço: pula o beat · 2 cliques: resumo · Shift: 2×" },
                { "F1", "alterna para a HUD antiga (comparação)" },
            };
            for (int i = 0; i < rows.GetLength(0); i++)
            {
                var r = Ui.El("row help-row", d);
                Ui.Lbl(rows[i, 0], "t-bold help-key", r);
                Ui.Lbl(rows[i, 1], "t-body help-val", r);
            }
        }

        public void ShowScriptList(HudContext c)
        {
            var run = c.Run;
            var d = Open(c, "script", "modal-wide panel col", true);
            var head = Ui.El("row", d);
            Ui.Lbl("ROTEIRO COMPLETO", "t-h2 modal-title fg-papel", head);
            Ui.El("grow", head);
            Ui.Icon("close", "ic-24", Ui.OnClick(Ui.El("icon-btn", head), Close));
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("log-scroll");
            d.Add(scroll);
            Ui.Lbl("PLOTS", "t-label section-title", scroll);
            var rows = HudModel.PlotRows(run, true);
            if (rows.Count == 0) Ui.Lbl("Nenhum plot ainda: explore salas.", "t-body t-dim", scroll);
            foreach (var r in rows)
            {
                var row = Ui.El("row card-item", scroll);
                Ui.Icon(r.Completed ? "check" : "diamond", "ic-20 " + (r.Completed ? "tint-ok" : "tint-plot"), row);
                Ui.Lbl($"<b>{r.Name}</b>  {HudText.Progress(r.Progress, r.Required)}  <color={HudColors.Texto2}>{r.Room}</color>" + (r.Ready ? $"  <color={HudColors.Ok}>pronto</color>" : ""), "t-body", row);
                c.Tooltip.Attach(row, r.Tooltip);
            }
            Ui.Lbl("CENAS DE DUPLA AGORA", "t-label section-title", scroll);
            var combos = run.AllActiveCombos();
            if (combos.Count == 0) Ui.Lbl("Ninguém junto numa sala.", "t-body t-dim", scroll);
            foreach (var cb in combos)
            {
                var row = Ui.El("row card-item", scroll);
                Ui.Icon("heart", "ic-20 tint-dupla", row);
                Ui.Lbl($"<b>{cb.Def.DisplayName}</b> · {run.Rooms[cb.Space].Def.DisplayName}: {cb.Def.Description}", "t-body", row);
            }
            Ui.Lbl("ARTEFATOS", "t-label section-title", scroll);
            if (run.OwnedArtefatos.Count == 0) Ui.Lbl("Nenhum (escolha entre atos).", "t-body t-dim", scroll);
            foreach (var a in run.OwnedArtefatos)
            {
                var row = Ui.El("row card-item", scroll);
                var ic = Ui.Icon("artefact", "ic-20", row);
                ic.style.unityBackgroundImageTintColor = a.Color;
                Ui.Lbl($"<b>{a.DisplayName}</b>: {a.Description}", "t-body", row);
            }
        }
    }
}
