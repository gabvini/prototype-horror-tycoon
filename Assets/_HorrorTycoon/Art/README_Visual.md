# Horror Tycoon — Identidade visual na Unity (v1)

Implementação do guia `Art/Direcao_de_Arte_v1.md` (proposta v1) no protótipo Unity 6.3 / URP 17.3 (Render Graph, Forward+).
Tudo é montado por código: rode **Horror Tycoon > Construir Cena Greybox** (ou o `GreyboxSceneBuilder.Build`) e o visual inteiro é reaplicado.

Comparação: `Art/Previews/Unity/01_antes_*` × `03..10_depois_*` (cópias de `Captures/` do projeto).

---

## 1. O que foi construído

| Passo | Resultado |
|---|---|
| **Shader toon `HorrorTycoon/Toon`** | HLSL à mão. Rampa de 3 degraus sobre half-Lambert (luz / meio-tom lilás `#B8AEDB` / sombra violeta `#5B4E8C`, nunca preta), sombra projetada da lua com a mesma tinta, **luzes pontuais em degraus** (poça de luz 100% / 45% / 0 com borda de desenho), loop de luzes Forward+ (`_CLUSTER_LIGHT_LOOP`) e Forward, camadas de luz, SSAO, `MixFog`. Passes: `UniversalForward`, `Outline` (`SRPDefaultUnlit`, casca com largura constante em px), `ShadowCaster`, `DepthOnly`, `DepthNormals`. Um CBUFFER `UnityPerMaterial` (SRP Batcher). Expõe `_BaseColor` (o `MaterialPropertyBlock` do piso continua funcionando). |
| **Variante de personagem** | Mesmo shader, preset `ToonMaterials.ApplyCharacter`: rim de lua `#8CA6FF` (força 0,4, potência 3,5, só no lado da lua), borda 0,02, casca 2,5 px na cor base × 0,25. **Ator selecionado**: casca 3,5 px `#F5C542` via property block (sem material extra). As cápsulas já usam; os modelos do Blender só precisam de `ToonMaterials.ApplyCharacter(mat, cor, 2.5f, 0.6f, 0f)` (origem nos pés). |
| **Ambiente toon** | Paredes (malva `#5E5068` + rodapé `#4A3328` + faixa `#7A6A80` + topo escuro no corte), pisos, móveis, chão, árvores/arbustos/cerca do cenário (materiais `M_Cen_*` trocados de Lit para Toon **mantendo a cor** ajustada à mão). |
| **Luz, névoa, céu, pós (§6)** | Lua `#8CA6FF` 0,5; ambiente Trilight (`#2A3352`/`#2A2340`/`#120E16`); névoa exp² `#2B3550` 0,015; céu procedural `HorrorTycoon/Sky` (gradiente, lua de desenho com crateras, estrelas piscando). Lâmpadas `#FFB35C` 2,2–2,6, alcance 5,5 m, sem sombra. Pós `PP_FilmeDeTerror` **reaplicado a cada build**: Neutral, saturação −10, contraste +15, split toning, vinheta `#0B0A12`, grão Thin1, bloom 1,0/0,5/0,6. |
| **Contorno de ambiente** | `HT_EdgeOutlineFeature` (Render Graph puro, `RecordRenderGraph`): Roberts Cross em profundidade **linear e relativa à distância** + normais, 1 px `#1A1420` a 60 %, apaga com a névoa e a distância, roda antes dos transparentes. Instalado no `PC_Renderer` automaticamente. |
| **Atmosfera** | `HorrorTycoon/FX` (unlit transparente): névoa rasteira (3 planos 0,14–0,6 m, noise rolando ~0,03 m/s, soft particles, "buraco" na casa); raios de luz falsos (cones aditivos sob cada lâmpada e na varanda, feixes saindo das janelas); cômodo não descoberto = piso `#0E0F17` + fita crepe a 25 % + névoa animada em volume; **descoberta**: dissolve de 0,6 s + lâmpada "pegando no tranco". Flicker do guia (Perlin 0,85–1,0 a 10 Hz + apagões de 0,05–0,25 s a cada 3–9 s) em lâmpada, bulbo, cúpula e raios. |
| **Camadas de luz por cômodo** | Cada slot tem sua rendering layer (1..7, nomes "Slot N"): a lâmpada só ilumina o próprio piso/móveis (sem vazar pela parede para o vizinho ainda escuro). Paredes externas também recebem varanda/refletores; internas, só as lâmpadas. |
| **Set dressing** | Móveis com **caixas chanfradas** (`BevelMesh`), tortos 1–3°, afunilados, escala exagerada (geladeira 1,2×, banheira 1,15×, relógio de pé), livros, TV com tela ciano que brilha, abajures, boneca de olhos vermelhos, X de fita crepe em todo cômodo, **cadeira de diretor** com megafone no corredor. Casa: batentes escuros em todas as portas, janelas com moldura/travessas que **acendem na cor da lâmpada** quando o cômodo é descoberto, beiral grosso (dica de telhado), oitão com janelinha redonda, chaminé torta, varanda com deque, colunas, toldo, luminária e porta vinho aberta. Set: 2 refletores de cena (spots reais, só camada Default) e câmera de cinema com rolos e luz REC. |

## 2. Arquivos

Fonte local (espelho) → caminho no projeto (`C:\Users\T-GAMER\Prototype Horror Tycoon\`):

| Local (`outputs/ht/…`) | Projeto (`Assets/_HorrorTycoon/…`) | O quê |
|---|---|---|
| `Art/Unity/Shaders/HT_Toon.shader` | `Art/Shaders/HT_Toon.shader` | toon (ambiente + personagem) |
| `Art/Unity/Shaders/HT_FX.shader` | `Art/Shaders/HT_FX.shader` | névoas, raios, fita |
| `Art/Unity/Shaders/HT_Sky.shader` | `Art/Shaders/HT_Sky.shader` | céu |
| `Art/Unity/Shaders/HT_EdgeOutline.shader` | `Art/Shaders/HT_EdgeOutline.shader` | contorno de tela cheia |
| `Art/Unity/Rendering/HT_EdgeOutlineFeature.cs` + `HorrorTycoon.Rendering.asmdef` | `Art/Rendering/…` | renderer feature (asmdef próprio com refs URP) |
| `Scripts/Art/ToonMaterials.cs` | `Scripts/Art/` | presets de material (runtime) |
| `Scripts/Art/BevelMesh.cs` | `Scripts/Art/` | caixa chanfrada/afunilada (cache) |
| `Scripts/Editor/Art/HTVisualSetup.cs` | `Scripts/Editor/Art/` | URP, materiais, luz, névoa, céu, pós |
| `Scripts/Editor/GreyboxSceneBuilder.cs` | (alterado) | casa, fachada, varanda, atmosfera, set, atores |
| `Scripts/Editor/SceneryBuilder.cs` | (alterado) | materiais do cenário viram toon |
| `Scripts/Editor/HorrorTycoon.Editor.asmdef` | (alterado) | + `HorrorTycoon.Rendering` |
| `Scripts/Rooms/FurnitureKit.cs` | (reescrito) | móveis chanfrados/tortos + set |
| `Scripts/Rooms/RoomAnchor.cs` | (alterado) | névoa/dissolve, flicker, janelas, camadas |
| `Scripts/Rooms/WallCutaway.cs` | (alterado) | enfeites que somem com o corte |
| `Scripts/Actors/ActorView.cs` | (alterado) | pupilas por expressão, contorno de selecionado |

Assets gerados: `Materials/M_*` (toon/FX), `Materials/M_Ceu`, `Art/Meshes/HT_*` (cone, oitão, chanfros), `Settings/PP_FilmeDeTerror.asset`, sub-asset `HT Edge Outline` em `Assets/Settings/PC_Renderer.asset`.
Não mexi em regras (FilmRun, RunModels, Content), RunPresenter, RunHud nem CinematicDirector.

## 3. Como ajustar

- **Cores/limiares do toon**: Inspector do material (`Materials/M_Parede`, `M_Piso`, `M_Ator_N`…). Valores-chave: `Meio-tom`, `Sombra`, `Limiar meio-tom/sombra`, `Força do ambiente` (1,5), `Ganho das luzes pontuais` (0,8), `Limiar 100%/45%` (tamanho da poça). Os presets estão em `ToonMaterials` (constantes `AmbientStrength`, `PointGain`). **Atenção:** reconstruir a cena reaplica os presets nos materiais da casa; as cores do cenário (`Art/Cenario/Materiais/M_Cen_*`) são preservadas.
- **Contorno de ambiente**: selecione `Assets/Settings/PC_Renderer.asset` → `HT Edge Outline` (cor, espessura, limiar de profundidade relativo, normal 0,5, fade 22–60 m, força da névoa). Desligar = checkbox do feature.
- **Pós**: edite `HTVisualSetup.ApplyPostProcessing` (é reaplicado a cada build) ou desligue essa chamada no builder para editar o perfil à mão.
- **Névoa / céu / lua**: `HTVisualSetup` (`FogDensity`, `MoonIntensity`, cores) e o material `M_Ceu`.
- **Névoa rasteira**: `M_FX_NevoaRasteira_1..3` (alpha, escala/velocidade do noise, `_HoleRect` = pegada da casa).
- **Cômodo não descoberto / descoberta**: `M_FX_NevoaSala`, `M_FX_NevoaCamada`, `M_FX_FitaApagada`; no `RoomAnchor`: `Undiscovered Floor`, `Reveal Duration` (0,6 s), `Window Glow`.
- **Contorno do ator selecionado**: `ToonMaterials.SelectedOutline` / `SelectedOutlineWidth`.
- Menu extra: **Horror Tycoon > Visual > Configurar render (URP)** (reinstala o feature e nomeia as camadas).

## 4. Desvios do guia (e por quê)

1. **Exposição +0,45 e split toning mais suave** (`#5A5878` / `#F0C08C` / −10 em vez de `#3B3A78` / `#FFB36B` / −20). No URP o split toning é proporcional à saturação da cor; os valores do guia deixavam o cenário com saturação 60–90 % e valor < 15 % (medido nos screenshots). Mesma intenção de matiz.
2. **Força do ambiente 1,5** no toon: com lua 0,5 o cenário ficava abaixo dos 20 % de valor da regra §3.5.
3. **Névoa do cômodo um pouco mais clara** que `#1A1D2B` (≈ `#232740`): sobre o piso `#0E0F17` a cor do guia some.
4. **Lâmpadas com camada de luz própria** (não estava no guia): sem sombra nas lâmpadas, a luz atravessava a parede e acendia o cômodo vizinho ainda "não descoberto".
5. **Cores dos atores** continuam as do `ActorDef` (dados de conteúdo); a paleta §4.3 fica para os modelos do Blender.
6. **Sem cor-tema de parede por cômodo**: as paredes são compartilhadas entre dois cômodos; a identidade do cômodo vem do piso (RoomDef), da cor da lâmpada e dos móveis.
7. **URPFog (névoa de altura)** não instalado: a névoa rasteira por planos + névoa built-in já recortam a silhueta; fica como etapa 2 opcional.
8. "Telhado" é só **dica** (beiral, oitão, chaminé), para não tapar a câmera iso.

## 5. Problemas conhecidos / próximos passos

- Pré-visualizações *inline* do MCP de screenshot clareiam a imagem (lavado); os PNG salvos em `Captures/` estão corretos.
- Personagens com quinas duras abrem falhas na casca: exportar do Blender com normais suaves (§8.2).
- Flicker de "perseguição" (2× e puxando para `#FF4A3D`) não está ligado: falta um gancho de jogo (ex.: `RoomAnchor.SetChase(bool)`).
- Enfeites presos a mais de um trecho de parede (porta da frente) seguem o último trecho que mudou de corte.
- Lab_Cenario e o Mobile_Renderer não receberam o contorno de ambiente (só o PC_Renderer ativo).
- Desempenho: 3 planos transparentes de tela cheia (névoa rasteira) + 1 passe de tela cheia (contorno); se pesar, deixar 2 planos.

---

## 6. Personagens do Blender na cena (integração)

Os 4 atores agora são os modelos de `Art/Characters/` (ver `Art/README_Personagens.md`). Se o FBX de um ator faltar, ele volta a ser a cápsula greybox (nada quebra).
Prévias: `Art/Previews/Unity/11_…` a `17_…` (fila na varanda, expressões, susto, conversa + caminhada, close de reação, iso, morte).

### Arquivos

| Local (`outputs/ht/…`) | Projeto (`Assets/_HorrorTycoon/…`) | O quê |
|---|---|---|
| `Art/Characters/HT_*.fbx`, `HT_Palette.png` | `Art/Characters/` | modelos, clipes e paleta (do agente de Blender) |
| `Scripts/Editor/Art/HTCharacterSetup.cs` | `Scripts/Editor/Art/` | importadores, controller, materiais (menu **Horror Tycoon > Visual > Configurar personagens**) |
| `Scripts/Actors/ActorView.cs` | (reescrito) | rosto por multiplicadores, 4 bocas, susto/morte no Animator, contorno de selecionado em vários renderers |
| `Scripts/Actors/ActorAnimDriver.cs` | (novo) | `Speed`/`MoveRate` a partir do NavMeshAgent |
| `Scripts/Actors/ActorIdle.cs` | (alterado) | conversa = bool `Talking` (cápsula: continua balançando) |
| `Scripts/Camera/ActorFocusController.cs` | (só o valor padrão) | `frameHeight` 0,8 → 1,1 (cabeças grandes) |
| `Scripts/Editor/GreyboxSceneBuilder.cs` | (alterado) | `BuildActors` instancia o modelo; `BuildCapsuleRig` é o fallback |
| `Scripts/Art/ToonMaterials.cs`, `Scripts/Editor/Art/HTVisualSetup.cs` | (alterados) | ganho de lâmpada menor no personagem (0,45); largura de casca por material |

Gerados: `Art/Characters/HT_Actor.controller`, `Materials/M_Personagem_<Ator>`, `M_Personagem_Rosto`, `M_Personagem_Olho`.

### Importação (feita por código, `HTCharacterSetup.Setup()`, chamada no build da cena)

- Escala 1, Convert Units, Normals Import, sem compressão, **Materials None**, Humanoid "Create From This Model" (5 avatares válidos, 21 ossos cada).
- **Bake Axis Conversion DESLIGADO** (desvio do README dos personagens): ligado, o ator olhava para −Z na Unity 6.3. Desligado: olha +Z, esquerda em −X, raiz sem rotação.
- O mapeamento Humanoid é **zerado** quando a importação muda (senão a Unity guarda a pose velha do "Rosto" e o rosto ia parar na nuca) e depois corrigido: tira `LeftEye/RightEye/Jaw` (o auto-mapeamento usava sobrancelha/pupila/boca como "olho" e "mandíbula") e garante `Chest` e `Toes`.
- `HT_Anims`: clipes Idle/Walk/Talk/Scared/Scared_Loop/Death, loop nos 4 ciclos, raiz "Bake Into Pose" (rotação, Y pela original, XZ). Animator com **Apply Root Motion desligado**.
- Paleta: Point, sem mipmap, sem compressão, Clamp, sRGB. (A ferramenta de envio recomprime o PNG: 375 → 6 145 bytes, pixels idênticos, conferido por hash.)

### Animator (`HT_Actor.controller`, um só para os 4 por retarget Humanoid)

| Parâmetro | Tipo | Quem mexe |
|---|---|---|
| `Speed` | float (m/s) | `ActorAnimDriver` (velocidade do NavMeshAgent) |
| `MoveRate` | float | `ActorAnimDriver` (ritmo da passada do Walk; o agente anda 2,6 m/s, o clipe ~1,4 m/s) |
| `Talking` | bool | `ActorIdle` (conversa com o colega) |
| `Afraid` | bool | `ActorView` (expressão = Assustada) |
| `Scared` | trigger | `ActorView` (só ao ENTRAR em Assustada) |
| `Dead` | trigger | `ActorView.SetDead()` |

Estados: Idle ⇄ Walk (Speed > 0,15 / < 0,1), Idle ⇄ Talk, Any → Scared → Scared_Loop (enquanto Afraid) → Idle, Any → Death (sem saída).

### Rosto

`ActorView` guarda a pose base de cada peça no build (serializada) e aplica os **multiplicadores da tabela do README dos personagens** (olho largura/altura, pupila, desvio da pupila, Δaltura e giro da sobrancelha, boca ligada, corpo esticado 0,9/1,15/0,9 no susto com suavização). Os eixos do rosto são descobertos sozinhos (não depende da convenção de eixos do FBX). Morte: clipe Death + olhos fechados, sem pupila, boca neutra.

### Como trocar / acrescentar um ator

1. Exporte `HT_<NomeSemEspaço>.fbx` com o mesmo contrato (ossos Humanoid, `Rosto` com as peças) para `Assets/_HorrorTycoon/Art/Characters/`. O nome vem do `ActorDef.DisplayName` sem espaços ("Final Girl" → `HT_FinalGirl`).
2. Acrescente o nome em `HTCharacterSetup.ActorModels` (para o importador ser configurado).
3. Rode **Horror Tycoon > Construir Cena Greybox**. O material `M_Personagem_<Nome>` é criado com a paleta.
4. Para ajustar contorno/rim/brilho: Inspector dos materiais `M_Personagem_*`; o brilho dos olhos está em `M_Personagem_Olho` (emissão).

### Problemas conhecidos (personagens)

- O ator anda mais rápido que o clipe Walk: a passada acelera (`MoveRate` até 2×). Ficou cartunesco; se incomodar, baixar `agent.speed` no builder (mexe só no ritmo visual da caminhada).
- A casca de contorno desenha linha onde as cascas do corpo se cruzam (ombro, punho, testa/cabelo), como avisado no README dos personagens. Não houve falha (buraco) visível com as normais importadas, então não usei o truque de normais suavizadas em UV.
- Expressão "Assustada" de pavor alto mantém o ator encolhido (Scared_Loop) até a expressão mudar. Se andar, ele sai para o Walk.
- O clique usa um CapsuleCollider na raiz (r 0,3 × 1,75 m). Como antes com a cápsula, ele entra no NavMesh se o NavMesh for assado com os atores parados no lugar.
