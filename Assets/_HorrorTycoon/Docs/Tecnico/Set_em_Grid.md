# Set em grid: a casa cresce peça a peça

> M1 do `Docs/Design/GDD_Base.md` (10/10/2026). Substitui a "casa por escolha" de 09/10 (que partia de corredores gerados).
> Liga com `HouseGenDef.growByDraft` (ligado no `Casa_Padrao`). Desligado = casa gerada inteira como antes. P0 não muda.

## 0. Tabuleiro (como Blue Prince)

- **1 célula = 1 cômodo de 6 × 6 m** (`HouseGenDef.gridCell`). Terreno do `Casa_Padrao`: **7 × 6 células** (42 × 36 m, `bounds`).
- Quase tudo ocupa **1 × 1**; Sala de estar e Sala de TV ocupam **2 × 1** (12 × 6 m).
- **Portas sempre no meio do lado de uma célula**: peças vizinhas se encaixam sem sobrar espaço.
- **Lados com porta** (`RoomDef.doorSides`, sem giro; "sul" = a entrada). A peça é **girada** para uma porta dela cair na porta aberta.
  Entre os giros possíveis, o jogo escolhe o que deixa mais portas dando para células livres.

| Peça | Portas (sem giro) | Na prática |
|---|---|---|
| Hall (começo) | todas | 3 portas para o vazio + a da frente |
| Corredor reto / em L / em T / Cruzamento | N+S / S+L / S+L+O / todas | +1 / +1 / +2 / +3 portas novas |
| Banheiro, Porão | S | beco sem saída |
| Quarto | S+L | +1 |
| Sótão | S+N | +1 |
| Cozinha | S+N+L | +2 |
| Sala de jantar | S+L+O | +2 |
| Sala de estar, Sala de TV (2 × 1) | todas | +3 |

## 1. Como joga

1. A run começa **só com o Hall**, na célula do meio da fileira da frente, com a porta da frente e uma **porta fechada** em cada
   outro lado. Do lado de fora de cada uma, uma **marcação de fita no chão**. A **grade do set** (uma linha a cada 6 m) aparece no
   terreno; tecla **G** liga/desliga.
2. Clique na porta (ou na fita) → card **PORTA FECHADA** → **QUEM VAI?** (custa 1 cena, `exploreActionCost`).
3. Tela **MONTE O SET**: até `draftOptions` (3) **peças** que cabem ali: salas, convivências e corredores (no máximo 1 corredor por oferta).
   Cada carta mostra o tipo, quantas células ocupa e **quantas portas novas abre** ("+2 portas novas" / "Beco sem saída").
4. A peça **se monta** e as portas dela viram portas para o vazio. Porta que dá numa peça vizinha: vira passagem se a vizinha tiver
   porta ali; senão, parede.
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
| `Rooms/Generation/HouseDraft.cs` | C# puro. `StartLayout` (só o Hall + portas), `AddSites` / `DoorPos` / `SideHasDoor` (portas por lado com giro), `TryPlace` (célula + giro), `Eligible`, `RollOffer`, `AddRoom`, `DraftRules`. |
| `Rooms/Generation/HouseLayout.cs` | `HouseDoorSite` + `DoorSiteState`; `Sites`, `GrowsByDraft`; `HouseWall.SiteIndices`. O cálculo das paredes (`ComputeWalls`) mudou do gerador para cá (a casa recalcula a cada sala) e `IsFree`. |
| `Rooms/Generation/HouseGenDef.cs` | `growByDraft`, `gridCell`, `corridorPieces`, `draftOptions`. |
| `Data/P1/Salas/Sala_CorredorL`, `Sala_CorredorT`, `Sala_Cruzamento` | **Novos**: corredores com outras formas de porta. |
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

- ~8 peças montadas no Ato 1: 49% salas, 32% corredores, 19% convivências.
- Ofertas: 95% com 3 opções, 4% com 2, 1% com 1.
- No fim do Ato 1 sobram ~5,7 portas abertas por casa; ~0,8 porta por casa virou parede (beco).
- 0 plantas inválidas, replay idêntico em todas.

## 5. Como testar no Unity

1. Abrir o projeto (compila sozinho). **Test Runner > EditMode**: os 54 antigos + 7 de `HouseDraftTests`.
2. **Reconstruir a cena**: menu **Horror Tycoon > Construir Cena Casa Procedural** (o terreno mudou para 42 × 36 m; o cenário em volta
   precisa se ajustar). Depois Play.
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

- Uma porta por lado em cada peça (na 1ª célula do meio, nas peças 2 × 1).
- O corredor ocupa a célula inteira (6 × 6 m) e por enquanto parece uma sala vazia com passadeira; falta arte de corredor estreito.
- Áreas externas (floresta, cemitério) ainda não existem como peças.
- O NavMesh é recalculado inteiro a cada sala (`BuildNavMesh`): pode dar um engasgo pequeno.
- Sem som nem poeira na montagem.
- Câmera por tipo de cena (exploração, susto, morte...) ainda é a próxima etapa.
- Verificado fora da Unity: compilação contra as DLLs de referência da Unity (com stubs de Cinemachine/Input System/URP/AI Navigation)
  e a lógica (`HouseDraft`, `FilmRun.Draft`, replay) rodando de verdade nos 61 testes. **A parte 3D (montagem, animação, câmera, HUD)
  não foi vista rodando**: precisa do checklist acima.
