# Horror Tycoon: GDD base (v1)

> Consolidação do brainstorm de 09/10/2026 entre o Gabriel e o Claude. É a **base para implementar**: regras concretas e números de
> partida (todos ajustáveis no Inspector). O que ainda é ideia solta está no §11.

---

## 1. A fantasia

**Você é o diretor de um filme de terror.** Monta o set peça por peça num grid e, no monitor, vê o filme sendo gravado sem parar.
Dirige os atores, descobre (e inventa) quem eles são uns para os outros e dá forma ao vilão pelas salas que escolhe.

Referências: *O Segredo da Cabana* (gente real vivendo um filme de terror controlado por quem está "embaixo"), *Blue Prince*
(montar a casa sala a sala), *Endless Dungeon* (energia, salas escuras = perigo, visão tática), *Until Dawn* e a série *Dark Pictures* (cinema e elenco).

### Pilares

1. **Estratégia legível**: o set em grid é um tabuleiro claro. Poucas regras, todas visíveis. Mais simples que *Endless Dungeon*.
2. **Sempre parece um filme**: o monitor nunca para; toda ação vira cena com planos de cinema.
3. **Cada run é um filme diferente**: elenco, set, tags e vilão se combinam, do terror sério ao trash cult.

---

## 2. Estrutura de uma run

```
Escolher ELENCO (4–5 atores)  →  Escolher CENÁRIO (Casa; depois outros)  →  Introdução no monitor
   → ATO 1 (chegada, montar o set, sinais do vilão)
   → fim do Ato 1: o VILÃO se revela (pelos sinais) + escolha de artefato
   → ATO 2 (o vilão caça; mortes; revelações)
   → ATO 3 (clímax)
   → ESTREIA: nota do público + tom do filme (sério ↔ trash)
```

- **Cena = turno.** Cada ato tem um número de **cenas** (hoje 8). Ações que "gravam" gastam cena e rodam o **tique da casa**
  (sistema atual: plots, combos, pavor, crises, vilão).
- **Meta do ato**: audiência mínima (como hoje). Os **objetivos do diretor** (§9) dão audiência extra e recompensas.
- **Perder**: não bater a meta de um ato fatal, ou o elenco inteiro sair do filme.

---

## 3. O set em grid

- **Célula = 2 m**, o "módulo de set". As medidas reais vezes 1,5 (escala de cinema), em módulos de 2 m:

| Peça | Células | Metros |
|---|---|---|
| Corredor | 1 de largura | 2 m |
| Banheiro | 2 × 2 | 4 × 4 |
| Quarto / Sótão | 3 × 3 | 6 × 6 |
| Cozinha / Porão | 3 × 4 | 6 × 8 |
| Sala de estar | 4 × 3 | 8 × 6 |
| Área externa (clareira, cemitério) | 8 × 8 a 10 × 10 | 16–20 m |
| **Terreno da Casa** | 24 × 20 | 48 × 40 |

- **Começo**: varanda + **sala de estar** (onde o filme começa) já montadas, com portas para o vazio.
- **Abrir porta** = um ator vai lá (custa 1 cena) → **escolha 1 de 3** peças que cabem → a peça **se monta** → o ator entra e explora.
  (Já implementado na casa por escolha; muda o esqueleto de corredores gerado para o grid.)
- Peças novas trazem **portas novas**: a casa cresce em qualquer direção dentro do terreno.
- Peças grandes (externas) ficam nas bordas do terreno ou atrás da porta dos fundos.

### Cada peça (sala) tem

| Campo | Para quê |
|---|---|
| Tamanho em células, portas por lado | Encaixe no grid |
| **Sinal** (opcional): Sobrenatural, Slasher, Culto, Criatura | Forma o vilão (§6) |
| **Objetos**: diário, espelho, faca, rádio, boneca… | Liberam cenas com tags (§7) e plots |
| **Cena de palco** | Cena que dá para dirigir ali (susto, morte, revelação) |
| **Vizinhança** (opcional) | Bônus se encostar em certa peça (ex.: Banheiro ao lado do Quarto → "o espelho mostra o quarto") |
| Especial (opcional) | Gerador (+energia), Mesa do roteirista (§7), Sala de revelação |

---

## 4. Energia e escuridão (Endless Dungeon simplificado)

- O set tem um **quadro de força** com **energia** (começa com **3**). Peças novas nascem **apagadas**.
- **Acender uma sala custa 1 de energia** (grátis, a qualquer momento; apagar devolve). O Gerador e alguns artefatos dão +1.
- **Sala escura**:
  - os atores ganham **mais pavor** por cena;
  - acumula **Presença** (ameaça) a cada cena: +2 se vazia, +1 com 1 ator, 0 com 2 ou mais;
  - **sustos e mortes dirigidos ali rendem mais** (risco × recompensa).
- **Sala acesa**: sem Presença, pavor normal, e a cena rende o normal.
- Uma regra só para o jogador lembrar: **"escuro e sozinho = o vilão aparece; escuro com gente = cena forte"**.

---

## 5. Os atores

- **Arquétipo inicial** (nunca chamado de "classe"): Atleta, Popular, Nerd, Final Girl e depois Criança, a Ex, o Namorado, o Influencer, o Padre cético…
  Cada um traz **1 tag de partida** e uma passiva (as atuais).
- **Pavor** (sistema atual): sobe com escuro, solidão e vilão; no máximo vem a **crise** (trava ou foge do filme).
- **Tags** (até **4 por ator**), ver §7.
- **Ordens**: mover-se é grátis (posicionamento); explorar, dirigir cena e usar objeto gastam cena.

---

## 6. O vilão

### 6.1 Ato 1: "A Ameaça" (???)

- Ainda não tem forma. Cada peça com **Sinal** que alguém explora soma 1 ponto naquela família:
  **Sobrenatural** (bonecas, símbolos, quarto trancado) · **Slasher** (porão de carnificina, máscaras) · **Culto** (ritual, velas) · **Criatura** (marcas, ninho).
- Nas salas escuras a Ameaça faz **eventos** (luz falha, barulho, porta bate): pavor, sem mortes.
- A HUD mostra os sinais coletados: o jogador **escolhe a família do vilão pelas peças que monta e explora**.

### 6.2 Fim do Ato 1: a revelação

- Família = a de mais sinais (empate: o jogador escolhe). Aparecem **2 vilões daquela família** e o jogador escolhe um.
  (O código atual já ordena vilões por subgênero dos elementos coletados.)

### 6.3 Atos 2 e 3: o vilão caça

- **Aparece onde a Presença estiver mais alta** (salas escuras, vazias). Com Presença ≥ 6 numa sala, ele se manifesta ali.
- Comportamento por família (Slasher anda anunciado e pega quem está sozinho; Fantasma acumula tensão e dá o Grande Susto: sistemas atuais).
- **Você não controla o vilão.** Você controla **luz, posição e o set**: expõe ou protege os atores.

---

## 7. Tags e relações (o coração da build)

- **Traços** (de um ator): Corajoso, Supersticioso, Traumatizado, Fã de terror, Mente…
- **Relações** (entre dois atores, ou um ator e o vilão): Namorados, Ex, Irmãos, Rivais, Paixão secreta, Pai/Filho…
  **Vale tudo, inclusive o absurdo** (irmão e ex ao mesmo tempo; o vilão é o tio da Criança).

### De onde vêm

| Fonte | Como |
|---|---|
| **Sala de revelação** | Ao explorar: escolha 1 de 3 revelações ("o Atleta é… ex da Popular / irmão do Nerd / filho do antigo dono") |
| **Mesa do roteirista** (peça especial) | O jogador **escreve** qualquer relação entre dois atores (custa credibilidade se for absurda, §8) |
| Plots e eventos | Plot cumprido dá tag; sobreviver ao vilão dá "Traumatizado" |

### O que liberam

- **Cenas de tag** em salas com o objeto certo: Ex + Ex + Espelho → *O reflexo do passado*; Irmãos + Porão → *O segredo da família*.
- **Combos** (sistema atual de duplas) passam a ser por **tag**, não por papel fixo: "qualquer par Namorados no mesmo cômodo".
- **Sinergia com o vilão**: relação com o vilão muda o comportamento dele (hesita, persegue primeiro, vira aliado por uma cena).
- **Falas** do filme corrente usam as tags (§10).

---

## 8. Choque × Credibilidade: o tom do filme

- **Credibilidade** (0–100, começa em 60): sobe com cenas "pé no chão" (conversa, susto clássico, plot bem amarrado);
  cai com absurdos (relação impossível, reviravolta sem pista, morte exagerada).
- **Sem derrota por credibilidade**: ela define o **tom**, e o tom muda o que rende:

| Tom | Credibilidade | Rende mais |
|---|---|---|
| **Terror sério** | 70–100 | Tensão, plots, sustos bem preparados |
| **Equilibrado** | 30–69 | Um pouco de tudo |
| **Trash cult** | 0–29 | Absurdo, gore, reviravoltas, humor |

- O jogador escolhe a build: um filme sério e bem encaixado, ou um escracho cult. Os extremos dão bônus na estreia.

---

## 9. Objetivos do diretor

- No início de cada ato, **escolha 1 de 3 objetivos** (pedidos do estúdio, sem tema de ritual). Exemplos:
  - "Uma morte numa sala escura neste ato."
  - "Revele uma relação antes da primeira morte."
  - "Termine o ato com o tom Trash." / "…com o tom Sério."
  - "A Final Girl não pode ficar sozinha no escuro."
- Cumprir = **muita audiência + recompensa** (energia, artefato, tag escolhida).
  É o "desafio de puzzle" de cada ato: o set, as tags e a luz são as ferramentas para resolvê-lo.

---

## 10. Câmera e cinema (o filme nunca para)

- **Duas visões**: o **set** (tela principal, planta em grid, tática) e o **monitor do diretor** (o filme; uma tecla deixa em tela cheia).
- **Selecionar um ator não para o filme**: só mostra o aviso e as ações ao lado do set.
- **Filme corrente**: um montador escolhe **beats** pelo estado da run: conversas (falas por fase, papel e tags), planos de ambiente,
  insertos de objetos, provocações do vilão, ações contínuas (perseguição) e **bastidores** quando nada novo acontece.
  Memória de beats recentes evita repetição.
- **Ordem dada → cena do ator** toma o monitor com os planos do tipo (exploração, susto, morte, conversa, montagem de peça) → volta ao filme corrente.

---

## 11. Fora da v1 (ideias guardadas)

- Outros cenários (rua estilo *A Hora do Mal*, lugar abandonado, acampamento) como **conjuntos de dados**: peças, objetos, objetivos.
- Contrato/ritual como modo especial.
- Meta entre runs: desbloquear arquétipos, peças, tags, cenários.
- Voz nas falas; trilha dinâmica.

---

## 12. O que já existe e o que muda

| Sistema | Situação |
|---|---|
| Atos, cenas, tique da casa, plots, combos, pavor, crises, artefatos, vilão (Slasher/Fantasma), replay | **Fica** (combos passam a usar tags) |
| Escolha 1 de 3 + montagem animada | **Fica**, passa para o grid |
| Esqueleto de corredores gerado (casa por escolha) | **Substituído** pelo grid |
| Monitor = planta / tela = filme | **Invertido** (§10) |
| Vilão escolhido no fim do Ato 1 | Passa a vir dos **sinais** (§6) |
| Energia, Presença, tags, credibilidade, objetivos, filme corrente | **Novos** |

---

## 13. Ordem de implementação

Cada etapa termina jogável e testada.

| # | Etapa | Entrega |
|---|---|---|
| **M1** | **Set em grid** | Terreno 24 × 20 células de 2 m; começa na sala de estar; peças com portas por lado; abrir porta → 1 de 3 → montagem (reaproveita o atual); visão de planta |
| **M2** | **Energia e escuridão** | Quadro de força, acender/apagar, Presença, pavor e bônus no escuro, HUD |
| **M3** | **Vilão pelos sinais** | Sinais nas peças, Ameaça no Ato 1, revelação (2 da família), surgir na Presença |
| **M4** | **Tags e relações** | Traços/relações, sala de revelação, mesa do roteirista, cenas de tag, combos por tag |
| **M5** | **Choque × Credibilidade + objetivos** | Medidor, tons, objetivos por ato |
| **M6** | **Filme corrente** | Inversão monitor/set, montador de beats, falas, cena por tipo de ordem |

Motivo da ordem: o **desafio** (estratégia) é o que falta para o jogo "ser alguma coisa", e M1–M3 o criam.
M6 pode subir na fila se o feeling de filme for prioridade para o próximo playtest.

---

## 14. Perguntas abertas

1. Energia inicial 3 e Presença 6 para o vilão surgir: bons pontos de partida?
2. Os artefatos continuam (regras do filme) ou viram recompensas dos objetivos?
3. O jogador escolhe o elenco livre ou sorteia uma oferta (ex.: escolhe 4 entre 6)?
