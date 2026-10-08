# 01 — Base, Greybox e Câmera

> Unity 6000.3.25f1 · URP · Cinemachine 3.1.7 · Input System novo

## 1. O que mudou

- Projeto organizado em `Assets/_HorrorTycoon/` (Scripts/Core, Run, Rooms, Actors, Scoring, Camera, UI, Replay + Data, Prefabs, Scenes, Materials, Docs).
- Um menu de editor (**Horror Tycoon > Construir Cena Greybox**) monta a cena `P0_Greybox` do zero: luz, chão, árvores, casa 3x2 com 6 cômodos, 4 atores-cápsula com rosto e as câmeras.
- Câmera isométrica de "diretor" (pan, giro, zoom) + close no rosto do ator ao clicar, com transição suave via Cinemachine.
- **Run In Background** ligado nas Player Settings: o jogo não pausa quando a janela perde o foco.

### O plano daqui pra frente (contexto)

A arquitetura combinada: conteúdo (cenas, relíquias, arquétipos) vira **ScriptableObjects** — assets de dados, parecido com um struct salvo como arquivo. Efeitos montáveis via `[SerializeReference]`, sinergias por tags, **lógica em C# puro** separada do visual (o visual só reage a eventos), RNG com seed e log de eventos para replay. O `ActorView` desta etapa é o primeiro exemplo: ele é **só visual**, não sabe nada de regras.

---

## 2. Tradutor GameMaker → Unity

| GameMaker | Unity | Observação |
|---|---|---|
| Room | **Scene** (`.unity`) | Também é um asset. Pode ter várias carregadas ao mesmo tempo. |
| Object | **GameObject + Components** | O GameObject é uma "caixa vazia"; o comportamento vem dos componentes (Transform, MeshRenderer, Collider, seus scripts). Composição em vez de herança. |
| Instance | GameObject na cena | Não existe "objeto vs instância" separado: o que está na cena *é* a instância. O "molde" reutilizável é o **Prefab**. |
| Create | `Awake()` / `Start()` | `Awake` roda ao nascer (bom para pegar referências próprias); `Start` roda antes do primeiro `Update` (bom quando depende de outros já inicializados). |
| Step | `Update()` | Roda **por frame**, não por step fixo. Use `Time.deltaTime`. |
| End Step | `LateUpdate()` | Depois de todos os `Update` — ideal para câmera. |
| Draw GUI | `OnGUI()` | Só como UI temporária aqui. UI de verdade virá depois. |
| Variáveis de instância | Campos `[SerializeField]` | Aparecem no **Inspector** e são salvos na cena. É como editar Creation Code, mas com campos tipados. |
| Parent de objeto | **Não é** parent de Transform | Ver abaixo. |
| Views / `camera_*` | `Camera` + **Cinemachine** | A Camera real renderiza; câmeras virtuais dizem *onde* ela deve estar. |
| `collision_point` / `instance_position` | `Physics.Raycast` | Raio 3D a partir do mouse, bate em Colliders. |
| `keyboard_check(ord("W"))` | `Keyboard.current.wKey.isPressed` | Input System novo. |
| `keyboard_check_pressed` | `.wasPressedThisFrame` | |
| `mouse_wheel_up()` | `Mouse.current.scroll.ReadValue().y` | Valor contínuo, não booleano. |
| Extensões / ferramentas do IDE | Pasta `Editor/` + `[MenuItem]` | Código que roda **no editor**, nunca vai para o build. |
| Asset (sprite, sound) | Asset + arquivo **`.meta`** | Ver armadilhas. |

### Parent no GMS ≠ parent no Unity

No GMS, "parent" é **herança**: `obj_enemy_bat` herda eventos de `obj_enemy` e chama `event_inherited()`.

No Unity, "parent" é **hierarquia espacial de Transform**: o filho se move, gira e escala junto com o pai. Não herda código nenhum. É o que usamos no rig de câmera: girar o `CameraRig` gira o `CameraArm` e a `CM_Iso` junto.

```
// GMS: posição "presa" a outro objeto, feito à mão no Step
x = owner.x + lengthdir_x(dist, owner.image_angle);

// Unity: basta ser filho; localPosition é relativa ao pai
child.SetParent(parentTransform, false);
child.localPosition = new Vector3(0f, 0f, -dist);
```

Herança de código em Unity existe, mas é C# normal (`class A : B`), e é pouco usada para comportamento — prefira componentes.

---

## 3. Passeio pelos 4 scripts

### 3.1 `Actors/ActorView.cs` — o "corpo" do ator

**O que faz:** guarda nome, referências às peças do rosto e a expressão atual (`Neutral`, `Happy`, `Tense`, `Scared`). Aplica a expressão mexendo escala/posição/rotação dos olhos, sobrancelhas e boca.

**Por que assim:** é a camada visual. Quando a lógica do jogo disser "Ator B está com medo", ela vai emitir um evento e o `ActorView` só reage com `SetExpression`. Quando houver modelo 3D, troca-se o miolo de `ApplyExpression` por blendshapes sem tocar na lógica.

```csharp
[SerializeField] private ActorExpression expression = ActorExpression.Neutral;

// Roda no EDITOR toda vez que um valor muda no Inspector.
// Não tem equivalente direto no GMS: é código rodando fora do Play.
private void OnValidate()
{
    ApplyExpression();
}
```

`FaceAnchor` é uma propriedade só-leitura com fallback — se ninguém ligou o rosto, usa a raiz:

```csharp
public Transform FaceAnchor => faceAnchor != null ? faceAnchor : transform;
```

### 3.2 `Camera/IsoCameraRig.cs` — a câmera do diretor

**O que faz:** WASD/setas e botão do meio = pan; Q/E = gira 45°; botão direito arrastado = giro livre; scroll = zoom. Pan limitado a ±15 em X/Z.

**Por que assim:** em vez de calcular a posição da câmera com trigonometria (o clássico `lengthdir` do GMS), montamos uma hierarquia: o **pivô** (`CameraRig`) fica no chão e só anda/gira em Y; o **braço** (`CameraArm`) tem o pitch e é afastado pela distância. A câmera virtual é filha do braço e só copia a posição dele.

```csharp
private void ApplyArm()
{
    Quaternion armRotation = Quaternion.Euler(pitch, 0f, 0f);
    cameraArm.localRotation = armRotation;
    // "Recua" o braço ao longo da própria direção inclinada: isso é o zoom.
    cameraArm.localPosition = armRotation * new Vector3(0f, 0f, -currentDistance);
}
```

Suavização independente de FPS — o equivalente correto de `x = lerp(x, alvo, 0.1)`, que no GMS funciona porque o step é fixo, mas no Unity variaria com o framerate:

```csharp
float zoomT = 1f - Mathf.Exp(-zoomSmoothing * Time.unscaledDeltaTime);
currentDistance = Mathf.Lerp(currentDistance, targetDistance, zoomT);
```

`unscaledDeltaTime` ignora `Time.timeScale` — a câmera continua respondendo mesmo com o jogo pausado. `InputEnabled` é desligado pelo controlador de foco durante o close.

### 3.3 `Camera/ActorFocusController.cs` — clique no ator, close no rosto

**O que faz:** clique esquerdo dispara um raio da tela; se acertar algo com `ActorView` acima na hierarquia, foca nele. Esc volta. Teclas 1–4 testam expressões. `OnGUI` mostra ajuda temporária.

```csharp
Ray ray = mainCamera.ScreenPointToRay(screenPosition);
if (Physics.Raycast(ray, out RaycastHit hit, 200f))
{
    // O raio bate no Collider do "Corpo"; o ActorView está no pai.
    ActorView actor = hit.collider.GetComponentInParent<ActorView>();
    if (actor != null) Focus(actor);
}
```

`GetComponentInParent` é o equivalente a "subir na hierarquia procurando quem tem esse script". Por isso o builder **remove os colliders** das peças do rosto: só o corpo recebe clique.

A câmera de foco é posicionada em `LateUpdate` (equivalente ao End Step): o ator já se moveu neste frame, então a câmera não "treme" atrasada. Fica na frente do rosto, levemente de lado (enquadramento 3/4), olhando para ele com `Quaternion.LookRotation`.

### 3.4 `Editor/GreyboxSceneBuilder.cs` — a cena feita por código

**O que faz:** `[MenuItem("Horror Tycoon/Construir Cena Greybox")]` adiciona o item no menu do Unity. Ao clicar: pede para salvar a cena atual, cria uma cena vazia, monta tudo, salva em `Scenes/P0_Greybox.unity` e adiciona às Build Settings.

**Por que assim:** dá para reconstruir a cena a qualquer momento, e o layout fica versionado como código legível no Git. Depois de gerada, a cena é normal — dá para editar à mão (mas rodar o menu de novo apaga essas edições).

Pontos que valem notar:

- `GameObject.CreatePrimitive` cria cubo/cápsula/esfera já com Mesh, Renderer e Collider — é o `instance_create` de primitivas.
- As árvores usam `new System.Random(7)`: **seed fixa**, sempre o mesmo layout. Mesmo princípio do RNG com seed do jogo.
- Materiais URP Lit são criados como assets em `Materials/` (ou reaproveitados se já existem).
- Campos `private [SerializeField]` são preenchidos via `SerializedObject` — o mesmo caminho que o Inspector usa:

```csharp
var view = root.AddComponent<ActorView>();
var so = new SerializedObject(view);
so.FindProperty("displayName").stringValue = actorName;
so.FindProperty("faceAnchor").objectReferenceValue = face.transform;
so.ApplyModifiedPropertiesWithoutUndo();
```

O nome em `FindProperty` é o nome **do campo em C#**. Renomeou o campo, quebra aqui (sem erro de compilação — só em execução).

---

## 4. O truque das prioridades do Cinemachine

Pense em várias **views** do GMS ligadas ao mesmo tempo, mas só uma mandando na tela. O **CinemachineBrain** (componente na Main Camera) olha todas as câmeras virtuais ativas e segue **a de maior `Priority`**. Quando a vencedora muda, ele faz o **blend** sozinho (aqui: EaseInOut, 0.6s).

| Câmera | Normal | Focando |
|---|---|---|
| CM_Iso | 10 | 10 |
| CM_Focus | 0 | **20** |

Então focar é só:

```csharp
PlaceFocusCamera(actor);              // posiciona a CM_Focus
focusCamera.Priority = focusPriority; // 20 > 10 -> Brain faz o blend
```

E desfocar é voltar para 0. Nenhuma interpolação manual de posição/rotação entre câmeras — o Brain cuida disso. No GMS você faria isso à mão com lerps entre duas posições de view.

Detalhe: a `CM_Iso` não tem componentes de movimento (Follow/Aim). Ela só fica onde o Transform dela estiver, e quem move o Transform é o nosso `IsoCameraRig`.

---

## 5. Hierarquia da cena

```
P0_Greybox
├── Directional Light          (luz fria, sombras suaves; ambiente flat azulado)
├── Ground_Floresta            (Plane 60x60)
├── Arvores
│   └── Arvore_00 … Arvore_39  (cilindros, seed 7)
├── Casa
│   ├── Fundacao
│   ├── Comodo_1 … Comodo_6
│   │   └── Piso               (cor por cômodo)
│   └── Paredes                (segmentos baixos, 1.2 de altura, com vãos de porta)
├── Atores
│   └── Ator A … Ator D        [ActorView]  (raiz nos pés, olhando para -Z)
│       ├── Corpo              (cápsula, tem Collider)
│       └── Rosto              (FaceAnchor)
│           ├── Olho_E, Olho_D
│           ├── Sobrancelha_E, Sobrancelha_D
│           └── Boca           (sem Collider)
├── Main Camera                [Camera, AudioListener, CinemachineBrain]
├── CameraRig                  [IsoCameraRig]  (yaw inicial 45°)
│   └── CameraArm              (pitch 35°, recuado pela distância)
│       └── CM_Iso             [CinemachineCamera, prioridade 10]
├── CM_Focus                   [CinemachineCamera, prioridade 0/20]
└── GameSystems                [ActorFocusController]
```

Os GameObjects vazios (`Arvores`, `Casa`, `Atores`) servem como "pastas" na hierarquia. Não custam praticamente nada.

---

## 6. Experimente você mesmo

1. **Pitch e limites ao vivo.** Dê Play, selecione `CameraRig` e mexa em `Pitch` (ex.: 20 e 60) e `Pan Limits` no Inspector. O efeito é imediato porque `ApplyArm` roda todo frame.
2. **Expressão sem Play.** Fora do Play, selecione `Ator A` e troque `Expression` no Inspector. O rosto muda na hora graças ao `OnValidate`.
3. **Enquadramento do close.** Em Play, foque um ator e ajuste `Face Distance`, `Side Offset` e `Height Offset` no `GameSystems`. Ache um close que pareça "cinema".
4. **Blend.** Selecione a `Main Camera` e mude o `Default Blend` do Brain (estilo e duração). Compare 0.2s com 1.5s.
5. **Perca as mudanças de propósito.** Em Play, mude qualquer valor, saia do Play e veja voltar ao anterior. (Dica: botão direito no componente > *Copy Component* antes de sair, e *Paste Component Values* depois.)
6. **Um 5º ator.** Abra `GreyboxSceneBuilder.cs`, adicione `("Ator E", new Color(...))` em `ActorSlots`, salve e rode o menu de novo. Repare que a posição X se recentraliza sozinha pela fórmula do loop.

---

## 7. Armadilhas de quem vem do GameMaker

- **Mudanças em Play mode não são salvas.** Tudo que você editar no Inspector durante o Play some ao parar.
- **`Update` é por frame, não por step.** Nada de `x += 4`: use `velocidade * Time.deltaTime`. Lerps também precisam compensar o tempo (ver 3.2).
- **Não existe `instance_create(obj)` mágico.** Ou você cria um GameObject vazio e adiciona componentes (`new GameObject` + `AddComponent`), ou instancia um **Prefab** com `Instantiate`. Os prefabs virão nas próximas etapas.
- **Namespaces.** Os scripts vivem em `HorrorTycoon.Actors`, `HorrorTycoon.Cameras`, `HorrorTycoon.EditorTools`. Para usar uma classe de outro namespace, precisa de `using HorrorTycoon.Actors;` no topo. No GMS tudo é global.
- **`.meta` é sagrado.** Todo asset tem um `.meta` com um **GUID**; cenas e prefabs referenciam assets por esse GUID, não pelo caminho. Sempre commite o `.meta` junto, e mova/renomeie arquivos **dentro do Unity**, não pelo Explorer — senão as referências quebram (o material some do ator, o script some do GameObject).
- **3D e Y para cima.** No GMS, Y cresce para baixo e o mundo é 2D. Aqui Y é altura, o "chão" é o plano **X/Z**, e +Z é "para frente". Por isso o pan e os limites mexem em X e Z.

---

## 8. Glossário rápido

| Termo | Significado |
|---|---|
| **GameObject** | Entidade da cena; container de componentes. |
| **Component** | Pedaço de comportamento/dado anexado a um GameObject. |
| **MonoBehaviour** | Classe base dos seus scripts que viram componentes. |
| **Transform** | Posição/rotação/escala + hierarquia pai/filho. |
| **Inspector** | Painel que mostra/edita os campos dos componentes. |
| **`[SerializeField]`** | Faz um campo privado aparecer no Inspector e ser salvo. |
| **Prefab** | Molde reutilizável de GameObject (o "objeto" do GMS). |
| **ScriptableObject** | Asset de dados puro, sem existir na cena. |
| **Collider** | Forma física usada por colisões e raycasts. |
| **Raycast** | Raio que retorna o primeiro Collider atingido. |
| **Quaternion** | Representação de rotação; `Quaternion.Euler` converte de graus. |
| **CinemachineCamera** | Câmera virtual: descreve onde a câmera real deve estar. |
| **CinemachineBrain** | Fica na Camera real; segue a virtual de maior prioridade e faz blends. |
| **Editor script** | Código em pasta `Editor/`; roda só no editor, nunca no build. |
| **`.meta`** | Arquivo com o GUID do asset; precisa ir pro Git. |
