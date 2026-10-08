# Horror Tycoon — Direção de Arte v1

> **Status: proposta v1 (🧪 hipótese).** Nada aqui está validado. Gabriel decide; os valores existem para outros agentes conseguirem implementar e testar já. Onde há valor atual no projeto, ele aparece ao lado para comparar.
> Tom (✅ GDD §1): terror **exagerado, agradável e cartunesco**. Assusta, mas é gostoso de assistir. Nada de gore ou realismo.
> Base técnica: Unity 6.3 LTS, URP 17 (Render Graph), Forward+. Somente soluções gratuitas.

---

## 1. Pilares visuais

1. **Fundo escuro, ator colorido.** O cenário é pintado em azuis e roxos de pouco valor (luz baixa). Os atores são as manchas mais saturadas e claras da tela. É a regra dos fundos de Scooby-Doo: dá para ler a cena até numa miniatura.
2. **Reação maior que a ameaça.** O medo aparece no corpo e no rosto do ator: olhos que dobram de tamanho, pulos, cabelo arrepiado. O susto é a piada, como no Luigi's Mansion. Sangue e vísceras nunca aparecem.
3. **Poças de luz quente num mar frio.** Cada cômodo é uma ilha de lâmpada quente (laranja) dentro da noite azul da lua. O verde doentio e o vermelho entram como sinal de perigo.
4. **Set de filmagem feito à mão.** Formas grossas, um pouco tortas e "de massinha", como Two Point, Coraline e Monster House. As cores são chapadas e não há textura de fotografia. A casa parece cenário construído, não casa real.
5. **Legível de cima, expressivo de perto.** Cada ator é reconhecível pelo **topo da cabeça e pelos ombros** (o que a câmera iso mostra) e pelo **rosto** (o que o close mostra).

---

## 2. Referências: o que pegar e o que evitar

| Referência | Pegar (observável) | Evitar |
|---|---|---|
| **Luigi's Mansion 3** | O protagonista medroso carrega o tom: tremedeira, pulo de susto, andar encolhido. Cada andar/cômodo tem uma **cor-tema** própria. Pontos de luz quente contra a escuridão fria. Fantasmas **translúcidos e luminosos** com rosto enorme. Móveis curvos e exagerados. Muita reação física (poeira, objetos voando). | O acabamento quase PBR (tecido, verniz, reflexo). Custa caro para um dev solo e briga com o toon. |
| **Ghost Master** | Prédio em corte visto de cima, estilo gestão. Os mortais **mostram o medo** (barras de terror/crença, fuga em pânico): é o equivalente direto do nosso Pavor. Humor de filme B. | Personagens pequenos e genéricos, ilegíveis em zoom out. Cores lavadas dos anos 2000. |
| **Two Point Hospital** | Visual de **massinha feita à mão** (objetivo declarado da equipe, que queria fugir do visual "puxado pela tecnologia"). Cabeças e mãos grandes, pernas curtas, rostos simples. Leitura perfeita na câmera de gestão. Atores interagindo e fazendo bobagens no corredor. Adereços que encaixam na mão. | A paleta pastel de dia e a luz chapada e alegre. Lá não existe noite nem perigo. |
| **Cult of the Lamb** | A abstração fofa permite temas sombrios: "é OK porque são cartoons" (diretor de arte). O personagem inocente com **um** elemento sinistro (a coroa). Contorno grosso. | Ir longe demais no demoníaco/grotesco. |
| **Scooby-Doo (desenho)** | Fundos pintados em azul-roxo escuro, personagens em cores chapadas e saturadas por cima. **Olhos brilhando no escuro.** Perseguição como gag. | O cenário 2D estático. |
| **Monster House / Coraline / Tim Burton** | A casa como personagem: linhas tortas, telhado inclinado, proporções esticadas. Laranja de janela contra azul da noite. | O detalhamento e a textura de stop-motion (inviável). |
| **Don't Starve** | Linhas tortas e assimétricas, móveis "bambos". | A dessaturação total e o marrom sujo. |
| **Little Nightmares (contraste)** | Exagero de escala e uma única luz quente no escuro. | Corpos grotescos, angústia real, sombra preta pura. É o tom que **não** queremos. |

---

## 3. Paleta (hex)

### 3.1 Ambiente (valores baixos, puxados para azul/roxo)

| Uso | Hex | Nota |
|---|---|---|
| Parede, papel de parede base | `#5E5068` | Malva empoeirado. Cada cômodo varia o matiz em ±15° (cor-tema) |
| Parede, faixa/detalhe | `#7A6A80` | |
| Rodapé, batente, madeira escura | `#4A3328` | |
| Móvel de madeira clara | `#8A5A3C` | |
| Piso de madeira | `#5C4033` | |
| Azulejo (cozinha/banheiro) | `#6F8580` | Verde-acinzentado |
| Tecido/tapete | `#7A3B47` | Vinho |
| Céu (topo → horizonte) | `#0D1024` → `#1E2A44` | |
| Neblina | `#2B3550` | Atual `#0A0D14` (quase preto). Proposta: mais clara, para a névoa **aparecer** |
| Névoa rasteira (perto do chão) | `#4A5A73` | |
| Silhueta da floresta | `#121A22` | Compatível com os pinheiros atuais (`#172E33`, `#1A293B`) |
| Tinta da sombra (multiplica a cor base) | `#5B4E8C` | Sombra violeta, **nunca preta** |
| Cômodo não descoberto | `#0E0F17` | |

**Cores-tema por cômodo** (matiz da parede): Cozinha `#5E6B55` (oliva) · Sala `#6A4E4A` (terracota) · Banheiro `#4E6670` (azul-petróleo) · Quarto `#5E5068` (malva) · Porão `#3E3A36` (marrom) · Sótão `#4A4258` (lavanda escura) · Corredor `#55483F` (neutro quente).

### 3.2 Luzes

| Luz | Hex | Atual |
|---|---|---|
| Abajur/lâmpada quente | `#FFB35C` | Varanda `#FFCC80` |
| Lâmpada de teto incandescente | `#FFD08A` | |
| Verde doentio (perigo, banheiro/porão) | `#A4E05F` | |
| Lua fria | `#8CA6FF` | Já em uso (`0.55, 0.65, 1`). Manter |
| Vermelho de alerta (Condenado/perseguição) | `#FF4A3D` | |
| Luz de TV/tela | `#7FE3FF` | |

### 3.3 Acentos por subgênero (reservados ao vilão e aos efeitos dele)

| | Primária | Secundária | Neutra |
|---|---|---|---|
| **Slasher, "O Mascarado"** | Vermelho cereja `#E0323C` | Ferrugem `#C8612A` | Osso da máscara `#EDE3C8` |
| **Sobrenatural, "A Entidade"** | Ectoplasma `#5CF2C2` | Violeta espectral `#9B6CFF` | Núcleo branco `#E8FFF8` |

Nenhum ator usa essas cores como cor principal. Quando elas aparecem na tela, o jogador sabe que o vilão está agindo.

### 3.4 UI (claquete e monitor)

Papel `#F2EAD8` · Tinta `#15121A` · Fita crepe/destaque `#F5C542` · REC `#FF3B3B` · Tensão `#FF8A3D` · Impacto `#FFD23F` · Pavor `#7C5CFF`.
Estados de perigo: Seguro `#6ED37A` · Em Risco `#F5C542` · Perigo Crítico `#FF8A3D` · Condenado `#E0323C`.

### 3.5 Regras de valor e contraste

- **Valor (V do HSV):** ambiente entre 20 e 45%. Atores entre 55 e 90%. Luzes e olhos acima de 90% (únicos com bloom).
- **Saturação:** ambiente entre 15 e 40%. Atores entre 45 e 75%. Acento de vilão acima de 80%.
- **Proporção 60/30/10:** 60% noite fria, 30% poças quentes, 10% acento (atores e vilão).
- **Teste obrigatório:** print em escala de cinza da câmera iso. Os 4 atores precisam continuar distinguíveis entre si e destacados do chão.

---

## 4. Personagens

### 4.1 Proporções e forma

- **Altura:** 1,75 m (✅ GDD §8.3). A variação de silhueta vem de cabelo, ombros e postura, não da altura do NavMesh. Opcional: ±5 cm só no visual.
- **3,5 cabeças de altura.** Cabeça (queixo ao topo, sem cabelo) ≈ 0,50 m. Tronco ≈ 1,2 cabeça. Pernas ≈ 1,3 cabeça (curtas).
- **Mãos 1,3× e pés 1,2×** o tamanho realista, como em Two Point. Braços grossos, sem cotovelo marcado. **Nenhum membro abaixo de 9 cm de diâmetro** para não sumir na câmera iso.
- **Formas:** cada ator tem uma forma-base (triângulo, retângulo, ampulheta, gota). Cantos arredondados e nada de detalhe menor que 3 cm.
- **Topo da cabeça = identidade.** Da câmera iso vê-se o cabelo e os ombros, por isso cada ator tem cor e forma de cabelo únicas.

### 4.2 Rosto e expressões

- **Olhos:** elipsoides brancos grandes (≈ 22% da altura da cabeça), separados, com pupila preta redonda. Material **unlit, branco levemente emissivo**, para os olhos brilharem no escuro (gag de Scooby-Doo, que já combina com os "olhos no mato" do cenário).
- **Sobrancelhas:** peças grossas soltas na testa (feitas para animar por rotação e altura).
- **Boca:** sprite trocável (atlas de 4 quadros) num decalque no rosto. Nariz pequeno e simples. Sem orelhas detalhadas.
- **Expressões** (combinação de peças, sem depender de blendshape facial):

| Expressão | Olhos | Pupila | Sobrancelha | Boca | Corpo |
|---|---|---|---|---|---|
| Neutra | 1,0× | 1,0×, centro | reta | linha leve | parado |
| Feliz | 0,8× de altura (meia-lua) | 1,1× | alta, arqueada | sorriso largo aberto | balanço |
| Tensa | 1,0× | 0,7×, olhando para o lado | baixa, em V invertido | dentes cerrados, ondulada | ombros erguidos, tremor |
| Assustada | **1,6×** | **0,35×** (pontinho) | no topo, inclinada | "O" gigante | shape key "Grito": estica 1,15× em Y e encolhe 0,9× em X, cabelo arrepiado |

### 4.3 Os 4 atores

Cada um ocupa um matiz diferente (laranja, oliva, magenta, azul), longe das cores do vilão.

| | **Atleta** | **Nerd** | **Popular** | **Final Girl** |
|---|---|---|---|---|
| Forma | Triângulo invertido (ombros 1,4×) | Retângulo fino, ombros caídos | Ampulheta + cabelo volumoso | Gota firme, postura reta |
| Gancho na iso | Ombreiras largas laranja | Mochila quadrada nas costas | Rabo de cavalo enorme rosa-claro | Camisa jeans azul (único ator frio) |
| Gancho no close | Faixa de cabeça e queixo quadrado | **Óculos redondos gigantes** com reflexo | Cílios grandes, brilho labial | Curativo na bochecha, olhar firme |
| Cabelo | Flat-top loiro palha `#E8C15A` | Cuia bagunçada marrom `#4A2E1E` | Rabo alto platinado-rosado `#F4C6D7` | Rabo baixo ruivo-escuro `#7A2E1F` |
| Roupa | Jaqueta varsity `#E07B2E` com mangas creme `#EFE3C4`, calça `#3A3F55`, tênis branco | Colete xadrez mostarda `#C9A23B` sobre camisa oliva `#6E8B3D`, calça cotelê `#6B4A33` | Top magenta `#E2559A`, saia/calça rosa `#F2A7C8`, jaqueta branca `#F5EEF0` | Regata branca `#EDEBE3`, camisa jeans aberta `#4F6C9A`, jeans `#2F3E5C`, coturno `#3A2A22` |
| Pele | `#8D5A3B` | `#F0C8A0` | `#D9A27A` | `#E8B48F` |
| Adereço | Bola de futebol americano / apito | Mochila com bottons | Celular com capinha brilhante | Molho de chaves no cinto (sem arma; não brigar com os artefatos) |
| Linguagem corporal | Pula e corre na frente | Encolhido, segura as alças da mochila | Pose de selfie, mão na cintura | Para, olha em volta, fica pronta |

### 4.4 Vilões (forma inicial)

- **O Mascarado:** 2,1 m e proporção **mais longa** (≈5 cabeças) para contrastar com os teens atarracados. Macacão `#3B3F3A`, máscara de osso `#EDE3C8` com olhos vazados pretos. A faca é **grande e cartunesca** (parece adereço de cena). Cabeça sempre inclinada. Contorno preto mais grosso que o dos atores.
- **A Entidade:** flutua, sem pernas, tem cauda de lençol. Shader unlit/emissivo `#5CF2C2` com fresnel `#9B6CFF` e transparência de 60–80%. Boca enorme ao estilo Luigi's Mansion. **Não tem contorno** de tinta; o brilho de borda substitui o contorno, para parecer "de outro mundo".

---

## 5. Ambiente

- **Estilo da casa e dos móveis:** grossos, com bordas chanfradas (bevel de 2–4 cm). Tudo **levemente torto**: inclinação de 1–3° em batentes, quadros e estantes e afunilamento de 5–10% em armários e cadeiras. **Escala exagerada** no que conta a história (relógio de pé, geladeira, banheira), em 1,15–1,3×. O espaço de passagem continua na escala real (porta 0,9 m, ✅).
- **Materiais:** **cor chapada** sem textura. Variação só por (a) gradiente vertical sutil em paredes e móveis (base 15% mais escura que o topo, sugerindo sujeira e oclusão) e (b) uma **paleta-textura** compartilhada (ver §8.2). Sem normal map nem roughness map. Smoothness 0 em quase tudo; brilho só em vidro, louça e TV.
- **Detalhes de "set":** fita crepe no chão (`#C9B458`), refletor de cena aparecendo atrás das paredes cortadas e cadeira de diretor no corredor. São baratos e reforçam o conceito de filme.
- **Cômodo não descoberto** (atual: escuro, sem móveis, sem luz ✅): manter, e acrescentar (1) piso `#0E0F17` com **marcações de fita crepe** a 25% de opacidade (set ainda não montado), (2) névoa interna animada (noise rolando, `#1A1D2B`, alpha 0,6) no lugar do cubo `M_Nevoa` sólido e (3) batente da porta visível com um leve contorno. Ao descobrir, a névoa se dissolve em 0,6 s e a lâmpada acende com um *flicker* de partida.

---

## 6. Iluminação e atmosfera (valores para testar)

| Item | Proposta v1 | Atual |
|---|---|---|
| Lua (Directional) | Cor `#8CA6FF`, intensidade **0,5**, rotação (55, −35, 0), sombra suave | 0,9, mesma cor e rotação |
| Ambiente | **Gradient**: céu `#2A3352`, equador `#2A2340`, chão `#120E16`, intensidade 1 | Flat `#292E42` |
| Lâmpada de cômodo (Point) | `#FFB35C`, intensidade 2–3, alcance 5,5 m, sem sombra (no máximo 2 pontos com sombra no ato) | |
| Névoa (built-in) | Exp², cor `#2B3550`, densidade **0,012–0,018** | Exp², `#0A0D14`, 0,02 |
| Névoa de altura (externa) | URPFog modo Height: base y = 0, falloff 1,5 m, cor `#4A5A73`, noise ligado | — |
| Névoa rasteira | Quads/partículas de 0,1–0,6 m de altura, alpha 0,2–0,3, noise rolando a 0,03 m/s, *soft particles* | — |
| Raios de luz falsos | Malha-cone aditiva na janela e na lâmpada, `#8CA6FF`/`#FFB35C`, alpha 0,06–0,1, fade por fresnel e pela distância à base | — |
| Rim (lua) | No shader, só em personagens: `#8CA6FF`, força 0,4, potência 3,5, mascarado pelo lado da lua | — |
| Flicker | Ruído de Perlin 0,85–1,0 a ~10 Hz + "apagão" de 0,05–0,25 s até 10% a cada 3–9 s. Em perseguição: 2× mais frequente e cor puxada para `#FF4A3D` | Bool `flickeringLight` |

**Por que a neblina mais clara:** com a câmera teleobjetiva a 11–20 m (doc 06), a névoa por distância pinta ator e fundo quase igual. Uma névoa escura vira só "escurecer tudo". Clara e baixa (de altura), ela recorta a silhueta da casa e da floresta.

**Pós-processamento** (perfil `PP_FilmeDeTerror`):

| Efeito | Proposta | Atual |
|---|---|---|
| Tonemapping | **Neutral** (preserva o matiz das cores chapadas) | ACES |
| Saturação / Contraste | **−10** / +15 | −30 / +18 |
| Split toning | Sombras `#3B3A78`, realces `#FFB36B`, balance −20 | — |
| Vinheta | 0,35, suavidade 0,45, cor `#0B0A12` | 0,4 / 0,45 |
| Film grain | Thin1, 0,25, response 0,8 | 0,35 |
| Bloom | Threshold 1,0, intensidade 0,5, scatter 0,6 (lâmpadas, olhos, ectoplasma) | — |
| Aberração cromática e distorção | 0 em repouso; pulso de 0,3 s no susto (0,4 / −0,15) | — |

> Risco: −30 de saturação "come" a identidade dos atores, que é o pilar 1. Comparar lado a lado no playtest.

---

## 7. Shading (toon)

- **Rampa de 3 degraus** sobre half-Lambert (`NdotL*0,5+0,5`):
  - luz: `base`
  - meio-tom (limiar 0,55): `base × #B8AEDB` (≈ 75% de valor, puxado para lilás)
  - sombra (limiar 0,30 ou sombra projetada): `base × #5B4E8C`
  - Suavidade da borda: 0,02 (personagem), 0,04 (ambiente).
- **Luzes pontuais em degraus:** atenuação quantizada em 2 níveis (100% e 45%) e corte em 0. Isso desenha a **poça de luz** com borda de cartoon no chão.
- **Especular:** desligado no ambiente. Nos personagens, só em cabelo, óculos e pupila: mancha dura branca (Blinn, limiar 0,92).
- **Rim:** só em personagens e vilão (ver §6). A Entidade usa rim emissivo forte (ela é feita de rim).
- **Sombra projetada:** recebe a sombra da lua com a mesma tinta violeta. Atores projetam sombra; móveis pequenos não (custo e ruído).
- **Contorno:**

| | Método | Largura (1080p) | Cor |
|---|---|---|---|
| Atores | Casca invertida no próprio shader | 2,5 px, constante na tela | `base × 0,25` (contorno colorido escuro, não preto) |
| O Mascarado | Casca invertida | 3,5 px | `#0B0A12` |
| A Entidade | sem contorno | — | (rim emissivo) |
| Ambiente | Detecção de bordas em tela cheia (profundidade + normais) | 1 px | `#1A1420` a 60%, apagando com a névoa |
| Ator selecionado | Mesma casca, troca de parâmetro | 3,5 px | `#F5C542` (sem pacote extra) |

---

## 8. Abordagem técnica recomendada (grátis)

### 8.1 Unity 6.3 / URP 17

**Shader toon: HLSL escrito à mão (`HT_Toon.shader`).** Recomendado.
- Passes: `UniversalForward` (rampa + loop de luzes adicionais compatível com Forward+, `MixFog`), `Outline` com `LightMode = "SRPDefaultUnlit"` (Cull Front, extrusão em clip space para largura constante em pixels), `ShadowCaster`, `DepthOnly` e **`DepthNormals`**. Este último é obrigatório para a detecção de bordas e o SSAO enxergarem o objeto.
- Por que HLSL e não Shader Graph: (1) Shader Graph não faz o passe extra da casca no mesmo material; (2) agentes escrevem e validam texto com facilidade; (3) controle total da rampa e da atenuação em degraus.
- Base de estudo: *UnityURPToonLitShaderExample* (NiloCat, MIT), que tem exatamente essa estrutura de casca + toon lit, e a página da Unity "Use shadows in a custom URP shader". Não copiar o shader: escrever o nosso, enxuto.
- **Plano B (se o Gabriel quiser ajustar visualmente):** Shader Graph + sub-graphs de *URP_ShaderGraphCustomLighting* (Cyanilux, MIT, URP 17.1+/Unity 6000.1+) com a casca via **Render Objects** renderer feature (material override com Cull Front, filtrado por layer).
- **Riscos:** a API da ShaderLibrary do URP muda entre versões (keywords de Forward+ e sombras). Validar no Frame Debugger se o SRP Batcher continua ativo com o passe extra. A casca abre falhas em quinas duras, por isso os personagens são exportados com normais suaves (ver §8.2). Se for preciso, gerar as normais suavizadas num canal UV (*OutlineNormalSmoother*/*OutlineSmoothNormalsGenerator*).

**Contorno do ambiente:** renderer feature própria com **Render Graph (`RecordRenderGraph`)**, seguindo o tutorial de Alexander Ameye (Unity 6, Roberts Cross com profundidade, normais e luminância).
- Ligar *Depth Texture* e o pré-passe de normais (o feature pede `ConfigureInput(Normal)`).
- Multiplicar a borda pelo fator de névoa para o fundo não virar ruído.
- **Atenção:** com a teleobjetiva, as diferenças de profundidade ficam comprimidas. O limiar precisa ser em profundidade linear e relativo à distância.
- Alternativa pronta: *Unity-URP-Outline* (CristianQiu, MIT, exige 6000.3+, 4 camadas de rendering layer). Serve para destaque por seleção, mas tem largura única e não funciona com animação por vértice. Opcional, não necessário.

**Névoa e atmosfera:**
- Etapa 1: névoa built-in (custo zero, já sai do `MixFog` no shader) com os valores da §6.
- Etapa 2: **URPFog** (meryuhi, MIT, URP 17, Render Graph, modo Height + noise, ≈0,6–0,8 ms a 1440p).
- Névoa rasteira e raios de luz: malhas e partículas simples com shader unlit transparente próprio.
- **Descartado:** *StylizedGradient-Fog-UnityURP*. Exige Compatibility Mode, que no 6.3 fica escondido atrás do define `URP_COMPATIBILITY_MODE` e tem remoção prevista. Regra geral: **qualquer renderer feature precisa ser nativo de Render Graph.**

**Também descartados:** *Unity Toon Shader* (licença Unity Companion, ainda preview, pesado, voltado para anime). *Delt06 urp-toon-shader* (feito em 2021.3/URP 12, sem manutenção). *ChiliMilk URP_Toon* (foco em anime/Genshin, sem versão declarada).

### 8.2 Blender (bpy 5.x): pipeline de personagem

Scripts headless: `blender -b -P build_actor.py -- --actor nerd --out Assets/_HorrorTycoon/Art/Actors/`.

1. **Blockout:** esqueleto de vértices (bacia → coluna → pescoço; ombro → cotovelo → mão; quadril → joelho → pé) com **Skin Modifier**, cujos raios por vértice vêm de uma tabela por ator (§4.3). Aplicar e depois **Subdivision nível 1** (2 só no close). O Skin gera quads limpos, bons para deformar e para o toon.
2. **Cabeça, cabelo, olhos, sobrancelhas e adereços:** malhas separadas (UV sphere/metaball convertida, deformadas por escala e lattice). As peças rígidas recebem 100% de peso no osso `Head` (ou `Spine2`), sem pintar peso.
3. **Topologia toon-friendly:** 3–6 mil triângulos por ator, tudo **smooth shaded** (evita falhas na casca), sem quinas duras no corpo, loops extras em ombro, cotovelo e joelho.
4. **Cor:** sem textura pintada. Uma **paleta-textura compartilhada** de 16×16 px (`HT_Palette.png`, cada célula um hex deste guia) e UVs das faces mapeadas na célula. Resultado: 1 material por ator, cor alterável num lugar só, nada de UV unwrap artístico.
5. **Shape keys:** só `Grito` (squash/stretch do corpo) e `Piscar`. O rosto usa peças e sprite de boca (§4.2), o que é mais robusto que blendshape gerado por script.
6. **Rig:** armature criada por script com **nomes compatíveis com o Humanoid da Unity** (Hips, Spine, Chest, Neck, Head, Left/RightUpperArm, LowerArm, Hand, UpperLeg, LowerLeg, Foot). Pose em T. `parent_set(type='ARMATURE_AUTO')` para pesos automáticos (bone heat, inalterado no 5.x).
   - API 5.0: a seleção de bones mudou (`pose.bones[i].select`). Conferir scripts antigos.
7. **Transformações:** origem em (0,0,0) **entre os pés**, personagem **olhando para −Y no Blender** (vira **+Z na Unity**). Aplicar rotação e escala (`transform_apply`) antes de exportar. Unidade métrica, Unit Scale 1,0.
8. **Exportar FBX** (o exportador continua em Python no 5.x):
   `export_scene.fbx(use_selection=True, object_types={'ARMATURE','MESH'}, apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y', bake_space_transform=False, use_mesh_modifiers=True, mesh_smooth_type='FACE', add_leaf_bones=False, use_armature_deform_only=True, bake_anim=False)`
9. **Importar na Unity:** Scale Factor 1, Convert Units ligado, **Bake Axis Conversion** desligado (testado na Unity 6.3: ligado, o personagem fica virado para −Z), Rig Humanoid (*Create From This Model*), Import BlendShapes ligado, Materials *None*. O material `HT_Toon` é atribuído por script.
   - Verificar: altura no Inspector = 1,75 m e transform raiz sem rotação.

**Riscos do Blender:** pesos automáticos falham em malhas com partes soltas (por isso as peças rígidas vão 100% num osso). O Humanoid exige hierarquia correta (Hips como raiz da cadeia). Animações ficam fora do escopo desta v1: começar com squash/stretch procedural na Unity e clipes simples gerados por bpy.

---

## 9. Próximos passos sugeridos (para o Gabriel decidir)

1. Aprovar ou ajustar pilares, paleta e proporções (§1, §3, §4).
2. *Visual Lab:* uma cena com 1 cômodo + 1 ator-cápsula testando `HT_Toon` + casca + névoa + pós da §6. Comparar com o perfil atual via screenshot.
3. Primeiro ator no Blender (sugestão: **Nerd**, que tem o gancho mais forte no close) e validação da importação.
4. Contorno por detecção de bordas no ambiente, só depois do passo 2 aprovado.

---

## Fontes

- Luigi's Mansion 3, entrevista com os devs (tom, reações): https://nintendoeverything.com/luigis-mansion-3-devs-on-how-the-hotel-concept-was-decided-on-what-makes-luigi-compelling-to-play-as-more/ · https://en.wikipedia.org/wiki/Luigi%27s_Mansion_3 · https://nextlevelgames.com/games/luigis-mansion-3/
- Ghost Master (medo, crença e loucura dos mortais): https://en.wikipedia.org/wiki/Ghost_Master · https://www.gamespot.com/reviews/ghost-master-review/1900-6074005/
- Two Point Hospital (visual de massinha feita à mão): https://mcvuk.com/development-news/when-we-made-two-point-hospital/
- Cult of the Lamb (fofo × horror): https://www.dreadcentral.com/horror-gaming/488375/finding-the-balance-between-cute-and-horrifying-cult-of-the-lamb-interview-with-james-pairmain-art-director/ · https://www.gamedeveloper.com/design/interview-corralling-the-inherent-cuteness-of-cult-of-the-lamb
- Toon lit + casca em URP (MIT): https://github.com/ColinLeung-NiloCat/UnityURPToonLitShaderExample
- Shader Graph custom lighting (MIT, URP 17.1+): https://github.com/Cyanilux/URP_ShaderGraphCustomLighting
- Sombras em shader URP custom (Unity 6): https://docs.unity3d.com/6000.0/Documentation/Manual/urp/use-built-in-shader-methods-shadows.html
- Detecção de bordas (Unity 6, Render Graph): https://ameye.dev/notes/edge-detection-outlines/ · https://danielilett.com/2023-03-21-tut7-1-fullscreen-outlines/ · https://www.cyanilux.com/tutorials/custom-renderer-features/
- Contorno por rendering layers (MIT, 6000.3+): https://github.com/CristianQiu/Unity-URP-Outline
- Normais suavizadas para casca: https://github.com/JasonMa0012/OutlineNormalSmoother · https://github.com/AleFeng/OutlineSmoothNormalsGenerator
- Full Screen Pass Renderer Feature: https://docs.unity3d.com/6000.1/Documentation/Manual/urp/renderer-features/renderer-feature-full-screen-pass.html
- Compatibility Mode escondido no 6.3: https://discussions.unity.com/t/render-graph-updates-in-unity-6-3/1668122 · https://docs.unity3d.com/6000.3/Documentation/Manual/UpgradeGuideUnity63.html
- Névoa URP 17 (MIT): https://github.com/meryuhi/URPFog
- Descartados: https://github.com/Josephy5/StylizedGradient-Fog-UnityURP · https://github.com/unity-technologies/com.unity.toonshader · https://github.com/Delt06/urp-toon-shader · https://github.com/ChiliMilk/URP_Toon
- Blender: Skin Modifier https://docs.blender.org/manual/en/latest/modeling/modifiers/generate/skin.html · Blender 5 para jogos https://app.cinevva.com/guides/blender-5-for-game-artists · API 5.0 https://developer.blender.org/docs/release_notes/5.0/python_api/ · Exportar para Unity https://app.cinevva.com/guides/blender-to-unity-export-checklist · https://www.immersivelimit.com/tutorials/blender-to-unity-export-correct-scale-rotation

> Observações sem fonte direta (análise própria a partir das obras): traços visuais de Luigi's Mansion 3 (cor-tema por andar, fantasmas luminosos), dos fundos de Scooby-Doo, de Monster House, Don't Starve e Little Nightmares. Não consegui acessar a análise técnica da GameXplain/Brainchild sobre LM3 (403) nem o README completo do NiloCat. Validar a compatibilidade do SRP Batcher com o passe `SRPDefaultUnlit` direto no Frame Debugger.
