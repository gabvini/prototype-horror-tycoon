# Geração da Casa — Montagem 3D (referência técnica)

> Segunda metade da Proposta_Geracao_Casa (v1): pega o `HouseLayout` (ver `Geracao_Casa_Logica.md` §5) e monta a casa em 3D **em tempo de execução**, numa cena separada (`P2_CasaProcedural`).
> A cena `P0_Greybox` (casa fixa) continua idêntica: nada muda quando o `RunPresenter` não tem `HouseGenDef`.
> A arte atual é **provisória**: tudo o que a montagem desenha vem de um asset de kit (`HouseArtKit`), do `FurnitureKit` ou do `RoomDef.interiorPrefab`, para a arte oficial entrar trocando assets.

## 1. Arquivos

| Arquivo | O quê |
|---|---|
| `Rooms/Building/HouseWorldMap.cs` | **Novo.** Régua planta → mundo (C# puro, testável): `ToWorld`, pegada no mundo, porta principal de cada espaço (`PrimaryConnection`), direção para fora (`Outward`), giro do interior (`FurnitureYaw`, `AuthoredYaw`, `LocalSize`), pedaços de parede com vão/verga (`Pieces`, `GapOf`), grupos de luz (`LightGroups`). |
| `Rooms/Building/HouseBuilder.cs` | **Novo.** `MonoBehaviour` que monta a casa: espaços (RoomAnchor, fundação, piso, interior, névoa, lâmpada), paredes (vãos, vergas, batentes, `WallCutaway`), janelas, beiral, varanda, caminho. Publica `HouseBounds`. |
| `Rooms/Building/HouseArtKit.cs` | **Novo.** `ScriptableObject` com medidas, materiais, prefabs opcionais e opções da fachada/interiores. |
| `Rooms/HouseBounds.cs` | **Novo.** Onde a casa está no mundo (estático). Câmeras e monitor leem; sem valor = constantes do P0. |
| `Editor/Art/HouseArtKitSetup.cs` | **Novo.** Cria `Art/Kits/HouseArtKit_Provisorio.asset` com os mesmos materiais da casa fixa (não sobrescreve). |
| `Tests/HouseBuildTests.cs` | **Novo.** 6 testes EditMode (sem cena). |
| `Rooms/RoomAnchor.cs` | `InteriorOverride` (quem monta o interior no `Bind`; null = P0). |
| `Rooms/FurnitureKit.cs` | Móveis de `Hall` e `Dining`; `BuildPassage` (corredor da casa gerada). |
| `Run/RunPresenter.cs` | Campos opcionais `houseGen` e `houseBuilder`; monta a casa antes dos slots e do NavMesh; atores começam no espaço inicial. |
| `UI/RunHud.cs` | Card de corredor = "Passagem" sem "Quem vai?"; corredores gerados sem nome no mapa. `ShowsMoveButtons` / `ShowsLabel` (estáticos, testados). |
| `Camera/CinematicDirector.cs` | Centro e raio dos planos gerais vêm de `HouseBounds` (P0: igual). |
| `Camera/IsoCameraRig.cs` | Limites de pan e zoom máximo crescem com a casa; pivô vai para o centro da casa nova. |
| `Camera/DirectorMonitor.cs` | `FitToHouse`: ortográfica centrada e com tamanho para a pegada. |
| `Editor/SceneryBuilder.cs` | `SiteLayout` (o que o cenário evita); padrão = números antigos do P0; `ForHouse(...)` para a casa gerada. |
| `Editor/GreyboxSceneBuilder.cs` | `Build(bool)`; menu novo **Construir Cena Casa Procedural**. |
| `HorrorTycoon.asmdef` | + `Unity.RenderPipelines.Core.Runtime` e `Unity.RenderPipelines.Universal.Runtime` (camada de luz das lâmpadas em runtime). |

## 2. Como a montagem funciona

### 2.1 Ordem no `RunPresenter.Start` (só quando `houseGen != null`)

1. `Run = new FilmRun(content, seed, houseGen)` → gera `Run.Layout`.
2. `houseBuilder.Build(Run.Layout)` (procura um `HouseBuilder` na cena se o campo estiver vazio).
3. Liga os `RoomAnchor` (os mesmos de sempre, achados por `FindObjectsByType`; `SlotIndex` = índice do espaço) → `Bind` monta o interior.
4. `NavMeshSurface.BuildNavMesh()`.
5. Cada ator com `RoomIndex` válido é colocado num `NextStandPoint()` do espaço inicial, liga o agente e `ActorIdle.SetHome(centro do espaço)`.

Com `houseGen` vazio o caminho é o antigo, linha por linha.

### 2.2 Régua (planta → mundo)

- Planta: metros, x = leste, y = norte. Mundo: `(x + OriginX, 0, y + frontZ)`.
- `frontZ` (campo do `HouseBuilder`) = z da fachada da frente (linha y = 0, onde fica a porta da frente). A cena P2 usa `-bounds.y / 2` (= -10 com 22 × 20).
- `OriginX` centraliza a **pegada real** (união dos espaços) em x = 0. Como a pegada cabe no retângulo máximo, a casa sempre fica dentro de x ∈ [-11, 11], z ∈ [-10, 10] (o que o cenário evita).
- A porta da frente muda de lugar a cada run; o caminho de terra sai dela até o portão (`pathEnd`, padrão (0, -19)), em diagonal se precisar.

### 2.3 Por espaço

| Peça | Detalhe |
|---|---|
| Raiz `Espaco_NN (Nome)` | `RoomAnchor.Configure(índice, tamanho, piso, Interior, névoa, lâmpada)`. |
| Porta principal | Espaço inicial: porta da frente. Outros: ligação com `ParentIndex` (senão a primeira ligação). `ConfigureDoor(ponto, para fora)` = do espaço para o pai. Usada no card (plano da porta) e na convenção dos móveis. |
| Fundação | Placa sob o retângulo (+0,1 m de cada lado), sem colisor. |
| Piso | Cubo com colisor (clique + NavMesh). Cor do `RoomDef` via property block (o material fica claro). |
| Interior | Girado para a porta principal ficar no **+X local** (convenção do `FurnitureKit`), com o tamanho já girado. Ver §2.6. |
| Névoa | Só tipo **Sala** (convivência e corredor já começam descobertos). Mesmo visual da casa fixa: volume + 2 camadas + fita apagada, dissolve ao descobrir. |
| Lâmpada | Luz pontual sem sombra na camada do espaço (§2.5). Alcance = máx(kit, metade do lado maior + 2 m): corredor de 9 m ganha 6,5 m como o do P0. Corredor usa `corridorLampIntensity`. |

### 2.4 Paredes

- `layout.Walls`: cada trecho uma vez (externo ou compartilhado). `HouseWorldMap.Pieces` divide em pedaços:
  - **Porta** (`Door`): sólido | vão de `Width` | sólido + **verga** de `doorHeight` até `wallHeight` (2,1 → 2,7 m) + **batente** (dois montantes + travessa, como no P0).
  - **Passagem** (`Opening`): vão de **altura inteira, sem verga e sem batente** (decisão: lê como "não é porta", e o custo de passagem é diferente do de porta). Corredor↔corredor costuma abrir a parede inteira.
  - Trechos sólidos longos são divididos em pedaços de até `maxWallPiece` (4 m) para o corte "Sims" funcionar por pedaço.
- Paredes inteiras: `BoxCollider` (NavMesh), +meia espessura em cada ponta para fechar os cantos. Vergas e batentes: sem colisor.
- Todo pedaço (parede, verga, montante, travessa) tem `WallCutaway.Setup`. Enfeites (janela, beiral, varanda) são presos ao pedaço com `AddAttachment` e somem com o corte.
- Camadas de luz: externas = todas (recebem varanda/refletores); internas = todas menos Default (só lâmpadas).

### 2.5 Camadas de luz

As lâmpadas não têm sombra; no P0 cada slot tem uma rendering layer (bits 1..7) para a luz não vazar pela parede. A casa gerada tem até ~15 espaços, então usa os **mesmos 7 bits** como "cores": `LightGroups` dá grupos diferentes a espaços que se encostam (sempre que houver grupo livre) e evita repetir entre espaços a menos de `lampRange`. Os móveis herdam a camada do piso (`RoomAnchor.Bind`). O piso do espaço inicial também tem a camada Default (luz da varanda).

### 2.6 Interiores (móveis)

`RoomAnchor.Bind` chama `HouseBuilder.BuildInterior` (via `InteriorOverride`):

1. `RoomDef.interiorPrefab` preenchido → instancia o prefab (§3.2).
2. Corredor → `FurnitureKit.BuildPassage` (passadeira no sentido comprido; nada nas paredes, porque portas podem estar em qualquer ponto).
3. Senão → `FurnitureKit.Build(estilo do RoomDef, tamanho local)`.
4. **Portas livres** (`clearDoorways`): para cada ligação do espaço calcula a faixa na frente do vão (largura + 0,3 m, `doorwayClearDepth` para dentro). Testa o interior normal e **espelhado em Z** (a porta pode estar fora do meio da parede); fica com o que bloqueia menos e **remove** as peças que ainda bloqueiam. Peças rentes ao chão (tapetes, fita) ficam.

Estilos novos: `Hall` (tapete, capacho, aparador com abajur e espelho, banco, cabideiro com casaco, porta-guarda-chuva, relógio de pé, cadeira de diretor) e `Dining` (mesa posta para ninguém com 5 lugares, pratos e copos, castiçal aceso, cristaleira, quadro). Os dois respeitam a faixa da porta no +X.

### 2.7 Câmeras, monitor e cenário

- `HouseBounds` (pegada no mundo, porta da frente, `Version`) é publicado no fim do `Build` e limpo no `OnDestroy` do `HouseBuilder`. `Scale` = meia diagonal / 8,1 (a do P0), mínimo 1.
- `CinematicDirector`: centro = `HouseBounds.Center`; plano geral orbita a `24–30 × Scale` m com quadro `13→10 × Scale`; visão da casa afasta para `overviewDistance × Scale`. No P0, `Scale = 1` (mesmos números e mesma ordem de sorteios).
- `IsoCameraRig`: limites = máx(Inspector, pegada + 4,5 m dos lados / 6 m na frente / 4 m atrás); zoom máximo = máx(24, maior lado × 1,6); o pivô pula para o centro da casa nova uma vez.
- `DirectorMonitor.FitToHouse`: tamanho ortográfico = máx(profundidade/2 + 2,5, (largura/2 + 1,5) / aspecto), centro z − 1,5 (a mesma regra dá 8,5 e −1,5 para a casa 11 × 12 do P0).
- `SceneryBuilder.SiteLayout.ForHouse(meia largura, fachada, profundidade)`: mesmas folgas do P0 em volta do **retângulo máximo** (cerca 9 m à frente da fachada, faixa de arbustos a 1,7–2,9 m, árvores a partir de ~15,6 m do centro e longe do retângulo, esconderijos de câmera a 19–24 m, cemitério em x ≈ 17, abóboras perto do portão, quintal da frente livre em |x| < 8,25 para a varanda e o caminho, que mudam de lugar: a porta da frente cai entre x = −7 e +7). Névoa rasteira com "buraco" no retângulo máximo. Refletores e câmera de cinema fora do retângulo.

## 3. Pontos de troca de arte

### 3.1 `HouseArtKit` (asset `Art/Kits/HouseArtKit_Provisorio`)

| Grupo | Campos | Observação |
|---|---|---|
| Medidas | `wallHeight` 2,7 · `wallThickness` 0,12 · `doorHeight` 2,1 · `maxWallPiece` 4 | O gerador continua em metros; mudar medidas não muda regra. |
| Casa | `wallMaterial`, `exteriorWallMaterial`, `lintelMaterial`, `floorMaterial` (+ `room/social/corridorFloorMaterial`), `foundationMaterial`, `trimMaterial`, `eaveMaterial` | Paredes são **caixas** reescaladas em Y pelo corte: troque o MATERIAL (o shader recebe a altura cortada). Piso: a cor vem do `RoomDef`, deixe o material claro. |
| Névoa | `fogVolumeMaterial`, `fogLayerMaterial`, `fogTapeMaterial` | Precisam da propriedade `_Dissolve` (shader `HorrorTycoon/FX`) para o dissolve. |
| Lâmpada | intensidades, alcance, altura, `lampCord/Shade/Bulb/ShaftMaterial`, `lampShaftMesh` | Vidro/brilho: `_EmissionColor` (toon) ou `_BaseColor` (FX). |
| Janelas | `windowChance`, `windowFrame/Pane/BeamMaterial` | O vidro acende com `_EmissionColor` na cor da lâmpada. |
| Fachada | `eaves`, `porch`, `path`, `deck/post/path/porchFixtureMaterial`, luz da varanda | |
| Prefabs | `doorFramePrefab`, `windowPrefab`, `lampPrefab`, `porchPrefab` | Vazio = peça por código. Convenções abaixo. |
| Interiores | `clearDoorways`, `doorwayClearDepth` | |

Campos de material vazios caem num toon de cor chapada criado por código (um kit "em branco" também roda).

**Convenções dos prefabs**

- **Batente**: origem no chão, no centro do vão; X local ao longo da parede; feito para vão de **1 m** (a escala X é multiplicada pela largura). Preso à verga (some no corte).
- **Janela**: origem no centro da janela (montada a 1,5 m); **+Z local para FORA** da casa. Renderers com `Vidro` no nome acendem com o cômodo. Presa ao pedaço de parede.
- **Luminária**: origem no ponto da lâmpada (`lampHeight`). Todos os renderers viram "brilhos" (somem com o cômodo escuro, piscam junto). A `Light` continua sendo criada pelo builder.
- **Varanda**: origem no chão, no centro da porta da frente; **+Z local para a rua**. A luz da varanda continua sendo criada pelo builder.

### 3.2 `RoomDef.interiorPrefab`

- Interior de arte oficial por cômodo (substitui o `FurnitureKit` naquele cômodo).
- Referencial: centro do piso na origem, **tamanho local = `RoomDef.size`** (x = largura, z = profundidade). A porta principal vai para o **+X local**; quando a sala foi girada pelo gerador e isso não cabe, ela vai para o **+Z local**.
- Cada filho direto do prefab é um "móvel": os que ficarem na frente de qualquer porta/passagem são removidos (`clearDoorways`). Deixe móveis grandes encostados nos cantos.
- Sem colisores de preferência (o clique na sala acha o piso; os colisores de móveis entram no NavMesh só se o cômodo estiver descoberto no início).

### 3.3 `FurnitureKit`

Continua sendo o "móvel por código" (cada móvel é uma função). Trocar um estilo inteiro = preencher `interiorPrefab` nos `RoomDef` daquele estilo.

## 4. Como testar

1. **Horror Tycoon > Criar Conteúdo P1** (garante `Geracao/Casa_Padrao`).
2. **Horror Tycoon > Construir Cena Casa Procedural** → cria o kit (se faltar) e `Scenes/P2_CasaProcedural.unity` (entra no Build Settings).
3. Play. Cada Play = casa nova (seed aleatória). Para repetir uma casa: `RunPresenter.seed` (o log mostra a seed).
4. Testes: Test Runner > EditMode — `HouseBuildTests` (6) + os 18 antigos.
5. P0 continua em **Construir Cena Greybox** (nenhum campo novo preenchido).

## 5. Limitações conhecidas

- Paredes são caixas (o corte reescala em Y). Paredes "de verdade" com modelo exigem outro corte (shader com plano de corte).
- Interiores de prefab com colisor: o NavMesh é calculado com os cômodos não descobertos desligados.
- A porta da frente é sorteada (perto do meio da fachada); o caminho pode sair em diagonal até o portão.
- O cenário evita o retângulo máximo (22 × 20): casas pequenas ficam com quintal grande.
- Sem telhado (só beiral), como a casa fixa.
- Corredores não têm encontro nem são destino: o card só explica; o jogador manda atores para salas/convivências.
- Até 7 grupos de luz: com muitos espaços encostados num corredor, dois vizinhos podem dividir a camada (a luz de um vaza no outro).
- Enfeites presos a um pedaço só (a varanda segue a verga da porta da frente).

## 6. Checklist do lead no Unity

1. **Compilar**: o asmdef `HorrorTycoon` ganhou as refs de URP; conferir que `GetUniversalAdditionalLightData` resolve em runtime.
2. Rodar os testes EditMode (24 no total).
3. Construir a cena P2 e dar Play; conferir:
   - [ ] Console sem erros; log "Run iniciada" com "casa gerada: N salas".
   - [ ] Casa dentro da clareira (sem árvore/arbusto atravessando parede); varanda e caminho até o portão.
   - [ ] Atores começam dentro do Hall (não lá fora) e andam (NavMesh azul passa por todas as portas: Window > AI > Navigation, gizmos).
   - [ ] Portas com verga e batente; passagens sem verga; nenhuma parede fechando um vão; cantos fechados.
   - [ ] Corte "Sims": paredes da frente abaixam, vergas somem, janelas/beiral somem junto.
   - [ ] Salas não descobertas escuras com névoa; ao entrar, névoa dissolve, lâmpada "pega no tranco", janelas acendem.
   - [ ] Luz de uma sala não acende o piso da sala vizinha escura.
   - [ ] Móveis de Hall/Sala de jantar/TV aparecem; nada bloqueando portas; móveis dentro das paredes (rooms girados).
   - [ ] Card do corredor: "Passagem", sem "Quem vai?". Card do Hall/convivências: com botões e custo ≥ 1.
   - [ ] Monitor (M): casa inteira enquadrada e clicável. Visão iso: dá para ir até as bordas da casa. Planos gerais com a casa toda.
4. Abrir P0 e dar Play: deve estar idêntico (casa fixa, atores lá fora, monitor igual).

## 7. Em aberto (decisão do Gabriel)

- Passagem (Opening): vão inteiro sem verga (atual) ou arco largo com verga?
- Corredores sem nome no mapa: ok, ou mostrar "Corredor" ao passar o mouse?
- Janelas também nas convivências (atual) ou só nas salas?
- Remover móveis que bloqueiam portas (atual) vs. empurrá-los para o lado.
