# 05 — Casa em escala real, corte de parede e clima

> Pré-requisito: docs 01–04. Aqui entra a casa nova (metros de verdade), o sorteio de cômodos por run, a névoa, o corte de parede estilo *The Sims*, móveis por código e o pós-processamento.
> Para ver tudo: menu **Horror Tycoon > Construir Cena Greybox** e depois Play.

---

## 1. O que mudou

- **Escala real (1 unidade = 1 metro):** pé-direito 2,7 m, portas internas 0,9 m (porta da frente 1,0 m) com 2,1 m de altura, paredes de 12 cm, atores com 1,75 m.
- **Planta nova:** um **corredor central** (slot 0, 2 m × 12 m) e **6 cômodos** de 4,5 × 4 m, três de cada lado.
- **Cômodos sorteados:** a planta é fixa, mas **qual cômodo fica em cada slot** muda a cada run (pela seed).
- **Névoa de guerra:** cômodo não visitado aparece como um bloco escuro, sem móveis e sem luz.
- **Corte de parede:** as paredes entre a câmera e a casa abaixam até virar rodapé.
- **Móveis por código**, luz de teto por cômodo (algumas piscam), luz da varanda, lua, neblina e **pós-processamento** "cara de filme".
- **Dados:** agora são **8 passos por ato** (`Formato_Curta`), e o corredor é um `RoomDef` próprio, "Corredor".

```
  z=+6  ┌──────────┬──┬──────────┐
        │ slot 3   │  │ slot 6   │
        ├──────────┤  ├──────────┤
        │ slot 2   │C │ slot 5   │     C = corredor (slot 0)
        ├──────────┤  ├──────────┤
        │ slot 1   │  │ slot 4   │
  z=-6  └──────────┴▯▯┴──────────┘  ← porta da frente (x = 0)
```

---

## 2. Por que escala real

No GMS você escolhe a escala livremente (32 px = 1 tile, e pronto). Na Unity, **1 unidade = 1 metro** não é só convenção: física, luz (alcance das point lights), neblina, NavMesh e câmera assumem isso. Com a casa em metros:

- a luz de 2,5 m de altura ilumina um cômodo de 4 m do jeito que você espera;
- o tamanho dos móveis vem de medidas reais (mesa a 0,74 m, cama de 1,4 × 2 m), sem chute;
- portas de 0,9 m fazem a casa parecer **apertada**, e isso já é clima.

**Conexão com o doc 03 (bug 2):** antes as portas tinham 1,8 m porque o agente Humanoid tinha raio 0,5. Agora o raio caiu para **0,25** (ver seção 8), então portas reais de 0,9 m passam.

---

## 3. Slot × cômodo: sorteio por run

Duas ideias separadas:

| Conceito | O que é | Onde |
|---|---|---|
| **Slot** | um retângulo físico da planta (0 = corredor, 1..6 = cômodos) | `RoomAnchor.slotIndex`, criado pelo construtor de cena |
| **Cômodo** | o `RoomDef` (Cozinha, Porão…) com regras e visual | `Data/P1/Salas` |

No construtor de `FilmRun`, o índice da lista de salas **é** o slot:

```csharp
var defs = content.Rooms.Where(r => r != null).ToList();
if (content.ShuffleRooms) Rng.Shuffle(defs);
// slot 0 = corredor (HubRoom), já descoberto; depois os cômodos na ordem sorteada
```

E o `Shuffle` é um **Fisher-Yates** usando o mesmo `GameRandom` com seed:

```csharp
for (int i = list.Count - 1; i > 0; i--)
{
    int j = random.Next(i + 1);
    (list[i], list[j]) = (list[j], list[i]);
}
```

Percorre a lista de trás para frente e troca cada item com um sorteado entre os que ainda não foram fixados. Toda ordem tem a mesma chance.

**Analogia GMS:** `random_set_seed(seed)` + `ds_list_shuffle(lista)`. A diferença é que aqui o gerador é um **objeto** (`GameRandom`), e não um estado global: a decoração (móveis espalhados, piscar da lâmpada) usa outros geradores e não "rouba" números da lógica. Mesma seed → mesma casa → mesmo replay.

Depois, no `RunPresenter.Start`, cada `RoomAnchor` da cena recebe o cômodo do seu slot:

```csharp
anchor.Bind(Run.Rooms[slot], runSeed);
anchor.SetDiscovered(Run.Rooms[slot].Discovered);
```

`Bind` apaga os móveis antigos, monta os novos (`FurnitureKit.Build`), pinta o piso e aplica cor e piscar da lâmpada do `RoomDef`.

---

## 4. HouseMap: de grade para estrela

O grafo (doc 03) deixou de ser uma grade. Agora é uma **estrela**:

```
fora (-1) ── Corredor (0) ──┬── 1
                            ├── 2
                            ├── ...
                            └── 6
```

- fora → corredor: 1 porta;
- fora → qualquer cômodo: 2 portas;
- cômodo → cômodo: **sempre 2 portas** (sai para o corredor, entra no outro).

O `Doors()` continua sendo a mesma BFS: não foi preciso mexer nela, só em como os vizinhos são ligados no construtor (`Link(hub, i)` para cada cômodo). Isso é o ganho de ter separado "regra" (grafo) de "visual" (cena).

As divisórias entre cômodos do mesmo lado **não têm porta** na cena, para bater com o grafo: tudo passa pelo corredor.

**Por que um corredor:** é a **área de convivência**. Todos os atores cruzam ali, então é o lugar natural para futuras interações ator-com-ator. Por enquanto o `RoomDef` "Corredor" não tem encontros nem palco; ele está lá como espaço e como hub do grafo.

---

## 5. Construindo a casa por código

Tudo em `Editor/GreyboxSceneBuilder.cs`. A cena é montada de uma vez (como um `room_goto` para uma room que o próprio código cria, instância por instância).

**Slot (`BuildSlot`)**, um GameObject com filhos:

| Filho | O que é |
|---|---|
| `Piso` | cubo fino; material branco `M_Piso`, a cor vem do `RoomDef` |
| `Interior` | pai vazio dos móveis (girado 180° no lado leste) |
| `Nevoa` | cubo escuro, material **URP Unlit**, **sem collider** (só nos cômodos, não no corredor) |
| `Lampada` | point light a 2,5 m, sem sombra |

O componente `RoomAnchor` fica na raiz do slot e guarda as referências (`Configure`).

**Paredes (`WallWithDoors`)**: uma parede reta de A até B, cortada nos vãos de porta. Para cada porta saem dois pedaços: a parede até o vão e uma **verga** (o pedaço acima da porta, de 2,1 a 2,7 m). A verga não tem collider, para não atrapalhar o NavMesh.

As paredes são **divididas por cômodo** (trechos de ~4 m), e não uma peça só por lado da casa. Motivo: o corte de parede decide **por peça**. Uma parede única de 12 m abaixaria inteira; em pedaços, só abaixa o trecho que tapa a visão.

**Névoa sem collider + Unlit:**
- sem collider → o clique atravessa e acerta o piso (o card da sala abre normal);
- Unlit → não recebe luz, então fica escura mesmo com a lua e a varanda.

`SetDiscovered(false)` liga a névoa, esconde o `Interior` e desliga a lâmpada. Quando alguém entra, `FilmRun` marca `Discovered = true` e o `RunPresenter` chama `SetDiscovered(true)`.

---

## 6. Cor por instância: MaterialPropertyBlock

Todos os pisos usam **o mesmo material** `M_Piso`. Se você fizesse `renderer.material.color = ...`, a Unity criaria uma **cópia** do material para cada piso (vazamento fácil de memória e quebra o batching). O `RoomAnchor` faz assim:

```csharp
floorRenderer.GetPropertyBlock(block);
block.SetColor(BaseColorId, c);        // _BaseColor do shader URP Lit
floorRenderer.SetPropertyBlock(block);
```

O bloco é um "pacote de overrides" que vale **só para aquele renderer**, sem tocar no material.

**Analogia GMS:** é como `image_blend` numa instância: todas usam o mesmo sprite, cada uma com a sua cor, sem duplicar o sprite. O destaque ao passar o mouse (`SetHighlighted`) é só um `Color.Lerp` com o amarelo antes de aplicar.

A lâmpada que pisca fica no `Update` do `RoomAnchor`: de tempos em tempos sorteia apagar (25%) ou variar a intensidade. Usa `UnityEngine.Random` de propósito: é cosmético, não pode influenciar a seed da run.

---

## 7. Corte de parede estilo The Sims (`WallCutaway`)

Cada pedaço de parede tem esse componente. A cada frame ele decide: "estou tapando a visão?". Se sim, encolhe até 0,35 m (rodapé); vergas somem (altura 0).

```csharp
bool facing  = Mathf.Abs(Vector3.Dot(normal, fwd)) > 0.35f;
bool inFront = Vector3.Dot(toWall, fwd) < 0.6f;
bool cut = facing && inFront;
```

**Produto escalar (dot) sem matemática pesada:** `Dot(a, b)` com vetores de tamanho 1 diz **o quanto eles apontam para o mesmo lado**. 1 = mesmo sentido, 0 = perpendicular, -1 = opostos.

- `normal` é para onde a face da parede aponta; `fwd` é para onde a câmera olha (achatado no chão). `|Dot| > 0.35` → a parede está **de frente** para a câmera, não de lado. O valor absoluto existe porque uma parede interna tem duas faces: tanto faz qual.
- `toWall` vai do pivô da câmera (o ponto que ela olha) até a parede. `Dot(toWall, fwd) < 0.6` → a parede **não está depois** do pivô, ou seja, está do lado da câmera. As paredes do fundo ficam inteiras.

**Encolher sem afundar:** o cubo da Unity tem o pivô **no centro**. Mudar só a escala Y faria a parede encolher para o meio. Por isso o `Apply()` reposiciona: `y = baseY + altura / 2`. No GMS é o mesmo problema de mexer em `image_yscale` com a origem centralizada; lá você poria a origem embaixo, aqui o código compensa.

**Por que `LateUpdate`:** roda **depois** de todos os `Update` do frame, quando a câmera já se moveu. É o **End Step** do GMS. Se fosse no `Update`, a parede às vezes usaria a posição da câmera do frame anterior.

A transição usa `1 - Exp(-speed * dt)` com `Time.unscaledDeltaTime`: suave igual em qualquer FPS e continua funcionando se o jogo estiver pausado (`timeScale = 0`).

---

## 8. NavMesh: mudando o agente por código

O bake do NavMesh usa o **tipo de agente** (Humanoid) das Project Settings, não o `radius` do componente no ator (lição do doc 03). Para não depender de clique manual, o construtor altera o arquivo de configuração direto:

```csharp
var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset");
var so = new SerializedObject(assets[0]);
var humanoid = so.FindProperty("m_Settings").GetArrayElementAtIndex(0);
humanoid.FindPropertyRelative("agentRadius").floatValue = 0.25f;
humanoid.FindPropertyRelative("agentHeight").floatValue = 1.75f;
so.ApplyModifiedPropertiesWithoutUndo();
```

`SerializedObject` é "editar pelo mesmo caminho que o Inspector usa". Serve para qualquer asset, inclusive arquivos de `ProjectSettings` que não têm API pública.

Conta da porta: vão livre ~0,8 m − 2 × 0,25 = **0,3 m** de passagem no NavMesh. Passa, mas é justo; se um dia a porta "sumir", comece por aqui.

**Analogia GMS:** é o tamanho da célula do `mp_grid` + o quanto você "engorda" as paredes ao marcar células ocupadas. Raio menor = corredores estreitos continuam navegáveis.

Os móveis **não têm collider** (`FurnitureKit` destrói com `DestroyImmediate`, porque o NavMesh é calculado logo depois). Então não bloqueiam o caminho nem o clique; os atores param perto do centro (`NextStandPoint`), que fica livre.

---

## 9. Móveis por código (`FurnitureKit`)

Cada estilo (`FurnitureStyle`: Hallway, Kitchen, Living, Bathroom, Bedroom, Basement, Attic) é um `case` que empilha cubos e cilindros com **medidas reais**. Peças compostas (`Table`, `Chair`, `Sofa`) são funções curtas, fáceis de trocar por um modelo 3D depois.

**Convenção da porta:** em coordenadas **locais** do `Interior`, a porta fica no lado **+X**, no meio (`z = 0`). Os cômodos do lado leste têm a porta no lado oposto do mundo, então o construtor gira o `Interior` deles em **180°**. Resultado: o mesmo código de móveis serve para os dois lados, e nenhum móvel deve ocupar a faixa `x > hw - 1, |z| < 0.8` (o `ScatterBoxes` já desvia dela).

**Cache de material:** `Mat(color)` guarda um material URP Lit por cor num `Dictionary`. Dez caixas de papelão = 1 material, não 10.

**Seed dos móveis:** `seed + slotIndex * 101`. Caixas espalhadas mudam de run para run, mas a mesma seed repete a mesma bagunça.

---

## 10. Luz e clima

| Peça | Configuração | Papel |
|---|---|---|
| Lua (directional) | 0,9, azulada, sombra suave | contorno geral, frio |
| Luz ambiente | `RenderSettings.ambientLight` escuro azulado | nada fica 100% preto |
| Neblina | `RenderSettings.fog`, ExponentialSquared, densidade 0,02 | esconde o horizonte e a floresta |
| Lâmpadas | point light por slot, cor do `RoomDef` | ilha de luz quente em cada cômodo |
| Varanda | point light âmbar na porta da frente | a casa não some antes de entrar |

`RenderSettings` é global da cena, parecido com ligar `gpu_set_fog` para a room inteira no GMS.

---

## 11. Pós-processamento

Asset: `Assets/_HorrorTycoon/Settings/PP_FilmeDeTerror.asset` (um `VolumeProfile`), usado por um **Volume global** (`PostFX`) na cena.

| Efeito | Valor | Para quê |
|---|---|---|
| Vignette | 0,4 | escurece as bordas, foco no centro |
| FilmGrain | 0,35 | textura de película |
| ColorAdjustments | contraste +18, saturação −30 | imagem dura e lavada |
| Tonemapping | ACES | contraste "de cinema" nas luzes |
| Bloom | 0,4 (limiar 1) | lâmpadas brilham um pouco |

Dois detalhes técnicos:

1. **Sub-assets:** cada efeito é um objeto separado. Se não for salvo **dentro** do arquivo do perfil (`AssetDatabase.AddObjectToAsset`), ele some ao recarregar o projeto e o perfil fica vazio.
2. **A câmera precisa ligar o pós:** `GetUniversalAdditionalCameraData().renderPostProcessing = true`. Sem isso, o Volume existe mas não aparece nada.

O perfil só é criado **se não existir**: reconstruir a cena não apaga os seus ajustes.

**Analogia GMS:** é o que você faria desenhando a `application_surface` numa surface e passando um shader de vinheta/granulação no Post Draw. Aqui o URP faz essa etapa por você; você só liga efeitos e ajusta números.

---

## 12. Ferramentas para arte por código

| Caminho | Situação | Observação |
|---|---|---|
| **Primitivas + código** | **atual** | rápido, versionável, mas tudo é caixa |
| **ProBuilder** | próximo passo | pacote gratuito da Unity; API de script (`ShapeFactory`) e o MCP tem `manage_probuilder`. Bom para paredes com vão de verdade e props com geometria real |
| **Modelos por IA** (Meshy/Tripo via `generate_model` do MCP) | depois | exige chaves de API pagas |

O que mais muda o clima pelo menor custo, nessa ordem: **escala**, **iluminação** e **pós-processamento**. Nenhum dos três precisa de modelo novo.

---

## 13. Como mexer

| Quero mudar… | Onde |
|---|---|
| tamanho da casa, pé-direito, portas, raio/altura do agente | constantes no topo de `GreyboxSceneBuilder` (`WallHeight`, `DoorWidth`, `HouseHalfWidth`, `AgentRadius`…) |
| vinheta, grão, cor, bloom | `PP_FilmeDeTerror.asset` em `Assets/_HorrorTycoon/Settings`, no Inspector (vale ao vivo, até no Play) |
| móveis, cor da luz, luz piscando de um cômodo | `RoomDef` em `Data/P1/Salas`, cabeçalho **Visual** (`furniture`, `lightColor`, `flickeringLight`) |
| cor do piso | `RoomDef` → `floorColor` |
| passos por ato | `Formato_Curta` |

Mudou constante do construtor? Rode **Horror Tycoon > Construir Cena Greybox** de novo (edições manuais na cena se perdem; o perfil de pós não).

---

## 14. Experimente

1. Mude `DoorWidth` para 0,7 e reconstrua. Os atores ainda passam? Ligue a visualização do NavMesh e veja o vão.
2. Rode duas vezes com a mesma seed e confira que os cômodos caem nos mesmos slots; troque a seed e compare.
3. No `PP_FilmeDeTerror`, zere a saturação e o grão durante o Play. Quanto do "terror" some?
4. Marque `flickeringLight` na Cozinha e mude `lightColor` para vermelho.
5. Em `WallCutaway`, troque 0,6 por 0 no teste `inFront` e gire a câmera: quais paredes deixam de cortar?
