# 04 — HUD reorganizada e card de sala

> Etapa pequena. Pré-requisito: docs 01–03.

## 1. O que mudou

- **Mapa limpo:** sobre as salas aparece só o **nome**, mais "explorada" e "● n" (quantidade de elementos na sala) quando for o caso. Todas as dicas saíram do mapa.
- **Card da sala:** clicar numa sala abre um painel à esquerda com:
  - a descrição da sala;
  - **O que se sabe:** as dicas, agora escritas como frases de jogador;
  - **Exploração:** se a sala ainda guarda um encontro neste ato;
  - **Palco:** que cenas dá para dirigir ali e a partir de qual ato;
  - **Ponto trancado:** se existe, e se você já tem a ferramenta;
  - **Elementos** que estão na sala, com a descrição;
  - **Quem vai?:** um botão por ator, com o custo em passos. É assim que se move agora.
- **Painel de baixo:** mostra só o ator selecionado e o que ele pode fazer onde está (dirigir cena, usar ferramenta), mais "Encerrar ato".
- **Câmera começa mais longe** (distância 28, máximo 40), e as árvores foram afastadas para não tapar a casa.
- **Controles:**
  - clique na sala abre o card;
  - clique no ator seleciona;
  - Tab passa para o próximo ator;
  - F dá o close;
  - Esc fecha o card ou sai do close.

## 2. Onde isso está no código

| O quê | Onde |
|---|---|
| Clique na sala abre o card (antes: movia direto) | `RunPresenter.HandleClick` → `OpenRoomCard` |
| Botão "Ir" do card | `RunPresenter.SendActor(actor, sala)` |
| Desenho do card | `RunHud.DrawRoomCard` |
| Texto das dicas | asset de cada sala em `Data/P1/Salas` (campo **Hints**) |
| Distância inicial da câmera | `IsoCameraRig` → `startDistance` / `maxDistance` |

## 3. Conceito novo: medir texto antes de desenhar (IMGUI)

O card tem altura variável, porque cada sala tem mais ou menos coisa para mostrar. Em IMGUI você precisa saber o tamanho **antes** de desenhar o fundo. Para isso existe o `CalcHeight`:

```csharp
float textH = small.CalcHeight(new GUIContent(lines.ToString()), w - 24);
```

É parecido com `string_height_ext` no GameMaker: você calcula a altura do texto com quebra de linha e só então desenha o painel do tamanho certo.

Outro detalhe: `GUI.enabled = false` antes de um botão deixa ele cinza e não clicável. É assim que "passos insuficientes" aparece desativado.

## 4. Uma lição de processo

Nesta etapa houve de novo um caso de **cópia para o projeto disparada junto com a recompilação**, e a Unity compilou o arquivo antigo. Agora o fluxo é sempre sequencial:

1. Salvar o arquivo.
2. Copiar para o projeto.
3. Confirmar que o conteúdo novo está lá.
4. Só então pedir a recompilação.

## 5. Experimente

1. Edite os **Hints** de uma sala em `Data/P1/Salas` e veja o card mudar no próximo Play.
2. Mude `startDistance` no `CameraRig`, durante o Play, para achar a distância inicial que você prefere.
3. Escreva a **Description** de uma sala no tom do filme e veja como muda a sensação ao abrir o card.
