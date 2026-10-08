# 03 — Protótipo 1: Passos, Encontros e Payoff

> Pré-requisito: docs 01 e 02. Aqui só entra o que é **novo** nesta etapa.
> Todos os nomes e números de conteúdo (salas, encontros, pontos, metas) são **placeholders** para playtest. Eles vivem em `Assets/_HorrorTycoon/Data/P1` e são gerados pelo menu **Horror Tycoon > Criar Conteúdo P1 (não sobrescreve)** (`Editor/ContentBuilder.cs`).

---

## 1. O que mudou e como jogar

O sistema de **cartas** do esqueleto saiu. No lugar dele entrou um loop de **exploração + setup + payoff**:

1. Cada ato começa com **passos** (6 por ato no formato Curta). Cada sala sorteia, escondido, **1 encontro** para o ato.
2. Você move atores pela casa. Custa **1 passo por porta**; o Atleta atravessa 2 portas por passo.
3. Na **1ª visita** de uma sala no ato, o encontro dela acontece: pontos pequenos, pavor, um **elemento de cena** e/ou uma **ferramenta**.
4. Em salas **[Palco]** você **dirige** uma cena (Susto, ou Morte a partir do Ato 2). Os elementos acumulados viram pontos e são consumidos.
5. Acabaram os passos (ou você clicou **Encerrar ato**): confere a meta **acumulada**. Passou no Ato 1 → escolhe o **vilão** e começa o Ato 2.

**Controles**

| Ação | Entrada |
|---|---|
| Selecionar ator | clique no ator (Tab = próximo ator) |
| Mover | clique na sala |
| Close no ator | F (Esc volta) |
| Dirigir / usar ferramenta | botões na HUD |
| Encerrar ato antes | botão "Encerrar ato" |

No GameMaker você faria isso com um `obj_game` controlando um `state` (explore, choose_villain, result). Aqui a ideia é a mesma, mas dividida: `FilmRun` (regras, C# puro), `RunPresenter` (fases, input, animação) e `RunHud` (só desenha e chama métodos).

---

## 2. Mapa dos novos dados

Tudo em `Data/P1`, editável no Inspector. Criar novos: **Create > Horror Tycoon > ...**

| Asset (script) | Para que serve | Onde editar |
|---|---|---|
| `FilmFormatDef` → `ActSettings` | passos, meta acumulada, multiplicador de payoff por ato | `Formato_Curta` |
| `GameRulesDef` | custo de dirigir/ferramenta, limite de pavor, peso e viés do vilão | `Regras` |
| `ActorDef` | `DoorsPerStep`, pavor inicial | `Atores/` |
| `RoomDef` | dicas, encontros com pesos, `stagePayoffs` (palco), ponto trancado | `Salas/` |
| `EncounterDef` | pontos, pavor, elemento, ferramenta | `Encontros/` |
| `ElementDef` | holder (ator/sala), `strength`, subgênero (`TagDef`), `validFor`, regra automática | `Elementos/` |
| `PayoffDef` | base, pavor, mata?, ato mínimo, consome elementos? | `Cenas/` |
| `ToolDef` | ferramenta global da run | `Ferramentas/` |
| `VillainDef` | subgênero que ele favorece | `Viloes/` |
| `GameContentDef` | catálogo: salas (ordem = grade), colunas, entrada, vilões, elementos automáticos | `Conteudo_P1` |

**Removidos:** `SceneTypeDef`, os Effects com `[SerializeReference]` + `EffectDrawer`, `ArtifactDef` e `ScoreCalculator`.

Apagar código morto faz bem. Cada arquivo que fica "por via das dúvidas" precisa compilar, aparece nas buscas, confunde quem lê ("isso ainda é usado?") e trava refatorações. No GMS seria como deixar um `obj_card_old` jogado no projeto: ninguém instancia, mas todo mundo tropeça nele. O Git já guarda tudo: `git log --oneline -- Scripts/Scoring/ScoreCalculator.cs` lista os commits que mexeram no arquivo, e `git show <commit>^:Scripts/Scoring/ScoreCalculator.cs` mostra o conteúdo como era antes do commit que o apagou (use `git checkout <commit>^ -- <caminho>` para trazer de volta). Apagar não é perder.

---

## 3. HouseMap e BFS

`Rooms/HouseMap.cs` é um **grafo** em C# puro. A casa é uma grade (`columns = 3`, 6 salas): vizinhos na horizontal/vertical têm porta, e a entrada (Sala de estar, índice 1) liga ao "lado de fora" (`-1`), onde os atores começam.

```
 0 Cozinha | 1 Sala de estar | 2 Banheiro      (fora = -1 → 1)
 3 Quarto  | 4 Porão         | 5 Sótão
```

O custo em portas vem de uma **busca em largura (BFS)**:

```csharp
var queue = new Queue<int>();
var dist = new Dictionary<int, int> { [from] = 0 };
queue.Enqueue(from);
while (queue.Count > 0)
{
    int n = queue.Dequeue();
    foreach (int m in neighbors[n])
    {
        if (dist.ContainsKey(m)) continue;
        dist[m] = dist[n] + 1;
        if (m == to) return dist[m];
        queue.Enqueue(m);
    }
}
```

**Analogia GMS:** é exatamente o que você escreveria com `ds_queue_create()` + `ds_map` de distâncias. O `mp_grid` também resolve caminhos, mas em **células de pixels**, para mover instâncias. Aqui são duas camadas separadas:

- **regra** (quantas portas?) → `HouseMap`, discreto, testável sem cena;
- **visual** (como o boneco anda?) → `NavMesh` + `ActorMover`.

O custo em passos arredonda para cima pelo `DoorsPerStep` do ator:

```csharp
int doors = Map.Doors(actor.RoomIndex, roomIndex);
if (doors <= 0) return doors; // 0 = já está lá, -1 = sem caminho
int perStep = Mathf.Max(1, actor.Def.DoorsPerStep);
return (doors + perStep - 1) / perStep; // arredonda para cima
```

Exemplo: de fora até o Porão = 2 portas (fora→1→4). Nerd paga 2 passos; Atleta paga 1.

---

## 4. Encontros, ferramenta e vilão

- **Encontro:** `StartAct()` chama `RollEncounter` para cada sala usando `Rng.PickWeightedIndex(weights)` (o mesmo `GameRandom` do doc 02, então a seed reproduz tudo). O encontro só dispara se `!room.VisitedThisAct`. Encontros de ferramenta já achada saem do sorteio.
- **Ferramenta + ponto trancado:** a Chave de fenda vai para o inventário **global** da run. No Porão, `CanUseTool` libera o botão; `UseTool` gasta 1 passo e entrega o **Machado** (strength 2, só conta para Morte).
- **Vilão:** no fim do Ato 1 (se passou), `AwaitingVillainChoice = true` e a HUD oferece os vilões ordenados por quantos elementos da tag deles você já juntou (`VillainOffer`). Depois da escolha, dois efeitos: elementos da tag dele pesam `villainTagWeight` (2) e encontros que dão elemento dessa tag têm o peso multiplicado por `villainEncounterBias` (2).

---

## 5. O cálculo do payoff, passo a passo

Fórmula (em `FilmRun.Direct`):

```csharp
var counted = CountElements(actor, payoff);
int sum = counted.Sum(c => c.Weight);
float mult = CurrentAct.payoffMultiplier;
int score = Mathf.RoundToInt(payoff.BaseScore * mult * (1 + sum));
```

E o peso de cada elemento, dentro de `CountElements`:

```csharp
if (e == null || !e.CountsFor(payoff)) return;
bool boosted = Villain != null && e.Subgenre != null && e.Subgenre == Villain.Subgenre;
// ...
Weight = e.Strength * (boosted ? Rules.villainTagWeight : 1),
```

Quem entra na lista: elementos **do ator**, elementos **da sala** e elementos **automáticos** ativos (`Isolado` = único ator vivo na sala; `Apavorado` = pavor ≥ 60). Só contam os que têm o payoff em `validFor`.

**Exemplo:** Ato 2, vilão **O Mascarado** (Slasher). A Final Girl carrega uma **Arma** e está **sozinha** no Porão (palco de Morte).

| Item | Valor |
|---|---|
| Base da Morte | 120 |
| Multiplicador do Ato 2 | 1.5 |
| Arma: strength 1, Slasher, conta para Morte | 1 × 2 = **2** |
| Isolado: strength 1, Slasher (automático) | 1 × 2 = **2** |
| Soma | 4 |
| **Pontos** | 120 × 1.5 × (1 + 4) = **900** |

Com A Entidade como vilão, a mesma cena daria 120 × 1.5 × (1 + 1 + 1) = 540. Com o Machado (strength 2 × 2) no lugar da Arma: 120 × 1.5 × (1 + 4 + 2) = 1260.

Depois: elementos que contaram são **removidos** (automáticos não, porque são condições), a Morte tira o ator do filme (`killsActor`), e o Susto aplica +25 de pavor.

---

## 6. Informação parcial: o que a HUD mostra

Regra de design: o jogador vê **ingredientes**, nunca o **resultado**. A separação de camadas do doc 02 é o que torna isso simples: `CountElements` devolve uma **lista** (`List<CountedElement>`), e cada camada usa o que precisa dela.

- `Direct()` soma os pesos → pontos.
- `RunHud` só escreve os **nomes**, com ★ se o vilão reforçou:

```csharp
var counted = run.CountElements(actor, p);
var sb = new StringBuilder($"<b>Dirigir {p.DisplayName}</b> ({run.Content.Rules.directStepCost} passo)\n");
if (counted.Count == 0) sb.Append("<i>nada preparado</i>");
foreach (var c in counted) sb.Append($"● {c.Element.DisplayName}{(c.BoostedByVillain ? "★" : "")}  ");
```

Sobre as salas, a HUD projeta a posição 3D para a tela (`WorldToScreenPoint`) e escreve: dicas (`[Escuro]`, `[Presença]`), `[Palco: Morte (Ato 2+)]`, `[Alçapão trancado]`, elementos já na sala e o **custo em passos** para o ator selecionado (verde se dá, vermelho se não). O encontro sorteado **nunca** aparece antes da visita.

No GMS você provavelmente calcularia o score dentro do Draw GUI "para mostrar" e teria que tomar cuidado para não vazar. Aqui a UI nem tem acesso direto à soma: ela teria que fazer a conta de propósito.

---

## 7. Bugs reais desta etapa (e lições)

**1) O ator "chegava" sem sair do lugar.** `NavMeshAgent.remainingDistance` vale **0** enquanto o caminho ainda não foi calculado (o cálculo pode levar um frame). O loop via `0 <= arriveDistance` e encerrava na hora. Correção em `ActorMover.MoveTo`:

```csharp
var path = new NavMeshPath();
if (!agent.CalculatePath(destination, path) || path.status != NavMeshPathStatus.PathComplete)
{
    agent.Warp(destination);
    yield break;
}
// ... no loop:
if (!agent.pathPending && agent.hasPath && agent.remainingDistance <= arriveDistance) arrived = true;
```

Mais um timeout que faz `Warp` (teleporte). Comparação: `mp_potential_path` / `mp_grid_path` no GMS devolvem `true/false` **na hora**, de forma síncrona. O NavMeshAgent é **assíncrono**: você pede, e ele responde depois. Lição: nunca confie numa propriedade de "estado" antes de checar que o estado existe (`pathPending`, `hasPath`). E sempre tenha um fallback que não trava o jogo.

**2) Portas que o NavMesh não enxergava.** O NavMesh é "bakeado" encolhido pelo **raio do agente** (0.5 no tipo padrão Humanoid). Uma porta de 1.2 perde 0.5 de cada lado → sobra 0.2, e com a borda da parede a passagem some. As portas foram para **1.8** (`GreyboxSceneBuilder.DoorWidth`). Atenção: o `agent.radius = 0.3` do componente no ator não muda o bake, que usa as configurações do *agent type*. Lição: **o NavMesh enxerga o mapa menos o raio do agente**. Se algo "não tem caminho", ligue a visualização do NavMesh na Scene view antes de mexer em código. É parecido com o `mp_grid` marcando células como ocupadas quando um objeto encosta nelas, mesmo que sobre um vão visual.

**3) A Unity compilou a versão antiga.** Uma edição do arquivo e a cópia para o projeto rodaram **ao mesmo tempo**; a cópia pegou o arquivo antes da edição terminar. Tudo "compilou", mas o bug continuava. Lição de processo: **salvar → depois sincronizar/compilar → confirmar** que a mudança está lá (abrir o arquivo no projeto, ou colocar um `Debug.Log` temporário). Quando "a correção não funcionou", a primeira pergunta é "a correção está mesmo rodando?".

---

## 8. Balanceamento: por que esses números

Metas **acumuladas** 150 / 1300 / 3500, multiplicadores 1 / 1.5 / 2.5 e 6 passos por ato foram calibrados por simulação em Python (~2000 runs com um bot simples, reimplementando as regras):

- bot "esperto" (prepara elementos e dirige no palco certo) vence **~70%**;
- bot aleatório vence **~0%**.

Ou seja, a **estratégia importa**. No esqueleto, a estratégia gulosa vencia 85% sem pensar, o que é sinal de que não havia decisão real.

**Ponto aberto para o playtest:** dirigir **Susto repetidas vezes com um ator isolado** é forte, porque Isolado não é consumido e o Susto não mata. O freio atual é o pavor (+25 por Susto, limite 100 = ator quebra **sem pontos**). Os números são hipótese; observe se isso domina as runs.

---

## 9. Experimente você mesmo

1. **Passos por ato:** em `Formato_Curta`, mude o Ato 1 para 4 passos. O Ato 1 ainda é passável? E com 8?
2. **Encontro novo:** crie `Encontro_Grito` (Create > Horror Tycoon > Encontro) com +60 pontos e +30 de pavor, e coloque no Sótão com peso 2. Use uma seed fixa e veja quantas vezes ele aparece.
3. **Nova cena dirigida:** crie um `PayoffDef` "Perseguição" (base 80, não mata, ato mínimo 0). Adicione-o ao `validFor` de Luz apagada e Isolado, e ao `stagePayoffs` do Quarto. A HUD deve mostrar o botão sem nenhuma linha de código nova.
4. **Outro palco:** coloque Morte no `stagePayoffs` da Cozinha. O que acontece com a Arma (a Faca costuma sair ali)?
5. **Peso do vilão:** em `Regras`, mude `villainTagWeight` para 3. Refaça a conta da seção 5 no papel e confira no log.
6. **Segunda ferramenta:** crie a `ToolDef` "Pé de cabra", um encontro que a entrega e um `LockedSpot` no Sótão que dá um elemento Sobrenatural forte (strength 2).
7. **Seed fixa:** no `RunPresenter`, coloque `seed = 1234`. Jogue duas vezes com as mesmas ações e compare os encontros.
8. **Ler o log:** no fim da run, inspecione `RunLog.actions` (ex.: `move Atleta 4`, `direct Nerd Susto`, `villain O Mascarado`). O número no `move` é o **índice da sala** na grade. Com seed + ações dá para reproduzir a run inteira. Veja o teste `MesmaSeed_MesmasAcoes_MesmoResultado`.

---

## 10. Glossário

| Termo | Significado |
|---|---|
| **Passo** | recurso do ato; mover (por porta), dirigir e usar ferramenta gastam passos |
| **Encontro** | evento sorteado por sala e por ato; dispara na 1ª visita |
| **Elemento de cena** | o "setup" (Arma, Presença, Isolado…) que multiplica o payoff |
| **Elemento automático** | vale enquanto uma condição for verdadeira (`ElementAutoRule`); não é consumido |
| **Payoff / cena dirigida** | Susto ou Morte; converte elementos em pontos |
| **Palco** | sala cujo `stagePayoffs` permite dirigir aquela cena |
| **Ponto trancado** | `LockedSpot` que exige uma ferramenta e dá um elemento |
| **Subgênero (TagDef)** | Slasher / Sobrenatural; liga elementos, encontros e vilão |
| **Informação parcial** | a HUD mostra ingredientes (nomes, dicas, custos), nunca o total |
| **BFS** | busca em largura; menor número de portas entre duas salas |
| **Raio do agente** | quanto o NavMesh "encolhe" o mapa a partir das paredes |
| **Código morto** | código que nada usa; apague, o Git guarda |
