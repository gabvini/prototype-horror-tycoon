# HUD nova (UI Toolkit) — hierarquia, ritmo e legibilidade

> Status: 🔄 implementada para playtest (07/10/2026), verificada em Play no P2 em 08/10/2026.
> Pedido do Gabriel: "a HUD está muito ruim de ver e entender; muita informação de uma vez". Objetivo: hierarquia clara,
> só mostrar o que importa no momento, textos legíveis e um ritmo de leitura melhor, com qualidade de protótipo.
> A HUD antiga (`RunHud`, IMGUI) continua no projeto: **F1** alterna entre as duas até o playtest decidir.

> ⚠️ Os comentários do código citam um `HUD_Spec` (§3.4, §4.1, §5, §6.1…). Esse documento (pesquisa de referências + regras)
> foi escrito na conversa do claude.ai e **não chegou a ser salvo** no projeto. Este doc substitui o spec: as regras abaixo
> são as que estão implementadas.

---

## 1. Princípio: o que aparece e quando

| Camada | O quê | Zona |
|---|---|---|
| **Sempre** | Claquete (ato, cena N/8 com bolinhas, audiência / meta com barra), chip do vilão, roteiro (top 3 plots + combos + artefatos), elenco, gravar cena / monitor / menu | Z1 Z2 Z3 Z4 Z6 |
| **Contexto** | Card da sala (gaveta à esquerda), barra de ações do ator selecionado, monitor do diretor | Z7 Z5 Z9 |
| **Hover** | Tooltip único (0,35 s, preso ao item, não ao mouse). A explicação longa mora aqui | — |
| **Sequência** | Relatório da cena como **legendas** no terço inferior; só claquete + elenco ficam visíveis | Z8 |
| **Fase** | Modais de tela cheia: fim de rolo, escolha de artefato, vilão, estreia (fim). O resto da HUD some | — |
| **Filme** | 7 s sem input: faixas pretas, cantos de visor e ● REC; o resto some | — |

Mapa das zonas na tela:

```
┌ Z1 claquete ┐        ┌ Z2 vilão ┐          ┌ Z3 roteiro ┐
│ Z7 card da sala (gaveta)                                  │
│                 (marcadores no mundo)                     │
│                       Z8 legendas                         │
└ Z4 elenco ┘ Z5 ações do ator            Z9 monitor  Z6 ● GRAVAR CENA
```

## 2. Arquivos

| Arquivo | Papel |
|---|---|
| `Scripts/UI/HudRoot.cs` | MonoBehaviour raiz: cria o `UIDocument`, monta a árvore em C#, decide visibilidade por fase, input (F1, Esc, R, Espaço, Shift), modo filme, contagem da audiência |
| `Scripts/UI/HudModel.cs` | Lógica **pura** (testável): cores, textos (encurtar legenda, romanos), escala, beats do relatório, `ReportSequencer`, view-models do roteiro e das ações |
| `Scripts/UI/Panels/HudCore.cs` | Utilitários (`Ui`), tweens, tooltip, `HudSettings` (velocidade do relatório 1–3×, escala 0,8–1,5, em PlayerPrefs), `HudContext`, anel de pavor, pips |
| `Scripts/UI/Panels/HudPanels.cs` | Claquete, chip do vilão, toasts, roteiro, elenco, barra de ações, cluster gravar/monitor/menu |
| `Scripts/UI/Panels/RoomCardPanel.cs` | Card da sala v2 (divulgação progressiva) |
| `Scripts/UI/Panels/SubtitleReport.cs` | Relatório em legendas + histórico (R) |
| `Scripts/UI/Panels/HudModals.cs` | Modais de fase e overlays (histórico, ajuda, lista do roteiro, confirmar fim de ato) |
| `Scripts/UI/Panels/HudWorld.cs` | Monitor do diretor, moldura do modo filme, marcadores no mundo |
| `Scripts/UI/HudInputBlocker.cs` | Impede que o clique na HUD vá para o mundo |
| `Scripts/Editor/Art/HudAssetsSetup.cs` | Menu **Horror Tycoon > HUD > Criar assets da HUD**: fontes SDF, PanelSettings, importação dos ícones |
| `Art/UI/HudTheme.uss` / `Hud.uss` | Tokens (cores, tamanhos) / componentes |
| `Art/UI/HudPanelSettings.asset`, `HudRuntimeTheme.tss` | Scale With Screen Size 1920×1080, match altura |
| `Art/UI/HudComposite.shader` | Composição da HUD na tela (correção de cor, §5) |
| `Art/UI/Fonts/` | Lilita One (títulos), Atkinson Hyperlegible Next (texto), Permanent Marker (carimbos). Todas OFL/Apache, licenças junto |
| `Art/UI/Icons/` | 36 ícones SVG (`ic_*.svg`) |
| `Scripts/Tests/HudModelTests.cs` | 10 testes EditMode da lógica pura |

`GreyboxSceneBuilder` coloca `RunHud` **e** `HudRoot` no objeto de sistemas das cenas P0 e P2.

## 3. Regras de cada peça

### 3.1 Claquete (Z1)
`ATO II · CENA 2/8`, bolinhas das cenas (cheia = disponível), "restam N", **audiência / meta** com barra e traço da meta.
Durante a sequência o número rola até o total conforme os "+N" chegam.

### 3.2 Vilão (Z2)
Antes do fim do Ato 1: "VILÃO ??? — revelado no fim do ato". Depois, cor da família:
Fantasma mostra **assombra Sala > próxima** e a barra de tensão em palavras (`subindo`, …); Slasher mostra o anúncio.

### 3.3 Roteiro (Z3)
Top 3 plots por prioridade (◆ com ●○ das cenas, selo **pronto** quando a condição está cumprida), combos ativos (♥ Casal · Quarto),
ícones dos artefatos. "ver tudo" abre a lista completa; "−" recolhe.

### 3.4 Card da sala (Z7)
Ordem: título + chips de estado + clima → **SE FICAR AQUI POR UMA CENA** (plots, combos, perigo, sozinho, fantasma) → palco → na sala →
rodapé fixo **QUEM VAI?** (retratos-botão com selo de custo: grátis / 1 cena / trancada / travado). Linguagem de filme, sem números
(GDD §6.3). Itens de uma linha; a explicação longa vai para o tooltip.

### 3.5 Elenco (Z4) e ações (Z5)
Retrato com **anel de pavor** em volta, ícone da ferramenta que carrega, estado de crise. Selecionado = moldura clara.
Barra de ações: `ATOR · papel · lugar` + botões de cena (Dirigir Susto / Morte com custo e "N elementos prontos"),
desabilitado com o motivo (ex.: "a partir do Ato 2"). Sem ação: linha cinza explicando o que fazer.

### 3.6 Relatório em legendas (Z8)
- Cada linha do `SceneReport` vira um **beat**, na ordem do tique. Linhas de "Medo" não viram legenda: animam os anéis do elenco.
- Durações (Inspector do `HudRoot` → Ritmo): simples 0,9 s, com pontos 1,05 s, grande 1,6 s, +0,012 s por caractere; teto da sequência 8 s
  (encolhe tudo, mínimo 0,5 s); resumo final 1,2 s. Beat grande = plot cumprido, crise, liberação, artefato, Grande Susto, pega, ou ≥150 pontos.
- O "+N" voa da legenda para a audiência. **Clique/Espaço** pula o beat, 2 cliques rápidos vão ao resumo, **Shift** = 2×, menu = 1×/2×/3×.
- **R** abre o histórico das cenas (texto completo).

### 3.7 Modais de fase
Fim de rolo (audiência/meta, carimbo APROVADO/REPROVADO, plots, mortes, crises, artefatos, o que vem a seguir) → escolha de artefato (3 cartas, pular)
→ "Quem é o vilão?" (cartas por família) → estreia no fim.

### 3.8 Marcadores no mundo e modo filme
Tamanho constante na tela. Um marcador por sala fora do hover (vilão > plot > porta). Nomes das salas só na visão da casa, no hover ou com o card aberto.
Anúncio do vilão sempre; vilão fora da tela = seta na borda. Nada é clicável (o clique passa para o mundo).

### 3.9 Escala
`clamp(altura/1080, 0,75, 2) × opção do jogador (0,8–1,5)`. Telas pequenas não ficam ilegíveis.

## 4. Atalhos

| Tecla | Efeito |
|---|---|
| F1 | Alterna HUD nova ↔ antiga |
| Clique / Espaço | Pula o beat da legenda (2× rápido = resumo) |
| Shift (segurar) | Legendas 2× |
| R | Histórico das cenas |
| Esc | Fecha menu/overlay |
| M | Monitor do diretor (já existia) |

## 5. Correção de cor (URP + HDR)
Com projeto em Linear e câmera HDR, o URP 17 desenha o UI Toolkit lendo as cores do USS como lineares (escuros ficam lavados: `#15121A` vira ~`#51495A`).
`HudRoot → Cor → hdrColorFix` (Auto/Ligado): o painel desenha numa RenderTexture e o `HudComposite.shader` compõe na tela com as cores certas. Desligado = direto na tela.

## 6. Verificação (08/10/2026)
- Compila sem erros; **65 testes EditMode** verdes (55 antigos + 10 da HUD).
- Run jogada em Play no P2 via MCP: card da sala → exploração do Quarto → plot do Casal cumprido → diário do Sótão libera o Porão →
  fim do Ato 1 (472/400) → artefato → vilão (Entidade) → Ato 2 com chip do Fantasma e tensão no mundo → **Grande Susto** com legenda e close.
  Todas as telas apareceram e responderam. Capturas locais em `Captures/hud_*.png` (fora do Git).
- Dica para testes por MCP: a captura de tela do MCP **pausa** o Editor; é preciso despausar depois.

## 7. Pontos de polimento vistos (não bloqueiam o playtest)
1. Cards do elenco grandes e com pouca informação quando o pavor é 0.
2. Traço da meta no fim da barra de audiência não é autoexplicativo.
3. "?" solto ao lado de plot no roteiro e ícone do artefato sem rótulo (só tooltip).
4. "Dirigir Susto · 2 elementos pr…" corta o texto na barra de ações.
5. Cartas do vilão repetem a descrição (resumo + texto longo).
6. ~~Legendas: "Tensão: perto do vilão em Porão" confundia com a tensão do Fantasma.~~ Corrigido (08/10/2026): "Cena perto do vilão em Porão".
