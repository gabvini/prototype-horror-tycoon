using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using HorrorTycoon.Actors;
using HorrorTycoon.Run;
using HorrorTycoon.Scoring;
using UnityEngine;

namespace HorrorTycoon.UI
{
    // =====================================================================================================
    // HUD NOVA (UI Toolkit) — MODELO PURO.
    // Tudo aqui é C# sem UnityEngine.UIElements: lê o FilmRun e devolve dados simples para os painéis.
    // Por isso dá para testar em EditMode (Tests/HudModelTests.cs). Os textos em PT da HUD moram aqui.
    // Nada aqui muda regra de jogo: só consulta a API pública do FilmRun.
    // =====================================================================================================

    /// <summary>Cores semânticas em hex (para rich text). Espelham os tokens do HudTheme.uss.</summary>
    public static class HudColors
    {
        public const string Papel = "#F2EAD8";
        public const string Tinta = "#15121A";
        public const string Texto2 = "#B8AFC4";
        public const string Audiencia = "#FFD23F";
        public const string Ok = "#6ED37A";
        public const string Alerta = "#FF8A3D";
        public const string Perigo = "#E0323C";
        public const string Pavor = "#7C5CFF";
        public const string Fantasma = "#5CF2C2";
        public const string Plot = "#8CA6FF";
        public const string Dupla = "#FF9AD5";
        public const string Rec = "#FF3B3B";
    }

    /// <summary>Textos curtos e formatação (testáveis).</summary>
    public static class HudText
    {
        /// <summary>Selo de custo: GRÁTIS / 1 CENA / N CENAS.</summary>
        public static string Cost(int scenes) => scenes <= 0 ? "GRÁTIS" : scenes == 1 ? "1 CENA" : $"{scenes} CENAS";

        /// <summary>Algarismo romano do ato (0 → I).</summary>
        public static string Roman(int actIndex)
        {
            string[] r = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" };
            return actIndex >= 0 && actIndex < r.Length ? r[actIndex] : (actIndex + 1).ToString();
        }

        /// <summary>Tensão do fantasma em 4 palavras (informação parcial: sem número).</summary>
        public static string TensionWord(float ratio) =>
            ratio >= 0.8f ? "no limite" : ratio >= 0.5f ? "alta" : ratio >= 0.2f ? "subindo" : "baixa";

        /// <summary>Tira as tags de rich text (&lt;b&gt;, &lt;color&gt;...).</summary>
        public static string StripTags(string s) => string.IsNullOrEmpty(s) ? "" : Regex.Replace(s, "<.*?>", "");

        /// <summary>Quantos caracteres aparecem na tela (sem tags).</summary>
        public static int VisibleLength(string s) => StripTags(s).Length;

        // Símbolos que as fontes da HUD não têm (viram ícone nos painéis; no texto do relatório, somem).
        private const string MissingGlyphs = "★☆◆◇♥♡●○✓✔✕✗▸▶▼▲⚠✝≡■□▣◎◔◑◕✦";

        /// <summary>Remove símbolos sem glifo nas fontes (Lilita/Atkinson) e espaços duplicados.</summary>
        public static string StripSymbols(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                if (MissingGlyphs.IndexOf(c) >= 0) continue;
                if (c == '→') { sb.Append('>'); continue; }
                sb.Append(c);
            }
            string r = Regex.Replace(sb.ToString(), "  +", " ").Trim();
            return r;
        }

        /// <summary>
        /// Encurta uma linha do relatório para legenda (≤ max caracteres visíveis):
        /// tira símbolos; corta a explicação depois de " — " (ela vai para o histórico R);
        /// se ainda passar, corta na palavra e põe "…" (aí sem tags, para não deixar tag aberta).
        /// </summary>
        public static string Shorten(string rich, int max)
        {
            string s = StripSymbols(rich);
            if (VisibleLength(s) <= max) return s;
            int dash = s.IndexOf(" — ", System.StringComparison.Ordinal);
            if (dash > 0 && VisibleLength(s.Substring(0, dash)) >= 8)
            {
                s = s.Substring(0, dash);
                if (VisibleLength(s) <= max) return s;
            }
            string plain = StripTags(s);
            int cut = plain.LastIndexOf(' ', Mathf.Max(0, max - 1));
            if (cut < max / 2) cut = max - 1;
            return plain.Substring(0, cut).TrimEnd(',', ';', ':', '.', ' ') + "…";
        }

        /// <summary>Marcadores de progresso como texto (para tooltip/registro): ●●○ → "2/3".</summary>
        public static string Progress(int done, int total) => $"{Mathf.Min(done, total)}/{total}";

        public static string RoleName(ActorRole r) => r == ActorRole.FinalGirl ? "Final Girl" : r.ToString();

        public static string RoleNames(PlotDef def)
        {
            var parts = new List<string>();
            foreach (var r in def.RequiredRoles) parts.Add(RoleName(r));
            if (def.MinActors > 0) parts.Add($"{def.MinActors}+ atores");
            if (def.MaxActors == 1) parts.Add("sozinho(a)");
            return string.Join(" + ", parts);
        }

        /// <summary>Corta um nome para caber num rótulo (monitor: ≤ 10).</summary>
        public static string Clip(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max) return s ?? "";
            return s.Substring(0, Mathf.Max(1, max - 1)).TrimEnd() + "…";
        }

        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
    }

    /// <summary>Escala da HUD (§4.1): clamp(altura/1080, 0,75, 2) × escala do jogador.</summary>
    public static class HudScaleMath
    {
        public const float MinScale = 0.75f;
        public const float MaxScale = 2f;

        public static float Compute(float screenHeight, float playerScale)
        {
            float s = Mathf.Clamp(screenHeight / 1080f, MinScale, MaxScale);
            return s * Mathf.Clamp(playerScale, 0.5f, 2f);
        }
    }

    // ================================================================================== Relatório em beats

    public enum BeatSize
    {
        Small,  // linha simples
        Points, // com "+N" para a audiência
        Big     // momento grande (legenda maior, tremor)
    }

    /// <summary>Um beat da sequência do relatório (uma legenda), ou um gatilho dos medidores de pavor.</summary>
    public class HudBeat
    {
        public ReportKind Kind;
        public string Text;       // já encurtado para legenda (pode ter <b>)
        public string FullText;   // texto original (vai para o histórico R e o tooltip)
        public int Score;
        public BeatSize Size;
        public float Duration;    // segundos a 1×
        /// <summary>Linha de Pavor: não vira legenda; anima os anéis do elenco (duração 0 na fila).</summary>
        public bool PavorOnly;
    }

    /// <summary>Tempos da sequência (ajustáveis no Inspector do HudRoot).</summary>
    [System.Serializable]
    public class BeatTiming
    {
        [Tooltip("Beat simples (s, a 1×). A spec pede 0,35; subimos para dar tempo de ler uma frase.")]
        public float small = 0.9f;
        [Tooltip("Beat com pontos (s, a 1×).")]
        public float points = 1.05f;
        [Tooltip("Beat grande: plot cumprido, crise, Grande Susto, liberado, artefato (s, a 1×).")]
        public float big = 1.6f;
        [Tooltip("Tempo extra de leitura por caractere (s).")]
        public float perChar = 0.012f;
        [Tooltip("Teto da sequência inteira sem pular (s, a 1×). Passou: todos os beats encolhem.")]
        public float maxTotal = 8f;
        [Tooltip("Beat mínimo depois de encolher (s).")]
        public float minBeat = 0.5f;
        [Tooltip("Resumo final 'CENA 4 · +138' (s).")]
        public float summary = 1.2f;
        [Tooltip("Pontos a partir dos quais um beat comum vira grande.")]
        public int bigScore = 150;
        [Tooltip("Caracteres máximos de uma legenda.")]
        public int maxChars = 50;
    }

    public static class ReportBeats
    {
        /// <summary>Tipos que sempre são beat grande (§6.1).</summary>
        public static bool IsBigKind(ReportKind k) =>
            k == ReportKind.PlotDone || k == ReportKind.Crisis || k == ReportKind.Unlock || k == ReportKind.Artefato;

        /// <summary>Linhas do relatório → beats, na ORDEM do tique. Pavor vira gatilho (sem legenda); vazias somem.</summary>
        public static List<HudBeat> Build(IReadOnlyList<ReportLine> lines, BeatTiming t)
        {
            var list = new List<HudBeat>();
            if (lines == null) return list;
            bool pavorAdded = false;
            foreach (var l in lines)
            {
                if (l == null || string.IsNullOrWhiteSpace(HudText.StripTags(l.Text)) && l.Score <= 0) continue;
                if (l.Kind == ReportKind.Pavor)
                {
                    // Vários "Medo:" na mesma cena = um gatilho só (os anéis animam juntos).
                    if (!pavorAdded) list.Add(new HudBeat { Kind = ReportKind.Pavor, PavorOnly = true, FullText = l.Text, Text = "" });
                    else list[list.FindIndex(b => b.PavorOnly)].FullText += "\n" + l.Text;
                    pavorAdded = true;
                    continue;
                }
                var b = new HudBeat
                {
                    Kind = l.Kind,
                    FullText = l.Text,
                    Text = HudText.Shorten(l.Text, t.maxChars),
                    Score = l.Score
                };
                bool big = IsBigKind(l.Kind)
                           || (l.Kind == ReportKind.Ghost && l.Score > 0)
                           || (l.Kind == ReportKind.Villain && l.Score > 0)
                           || l.Score >= t.bigScore;
                b.Size = big ? BeatSize.Big : l.Score > 0 ? BeatSize.Points : BeatSize.Small;
                float baseT = b.Size == BeatSize.Big ? t.big : b.Size == BeatSize.Points ? t.points : t.small;
                b.Duration = baseT + HudText.VisibleLength(b.Text) * t.perChar;
                list.Add(b);
            }
            Compress(list, t);
            return list;
        }

        /// <summary>Se a soma passar do teto, encolhe tudo na mesma proporção (sem ficar abaixo do mínimo).</summary>
        public static void Compress(List<HudBeat> beats, BeatTiming t)
        {
            float total = 0f;
            foreach (var b in beats) total += b.Duration;
            if (total <= t.maxTotal || total <= 0f) return;
            float k = t.maxTotal / total;
            foreach (var b in beats)
            {
                if (b.PavorOnly) continue;
                b.Duration = Mathf.Max(t.minBeat, b.Duration * k);
            }
        }

        public static int TotalScore(IReadOnlyList<HudBeat> beats)
        {
            int s = 0;
            foreach (var b in beats) s += b.Score;
            return s;
        }
    }

    /// <summary>
    /// Máquina de estados da sequência do relatório (pura). O SubtitleReport chama Tick todo frame e lê
    /// Index / InSummary / Done. Pular: Skip() termina o beat atual; 2º Skip em menos de 0,4 s vai ao resumo.
    /// </summary>
    public class ReportSequencer
    {
        public const float DoubleSkipWindow = 0.4f;

        public List<HudBeat> Beats { get; private set; } = new List<HudBeat>();
        public int Index { get; private set; } = -1;
        public bool InSummary { get; private set; }
        public bool Done { get; private set; }
        public bool Running => Index >= -1 && !Done && started;
        /// <summary>Soma dos pontos dos beats que já começaram (o contador da audiência anda até aqui).</summary>
        public int ScoreShown { get; private set; }
        /// <summary>Um gatilho de pavor já passou (os anéis podem animar).</summary>
        public bool PavorReleased { get; private set; }
        public float SummaryDuration = 1.2f;

        private float t;
        private float lastSkip = -99f;
        private bool started;

        public void Start(List<HudBeat> beats, float summaryDuration)
        {
            Beats = beats ?? new List<HudBeat>();
            SummaryDuration = summaryDuration;
            Index = -1;
            InSummary = false;
            Done = false;
            ScoreShown = 0;
            PavorReleased = false;
            t = 0f;
            lastSkip = -99f;
            started = true;
            Advance();
        }

        public void Stop()
        {
            started = false;
            Done = true;
        }

        /// <summary>Avança o tempo (dt já multiplicado pela velocidade). Devolve true se trocou de beat/resumo.</summary>
        public bool Tick(float dt)
        {
            if (!started || Done) return false;
            t += dt;
            if (InSummary)
            {
                if (t >= SummaryDuration) Done = true;
                return Done;
            }
            if (Index >= 0 && Index < Beats.Count && t >= Beats[Index].Duration)
            {
                Advance();
                return true;
            }
            return false;
        }

        /// <summary>Clique/Espaço. now = tempo real (para o duplo clique).</summary>
        public void Skip(float now)
        {
            if (!started || Done) return;
            bool dbl = now - lastSkip < DoubleSkipWindow;
            lastSkip = now;
            if (InSummary)
            {
                Done = true;
                return;
            }
            if (dbl)
            {
                GoSummary();
                return;
            }
            Advance();
        }

        private void Advance()
        {
            t = 0f;
            Index++;
            // Gatilhos de pavor passam na hora (os anéis animam em paralelo).
            while (Index < Beats.Count && Beats[Index].PavorOnly)
            {
                PavorReleased = true;
                Index++;
            }
            if (Index >= Beats.Count)
            {
                GoSummary();
                return;
            }
            ScoreShown += Beats[Index].Score;
        }

        private void GoSummary()
        {
            Index = Beats.Count;
            InSummary = true;
            PavorReleased = true;
            ScoreShown = ReportBeats.TotalScore(Beats);
            t = 0f;
        }

        public HudBeat Current => Index >= 0 && Index < Beats.Count ? Beats[Index] : null;
    }

    // ================================================================================== View-models

    /// <summary>Uma linha de plot do Roteiro (Z3).</summary>
    public class PlotRowVM
    {
        public string Name;
        public string Room;
        public int Progress;
        public int Required;
        public bool Ready;      // condição cumprida agora
        public bool Completed;
        public string Tooltip;
        /// <summary>Prioridade de exibição: cumprida agora (2) &gt; em andamento (1) &gt; nova (0).</summary>
        public int Priority => Ready ? 2 : Progress > 0 ? 1 : 0;
    }

    public class ActionVM
    {
        public string Icon;        // classe do ícone (ex.: "ic-scare")
        public string Verb;        // "DIRIGIR SUSTO"
        public int Cost;           // cenas
        public string Line;        // 1 linha de consequência
        public bool Danger;
        public bool Enabled = true;
        public string Tooltip;
        public List<Color> Dots = new List<Color>();
        /// <summary>"direct" (dirigir cena), "tool" (usar ferramenta) ou "hold" (segurar a porta).</summary>
        public string Kind;
        public PayoffDef Payoff;
    }

    public static class HudModel
    {
        /// <summary>Até 3 plots ativos, por prioridade (cumprida agora &gt; andando &gt; nova); empate = ordem do roteiro.</summary>
        public static List<PlotRowVM> TopPlots(List<PlotRowVM> all, int max = 3)
        {
            var active = all.FindAll(p => !p.Completed);
            var ordered = new List<PlotRowVM>();
            for (int pr = 2; pr >= 0; pr--)
                foreach (var p in active)
                    if (p.Priority == pr) ordered.Add(p);
            if (ordered.Count > max) ordered.RemoveRange(max, ordered.Count - max);
            return ordered;
        }

        public static List<PlotRowVM> PlotRows(FilmRun run, bool includeDone)
        {
            var list = new List<PlotRowVM>();
            foreach (var p in run.Plots)
            {
                if (p.Status == PlotStatus.Failed) continue;
                if (p.Status == PlotStatus.Completed && !includeDone) continue;
                int room = run.PlotDisplayRoom(p);
                bool known = room >= 0 && run.Rooms[room].Discovered;
                var vm = new PlotRowVM
                {
                    Name = p.Def.DisplayName,
                    Room = known ? run.Rooms[room].Def.DisplayName : "?",
                    Progress = p.Progress,
                    Required = p.Status == PlotStatus.Completed ? p.Def.Scenes : run.RequiredScenes(p, room),
                    Completed = p.Status == PlotStatus.Completed,
                    Ready = p.IsActive && room >= 0 && run.PlotConditionMet(p, room)
                };
                vm.Tooltip = $"<b>{p.Def.DisplayName}</b>\n{p.Def.Description}\n<color={HudColors.Texto2}>Precisa: {HudText.RoleNames(p.Def)} · {vm.Required} cena(s) em {vm.Room}</color>"
                             + (string.IsNullOrEmpty(p.Def.RewardText) ? "" : $"\nRecompensa: {p.Def.RewardText}");
                list.Add(vm);
            }
            return list;
        }

        /// <summary>Ações do ator selecionado onde ele está (Z5). Sem ação = lista vazia.</summary>
        public static List<ActionVM> Actions(FilmRun run, ActorRunState actor)
        {
            var list = new List<ActionVM>();
            if (actor == null || !actor.Alive) return list;
            var rules = run.Content.Rules;
            var room = run.RoomOf(actor);
            var available = run.AvailablePayoffs(actor);

            if (room != null)
            {
                foreach (var p in room.Def.StagePayoffs)
                {
                    if (p == null) continue;
                    bool ok = available.Contains(p);
                    var vm = new ActionVM
                    {
                        Icon = p.KillsActor ? "ic-skull" : "ic-scare",
                        Verb = "DIRIGIR " + p.DisplayName.ToUpperInvariant(),
                        Cost = rules.directStepCost,
                        Danger = p.KillsActor,
                        Enabled = ok,
                        Kind = "direct",
                        Payoff = p,
                    };
                    if (!ok)
                    {
                        vm.Line = run.ActIndex < p.MinActIndex ? $"a partir do Ato {p.MinActIndex + 1}" : "sem cenas";
                        vm.Tooltip = $"<b>{p.DisplayName}</b>: {p.Description}\n<color={HudColors.Alerta}>Indisponível: {vm.Line}.</color>";
                    }
                    else
                    {
                        var counted = run.CountElements(actor, p);
                        foreach (var c in counted) vm.Dots.Add(c.Element.Color);
                        vm.Line = p.KillsActor ? "sai do filme"
                            : counted.Count == 0 ? "nada preparado"
                            : counted.Count == 1 ? "1 elemento pronto" : $"{counted.Count} elementos prontos";
                        var sb = new StringBuilder($"<b>{p.DisplayName}</b>: {p.Description}");
                        if (counted.Count > 0)
                        {
                            sb.Append("\nVai usar:");
                            foreach (var c in counted)
                                sb.Append($"\n  <color={HudText.Hex(c.Element.Color)}>{c.Element.DisplayName}</color>{(c.BoostedByVillain ? " (vilão: vale mais)" : "")}");
                        }
                        if (p.KillsActor) sb.Append($"\n<color={HudColors.Perigo}>{actor.Def.DisplayName} sai do filme.</color>");
                        vm.Tooltip = sb.ToString();
                    }
                    list.Add(vm);
                }
            }

            if (run.CanUseTool(actor) && room != null)
            {
                var tool = room.Def.Locked.requiredTool;
                var holder = run.ToolHolderFor(actor, tool);
                list.Add(new ActionVM
                {
                    Icon = "ic-key",
                    Kind = "tool",
                    Verb = "USAR " + tool.DisplayName.ToUpperInvariant(),
                    Cost = rules.toolStepCost,
                    Line = "abre: " + room.Def.Locked.name,
                    Tooltip = $"Abre o <b>{room.Def.Locked.name}</b> com {tool.DisplayName}"
                              + (holder != null && holder != actor ? $" (emprestado por {holder.Def.DisplayName}, que está junto)" : "") + "."
                });
            }

            if (run.CanHoldDoor(actor))
            {
                list.Add(new ActionVM
                {
                    Icon = "ic-doorhold",
                    Kind = "hold",
                    Verb = "SEGURAR A PORTA",
                    Cost = 0,
                    Line = "1× por ato",
                    Tooltip = "O vilão não entra nesta sala na próxima cena. Grátis, 1× por ato."
                });
            }
            return list;
        }

        /// <summary>Linha cinza da barra de ações quando não há ação (§3.3).</summary>
        public static string NoActionLine(FilmRun run, ActorRunState actor)
        {
            if (actor == null) return "";
            if (actor.IsLocked) return $"Em pânico: travado(a) por {actor.LockedScenes} cena(s). Grave uma cena para se recompor.";
            return "Nada para dirigir aqui · clique numa sala para mover (andar é grátis)";
        }

        /// <summary>Intenção do vilão em 1 linha (rich text), para o chip Z2. null = vilão fora da casa.</summary>
        public static string VillainIntent(FilmRun run, bool reveal)
        {
            var v = run.VillainNpc;
            if (v == null || !v.InHouse) return null;
            string Room(int i) => i >= 0 && i < run.Rooms.Count ? run.Rooms[i].Def.DisplayName : "?";
            if (v.IsGhost)
            {
                string next = v.NextSpace >= 0 && v.NextSpace != v.Space ? $" > {Room(v.NextSpace)}" : "";
                if (v.IsStunned) return $"se recompondo em <b>{Room(v.Space)}</b>";
                return $"assombra <b>{Room(v.Space)}</b>{next}";
            }
            if (v.IsStunned) return $"parado ({v.StunBeats} cena{(v.StunBeats > 1 ? "s" : "")})";
            string target = v.Target != null ? $" · caça <b>{v.Target.Def.DisplayName}</b>" : "";
            if (v.IsTelegraphing) return $"vai para <b>{Room(v.NextSpace)}</b>{target}";
            string where = reveal ? $"em <b>{Room(v.Space)}</b>" : "à espreita";
            return where + target;
        }
    }
}
