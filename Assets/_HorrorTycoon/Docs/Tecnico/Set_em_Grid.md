# Set em grid: a casa cresce peça a peça

> M1 do `Docs/Design/GDD_Base.md` (10/10/2026). Substitui a "casa por escolha" de 09/10 (que partia de corredores gerados).
> Liga com `HouseGenDef.growByDraft` (ligado no `Casa_Padrao`). Desligado = casa gerada inteira como antes. P0 não muda.

## 0. Medidas

- **Célula = 2 m** (`HouseGenDef.gridCell`). Terreno do `Casa_Padrao`: **48 × 40 m = 24 × 20 células** (`bounds`).
- Tamanhos das peças são arredondados para múltiplos da célula. Conteúdo atual: Hall 4×3, Sala de jantar 3×3, Sala de TV 4×3,
  Cozinha 3×4, Sala de estar 4×3, **Banheiro 2×2** (era 5×5 m), Quarto 3×3, Porão 3×4, Sótão 3×3, **Corredor 1×4** (2×8 m).
- Portas ficam **no meio de uma célula**; peças começam em múltiplos da célula. Vão 1,2 m, margem 0,4 m.

## 1. Como joga

1. A run começa **só com o Hall**, na frente do terreno, com a porta da frente e uma **porta fechada** em cada lado livre
   (norte, leste, oeste). Do lado de fora de cada uma, uma **marcação de fita no chão**. A **grade do set** aparece no terreno (tecla **G** liga/desliga).
2. Clique na porta (ou na fita) → card **PORTA FECHADA** → **QUEM VAI?** (custa 1 cena, `exploreActionCost`).
3. Tela **MONTE O SET**: até `draftOptions` (3) **peças** que cabem ali: salas, convivências e corredores (no máximo 1 corredor por oferta).
   Cada carta mostra o tipo, o tamanho em células, a descrição e etiquetas. "voltar" desiste sem gastar nada.
4. A peça **se monta** (piso, paredes de fora subindo, móveis caindo, lâmpada acendendo) e ganha **portas novas** (uma por lado, na
   célula mais ao meio). Uma porta que daria em algo já montado vira **passagem direta** entre os dois.
5. Depois:
   - **Sala**: o ator entra e explora (encontro, plots, ferramentas);
   - **Convivência**: o ator vai para lá; a cena passa (tique da casa);
   - **Corredor**: só passagem, o ator fica onde está; a cena passa.
6. A câmera e o monitor do diretor **acompanham o set crescendo** (a área publicada é só o que já foi montado).

## 2. Regras da oferta

- Peças: salas do pool (Catálogo + `extraRoomPool`) + convivências (`socialPool`) + o `corridorDef`. Peso > 0, sem `excludeFromDraft`,
  abaixo do `maxPerRun` (corredor: sem limite).
- **Cabe**: colada na parede da porta, alinhada ao grid, cobrindo o vão + margem dos dois lados, dentro do terreno e sem passar por cima de
  nada. Gira 90° se `allowRotation`; respeita `doorSides` (a peça precisa ter porta do lado que encosta). Fica a posição mais centrada na porta.
- **Zona** (`draftZones`, só salas): a porta está na frente / meio / fundos do terreno (terços da profundidade). Salas da zona têm
  preferência; se nenhuma da zona couber, vale qualquer uma.
- **Lacradas** (`startsSealed`, o Porão) só entram na oferta depois que um plot liberar (`PlotDef.unlockRoom`).
- A oferta de cada porta é sorteada **uma vez** (fechar e abrir não troca). Se alguma deixou de caber, sai; se nenhuma sobrar, sorteia de novo.
- Uma sala nova que cobre outra porta para o vazio: se o vão cabe na parede em comum, a porta passa a levar à sala nova; senão vira parede.

## 3. Arquivos

| Arquivo | O quê |
|---|---|
| `Rooms/Generation/HouseDraft.cs` | C# puro. `StartLayout` (só o Hall + portas), `AddSites` (portas de uma peça), `TryPlace` (alinhado ao grid), `Eligible`, `RollOffer`, `AddRoom`, `DraftRules`. |
| `Rooms/Generation/HouseLayout.cs` | `HouseDoorSite` + `DoorSiteState`; `Sites`, `GrowsByDraft`; `HouseWall.SiteIndices`. O cálculo das paredes (`ComputeWalls`) mudou do gerador para cá (a casa recalcula a cada sala) e `IsFree`. |
| `Rooms/Generation/HouseGenDef.cs` | `growByDraft`, `gridCell`, `draftOptions`. |
| `Run/FilmRun.Draft.cs` | **Novo**. `DoorSites`, `SiteHasRoom`, `CanDraft`, `DraftCost`, `OpenDraft` (oferta), `Draft` (sala nasce + ator entra), evento `RoomAdded`. Fluxo aleatório próprio. |
| `Run/FilmRun.cs` | `growByDraft` → `HouseDraft.StartLayout` (sem gerador); `Map` é refeito a cada peça; `Move` interno sem registrar ação. |
| `Run/FilmRun.Build.cs` | Plot que libera sala lacrada ainda não montada: ela entra na oferta. |
| `Run/RunReplay.cs` | Ações `draft-offer <porta>` e `draft <porta> <opção> <ator>`. |
| `Rooms/Building/HouseBuilder.cs` | **Grade do set** (`BuildGridOverlay`, `SetGridVisible`); área publicada = peças montadas (`PublishBounds`). Paredes viraram **grupos por trecho** (chave = trecho + vãos): `SyncWalls` só refaz o que mudou. `AddRoom(i)`, `Assemble(i)` (animação), folha da porta + fita (`DoorLeaf`, `SyncSites`), `SiteAt`, `SetSiteHighlight`, `SetSiteAvailable`. Janela/beiral sorteados por trecho (estável). Sem névoa nas salas da casa por escolha. |
| `Rooms/Building/HouseWorldMap.cs` | `Gaps` (vários vãos por trecho: portas para o vazio); origem do mundo fixa no terreno inteiro quando a casa cresce. |
| `Rooms/Building/DraftSiteMarker.cs` | **Novo**. Marca a folha da porta (o clique acha a porta por ele). |
| `Rooms/RoomAnchor.cs` | `AddVisuals` (janelas novas depois do Awake), `SetLampPower` (apaga durante a montagem, acende no tranco). |
| `Run/RunPresenter.cs` | Fase `DraftChoice`; `CardSite`, `HoveredSite`, `OpenDraft`, `ChooseDraft`, `DraftSequence`, `AssemblyPoint`; recalcula o NavMesh a cada sala. |
| `Camera/IsoCameraRig.cs` | O pivô só pula para o centro da casa na 1ª vez (o set cresce sem a câmera pular). |
| `Camera/CinematicDirector.cs` | Modo `Build` (plano de grua da sala se montando) e plano da porta para o vazio no card. |
| `UI/Panels/RoomCardPanel.cs`, `UI/Panels/HudModals.cs`, `UI/HudRoot.cs` | Card da porta e tela "Escolha a sala". |
| `UI/RunHud.cs` | HUD antiga (F1): versão simples do card e da escolha. |
| `Tests/HouseDraftTests.cs` | **Novo**: 7 testes EditMode. |

## 4. Números (400 runs simuladas, Ato 1 = 8 cenas, sempre abrindo portas)

- ~8 peças montadas no Ato 1 (9 espaços com o Hall): 49% salas, 31% corredores, 20% convivências.
- Ofertas: 85% com 3 opções, 5% com 2, 10% com 1 (o pool de salas acaba: 5 salas sem lacre).
- Sobram ~12 portas abertas com peça possível no fim: espaço de sobra no terreno.
- 0 plantas inválidas, replay idêntico em todas.

## 5. Como testar no Unity

1. Abrir o projeto (compila sozinho). **Test Runner > EditMode**: os 54 antigos + 7 de `HouseDraftTests`.
2. **Reconstruir a cena**: menu **Horror Tycoon > Construir Cena Casa Procedural** (o terreno cresceu para 48 × 40 m; o cenário em volta
   precisa abrir espaço). Depois Play.
3. Conferir:
   - [ ] Começa só com o Hall; 3 portas fechadas com fita em X; marcação de fita no chão; grade do set no terreno (G esconde).
   - [ ] Passar o mouse na porta/fita acende a fita; clicar abre o card PORTA FECHADA.
   - [ ] Escolher ator → tela MONTE O SET com até 3 cartas (tipo + células); "voltar" fecha sem gastar cena.
   - [ ] Corredor: monta e o ator fica; o relatório da cena aparece. Convivência: o ator vai para lá.
   - [ ] A peça nova traz portas novas; porta que daria em peça vizinha vira passagem.
   - [ ] Escolher → a sala se monta (piso, paredes subindo, móveis caindo, lâmpada acendendo) e o ator entra.
   - [ ] NavMesh: o ator anda até dentro da sala nova; ninguém sai pela porta fechada.
   - [ ] Com o monitor (M): plano de grua durante a montagem.
   - [ ] Janelas da sala nova acendem; luz não vaza para a sala vizinha.
4. Para a casa antiga (tudo pronto no começo): desligar `growByDraft` no `Casa_Padrao`.

## 6. Limitações / próximos passos

- Uma porta por lado em cada peça (na célula do meio); os lados com porta vêm do `doorSides` do RoomDef.
- Áreas externas (floresta, cemitério) ainda não existem como peças.
- O NavMesh é recalculado inteiro a cada sala (`BuildNavMesh`): pode dar um engasgo pequeno.
- Sem som nem poeira na montagem.
- Câmera por tipo de cena (exploração, susto, morte...) ainda é a próxima etapa.
- Verificado fora da Unity: compilação contra as DLLs de referência da Unity (com stubs de Cinemachine/Input System/URP/AI Navigation)
  e a lógica (`HouseDraft`, `FilmRun.Draft`, replay) rodando de verdade nos 61 testes. **A parte 3D (montagem, animação, câmera, HUD)
  não foi vista rodando**: precisa do checklist acima.
