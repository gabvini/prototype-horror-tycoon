# Visão: dirigir um filme de terror montando o set

> Consolidado das observações do Gabriel como jogador (09/10/2026). Documento de direção: guia as próximas etapas.

## 1. A fantasia em uma frase

**Você é o diretor de um filme de terror B: monta o cenário peça por peça num terreno em grid e, no monitor, vê o filme
acontecendo sem parar enquanto decide a próxima cena.**

Duas sensações, sempre juntas:

| Sensação | Onde mora | O que o jogador faz |
|---|---|---|
| **Estratégia / construção** | Tela principal: o **set** em grid, visto de cima | Escolhe peças (salas, áreas externas), abre portas, monta a build com artefatos e plots |
| **Direção / cinema** | O **monitor do diretor** ("a TVzinha"): o take rodando, com planos de filme | Assiste ao filme e chama um ator para a próxima ação ("Atleta!") |

## 2. Estrutura de um filme de terror (pesquisa → níveis e conteúdo)

### 2.1 Como um filme é feito (vocabulário que o jogo usa)

| Etapa real | No jogo |
|---|---|
| **Roteiro / argumento** | Os plots e o vilão que você escolhe formam o roteiro |
| **Locação e cenografia** | O grid: você monta a casa e os arredores |
| **Elenco** | Os arquétipos (Atleta, Popular, Nerd, Final Girl…) |
| **Decupagem** (lista de planos) | Cada tipo de cena tem seu jeito de filmar (§4) |
| **Diária de gravação: claquete, "Ação!", "Corta!", takes** | A cena ordenada pelo jogador é um take; a claquete marca a cena |
| **Cobertura**: plano geral, médio, close, plano/contraplano, inserto | O diretor de câmera alterna estes planos no monitor |
| **Montagem e trilha**: silêncio antes do susto, "stinger" | Ritmo do relatório e dos sustos |

### 2.2 Estrutura narrativa clássica (casa assombrada / slasher)

| Fase do filme | O que acontece | Ato do jogo |
|---|---|---|
| **Chegada / normalidade** | O grupo chega na casa, piadas, apresentações. Começa na **sala de estar** | Ato 1 |
| **Presságio** | Um aviso: o diário, o rádio chiando, a lenda local | Ato 1 |
| **Primeiros sinais** | Barulhos, porta que bate, **susto falso** (o gato) | Ato 1 |
| **"Vamos nos separar"** | O grupo se divide: cada um vai fazer uma coisa | Fim do Ato 1 / Ato 2 |
| **Primeira morte** | O vilão se revela | Ato 2 |
| **Escalada / a verdade** | Mais mortes, descobrem a origem do mal (porão, sótão) | Ato 2 |
| **Clímax** | Confronto, fuga, a Final Girl | Ato 3 |
| **Gancho final** | Último susto antes dos créditos | Fim |

### 2.3 Estereótipos de lugar (peças do grid)

| Peça | Clichê | Cena típica |
|---|---|---|
| Sala de estar | Onde o filme começa; conversa em grupo, lareira | Apresentação, contar a lenda |
| Cozinha | Facas, geladeira aberta no escuro | Susto falso, achar arma |
| Banheiro | O espelho | Reflexo que não é você |
| Quarto | O casal; debaixo da cama | Cena do casal, esconder |
| Porão | O mal mora lá; luz que falha | Descoberta, morte |
| Sótão | Segredos, diário, bonecas | Investigação |
| Floresta (externa, grande) | Perseguição, se perder | Correr, ser caçado |
| Cemitério / galpão / lago (externas) | Ritual, ferramentas, corpo | Clímax, arma, revelação |

## 3. O set em grid

- **Terreno máximo** dividido em grid. Cada peça ocupa uma área: closet 1×1, corredor 5×10, floresta 20×20.
- **Áreas externas são peças** como as salas: dá para filmar nelas.
- Você começa na **chegada** (varanda + sala de estar) e vai **abrindo portas/caminhos**: cada abertura oferece peças para escolher
  (o "1 de 3" que já existe), que **se montam** no lugar (animação que já existe).
- A build vem das peças, artefatos e plots: o set que você monta **é o roteiro** do seu filme.

## 4. O filme nunca para (a câmera sempre rodando)

O monitor mostra o filme o tempo todo. Não existe tela parada de "pensando".

1. **Vida de set com roteiro**: atores parados **conversam com falas de filme de terror** (§5), em plano/contraplano, plano de grupo etc.
2. **Chamar um ator** (clique / "Atleta!"): o monitor faz **foco** nele (zoom lento, close), o grupo continua ao fundo.
3. **Dar a ordem**: o ator fala uma **fala motivada** ("Vou buscar lenha na cozinha.") e sai de cena. O monitor acompanha.
4. **A cena acontece** com a cobertura do seu tipo:

| Tipo de cena | Planos |
|---|---|
| Exploração | Plano geral escuro → POV do ator → inserto no que achou |
| Susto | Silêncio, plano médio longo → corte seco para o close (stinger) |
| Morte | POV do vilão, plano holandês (inclinado), corte antes do golpe |
| Conversa / plot | Plano e contraplano, two-shot |
| Montagem de peça nova | Plano de grua aberto (já existe) |

5. **Volta para o grupo**: a câmera retorna à vida de set até a próxima ordem.

## 5. Falas (sem voz, por enquanto em legenda)

- **Falas avulsas** por situação e papel: chegada, medo, sozinho, perto do vilão, achou algo, alguém morreu.
- **Trocas curtas** (2 a 4 falas) que parecem roteiro:
  — "Vocês ouviram isso?" / — "Deve ser o vento." / — "Não tem janela aberta…"
- **Falas de saída** ao receber uma ordem, ligadas ao destino ("Alguém viu as velas? Vou olhar no porão.").
- Escolhidas pela **fase do filme** (§2.2), pelo papel, pela sala e pelo que aconteceu. Conteúdo em assets, fácil de ampliar.

## 6. Ordem de implementação proposta

1. **Monitor sempre rodando + foco ao chamar o ator + cena com planos por tipo** (§4). Inverte a tela: a principal mostra o set e o monitor mostra o filme.
2. **Falas** (§5): sistema + primeiro pacote de falas para o Ato 1.
3. **Grid com áreas externas** (§3): troca o esqueleto gerado por peças no grid; reaproveita a oferta, a montagem e o replay.

## 7. Em aberto

1. Tamanho da célula do grid: 1 m (floresta 20×20 = 20 m) ou maior?
2. A tela principal (set) é a vista de cima isométrica de hoje?
3. Ao chamar um ator, o tempo do jogo pausa ou o filme segue rolando (só a câmera foca)?
