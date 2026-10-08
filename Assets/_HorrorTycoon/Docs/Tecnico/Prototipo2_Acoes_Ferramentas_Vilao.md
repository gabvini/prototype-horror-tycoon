# Protótipo 2 — Ações, ferramentas com o ator e vilão NPC (seções 1–3)

> Status: 🧪 implementado para playtest (07/10/2026). Fonte: `Prototipo_2_Proposta.md`, seções 1, 2 e 3.
> Regra de ouro mantida: tudo é parâmetro em dados. O vilão NPC desliga com um checkbox (`villainNpcEnabled`).
> Cenas P0 (casa fixa) e P2 (casa gerada) funcionam. Testes: 37 EditMode (24 antigos + 13 novos), todos verdes.

---

## 1. Arquivos

| Arquivo | O que mudou |
|---|---|
| `Scripts/Core/GameRulesDef.cs` | Novos parâmetros: ações, ferramentas por ator e todo o vilão NPC (ver §5). Campos antigos (`directStepCost`, `toolStepCost`) mantidos com o mesmo nome para não perder valores salvos. |
| `Scripts/Run/FilmRun.cs` | Custo de movimento novo; ferramentas por ator; batidas; vilão NPC (entrada, anúncio, encontros); `ActionsLeft` (antigo `StepsLeft`). Eventos `VillainMoved` e `VillainEncountered`. |
| `Scripts/Run/VillainAgent.cs` *(novo)* | Estado do vilão (espaço, próximo espaço, alvo, batidas parado) + decisões puras: `Hops`, `NextHop`, `ChooseTarget`, `Plan`. |
| `Scripts/Run/RunModels.cs` | `ActorRunState.Tools`, `RoomRunState.FloorTools`, campos novos em `MoveOutcome`/`ToolOutcome`, classes `VillainMoveEvent` e `VillainEncounterOutcome`. |
| `Scripts/Run/ToolDef.cs` | Campo `counters` (TagDef do subgênero que a ferramenta combate) e `Repels(vilão)`. |
| `Scripts/Run/VillainDef.cs` | Campo `look` (Auto/Mascarado/Fantasma), só visual. |
| `Scripts/Run/RunPresenter.cs` | Guarda os eventos do vilão durante a ação e encena depois (anda, encontro, some/recua); popup com as linhas do vilão; cria o boneco e o anúncio; `DoorBetween` (porta entre dois espaços nas duas plantas). |
| `Scripts/Run/VillainTelegraph.cs` *(novo)* | Marca do anúncio: brilho pulsando no chão do próximo espaço, pegadas + seta na porta, ponta de seta flutuando por cima das paredes. |
| `Scripts/Actors/VillainView.cs` *(novo)* | Boneco provisório por código (Mascarado: corpo escuro + máscara; Fantasma: lençol). NavMeshAgent, `SeeThroughSubject.isVillain = true`. |
| `Scripts/UI/RunHud.cs` | "Passos" → "Ações"; "Quem vai?" mostra **grátis** / **1 ação (explora)**; ferramentas por ator no Elenco; "● na mira"; avisos no card; vilão e anúncio no monitor e no mapa. |
| `Scripts/Editor/ContentBuilder.cs` | Upgrade idempotente `UpgradePrototype2` (ver §4). |
| `Scripts/Tests/Prototipo2Tests.cs` *(novo)* | 13 testes (ver §6). |
| `Scripts/Tests/RunLogicTests.cs`, `HouseGenTests.cs` | 4 testes adaptados às regras novas (ver §6). |

As cenas **não precisam** de nada novo: o boneco do vilão e o anúncio são criados em Play pelo `RunPresenter` quando o vilão entra. As duas cenas foram reconstruídas mesmo assim (menus *Construir Cena Greybox* e *Construir Cena Casa Procedural*).

---

## 2. Regras implementadas

### 2.1 Ações
- Recurso por ato: **Ações** (`FilmFormatDef.acts[i].steps`, padrão 8; o nome do campo ficou `steps`).
- **Andar é grátis** para convivências, corredor do P0, e salas **já exploradas neste ato**.
- **1 ação** (`exploreActionCost`) para entrar numa sala ainda não explorada no ato (gera o encontro).
- Dirigir = `directStepCost` (1). Usar ferramenta = `toolStepCost` (1).
- `ActorDef.DoorsPerStep` continua no dado, **sem efeito** (a HUD não mostra mais).
- O ato acaba quando as ações chegam a 0 **depois** da ação (e das batidas dela), ou por "Encerrar ato" (sem batida).
- Continua valendo: sem caminho → não move; corredor da casa gerada não é destino.

### 2.2 Ferramentas com o ator
- A ferramenta achada vai para **quem achou**, até `maxToolsPerActor` (2). Mãos cheias → fica no **chão da sala** (`RoomRunState.FloorTools`).
- Quem **entra** numa sala com espaço nas mãos pega o que está no chão (grátis, ao entrar).
- **Atores juntos compartilham**: usar ferramenta vale se ele ou **qualquer ator vivo no mesmo espaço** tem a ferramenta (`FilmRun.ToolHolderFor`). O alçapão (Chave de fenda) segue igual; a chave **não** é gasta.
- Ator que sai do filme (morte, pego, pavor no limite) **larga as ferramentas no chão** do espaço onde estava.
- Ferramenta única em jogo: encontro que **só** dá uma ferramenta já em jogo sai do sorteio (como antes). Encontro com elemento + ferramenta (Faca na gaveta → Arma + Faca) continua saindo, mas não dá ferramenta repetida.

### 2.3 Vilão NPC
- Só existe depois de escolhido e a partir de `villainFirstActIndex` (Ato 2). **Não age no Ato 1.**
- **Entrada:** numa **Sala sem atores**, a mais longe (em espaços) do ator mais próximo. Empate: sorteio num fluxo próprio da seed (não muda os encontros da run).
- **Batida:** cada ação gasta = 1 batida → o vilão anda `villainMovesPerBeat` (1) espaço pelo grafo da casa (`HouseMap`), passando por corredores. Andar grátis **não** gera batida.
- **Alvo:** ator vivo dentro da casa; primeiro os **sozinhos**; depois **mais pavor**; depois o **mais perto**; depois a ordem do elenco.
- **Anúncio (compromisso):** depois de cada batida o vilão calcula alvo e próximo espaço (`NextSpace`). A próxima batida vai **exatamente** para lá, mesmo que o alvo tenha saído. É assim que o jogador "desvia".
- **Encontro** (vilão chega num espaço com atores, ou um ator entra no espaço do vilão — inclusive andando grátis):
  1. **Repelido** — alguém ali tem ferramenta com `counters` = subgênero do vilão: `repelScore` × multiplicador do ato, `repelPavor` em cada ator, a ferramenta é gasta, o vilão **some e reaparece** na sala livre mais longe e fica parado `repelStunBeats` (2).
  2. **Susto em grupo** — 2+ atores: `groupScarePavor` (30) em cada, recua `groupScareRetreat` (1) espaço (de preferência de onde veio) e fica parado `groupScareStunBeats` (1).
  3. **Pego** — ator sozinho: sai do filme e a cena `villainCatchPayoff` (Morte) **dispara sozinha**, com os elementos dele + da sala + automáticos (Isolado conta), mesma fórmula do Dirigir (`base × mult. do ato × (1 + soma)`), respeitando `MinActIndex`. Os elementos usados são gastos.
- Pavor no limite continua tirando o ator do filme (inclusive pelo +30 do grupo).
- Ordem dentro de uma ação: ação resolve → (se entrou no espaço do vilão) encontro → batidas → fim de ato.

### 2.4 Apresentação
- **Boneco:** `VillainView` (Mascarado escuro com máscara branca; Entidade = lençol). Anda pelo NavMesh na batida (máx. `villainMoveSeconds`), teleporta ao entrar/repelido. Nunca abre buraco na parede (`SeeThroughSubject.isVillain`).
- **Anúncio:** brilho vermelho no próximo espaço + ponta de seta flutuando + pegadas/seta na porta entre o espaço atual e o próximo. Some quando o vilão está parado.
- **HUD:** painel do topo ("Em: X → vai para Y · caça Z" ou "parado (n batidas)"); card da sala ("Passos se aproximando…", "O vilão está aqui.", "No chão: …", "vilão aqui!" no botão); Elenco ("● na mira", "ferr.: Faca (1/2)"); monitor (quadrado "VILÃO" + moldura pulsando "▼ passos"); nome do vilão sobre o boneco.
- **Popup:** as linhas do vilão entram no mesmo popup da ação ("— O Mascarado chega em Banheiro —" etc.), com +1,5 s de leitura (`villainResultExtraSeconds`).

---

## 3. Onde ajustar (tudo no Inspector)

| O quê | Onde | Padrão |
|---|---|---|
| Ações por ato | `Data/P1/Formato_Curta` → acts[i].steps | 8 |
| Custo de explorar / dirigir / usar | `Data/P1/Regras` → exploreActionCost / directStepCost / toolStepCost | 1 / 1 / 1 |
| Ferramentas por ator | `Regras` → maxToolsPerActor | 2 |
| Ligar/desligar o vilão NPC | `Regras` → villainNpcEnabled | ligado |
| Mostrar onde o vilão está (`revelarVilao`) | `Regras` → revealVillain | ligado |
| Ato em que ele entra | `Regras` → villainFirstActIndex | 1 (Ato 2) |
| Espaços por batida | `Regras` → villainMovesPerBeat | 1 |
| Repelido: pontos / pavor / batidas parado | `Regras` → repelScore / repelPavor / repelStunBeats | 80 / 10 / 2 |
| Grupo: pavor / recuo / batidas parado | `Regras` → groupScarePavor / groupScareRetreat / groupScareStunBeats | 30 / 1 / 1 |
| Cena da pega | `Regras` → villainCatchPayoff | Cena_Morte |
| O que cada ferramenta combate | `Ferramentas/*` → counters | Faca → Slasher, Crucifixo → Sobrenatural |
| Aparência do vilão | `Viloes/*` → look | Mascarado: Auto; Entidade: Ghost |
| Ritmo da encenação | `RunPresenter` (cena) → villainMoveSeconds / villainResultExtraSeconds | 2,2 s / 1,5 s |

---

## 4. Conteúdo (upgrade idempotente no `ContentBuilder`)

Rodar *Horror Tycoon > Criar Conteúdo P1 (não sobrescreve)* (também roda ao construir as cenas). Só preenche o que está vazio:
- Ferramentas novas: `Ferramenta_Faca` (counters Slasher), `Ferramenta_Crucifixo` (counters Sobrenatural).
- `Encontro_Faca` ("Faca na gaveta", Cozinha e Porão) passa a dar **também** a ferramenta Faca (o elemento Arma continua).
- Encontro novo `Encontro_Crucifixo` ("Crucifixo na parede", +20 pontos, −5 de pavor) adicionado ao **Quarto** e ao **Sótão** (peso 2).
- `Regras.villainCatchPayoff` = Cena_Morte; `Vilao_Entidade.look` = Ghost.
- Conferido: `Vilao_Mascarado` → Tag_Slasher, `Vilao_Entidade` → Tag_Sobrenatural.

---

## 5. Como testar

**Automático:** Window > General > Test Runner > EditMode > Run All (37 testes).
- Novos (`Prototipo2Tests`): grátis × pago e fim do ato; ferramenta com quem achou / limite / chão / próximo pega; compartilhar no alçapão; vilão não age no Ato 1 e entra numa sala sem atores; ato de entrada configurável; **anúncio = movimento real** (inclusive com o alvo fugindo); andar grátis não gera batida; repelido com Faca (ao entrar no espaço dele); ferramenta errada não repele; susto em grupo (recua + parado); **pego sozinho → Morte pontuada** (540 = 120 × 1,5 × 3); ator entra sozinho no espaço do vilão → pego e a chave fica no chão; prioridade do alvo.
- Adaptados (com o motivo no próprio teste): `Acoes_AndarGratis_SoSalaNovaCusta1_DoorsPerStepSemEfeito` (era "custo por porta / Atleta anda o dobro"); `FimDoAto1_PedeVilao...` (vilão NPC desligado no teste, senão ele pega o ator sozinho no meio da verificação da build); `CasaGerada_..._ConvivenciaGratis` (convivência agora custa 0); `CasaGerada_SalaNovaLongeCusta1_IndependenteDaDistancia` (era "Atleta arredonda para cima").

**Em Play (P0 e P2):** jogar o Ato 1 até a meta (ou encerrar), escolher o vilão. No Ato 2: o boneco aparece numa sala vazia, a marca vermelha mostra o próximo espaço; o card avisa "Passos se aproximando…". Gaste ações e veja o vilão andar; tire o ator sozinho do caminho (andar é grátis) ou junte atores; entre no espaço dele com a ferramenta certa para repelir.

Capturas: `Art/Previews/Unity/30_…` a `40_…` (P0: card de ações, vilão + anúncio, monitor + card, repelido com Faca, entrou sozinho e foi pego, vilão andando na batida, vilão chega e pega; P2: Entidade + anúncio, monitor + "O vilão está aqui", repelido com Crucifixo, pega sozinha).

---

## 6. Decisões tomadas na implementação (para validar)

1. **Anúncio é compromisso** (estilo Into the Breach): o vilão vai para o espaço anunciado mesmo que o alvo tenha saído; alvo e anúncio são recalculados **depois** de cada batida.
2. **Andar grátis não gera batida.** Consequência: o jogador sempre pode tirar um ator do espaço anunciado sem custo (é a pergunta do §5 da proposta: "posicionamento interessante ou teleporte?").
3. **Batida da mesma ação conta como parado:** se o ator paga 1 ação para entrar no espaço do vilão e o repele, a batida dessa ação já é a 1ª das 2 batidas parado. Entrando de graça (sala já explorada), as 2 batidas ficam inteiras.
4. **Vilão parado ainda pega quem entra** no espaço dele (o encontro acontece; só o movimento fica parado).
5. **Pontos de "sobreviveu"**: um valor por encontro (não por ator): `repelScore × mult. do ato` (80 × 1,5 = 120 no Ato 2).
6. **Distância do vilão = número de espaços** (cada corredor conta). Na casa gerada ele leva várias batidas para cruzar corredores longos; na estrela do P0, sala → sala = 2 batidas.
7. Se numa batida o vilão (não parado) fica num espaço com atores, o encontro acontece de novo (evita vilão "dormindo" ao lado de um ator quando não há para onde recuar).
8. Ferramenta continua única em jogo (não há duas Facas ao mesmo tempo).
9. No Ato 3 o vilão continua de onde estava (não reaparece).

---

## 7. Limitações conhecidas

- **Metas não recalibradas** (150 / 1.300 / 3.500). No teste em Play, o Ato 2 passou de 1.300 com metade das ações (Morte disparada pelo vilão vale muito: 720–1.080). Recalibrar no playtest.
- **Close de reação** (`ActorFocusController`) em salas pequenas às vezes enquadra a parede atrás do popup (visto nas pegas). Comportamento já existente do close; não mexi.
- O diretor de câmera (`CinematicDirector`) ainda **não** inclui o vilão nos planos (opcional, não feito).
- Com `villainMovesPerBeat > 1`, só o primeiro passo é anunciado.
- `revealVillain` ligado mostra "O vilão está aqui" até em sala ainda não descoberta.
- Boneco do vilão é placeholder (primitivas). Sem animação de ataque; o "pego" usa o `SetDead` do ator.
- Card da sala e painel do ator selecionado se sobrepõem em telas pequenas (≈1100 × 600) — layout da HUD já existente.
- O `RunLog` registra entrada, movimentos e encontros do vilão como texto; não há ação nova no replay (o vilão é determinístico pela seed + ações).

## 8. Perguntas para o Gabriel

- Andar grátis + anúncio fixo deixa o vilão fácil de evitar? Testar: (a) anúncio que segue o alvo ao vivo, ou (b) andar grátis gerar meia batida.
- A pega deve disparar a Morte com pontos cheios (hoje é o melhor jeito de pontuar: "preparar e isolar") ou com um multiplicador menor (`villainCatchPayoff` poderia ser uma cena própria)?
- Repelir deve dar pontos por ator presente?
- O vilão deve evitar corredores (andar por "portas" em vez de espaços) para chegar mais rápido na casa gerada?
