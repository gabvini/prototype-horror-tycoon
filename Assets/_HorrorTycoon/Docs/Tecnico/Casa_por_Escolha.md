# Casa por escolha: a casa cresce porta a porta

> Pedido do Gabriel (09/10/2026): "igual ao Blue Prince, mas sem o grid. Você não vê todas as salas da casa, só as portas ligadas ao
> corredor. Clica, escolhe, e ela se monta. Você vê a casa crescer, o espaço aumentar."
> Liga com `HouseGenDef.growByDraft` (ligado no `Casa_Padrao`). Desligado = casa gerada inteira como antes. P0 não muda.

## 1. Como joga

1. A run começa só com o **Hall, os corredores e as convivências**. Nas paredes de fora deles há **portas fechadas** (folha de madeira com
   fita em X) e, do lado de fora, uma **marcação de fita no chão**: é ali que o cômodo vai nascer.
2. Clique na porta (ou na fita) → card **PORTA FECHADA** com **QUEM VAI?** (custa o mesmo que explorar uma sala, `exploreActionCost`).
3. Escolha o ator → tela **ESCOLHA A SALA**: até `draftOptions` (3) salas que **cabem** ali, com a cor do piso, tamanho em metros,
   descrição, etiquetas (Palco, Plot, Escura, Trancado) e a primeira dica. "voltar" desiste sem gastar nada.
4. Escolheu → a sala **se monta**: o piso abre do centro, as paredes de fora sobem do chão uma depois da outra (a partir da porta),
   os móveis caem no lugar com quique e a lâmpada desce do forro e acende no tranco. Com o monitor aberto a câmera faz um plano de grua
   em volta da sala; sem monitor, a visão da casa desliza até ela.
5. O ator entra: é uma **exploração** normal (encontro, plots da sala, ferramentas). Andar até a sala depois é grátis como sempre.

## 2. Regras da oferta

- Só salas do pool (Catálogo + `extraRoomPool`), tipo Sala, com peso > 0, sem `excludeFromDraft`, abaixo do `maxPerRun`.
- **Cabe**: colada na parede da porta, cobrindo o vão + `doorCornerMargin` dos dois lados, dentro do terreno e sem encostar por cima de
  nada. Gira 90° se `allowRotation`; respeita `doorSides` (sala precisa ter porta do lado do corredor). Fica a posição mais centrada na porta.
- **Zona** (`draftZones`): a porta está na frente / meio / fundos do terreno (terços da profundidade). Salas da zona têm preferência;
  se nenhuma da zona couber, vale qualquer uma.
- **Lacradas** (`startsSealed`, o Porão) só entram na oferta depois que um plot liberar (`PlotDef.unlockRoom`).
- A oferta de cada porta é sorteada **uma vez** (fechar e abrir não troca). Se alguma deixou de caber, sai; se nenhuma sobrar, sorteia de novo.
- Uma sala nova que cobre outra porta para o vazio: se o vão cabe na parede em comum, a porta passa a levar à sala nova; senão vira parede.

## 3. Arquivos

| Arquivo | O quê |
|---|---|
| `Rooms/Generation/HouseDraft.cs` | **Novo**, C# puro. `Skeleton` (casa gerada → esqueleto + portas para o vazio onde estavam as salas), `TryPlace`, `Eligible`, `RollOffer`, `AddRoom`, `Landing`. |
| `Rooms/Generation/HouseLayout.cs` | `HouseDoorSite` + `DoorSiteState`; `Sites`, `GrowsByDraft`; `HouseWall.SiteIndices`. O cálculo das paredes (`ComputeWalls`) mudou do gerador para cá (a casa recalcula a cada sala) e `IsFree`. |
| `Rooms/Generation/HouseGenDef.cs` | `growByDraft`, `draftOptions`. |
| `Run/FilmRun.Draft.cs` | **Novo**. `DoorSites`, `SiteHasRoom`, `CanDraft`, `DraftCost`, `OpenDraft` (oferta), `Draft` (sala nasce + ator entra), evento `RoomAdded`. Fluxo aleatório próprio. |
| `Run/FilmRun.cs` | Constrói o esqueleto quando `growByDraft`; `Map` é refeito a cada sala; `Move` interno sem registrar ação (o `draft` já registra). |
| `Run/FilmRun.Build.cs` | Plot que libera sala lacrada ainda não montada: ela entra na oferta. |
| `Run/RunReplay.cs` | Ações `draft-offer <porta>` e `draft <porta> <opção> <ator>`. |
| `Rooms/Building/HouseBuilder.cs` | Paredes viraram **grupos por trecho** (chave = trecho + vãos): `SyncWalls` só refaz o que mudou. `AddRoom(i)`, `Assemble(i)` (animação), folha da porta + fita (`DoorLeaf`, `SyncSites`), `SiteAt`, `SetSiteHighlight`, `SetSiteAvailable`. Janela/beiral sorteados por trecho (estável). Sem névoa nas salas da casa por escolha. |
| `Rooms/Building/HouseWorldMap.cs` | `Gaps` (vários vãos por trecho: portas para o vazio); origem do mundo fixa no terreno inteiro quando a casa cresce. |
| `Rooms/Building/DraftSiteMarker.cs` | **Novo**. Marca a folha da porta (o clique acha a porta por ele). |
| `Rooms/RoomAnchor.cs` | `AddVisuals` (janelas novas depois do Awake), `SetLampPower` (apaga durante a montagem, acende no tranco). |
| `Run/RunPresenter.cs` | Fase `DraftChoice`; `CardSite`, `HoveredSite`, `OpenDraft`, `ChooseDraft`, `DraftSequence`, `AssemblyPoint`; recalcula o NavMesh a cada sala. |
| `Camera/CinematicDirector.cs` | Modo `Build` (plano de grua da sala se montando) e plano da porta para o vazio no card. |
| `UI/Panels/RoomCardPanel.cs`, `UI/Panels/HudModals.cs`, `UI/HudRoot.cs` | Card da porta e tela "Escolha a sala". |
| `UI/RunHud.cs` | HUD antiga (F1): versão simples do card e da escolha. |
| `Tests/HouseDraftTests.cs` | **Novo**: 7 testes EditMode. |

## 4. Números (400 casas simuladas, medidas da escala ×1,5, pool atual de 6 salas)

- Portas para o vazio por casa: média 4,7 (2 a 6). Salas abertas por run: ~4,3 (o pool tem 5 salas sem lacre).
- Ofertas: 69% com 3 opções, 20% com 2, 12% com 1. **Com 6 salas no pool, as últimas portas oferecem pouca escolha**:
  para a escolha "1 de 3" render até o fim, o pool precisa de mais salas (ou `maxPerRun` > 1 em algumas).
- 0 plantas inválidas, replay idêntico em todas.

## 5. Como testar no Unity

1. Abrir o projeto (compila sozinho). **Test Runner > EditMode**: os 54 antigos + 7 de `HouseDraftTests`.
2. Abrir `Scenes/P2_CasaProcedural` e dar Play (não precisa reconstruir a cena: o `Casa_Padrao` já está com `growByDraft` ligado).
3. Conferir:
   - [ ] Começa só com Hall + corredores; portas fechadas com fita em X; marcação de fita no chão do lado de fora.
   - [ ] Passar o mouse na porta/fita acende a fita; clicar abre o card PORTA FECHADA.
   - [ ] Escolher ator → tela ESCOLHA A SALA com até 3 cartas; "voltar" fecha sem gastar cena.
   - [ ] Escolher → a sala se monta (piso, paredes subindo, móveis caindo, lâmpada acendendo) e o ator entra.
   - [ ] NavMesh: o ator anda até dentro da sala nova; ninguém sai pela porta fechada.
   - [ ] Com o monitor (M): plano de grua durante a montagem.
   - [ ] Janelas da sala nova acendem; luz não vaza para a sala vizinha.
4. Para a casa antiga (tudo pronto no começo): desligar `growByDraft` no `Casa_Padrao`.

## 6. Limitações / próximos passos

- Só corredores e convivências têm portas para o vazio (salas não abrem para outras salas novas).
- O NavMesh é recalculado inteiro a cada sala (`BuildNavMesh`): pode dar um engasgo pequeno.
- Sem som nem poeira na montagem.
- Câmera por tipo de cena (exploração, susto, morte...) ainda é a próxima etapa.
- Verificado fora da Unity: compilação contra as DLLs de referência da Unity (com stubs de Cinemachine/Input System/URP/AI Navigation)
  e a lógica (`HouseDraft`, `FilmRun.Draft`, replay) rodando de verdade nos 61 testes. **A parte 3D (montagem, animação, câmera, HUD)
  não foi vista rodando**: precisa do checklist acima.
