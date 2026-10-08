# Horror Tycoon: Personagens (Atleta, Nerd, Popular, Final Girl)

Os 4 atores são gerados **100% por script** (Blender `bpy` 5.2, headless). Não existe modelagem manual. Tudo vem de uma **tabela de dados** (`Blender/actors_data.py`) e de construtores reutilizáveis. Rodar de novo produz os mesmos arquivos (só muda o timestamp interno do FBX).

Status: **🧪 proposta v1**. O modelo segue o guia `Direcao_de_Arte_v1.md` §4 e §8.2, mas nada foi testado dentro da Unity ainda (ver "Limitações").

---

## 1. Como regenerar

```bash
pip install bpy==5.2.*            # ou use o Blender 5.x instalado
cd Art/Blender
python3 build_all.py              # paleta + 4 FBX + animações + verificação + previews (~2 min)
python3 build_all.py --no-previews   # só os arquivos (~5 s)
blender -b -P build_all.py        # alternativa com o Blender instalado
```

Etapas avulsas: `python3 build_actor.py --actor nerd`, `python3 build_anims.py`, `python3 verify_fbx.py`, `python3 render_previews.py [--quick]`.

> O `build_all.py` já define `PYTHONHASHSEED=0`, porque o exportador FBX usa `hash()` nos IDs. Para rodar uma etapa avulsa e ter um arquivo idêntico byte a byte, exporte `PYTHONHASHSEED=0` antes.

**Criar um 5º ator:** copie uma entrada de `ACTORS` em `actors_data.py`, troque medidas, cores (nomes da paleta), regras de roupa, cabelo (`flat_top`, `cuia`, `rabo_alto`, `rabo_baixo`) e adereços (`ACC_BUILDERS` em `build_actor.py`). Depois acrescente a chave em `ACTOR_ORDER`. Cor nova vai **no final** da lista `PALETTE` em `ht_common.py`, para não deslocar as UVs existentes.

---

## 2. Arquivos

| Pasta / arquivo | O que é |
|---|---|
| `Characters/HT_Atleta.fbx`, `HT_Nerd.fbx`, `HT_Popular.fbx`, `HT_FinalGirl.fbx` | Modelo + rig + peças do rosto, em pose T |
| `Characters/HT_Anims.fbx` | Esqueleto base com 6 takes: Idle, Walk, Talk, Scared, Scared_Loop, Death |
| `Characters/HT_Palette.png` | Paleta 16×16 (1 pixel = 1 cor do guia). Células vazias em magenta |
| `Blender/ht_common.py` | Paleta, geometria procedural, pesos, rig, exportação |
| `Blender/actors_data.py` | **Tabela dos atores** (medidas, cores, roupa, cabelo, adereços, rosto) |
| `Blender/build_actor.py` | Construtores de corpo, cabeça, cabelo, adereços e rosto, mais a montagem e exportação |
| `Blender/ht_anims.py` / `build_anims.py` | Clipes escritos como poses-chave (dados) / exportação |
| `Blender/verify_fbx.py` | Reimporta cada FBX e confere o contrato (resultado em `Previews/verificacao.txt`) |
| `Blender/ht_preview.py` / `render_previews.py` | Renders de revisão (Cycles imitando o HT_Toon + casca invertida) |
| `Blender/blend/*.blend` | Cenas geradas, para ajuste manual se quiser (o script sobrescreve) |
| `Previews/` | 3/4 de cada ator, folhas de expressão, lineup (frente, iso 50°, cinza, pose T) e quadros das animações |

---

## 3. Estrutura de cada FBX

```
HT_<Ator>_Rig                (armature; escala 1, sem rotação depois do Bake Axis Conversion)
 ├─ Corpo                    SkinnedMesh: corpo + cabeça + cabelo + roupas + adereços (1 material)
 └─ Hips → Spine → Chest → Neck → Head
                                └─ Rosto            (empty no centro do rosto = FaceAnchor)
                                     ├─ Olho_E, Olho_D
                                     │    └─ Cilios_E / Cilios_D   (só Popular; acompanham o olho)
                                     ├─ Pupila_E, Pupila_D
                                     ├─ Sobrancelha_E, Sobrancelha_D
                                     └─ Boca_Neutra, Boca_Feliz, Boca_Tensa, Boca_Grito
```

E = esquerdo **do ator** (+X no Blender). D = direito.

### Ossos (21, nomes do Humanoid da Unity, pose T)

| Cadeia | Ossos |
|---|---|
| Tronco | `Hips` (raiz) → `Spine` → `Chest` → `Neck` → `Head` |
| Braços | `Chest` → `Left/RightShoulder` → `…UpperArm` → `…LowerArm` → `…Hand` |
| Pernas | `Hips` → `Left/RightUpperLeg` → `…LowerLeg` → `…Foot` → `…Toes` |

Sem ossos de dedo. A mão é "luva" com 3 dedos e polegar, sempre aberta. `Toes` existe só para o Humanoid (o calçado é rígido no `Foot`).

### Pesos
- Corpo (tronco, braços, pernas, pescoço): pesos suaves calculados por script ao longo de cada cadeia, com transição em ombro, cotovelo, pulso, quadril, joelho e tornozelo. Os ombros do tronco puxam o osso `Shoulder`.
- Peças rígidas com 100% num osso: cabeça, orelhas, nariz, cabelo, faixa e óculos em `Head`; mãos e celular em `Hand`; calçados em `Foot`; mochila, apito e letra em `Chest`; chaves em `Hips`; ombreiras 75% `Shoulder` + 25% `Chest`.
- As peças do rosto **não têm skin**: são `MeshRenderer` filhos do `Rosto`, para o jogo mexer em escala, posição e rotação.

### Peças do rosto (para que serve cada uma)

| Objeto | Uso no jogo |
|---|---|
| `Rosto` | Empty no centro do rosto, no lugar do atual `faceAnchor` (câmera de close). Pai de todas as peças, com rotação identidade em relação a elas |
| `Olho_E/D` | Branco do olho (`#F7F4EC`, bom para material unlit levemente emissivo). Escalar **só nos 2 eixos do plano do rosto** (não na profundidade) para abrir, fechar, piscar (altura → 0,1) ou arregalar |
| `Pupila_E/D` | Pupila com brilho. Escala = dilatar/contrair. Deslocar em X = olhar para o lado (mover as duas no mesmo sentido) |
| `Sobrancelha_E/D` | Pivô no centro da sobrancelha. Subir/descer e girar no eixo que sai do rosto. A inclinação característica do ator já vem na malha |
| `Boca_Neutra/Feliz/Tensa/Grito` | 4 bocas inteiras no mesmo lugar: **ligar só uma** (`SetActive`). As quatro vêm visíveis no FBX |
| `Cilios_E/D` (Popular) | Delineado e cílios. Filhos do olho, escalam junto |

Todas as peças têm origem no próprio centro, escala 1 e rotação 0.

### Valores sugeridos para as expressões
São os mesmos usados nas folhas `Previews/HT_*_Expressoes.png` e seguem a tabela do guia §4.2. São **multiplicadores sobre a pose base**, não valores absolutos.

| Expressão | Olho (largura, altura) | Pupila | Pupila desvio X | Sobrancelha Δaltura | Sobrancelha giro* | Boca | Corpo (raiz) |
|---|---|---|---|---|---|---|---|
| Neutra | 1,0 / 1,0 | 1,0 | 0 | 0 | 0° | Neutra | 1 |
| Feliz | 1,0 / 0,8 | 1,1 | 0 | +0,014 m | +4° | Feliz | 1 |
| Tensa | 1,0 / 1,0 | 0,7 | 0,013 m | −0,008 m | −18° | Tensa | 1 |
| Assustada | 1,6 / 1,6 | 0,35 | 0 | +0,030 m | +16° | Grito | XZ 0,9 / Y 1,15 |

\* Giro positivo = ponta de dentro sobe (preocupado). Negativo = desce (bravo). Espelhar o sinal na sobrancelha direita.

**Ajustes necessários no `ActorView` (para o lead):**
1. O `ApplyExpression` atual grava escala e posição **absolutas** do greybox (0,07; −0,11…). Trocar por: guardar `localScale`, `localPosition` e `localRotation` de cada peça no `Awake` e aplicar os multiplicadores da tabela. A rotação fica `base * Quaternion.AngleAxis(giro, eixo_frente_local)`.
2. O campo `mouth` vira 4 referências (ou uma lista) com `SetActive`.
3. `SetDead` pode tocar o clipe `Death` em vez de deitar o transform.
4. O "Grito" (esticar o corpo) é feito na escala do transform raiz (como no preview), não por shape key (ver §6).

---

## 4. Importação na Unity (recomendado)

**Model:** Scale Factor 1 · Convert Units ✔ · **Bake Axis Conversion ✘ (DESLIGADO — testado na Unity 6.3: ligado, os atores ficam virados para −Z)** · Import BlendShapes (não há) · Normals: Import · Mesh Compression Off.
**Rig:** Animation Type **Humanoid** · Avatar Definition: *Create From This Model* (os 4 atores e o `HT_Anims` estão em pose T).
**Materials:** Material Creation Mode **None**. O `HT_Toon` (com `HT_Palette.png`) é atribuído por script ao `Corpo` e às peças do rosto. Os olhos podem usar a variante unlit/emissiva.
**Textura `HT_Palette.png`:** Filter **Point**, Compression **None**, Wrap **Clamp**, sRGB ✔, Mip Maps off. Cada face aponta para o **centro** de uma célula, então a cor não "vaza".
**Conferir:** altura no Inspector entre 1,71 e 1,79 m, raiz sem rotação, ator olhando para +Z.

**`HT_Anims.fbx`:** Humanoid (Create From This Model ou Copy From Other Avatar). Em cada clipe:
- Loop Time ✔ em `Idle`, `Walk`, `Talk` e `Scared_Loop` (o primeiro e o último quadro são idênticos, conferido).
- Root Transform Rotation e Position (XZ): *Bake Into Pose*. Position (Y): *Bake Into Pose*, Based Upon *Original*, para o pulo do Scared e a queda do Death ficarem na pose.
- Animator com **Apply Root Motion desligado**: o `NavMeshAgent` move o ator e o `Walk` é no lugar.

| Clipe | Duração | Tipo | Descrição |
|---|---|---|---|
| Idle | 2,0 s | loop | Respiração e troca de peso |
| Walk | 0,8 s | loop | 2 passos, quicando, braços opostos, tronco gira |
| Talk | 2,0 s | loop | Mão na cintura, a outra gesticula, cabeça acena |
| Scared | 1,6 s | uma vez | Antecipa, **pula**, cai encolhido e treme |
| Scared_Loop | 0,8 s | loop | Encolhido tremendo (para depois do Scared) |
| Death | 2,0 s | uma vez | Endurece, balança, cai de costas "como tábua", quica e desmaia (sem gore) |

---

## 5. Contagem de triângulos

| Ator | Corpo (skin) | Rosto visível | **Total visível** | Arquivo (4 bocas) | Altura |
|---|---|---|---|---|---|
| Atleta | 7 172 | 1 184 | **8 356** | 9 584 | 1,745 m |
| Nerd | 7 948 | 1 376 | **9 324** | 10 600 | 1,728 m |
| Popular | 6 980 | 1 724 | **8 704** | 10 332 | 1,789 m |
| Final Girl | 8 116 | 1 184 | **9 300** | 10 528 | 1,714 m |

---

## 6. Desvios do guia (e por quê)

1. **Pesos calculados, não *bone heat*.** O corpo é feito de cascas fechadas que se sobrepõem (tronco, braços, pernas). O próprio guia aponta que pesos automáticos falham nesse tipo de malha. Os pesos por cadeia são determinísticos e foram testados posando todos os clipes.
2. **Loft próprio no lugar do Skin Modifier.** É a mesma ideia (anéis em volta de um esqueleto), mas com seção superelíptica por anel. O resultado são loops limpos onde a roupa troca de cor (punho, barra, cintura) e controle fino da silhueta.
3. **Bocas como 4 malhas, não sprite.** Foi o pedido desta tarefa. É compatível com o pipeline do toon sem shader extra.
4. **Shape keys `Grito` e `Piscar` não exportadas.** As peças do rosto não têm skin, então uma shape key no corpo deixaria o rosto para trás. O esticar fica na escala da raiz e piscar é escalar `Olho_*` em altura.
5. **Triângulos: 8,4–9,3 mil visíveis**, contra 3–6 mil do guia. A maior parte está na cabeça e no cabelo (o close precisa) e nos adereços de identidade (óculos, mochila, chaves). Para reduzir, basta baixar `segs` nos construtores. Custo total dos 4 em cena: cerca de 36 mil triângulos.
6. **Alturas de 1,71 a 1,79 m** (o guia permite ±5 cm no visual). A cabeça tem cerca de 0,50 m (3,5 cabeças).
7. **Cores acrescentadas à paleta** (fim da lista `PALETTE`): branco do olho, interior da boca, língua, tênis/sola, sobrancelhas, xadrez e cotelê escuros, meia, óculos, mochila, blush, batom, curativo, metal e outras. Nenhuma usa as cores reservadas aos vilões.
8. **Escolhas onde o guia dava opção:** a Popular usa calça (boca de sino magenta), não saia, para evitar atravessar as pernas na caminhada. O Atleta usa o apito, não a bola, para deixar as mãos livres para as animações. O Nerd ganhou decote em V, xadrez, meia branca e dentes de coelho. A Final Girl ganhou cinto, mangas dobradas e cadarços.
9. **Exporta também `EMPTY`** (o `Rosto`), além de ARMATURE e MESH.
10. **Clipe extra `Scared_Loop`**, para manter o susto depois do pulo.

---

## 7. Limitações conhecidas

- **Importado e testado na Unity (05/10/2026):** a importação é configurada por código em `Scripts/Editor/Art/HTCharacterSetup.cs` (Bake Axis Conversion desligado, mapa Humanoid sem olhos/mandíbula). Nota original: A conversão de eixos foi validada só por reimportação no Blender: frente −Y vira +Z, escala 1. **Confirmar na Unity** qual eixo local das peças do rosto aponta para fora do rosto (esperado +Z com Bake Axis Conversion) antes de fixar o eixo de giro das sobrancelhas.
- O contorno por casca invertida desenha **uma linha onde as cascas se cruzam** (ombro, virilha, punho, cabelo/testa). É intencional (lê como costura e borda de roupa), mas aparece.
- Cabelo e rabos de cavalo são rígidos na cabeça: não balançam. Um osso extra fora do Humanoid poderia fazer isso depois.
- As mãos não fecham (sem ossos de dedo). O celular da Popular fica colado na palma.
- O topo do flat-top do Atleta tem sombreado um pouco facetado (lê como textura de cabelo; dá para alisar subindo `segs`).
- No `Death` o Nerd deita "por cima" da mochila: ela atravessa um pouco o chão.
- Os previews são uma **aproximação** do HT_Toon no Cycles: rampa de 3 degraus, sem luzes pontuais nem neblina.
