# 08 — Identidade visual toon e personagens do Blender

> Pré-requisito: docs 01–06 (e o `07_Cenario_por_Codigo`, que monta árvores e arbustos). Aqui o greybox ganha **cara de desenho**: um shader toon escrito à mão, contorno de tinta nos atores e no cenário, névoa clara, céu de desenho, pós-processamento de "filme", e os 4 atores viram **modelos 3D gerados por script no Blender**, com esqueleto, rosto expressivo e animações.
> Para ver tudo: menu **Horror Tycoon > Construir Cena Greybox**, Play. Compare com `Art/Previews/Unity/01_antes_*` × `03..17_*`.
> Fonte de design: `Art/Direcao_de_Arte_v1.md` (**proposta v1, 🧪 hipótese**). O que foi feito e onde ele foi desviado está em `Art/Unity/README_Visual.md` e `Art/README_Personagens.md`.

---

## 1. O que mudou

- **Shader `HorrorTycoon/Toon`** em tudo que é opaco: luz em 3 degraus (luz / meio-tom lilás / sombra violeta, **nunca preta**), lâmpadas que desenham uma "poça" com borda de cartoon, rim de lua nos personagens.
- **Dois contornos:** casca invertida nos atores (2,5 px, cor da roupa × 0,25; **amarelo fita crepe** no selecionado) e detecção de bordas em tela cheia no cenário (1 px, some na névoa).
- **Atmosfera:** névoa exp² clara `#2B3550`, névoa rasteira em planos, raios de luz falsos, céu com lua de desenho e estrelas, cômodo não descoberto com névoa animada que **se dissolve** ao descobrir, lâmpada "pegando no tranco".
- **Pós `PP_FilmeDeTerror`:** Neutral, saturação −10, contraste +15, split toning, vinheta, grão, bloom.
- **Set dressing:** móveis chanfrados e tortos (`BevelMesh`), fachada, varanda, refletores, cadeira de diretor.
- **Atores do Blender:** 4 FBX Humanoid, um `Animator` com 6 clipes, rosto feito de peças (olhos, pupilas, sobrancelhas, 4 bocas). Sem o FBX, o ator volta a ser a cápsula greybox.

Arquivos envolvidos: `Art/Shaders/HT_Toon.shader`, `HT_FX.shader`, `HT_Sky.shader`, `HT_EdgeOutline.shader`; `Art/Rendering/HT_EdgeOutlineFeature.cs`; `Scripts/Art/ToonMaterials.cs`, `BevelMesh.cs`; `Scripts/Editor/Art/HTVisualSetup.cs`, `HTCharacterSetup.cs`; `Actors/ActorView.cs`, `ActorAnimDriver.cs`, `ActorIdle.cs`; `Rooms/RoomAnchor.cs`, `FurnitureKit.cs`, `WallCutaway.cs`; `Editor/GreyboxSceneBuilder.cs`. No Blender: `Art/Blender/*.py`.

---

## 2. Shader, passe e "quem chama quem"

No GMS um shader é **um** par vertex + fragment. Você faz `shader_set(shd)`, desenha, `shader_reset()`. Se quer um contorno, desenha o objeto **duas vezes** no Draw: uma com o shader de contorno, outra com o normal. **Você** decide a ordem.

Na Unity (URP), um arquivo `.shader` tem **vários passes**, cada um com uma etiqueta `LightMode`. Você nunca chama um passe: o pipeline percorre suas etapas do frame (mapa de sombra, pré-passe de profundidade, opacos…) e, em cada etapa, desenha **os passes com a etiqueta daquela etapa**.

| Passe do `HT_Toon` | `LightMode` | Quando o URP desenha | Para quê |
|---|---|---|---|
| `ForwardToon` | `UniversalForward` | etapa dos opacos | a cor: rampa, luzes, rim, névoa |
| `Outline` | `SRPDefaultUnlit` | **junto** com os opacos | a casca de contorno (seção 5) |
| `ShadowCaster` | `ShadowCaster` | antes, no mapa de sombra da lua | o objeto projeta sombra |
| `DepthOnly` | `DepthOnly` | pré-passe de profundidade | preenche a textura de profundidade |
| `DepthNormals` | `DepthNormals` | pré-passe de normais | **obrigatório** para o SSAO e o contorno de bordas "enxergarem" o objeto |

Esquecer o `DepthNormals` não dá erro: o objeto simplesmente some da textura de normais, e o contorno de ambiente passa reto por ele.

**Analogia GMS:** é como se cada objeto tivesse um evento Draw separado para cada "camada" do frame (Draw Shadow, Draw Depth, Draw Color…), e o motor rodasse cada evento na hora certa para todas as instâncias. Você escreve os eventos; a ordem é do motor.

### Por que HLSL à mão e não Shader Graph

Do guia §8.1: o Shader Graph não faz o passe extra da casca no mesmo material; texto é fácil de revisar e validar; e a rampa e a atenuação em degraus precisam de controle fino. O preço: a ShaderLibrary do URP muda entre versões (keywords de Forward+, sombras), então este arquivo é o primeiro a quebrar num upgrade.

---

## 3. A rampa toon

Luz "realista" (Lambert) vai de 0 a 1 suavemente conforme a face vira para a luz. O toon **quantiza** esse valor em degraus, e cada degrau é uma cor chapada.

```hlsl
half halfLambert = NdotL * 0.5h + 0.5h;                 // -1..1 vira 0..1 (sombra mais "aberta")
half t = min(halfLambert, lerp(0.0h, 1.0h, shadow));    // sombra projetada força o degrau escuro
half toMid = Step2(_ShadowThreshold, t, soft);          // 0,30
half toLit = Step2(_MidThreshold, t, soft);             // 0,55
half3 ramp = lerp(_ShadowTint.rgb, lerp(_ShadeTint.rgb, half3(1,1,1), toLit), toMid);
```

| `t` | Degrau | Multiplica a cor base por |
|---|---|---|
| > 0,55 | luz | 1 |
| 0,30–0,55 | meio-tom | `#B8AEDB` (lilás, ~75% de valor) |
| < 0,30 ou na sombra da lua | sombra | `#5B4E8C` (violeta) |

`Step2` é um `smoothstep` estreito: `_EdgeSoftness` 0,04 no ambiente, 0,02 nos personagens (borda mais dura). Depois da rampa, o fragment soma o **ambiente** (`SampleSH`, o gradiente céu/equador/chão × `_AmbientStrength` 1,5 × SSAO), as lâmpadas (seção 4), especular duro (desligado por padrão), rim de lua (só personagens: `pow(1 - N·V, 3,5)`, cortado em degrau e só no lado iluminado pela lua) e emissão. Por fim `MixFog`.

Extras para paredes, todos no mesmo shader e desligados por padrão: gradiente vertical (base 15% mais escura, "sujeira"), rodapé (`_BandHeight`), faixa (`_StripeY`) e cor de topo (`_TopBlend`, o "corte" da parede parece tinta escura).

**Analogia GMS:** é o shader de *palette swap* que você já fez em 2D: pega a luminosidade e troca por uma de 3 cores da paleta. Só que a "luminosidade" vem da conta de luz 3D, não do sprite.

---

## 4. Poças de luz e camadas por cômodo

### Lâmpadas em degraus

Cada luz pontual também é quantizada, só que pela **distância**:

```hlsl
half f = 1.0 - sqrt(saturate(d2 * atten.x));   // 1 no centro, 0 no alcance
half level = Step2(_PointHigh, f, e) * (1 - _PointLowLevel) + Step2(_PointLow, f, e) * _PointLowLevel;
```

Resultado: 100% perto da lâmpada (`f > 0,5`), 45% num anel (`f > 0,16`), 0 fora. No chão aparece uma poça com borda de desenho. Faces de costas para a lâmpada recebem só 15% (`_PointBackFace`). O ganho é 0,8 no ambiente e **0,45 nos personagens** (a cabeça passa a ~1 m do bulbo e o rosto "estourava").

O loop de luzes usa as macros do URP (`LIGHT_LOOP_BEGIN/END`) e funciona em **Forward+** (`_CLUSTER_LIGHT_LOOP`) e em Forward.

### Rendering layers: a luz não atravessa a parede

As lâmpadas não têm sombra (custo). Sem sombra, a luz do cômodo atravessa a parede e acende o vizinho ainda "não descoberto". A solução (desvio 4 do README, não estava no guia): cada slot tem uma **rendering layer**.

| Quem | Máscara |
|---|---|
| lâmpada do slot N, piso e móveis do slot | `HTVisualSetup.SlotLayer(N)` = `1 << (N + 1)` (bit 0 é "Default") |
| paredes externas, batentes, janelas | `AllLayers` (0xFF): lâmpadas, varanda e refletores |
| paredes internas | `AllLayers & ~1`: só lâmpadas (os refletores de cena são "Default") |
| atores | `AllLayers`: recebem tudo |

No shader: `if (!IsMatchingLightLayer(layerMask, meshLayers)) return 0;`. O `EnsureRenderer` liga *Light Layers* no URP e nomeia as camadas ("Slot 0 Corredor", "Slot 1"…).

**Analogia GMS:** é como dar a cada luz uma lista de `object_index` que ela pode iluminar, só que por bits.

---

## 5. Contorno 1: casca invertida (atores)

No GMS 2D, contorno é desenhar o sprite 4 vezes deslocado em preto e depois o sprite por cima. Em 3D o truque equivalente é a **casca invertida** (*inverted hull*): desenhar o modelo de novo, **inchado** ao longo das normais, só com as faces **de trás** (`Cull Front`). A casca fica atrás do modelo e só aparece como uma borda em volta da silhueta.

O passe `Outline` incha **em clip space**, para a largura ser constante na tela (não engrossa de perto nem some de longe):

```hlsl
float2 dir = normalize(normalCS.xy);                     // direção da normal na tela
float px = _OutlineWidth * (_ScreenParams.y / 1080.0);   // 2,5 px a 1080p, escala com a resolução
positionCS.xy += dir * (px * 2.0) / _ScreenParams.xy * positionCS.w;
```

O `× 2` é porque o clip space vai de −1 a 1 (2 unidades na largura da tela). O `× w` cancela a divisão de perspectiva que a GPU faz depois: o deslocamento final fica em pixels.

Cor: `lerp(_OutlineColor, albedo × 0,25, _OutlineFromBase)`. Com `_OutlineFromBase = 1` (padrão), a borda é a **cor da roupa escurecida** (contorno colorido, não preto). `_OutlineWidth = 0` joga o vértice para fora da tela (descarta).

| Material | Largura | Cor |
|---|---|---|
| `M_Personagem_<Ator>` (corpo) | 2,5 px | base × 0,25 |
| `M_Personagem_Rosto` (pupila, sobrancelha, boca) | 1,2 px | base × 0,25 |
| `M_Personagem_Olho` | 1,4 px | `#15121A` |
| ator selecionado (property block) | 3,5 px | `#F5C542` |
| ambiente | **passe desligado** | — |

No ambiente o passe é desligado **por material**: `m.SetShaderPassEnabled("SRPDefaultUnlit", false)`. Mesmo shader, um passe a menos.

**O ponto fraco:** onde duas cascas se cruzam (ombro, punho, virilha, testa/cabelo), cada uma desenha sua borda e aparece uma linha. Lê como costura de roupa, mas aparece. E em quinas duras a casca abre buracos; por isso os modelos usam normais suaves.

---

## 6. Contorno 2: detecção de bordas (cenário)

Para o cenário (centenas de caixas), casca seria caro e feio em quinas. O guia pede outra técnica: **pós em tela cheia**. Depois que os opacos estão desenhados, um shader olha a **profundidade** e as **normais** de cada pixel e dos vizinhos; onde muda de repente, é borda.

`HT_EdgeOutline.shader` faz Roberts Cross (4 amostras em diagonal):

```hlsl
float depthEdge = sqrt((d1-d0)*(d1-d0) + (d3-d2)*(d3-d2)) / max(dc, 0.01);  // RELATIVO à distância
float normalEdge = sqrt(dot(n1-n0, n1-n0) + dot(n3-n2, n3-n2));
float edge = max(smoothstep(...depthEdge), smoothstep(...normalEdge));
edge *= distFade * fogFade * _EdgeColor.a;   // some entre 22 e 60 m e na névoa
```

Por que **dividir pela distância**: a câmera de cinema (doc 06) é teleobjetiva a 11–20 m. Nela, a diferença de profundidade entre um armário e a parede atrás é pequena em relação à distância; um limiar absoluto não pegaria nada de longe e pegaria tudo de perto. O fade pela névoa usa a mesma conta exp² do built-in, então a linha some junto com o cenário.

Os atores também ganham essa linha fina (eles estão na profundidade), por baixo da casca deles.

**Analogia GMS:** é o shader de contorno que amostra os vizinhos procurando `alpha` diferente, só que aplicado à tela inteira e olhando profundidade/normal em vez de alpha. Igual a desenhar o `application_surface` com um shader no Post Draw.

---

## 7. Renderer feature e Render Graph

No GMS, para um pós em tela cheia você desliga o desenho automático do `application_surface`, cria uma surface e no Post Draw desenha uma na outra com o shader. Você gerencia as surfaces.

No URP, você **injeta uma etapa** no pipeline com um **renderer feature** (`ScriptableRendererFeature`), que fica guardado dentro do asset do renderer (`Assets/Settings/PC_Renderer.asset`, sub-asset "HT Edge Outline"). No Unity 6 essa etapa é escrita para o **Render Graph**: em vez de executar comandos, você **declara** o que lê e o que escreve, e o motor decide quando rodar, aloca e recicla as texturas.

`HT_EdgeOutlineFeature`:

| Peça | O que faz |
|---|---|
| `AddRenderPasses` | enfileira o passe e pede insumos: `ConfigureInput(Depth \| Normal)`. É isso que **liga o pré-passe `DepthNormals`** (seção 2) |
| `renderPassEvent` | `BeforeRenderingTransparents`: a névoa rasteira e os raios (transparentes) ficam **por cima** da linha |
| `RecordRenderGraph` | cria uma textura nova do tamanho da tela, declara `UseTexture(source, Read)`, `UseAllGlobalTextures` (profundidade e normais) e `SetRenderAttachment(destination)`; a função de render é um `Blitter.BlitTexture` com o material |
| `resourceData.cameraColor = destination` | "a cor da câmera agora é esta textura". Não copia de volta: só troca o ponteiro |
| `requiresIntermediateTexture = true` | garante que a câmera desenha numa textura intermediária, não direto no back buffer |

**Por que Render Graph é regra:** no Unity 6.3 o modo antigo (*Compatibility Mode*) está escondido atrás de um define e vai sair. Qualquer feature de terceiros que só funcione nele foi descartada (ex.: *StylizedGradient-Fog*).

**Analogia GMS:** `resourceData.cameraColor = destination` é o `application_surface` virar outra surface. O Render Graph é um gerente de surfaces: você diz "preciso ler esta e escrever naquela", e ele cria, libera e ordena.

Quem instala: `HTVisualSetup.EnsureRenderer()` (chamado no build e no menu **Horror Tycoon > Visual > Configurar render (URP)**). Ele percorre os renderers do **URP asset ativo**, adiciona o feature se faltar, aponta o shader e liga *Light Layers*.

---

## 8. Material × uniform, SRP Batcher e MaterialPropertyBlock

No GMS, cada uniform é ajustado na hora do desenho: `shader_set_uniform_f(u_cor, r, g, b)`. Na Unity:

| Conceito | O que é | No GMS |
|---|---|---|
| **Shader** | o código | o `shd_` |
| **Material** (`.mat`) | shader + **valores salvos** de todas as propriedades. Um asset, compartilhado por vários objetos | um "preset" de `shader_set_uniform_*` que fica gravado |
| `Properties { }` no shader | lista o que aparece no Inspector do material | `shader_get_uniform` |
| `MaterialPropertyBlock` | sobrescreve algumas propriedades **só para um renderer**, sem criar material novo | setar o uniform logo antes de desenhar **uma** instância |

### SRP Batcher

Todas as propriedades do `HT_Toon` estão num único `CBUFFER_START(UnityPerMaterial)`, **igual em todos os passes**. Isso deixa o URP guardar os valores de cada material na GPU e desenhar objetos com o mesmo shader sem reenviar tudo (SRP Batcher). Uma propriedade fora do CBUFFER, ou um CBUFFER diferente num passe, desliga o batching do shader inteiro. `HT_FX` e `HT_Sky` seguem a mesma regra.

### Onde o projeto usa property block

| Quem | Propriedade | Para quê |
|---|---|---|
| `ToonMaterials.SetSelectedOutline` | `_OutlineWidth`, `_OutlineColor`, `_OutlineFromBase` | contorno amarelo do selecionado, sem material extra |
| `RoomAnchor.ApplyBaseColor` | `_BaseColor` | cor do piso vem do `RoomDef` (um `M_Piso` branco para todos) |
| `RoomAnchor.SetFogDissolve` | `_Dissolve` | névoa do cômodo sumindo |
| `RoomAnchor.ApplyWindows` / `ApplyGlow` | `_EmissionColor`, `_BaseColor` | janela acende na cor da lâmpada; bulbo e raios acompanham o flicker |

Um renderer com property block **sai do SRP Batcher**. Por isso, ao desligar o selecionado, o código faz `r.SetPropertyBlock(null)` em vez de deixar um bloco "neutro":

```csharp
else r.SetPropertyBlock(null); // volta a valer o material (e o renderer volta para o SRP Batcher)
```

**Cuidado:** `renderer.material` (sem `shared`) **clona** o material por objeto. O projeto usa sempre `sharedMaterial` + property block.

---

## 9. Presets em código: `ToonMaterials` e `HTVisualSetup`

Os valores do guia moram em **dois lugares só**:

- **`ToonMaterials`** (runtime): `ApplyEnvironment` (borda 0,04, gradiente, sem rim, sem casca), `ApplyCharacter` (borda 0,02, rim de lua 0,4/3,5, casca, ganho de lâmpada 0,45), `ApplyEmissive` (olhos, bulbos, telas com emissão HDR > 1 para o bloom). Constantes: `AmbientStrength` 1,5, `PointGain` 0,8, `CharacterPointGain` 0,45, `SelectedOutline`, `SelectedOutlineWidth`. Se o shader não for achado, cai no URP/Lit.
- **`HTVisualSetup`** (editor): cria/atualiza materiais como **assets** (`LoadOrCreate` em `Assets/_HorrorTycoon/Materials`), `Wall` (malva + rodapé + faixa + topo), `Fx`, `UpgradeToToon` (troca os `M_Cen_*` do cenário de Lit para Toon **mantendo a cor** ajustada à mão), luz/névoa/céu (`ApplyEnvironmentLighting`) e pós (`ApplyPostProcessing`).

| Item | Valor aplicado | Guia |
|---|---|---|
| Lua | `#8CA6FF`, 0,5 | igual |
| Ambiente | Trilight `#2A3352` / `#2A2340` / `#120E16` | igual |
| Névoa | exp², `#2B3550`, 0,015 | 0,012–0,018 |
| Céu | `M_Ceu` (`HorrorTycoon/Sky`): horizonte = cor da névoa (emenda sem costura), lua na direção oposta à luz da lua | `#0D1024` → `#1E2A44` |
| Lâmpadas | `#FFB35C`, 2,2–2,6, alcance 5,5 m, sem sombra | igual |
| Pós | Neutral, sat −10, contraste +15, **exposição +0,45**, split toning **`#5A5878` / `#F0C08C` / −10**, vinheta 0,35 `#0B0A12`, grão Thin1 0,25, bloom 1,0/0,5/0,6 | split `#3B3A78` / `#FFB36B` / −20, sem exposição |

Os desvios (README_Visual §4) vieram de **medir screenshots**: no URP o split toning é proporcional à saturação da cor, e os valores do guia afogavam o cenário em azul com valor < 15%. A força do ambiente 1,5 também veio daí (regra §3.5: cenário entre 20 e 45% de valor).

**Importante:** o build **reaplica** os presets. Ajuste à mão nos materiais da casa, dos personagens ou no `PP_FilmeDeTerror` some na próxima reconstrução. O lugar certo é o código (ou desligar a chamada no builder). As cores `M_Cen_*` do cenário são preservadas.

---

## 10. Atmosfera: `HT_FX`, céu e o cômodo que "acende"

`HorrorTycoon/FX` é **um** shader unlit transparente para quatro efeitos baratos, cada um sendo só um material com valores diferentes:

| Efeito | Material(is) | Truques do shader |
|---|---|---|
| Névoa rasteira (3 planos, 0,14–0,6 m) | `M_FX_NevoaRasteira_1..3` | noise 3D rolando no mundo (~0,03 m/s), `_HoleRect` = "buraco" na pegada da casa |
| Raios de luz falsos | `M_FX_RaioLampada`, `_RaioJanela`, `_RaioVaranda` | cone **aditivo** (`HTVisualSetup.ConeMesh`), fade por fresnel e pela altura |
| Névoa do cômodo não descoberto | `M_FX_NevoaSala`, `M_FX_NevoaCamada` | noise + `_Dissolve` com borda de noise |
| Fita crepe apagada | `M_FX_FitaApagada` | alpha 25% |

Comum a todos: *soft particles* (some ao encostar na geometria, lendo a profundidade da cena; tem ramo para câmera ortográfica, a do monitor do doc 06), some perto da câmera, e névoa. Num efeito aditivo, a névoa **apaga** o brilho em vez de pintar com a cor dela. O modo de mistura vem de propriedades (`_SrcBlend`/`_DstBlend`), então "alpha" e "aditivo" são o mesmo shader.

**Analogia GMS:** `gpu_set_blendmode(bm_add)` vs `bm_normal`, só que guardado no material.

### Descoberta do cômodo (`RoomAnchor`)

`SetDiscovered(true)` durante o jogo dispara a corrotina `Reveal`:

1. A lâmpada liga e "pega no tranco": nível 0,55 → 0 → 0,8 → 0,05 → 1 em ~0,3 s.
2. Ao mesmo tempo, `_Dissolve` vai de 0 a 1 em `revealDuration` (0,6 s) via property block.
3. No fim, a névoa é desativada e o bloco é limpo (`SetPropertyBlock(null)`).

Lâmpada velha (`RoomDef.FlickeringLight`): Perlin 0,85–1,0 a ~10 Hz + "apagão" a 10% por 0,05–0,25 s a cada 3–9 s. O mesmo nível tinge bulbo, cúpula e raio. Como no doc 05/06, é `UnityEngine.Random`, cosmético, fora da seed da run.

### `BevelMesh`

Caixa chanfrada gerada por código: 6 faces + 12 chanfros + 8 cantos, normais chapadas por grupo (o toon desenha uma faixa de luz em cada quina, efeito "massinha"), com afunilamento opcional (topo menor). Malhas em cache por tamanho; no editor, `HTVisualSetup.BevelAsset` salva como asset em `Art/Meshes/` para a cena não guardar malhas soltas.

---

## 11. Personagens: do script Python ao FBX

No GMS você desenha a animação no Aseprite, exporta `spr_ator_walk_strip8.png` e importa. Aqui **não existe modelagem manual**: os 4 atores saem de uma **tabela de dados** e de construtores em Python, rodando o Blender sem janela (`bpy` 5.2). Rodar de novo gera os mesmos arquivos.

```bash
cd Art/Blender
python3 build_all.py                 # paleta + 4 FBX + animações + verificação + previews (~2 min)
python3 build_all.py --no-previews   # só os arquivos (~5 s)
```

`build_all.py` roda cada etapa num **processo novo** (cena limpa) com `PYTHONHASHSEED=0` (o exportador FBX usa `hash()` nos IDs; sem isso, o arquivo muda a cada build):

| Etapa | Script | Saída |
|---|---|---|
| Paleta | `ht_common.write_palette_png` | `Characters/HT_Palette.png` (16×16) |
| Atores | `build_actor.py --actor all` | `HT_Atleta/Nerd/Popular/FinalGirl.fbx` + `blend/*.blend` |
| Animações | `build_anims.py` | `HT_Anims.fbx` (6 takes) |
| Verificação | `verify_fbx.py` | reimporta e confere o contrato → `Previews/verificacao.txt` |
| Previews | `render_previews.py` | renders Cycles que **imitam** o toon (sem lâmpadas nem névoa) |

### A tabela (`actors_data.py`)

Cada ator é um `dict` em `ACTORS`: `name`, `file` (`"HT_Nerd"`), `sk` (alturas das juntas sobre o `BASE_SK`), `torso` (anéis: z, meia-largura, profundidades, expoente), `arm`/`leg` (raios ao longo do membro), `head`, `face` (olhos, pupila, sobrancelha, nariz, boca), `colors`, `roupa` (regras de cor: a primeira que casar vence), `shoes`, `hair` (`flat_top`, `cuia`, `rabo_alto`, `rabo_baixo`) e `acc` (adereços de `ACC_BUILDERS`). `ACTOR_ORDER` diz quem é gerado.

### O que `build_actor` faz

1. Corpo por **loft** (anéis em volta do esqueleto, seção superelíptica), cabeça, peças do rosto, cabelo e adereços, acumulados num `MeshData` com cor e **peso por vértice**.
2. Armature com os 21 nomes do Humanoid da Unity, pose T.
3. Pesos **calculados por cadeia** (`chain_weights`), não *bone heat*: o corpo é feito de cascas fechadas sobrepostas, onde o automático falha. Peças rígidas (cabeça, cabelo, óculos, calçados, mochila) vão 100% num osso.
4. Empty `Rosto` filho do osso `Head`; olhos, pupilas, sobrancelhas e as 4 bocas são filhos do `Rosto`, **sem skin**, com origem no próprio centro, escala 1 e rotação 0 (o jogo mexe nelas).
5. Exporta FBX com os parâmetros do guia: `axis_forward='-Z'`, `axis_up='Y'`, `bake_space_transform=False`, `mesh_smooth_type='FACE'`, `add_leaf_bones=False`, só ossos *deform*, e `EMPTY` incluído (o `Rosto`).

As animações (`ht_anims.py`) também são **dados**: poses-chave como listas de `(eixo, graus)` por osso, com espelhamento automático (`"L*UpperArm"` gera o lado direito). `build_anims` assa cada clipe numa faixa NLA; o exportador gera **um take por faixa**. Exporta no quadro −10 (sem faixas ativas = pose T) porque um FBX só de esqueleto não tem *bind pose*.

**Analogia GMS:** é um gerador de sprites por código: em vez de desenhar, você descreve medidas e cores numa tabela, roda o script e sai a strip. Para mudar a jaqueta, muda um número.

### Paleta-textura

Nada de textura pintada nem UV artístico. `HT_Palette.png` tem 16×16 pixels; cada pixel é um hex do guia (lista `PALETTE` em `ht_common.py`). Cada face do modelo tem UV apontando para o **centro** de uma célula (`palette_uv`). Células vazias são magenta (`#FF00FF`) para chamar atenção.

Consequências:
- **1 material por ator** (`M_Personagem_<Ator>`, `_BaseColor` branco × paleta).
- Mudar um hex = só regerar o PNG; as UVs dos FBX continuam apontando para a mesma célula.
- **Nunca reordenar** a lista: o índice é a posição na textura. Cor nova vai **no final**.
- A textura precisa de filtro **Point**, sem mipmap, sem compressão, Clamp: senão a GPU mistura vizinhos e a cor "vaza".

**Analogia GMS:** é o `gpu_set_texfilter(false)` + uma sprite de paleta para *palette swap*, só que a "cor" de cada face é um endereço na paleta.

---

## 12. Importação na Unity (`HTCharacterSetup`)

No GMS as propriedades de sprite (origem, máscara, grupo de textura) são ajustadas à mão. Na Unity cada arquivo importado tem um **importador** com configurações gravadas no `.meta`, e um script de editor pode mudá-las. `HTCharacterSetup.Setup()` (chamado pelo builder e pelo menu **Horror Tycoon > Visual > Configurar personagens**) faz isso:

| Configuração | Valor | Por quê |
|---|---|---|
| Scale Factor / Convert Units | 1 / ligado | o Blender já exporta em metros |
| **Bake Axis Conversion** | **desligado** | ver abaixo |
| Normals | Import | as normais suaves do Blender (casca sem buracos) |
| Mesh Compression | Off | |
| Materials | **None** | o `HT_Toon` é atribuído por script |
| Animation Type | **Humanoid**, *Create From This Model* | permite o retarget (seção 13) |
| Import Animation | só no `HT_Anims` | |
| Paleta | Point, sem mip, sem compressão, Clamp, sRGB | seção 11 |

`ConfigureModel` usa um `Set(atual, desejado, aplicar)` que só marca "sujo" quando muda: reimportar FBX é lento, então o build não reimporta se nada mudou.

### Armadilha 1: Bake Axis Conversion

Blender é Z para cima; Unity é Y para cima. O exportador converte, e a Unity pode "assar" a conversão nos vértices (*Bake Axis Conversion*). O guia e o README do Blender esperavam ligado. **Testado na Unity 6.3: ligado, o ator olha para −Z.** Desligado, ele olha para +Z, esquerda em −X e a raiz sem rotação (o −90° fica só nos filhos `Corpo` e `*_Rig`, o que o Humanoid ignora). O builder gira a raiz do ator 180° para ele encarar a câmera inicial.

### Armadilha 2: o mapa Humanoid guarda pose velha

O mapeamento Humanoid no `.meta` inclui a pose de **todos** os transforms (inclusive `Rosto`). Quando a conversão de eixos mudou, essa pose ficou velha e o Animator a reaplicava em runtime: **o rosto ia parar na nuca**. Solução: quando a importação muda, zerar `humanDescription.skeleton` e `.human` antes de reimportar, para a Unity remapear com a pose atual.

### Armadilha 3: o auto-mapeamento "acha" olhos e mandíbula

O Humanoid tem ossos opcionais `LeftEye`, `RightEye` e `Jaw`. O auto-mapeamento pegava `Sobrancelha_E` como olho e `Boca_Feliz` como mandíbula; aí o Animator sobrescrevia essas peças todo frame e as expressões paravam. O setup **remove** esses três do mapa e, no sentido contrário, garante `Chest`, `LeftToes` e `RightToes`, que o automático às vezes pulava.

### Clipes do `HT_Anims`

Loop em `Idle`, `Walk`, `Talk`, `Scared_Loop`. Raiz com rotação, Y e XZ *Bake Into Pose* (o pulo do Scared e a queda do Death ficam na pose). **Apply Root Motion desligado**: quem move o ator é o `NavMeshAgent`; o `Walk` anda no lugar.

---

## 13. Animator Controller (o `sprite_index` com cérebro)

No GMS a máquina de estados visual é você no Step: `if (speed > 0) sprite_index = spr_walk; else sprite_index = spr_idle;`. Na Unity, a máquina mora num asset (**Animator Controller**), com estados, transições e **parâmetros**. O código só escreve parâmetros; as regras de troca são dados.

`HT_Actor.controller` é **recriado a cada build** por `BuildController()` e serve os 4 atores: o **Humanoid** traduz cada esqueleto para um "corpo padrão", então um clipe feito no esqueleto base toca em qualquer ator (*retarget*).

| Parâmetro | Tipo | Quem escreve |
|---|---|---|
| `Speed` | float (m/s) | `ActorAnimDriver` (velocidade do `NavMeshAgent`, com amortecimento 0,08) |
| `MoveRate` | float | `ActorAnimDriver`: `speed / 1,4` limitado a 0,6–2,0. É o **multiplicador de velocidade** do estado Walk |
| `Talking` | bool | `ActorIdle` (conversa com o colega, doc 06) |
| `Afraid` | bool | `ActorView` (expressão = Assustada) |
| `Scared` | trigger | `ActorView`, só ao **entrar** em Assustada |
| `Dead` | trigger | `ActorView.SetDead()` |

Estados: `Idle ⇄ Walk` (Speed > 0,15 / < 0,1), `Idle ⇄ Talk`, `Talk → Walk`, **Any State** → `Scared` → `Scared_Loop` (enquanto `Afraid`) → `Idle`, **Any State** → `Death` (sem saída).

**Analogia GMS:** `Speed` e `Talking` são variáveis de instância que o Step lê; um **trigger** é um bool que se desliga sozinho depois de consumido (o seu `just_scared = true` que o próprio estado zera). `MoveRate` é o `image_speed`.

Detalhes do builder: `cullingMode = AlwaysAnimate` (o close do doc 06 lê o `Rosto` mesmo com o ator fora da tela) e `updateWhenOffscreen` no `SkinnedMeshRenderer` (pulo e morte saem da caixa da pose T e o ator sumia na borda da tela).

---

## 14. Rosto por multiplicadores (`ActorView`)

O `ActorView` tem dois "rigs": **modelo** (tem as 4 bocas) e **cápsula** (fallback antigo, valores absolutos). No modelo:

1. `CaptureFaceRig()` (no build, com o modelo em pose T; ou no `Awake` se faltar) guarda posição, escala e rotação **base** de cada peça, e **descobre os eixos** do rosto no espaço local de cada peça (qual eixo é profundidade, altura, frente da sobrancelha). Assim o código não depende da convenção de eixos do FBX (armadilha 1).
2. `ApplyModelExpression()` aplica a tabela do README como **multiplicadores** sobre a base:

| Expressão | Olho (L / A) | Pupila | Olhar | Sobrancelha | Boca | Corpo |
|---|---|---|---|---|---|---|
| Neutra | 1 / 1 | 1 | 0 | 0 | Neutra | 1 |
| Feliz | 1 / 0,8 | 1,1 | 0 | +0,014 m, +4° | Feliz | 1 |
| Tensa | 1 / 1 | 0,7 | 0,013 m | −0,008 m, −18° | Tensa | 1 |
| Assustada | 1,6 / 1,6 | 0,35 | 0 | +0,030 m, +16° | Grito | XZ 0,9 / Y 1,15 |

3. Boca: `mouths[i].SetActive(i == mouthIndex)`, as 4 malhas no mesmo lugar.
4. O "esticar" do susto é na **escala da raiz do modelo**, suavizado no `LateUpdate` (`squashSpeed` 14, lerp independente de FPS do doc 05). Não é shape key: as peças do rosto não têm skin, e uma shape key no corpo deixaria o rosto para trás.
5. Morte: trigger `Dead`, olhos fechados (altura 0,12), pupilas escondidas, boca neutra.

`OnValidate` chama `ApplyExpression`: trocar a expressão no Inspector mostra o rosto **sem dar Play**.

### `BuildActors` no construtor

Para cada `ActorDef` do conteúdo: `HTCharacterSetup.ModelFor(def.DisplayName)` procura `HT_` + nome sem espaços ("Final Girl" → `HT_FinalGirl.fbx`). Achou → `BuildModelRig`: instancia o FBX como filho "Modelo", `CapsuleCollider` na raiz (r 0,3 × 1,75 m, para o clique), Animator com avatar + controller, `ActorAnimDriver`, materiais (`SkinnedMeshRenderer` → corpo e lista de contorno; `Olho_*` → olho; resto → rosto, sem sombra) e liga tudo no `ActorView` por `SerializedObject` (técnica do doc 05). Não achou → `BuildCapsuleRig`. Por fim, todos os renderers do ator recebem `AllLayers`.

As cores do modelo vêm da paleta; o `ActorDef.Color` **não tinge** o modelo (ele só vale para a cápsula). O `frameHeight` do close (`ActorFocusController`) subiu de 0,8 para 1,1 por causa das cabeças grandes.

---

## 15. Como mexer

| Quero mudar… | Onde |
|---|---|
| degraus da rampa (cor/limiar) | `ToonMaterials.ApplyEnvironment/ApplyCharacter` (permanente) ou Inspector do material para testar: `Meio-tom`, `Sombra`, `Limiar meio-tom` (0,55), `Limiar sombra` (0,30), `Suavidade da borda` |
| força do ambiente / poça das lâmpadas | `ToonMaterials.AmbientStrength` (1,5), `PointGain` (0,8), `CharacterPointGain` (0,45); no material: `Limiar 100%` (0,5), `Limiar 45%` (0,16), `Nível do degrau baixo` (0,45) |
| cores da casa | `HTVisualSetup.Wall` e as chamadas `HTVisualSetup.Environment(...)` no `GreyboxSceneBuilder`; móveis em `FurnitureKit`; cenário: `Art/Cenario/Materiais/M_Cen_*` à mão (preservado) |
| largura do contorno dos atores | `M_Personagem_<Ator>` / `_Rosto` / `_Olho` → `Largura (px a 1080p)`; padrão em `ToonMaterials.CharacterOutlineWidth` e `HTCharacterSetup.FaceMaterial/EyeMaterial` |
| contorno do selecionado | `ToonMaterials.SelectedOutline`, `SelectedOutlineWidth` (3,5) |
| contorno do cenário | `Assets/Settings/PC_Renderer.asset` → `HT Edge Outline`: cor, `thickness` (1), `depthThreshold` (0,06), `normalThreshold` (0,5), `fadeStart`/`maxDistance` (22/60 m), `fogFade`. Desligar = checkbox do feature |
| rim de lua | `M_Personagem_*` → `Força/Potência/Corte do rim` (0,4 / 3,5 / 0,22) |
| névoa, lua | `HTVisualSetup.FogDensity` (0,015), `FogColor`, `MoonIntensity` (0,5); céu: `M_Ceu` |
| névoa rasteira | `M_FX_NevoaRasteira_1..3` (alpha, escala/velocidade do noise, `_HoleRect`) |
| pós-processamento | `HTVisualSetup.ApplyPostProcessing` (reaplicado a cada build). Para editar o `PP_FilmeDeTerror` à mão, desligue essa chamada no builder |
| cômodo não descoberto / descoberta | `M_FX_NevoaSala`, `M_FX_NevoaCamada`, `M_FX_FitaApagada`; `RoomAnchor` → `Undiscovered Floor`, `Reveal Duration` (0,6), `Window Glow` (1,4) |
| brilho dos olhos | `M_Personagem_Olho` → Emissão |
| ritmo da caminhada | `ActorAnimDriver` → `walkClipSpeed` (1,4), `moveRateRange` (0,6–2,0); velocidade real: `agent.speed = 2.6f` no `BuildActors` |
| cor de roupa/pele de um ator | hex em `PALETTE` (`ht_common.py`) → `python3 build_all.py --no-previews` → copiar só `HT_Palette.png` |
| forma/roupa/cabelo/adereço | entrada do ator em `actors_data.py` → `build_all.py` → copiar o FBX |
| reinstalar render / personagens | menus **Horror Tycoon > Visual > Configurar render (URP)** / **Configurar personagens** |

Regra de ouro: Inspector serve para **experimentar**; o build reaplica os presets. O valor que fica vai para o código.

### Acrescentar um ator, de ponta a ponta

1. **Tabela:** em `Art/Blender/actors_data.py`, copie uma entrada de `ACTORS` com chave nova (ex.: `"roqueira"`), `name="Roqueira"`, `file="HT_Roqueira"`. Troque medidas, `colors`, `roupa`, `hair` (um dos 4 construtores) e `acc` (só os de `ACC_BUILDERS`; adereço novo = função nova em `build_actor.py`). Acrescente a chave em `ACTOR_ORDER`.
2. **Cores novas:** no **final** de `PALETTE` em `ht_common.py`. Nunca no meio, e longe das cores reservadas aos vilões (guia §3.3).
3. **Gerar:** `python3 build_all.py`. Confira `Previews/verificacao.txt` (altura ~1,75 m, frente, hierarquia, pesos) e as folhas `Previews/HT_*`.
4. **Copiar** `Characters/HT_Roqueira.fbx` e `HT_Palette.png` (mudou) para `Assets/_HorrorTycoon/Art/Characters/`. O `HT_Anims.fbx` não muda: o retarget serve o ator novo.
5. **Importador:** acrescente `"HT_Roqueira"` em `HTCharacterSetup.ActorModels`.
6. **Conteúdo:** o builder só cria atores que existem na lista `actors` do `GameContentDef`. É preciso um `ActorDef` cujo `DisplayName` sem espaços seja `Roqueira` (é assim que `ModelFor` acha o arquivo: só tira os espaços, não tira acento; "Gótica" procuraria `HT_Gótica.fbx`).
7. **Construir Cena Greybox.** O material `M_Personagem_Roqueira` é criado com a paleta. Confira no Inspector do FBX: altura 1,71–1,79 m, raiz sem rotação; na cena: ator olhando para a câmera e rosto mudando no Inspector do `ActorView`.

Se o FBX faltar ou o nome não bater, o ator aparece como **cápsula**: é o sinal de que o passo 5 ou 6 falhou.

---

## 16. Limitações conhecidas

- **Caminhada acelerada:** o agente anda 2,6 m/s e o clipe `Walk` casa com ~1,4 m/s, então `MoveRate` fica perto de 1,9. Ficou cartunesco; se incomodar, baixar `agent.speed` (mexe no ritmo do jogo, não só no visual).
- **Linhas onde as cascas se cruzam** (ombro, punho, virilha, testa/cabelo). Não houve buraco visível com as normais importadas, então o truque de normais suavizadas em UV não foi usado.
- **Cabelo e rabos de cavalo rígidos** (não balançam), mãos sempre abertas (sem ossos de dedo), celular da Popular colado na palma, mochila do Nerd atravessa o chão no `Death`, topo do flat-top do Atleta um pouco facetado.
- **Contorno de cenário só no `PC_Renderer`.** `Mobile_Renderer` e a cena `Lab_Cenario` ficaram sem.
- **Flicker de perseguição não ligado** (2× mais frequente, puxando para `#FF4A3D`): falta um gancho de jogo, ex. `RoomAnchor.SetChase(bool)`.
- **`Scared_Loop` mantém o ator encolhido** enquanto a expressão for Assustada; se ele andar, sai para o `Walk`.
- **Colisor do clique entra no NavMesh** se o NavMesh for assado com os atores parados no lugar (já era assim com a cápsula).
- **Triângulos acima do guia:** 8,4–9,3 mil visíveis por ator (guia: 3–6 mil), ~36 mil os quatro. Para baixar: `segs` nos construtores.
- **Desvios de identidade:** sem cor-tema de parede por cômodo (paredes são compartilhadas), sem URPFog (névoa de altura), especular duro desligado nos personagens (no corpo inteiro virava ruído), bocas como 4 malhas em vez de sprite, sem shape keys `Grito`/`Piscar`.
- **Enfeites presos a mais de um trecho de parede** (porta da frente) seguem o último trecho que mudou de corte.
- **Desempenho:** 3 planos transparentes de tela cheia (névoa rasteira) + 1 passe de tela cheia (contorno). Se pesar, deixar 2 planos.
- **Ajuste à mão é sobrescrito** no build (materiais da casa e dos personagens, perfil de pós).
- As prévias *inline* da ferramenta de screenshot saem lavadas; os PNG em `Captures/` estão corretos.
- O guia inteiro é **proposta v1**: paleta, proporções e pós ainda pedem o teste de cinza (§3.5) e playtest.

---

## 17. Experimente

1. No Play, abra **Window > Analysis > Frame Debugger**. Ache `ForwardToon` e `Outline` de um ator e o passe `HT Edge Outline`. Selecione um ator e veja se ele sai do lote do SRP Batcher (property block).
2. Desligue o checkbox do `HT Edge Outline` no `PC_Renderer`. O que some no cenário? E nos atores?
3. Em `M_Personagem_Nerd`, ponha `Largura` em 0 e depois em 6. Onde aparecem as linhas de cruzamento de cascas?
4. Em `M_Parede`, suba `Suavidade da borda` para 0,2: o toon vira Lambert comum. Volte a 0,04.
5. Selecione o "Modelo" de um ator, abra a janela **Animator** e mande ele andar e conversar. Mude `agent.speed` para 1,4 no Play e veja o `MoveRate` ir para 1.
6. Troque o hex de `nerd_colete` em `PALETTE`, rode `python3 build_all.py --no-previews` e copie só o `HT_Palette.png`. O FBX nem precisou mudar.
7. No importador do `HT_Nerd.fbx`, ligue *Bake Axis Conversion* e aplique. Para onde o ator olha? (**Construir Cena Greybox** desfaz.)
