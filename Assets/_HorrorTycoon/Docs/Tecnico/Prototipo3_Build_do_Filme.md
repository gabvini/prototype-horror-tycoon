# Protótipo 3 — Build do Filme (cena como turno, plots, classes, Fantasma, crise, artefatos)

> Status: 🧪 implementado para playtest (07/10/2026). Fonte: `Build_do_Filme_v1.md` (todas as seções) + GDD §1.1.
> Regra de ouro mantida: tudo é parâmetro em dados e cada mecânica desliga sozinha (checkbox em `Regras`).
> Cenas P0 (casa fixa) e P2 (casa gerada) funcionam. Testes: **55 EditMode** (37 antigos + 18 novos), todos verdes.

---

## 1. Arquivos

| Arquivo | O que mudou |
|---|---|
| `Scripts/Run/FilmRun.cs` | Vira `partial`. Ações gastam **cenas** e rodam o tique da casa (`Scenes(n)`). Popular/artefatos na pontuação de encontros e cenas dirigidas. Encontro ao entrar no espaço do vilão só para o Slasher. Oferta de artefato no fim do ato. Sala lacrada não aceita ninguém. |
| `Scripts/Run/FilmRun.Build.cs` *(novo)* | Todo o Protótipo 3: tique da casa, plots, combos, "perto do vilão", pavor por cena, passivos, crise, Fantasma, artefatos, "Gravar cena", "Segurar a porta", relatório da cena. |
| `Scripts/Run/BuildModels.cs` *(novo)* | `PlotRunState`, `SceneReport`/`ReportLine`/`ReportKind`, `GhostScareOutcome`, `CrisisEvent`, `ActiveCombo`. |
| `Scripts/Run/PlotDef.cs` *(novo)* | Plot de permanência (condição, cenas, recompensa, quebra). |
| `Scripts/Run/DuoComboDef.cs` *(novo)* | Cena de dupla / combo de elenco. |
| `Scripts/Run/ArtefatoDef.cs` *(novo)* | Artefato = lista de efeitos genéricos (`ArtefatoEffectType`). |
| `Scripts/Run/RunReplay.cs` *(novo)* | Reproduz uma run a partir de seed + `RunLog.actions`. |
| `Scripts/Run/RunModels.cs` | Ator: crises, travado, determinada, porta segurada, motivo de saída (`ExitReason`). Sala: `Sealed`, `PlotsOffered`. `MoveOutcome.Points`, `VillainEncounterOutcome.Blocked`, `ActOutcome.OffersArtefato`, `VillainMoveReason.Haunt`. |
| `Scripts/Run/VillainAgent.cs` | Família, `Tension`, `Scares`; alvo com **isca** (Popular) depois de "sozinho". Fantasma não planeja caça. |
| `Scripts/Run/VillainDef.cs` | `VillainFamily` (Auto/Slasher/Fantasma). Auto = Fantasma se `look = Ghost`. |
| `Scripts/Actors/ActorDef.cs` | `ActorRole` (Atleta, Nerd, FinalGirl, Popular), `roleTitle`, `passiveText`, `plots`. |
| `Scripts/Rooms/RoomDef.cs` | `pavorPerScene`, `plots`, `startsSealed`, `sealedText`. ⚠️ Foi junto para o Unity a versão local que já tinha os campos da casa por escolha (P2 §4, `HouseZone`/`DoorSides`), que ainda não estavam lá: só campos novos com padrão neutro. |
| `Scripts/Core/GameRulesDef.cs` | Seções **P3 ·** (ver §4). Valores novos de pavor/fantasma calibrados pela simulação. |
| `Scripts/Core/GameContentDef.cs` | Listas `duoCombos` e `artefatos`. |
| `Scripts/Core/FilmFormatDef.cs` | Só o texto: `steps` = **cenas** por ato. |
| `Scripts/Core/AssemblyInfo.cs` *(novo)* | `InternalsVisibleTo("HorrorTycoon.Tests")` (os testes montam situações exatas). |
| `Scripts/Run/RunPresenter.cs` | Fase `ArtefatoChoice`; `RecordScene()`, `HoldDoor()`, `ChooseArtefato()`; relatório da cena no popup; encena Grande Susto e crise; anúncio roxo do Fantasma. |
| `Scripts/Run/VillainTelegraph.cs` | `SetTint` e `ShowArea` (Fantasma: só a área da próxima sala, sem pegadas). |
| `Scripts/Actors/VillainView.cs` | Família Fantasma usa o lençol; luz fria em volta do fantasma. |
| `Scripts/UI/RunHud.cs` | "Cenas", painel **ROTEIRO**, Elenco com papel/crise, card "Se ficar aqui por uma cena…", Fantasma (sala + barra de tensão), botões "Gravar cena ▶" e "Segurar a porta", tela "Escolha 1 artefato", marcadores no monitor. |
| `Scripts/Editor/ContentBuilder.cs` | Upgrade idempotente `UpgradePrototype3` (ver §5). |
| `Scripts/Tests/Prototipo3Tests.cs` *(novo)* | 18 testes (ver §6). `RunLogicTests`/`Prototipo2Tests`: adaptados (ver §6). |
| `Tools/SimP3/` *(fora do Unity)* | Simulação de balanceamento (ver §7). |

As cenas **não precisam** ser reconstruídas: tudo novo é criado em Play (boneco/anúncio do vilão) ou desenhado pela HUD.

---

## 2. Regras como implementadas

### 2.1 A cena é o turno
- O recurso do ato chama **Cenas** (`FilmFormatDef.acts[i].steps`, 8). Internamente continua `ActionsLeft` (alias `ScenesLeft`).
- **Andar é grátis** (convivência, corredor, sala já explorada no ato): é posicionamento, **não** roda o tique.
- **Gasta 1 cena** (cada uma roda 1 tique da casa):
  - explorar sala ainda não explorada no ato (`exploreActionCost`) — *Exploração*;
  - **Gravar cena aqui** (`recordSceneCost`): ninguém se mexe, a casa conta a cena — *Permanência* e *Plot*;
  - dirigir cena (`directStepCost`) — *Dirigida*;
  - usar ferramenta no ponto trancado (`toolStepCost`) — *Ferramenta*.
- "Encerrar ato" e "Segurar a porta" **não** gastam cena e não rodam o tique.

### 2.2 Tique da casa — ordem FIXA (para cada cena gasta)
1. **(a)** A cena escolhida acontece (encontro/plot oferecido, payoff, ferramenta). Entrar no espaço do **Slasher** = encontro na hora (P2).
2. **(b) Cada ator vivo conta 1 cena onde está:**
   1. **b1 Plots**: avançam (`+1`, `+bônus` de combo) ou quebram; completos dão a recompensa.
   2. **b2 Cenas de dupla** pontuam; atores **perto do vilão** rendem audiência.
   3. **b3 Pavor por cena**: sala (`RoomDef.pavorPerScene`) + sozinho + perto do vilão; depois a **Final Girl acalma** quem está com ela. (A crise espera o b4.)
   4. **b4 Crise**: quem chegou ao máximo entra em crise.
3. **(c) O vilão age** pela família: Slasher anda 1 espaço anunciado (pode ser barrado pela porta); Fantasma enche a tensão / Grande Susto.
4. **Fim do tique**: "porta segurada" vence; plots revalidados (o vilão pode ter tirado alguém); travas de crise descontam 1 (menos as criadas nesta ação).
- Coberto pelo teste `Tique_OrdemFixa_PlotAntesDaCrise_CriseAntesDoVilao` (ordem das entradas no log).

### 2.3 Pontuação = audiência (direto)
| Fonte | Onde | Fórmula |
|---|---|---|
| Exploração | encontro | `pontos × Popular × artefato ExploreScoreMult` |
| Plot cumprido | b1 | `rewardPoints × mult. do ato × artefato PlotRewardMult × perto do vilão (1,5) × Popular` |
| Cena de dupla | b2 | `scorePerScene × mult. do ato × mult. do espaço (Grupo 0,5) × perto (1,5) × Popular × artefato ComboScoreMult` |
| Perto do vilão | b2 | por ator: `nearVillainScenePoints × mult. do ato × mult. do espaço × Popular × artefato NearVillainScoreMult` |
| Cena dirigida / pega | ação/(c) | P2 `base × mult. do ato × (1 + soma)` `× Popular × artefato Scare/DeathScoreMult` |
| Grande Susto | (c) | `ghostScorePerTension × tensão × mult. do ato × Σ(1 + pavor/100) dos atores lá dentro × artefato ScareScoreMult × Popular` |
| Crise (cena forçada) | b4 | `crisisPanicScore × mult. do ato × Popular` |
| Final Girl testemunha | morte | `finalGirlWitnessScore × mult. do ato` |
- **Perto do vilão** = no espaço dele ou vizinho (`nearVillainIncludesAdjacent`). Na estrela do P0 o corredor é vizinho de todas as salas.
- O multiplicador "perto" **não** vale para cenas dirigidas (a pega acontece sempre no espaço do vilão).

### 2.4 Plots de permanência
- **De onde vêm:** `RoomDef.plots` (oferecidos na **1ª exploração da sala na run**) e `ActorDef.plots` (oferecidos no começo da run). Papel que não existe no elenco = o plot nem aparece. Cada plot só uma vez por run.
- **Condição:** papéis exigidos (todos) + mín./máx. de atores + sala (a que ofereceu, ou a lista do PlotDef; plot de ator sem lista = qualquer sala).
- **Conta a partir da próxima cena** (`plotStartsNextScene`): achado na exploração, a cena da exploração não conta. Plot de ator começa valendo já na 1ª cena.
- **Avanço:** `+1` por cena com a condição cumprida, `+plotSpeedBonus` das cenas de dupla da sala (Investigação = +1).
- **Cenas necessárias:** `scenes`, menos `nerdPlotReduction` se for investigação e o Nerd estiver na sala (mínimo 1).
- **Quebra** (progresso > 0): alguém sai da sala (na hora, mesmo andando grátis), o ator é pego/morre/sai, ou entra em crise. `failOnBreak = false` (padrão): zera e dá para recomeçar; `true`: fracassa. Se não existe mais ninguém com o papel exigido: **fracassa**.
- **Recompensa:** audiência + liberar **sala lacrada** (`unlockRoom`), ferramenta, artefato (fixo ou sorteado), elemento (no 1º participante ou na sala), pavor (negativo acalma).
- **"Ações liberam salas" (exemplo real):** o **Porão começa LACRADO** (`startsSealed`); o plot **"O diário do Sótão"** (Final Girl, 2 cenas, investigação) libera o Porão (e o Alçapão/Machado e o palco de Morte dele).

### 2.5 Classes (passivos, `passivesEnabled`)
| Papel | Passivo implementado |
|---|---|
| **Atleta** (tanque) | Aliados no mesmo espaço ganham pavor × 0,5 (ele mesmo não). **Segurar a porta** (1× por ato, grátis): na PRÓXIMA cena o vilão não entra no espaço dele (Slasher fica parado; Fantasma escolhe outra sala). Vale se o Atleta continuar lá. |
| **Nerd** (mago) | Plots de investigação: −1 cena com ele na sala. Ganha pavor × 1,5. |
| **Final Girl** (suporte) | −10 de pavor por cena em cada aliado no mesmo espaço. **Testemunhar uma morte** (no espaço dela ou vizinho): pavor zera, +60 × ato e fica **determinada** (pavor × 0,5 pelo resto da run). |
| **Popular** (isca) | Tudo que a envolve rende × 1,25 (exploração, cena dirigida, combo/plot/susto com ela, "perto"). **Isca:** o Slasher a prefere como alvo depois de "sozinho" (sozinho → isca → pavor → distância). |
- Modificadores de pavor só valem para GANHO; ordem: artefato × Nerd × determinada × Atleta junto; arredonda no fim (`Mathf.RoundToInt`, 0,5 vai para o par).

### 2.6 Cenas de dupla (`combosEnabled`)
| Combo | Condição | Efeito |
|---|---|---|
| **Casal** | Atleta + Popular na mesma **sala** | +30 por cena |
| **Investigação** | Nerd + Final Girl na mesma **sala** | +10 por cena e plots dali +1 por cena |
| **Grupo** | 3+ no mesmo espaço (qualquer) | audiência por cena do espaço × 0,5 e **o vilão não ataca** (susto em grupo do Slasher sem pavor) |

### 2.7 Vilão por família
- **Slasher** (Mascarado): igual ao P2, agora **1 batida por cena**. Pode ser barrado pela porta segurada.
- **Fantasma** (Entidade): não persegue nem pega ninguém (entrar na sala dele não faz nada).
  - Entra no Ato 2 numa sala sem atores; **anuncia a próxima sala** (sorteada, fluxo próprio da seed).
  - **Cada cena:** tensão `+ (2 + 2 × atores lá dentro) × artefato`.
  - **Tensão ≥ 10:** **GRANDE SUSTO** em quem estiver na sala (fórmula §2.3, +25 de pavor em cada) e muda para a sala anunciada. Ninguém lá = susto no vazio (0).
  - **Crucifixo** (ferramenta que combate o Sobrenatural) na sala assombrada no passo (c): **exorciza** (gasta o crucifixo, +80 × ato, tensão zera, muda de sala, fica 1 cena parado).
  - "Luz" não existe como sistema: **não implementado**.
- Oferta no fim do Ato 1: continua oferecendo **os 2 vilões que existem** (o mais ligado à build primeiro).

### 2.8 Crise de pavor (`crisisEnabled`)
- Pavor no máximo → **crise**: trava `crisisLockScenes` (1) cena (não anda; ainda conta para plots), **cena forçada de pânico** no relatório (+20 × ato), pavor volta para **60**, quebra plots em andamento com ele.
- **2ª crise** (`crisesToLeave`) → sai do filme (fugiu; não é morte).
- Trava: vale da decisão seguinte até o fim da próxima cena gravada.
- Desligado = regra antiga (100 = sai do filme na hora).

### 2.9 Artefatos (Jokers v1)
- `ArtefatoDef` = lista de efeitos: `ComboScoreMult` (todas ou uma combo), `PlotRewardMult`, `ScareScoreMult`, `DeathScoreMult`, `GhostTensionGainMult`, `PavorGainMult`, `NearVillainScoreMult`, `ExtraScenesPerAct`, `ExploreScoreMult`. Multiplicadores se multiplicam; cenas extras se somam (valem a partir do próximo ato).
- **Entre atos**: tela de fim de ato → **"Escolha 1 artefato"** (3 sorteados, sem repetir os que já tem, grátis, dá para pular) → vilão (fim do Ato 1) ou próximo ato. Fluxo aleatório próprio da seed.
- Também vêm de plots (o mistério do Nerd dá 1 sorteado).

### 2.10 Relatório da cena
- Cada ação monta um `SceneReport` (linhas com tipo e audiência). O popup existente mostra: o resultado da ação → as linhas do vilão (P2) → **"— Relatório da cena —"** (plot novo/avanço/cumprido/quebrou, combos, "perto do vilão", medo, crise, Fantasma, liberações, artefatos).

### 2.11 Log e replay
- Ações novas no `RunLog.actions`: `hold`, `holddoor <ator>`, `artefato <nome|->`. `RunReplay.Apply(run, actions)` reproduz a run (teste `Determinismo_SeedMaisLogReproduzARun`: mesma pontuação, mesmas entradas de texto).

---

## 3. HUD (IMGUI, informação parcial)
- **Topo:** Cenas N/M, pontos e meta, vilão; Fantasma: sala assombrada → próxima + **barra de tensão** (sem número).
- **Elenco:** papel ("tanque", "mago"…), **CRISE: travado(a)**, crises n/2, ★ determinada, porta disponível/usada.
- **ROTEIRO** (direita): plots com marcadores ●○ (cenas), ✓ cumpridos; cenas de dupla ativas; artefatos. Com o monitor aberto e pouco espaço, vira linha-resumo (ou some).
- **Card da sala:** "**Se ficar aqui por uma cena…**" (plots daqui com marcadores e "condição cumprida", combos ativos, lugar escuro/perigoso, sozinho, fantasma/tensão, perto do vilão); sala trancada; "porta segurada"; botões "Quem vai?" mostram "em pânico — travado(a)" e "trancada".
- **Painel do ator:** "Gravar cena ▶" (sempre), "Segurar a porta" (Atleta), passivo do ator.
- **Monitor do diretor:** ◆ ●○ nos plots, quadro roxo + barra no quarto assombrado, "▼ próxima".
- **Fim de ato:** aviso "A seguir: escolha 1 de 3 artefatos". **Escolha de vilão:** família de cada um.

---

## 4. Onde ajustar (Inspector)

| O quê | Onde | Padrão |
|---|---|---|
| Cenas por ato / metas | `Data/P1/Formato_Curta` → acts[i].steps / goal | 8 / **400 · 2.000 · 4.000** |
| Custo de "Gravar cena" | `Regras` → recordSceneCost | 1 |
| Pontos por cena usam mult. do ato | `Regras` → tickScoreUsesActMultiplier | ligado |
| Plots: liga / conta da próxima cena / mult. do ato | `Regras` → plotsEnabled / plotStartsNextScene / plotUsesActMultiplier | ligado / ligado / ligado |
| Pavor por cena: sozinho / perto do vilão | `Regras` → alonePavorPerScene / nearVillainPavorPerScene | 2 / 4 |
| Perto do vilão: vizinhos / pontos por ator / mult. | `Regras` → nearVillainIncludesAdjacent / nearVillainScenePoints / nearVillainScoreMult | ligado / 10 / 1,5 |
| Combos: liga | `Regras` → combosEnabled | ligado |
| Passivos: liga + números | `Regras` → passivesEnabled, atletaAllyPavorMult, atletaHoldDoorPerAct, nerdPlotReduction, nerdPavorMult, finalGirlCalmPerScene, finalGirlWitnessScore, finalGirlDeterminedPavorMult, popularSceneMult, popularIsBait | ligado, 0,5, 1, 1, 1,5, 10, 60, 0,5, 1,25, ligado |
| Crise | `Regras` → crisisEnabled, crisisResetPavor, crisisLockScenes, crisesToLeave, crisisPanicScore | ligado, 60, 1, 2, 20 |
| Fantasma | `Regras` → ghostTensionMax, ghostTensionPerScene, ghostTensionPerActor, ghostScorePerTension, ghostScarePavor, ghostRepelStunScenes | 10, 2, 2, 5, 25, 1 |
| Artefatos entre atos | `Regras` → artefatoOfferEnabled / artefatoOfferCount | ligado / 3 |
| Pavor por cena de cada sala / lacrada | `Salas/*` → pavorPerScene / startsSealed / sealedText | Porão 6 (lacrado), Sótão 4, Banheiro 3 |
| Plots de cada sala / de cada ator | `Salas/*` → plots / `Atores/*` → plots | ver §5 |
| Papel do ator | `Atores/*` → role, roleTitle, passiveText | ver §5 |
| Família do vilão | `Viloes/*` → family | Mascarado: Slasher · Entidade: Fantasma |
| Plots / combos / artefatos | `Plots/*`, `Combos/*`, `Artefatos/*`; catálogo `Conteudo_P1` → duoCombos / artefatos | ver §5 |

---

## 5. Conteúdo (upgrade idempotente no `ContentBuilder`)

*Horror Tycoon > Criar Conteúdo P1 (não sobrescreve)*. Cria só o que falta e preenche só listas vazias / papel `None` / família `Auto`. O que não dá para distinguir de uma edição do Gabriel (pavor das salas, Porão lacrado, descrições, metas) é aplicado **uma vez só**: quando `Plots/Plot_CasalQuarto` ainda não existia.

- **Plots (5):** *Casal no Quarto* (Atleta + Popular, 1 cena, 120) · *O diário do Sótão* (Final Girl, 2 cenas, investigação, 100, **libera o Porão**) · *O mistério da casa* (do **Nerd**, Sala de estar, 2 cenas de investigação = 1 com ele, 90 + **artefato sorteado**) · *Conversa na Cozinha* (2+ atores, 1 cena, 60, −15 de pavor) · *A lenda do espelho* (Popular **sozinha** no Banheiro, 1 cena, 100 + elemento Presença).
- **Combos (3):** Casal, Investigação, Grupo (§2.6).
- **Artefatos (8):** Trilha de violinos (sustos +50%) · Câmera na mão (duplas ×1,5) · Roteiro amarrado (plots +50%) · Máquina de neblina (tensão do fantasma +50%) · Chá de camomila no set (−25% de pavor) · Close no vilão (perto do vilão ×2) · Sangue cenográfico (mortes +50%) · Hora extra da equipe (+1 cena por ato).
- **Atores:** Atleta = tanque, Nerd = mago (+ plot do mistério), Popular = isca, Final Girl = suporte.
- **Vilões:** Mascarado → Slasher, Entidade → Fantasma (descrições atualizadas uma vez).
- **Salas:** Porão 6 de pavor/cena e **lacrado**; Sótão 4; Banheiro 3.
- **Metas:** 400 / 2.000 / 4.000 (§7).

---

## 6. Como testar

**Automático:** Test Runner > EditMode > Run All (**55**).
- Novos (`Prototipo3Tests`): andar grátis × gravar cena; **ordem do tique** (plot → crise → vilão); plot do Casal (achado na exploração, conta da próxima cena, pontos com combo); plot do diário (quebra ao sair, recomeça, **libera o Porão**); plot quebra na crise e fracassa sem o ator; Nerd (−1 cena, ×1,5 pavor; plot de ator); Atleta (metade do pavor; **segurar a porta** barra 1 cena); Final Girl (acalma; testemunha morte); Popular (×1,25; isca); Investigação (+1 por cena) e Grupo (×0,5, bloqueia ataque); perto do vilão (pontos e pavor); Fantasma (tensão, Grande Susto, muda para a sala anunciada); Crucifixo exorciza e entrar na sala dele não mata; crise (trava 1 cena, volta, 2ª sai) e crise desligada; oferta de artefato entre atos (antes do vilão, sem repetir, pular, último ato sem oferta); efeitos genéricos; **determinismo + replay pelo log**.
- Adaptados (com o motivo no `SetUp`): `RunLogicTests` e `Prototipo2Tests` desligam o pavor por cena (sozinho/perto) e os pontos "perto do vilão" para medir só as regras P1/P2 com números exatos. Nenhum teste antigo mudou de expectativa.

**Em Play (P0):** Atleta explora o Quarto → plot "Casal" aparece; leve a Popular (grátis) → ♥ Casal; **Gravar cena** → plot cumprido. Final Girl no Sótão + 2× Gravar cena → Porão liberado. Nerd na Sala de estar → mistério + artefato. Fim do Ato 1 → escolha 1 de 3 → vilão. Com a Entidade: monitor mostra a sala assombrada e a barra; junte gente lá e grave cenas → Grande Susto.
**Em Play (P2):** idem; com o Mascarado, Atleta "Segurar a porta" quando o anúncio aponta para a sala dele.

Capturas: `Art/Previews/Unity/60_…` a `68_…` (60 HUD/ROTEIRO/card · 61 diário cumprido libera o Porão · 62 crise do Nerd · 63 escolha de artefato · 64 Fantasma assombrando + tensão + monitor · 65 Grande Susto · 66 P2 card "Se ficar aqui" · 67 P2 Slasher anunciado + "Segurar a porta" · 68 P2 Slasher barrado).
Nas capturas em Play foram usados atalhos de teste por reflexão (pavor 99 no Nerd para forçar a crise; pontos 450 no P2 para pular a meta do Ato 1).

---

## 7. Simulação e metas (valores INICIAIS — precisam de playtest)

`Tools/SimP3/` (fora do Unity): roda o **FilmRun real** com um espelho do conteúdo do `ContentBuilder` (`dotnet run -- sim 2000 400 2000 4000`). Dois bots, 2.000 runs cada:
- **Heurístico:** tira quem está sozinho do caminho do Slasher, leva os papéis certos aos plots (andar grátis), junta o casal, põe gente com pouco pavor na sala do Fantasma antes do susto, tira quem está com pavor alto; grava cena enquanto um plot avança; dirige cenas com elementos; explora preferindo salas com plot para o papel.
- **Aleatório:** 0–2 movimentos grátis e uma ação de cena qualquer.

| Pontuação acumulada (P25 / P50) | Fim Ato 1 | Fim Ato 2 | Fim Ato 3 |
|---|---|---|---|
| Heurístico | 687 / 749 | 2.862 / 3.810 | 3.757 / 4.765 |
| Aleatório | 438 / 556 | 673 / 2.025 | 673 / 3.569 |

**Metas escolhidas: 400 / 2.000 / 4.000.** Passa o filme: heurístico **70%** (Mascarado 62%, Entidade 90%), aleatório **28%** (Mascarado 11%, Entidade 43%). Ato 1 quase trivial para quem joga bem (100%) e ainda falha 18% no aleatório (pacing §4.1).
Ajustes feitos por causa da simulação: pavor sozinho 3→2, perto do vilão 6→4, Grande Susto 35→25 de pavor, volta da crise 70→60; o Fantasma **sorteia** a próxima sala (antes ia para onde havia mais atores e dava sustos "de graça") e o susto soma por ator `(1 + pavor/100)`.

---

## 8. Desvios da proposta (e por quê)

1. **"Cena de plot" não é uma ação separada:** é o "Gravar cena aqui" enquanto um plot avança (o relatório diz "Plot X: 1/2"). Diálogos de verdade ficam para quando houver conteúdo de câmera/texto.
2. **Plot achado na exploração só conta da próxima cena** (parâmetro): senão os plots de 1 cena de 1 ator se cumpririam sozinhos ao entrar. Plot de ator (mistério do Nerd) conta já na cena em que ele chega (é o "1 cena" da proposta).
3. **Sala liberada = Porão lacrado** (não uma sala nova): o P0 tem 6 vagas fixas na planta; uma 7ª sala exigiria refazer a cena. O Porão continua no sorteio das duas plantas, começa trancado e o diário do Sótão abre.
4. **Grupo = "o vilão não ataca"** (susto em grupo sem pavor) — no P2 o Slasher já não pega quem está com alguém; o Grupo precisava de um efeito próprio.
5. **"Perto do vilão"** = pontos por ator por cena + ×1,5 em combos/plots; não vale para cenas dirigidas.
6. **Fantasma:** próxima sala **sorteada e anunciada** (a proposta não define; ir atrás do grupo dava pontos sem decisão). Tensão passa do máximo (o excesso conta no susto). **Luz:** não existe sistema → não implementado. Crucifixo **exorciza** (gasta e muda de sala) em vez de só reduzir.
7. **Bônus da Final Girl** ao testemunhar = +60 × ato e "determinada" (metade do pavor pelo resto da run) — a proposta só diz "ganha bônus".
8. **Crise** reseta para **60** (proposta: 70) por causa da simulação (com 70 havia ~4 crises por run).
9. **Artefatos já entraram** (a proposta deixava para depois), como pedido: escolha grátis entre atos, sem loja/moeda.
10. **Popular "+audiência"** = ×1,25 em tudo que a envolve (um número só).

---

## 9. Limitações conhecidas
- HUD em telas pequenas (1100×602 do Game view): card da sala e painel do ator se sobrepõem (já existia); com o monitor aberto o ROTEIRO vira uma linha ou some.
- O Fantasma com o Grande Susto + ninguém morrendo deixa o filme **mais fácil** que com o Slasher (90% × 62% no bot). Ajustar `ghostScorePerTension` no playtest.
- As cenas dirigidas (P1/P2) ainda são a maior fonte de pontos nos Atos 2–3; plots/combos são ~25–35%.
- A tensão do Fantasma pode passar bastante do máximo com muita gente (o susto fica enorme, ex.: +776 no Ato 2).
- O total de cenas do ato só inclui "Hora extra" pega antes do ato começar.
- Sem animação de pânico (só a expressão "assustado"); "travado" só na HUD.

---

## 10. Perguntas para o Gabriel
1. Plot achado numa exploração deve contar a cena da própria exploração? (hoje não — parâmetro `plotStartsNextScene`.)
2. Plot quebrado: recomeçar do zero (hoje) ou fracassar de vez (`failOnBreak`)?
3. "Ações liberam salas": vale o Porão lacrado como modelo, ou quer salas especiais NOVAS (exige vaga na planta do P0 / pool da casa gerada)?
4. Fantasma: próxima sala sorteada (hoje) ou "onde está o grupo"? O susto deve ter teto?
5. Grupo de 3+: "o vilão não ataca" é o efeito certo?
6. Crise: a cena forçada de pânico deve dar audiência (hoje +20 × ato)?
7. Metas 400 / 2.000 / 4.000 e o Fantasma mais fácil que o Slasher: equilibrar pelo vilão ou pela meta?
8. Artefatos: 1 de 3 grátis entre atos é o formato, ou loja com dinheiro (GDD §7.7)?
