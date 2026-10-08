# 02 — Esqueleto do Loop

> Pré-requisito: doc 01. Aqui só entram conceitos novos.

## 1. O que mudou e como jogar

**Mudou:**
- Todo o conteúdo virou **asset de dados** em `Assets/_HorrorTycoon/Data/`: 4 atores, 6 salas, 4 tipos de cena, 5 artefatos, 9 tags, `Formato_Curta`, `Regras` e o catálogo `Conteudo_P0`.
- A run inteira (3 atos × 5 cenas, metas acumuladas 700/2100/6300) roda em **C# puro** (`FilmRun` + `ScoreCalculator`).
- Atores andam pela casa com **NavMesh**, reagem com close e expressão, e "saem do filme" no pavor 100.
- HUD temporária em IMGUI, log da run em JSON, 5 testes automáticos.
- Código dividido em 3 **assemblies** (runtime, editor, testes).

**Como jogar:**
1. Abra `Assets/_HorrorTycoon/Scenes/P0_Greybox.unity` e dê **Play**. (Se a cena ou os dados não existirem: menu **Horror Tycoon > Construir Cena Greybox** — ele cria o conteúdo antes, se faltar.)
2. Passe o mouse numa carta: aparece o painel **Cálculo** (linha a linha de onde veio cada ponto), uma esfera amarela acende sobre o ator e o piso da sala fica destacado.
3. Clique para jogar: o ator anda até a sala, a câmera faz o close de reação, aparece o resultado.
4. Fim de ato: painel com meta e botão **Continuar**.
5. Fim da run: **Jogar de novo** ou **Copiar log da run** (JSON na área de transferência).

As câmeras do doc 01 continuam funcionando enquanto você escolhe.

---

## 2. Mapa da arquitetura

```
┌──────────────────────── DADOS (assets em Data/) ────────────────────────┐
│ GameContentDef (Conteudo_P0)                                            │
│   ├─ FilmFormatDef · GameRulesDef                                       │
│   ├─ ActorDef[] · RoomDef[] · SceneTypeDef[] · ArtifactDef[]            │
│   └─ (cada um com TagDef[] e Effect[])                                  │
└───────────────────────────────┬─────────────────────────────────────────┘
                                │ lido por
┌───────────────────────────────▼──────── LÓGICA (C# puro) ───────────────┐
│ FilmRun  ── usa ──> ScoreCalculator ──> ScoreContext / ScoreResult      │
│   ├─ GameRandom (seed)     ├─ ActorRunState · Card                      │
│   └─ RunLog                └─ eventos: HandDrawn, ScenePlayed, ...      │
└───────────────────────────────┬─────────────────────────────────────────┘
                                │ eventos / leitura de estado
┌───────────────────────────────▼──────── APRESENTAÇÃO (MonoBehaviours) ──┐
│ RunPresenter ──> ActorView + ActorMover · RoomAnchor                    │
│      ▲           ActorFocusController (câmera)                          │
│      └── RunHud (lê o presenter, chama ChooseCard/Continue/Restart)     │
└─────────────────────────────────────────────────────────────────────────┘
```

A seta só desce. `FilmRun` não tem `using UnityEngine.SceneManagement`, não conhece câmera, não sabe que existe tela.

**Comparação com GMS.** Num projeto GMS típico a regra fica espalhada: o Step do `obj_player` soma ponto, o Alarm do `obj_enemy` decide dano, o Draw GUI calcula o que mostrar. Aqui o "cérebro" é um objeto puro, como um struct com métodos criado por `new` e guardado numa variável global, que nenhum objeto da room "possui". As instâncias da room só pedem ("jogue a carta 2") e escutam ("a cena aconteceu, eis o resultado").

**Por que separar:**
- **Testes:** dá para rodar uma run inteira sem abrir cena (seção 10).
- **Replay:** como a lógica é determinística, seed + escolhas reproduzem tudo (seção 5).
- **Trocar o visual:** a HUD IMGUI vai virar UI Toolkit, as cápsulas viram modelos. Nenhuma regra muda.

---

## 3. ScriptableObject para quem vem do GMS

O mais próximo no GMS: **um struct/JSON de dados salvo como arquivo do projeto, editável num formulário**. Ou: "um objeto que nunca é instanciado na room e só guarda variáveis". Ele não tem `Update`, não está na cena e é um arquivo `.asset` em `Data/`, com `.meta` e GUID como qualquer asset.

```csharp
[CreateAssetMenu(menuName = "Horror Tycoon/Artefato", fileName = "Artefato_Novo")]
public class ArtifactDef : ScriptableObject
{
    [SerializeField] private string displayName = "Artefato";
    [SerializeField, TextArea] private string description = "";
    [SerializeReference] private List<Effect> effects = new List<Effect>();

    public string DisplayName => displayName;
```

- `ScriptableObject` no lugar de `MonoBehaviour` = asset, não componente.
- `[CreateAssetMenu]` adiciona a entrada no menu **Create** da janela Project.
- Campos privados + `[SerializeField]` + propriedade só-leitura (`=>`): o Inspector edita, o código só lê.
- Referência entre assets é por **arquivo**, não por nome. O `ActorDef` aponta para o asset `Tag_Atleta`. Renomear a tag não quebra nada (por isso tag é asset e não string).

**Dado ≠ estado.** `ActorDef` (asset) diz "o Atleta ganha +50 em Perseguição". `ActorRunState` (C# puro, criado por run) guarda pavor/vivo/tempo de tela. Nunca escreva estado de run num ScriptableObject: em Play no editor, a alteração **fica salva no arquivo**.

### Criar um artefato novo, passo a passo

1. Na janela Project, vá em `Data/Artefatos`, clique com o botão direito e escolha **Create > Horror Tycoon > Artefato**. Dê um nome (ex.: `Artefato_Boneca`).
2. No Inspector, preencha **Display Name** e **Description**.
3. Em **Effects**, clique em **+**. Na linha nova, **escolha o tipo no menu suspenso** (ex.: `Add Bonus Effect`).
   > ⚠️ Depois do "+", o Unity pode duplicar a referência do item anterior (dois itens apontando para o mesmo efeito). **Sempre escolha o tipo no menu**: trocar o tipo cria um objeto novo.
4. Expanda o efeito, ajuste `Amount` e, se quiser, a **Condition**: arraste um asset de tag para `Scene Tag`/`Actor Tag`/`Room Tag`, ou use `Min Pavor`/`Max Pavor` (-1 = ignora).
5. Selecione `Data/Conteudo_P0` e arraste o artefato novo para a lista **Artifacts**.

O menu **Horror Tycoon > Criar Conteúdo P0 (não sobrescreve)** só cria o que falta (`LoadOrCreate`), então as suas edições sobrevivem. Para "resetar" um item, apague o asset e rode o menu.

---

## 4. Efeitos e tags

Um efeito é uma classe C# **comum** (não asset), guardada *dentro* do asset dono:

```csharp
[Serializable]
public abstract class Effect
{
    public EffectCondition condition = new EffectCondition();
    public abstract string Describe();
    protected abstract void Apply(ScoreContext ctx);
    // TryApply: testa condition.Matches(ctx), chama Apply e anota a linha do breakdown
}
```

```csharp
[Serializable]
public class AddTensionEffect : Effect
{
    public int amount = 50;
    public override string Describe() => $"{Signed(amount)} Tensão";
    protected override void Apply(ScoreContext ctx) => ctx.Tension += amount;
```

**`[SerializeReference]` é o truque.** Uma `List<Effect>` normal só serializaria a classe base e perderia o tipo real. Com `[SerializeReference]`, o Unity grava "este item é um `AddTensionEffect` com amount=50". É **polimorfismo serializado**. O `EffectDrawer` (pasta Editor) desenha o menu de tipos usando `TypeCache.GetTypesDerivedFrom<Effect>()`, ou seja, descobre sozinho toda subclasse.

**Comparação com GMS.** Lembra que o parent do GMS é herança de *eventos* (`event_inherited()`)? Aqui é herança de **classe C# de verdade**: `abstract` = "não pode existir sozinha", `override` = "minha versão deste método". E está sendo usada para **dados com comportamento pequeno**, não para objetos da cena. Os 6 tipos atuais: `AddTension`, `AddImpact`, `MultiplyImpact`, `AddBonus`, `ModifyPavor`, `ModifyArtifactChance`.

### Ordem de cálculo (`ScoreCalculator`)

1. Base da cena: tensão já sorteada na carta, impacto, pavor, chance de artefato
2. Efeitos do **ator**
3. Efeitos da **sala**
4. Efeitos dos **artefatos**, na ordem em que foram obtidos
5. Bônus de pavor: `Impacto × (1 + pavorImpactBonusAtLimit × pavor/limite)`
6. `Tensão × Impacto + Bônus` (+ `deathBonus` se o ator quebrar)

A ordem importa como no Balatro: um `+0.5 Impacto` antes de um `×1.5` rende mais do que depois.

`ScoreCalculator.Calculate` **não muda nada** no jogo. Por isso a mesma função serve para a prévia (hover) e para a jogada real, e a prévia é sempre exata.

### Criar um TIPO de efeito novo

Crie a classe em qualquer `.cs` do runtime (ex.: no fim de `Scoring/Effects.cs`):

```csharp
[Serializable]
public class MultiplyTensionEffect : Effect
{
    public float multiplier = 2f;
    public override string Describe() => "×" + multiplier.ToString("0.##") + " Tensão";
    protected override void Apply(ScoreContext ctx) =>
        ctx.Tension = Mathf.RoundToInt(ctx.Tension * multiplier);
}
```

Salve, espere compilar e ele **aparece sozinho** no menu de efeitos do Inspector. Não precisa registrar em lugar nenhum. Não esqueça o `[Serializable]`.

> Cuidado: `[SerializeReference]` grava o **nome da classe**. Renomear ou mudar de namespace uma classe de efeito já usada em assets quebra esses itens.

---

## 5. FilmRun: estados, eventos, seed, log

**Estado:** `Status` (`Playing`/`Won`/`Lost`), `ActIndex`, `SceneInAct`, `TotalScore`, `Turn`, mais as listas `Actors`, `Artifacts`, `Hand`.

**Eventos C#:**
```csharp
public event Action<SceneOutcome> ScenePlayed;
public event Action<ActOutcome> ActEnded;
// disparo:
ActEnded?.Invoke(result);
// assinatura (no RunPresenter):
Run.ActEnded += act => pendingAct = act;
```
`Action<T>` = "lista de funções que recebem um T". `+=` adiciona, `-=` remove, `?.Invoke` chama todas (se houver alguma). O GMS não tem eventos customizados nativos. O equivalente seria você manter um array de *method variables* (callbacks) e, ao acontecer algo, percorrer e chamar cada um, ou fazer um broadcast com `with (all) event_user(0)`. Aqui isso é da linguagem e tipado. A lógica **avisa**, sem saber quem escuta.

**Fluxo de uma jogada:**
```
Begin() → DrawHand() → [HandDrawn]
PlayCard(i):
  Turn++, Log.choices.Add(i)
  ScoreCalculator.Calculate → soma pontos, aplica pavor
  pavor no limite? → ator morre
  Rng.Chance(artefato)? → RollArtifact
  [ScenePlayed] (+ [ArtifactGained])
  ninguém vivo? → End(Lost)
  fim do ato? → EndAct() : DrawHand()
EndAct():
  [ActEnded]; falhou e fatal (ou último ato)? → End(Lost)
  último ato? → End(Won) : ActIndex++ e DrawHand()
```

**`GameRandom` e seed.** É o `random_set_seed()` do GMS, mas em uma **instância própria** (`new System.Random(seed)`) em vez de um estado global. Toda aleatoriedade da lógica passa por `run.Rng`. **Nunca use `UnityEngine.Random` na lógica**: ele é global, então qualquer outro script (partícula, árvore, câmera) que o consuma muda a sequência e a run deixa de ser reproduzível.

Detalhe que importa: a tensão é sorteada **ao criar a carta** (`RolledTension`), não ao jogar. Assim o hover não consome números aleatórios, e olhar as cartas não altera a run.

**`RunLog`:** guarda `seed`, `choices` (índice escolhido em cada turno) e entradas de texto. Seed + escolhas = replay completo, porque a mesma seed gera as mesmas mãos e as mesmas escolhas levam ao mesmo resultado. `ToJson()` usa o `JsonUtility` do Unity (parecido com o `json_stringify` do GMS; precisa de `[Serializable]` e campos públicos).

---

## 6. RunPresenter e corrotinas

A lógica resolve a jogada **instantaneamente**. O presenter então **encena** com calma. Para isso usa uma **corrotina**: uma função que pode pausar no meio e continuar em frames seguintes.

```csharp
private IEnumerator PlaySequence(int index)
{
    ...
    SceneOutcome outcome = Run.PlayCard(index);   // lógica: pronto
    yield return mover.MoveTo(anchor.NextStandPoint()); // espera o ator chegar
    view.SetExpression(ReactionFor(outcome));
    if (focusController != null) focusController.Focus(view);
    yield return new WaitForSeconds(reactionSeconds);   // segura o close
```

- `IEnumerator` + `yield return` = "pause aqui".
- `yield return null` → continua no próximo frame.
- `yield return new WaitForSeconds(x)` → continua daqui a x segundos.
- `yield return outraCorrotina` → espera ela terminar (é o que faz `MoveTo`).
- Inicia com `StartCoroutine(PlaySequence(index))`.

**No GMS** você faria isso com uma máquina de estados + alarms (`alarm[0] = 60` → no Alarm 0 avança para o passo 2 → arma `alarm[1]`...), ou com timeline/`call_later`. A corrotina é a mesma ideia, mas escrita **de cima para baixo** numa função só, com variáveis locais vivas entre as pausas.

Cuidados: a corrotina pertence ao MonoBehaviour e **para** se o GameObject for desativado/destruído. `WaitForSeconds` respeita `Time.timeScale`.

O presenter também expõe `CurrentPhase` (`Choosing`, `Playing`, `ShowingResult`, `ActBreak`, `Ended`), que a HUD lê para saber o que desenhar, e desliga `focusController.AllowPlayerInput` durante a encenação para o jogador não brigar com a câmera automática.

---

## 7. NavMesh

**NavMesh** = malha de "onde dá para andar", calculada a partir da geometria. **NavMeshAgent** = componente que anda sobre ela, desviando de paredes e passando pelas portas.

**Comparação com GMS.** É o `mp_grid` + `mp_grid_path` + `path_start`, só que sem grid: em vez de células, polígonos gerados do cenário 3D. O agente já faz o "seguir o path" com aceleração e curva (`speed`, `angularSpeed`, `acceleration`).

- `NavMeshSurface` (pacote AI Navigation) fica no objeto `NavMesh` e é **construída em runtime**: `navMeshSurface.BuildNavMesh()` no `Start` do presenter. Assim a malha sempre bate com a casa atual. Ela coleta os colliders (`PhysicsColliders`).
- `ActorMover.MoveTo` faz `agent.SetDestination(...)` e espera até `remainingDistance <= arriveDistance` (com timeout de 10 s; se não houver NavMesh, teleporta).

**Por que o agente é salvo desligado?** Um `NavMeshAgent` ligado procura o NavMesh ao nascer. Como a malha só existe depois do `BuildNavMesh()`, ele daria erro ("não está perto do NavMesh"). Então: o builder salva `agent.enabled = false`, o `Awake` do mover garante isso, e só depois do build o presenter chama `EnableAgent()`, que encaixa o ator no ponto válido mais próximo (`NavMesh.SamplePosition`) e liga o agente.

---

## 8. HUD em IMGUI

**Por que temporária:** IMGUI (`OnGUI`) é imediata, só código, ótima para iterar rápido. Para UI final é lenta e difícil de estilizar. Como a `RunHud` só **lê** o presenter e chama `ChooseCard`/`ContinueAfterAct`/`Restart`, trocá-la depois não toca em regra nenhuma.

**Como funciona:** parecido com o Draw GUI do GMS (redesenha tudo do zero sempre), com uma diferença: `OnGUI` é chamado **várias vezes por frame**, uma por tipo de evento (`Layout`, `Repaint`, `MouseDown`...). `GUI.Button` desenha no `Repaint` e retorna `true` no evento do clique. Por isso o hover só é enviado no repaint:

```csharp
if (Event.current.type == EventType.Repaint)
{
    presenter.Hover(hovered);
}
```

Sem isso, `Hover` seria chamado 2 ou 3 vezes por frame com valores intermediários.

**`HudInputBlocker`:** o clique numa carta também chegaria ao `ActorFocusController` (que faz raycast no mundo) e focaria o ator atrás da carta. A HUD registra os retângulos que ocupa (`Register(rect)`) e o controller pergunta `IsPointerOverHud(pos)` antes do raycast. Duas pegadinhas tratadas lá: o GUI tem **Y para baixo** e o Input System tem **Y para cima** (daí `Screen.height - y`); e o `Update` do controller roda antes do `OnGUI`, então ele usa os retângulos do frame anterior, o que basta para uma HUD que não pula de lugar.

---

## 9. Assembly Definitions (`.asmdef`)

No GMS tudo compila junto. No Unity, sem `.asmdef`, também: todo script vai para um único `Assembly-CSharp`. Um `.asmdef` numa pasta diz "**os scripts desta pasta (e subpastas) formam um pacote compilado à parte**".

| Assembly | Pasta | Referencia | Plataforma |
|---|---|---|---|
| `HorrorTycoon` | `Scripts/` | Cinemachine, InputSystem, AI.Navigation | todas |
| `HorrorTycoon.Editor` | `Scripts/Editor/` | `HorrorTycoon` + Cinemachine + AI.Navigation | só Editor |
| `HorrorTycoon.Tests` | `Scripts/Tests/` | `HorrorTycoon` + TestRunner + NUnit | só Editor |

Por quê:
- **Direção das dependências garantida:** Editor e Tests enxergam o runtime; o runtime **não** enxerga os dois. Se alguém usar `UnityEditor` no código do jogo, dá erro de compilação na hora, e não só no build.
- **Testes fora do jogo:** `defineConstraints: UNITY_INCLUDE_TESTS` e `autoReferenced: false` mantêm os testes fora do build.
- **Compilação mais rápida:** mexer num teste não recompila o runtime.

As referências são **por nome** (`"references": ["HorrorTycoon", ...]`). Usou um pacote novo no código e deu "namespace não encontrado"? Falta adicionar o nome do assembly dele na lista do `.asmdef`.

---

## 10. Testes automáticos

Um teste **EditMode** é um método marcado com `[Test]` que roda dentro do editor, sem Play e sem cena. Ele monta um cenário, executa e **afirma** (`Assert`) o resultado. Se a afirmação falha, o teste fica vermelho.

**Rodar:** Window > General > Test Runner > aba **EditMode** > **Run All**. Os 5 testes atuais passam.

```csharp
[Test]
public void Pavor_NoLimite_AtorQuebra_ERunAcabaSemElenco()
{
    cenaSusto.Setup("Susto", "", Color.white, new List<TagDef> { susto }, 100, 100, 1.0f, 60, 0f, new[] { 1f });
    var run = new FilmRun(Content(), 1);
    run.Begin();

    run.PlayCard(0);          // pavor 60
    var outcome = run.PlayCard(0); // pavor 100 -> quebra

    Assert.IsTrue(outcome.ActorBroke);
    Assert.AreEqual(RunStatus.Lost, run.Status);
}
```

`[SetUp]` roda antes de cada teste e cria assets **só em memória** (`ScriptableObject.CreateInstance<T>()`). Os testes cobrem: fórmula base, condição de tag em artefato, morte por pavor, falha de meta e determinismo (mesma seed + mesmas escolhas = mesmo total). Isso só é possível porque a lógica não depende da cena (seção 2).

---

## 11. Um bug real: o construtor gerou 0 atores

**Sintoma:** **Construir Cena Greybox** terminava sem erro, mas com "0 atores".

**Causa:** o builder pegava o catálogo, chamava `EditorSceneManager.NewScene(...)` e depois lia `content.Actors`. Trocar de cena faz o Unity **descarregar da memória** assets que a cena anterior usava. A lista C# continuava com os mesmos itens, mas cada um apontava para um objeto cujo lado nativo tinha sumido. Aí `if (def == null) continue;` pulava todos.

**O "fake null" do Unity.** Todo `UnityEngine.Object` (GameObject, componente, asset) tem duas metades: o objeto nativo (C++) e um "invólucro" C#. Quando o nativo é destruído ou descarregado, o invólucro continua existindo, mas o Unity **sobrescreve o `==`** para que `obj == null` responda `true`. A referência não é nula de verdade, só está "morta". Consequências:
- `if (x == null)` e `if (x)` detectam o objeto morto. ✔
- `x?.Metodo()` e `x ?? y` **não** detectam, porque usam o null "real" do C#. ✘ Evite-os com tipos do Unity. (Com classes C# puras, como `Effect` ou `MaterialPropertyBlock`, tudo bem.)

Pense no `instance_exists()` do GMS: o id da instância continua na sua variável depois do `instance_destroy`, mas a instância não existe mais. A diferença é que o Unity embute essa checagem no `== null`.

**Solução:** recarregar o catálogo **depois** do `NewScene` (`AssetDatabase.LoadAssetAtPath`) e ler as listas pelo `SerializedObject`, o mesmo caminho que o Inspector usa, que resolve a referência a partir do arquivo:

```csharp
var so = new SerializedObject(owner);
var prop = so.FindProperty(fieldName);
for (int i = 0; i < prop.arraySize; i++)
{
    var element = prop.GetArrayElementAtIndex(i).objectReferenceValue;
    // se ainda vier null: recarrega pelo caminho do asset
```

**Lição:** em ferramentas de editor, **depois de trocar de cena, recarregue/leia os dados de novo**. Não confie em referências guardadas de antes.

---

## 12. O que o esqueleto já nos diz

Simulamos 200 runs com uma estratégia gulosa ("sempre a carta de maior pontuação, evitando matar"): **171 vitórias (85%)**, média de **7563 pontos** contra a meta final de 6300.

Leitura: o jogo está fácil, e "sempre a maior carta" basta. Como a prévia é exata, a escolha vira uma conta em vez de uma decisão. É o esperado: este esqueleto **não tem camada estratégica**. O próximo passo é o puzzle Setup→Payoff, não mexer nas metas. Ajustar número agora só esconderia a falta de decisão.

---

## 13. Experimente você mesmo

1. **Meta:** em `Data/Formato_Curta`, mude o `Goal` do Ato 3 para 9000 e jogue. Ficou difícil ou só mais longo?
2. **Pesos por ato:** em `Data/Cenas/Cena_Perseguicao`, mude `Weight Per Act` de `0, 1, 3` para `2, 1, 3`. Perseguição no Ato 1 quebra o ritmo?
3. **Artefato novo:** siga a seção 3 (ex.: "+100 Bônus para a Final Girl", com `Actor Tag = Tag_FinalGirl`) e coloque-o no `Conteudo_P0`.
4. **Regras de pavor:** em `Data/Regras`, teste `pavorLimit = 60` e `pavorImpactBonusAtLimit = 1.5`. Vale a pena "esticar" um ator até quase quebrar?
5. **Seed fixa:** jogue uma vez e anote a seed (aparece no Console e na tela final). Coloque-a em `GameSystems > RunPresenter > Seed`, repita as mesmas escolhas e confira se o resultado é idêntico.
6. **Testes:** rode o Test Runner. Depois mude `Assert.AreEqual(100, preview.Score)` para 101, veja ficar vermelho e desfaça.
7. **Log:** no fim de uma run, clique em **Copiar log da run**, cole num editor de texto e encontre `seed`, `choices` e a entrada `"type": "act"` de cada ato.
8. **Efeito em código:** adicione o `MultiplyTensionEffect` da seção 4, use-o num artefato e confira a linha no painel **Cálculo**.

---

## 14. Glossário rápido

| Termo | Em uma linha |
|---|---|
| **ScriptableObject** | Asset de dados (classe salva como arquivo `.asset`), editável no Inspector, sem cena. |
| **`[CreateAssetMenu]`** | Põe o ScriptableObject no menu Create da janela Project. |
| **`[SerializeReference]`** | Serializa o tipo real de cada item (polimorfismo), não só a classe base. |
| **`abstract` / `override`** | Classe/método que precisa ser completado pela subclasse / a versão da subclasse. |
| **PropertyDrawer** | Código de editor que muda como um tipo é desenhado no Inspector (`EffectDrawer`). |
| **`event Action<T>`** | Lista de funções a chamar quando algo acontece; `+=` assina, `?.Invoke` dispara. |
| **Lambda (`x => ...`)** | Função anônima curta, como uma method variable inline do GMS. |
| **Seed / `System.Random`** | Gerador próprio e reproduzível; o `random_set_seed` por instância. |
| **Corrotina** | Função `IEnumerator` que pausa com `yield return` e continua depois. |
| **`WaitForSeconds`** | Pausa uma corrotina por X segundos de jogo. |
| **NavMesh / NavMeshSurface** | Malha de áreas andáveis / componente que a constrói. |
| **NavMeshAgent** | Componente que anda pela NavMesh até um destino. |
| **IMGUI / `OnGUI`** | UI imediata, redesenhada várias vezes por frame (uma por evento). |
| **`Event.current.type`** | Qual evento de GUI está sendo processado (`Layout`, `Repaint`...). |
| **Assembly Definition** | Arquivo `.asmdef` que transforma uma pasta num assembly compilado à parte. |
| **Teste EditMode** | Método `[Test]` que roda no editor sem Play e verifica com `Assert`. |
| **Fake null** | Objeto Unity destruído/descarregado que responde `== null` mas não é null em C#. |
| **SerializedObject** | Acesso aos dados de um objeto pelo mesmo caminho do Inspector (seguro, com undo). |
