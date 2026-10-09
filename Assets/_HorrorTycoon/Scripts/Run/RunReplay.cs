using System;
using System.Collections.Generic;
using System.Linq;

namespace HorrorTycoon.Run
{
    /// <summary>
    /// REPRODUZ uma run a partir do RunLog: mesma seed + mesma lista de ações = mesmo filme.
    /// Formato das ações (RunLog.actions):
    ///   move &lt;ator&gt; &lt;espaço&gt; · direct &lt;ator&gt; &lt;cena&gt; · tool &lt;ator&gt; · hold · holddoor &lt;ator&gt;
    ///   end-act · villain &lt;vilão&gt; · artefato &lt;artefato | -&gt;
    ///   draft-offer &lt;porta&gt; · draft &lt;porta&gt; &lt;opção&gt; &lt;ator&gt; (casa por escolha)
    /// Nomes podem ter espaço ("Final Girl"): o ator é achado pelo maior prefixo que bate.
    /// </summary>
    public static class RunReplay
    {
        /// <summary>Aplica as ações numa run nova (já com Begin()). Devolve quantas foram aplicadas.</summary>
        public static int Apply(FilmRun run, IEnumerable<string> actions)
        {
            int applied = 0;
            foreach (var raw in actions)
            {
                if (string.IsNullOrEmpty(raw)) continue;
                int sp = raw.IndexOf(' ');
                string verb = sp < 0 ? raw : raw.Substring(0, sp);
                string rest = sp < 0 ? "" : raw.Substring(sp + 1);

                switch (verb)
                {
                    case "move":
                    {
                        int last = rest.LastIndexOf(' ');
                        var actor = FindActor(run, rest.Substring(0, last));
                        run.Move(actor, int.Parse(rest.Substring(last + 1)));
                        break;
                    }
                    case "direct":
                    {
                        var actor = FindActor(run, rest, out string payoffName);
                        var payoff = run.AvailablePayoffs(actor).First(p => p.DisplayName == payoffName);
                        run.Direct(actor, payoff);
                        break;
                    }
                    case "tool":
                        run.UseTool(FindActor(run, rest));
                        break;
                    case "hold":
                        run.RecordScene();
                        break;
                    case "holddoor":
                        run.HoldDoor(FindActor(run, rest));
                        break;
                    case "draft-offer":
                        run.OpenDraft(int.Parse(rest));
                        break;
                    case "draft":
                    {
                        var parts = rest.Split(new[] { ' ' }, 3);
                        run.Draft(FindActor(run, parts[2]), int.Parse(parts[0]), int.Parse(parts[1]));
                        break;
                    }
                    case "end-act":
                        run.EndActEarly();
                        break;
                    case "villain":
                        run.ChooseVillain(run.Content.Villains.First(v => v != null && v.DisplayName == rest));
                        break;
                    case "artefato":
                        run.ChooseArtefato(rest == "-" ? null : run.ArtefatoOffer.First(a => a.DisplayName == rest));
                        break;
                    default:
                        throw new InvalidOperationException($"Ação desconhecida no log: {raw}");
                }
                applied++;
            }
            return applied;
        }

        private static ActorRunState FindActor(FilmRun run, string text) => FindActor(run, text, out _);

        /// <summary>Ator cujo nome é o MAIOR prefixo de 'text'; 'remainder' = o que sobra depois do nome.</summary>
        private static ActorRunState FindActor(FilmRun run, string text, out string remainder)
        {
            ActorRunState best = null;
            foreach (var a in run.Actors)
            {
                string n = a.Def.DisplayName;
                if (!text.StartsWith(n, StringComparison.Ordinal)) continue;
                if (text.Length > n.Length && text[n.Length] != ' ') continue;
                if (best == null || n.Length > best.Def.DisplayName.Length) best = a;
            }
            if (best == null) throw new InvalidOperationException($"Ator não encontrado no log: {text}");
            remainder = text.Length > best.Def.DisplayName.Length ? text.Substring(best.Def.DisplayName.Length + 1) : "";
            return best;
        }
    }
}
