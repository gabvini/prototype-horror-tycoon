using System.Collections.Generic;
using HorrorTycoon.Run;
using UnityEngine;
using UnityEngine.UIElements;

namespace HorrorTycoon.UI
{
    /// <summary>Uma cena no histórico (menu ≡ → Relatório, tecla R).</summary>
    public class SceneLogEntry
    {
        public string Title;
        public int Act;
        public int Scene;
        public int Gain;
        public List<string> Lines = new List<string>();
    }

    /// <summary>
    /// Relatório da cena como LEGENDAS no terço inferior (Z8), um beat por linha, na ordem do tique (§6.1).
    /// "+N" voa da legenda para a Audiência e o contador rola. Clique/Espaço pula o beat; 2º clique rápido
    /// vai ao resumo; Shift = 2×; velocidade 1×/2×/3× no menu. Ao terminar chama presenter.SkipResult().
    /// </summary>
    public class SubtitleReport
    {
        public readonly VisualElement Root;
        private readonly VisualElement box, icon, titleChip;
        private readonly Label text, title, hint, summary;
        private readonly ReportSequencer seq = new ReportSequencer();
        private bool active;
        private int frozenScore;
        private int lastIndex = -2;
        private bool lastSummary;
        private readonly VisualElement flyLayer;

        public bool Active => active;
        public ReportSequencer Sequencer => seq;

        public SubtitleReport(VisualElement parent, VisualElement flyLayer)
        {
            this.flyLayer = flyLayer;
            Root = Ui.El("subtitles col", parent, "Z8-Legendas");
            Root.pickingMode = PickingMode.Ignore;
            titleChip = Ui.El("sub-title paper row", Root);
            Ui.Icon("clapper", "ic-24 tint-tinta", titleChip);
            title = Ui.Lbl("", "t-display sub-title-text", titleChip);
            box = Ui.El("sub-box row", Root);
            icon = Ui.Icon("dot", "ic-32", box);
            text = Ui.Lbl("", "sub-text", box);
            summary = Ui.Lbl("", "t-display sub-summary", Root);
            hint = Ui.Lbl("clique ou Espaço: pular  ·  2 cliques: resumo  ·  Shift: 2×", "t-small sub-hint", Root);
            Ui.Display(Root, false);
        }

        /// <summary>Começa a sequência (fase ShowingResult). baseScore = audiência antes da ação.</summary>
        public void Begin(HudContext c, int baseScore, List<SceneLogEntry> history)
        {
            var p = c.Presenter;
            var beats = ReportBeats.Build(p.ResultBeats, c.Timing);
            seq.Start(beats, c.Timing.summary);
            active = true;
            frozenScore = baseScore;
            lastIndex = -2;
            lastSummary = false;
            Ui.Display(Root, true);
            Ui.Display(summary, false);
            Ui.Display(box, true);

            string t = HudText.StripSymbols(HudText.StripTags(p.ResultTitle));
            title.text = string.IsNullOrEmpty(t) ? $"CENA {c.SceneShown}" : $"CENA {c.SceneShown} · {HudText.Clip(t, 44)}";
            c.Tweens.Add(0.4f, k => titleChip.style.scale = new Scale(Vector2.one * Mathf.Lerp(1.12f, 1f, HudTweens.EaseOut(k))), null, 0f, titleChip);

            // Histórico (últimas 10 cenas).
            var entry = new SceneLogEntry
            {
                Title = string.IsNullOrEmpty(t) ? "Cena" : t,
                Act = c.Run.ActIndex,
                Scene = c.SceneShown,
                Gain = c.Run.TotalScore - baseScore
            };
            foreach (var b in p.ResultBeats) entry.Lines.Add((b.Score > 0 ? $"+{b.Score}  " : "") + HudText.StripSymbols(b.Text));
            history.Add(entry);
            while (history.Count > 10) history.RemoveAt(0);
        }

        public void Cancel()
        {
            active = false;
            seq.Stop();
            Ui.Display(Root, false);
        }

        /// <summary>Alvo do contador da audiência enquanto a sequência roda.</summary>
        public int TargetScore(HudContext c)
        {
            if (!active) return c.Run.TotalScore;
            if (seq.Done) return c.Run.TotalScore;
            return Mathf.Min(c.Run.TotalScore, frozenScore + seq.ScoreShown);
        }

        public bool PavorHeld => active && !seq.PavorReleased;

        public void Update(HudContext c, bool skip, bool shift, VisualElement scoreAnchor, VisualElement shakeTarget)
        {
            if (!active) return;
            if (c.Phase != RunPresenter.Phase.ShowingResult)
            {
                Cancel();
                return;
            }
            if (skip) seq.Skip(Time.unscaledTime);
            float speed = Mathf.Max(1, c.Settings.ReportSpeed) * (shift ? 2f : 1f);
            if (!c.DebugFreeze) seq.Tick(Time.unscaledDeltaTime * speed);

            if (seq.InSummary && !lastSummary)
            {
                lastSummary = true;
                Ui.Display(box, false);
                Ui.Display(summary, true);
                int gain = c.Run.TotalScore - frozenScore;
                summary.text = gain > 0 ? $"CENA {c.SceneShown}  ·  <color={HudColors.Audiencia}>+{gain}</color> de audiência" : $"CENA {c.SceneShown}";
                c.Tweens.Add(0.25f, k => summary.style.opacity = k, null, 0f, summary);
            }
            else if (!seq.InSummary && seq.Index != lastIndex)
            {
                lastIndex = seq.Index;
                ShowBeat(c, seq.Current, scoreAnchor, shakeTarget);
            }

            if (seq.Done)
            {
                active = false;
                Ui.Display(Root, false);
                c.Presenter.SkipResult();
            }
        }

        private void ShowBeat(HudContext c, HudBeat b, VisualElement scoreAnchor, VisualElement shakeTarget)
        {
            if (b == null) return;
            string ic, tint;
            KindStyle(c, b.Kind, out ic, out tint);
            Ui.SetIcon(icon, ic);
            Ui.SetTint(icon, tint);
            text.text = b.Text;
            box.EnableInClassList("is-big", b.Size == BeatSize.Big);
            box.RemoveFromClassList("kind-perigo");
            box.RemoveFromClassList("kind-ok");
            box.RemoveFromClassList("kind-plot");
            box.RemoveFromClassList("kind-fantasma");
            box.RemoveFromClassList("kind-dupla");
            box.RemoveFromClassList("kind-audiencia");
            box.RemoveFromClassList("kind-alerta");
            box.AddToClassList("kind-" + tint.Replace("tint-", ""));
            c.Tweens.Add(0.18f, k =>
            {
                box.style.opacity = k;
                box.style.translate = new Translate(0, Mathf.Lerp(10f, 0f, HudTweens.EaseOut(k)));
            }, null, 0f, box);

            if (b.Score > 0) Fly(c, b.Score, scoreAnchor);
            if (b.Size == BeatSize.Big && shakeTarget != null)
            {
                // Tremor em 3 níveis pelo tamanho do ganho (Balatro): 0,2 / 0,3 / 0,5 s.
                float dur = b.Score >= 300 ? 0.5f : b.Score >= 100 ? 0.3f : 0.2f;
                float amp = b.Score >= 300 ? 8f : 5f;
                c.Tweens.Add(dur, k => shakeTarget.style.translate = new Translate(
                    Mathf.Sin(k * 60f) * amp * (1f - k), Mathf.Cos(k * 47f) * amp * 0.6f * (1f - k)), () => shakeTarget.style.translate = new Translate(0, 0), 0f, shakeTarget);
            }
        }

        /// <summary>"+N" sai da legenda e voa até a Audiência (0,35 s, em curva).</summary>
        private void Fly(HudContext c, int score, VisualElement scoreAnchor)
        {
            if (scoreAnchor == null || flyLayer == null) return;
            var lbl = Ui.Lbl($"+{score}", "t-display fly-score", flyLayer);
            lbl.pickingMode = PickingMode.Ignore;
            lbl.style.fontSize = Mathf.Clamp(28f + score / 15f, 28f, 48f);
            Rect from = box.worldBound;
            Vector2 a = new Vector2(from.xMax - 40f, from.yMin - 20f);
            Rect to = scoreAnchor.worldBound;
            Vector2 b = to.center - new Vector2(20f, 20f);
            Vector2 ctrl = new Vector2((a.x + b.x) * 0.5f, Mathf.Min(a.y, b.y) - 120f);
            lbl.style.left = a.x;
            lbl.style.top = a.y;
            c.Tweens.Add(0.2f, k => lbl.style.scale = new Scale(Vector2.one * Mathf.Lerp(0.6f, 1.15f, k)));
            c.Tweens.Add(0.35f, k =>
            {
                float e = k * k;
                Vector2 p = (1 - e) * (1 - e) * a + 2 * (1 - e) * e * ctrl + e * e * b;
                lbl.style.left = p.x;
                lbl.style.top = p.y;
                lbl.style.opacity = 1f - Mathf.Max(0f, (k - 0.8f) * 5f);
            }, () => lbl.RemoveFromHierarchy(), 0.25f);
        }

        public static void KindStyle(HudContext c, ReportKind k, out string icon, out string tint)
        {
            bool ghostFamily = c.Run?.Villain != null && c.Run.Villain.IsGhost;
            switch (k)
            {
                case ReportKind.Score: icon = "ticket"; tint = "tint-audiencia"; break;
                case ReportKind.Plot: icon = "diamond"; tint = "tint-plot"; break;
                case ReportKind.PlotDone: icon = "diamond"; tint = "tint-plot"; break;
                case ReportKind.PlotBroken: icon = "diamond"; tint = "tint-alerta"; break;
                case ReportKind.Combo: icon = "heart"; tint = "tint-dupla"; break;
                case ReportKind.Pavor: icon = "drop"; tint = "tint-pavor"; break;
                case ReportKind.Crisis: icon = "panic"; tint = "tint-perigo"; break;
                case ReportKind.Villain: icon = ghostFamily ? "ghost" : "knife"; tint = ghostFamily ? "tint-fantasma" : "tint-perigo"; break;
                case ReportKind.Ghost: icon = "ghost"; tint = "tint-fantasma"; break;
                case ReportKind.Unlock: icon = "unlock"; tint = "tint-ok"; break;
                case ReportKind.Artefato: icon = "artefact"; tint = "tint-papel"; break;
                default: icon = "clapper"; tint = "tint-papel"; break;
            }
        }
    }
}
