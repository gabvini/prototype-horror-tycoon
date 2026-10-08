using System;
using System.Collections.Generic;
using HorrorTycoon.Cameras;
using HorrorTycoon.Run;
using UnityEngine;
using UnityEngine.UIElements;

namespace HorrorTycoon.UI
{
    // =====================================================================================================
    // HUD NOVA — peças comuns: atalhos para montar elementos, animações simples (tween), tooltip,
    // contexto compartilhado pelos painéis e elementos desenhados por código (anel de pavor, listras).
    // =====================================================================================================

    /// <summary>Atalhos para montar a árvore em C# (sem UXML).</summary>
    public static class Ui
    {
        public static VisualElement El(string classes = null, VisualElement parent = null, string name = null)
        {
            var e = new VisualElement();
            if (name != null) e.name = name;
            AddClasses(e, classes);
            parent?.Add(e);
            return e;
        }

        public static Label Lbl(string text, string classes = null, VisualElement parent = null)
        {
            var l = new Label(text) { enableRichText = true };
            AddClasses(l, classes);
            parent?.Add(l);
            return l;
        }

        /// <summary>Ícone SVG: icon = "knife" ou "ic-knife"; extra = tamanho/tinta ("ic-20 tint-perigo").</summary>
        public static VisualElement Icon(string icon, string extra = null, VisualElement parent = null)
        {
            var e = new VisualElement();
            e.AddToClassList("ic");
            if (!string.IsNullOrEmpty(icon)) e.AddToClassList(icon.StartsWith("ic-") ? icon : "ic-" + icon);
            AddClasses(e, extra);
            e.pickingMode = PickingMode.Ignore;
            parent?.Add(e);
            return e;
        }

        /// <summary>Troca o ícone de um elemento criado por Icon().</summary>
        public static void SetIcon(VisualElement e, string icon)
        {
            var remove = new List<string>();
            foreach (var c in e.GetClasses()) if (c.StartsWith("ic-") && !IsSizeClass(c)) remove.Add(c);
            foreach (var c in remove) e.RemoveFromClassList(c);
            if (!string.IsNullOrEmpty(icon)) e.AddToClassList(icon.StartsWith("ic-") ? icon : "ic-" + icon);
        }

        private static bool IsSizeClass(string c) => c.Length <= 6 && char.IsDigit(c[c.Length - 1]);

        /// <summary>Troca a classe de tinta (tint-*) de um ícone.</summary>
        public static void SetTint(VisualElement e, string tint)
        {
            var remove = new List<string>();
            foreach (var c in e.GetClasses()) if (c.StartsWith("tint-")) remove.Add(c);
            foreach (var c in remove) e.RemoveFromClassList(c);
            if (!string.IsNullOrEmpty(tint)) e.AddToClassList(tint.StartsWith("tint-") ? tint : "tint-" + tint);
        }

        public static void AddClasses(VisualElement e, string classes)
        {
            if (string.IsNullOrEmpty(classes)) return;
            foreach (var c in classes.Split(' '))
                if (c.Length > 0) e.AddToClassList(c);
        }

        /// <summary>Clique (ClickEvent) que respeita a classe is-disabled.</summary>
        public static T OnClick<T>(T e, Action action) where T : VisualElement
        {
            e.AddToClassList("is-clickable");
            e.RegisterCallback<ClickEvent>(ev =>
            {
                if (e.ClassListContains("is-disabled") || !e.enabledInHierarchy) return;
                ev.StopPropagation();
                action?.Invoke();
            });
            return e;
        }

        public static void Display(VisualElement e, bool show) =>
            e.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;

        /// <summary>
        /// Some/aparece com fade (classe is-off = opacidade 0 com transição do USS). Depois do fade-out
        /// o elemento fica invisível de verdade (visibility hidden) para não receber clique.
        /// </summary>
        public static void Fade(VisualElement e, bool show, float outSeconds = 0.4f)
        {
            bool isOff = e.ClassListContains("is-off");
            if (show)
            {
                if (!isOff && e.style.visibility.keyword == StyleKeyword.Null) return;
                e.RemoveFromClassList("is-off");
                e.style.visibility = StyleKeyword.Null;
                return;
            }
            if (isOff) return;
            e.AddToClassList("is-off");
            e.schedule.Execute(() =>
            {
                if (e.ClassListContains("is-off")) e.style.visibility = Visibility.Hidden;
            }).StartingIn((long)(outSeconds * 1000f));
        }

        public static bool IsShown(VisualElement e) =>
            !e.ClassListContains("is-off") && e.resolvedStyle.display != DisplayStyle.None;

        public static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);
    }

    /// <summary>Animações curtas por código (tempo real, não para com pausa de jogo).</summary>
    public class HudTweens
    {
        private class Tween
        {
            public float Delay, Duration, T;
            public Action<float> Apply;
            public Action Done;
            public object Owner;
        }

        private readonly List<Tween> list = new List<Tween>();

        /// <summary>apply recebe 0..1 (já com "ease out"). owner != null: substitui a animação anterior do mesmo dono.</summary>
        public void Add(float duration, Action<float> apply, Action done = null, float delay = 0f, object owner = null)
        {
            if (owner != null) list.RemoveAll(t => t.Owner == owner);
            list.Add(new Tween { Delay = delay, Duration = Mathf.Max(0.0001f, duration), Apply = apply, Done = done, Owner = owner });
        }

        public void Update(float dt)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (i >= list.Count) continue;
                var t = list[i];
                if (t.Delay > 0f)
                {
                    t.Delay -= dt;
                    continue;
                }
                t.T += dt;
                float k = Mathf.Clamp01(t.T / t.Duration);
                t.Apply?.Invoke(k);
                if (k >= 1f)
                {
                    list.Remove(t);
                    t.Done?.Invoke();
                }
            }
        }

        public static float EaseOut(float k) => 1f - (1f - k) * (1f - k);
        public static float EaseInOut(float k) => k * k * (3f - 2f * k);
    }

    /// <summary>
    /// Tooltip único da HUD (§4.4): aparece em 0,35 s e cola no item (não no mouse). Passando de um item
    /// a outro em menos de 0,5 s, o novo aparece na hora.
    /// </summary>
    public class HudTooltip
    {
        public float Delay = 0.35f;
        public float ChainWindow = 0.5f;

        private readonly VisualElement box;
        private readonly Label text;
        private VisualElement target;
        private Func<string> provider;
        private float showAt;
        private float hiddenAt = -99f;
        private bool shown;

        public HudTooltip(VisualElement layer)
        {
            box = Ui.El("tooltip", layer);
            box.pickingMode = PickingMode.Ignore;
            text = Ui.Lbl("", "tooltip-text", box);
            text.pickingMode = PickingMode.Ignore;
            Ui.Display(box, false);
        }

        public void Attach(VisualElement e, string content) => Attach(e, () => content);

        public void Attach(VisualElement e, Func<string> content)
        {
            if (e == null || content == null) return;
            e.RegisterCallback<PointerEnterEvent>(_ =>
            {
                target = e;
                provider = content;
                float now = Time.unscaledTime;
                showAt = now - hiddenAt < ChainWindow ? now : now + Delay;
            });
            e.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                if (target != e) return;
                Hide();
            });
            e.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                if (target == e) Hide();
            });
        }

        public void Hide()
        {
            if (shown) hiddenAt = Time.unscaledTime;
            target = null;
            provider = null;
            shown = false;
            Ui.Display(box, false);
        }

        public void Update(VisualElement root)
        {
            if (target == null) return;
            if (target.panel == null || target.resolvedStyle.visibility == Visibility.Hidden || !target.enabledInHierarchy)
            {
                Hide();
                return;
            }
            if (!shown && Time.unscaledTime >= showAt)
            {
                string s = provider?.Invoke();
                if (string.IsNullOrEmpty(s)) return;
                text.text = s;
                Ui.Display(box, true);
                shown = true;
            }
            if (!shown) return;

            // Posição: acima do item; sem espaço, embaixo. Preso à tela.
            Rect wb = target.worldBound;
            float w = box.resolvedStyle.width;
            float h = box.resolvedStyle.height;
            if (float.IsNaN(w) || w <= 1f) { w = 360f; h = 80f; }
            float sw = root.layout.width, sh = root.layout.height;
            float x = Mathf.Clamp(wb.center.x - w * 0.5f, 8f, Mathf.Max(8f, sw - w - 8f));
            float y = wb.yMin - h - 10f;
            if (y < 8f) y = Mathf.Min(wb.yMax + 10f, sh - h - 8f);
            box.style.left = x;
            box.style.top = y;
        }
    }

    /// <summary>Opções do jogador (PlayerPrefs). Velocidade do relatório e tamanho da HUD.</summary>
    public class HudSettings
    {
        private const string KeySpeed = "HT.Hud.ReportSpeed";
        private const string KeyScale = "HT.Hud.PlayerScale";

        public int ReportSpeed = 1;      // 1×, 2×, 3×
        public float PlayerScale = 1f;   // 0,8 – 1,5

        public void Load()
        {
            ReportSpeed = Mathf.Clamp(PlayerPrefs.GetInt(KeySpeed, 1), 1, 3);
            PlayerScale = Mathf.Clamp(PlayerPrefs.GetFloat(KeyScale, 1f), 0.8f, 1.5f);
        }

        public void Save()
        {
            PlayerPrefs.SetInt(KeySpeed, ReportSpeed);
            PlayerPrefs.SetFloat(KeyScale, PlayerScale);
        }
    }

    /// <summary>Tudo que os painéis precisam ler/usar, montado pelo HudRoot a cada frame.</summary>
    public class HudContext
    {
        public RunPresenter Presenter;
        public CinematicDirector Director;
        public DirectorMonitor Monitor;
        public ActorFocusController Focus;
        public HudTooltip Tooltip;
        public HudTweens Tweens;
        public HudSettings Settings;
        public BeatTiming Timing;

        public VisualElement Root;     // .ht-root
        public VisualElement World;    // marcadores no mundo
        public VisualElement Film;     // faixas, visor, REC
        public VisualElement Hud;      // zonas
        public VisualElement Modal;    // modais
        public VisualElement Top;      // tooltip / menu

        public FilmRun Run => Presenter != null ? Presenter.Run : null;
        public RunPresenter.Phase Phase => Presenter.CurrentPhase;

        // ---- estado da HUD (calculado pelo HudRoot)
        public bool FilmMode;          // modo filme (idle cinematográfico)
        public bool Sequence;          // encenação/relatório (Busy ou ShowingResult)
        public bool PhaseModal;        // fim de ato, artefato, vilão, fim
        public bool OverlayOpen;       // menu/confirmação/registro abertos por cima da HUD
        public bool MonitorVisible;
        public float DisplayedScore;   // o contador da audiência (anima)
        public bool PavorHeld;         // os anéis do elenco esperam o beat de pavor
        /// <summary>Número da cena mostrada (a que vai ser / está sendo filmada). Congela durante a sequência.</summary>
        public int SceneShown = 1;
        /// <summary>Capturas/testes: congela o relatório no beat atual.</summary>
        public bool DebugFreeze;

        /// <summary>Converte posição de tela (Y para cima, Input System) para coordenadas do painel.</summary>
        public Vector2 ScreenToPanel(Vector2 screenYUp)
        {
            var p = Root.panel;
            if (p == null) return Vector2.zero;
            return RuntimePanelUtils.ScreenToPanel(p, new Vector2(screenYUp.x, Screen.height - screenYUp.y));
        }

        /// <summary>Pixels de tela por px lógico.</summary>
        public float PanelToScreen => Root.layout.width > 1f ? Screen.width / Root.layout.width : 1f;

        /// <summary>Mundo → painel pela câmera principal. false = atrás da câmera.</summary>
        public bool WorldToPanel(Vector3 world, out Vector2 panelPos)
        {
            panelPos = default;
            var cam = Presenter != null ? Presenter.MainCamera : null;
            if (cam == null || Root.panel == null) return false;
            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z <= 0f) return false;
            panelPos = RuntimePanelUtils.ScreenToPanel(Root.panel, new Vector2(sp.x, Screen.height - sp.y));
            return true;
        }
    }

    /// <summary>Anel de pavor (0–1) desenhado com Painter2D: trilho + arco a partir do topo.</summary>
    public class RingElement : VisualElement
    {
        public float Value;
        public float Thickness = 6f;
        public Color Fill = new Color(0.49f, 0.36f, 1f);
        public Color Track = new Color(1f, 1f, 1f, 0.14f);

        public RingElement()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public void Set(float value, Color fill)
        {
            value = Mathf.Clamp01(value);
            if (Mathf.Abs(value - Value) < 0.001f && fill == Fill) return;
            Value = value;
            Fill = fill;
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect;
            float radius = Mathf.Min(r.width, r.height) * 0.5f - Thickness * 0.5f;
            if (radius <= 1f) return;
            var c = r.center;
            var p = ctx.painter2D;
            p.lineWidth = Thickness;
            p.lineCap = LineCap.Butt;
            p.strokeColor = Track;
            p.BeginPath();
            p.Arc(c, radius, Angle.Degrees(0f), Angle.Degrees(360f));
            p.Stroke();
            if (Value <= 0.001f) return;
            p.strokeColor = Fill;
            p.lineCap = LineCap.Round;
            p.BeginPath();
            p.Arc(c, radius, Angle.Degrees(-90f), Angle.Degrees(-90f + 360f * Value));
            p.Stroke();
        }
    }

    /// <summary>Faixa listrada da claquete (Tinta/Papel a 45°).</summary>
    public class StripeElement : VisualElement
    {
        public Color A = new Color(0.082f, 0.071f, 0.102f);
        public Color B = new Color(0.949f, 0.918f, 0.847f);
        public float Stripe = 22f;

        public StripeElement()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        private void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect;
            var p = ctx.painter2D;
            p.fillColor = A;
            p.BeginPath();
            p.MoveTo(new Vector2(0, 0));
            p.LineTo(new Vector2(r.width, 0));
            p.LineTo(new Vector2(r.width, r.height));
            p.LineTo(new Vector2(0, r.height));
            p.ClosePath();
            p.Fill();
            p.fillColor = B;
            float h = r.height;
            for (float x = -h; x < r.width + h; x += Stripe * 2f)
            {
                p.BeginPath();
                p.MoveTo(new Vector2(x, h));
                p.LineTo(new Vector2(x + Stripe, h));
                p.LineTo(new Vector2(Mathf.Min(x + Stripe + h, r.width + h), 0));
                p.LineTo(new Vector2(x + h, 0));
                p.ClosePath();
                p.Fill();
            }
        }
    }

    /// <summary>Pip redondo (cheio/vazio). Usado nas cenas da claquete e no progresso dos plots.</summary>
    public static class Pips
    {
        public static void Set(VisualElement row, int filled, int total, string cls)
        {
            while (row.childCount < total) Ui.El(cls, row);
            while (row.childCount > total) row.RemoveAt(row.childCount - 1);
            for (int i = 0; i < total; i++) row[i].EnableInClassList("is-full", i < filled);
        }
    }
}
