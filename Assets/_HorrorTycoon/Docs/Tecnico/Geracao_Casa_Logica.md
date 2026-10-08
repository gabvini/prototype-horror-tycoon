# Geração da Casa — Lógica e Dados (referência técnica)

> Metade LÓGICA da Proposta_Geracao_Casa (v1). A montagem 3D (HouseBuilder, cena P2, câmera/HUD) é outra etapa e consome o contrato abaixo.
> Planta fixa do P0 continua idêntica: só muda algo quando a run recebe um `HouseGenDef`.

## 1. Arquivos

| Arquivo | O quê |
|---|---|
| `Rooms/RoomDef.cs` | `enum SpaceKind { Room, Social, Corridor }` + campos de geração: `kind`, `size` (m, padrão 4x4), `allowRotation`, `weight`, `maxPerRun`, `required`, `interiorPrefab` (só arte). `SetupGen(...)`. |
| `Rooms/Generation/HouseGenDef.cs` | Asset de parâmetros (Create > Horror Tycoon > Geração de Casa). |
| `Rooms/Generation/HouseLayout.cs` | Resultado: `HouseSpace`, `HouseConnection`, `HouseWall`, `HouseLayout` (+ `Validate`, `TryGetSharedWall`). |
| `Rooms/Generation/HouseGenerator.cs` | `HouseGenerator.Generate(def, roomPool, rng)`. C# puro, determinístico. |
| `Rooms/Generation/HouseGenStats.cs` | Simulação em lote (N casas → resumo). |
| `Rooms/HouseMap.cs` | Grafo com pesos (Dijkstra). Construtor antigo intacto + `HouseMap(layout)` e `HouseMap(layout, portaOverride, passagemOverride)`. |
| `Run/FilmRun.cs` | `FilmRun(content, seed, houseGen)`; propriedade `Layout`. |
| `Run/RunModels.cs` | `RoomRunState`: `Kind`, `Space`, `IsExplorable`, `IsDestination`. |
| `Rooms/FurnitureKit.cs` | `FurnitureStyle` ganhou `Hall` e `Dining` NO FIM (ainda sem móveis no kit). |
| `Editor/ContentBuilder.cs` | Cria convivências + `Casa_Padrao`; menu **Horror Tycoon/Geração/Estatísticas da casa (500 casas)**. |
| `Tests/HouseGenTests.cs` | 11 testes da geração (EditMode). |

## 2. Tipos de espaço

| Kind | Encontro | Atores param | `IsHub` | Descoberto no início |
|---|---|---|---|---|
| Room | sim (1 por ato, 1ª visita) | sim | false | não |
| Social | nunca | sim | true | sim |
| Corridor | nunca | **não** (`CanMove` = false), só atravessa | true | sim |

Planta fixa: o corredor central vira `Kind = Social` (continua destino, como hoje). RoomDefs `Social` no catálogo são ignorados como cômodo.

## 3. Algoritmo (por tentativa)

Grade de 1 m dentro de `bounds` (padrão 22×20). x = leste, y = norte. Porta da frente na borda **sul** (y = 0).

1. **Convivência inicial**: 1ª `Social` com `required` do `socialPool` (senão a 1ª Social). Colada em y = 0, perto do meio. Porta da frente na parede sul dela.
2. **Corredores** (`corridorDef`, largura `corridorWidth`): 1º segmento sai da inicial (norte preferido). Cada novo segmento: segue reto, vira 90° (`turnChance`, sai do bloco final) ou ramifica do meio de um segmento antigo (`branchChance`). Comprimento `segmentLength`. Folga: corredor não encosta em nada além do pai, e fica a ≥ (menor lado de sala do pool) de outros corredores, para caber sala entre eles.
3. **Convivências extras** (`extraSocials`): presas a corredores, ligação **Opening**.
4. **Salas**: obrigatórias primeiro, depois sorteio por `weight` respeitando `maxPerRun`; quantidade `roomCount`, limitada pela capacidade do pool. Encaixe = todas as posições livres coladas a corredores/convivências com parede em comum ≥ `minSharedWall` (com rotação se permitido); sorteia uma. Com `roomInsideRoomChance`, tenta primeiro encaixar numa sala comum (sala **funda**, `IsDeep`, profundidade máx. 1).
5. **Paredes**: cada borda de cada espaço é percorrida metro a metro e dividida por vizinho.
6. **Validação**: dentro dos limites, sem sobreposição, tudo conectado ao inicial, cada vão dentro da parede em comum; metas = obrigatórias + mínimo de salas + mínimo de corredores/convivências. Falhou → nova tentativa com sub-seed `GameRandom.Mix(base, tentativa)`. Após `maxAttempts`, aceita a melhor (sempre estruturalmente válida, `UsedFallback = true`).

Portas: largura `doorWidth` (1 m), centro em múltiplos de 0,5 m, a ≥ `doorCornerMargin` dos cantos da parede em comum. Passagens (Opening): largura = min(parede em comum, largura do corredor).

## 4. Custos e movimento

- Ligação Door custa `doorCost` (1); Opening custa `openingCost` (**0**, decisão do Gabriel: parâmetro).
- `HouseMap.Doors(a, b)` = menor caminho com pesos. `MoveCost` = ceil(portas / `DoorsPerStep`), **mínimo 1 ao mudar de lugar** (passagens podem somar 0 portas; ex.: Hall → Sala de jantar).
- `Map.Hub` = `Map.StartIndex` = convivência inicial; `Map.EntranceIndex` = espaço da porta da frente (hoje o mesmo). Outside = -1 continua existindo (porta da frente, custo 1).
- A casa usa um fluxo aleatório próprio (`GameRandom.Mix(seed, sal)`): gerar a casa não muda os sorteios de encontros da run.

## 5. Contrato para a montagem 3D

```
FilmRun.Layout                       // null = planta fixa
layout.Bounds                        // Vector2Int (m), origem (0,0)
layout.Spaces[i]                     // i == índice em FilmRun.Rooms
  .Kind / .Def / .Rect (RectInt, m) / .Rotated / .IsDeep / .ParentIndex / .Center
layout.Connections[k]
  .A, .B (B = -1: porta da frente) / .Type (Door|Opening)
  .Position (Vector2, centro do vão SOBRE a linha da parede) / .HorizontalWall / .Width / .Cost
layout.Walls[w]                      // cobre TODA borda de todo espaço, sem repetir
  .A, .B (-1 = parede externa) / .From, .To (Vector2Int) / .Horizontal / .ConnectionIndex (-1 = sólida)
layout.StartSpaceIndex, layout.FrontDoorIndex / layout.FrontDoor
layout.ConnectionsOf(i), layout.WallsOf(i), HouseLayout.TryGetSharedWall(a, b, ...)
```

Mundo 3D sugerido: `(x, 0, y) * escala + origem`. Parede com `ConnectionIndex >= 0`: abrir vão de `Width` centrado em `Position`. Espaços vazios dentro do retângulo são "lado de fora" (B = -1).

## 6. Conteúdo (Data/P1)

- `Salas/Conv_HallDeEntrada` — **Hall de entrada**, Social, 5×4, obrigatório = espaço inicial (estilo `Hall`).
- `Salas/Conv_SalaDeJantar` — **Sala de jantar**, Social, 4×4 (estilo `Dining`).
- `Salas/Conv_SalaDeTV` — **Sala de TV**, Social, 5×4 (estilo `Living`).
- `Geracao/Casa_Padrao` — `HouseGenDef` com `corridorDef = Sala_Corredor` e as 3 convivências.
- "Sala de estar" continua **Sala** com encontros (P0). `Sala_Corredor` passou a `kind = Corridor` (P0 não muda).
- Tamanhos aplicados só onde estava 4×4: Cozinha 4×5, Sala de estar 5×4, Banheiro 3×3, Porão 4×5 (Quarto e Sótão 4×4).
- `Casa_Padrao` NÃO está no Catálogo: a cena da casa procedural recebe por campo próprio.
- Sala nova criada pelo menu já nasce `Room` 4×4, peso 1, máx. 1 — ao entrar no Catálogo, entra no pool.

## 7. Números (500 casas, conteúdo real, padrão)

- 0 inválidas, 0 fallback, 58 re-tentativas (máx. 3 numa casa); ~150 ms para 500 casas.
- Salas/casa: sempre 6 (pool atual = 6). Simulando pool de 10: 6→198, 7→170, 8→132.
- ~2,9 segmentos de corredor, ~0,5 convivência extra por casa; 21% das salas fundas.
- Portas início→sala: média 1,21 (1–2). Com passagem = 1: 2,81 (1–7).
- Portas sala→sala: média 2,22 (1–4) (P0 = 2). Com passagem = 1: 3,52 (1–8).

## 8. Em aberto / próximos passos

- Validar com o Gabriel: custo mínimo 1 para mudar de lugar; convivências/corredores já visíveis no início.
- Montagem 3D, `Hall`/`Dining` no `FurnitureKit` e cena `P2_CasaProcedural`: ver `Geracao_Casa_Montagem3D.md`.
- Fora da v1: andares, salas com regra de posição, casa que muda entre atos.
