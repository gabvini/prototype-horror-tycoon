# Pesquisa — Oclusão de câmera (paredes × atores)

> Contexto: Unity 6.3, URP 17 (Render Graph), casa térrea em escala real (paredes 2,7 m, portas 0,9–1 m, sem teto), shader toon `HT_Toon` + contorno por detecção de bordas no cenário + casca invertida nos atores (doc 08). Hoje o `WallCutaway` abaixa para 0,35 m as paredes entre câmera e `CameraFocus` (docs 05/06). O problema: a casa perde a leitura de onde estão as paredes.
> Objetivo: substituir por um **buraco de visão estilo Baldur's Gate 3** (parede inteira, só abre em volta do ator quando ele está escondido) e usar a linguagem de câmera de terror a favor.
> Status: **pesquisa + proposta**. Nada aqui foi implementado. Decisões em aberto marcadas com ❓.

---

## 1. Como o BG3 faz

| Aspecto | O que se sabe | Confiança |
|---|---|---|
| Forma | Recorte em volta do personagem, com borda quebrada por **ruído** (não um círculo liso). Recriações públicas usam *sphere mask* em volta do ator + textura de ruído tileável na borda. | Alta (recriação de B. Sullivan, 80.lv / ArtStation) |
| Quando dispara | Só quando há geometria entre câmera e personagem. Recriação: *spherecast* do ator até a câmera, com atraso (não todo frame); só o raio/transição é interpolado por frame. | Alta (recriação) |
| O que é cortado | Só o que está **na frente** do ator. Chão onde ele pisa nunca é cortado (chão e paredes em materiais separados). Sombras das paredes **continuam** (passe de sombra não recorta). | Alta (recriação + issue thirdfold) |
| Clique | O recorte também vale para o **mouse**: dá para clicar/andar até o chão visível dentro do buraco. | Alta (Sullivan, 80.lv) |
| Telhados / andares | Ao entrar num prédio, telhado e andares de cima somem/esmaecem. | Média (observação de gameplay; sem fonte técnica oficial) |
| Vários personagens | Cada membro do grupo tem o próprio recorte. Uma implementação "BG3-like" open source usa até 4 cápsulas (peito + chão do ator, raio ~0,6 célula). | Média |

**Resumo:** é um *alpha clip* no shader do cenário, com máscara esférica/cápsula por personagem, teste "está na frente do ator?", borda com ruído, sombra preservada. Não há transparência real (sem ordenação), então profundidade e contornos continuam funcionando.

---

## 2. Outras referências

| Jogo | Técnica de oclusão | O que levar |
|---|---|---|
| **Divinity: Original Sin 2** | Telhado some ao entrar; **destaque/silhueta de personagens** ligável (tecla `~`, opção "Highlight Characters", configurável por modo). | Silhueta dos atores como opção (acessibilidade/legibilidade). |
| **Diablo III / IV** | Peças de parede entre câmera e herói somem/esmaecem; herói atrás de obstáculo aparece como **silhueta**. *(observação)* | Silhueta para o caso "atrás de móvel", onde abrir buraco não compensa. |
| **XCOM 2** | Corte de prédio por **andar** + paredes cortadas perto da unidade. O patch 1 reduziu obstruções e fez prédios **não** cortarem ao trocar de unidade (Tab) — o corte "piscando" incomodava. | Não precisamos de andares. Lição: **histerese** — corte não pode pular a cada troca de foco. |
| **The Sims 4** | *Walls up / cutaway / down*. No cutaway, paredes perto da câmera viram tocos, mas o **topo da parede** e o padrão continuam visíveis, e o modo é **escolha do jogador**. | Por que o nosso ficou ruim: (1) toco de 0,35 m apaga a pista de altura; (2) nossa câmera **gira sozinha** (`AutoDrift` 4°/s), então o conjunto de paredes baixas muda o tempo todo; (3) o jogador não escolheu nada. |
| **Shadow Tactics / Desperados III** | Áreas de cone de visão sobre geometria ganharam **hachura** para continuar legíveis quando o contraste some; bordas duras, sem gradiente. Personagens ocluídos com silhueta colorida *(observação)*. | **Hachura de tinta** no lugar de dither Bayer: combina com o toon. |
| **Pillars of Eternity / Tyranny** | Fundos pré-renderizados com profundidade; personagens atrás do cenário compostos pela profundidade e mostrados com contorno/silhueta *(observação para a silhueta)*. | Reforça silhueta como solução para oclusão parcial. |
| **Hades / Transistor** | Câmera fixa; objetos de primeiro plano esmaecem e personagens atrás deles ganham contorno *(observação)*. | Fade só de objetos **muito perto da câmera**. |
| **Luigi's Mansion 3** | Câmera semi-fixa tipo "casa de bonecas": a parede da frente do cômodo simplesmente não é mostrada *(observação)*. | Tom mais próximo do nosso. Variante a testar: só a **fachada externa** voltada para a câmera vira "fantasma". |
| **Little Nightmares** | Câmera lateral baixa; personagem pequeno; **silhuetas escuras em primeiro plano** emolduram a cena. | Nos planos de cinema, parede da frente vira **moldura escura**, não buraco. |
| **What Remains of Edith Finch** | Props em camadas de profundidade; primeiro plano parcialmente visível = sensação de **espiar**. | Planos "pela porta" com batente em primeiro plano. |
| **Resident Evil (clássicos/remakes), Alone in the Dark, Silent Hill** | Ângulos fixos escondem a ameaça fora de quadro; câmera alta no canto deixa o personagem pequeno e exposto; câmera baixa no corredor esconde o fim dele. | Escolher planos que **escondem** o vilão em vez de revelar tudo. |
| **Until Dawn / The Quarry** | Planos semi-fixos com primeiro plano (galhos, janelas) e planos "voyeur", como se alguém observasse *(observação)*. | Plano "POV do vilão": pela janela, primeiro plano escuro. |
| **Alan Wake 2, RE2/3/4 remake** | Câmera de ombro; colisão empurra a câmera para frente. | Útil só no close (`Track`). Cinemachine 3 tem `CinemachineDeoccluder`, mas ele muda o enquadramento. |

Não verificados (sem fonte técnica encontrada, não incluídos para não chutar): Ghost Master, Disco Elysium, Wasteland 3, Two Point Hospital, Darkest Dungeon (2D, não se aplica), Phasmophobia (1ª pessoa, não se aplica).

### Técnicas destiladas

| Técnica | Para nós |
|---|---|
| (a) Buraco em tela com teste de profundidade contra o alvo | **Base da solução.** Overview e close. |
| (b) "Paredes fantasma" (hachura/dither + topo e contorno mantidos) | Só como **variante a testar** no overview, para a fachada voltada à câmera. |
| (c) Silhueta / raio-X dos atores atrás de paredes/móveis | Complemento barato (Render Objects). Útil atrás de móveis. |
| (d) Teto de altura só nas paredes mais próximas da câmera | **Descartar.** É o mesmo problema do Sims-style em versão menor. |
| (e) Oclusor como moldura (primeiro plano escuro) | **Padrão dos planos de cinema.** |

---

## 3. Solução recomendada por modo de câmera

| Modo (doc 06) | Recomendação | Parâmetros iniciais |
|---|---|---|
| **Overview** (`CM_Iso`, ~50°, 13–21 m, gira sozinho) | Paredes **sempre inteiras**. Buraco em volta de cada ator que estiver atrás de parede. Topo da parede **nunca** recortado (fica a "linha de planta"). Silhueta de raio-X para ator atrás de móvel. | raio 1,1 m (selecionado 1,4 m), centro no peito (+1,0 m) |
| **Cinematic** (`CM_Cine`, teleobjetiva de fora, pela porta) | **Sem buraco por padrão.** Validação do plano passa a tratar parede como obstáculo real (fora do `foregroundAllowance`): só sobram planos pela porta/janela, que é justamente a linguagem de terror. Paredes perto da lente escurecem para silhueta (moldura). Buraco só como recurso raro ("olho mágico"). | escurecer geometria a < 2,5 m da câmera, até 85% da cor de tinta |
| **Close / Track** (câmera atrás do ator, 2–4 m) | Buraco maior + **fade de proximidade**: geometria a < 1 m da câmera some (dither/hachura). Evitar `CinemachineDeoccluder` (muda o quadro). | raio 1,6 m; fade de 0,4 a 1,0 m da câmera |
| **DoorFocus** (`CM_Porta`) | A parede da porta tem que ficar de pé. O teste de profundidade já resolve: o foco é a própria porta, então a parede dela não está "na frente". | viés de profundidade 0,3 m |
| **Monitor** (`MonitorCam`, ortográfica de cima) | Sem buraco (não tem teto, já vê tudo). Desligar por câmera. | global `_HT_SeeThroughOn = 0` nessa câmera |

**Isso substitui o `WallCutaway`?** Sim, no overview/close. ❓ Decisão sua: manter o `WallCutaway` como opção de jogador ("paredes baixas", estilo Sims) ou removê-lo.

---

## 4. Desenho do shader (`HT_Toon`, ramo de cenário)

### Entradas

| Nome | Tipo | Escopo | Conteúdo |
|---|---|---|---|
| `_HT_SeeThroughTargets[8]` | `float4[]` | **global** | xyz = posição mundo do alvo (peito); w = raio em metros × quantidade animada (0 = fechado) |
| `_HT_SeeThroughCount` | `int` | global | quantos alvos válidos |
| `_HT_SeeThroughOn` | `float` | global, **por câmera** | 0/1, setado em `RenderPipelineManager.beginCameraRendering` (monitor = 0) |
| `_SeeThroughEnabled` | `float` | material (`UnityPerMaterial`) | 0 no chão, 1 em paredes/móveis altos |
| `_SeeThroughEdgeNoise`, `_SeeThroughNoiseScale`, `_SeeThroughInkWidth`, `_SeeThroughInkColor`, `_SeeThroughDepthBias` | material | `UnityPerMaterial` | ver tabela de parâmetros |

Globais não entram no `UnityPerMaterial`, então o **SRP Batcher continua valendo**. Cuidado: o tamanho de um array global é fixado no **primeiro** `Shader.SetGlobalVectorArray`; sempre enviar o array com 8 posições.

### Lógica por fragmento

1. Se `_HT_SeeThroughOn × _SeeThroughEnabled = 0`, sai.
2. Se a normal do fragmento aponta para cima (`normalWS.y > 0,7`): **nunca** recorta. É o topo da parede, que desenha a planta da casa.
3. Para cada alvo: projeta o centro com a matriz da **câmera atual** (funciona em qualquer câmera, sem C# por câmera). Raio na tela: `r = R × P[1][1] / profundidadeDoAlvo` (perspectiva) ou `R × P[1][1]` (ortográfica). Distância em NDC com x multiplicado pelo aspecto (`_ScreenParams.x / _ScreenParams.y`) para ficar redondo.
4. **Teste de profundidade:** só recorta se a profundidade de olho do fragmento for menor que a do alvo menos `_DepthBias`. Sem isso o círculo abriria também a parede **atrás** do ator (vira "holofote").
5. Borda: distância + ruído (textura de ruído em espaço de tela ancorada no centro do alvo, para não "nadar" quando a câmera gira) → **corte duro** (`clip`). Faixa logo fora do corte pinta a cor de tinta (pincel).
6. Transição: abrir/fechar animando o **raio** (efeito *íris*), não alfa. Assim não precisa de dither.

### Comportamento por passe

| Passe | Recorta? | Por quê |
|---|---|---|
| `ForwardToon` | Sim | é o que se vê |
| `DepthOnly` | **Sim, idêntico** | com *depth priming* o passe de cor usa `ZTest Equal`: se a profundidade não bater, aparecem buracos ou parede fantasma |
| `DepthNormals` | **Sim, idêntico** | SSAO e o `HT Edge Outline` leem daqui. Bônus: a borda do buraco vira descontinuidade de profundidade e ganha **contorno de tinta de graça** |
| `ShadowCaster` | **Não** | as matrizes ali são da luz (o teste daria errado) e a sombra da parede mantém a luz do cômodo coerente |
| `Outline` (casca invertida) | Não se aplica | paredes não usam casca. Se algum móvel ganhar casca, o passe precisa do mesmo recorte |

**Por que corte duro e não dither (como Daniel Ilett e a maioria dos tutoriais):** dither na textura de profundidade faz o detector de bordas desenhar pontinhos em toda a área pontilhada, e com TAA o padrão treme ou deixa rastro. Corte duro com borda de ruído + faixa de tinta combina com o visual de tinta e evita os dois problemas. Dither/hachura fica só para a variante "parede fantasma" e para o fade de proximidade. Se usar, prefira hachura em espaço de tela e, com TAA ligado, varie o padrão por frame.

### Lado C# (`SeeThroughTargets`, sugestão)

- Junta até 8 alvos: atores vivos, com prioridade para o selecionado. ❓ O **vilão** entra? Recomendo **não**: vilão escondido atrás da parede é tensão, não bug.
- Oclusão: `Physics.RaycastNonAlloc` da câmera até 3 pontos do ator (cabeça, peito, pés) na layer `Walls`. Ocluído se ≥ 2 forem bloqueados. A cada 0,1 s, não todo frame (como a recriação do BG3).
- Animação: abre em ~0,15 s, fecha em ~0,35 s, segura 0,25 s antes de fechar (histerese, lição do XCOM 2).
- Mesmo sem C# o shader já só corta o que está na frente do alvo. O C# serve para a transição suave e para não abrir buraco por causa de quina que mal encosta.
- **Clique através do buraco:** o raycast do mouse acerta o collider da parede. Repetir a conta do buraco em C# e, se o ponto estiver dentro de um buraco ativo, ignorar o hit e seguir com o raio.

### Parâmetros iniciais (todos precisam de playtest)

| Parâmetro | Padrão | Comparação / motivo |
|---|---|---|
| Raio overview | 1,1 m | ator ~1,7 m: cobre do joelho à cabeça; topo da parede (2,7 m) fica de pé |
| Raio selecionado | 1,4 m | destaque, como o contorno amarelo do doc 08 |
| Raio close | 1,6 m | câmera perto: precisa abrir mais |
| Centro | pivô + 1,0 m | peito |
| `_DepthBias` | 0,3 m | ator encostado na parede de trás não abre a parede de trás |
| Ruído na borda | 12% do raio | quebra o círculo "de compasso" |
| Escala do ruído | 6 ciclos por raio | |
| Largura da tinta | 3 px a 1080p | entre a casca dos atores (2,5) e o selecionado (3,5) |
| Cor da tinta | a mesma do `HT Edge Outline` | consistência |
| Abre / fecha / segura | 0,15 / 0,35 / 0,25 s | |
| Máx. alvos | 8 | 4 atores + folga |
| Fade de proximidade | 0,4 → 1,0 m | só close/cinema |

---

## 5. Ideias de terror (cartunesco mas assustador)

1. **Buraco íris** (*iris-out* de desenho animado): abre e fecha como diafragma, borda de pincel. Cartunesco no movimento, sinistro na forma.
2. **Escurecer em volta do buraco:** um anel de vinheta escura na parede logo fora da borda, como se o buraco fosse feito por uma lanterna. Fica no próprio shader da parede, sem pós extra.
3. **Raio ligado ao perigo:** com o Perigo subindo, o buraco do ator **encolhe** (o diretor perde a visão). Liga com Tensão × Impacto. ❓ Decisão de design: mexe em sistema consolidado, precisa de validação no GDD.
4. **Olho mágico / fechadura:** em planos especiais (POV do vilão), o buraco ganha forma de fechadura ou de olho mágico e o resto do quadro escurece. Uso raro, para ter impacto.
5. **Vidro fosco:** o vilão atrás da parede aparece só como **mancha escura** (silhueta raio-X borrada/hachurada), nunca nítido. Revela sem mostrar.
6. **Moldura de primeiro plano:** nos planos de cinema, batente e parede perto da lente quase pretos. Ator emoldurado pela porta (Little Nightmares, Edith Finch).
7. **Atraso proposital:** o buraco abre com 0,15 s de atraso. Um susto pode acontecer **antes** de o jogador conseguir ver.

---

## 6. Riscos

| Risco | Mitigação |
|---|---|
| Passes de profundidade divergem da cor (depth priming) → buracos/artefatos | mesma função de recorte incluída em `ForwardToon`, `DepthOnly` e `DepthNormals` |
| Detector de bordas desenha ruído no dither | corte duro (sem dither) no buraco |
| Teste de profundidade falha com o ator **dentro** da parede (atravessando porta) | viés de 0,3 m + o batente da porta não marcado como recortável (❓ testar) |
| Mouse acerta parede "invisível" | filtro do buraco no raycast de clique |
| Câmera girando sozinha faz buracos abrirem/fecharem sem parar | histerese + checagem a cada 0,1 s |
| Móveis altos (estante) escondem ator sem ser parede | recortável por material + silhueta raio-X como rede de segurança |
| `Render Objects` (raio-X) + Render Graph | o feature nativo funciona no Render Graph do URP 17; os exemplos do GitHub são pré-Render Graph (2022.3), usar só como referência |
| Tamanho do array global travado | sempre enviar 8 entradas |
| Overview a 21 m: buraco de 1,1 m fica pequeno na tela | raio mínimo em pixels (ex.: 60 px a 1080p) |

---

## 7. Implementações abertas para estudar

| Projeto | O que é | Licença |
|---|---|---|
| [daniel-ilett/shaders-wall-cutout](https://github.com/daniel-ilett/shaders-wall-cutout) + [tutorial](https://danielilett.com/2021-03-19-tut5-15-wall-cutout/) | Shader Graph URP: círculo em tela, correção de aspecto, smoothstep + dither na borda | MIT |
| [slagatoras/shaders-wall-cutout](https://github.com/slagatoras/shaders-wall-cutout) | fork do anterior (Unity 2020.2, URP 10) | MIT |
| [daniel-ilett/dither-transparency-urp](https://github.com/daniel-ilett/dither-transparency-urp) + [tutorial](https://danielilett.com/2020-04-19-tut5-5-urp-dither-transparency/) | transparência por dither com clip no opaco | MIT |
| [gkjohnson/unity-dithered-transparency-shader](https://github.com/gkjohnson/unity-dithered-transparency-shader) | dither com clip (Built-in, referência da matriz) | MIT |
| [Clept0/XRay-Rendering-URP](https://github.com/Clept0/XRay-Rendering-URP) | raio-X com Render Objects (2022.3) | sem licença declarada: **não copiar** |
| [Unity — Render Objects: desenhar atrás (Unity 6)](https://docs.unity3d.com/6000.1/Documentation/Manual/urp/renderer-features/how-to-custom-effect-render-objects.html) | silhueta com `Depth Test = Greater` + material override | doc oficial |
| [Daniel Ilett — Stealth Vision URP](https://danielilett.com/2023-04-07-tut6-4-stealth-vision/) | raio-X / visão através de paredes | tutorial |
| [BenjaminBenetti/tut PR #581](https://github.com/BenjaminBenetti/tut/pull/581) | "fantasma" de prédios em volta de unidades: raio **e** mais perto da câmera que a unidade, Bayer 4×4 com descarte, centros vindos só de unidades visíveis | ver repo |
| [thirdfold issue #283](https://github.com/tougenrip/thirdfold/issues/283) | especificação "BG3": cápsula câmera→ator, 4 alvos, ruído em mundo, sombras mantidas, 0,1–0,3 ms | ver repo |

---

## Fontes

- 80.lv — [Artist Recreated Baldur's Gate 3 Occlusion Cutout Effect](https://80.lv/articles/artist-recreated-baldur-s-gate-3-occlusion-cutout-effect)
- ArtStation — [Occlusion Cutout Shader, inspired by BG3 (B. Sullivan)](https://www.artstation.com/artwork/WXVnyD)
- GitHub — [thirdfold #283: dither occluders (BG3)](https://github.com/tougenrip/thirdfold/issues/283) · [tut PR #581: building ghosting](https://github.com/BenjaminBenetti/tut/pull/581)
- Larian Forums — [DOS2: NPC outlines/silhouettes toggle](https://forums.larian.com/ubbthreads.php?ubb=showflat&Number=630572)
- Tom's Hardware — [XCOM 2 patch: reduced camera obstructions](https://www.tomshardware.com/news/xcom-2-first-major-patch,31384.html)
- EA Forums — [Sims 4: Walls up / Cutaway / Down](https://forums.ea.com/discussions/the-sims-4-general-discussion-en/re-walls-up-%E2%AC%86%EF%B8%8F-cutaway-%E2%86%98%EF%B8%8Fdown-%E2%AC%87%EF%B8%8F---how-do-you-play-%F0%9F%8F%A1/11246537)
- Game Developer — [Shadow Tactics: dynamic detection deep dive](https://www.gamedeveloper.com/design/game-design-deep-dive-dynamic-detection-in-i-shadow-tactics-i-)
- Projection Space — [Pillars of Eternity's rendering techniques](https://projectionspace.wordpress.com/2016/05/06/pillars-of-eternitys-rendering-techniques/)
- Game Developer — [The camera angle as an expressive resource](https://www.gamedeveloper.com/design/the-camera-angle-as-an-expressive-resource-and-narrative-booster-in-video-games) (Little Nightmares, Edith Finch)
- New Game Plus — [The cinematography of horror games](https://newgameplus.co.uk/2018/05/22/cinematography-of-horror-games/)
- Infinite Frontiers — [Why fixed-camera horror is returning](https://www.infinitefrontiers.org.uk/why-fixed-camera-horror-is-returning-to-games/)
- Game Developer — [Creating walls to see the player through](https://www.gamedeveloper.com/design/creating-walls-to-see-the-player-through)
- Devlogs — [Penitence: fading walls](https://tonycooper.itch.io/penitence/devlog/385977/fading-walls) · [SPACEBLOOD: wall occlusion](https://jankbot.itch.io/spaceblood/devlog/848814/wall-occlusion)

Itens marcados *(observação)* vêm de gameplay conhecido, sem fonte técnica oficial: confirmar com vídeo antes de copiar.
