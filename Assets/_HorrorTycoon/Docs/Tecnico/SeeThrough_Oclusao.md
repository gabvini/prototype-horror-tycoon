# Buraco de visão (see-through) — referência técnica

> Substitui o corte estilo Sims (`WallCutaway`, agora legado e desligado). Pesquisa e motivos: `Pesquisa_Oclusao_Camera.md`.

**Ajuste de 06/10/2026:** a faixa do topo das paredes só fica de pé com a câmera alta (y ≥ 6 m, sem close). Em planos baixos ela aparecia como barras soltas no quadro; agora o buraco atravessa a parede inteira (`SeeThroughTargets.keepTopMinCameraY`).


As paredes agora ficam **sempre em altura cheia**. Quando um ator fica escondido atrás de parede (do ponto de vista da câmera principal), abre-se um **buraco em tela** em volta dele, com borda de pincel (ruído), um contorno de tinta fino e um leve escurecimento na borda. Só some o que está **na frente** do ator (teste de profundidade em vista); o piso nunca é cortado e uma **faixa no topo das paredes** (acima de 2,62 m) fica de pé, para a planta continuar legível.

### Comportamento por modo

| Modo | O que acontece |
|---|---|
| Visão geral (iso) | Buraco só para atores realmente escondidos (raio 1,1 m; o selecionado 1,4 m). Ator à vista = parede intacta. |
| Cinemática | O diretor agora **penaliza** pontos de vista com parede no caminho (não descarta: se só houver esses, o buraco resolve). Em teleobjetiva o buraco fica grande e parece um "íris". |
| Close-up / foco no ator | Alvo de foco com raio 1,6 m; atravessa duas paredes se precisar. |
| Monitor do diretor (RT) e Scene View | **Sem buracos** (o shader só corta na câmera principal do tipo Game). |

O raio abre como íris (0,15 s, com leve "overshoot") e fecha em 0,35 s; um ator precisa ficar visível por 0,25 s antes de o buraco fechar (sem piscar ao passar por batentes).

### Arquivos

| Arquivo | O que faz |
|---|---|
| `Art/Shaders/HT_Toon.shader` | Palavra-chave `_SEETHROUGH` + `Mantém o topo`. Mesmo recorte em ForwardToon, Contorno, DepthOnly e DepthNormals (o contorno e o SSAO/contorno de borda somem junto). **ShadowCaster não corta** (a sombra da parede continua). Arrays globais, compatível com SRP Batcher. |
| `Scripts/Camera/SeeThroughTargets.cs` | Fica em `GameSystems`. Junta atores vivos + `SeeThroughSubject`, faz o raycast de oclusão (3 pontos: pés/peito/cabeça, ≥2 bloqueados = escondido), anima o raio e envia até 8 alvos por quadro. Também tem `SeeThroughTargets.Raycast` (clique através do buraco). |
| `Scripts/Camera/SeeThroughSubject.cs` | Gancho para alvos extras. `isVillain = true` → **não** abre buraco (o vilão continua escondido). |
| `Scripts/Rooms/WallCutaway.cs` | **Legado.** O rebaixamento estilo Sims fica desligado (`WallCutaway.LegacyLowering = false`). Componente e API de enfeites mantidos (servem de "etiqueta" de parede para o raycast). |
| `Scripts/Art/ToonMaterials.cs` | `SetSeeThrough(mat, on)` e `SetSeeThroughKeepTop(mat, keep)`. |
| `Scripts/Editor/Art/HTVisualSetup.cs` | `ApplySeeThroughFlags()` liga o recorte nos materiais da casa (paredes, batentes, beiral, janelas, porta, chaminé, colunas, luminárias). Beiral e topo da chaminé **não** mantêm o topo. |
| `Scripts/Rooms/FurnitureKit.cs`, `Scripts/Rooms/Building/HouseBuilder.cs` | Móveis e a casa procedural (P2) recebem a flag; pisos, caminho, deque e fundação nunca. |
| `Scripts/Run/RunPresenter.cs`, `Scripts/Camera/ActorFocusController.cs` | Clique/hover usam `SeeThroughTargets.Raycast` (ignora parede dentro de buraco aberto). |
| `Scripts/Camera/CinematicDirector.cs` | Penalidade de −2 na nota de pontos de vista bloqueados por parede. |

### Como ajustar (Inspector do `SeeThroughTargets` em GameSystems)

- **Raios**: `radius` 1,1 · `selectedRadius` 1,4 · `closeRadius` 1,6 (m, no mundo). `minRadiusPx` 60 garante buraco visível de longe.
- **Tempos**: `openTime` 0,15 · `closeTime` 0,35 · `holdTime` 0,25 · `checkInterval` 0,1.
- **Oclusão**: `checkHeights` (0,35 / 1,0 / 1,55) e `minBlockedPoints` 2.
- **Visual**: `edgeNoise` 0,12 e `noiseScale` 3,5 (borda de pincel), `inkColor` `#1A1420` e `inkWidthPx` 3, `darkWidthPx` 28 e `darkStrength` 0,35.
- **Profundidade**: `depthBias` 0,3 m (o que estiver a menos disso do ator não é cortado), `keepAboveY` 2,62 m (faixa do topo).
- **Ligar/desligar tudo**: `SeeThroughTargets.GlobalEnabled`. Voltar ao rebaixamento antigo: `WallCutaway.LegacyLowering = true` (e desligar o see-through).
- **Novo material que deve abrir buraco**: marcar "See-through" no material (ou chamar `ToonMaterials.SetSeeThrough`). Nunca em piso/chão.

### Problemas conhecidos (see-through)

- `keepAboveY` supõe paredes de 2,7 m. Se a altura de parede do kit mudar, ajustar o valor.
- Árvores/arbustos do cenário não têm collider de parede: não abrem buraco (um close pode enquadrar folhagem por um instante).
- Todos os móveis têm a flag; um móvel alto só abre buraco se uma **parede** também bloquear (a oclusão só por móvel não é detectada).
- O clique através do buraco usa o círculo sem o ruído da borda (diferença de poucos pixels na borda).
- Sem silhueta "raio-X" para oclusão parcial; não foi necessário nos testes.

Prévias: `Art/Previews/Unity/20_…` a `26_…` (sem oclusão, buraco P0, monitor sem buraco, cinemática, close P2, buraco com faixa do topo P2, cinemática + monitor P2).
