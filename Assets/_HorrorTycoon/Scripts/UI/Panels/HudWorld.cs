using System.Collections.Generic;
using HorrorTycoon.Run;
using UnityEngine;
using UnityEngine.UIElements;

namespace HorrorTycoon.UI
{
    // ================================================================================== Z9 Monitor

    /// <summary>
    /// Monitor do diretor (Z9): a RenderTexture do DirectorMonitor como fundo. A cada frame escreve
    /// monitor.GuiRect (pixels de tela) para o RunPresenter.TryGetRay continuar clicando na planta.
    /// Marcadores: 1 rótulo por sala, atores = bolinhas de 14 px, vilão = ícone vermelho, fantasma = moldura.
    /// </summary>
    public class MonitorPanel
    {
        public readonly VisualElement Root;
        private readonly VisualElement img, overlay, recDot;
        private Texture boundTexture;
        private readonly Dictionary<int, VisualElement> roomTags = new Dictionary<int, VisualElement>();
        private readonly Dictionary<ActorRunState, VisualElement> dots = new Dictionary<ActorRunState, VisualElement>();
        private VisualElement villainMark, nextFrame, ghostFrame, ghostMeterFill;

        public MonitorPanel(VisualElement parent, HudContext c)
        {
            Root = Ui.El("monitor panel col fadeable", parent, "Z9-Monitor");
            var head = Ui.El("row monitor-head", Root);
            recDot = Ui.Icon("rec", "ic-16 tint-rec", head);
            Ui.Lbl("MONITOR", "t-label monitor-title", head);
            Ui.El("grow", head);
            Ui.Lbl("clique na planta = clique na casa", "t-small t-dim monitor-sub", head);
            var close = Ui.OnClick(Ui.El("icon-btn", head), () => c.Monitor?.SetOpen(false));
            Ui.Icon("close", "ic-20", close);
            img = Ui.El("monitor-img", Root);
            overlay = Ui.El("monitor-overlay", img);
            overlay.pickingMode = PickingMode.Ignore;
            // Cantos de "safe frame".
            foreach (var cls in new[] { "sf-tl", "sf-tr", "sf-bl", "sf-br" }) Ui.El("safe-corner " + cls, overlay).pickingMode = PickingMode.Ignore;
        }

        /// <summary>Tamanho do monitor em px lógicos (cabe acima da Z6 e abaixo do Roteiro recolhido).</summary>
        public static Vector2 Size(HudContext c)
        {
            float aspect = c.Monitor != null ? c.Monitor.Aspect : 4f / 3f;
            float pw = c.Root.layout.width, ph = c.Root.layout.height;
            if (float.IsNaN(pw) || pw < 10f) { pw = 1920f; ph = 1080f; }
            float availH = ph - 112f - 24f - 64f - 52f;
            float w = Mathf.Min(560f, pw * 0.38f, availH * aspect);
            return new Vector2(w, w / aspect);
        }

        public void Update(HudContext c, bool visible, bool clickable)
        {
            var m = c.Monitor;
            if (m == null || !visible)
            {
                if (m != null) m.GuiRect = Rect.zero;
                return;
            }
            if (m.Texture != boundTexture && m.Texture is RenderTexture rt)
            {
                boundTexture = rt;
                img.style.backgroundImage = Background.FromRenderTexture(rt);
            }
            var size = Size(c);
            img.style.width = size.x;
            img.style.height = size.y;
            recDot.style.opacity = Mathf.Repeat(Time.unscaledTime, 1f) < 0.5f ? 1f : 0.25f;

            // Retângulo em pixels de tela (GUI, Y para baixo) para o TryGetRay.
            Rect wb = img.worldBound;
            float k = c.PanelToScreen;
            m.GuiRect = clickable && wb.width > 1f ? new Rect(wb.x * k, wb.y * k, wb.width * k, wb.height * k) : Rect.zero;

            UpdateMarkers(c, size);
        }

        private bool ToLocal(HudContext c, Vector3 world, Vector2 size, out Vector2 p)
        {
            p = default;
            var cam = c.Monitor.Camera;
            if (cam == null) return false;
            Vector3 v = cam.WorldToViewportPoint(world);
            if (v.z < 0f || v.x < 0f || v.x > 1f || v.y < 0f || v.y > 1f) return false;
            p = new Vector2(v.x * size.x, (1f - v.y) * size.y);
            return true;
        }

        private static void Place(VisualElement e, Vector2 p)
        {
            e.style.left = p.x;
            e.style.top = p.y;
        }

        private void UpdateMarkers(HudContext c, Vector2 size)
        {
            var run = c.Run;
            var pr = c.Presenter;
            var v = run.VillainNpc;

            for (int i = 0; i < run.Rooms.Count; i++)
            {
                var room = run.Rooms[i];
                if (!roomTags.TryGetValue(i, out var tag))
                {
                    tag = Ui.El("mon-room row", overlay);
                    tag.pickingMode = PickingMode.Ignore;
                    Ui.Icon("diamond", "ic-16 tint-plot mon-plot", tag);
                    Ui.Lbl("", "t-label mon-room-text", tag);
                    roomTags[i] = tag;
                }
                var anchor = pr.AnchorOf(i);
                bool show = anchor != null && (RunHud.ShowsLabel(room) || i == pr.CardRoom) && ToLocal(c, anchor.transform.position, size, out Vector2 p0);
                Ui.Display(tag, show);
                if (!show) continue;
                ToLocal(c, anchor.transform.position, size, out Vector2 p);
                Place(tag, p);
                ((Label)tag[1]).text = room.Discovered ? HudText.Clip(room.Def.DisplayName, 10) : "?";
                Ui.Display(tag[0], run.PlotsFor(i).Count > 0);
                tag.EnableInClassList("is-card", i == pr.CardRoom);
                tag.EnableInClassList("is-unknown", !room.Discovered);
            }

            foreach (var a in run.Actors)
            {
                if (!dots.TryGetValue(a, out var dot))
                {
                    dot = Ui.El("mon-dot", overlay);
                    dot.style.backgroundColor = a.Def.Color;
                    var actor = a;
                    c.Tooltip.Attach(dot, () => actor.Def.DisplayName);
                    dots[a] = dot;
                }
                var view = pr.ViewOf(a);
                bool show = a.Alive && view != null && ToLocal(c, view.transform.position, size, out Vector2 p0);
                Ui.Display(dot, show);
                if (!show) continue;
                ToLocal(c, view.transform.position, size, out Vector2 p);
                Place(dot, p);
                dot.EnableInClassList("is-selected", a == pr.Selected);
            }

            // Vilão: posição (se revelada), próximo espaço (moldura pulsando) e fantasma (moldura ectoplasma + medidor).
            if (villainMark == null)
            {
                nextFrame = Ui.El("mon-frame mon-next", overlay);
                nextFrame.pickingMode = PickingMode.Ignore;
                Ui.Icon("footprints", "ic-20 tint-perigo mon-frame-icon", nextFrame);
                ghostFrame = Ui.El("mon-frame mon-ghost", overlay);
                ghostFrame.pickingMode = PickingMode.Ignore;
                var meter = Ui.El("meter-track mon-ghost-meter", ghostFrame);
                ghostMeterFill = Ui.El("meter-fill", meter);
                villainMark = Ui.Icon("knife", "ic-24 tint-perigo mon-villain");
                overlay.Add(villainMark);
            }
            bool ghost = v != null && v.InHouse && v.IsGhost;
            Vector2 gp = default, np = default, vp = default;
            var gA = ghost ? pr.AnchorOf(v.Space) : null;
            bool showGhost = gA != null && ToLocal(c, gA.transform.position, size, out gp);
            Ui.Display(ghostFrame, showGhost);
            if (showGhost)
            {
                Place(ghostFrame, gp);
                ghostMeterFill.style.width = Length.Percent(run.GhostTensionRatio * 100f);
                ghostFrame.style.opacity = 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3f));
            }
            var nA = v != null && v.IsTelegraphing ? pr.AnchorOf(v.NextSpace) : null;
            bool showNext = nA != null && ToLocal(c, nA.transform.position, size, out np);
            Ui.Display(nextFrame, showNext);
            if (showNext)
            {
                Place(nextFrame, np);
                nextFrame.EnableInClassList("is-ghost", ghost);
                nextFrame.style.opacity = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5f));
            }
            var vv = pr.VillainViewObject;
            bool showV = !ghost && v != null && v.InHouse && pr.RevealVillain && vv != null && ToLocal(c, vv.transform.position, size, out vp);
            Ui.Display(villainMark, showV);
            if (showV) Place(villainMark, vp);
        }
    }

    // ================================================================================== Modo filme

    /// <summary>Faixas pretas (letterbox), cantos de visor e ● REC (modo filme, §3.8).</summary>
    public class FilmFrame
    {
        private readonly VisualElement top, bottom, corners, rec;
        private float letter;

        public FilmFrame(VisualElement layer)
        {
            top = Ui.El("letterbox letterbox-top", layer);
            bottom = Ui.El("letterbox letterbox-bottom", layer);
            corners = Ui.El("viewfinder", layer);
            foreach (var cls in new[] { "vf-tl", "vf-tr", "vf-bl", "vf-br" }) Ui.El("vf-corner " + cls, corners);
            rec = Ui.El("rec-badge row", corners);
            Ui.Icon("rec", "ic-24 tint-rec", rec);
            Ui.Lbl("REC", "t-display rec-text", rec);
            foreach (var e in new[] { top, bottom, corners, rec }) e.pickingMode = PickingMode.Ignore;
            corners.AddToClassList("is-off");
        }

        /// <summary>letterbox: faixas (sequência ou planos de filme). film: visor + REC (idle cinematográfico).</summary>
        public void Update(HudContext c, bool letterbox, bool film)
        {
            float target = letterbox ? 1f : 0f;
            letter = Mathf.MoveTowards(letter, target, Time.unscaledDeltaTime / 0.3f);
            float h = c.Root.layout.height;
            if (float.IsNaN(h)) h = 1080f;
            float bar = h * 0.08f * HudTweens.EaseInOut(letter);
            top.style.height = bar;
            bottom.style.height = bar;
            Ui.Fade(corners, film, 0.4f);
            corners.style.top = bar + 36f;
            corners.style.bottom = bar + 24f;
            rec.style.opacity = Mathf.Repeat(Time.unscaledTime, 1f) < 0.5f ? 1f : 0.2f; // pisca a 1 Hz
        }
    }

    // ================================================================================== Marcadores no mundo

    /// <summary>
    /// Marcadores de TELA ancorados no mundo (§5), tamanho constante. Regras anti-poluição:
    /// 1 marcador por sala fora do hover (vilão > plot > porta); nomes de sala só na visão da casa,
    /// no hover ou com o card aberto; anúncio do vilão SEMPRE (no modo filme fica só o chão);
    /// vilão fora da tela = seta na borda. Nada aqui é clicável (o clique passa para o mundo).
    /// </summary>
    public class WorldMarkers
    {
        private readonly VisualElement layer;
        private readonly Dictionary<int, VisualElement> rooms = new Dictionary<int, VisualElement>();
        private readonly Dictionary<ActorRunState, VisualElement> nameTags = new Dictionary<ActorRunState, VisualElement>();
        private readonly Dictionary<ActorRunState, RingElement> rings = new Dictionary<ActorRunState, RingElement>();
        private readonly List<VisualElement> hearts = new List<VisualElement>();
        private VisualElement telegraph, ghostMeter, ghostFill, edgeArrow;
        private Label ghostWord, telegraphText;

        public WorldMarkers(VisualElement layer)
        {
            this.layer = layer;
            layer.pickingMode = PickingMode.Ignore;
        }

        private VisualElement Make(string cls)
        {
            var e = Ui.El(cls, layer);
            e.pickingMode = PickingMode.Ignore;
            return e;
        }

        private static void Place(VisualElement e, Vector2 p)
        {
            e.style.left = p.x;
            e.style.top = p.y;
        }

        public void Update(HudContext c, bool visible)
        {
            Ui.Display(layer, visible);
            if (!visible) return;
            var run = c.Run;
            var pr = c.Presenter;
            var v = run.VillainNpc;
            bool overview = c.Director == null || c.Director.ShowWorldLabels;
            bool idle = c.Phase == RunPresenter.Phase.Idle;
            bool film = c.FilmMode;
            bool calm = idle && !film;

            // ---------------- salas
            for (int i = 0; i < run.Rooms.Count; i++)
            {
                var room = run.Rooms[i];
                if (!rooms.TryGetValue(i, out var tag))
                {
                    tag = Make("wm-room col");
                    var row = Ui.El("row wm-room-row", tag);
                    row.pickingMode = PickingMode.Ignore;
                    Ui.Icon("door", "ic-24 wm-room-icon", row);
                    Ui.Lbl("", "t-display wm-room-name", row).pickingMode = PickingMode.Ignore;
                    var pips = Ui.El("row plot-pips wm-pips", tag);
                    pips.pickingMode = PickingMode.Ignore;
                    rooms[i] = tag;
                }
                bool hover = i == pr.HoveredRoom;
                bool card = i == pr.CardRoom;
                bool corridor = !RunHud.ShowsLabel(room);
                bool show = calm && (overview || hover || card) && (!corridor || hover);
                var anchor = pr.AnchorOf(i);
                Vector2 p = default;
                show = show && anchor != null && c.WorldToPanel(anchor.transform.position + Vector3.up * 0.4f, out p);
                Ui.Display(tag, show);
                if (!show) continue;
                Place(tag, p);

                var rowEl = tag[0];
                var iconEl = rowEl[0];
                var nameEl = (Label)rowEl[1];
                var pipsEl = tag[1];
                var plots = run.PlotsFor(i);
                // 1 marcador por sala: plot > porta (o vilão tem marcador próprio).
                string icon = null, tint = "papel";
                if (plots.Count > 0) { icon = "diamond"; tint = "plot"; }
                else if (room.Sealed) { icon = "lock"; tint = "alerta"; }
                else if (!room.Discovered) { icon = "door"; tint = "dim"; }
                Ui.Display(iconEl, icon != null);
                if (icon != null) { Ui.SetIcon(iconEl, icon); Ui.SetTint(iconEl, tint); }
                nameEl.text = corridor ? "Passagem" : room.Discovered ? room.Def.DisplayName : (room.Sealed ? "Trancada" : "?");
                Ui.Display(nameEl, hover || card || room.Discovered || corridor);
                tag.EnableInClassList("is-hover", hover || card);
                Ui.Display(pipsEl, plots.Count > 0 && (overview || hover));
                if (plots.Count > 0)
                {
                    var best = plots[0];
                    foreach (var pl in plots) if (pl.Progress > best.Progress) best = pl;
                    Pips.Set(pipsEl, best.Progress, run.RequiredScenes(best, i), "ppip");
                }
            }

            // ---------------- anúncio do vilão (sempre; modo filme = só o chão)
            if (telegraph == null)
            {
                telegraph = Make("wm-telegraph col");
                Ui.Icon("footprints", "ic-48 tint-perigo", telegraph);
                telegraphText = Ui.Lbl("", "t-label wm-telegraph-text", telegraph);
                telegraphText.pickingMode = PickingMode.Ignore;
                ghostMeter = Make("wm-ghost col");
                var track = Ui.El("meter-track wm-ghost-track", ghostMeter);
                ghostFill = Ui.El("meter-fill", track);
                ghostWord = Ui.Lbl("", "t-label wm-ghost-word", ghostMeter);
                edgeArrow = Make("wm-edge");
                Ui.Icon("arrow", "ic-40 tint-perigo", edgeArrow);
            }
            bool inHouse = v != null && v.InHouse && run.Status == RunStatus.Playing;
            bool showTele = inHouse && !v.IsGhost && v.IsTelegraphing && !film && c.Phase != RunPresenter.Phase.ShowingResult;
            Vector2 tp = default;
            if (showTele)
            {
                Vector3 door = pr.DoorBetween(v.Space, v.NextSpace) + Vector3.up * 1.6f;
                showTele = c.WorldToPanel(door, out tp);
            }
            Ui.Display(telegraph, showTele);
            if (showTele)
            {
                Place(telegraph, tp);
                telegraphText.text = run.Rooms[v.NextSpace].Discovered ? run.Rooms[v.NextSpace].Def.DisplayName.ToUpperInvariant() : "PASSOS";
                telegraph.style.scale = new Scale(Vector2.one * (1f + 0.06f * Mathf.Sin(Time.unscaledTime * 5f)));
            }

            bool showGhost = inHouse && v.IsGhost && calm;
            Vector2 gp = default;
            var ga = showGhost ? pr.AnchorOf(v.Space) : null;
            showGhost = ga != null && c.WorldToPanel(ga.transform.position + Vector3.up * 2.4f, out gp);
            Ui.Display(ghostMeter, showGhost);
            if (showGhost)
            {
                Place(ghostMeter, gp);
                ghostFill.style.width = Length.Percent(run.GhostTensionRatio * 100f);
                ghostFill.EnableInClassList("is-max", run.GhostTensionRatio >= 0.8f);
                ghostWord.text = "TENSÃO " + HudText.TensionWord(run.GhostTensionRatio).ToUpperInvariant();
            }

            // ---------------- seta na borda: vilão fora da tela
            var vv = pr.VillainViewObject;
            bool arrow = false;
            if (inHouse && !v.IsGhost && pr.RevealVillain && vv != null && calm && pr.MainCamera != null)
            {
                var cam = pr.MainCamera;
                Vector3 sp = cam.WorldToScreenPoint(vv.transform.position + Vector3.up);
                bool behind = sp.z < 0f;
                bool off = behind || sp.x < 0 || sp.x > Screen.width || sp.y < 0 || sp.y > Screen.height;
                if (off)
                {
                    Vector2 center = new Vector2(Screen.width, Screen.height) * 0.5f;
                    Vector2 dir = new Vector2(sp.x, sp.y) - center;
                    if (behind) dir = -dir;
                    if (dir.sqrMagnitude < 1f) dir = Vector2.up;
                    float sx = (Screen.width * 0.5f - 60f) / Mathf.Max(1f, Mathf.Abs(dir.x));
                    float sy = (Screen.height * 0.5f - 60f) / Mathf.Max(1f, Mathf.Abs(dir.y));
                    Vector2 edge = center + dir * Mathf.Min(sx, sy);
                    Vector2 panel = c.ScreenToPanel(edge);
                    Place(edgeArrow, panel);
                    float ang = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg; // 0 = para cima
                    edgeArrow.style.rotate = new Rotate(Angle.Degrees(ang));
                    arrow = true;
                }
            }
            Ui.Display(edgeArrow, arrow);

            // ---------------- atores: etiqueta do selecionado + anel no chão (selecionado, pavor alto, na mira)
            float limit = Mathf.Max(1, run.Content.Rules.pavorLimit);
            foreach (var a in run.Actors)
            {
                if (!nameTags.TryGetValue(a, out var tag))
                {
                    tag = Make("wm-name");
                    Ui.Lbl(a.Def.DisplayName, "t-bold wm-name-text", tag).pickingMode = PickingMode.Ignore;
                    nameTags[a] = tag;
                    var ring = new RingElement { Thickness = 4f };
                    ring.AddToClassList("wm-ring");
                    layer.Add(ring);
                    rings[a] = ring;
                }
                var view = pr.ViewOf(a);
                bool sel = a == pr.Selected;
                bool hunted = v != null && v.InHouse && v.Target == a;
                float r = a.Pavor / limit;
                Vector2 hp = default, fp = default;
                bool showTag = a.Alive && view != null && sel && !film && c.Phase == RunPresenter.Phase.Idle
                               && c.WorldToPanel(view.transform.position + Vector3.up * 2.3f, out hp);
                Ui.Display(tag, showTag);
                if (showTag) Place(tag, hp);
                var rg = rings[a];
                bool showRing = a.Alive && view != null && (sel || r >= 0.75f || hunted) && (!film || hunted) && c.Phase == RunPresenter.Phase.Idle
                                && c.WorldToPanel(view.transform.position, out fp);
                Ui.Display(rg, showRing);
                if (showRing)
                {
                    Place(rg, fp);
                    Color col = hunted ? new Color(0.878f, 0.196f, 0.235f) : r >= 0.75f ? new Color(1f, 0.541f, 0.239f) : new Color(0.486f, 0.361f, 1f);
                    rg.Set(r, col);
                }
            }

            // ---------------- duplas ativas: ♥ entre os atores (visão da casa e hover)
            var combos = run.AllActiveCombos();
            int h = 0;
            foreach (var cb in combos)
            {
                if (!calm || !(overview || cb.Space == pr.HoveredRoom)) break;
                Vector3 mid = Vector3.zero;
                int n = 0;
                foreach (var a in cb.Actors)
                {
                    var view = pr.ViewOf(a);
                    if (view == null) continue;
                    mid += view.transform.position;
                    n++;
                }
                if (n == 0 || !c.WorldToPanel(mid / n + Vector3.up * 2.6f, out Vector2 hp)) continue;
                while (hearts.Count <= h)
                {
                    var e = Make("wm-heart");
                    Ui.Icon("heart", "ic-24 tint-dupla", e);
                    hearts.Add(e);
                }
                Ui.Display(hearts[h], true);
                Place(hearts[h], hp);
                h++;
            }
            for (int i = h; i < hearts.Count; i++) Ui.Display(hearts[i], false);
        }
    }
}
