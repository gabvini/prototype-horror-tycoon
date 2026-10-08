# 06 — Câmera de cinema e monitor do diretor

> Pré-requisito: docs 01–05. Aqui a câmera ganha um **diretor**: ela nunca fica parada, filma de longe com lente de zoom, corta seco entre planos, treme como câmera na mão e vai até a porta da sala que você clicou. Os atores fazem hora enquanto você pensa, e o **monitor do diretor** mostra a planta da casa num canto, clicável.
> Para ver tudo: menu **Horror Tycoon > Construir Cena Greybox**, Play, aperte **M** e não mexa em nada por alguns segundos.

---

## 1. O que mudou

- **Câmera sempre em movimento.** Sem o monitor, a visão da casa gira devagar e o zoom "respira". Com o monitor aberto, a tela principal vira **filme**: planos de longe com teleobjetiva, cortes secos e tremor de câmera na mão.
- **Mexeu na câmera → visão da casa.** Qualquer comando (WASD, Q/E, scroll, arrastar) afasta a câmera para mostrar a casa. Com o monitor aberto, depois de alguns segundos parado o filme volta.
- **Card de sala → zoom na porta.** Com monitor: um plano de lente na porta. Sem monitor: a visão da casa desliza até ela.
- **Monitor do diretor (M).** Uma segunda câmera, de cima, desenha a planta numa textura. A HUD mostra essa textura no canto inferior direito e o clique nela funciona como clicar na casa.
- **Vida de set** (`ActorIdle`): atores parados conversam, andam um pouco e olham em volta. Param quando estão encenando.
- **Faixas pretas (letterbox)** nos planos de filme e no close de reação.

Arquivos envolvidos: `Camera/CinematicDirector.cs`, `CameraFocus.cs`, `DirectorMonitor.cs`, `CameraVantage.cs`, `IsoCameraRig.cs`; `Rooms/WallCutaway.cs`, `RoomAnchor.cs`; `Actors/ActorIdle.cs`; `Run/RunPresenter.cs`; `UI/RunHud.cs`; `Editor/GreyboxSceneBuilder.cs`.

---

## 2. Várias câmeras virtuais, uma câmera real

| Objeto | Tipo | Prioridade | Quem controla |
|---|---|---|---|
| `Main Camera` | `Camera` + `CinemachineBrain` | — | o Brain (copia a câmera virtual vencedora) |
| `CM_Iso` | `CinemachineCamera` | 10 (fixa) | `IsoCameraRig` (visão da casa) |
| `CM_Cine` | `CinemachineCamera` | 0 / **15** | `CinematicDirector` (planos de filme e acompanhar ator) |
| `CM_Porta` | `CinemachineCamera` | 0 / **17** | `CinematicDirector` (plano da porta, com monitor) |
| `CM_Focus` | `CinemachineCamera` | 0 / **20** | `ActorFocusController` (close de reação) |
| `MonitorCam` | `Camera` comum, ortográfica | — | ninguém: fica parada em cima da casa |

A regra do Brain é uma só: **mostra a câmera virtual de maior prioridade**. O diretor nunca "liga a câmera X"; ele só sobe e desce prioridades em `EnterMode`:

```csharp
cineCamera.Priority = (next == Mode.Cinematic || next == Mode.Track) ? activePriority : 0;
doorCamera.Priority = doorWithMonitor ? doorPriority : 0;
```

Com as duas em 0, sobra a `CM_Iso` (10). O close (20) passa por cima de tudo sem o diretor precisar saber.

**Analogia GMS:** cada `CinemachineCamera` é uma câmera criada com `camera_create`, e o Brain é um `view_camera[0] = a_de_maior_prioridade` automático, com transição suave de brinde. Você não escolhe a câmera da view; escolhe números.

---

## 3. Corte seco × blend

Duas formas de trocar de imagem, e o filme usa as duas de propósito:

| Situação | O que acontece | Por quê |
|---|---|---|
| **Troca de câmera virtual** (Iso → Cine, Cine → Porta, qualquer → Focus) | **blend** `EaseInOut` de 0,6 s (`DefaultBlend` do Brain, definido pelo construtor) | o Brain interpola entre duas câmeras diferentes |
| **Próximo plano dentro da `CM_Cine`** | **corte seco** | o diretor teletransporta a mesma câmera (`SetPositionAndRotation`); a câmera ativa não mudou, então não há blend |

```csharp
if (autoCut && shotTime >= shotLength) NextShot(); // corte seco para o próximo plano
```

`NextShot` escolhe um plano novo e o `BeginShot` já põe a câmera na posição inicial (`PlaceCamera(cam, 0f)`). No frame seguinte a imagem está em outro lugar: é um corte de montagem.

**Analogia GMS:** `camera_set_view_pos` direto = corte. Fazer `lerp` da posição antiga para a nova por vários Steps = blend. Entre câmeras, o Brain faz o lerp; dentro de uma câmera, quem decide é você.

---

## 4. Os quatro modos (máquina de estados)

```csharp
public enum Mode { Cinematic, Track, Overview, DoorFocus }
```

| Modo | Quando | O que a tela mostra |
|---|---|---|
| `Overview` | monitor fechado, ou o jogador mexeu na câmera há menos de `returnToCinematicAfter` | `CM_Iso`, com giro e respiração |
| `Cinematic` | monitor aberto e ninguém mexendo | `CM_Cine`, planos sorteados com cortes |
| `Track` | fase `Busy` com ator selecionado **e** monitor aberto | `CM_Cine` parada no esconderijo, girando atrás do ator |
| `DoorFocus` | fase `Idle` com card de sala aberto | com monitor: `CM_Porta`; sem monitor: `CM_Iso` deslizando até a porta |

Todo frame, no `LateUpdate`:

1. `DecideMode()` olha a fase do `RunPresenter`, o card aberto, o monitor e o `LastInputTime` do rig, e devolve o modo desejado.
2. Se mudou (ou o card trocou de sala, ou o monitor abriu/fechou com o card aberto), `EnterMode(wanted)` faz a **entrada** do estado: prioridades, `AutoDrift`, primeiro plano.
3. Um `switch (mode)` roda o **corpo** do estado: anda o plano, acompanha o ator ou só publica o foco.

Detalhe do `DoorFocus`: se o jogador abre o card e **depois** mexe na câmera (`lastInput > cardOpenedAt`), o diretor respeita e vai para `Overview`.

**Analogia GMS:** é o `switch (state)` que você escreve no Step. `EnterMode` é o `if (state != prev_state) { ... }` que roda uma vez na troca. Está no `LateUpdate` = **End Step**: os atores e o rig já se moveram neste frame quando a câmera decide onde olhar.

---

## 5. Teleobjetiva: FOV pela altura do quadro

A ideia de direção é "alguém filmando escondido, de longe". A câmera **fica longe** e o enquadramento vem do **FOV**, não da distância. Cada plano diz quantos metros cabem na vertical da tela (`frameHeight`), e o FOV sai de um triângulo retângulo:

```csharp
public static float FovFor(float frameHeight, float distance)
{
    return Mathf.Clamp(2f * Mathf.Atan(frameHeight * 0.5f / Mathf.Max(0.5f, distance)) * Mathf.Rad2Deg, 2.5f, 60f);
}
```

Meia altura do quadro ÷ distância = tangente de meio ângulo. Para um quadro de 1,8 m (`mediumFrame`):

| Distância | 5 m | 10 m | 15 m | 20 m |
|---|---|---|---|---|
| FOV | 20,4° | 10,3° | 6,9° | 5,2° |

Os planos de filme ficam a 11–20 m (`watchDistance`), então o FOV anda entre 5° e 9° (a `CM_Iso` usa 30°). Resultado: perspectiva achatada, fundo "colado" no ator, pouco cenário em volta. Parece que alguém está **mirando** naquela pessoa.

Um plano é: posição inicial e final (`camFrom`/`camTo`), quadro inicial e final (`frameFrom`/`frameTo`) e duração. `PlaceCamera` faz `SmoothStep` de 0 a 1 ao longo do plano e interpola os dois. **Zoom lento** = `frameTo` menor que `frameFrom`. Como o FOV é recalculado todo frame pela distância real, o enquadramento continua certo se o ator anda.

O close de reação (`ActorFocusController`) usa a mesma função: fica a ~9 m do rosto (`faceDistance`) com quadro de 0,8 m.

**Analogia GMS:** é como definir a view por "quanto do mundo cabe" (`camera_set_view_size`) em vez de pensar em zoom: você descreve o resultado e a conta acha o parâmetro.

---

## 6. Tipos de plano

| Plano | Quadro (m) | Quando |
|---|---|---|
| **Estabelecimento** (`EstablishingShot`) | 13 → 10 | a casa inteira de 24–30 m, 6–9 m de altura. **Sempre o primeiro plano**; depois, com chance `establishingChance` |
| **Grupo** (`GroupShot`) | do tamanho do grupo | 2+ atores no mesmo cômodo, 50% |
| **Zoom assustador** (`CreepZoomShot`) | 7 → 0,9 | câmera parada, fecha no rosto em 7–9 s (chance `creepZoomChance`) |
| **Observando** (`WatchShot`) | `mediumFrame` × 1,3 → `mediumFrame` | o resto: plano médio, zoom bem lento |
| **Porta** (`DoorShot`) | 3,6 → 2,8 (corredor: 4,5 → 3,6) | card aberto com monitor; 6 s, **não corta sozinho** |

Escolha do assunto em `NextShot`: um ator vivo sorteado; o selecionado tem 50% de preferência e, logo depois de um `Track`, é **sempre** ele (`preferSelected`), para mostrar quem acabou de agir.

### De onde filmar (`PickVantage`)

Candidatos: os marcadores `CameraVantage` (esconderijos que o `SceneryBuilder` espalha atrás de arbustos, doc 07) mais 10 pontos sorteados em volta do alvo (`watchDistance`, `watchHeight`). Descarta o que estiver longe ou perto demais, testa a linha de visão e dá uma nota: sorteio + bônus se estiver na direção preferida (no plano da porta, `DoorOutward`: filmar pelo lado de fora do cômodo).

```csharp
var hits = Physics.RaycastAll(from, dir / dist, dist - 0.6f);
foreach (var h in hits)
{
    if (h.distance < foregroundAllowance) continue;                       // arbusto na frente da lente: pode
    if (h.collider.GetComponentInParent<WallCutaway>() != null) continue; // paredes são cortadas
    if (h.collider.GetComponentInParent<ActorView>() != null) continue;
    if (h.collider.GetComponentInParent<RoomAnchor>() != null) continue;  // piso
    return false;
}
```

Os primeiros `foregroundAllowance` metros (3) podem ter obstáculo: é o mato na frente que vende o "espiando". Paredes não contam porque vão abaixar (seção 9). Qualquer outra coisa no caminho (uma árvore a 8 m) reprova o ponto.

**Analogia GMS:** `RaycastAll` é o `collision_line_list`: devolve **todas** as instâncias na linha e você filtra. `GetComponentInParent<WallCutaway>()` é o "essa instância é filha de `obj_parede`?".

---

## 7. Câmera na mão (Perlin noise)

```csharp
float n = Time.unscaledTime * 0.35f;
pos += new Vector3(Mathf.PerlinNoise(n, noiseSeed) - 0.5f, ...) * (shakePosition * 2f);
float deg = shakeDegrees * Mathf.Clamp(fov / 25f, 0.15f, 1f);
```

`Random.value` todo frame daria terremoto: cada frame sem relação com o anterior. `PerlinNoise` devolve 0..1 que **muda suave** com o tempo; menos 0,5 vira um balanço em volta de zero. Cada eixo lê um ponto diferente do ruído (`noiseSeed`, `+7`, `+3`) para não balançarem juntos.

O tremor de **ângulo** diminui com teleobjetiva: com FOV de 5°, 0,35° já sacode boa parte do quadro, então ele cai até 15%.

### UnityEngine.Random × seed da run

Tudo no diretor (e no `ActorIdle`) usa `UnityEngine.Random`, **nunca** o `GameRandom` da run. É a regra da lâmpada piscando do doc 05: câmera e vida de set são cosméticas. Se usassem a seed, o número de planos sorteados (que depende de quanto tempo você ficou parado) mudaria os resultados, e o replay quebraria.

---

## 8. Track: o observador gira, não anda

Ao entrar no Track, o diretor escolhe **um** esconderijo e **fica lá**. Todo frame só gira a câmera para continuar olhando o ator (panorâmica), com o lerp independente de FPS do doc 05:

```csharp
trackLook = Vector3.Lerp(trackLook, tracked.position + Vector3.up * 1.3f, k); // k = 1 - Exp(-3 * dt)
```

A panorâmica atrasa um pouquinho, como um operador de verdade. O quadro é fixo (`mediumFrame` × 1,4), mas o FOV é recalculado pela distância: o ator se afastando continua do mesmo tamanho. Sem monitor não existe Track: você vê da visão da casa.

---

## 9. Foco compartilhado: `CameraFocus` + `WallCutaway`

O corte de parede precisa saber "para onde a câmera olha". No doc 05 era o pivô da câmera isométrica; agora há várias câmeras. Quem está no controle **publica** o foco numa classe estática:

```csharp
public static class CameraFocus
{
    public static Vector3 Point { get; private set; }
    public static float CutMargin { get; private set; } = 0.6f;
    public static void Set(Vector3 point, float cutMargin) { ... }
}
```

O diretor chama `CameraFocus.Set` todo frame em cada modo; o close do ator é publicado por último, para vencer. Cada `WallCutaway` só lê. O `0.6` fixo do teste `inFront` do doc 05 virou `CameraFocus.CutMargin`:

| Margem | Efeito | Usado em |
|---|---|---|
| `0.6` | corta tudo entre a câmera e o foco, e um pouco além | planos de filme, Track, Overview, close |
| `-0.3` | só corta paredes **claramente na frente** do foco | plano da porta: a parede da porta fica de pé, senão não há porta para filmar |

No `DoorShot` o foco publicado é a **própria porta** (`fixedFocus`), não o ponto que a lente olha (meio metro para dentro).

**Analogia GMS:** uma **variável global** (`global.cam_focus_x`, `global.cam_cut_margin`) que a câmera escreve e todas as paredes leem no End Step. `static` = existe uma só, sem instância e sem referência no Inspector.

---

## 10. Visão da casa viva (`IsoCameraRig`)

| Membro | O que faz |
|---|---|
| `LastInputTime` | `Time.unscaledTime` do último comando do jogador (WASD/setas, botão do meio, Q/E, botão direito arrastado, scroll) |
| `AutoDrift` | o diretor liga só no `Overview`. Após `driftDelay` (2 s) sem mexer, gira `driftDegreesPerSecond` (4°/s) e o zoom respira ±`breatheAmount` (2,5 m) num ciclo de `breathePeriod` (26 s) |
| `ZoomOutAtLeast(d)` | afasta até pelo menos `d`, **nunca aproxima**. O diretor chama com `overviewDistance` (22) ao entrar no `Overview` |
| `GlideTo(ponto, d)` | desliza o pivô até a porta e põe o zoom em `d` (card aberto sem monitor). Mexer cancela |

A respiração entra e sai devagar (`breatheWeight`, 0,3 por segundo) e é somada só no braço (`ApplyArm`), sem mexer no seu zoom de scroll.

**Analogia GMS:** em vez de um `alarm[0]` que você rearma a cada tecla, guarda-se **o momento** da última tecla e compara com o relógio: `Time.unscaledTime - lastInput < returnToCinematicAfter` é o "alarme ainda não disparou". A respiração é o velho `dsin(current_time * k) * amplitude` somado ao zoom.

---

## 11. Monitor do diretor: RenderTexture

`DirectorMonitor` cria uma `RenderTexture` (640 × 480) e manda a `MonitorCam` desenhar **nela** em vez de na tela:

```csharp
texture = new RenderTexture(resolution.x, resolution.y, 16) { name = "RT_MonitorDoDiretor" };
monitorCamera.targetTexture = texture;
```

A `MonitorCam` é uma `Camera` **comum** (ortográfica, `orthographicSize` 8,5, a 16 m de altura olhando reto para baixo), não Cinemachine: o Brain só manda na `Main Camera`. Monitor fechado → `monitorCamera.enabled = false`, ela nem renderiza. O `OnDestroy` libera a textura (`Release`): memória de vídeo não é coletada sozinha.

A `RunHud.DrawMonitor` desenha a textura no canto inferior direito com `GUI.DrawTexture` (30% da largura da tela, entre 260 e 480 px), põe por cima os nomes das salas (`?` se não descoberta, a sala do card entre colchetes) e dos atores (`▶` no selecionado) usando `WorldToGui`, e registra o retângulo no `HudInputBlocker`. O card de sala é empurrado para a esquerda do monitor. Fechado, vira um botão "Monitor do diretor (M)".

**Analogia GMS:** é `surface_create` + `view_surface_id[1] = surf` + `draw_surface_stretched` no Draw GUI. A `RenderTexture` é a surface, `targetTexture` é o `view_surface_id`, `GUI.DrawTexture` é o `draw_surface_stretched`, e `Release` é o `surface_free`.

---

## 12. Clicar no monitor: de pixel para raio

`DirectorMonitor.TryGetRay`:

1. O Input System dá o mouse com **Y para cima**; o `OnGUI` usa **Y para baixo**: `Screen.height - y`.
2. Se o ponto está dentro de `GuiRect` (onde a HUD desenhou a imagem neste frame), normaliza para 0..1 dentro da imagem, invertendo Y de volta = **viewport**.
3. `monitorCamera.ViewportPointToRay(viewport)`: o raio que sai da câmera do monitor por aquele ponto (ortográfica → reto para baixo).

No `RunPresenter`, clique **e** hover passam por `TryPointerRay`:

```csharp
if (monitor != null && monitor.TryGetRay(p, out ray)) return true;      // 1º: monitor
if (mainCamera == null || HudInputBlocker.IsPointerOverHud(p)) return false;
ray = mainCamera.ScreenPointToRay(p);                                   // 2º: tela principal
```

Depois disso, o `Physics.Raycast` é o de sempre: acertou ator, seleciona; acertou `RoomAnchor`, abre o card (e o diretor vai para a porta). O resto do jogo não sabe de qual câmera veio o raio.

**Analogia GMS:** a conta de um minimapa clicável: `room_x = view_x + (mouse_x - surf_x) / surf_w * view_w`. Aqui você faz só a parte "onde está a imagem na tela"; `ViewportPointToRay` faz a parte da câmera.

---

## 13. Letterbox e nomes no mundo

Duas propriedades do diretor que a `RunHud` consulta:

```csharp
public bool IsFilmMode => mode == Mode.Cinematic || mode == Mode.Track || (focusController != null && focusController.CurrentActor != null);
public bool ShowWorldLabels => mode == Mode.Overview && (focusController == null || focusController.CurrentActor == null);
```

- **Letterbox:** no `Update`, `letterbox` vai de 0 a 1 com `MoveTowards` (~0,4 s); `DrawLetterbox` pinta duas faixas de `letterboxHeight` × altura da tela, com `SmoothStep`. Sem monitor, as faixas só aparecem no close de reação.
- **Nomes sobre a cena** só no `Overview`. No filme eles quebrariam a ilusão; o monitor sempre mostra os nomes.

---

## 14. Vida de set (`ActorIdle`)

Só visual, não mexe em regra. A cada `thinkInterval` (3–7 s) cada ator "pensa":

| Situação | O que faz |
|---|---|
| colega a menos de `talkDistance` (2,6 m), 70% | vira para ele e "conversa": o filho `Corpo` sobe e desce (`talkBob`) e balança |
| sozinho, 50% | anda até um ponto sorteado dentro da "casa" (centro + raio), validado com `NavMesh.SamplePosition` |
| sozinho, 50% | olha para uma direção sorteada (`Slerp`) |

A conversa acaba se o colega morrer, for pausado ou se afastar.

**Registro estático:** `private static readonly List<ActorIdle> all`, alimentada no `OnEnable`/`OnDisable`. `NearestMate()` percorre a lista e pega o mais perto, pulando a si mesmo, mortos e pausados.

**Analogia GMS:** `instance_nearest(x, y, obj_actor)` com filtro. A Unity não tem "todas as instâncias deste objeto" barato (`FindObjectsByType` é lento para todo frame), então a própria classe mantém a lista. O `timer` que desce com `Time.deltaTime` é o seu **alarme**.

**Quem manda no idle é o `RunPresenter`:**

| Momento | Chamada |
|---|---|
| início da run, depois do NavMesh | `SetHome(posição, 0.6)` e `Paused = false` |
| ator vai para outra sala | `SetHome(centro do cômodo, 1.0)` (corredor: 0,5) |
| começa a cena (`BeginBusy`) | `SetIdlePaused(Selected, true)`: quem vai agir para de fazer hora (o `NavMeshAgent` é do `ActorMover` agora) |
| fim da cena (`AfterSequence`) | todos os vivos voltam; mortos ficam pausados |

`Paused = true` chama `StopIdle()`: solta a conversa, cancela a caminhada e endireita o corpo na hora.

---

## 15. Peças de apoio

- **`RoomAnchor.DoorPoint` / `DoorOutward`:** centro da porta principal no mundo e a direção que sai do cômodo por ela. O construtor preenche com `ConfigureDoor`: corredor → porta da frente (`Vector3.back`); cômodos → parede do lado do corredor. Usados no `DoorShot` e no `GlideTo`.
- **`RoomAnchor.ClampInside(ponto, margem)`:** prende um ponto dentro do retângulo do cômodo (clamp em X e Z, mantém a altura). Está pronto, mas **nenhum script chama ainda**.
- **`GreyboxSceneBuilder.BuildCameras`:** cria `CM_Cine` e `CM_Porta` com prioridade 0, a `MonitorCam` desligada e `DirectorMonitor` + `CinematicDirector` no `GameSystems`, ligando referências por `SerializedObject` (técnica do doc 05). O `presenter` do diretor e o `director`/`monitor` da HUD são ligados depois, quando esses objetos existem. Cada ator ganha um `ActorIdle`.

---

## 16. Como mexer

| Quero mudar… | Onde |
|---|---|
| tempo parado até o filme voltar | `GameSystems` → `CinematicDirector` → `returnToCinematicAfter` (7 s) |
| duração dos planos | `CinematicDirector` → `shotDuration` (6–9 s) |
| frequência do plano da casa inteira | `CinematicDirector` → `establishingChance` (0,15) |
| frequência do zoom assustador | `CinematicDirector` → `creepZoomChance` (0,25) |
| quanto a visão da casa afasta ao mexer | `CinematicDirector` → `overviewDistance` (22; o rig limita entre 5 e 24). Zoom da porta sem monitor: `doorGlideDistance` (12) |
| distância, altura e quadro do observador | `CinematicDirector` → `watchDistance` (11–20), `watchHeight` (2,2–4,5), `mediumFrame` (1,8) |
| câmera na mão | `CinematicDirector` → `shakePosition` (0,04 m), `shakeDegrees` (0,35°). Zere os dois para tripé |
| conversa/caminhada dos atores | cada ator → `ActorIdle` → `talkDistance` (2,6), `thinkInterval` (3–7 s), `turnSpeed` (4), `talkBob` (0,025) |
| faixas pretas | `GameSystems` → `RunHud` → `letterboxHeight` (0,08 = 8% em cima e embaixo) |
| giro e respiração da visão da casa | `CameraRig` → `IsoCameraRig` → `driftDegreesPerSecond`, `driftDelay`, `breatheAmount` (0 desliga), `breathePeriod` |
| resolução do monitor / começar aberto | `DirectorMonitor` → `resolution`, `startOpen` (aberto = o jogo já começa em filme) |
| abrir/fechar o monitor | tecla **M**, botão "Monitor do diretor (M)" ou o **X** no painel |

Campos do Inspector valem ao vivo no Play (e se perdem ao sair dele). Valores do construtor (FOV inicial, posição da `MonitorCam`) só depois de reconstruir a cena.

---

## 17. Limitações conhecidas

- **A câmera pode atravessar móveis.** O `ClearLine` só enxerga colliders, e os móveis não têm collider (doc 05). Um esconderijo pode passar no teste com um armário no meio, ou a lente nascer dentro de um móvel.
- **O modo `Overview` só foi verificado lendo o código.** No Play foram testados: plano de estabelecimento, atores filmados de fora, plano da porta com card, cliques no monitor caindo na sala certa, acompanhar o ator andando e close de reação com letterbox. A deriva, a respiração do zoom e o `ZoomOutAtLeast` ainda pedem um teste à mão.
- **O enquadramento da porta é básico:** ponto fixo meio metro para dentro da porta e quadro fixo; não considera móveis nem atores na frente.

---

## 18. Experimente

1. Com o monitor aberto e sem mexer, conte quantos planos passam até aparecer a casa inteira. Suba `establishingChance` para 0,8 e compare o ritmo.
2. Mude `watchDistance` para (3, 5) e depois para (25, 30). O ator fica do mesmo tamanho; compare o fundo (tabela da seção 5).
3. Zere `shakePosition` e `shakeDegrees` no Play. Quanto do clima vai junto?
4. No `DoorShot`, troque a margem `-0.3f` por `0.6f`. O que acontece com a parede da porta?
5. Mude `activePriority` para 5 (abaixo da Iso) e entenda por que o filme some mesmo com o monitor aberto.
6. Ponha dois atores no mesmo cômodo e espere: eles se viram um para o outro? Mude `talkDistance` para 0,5.
