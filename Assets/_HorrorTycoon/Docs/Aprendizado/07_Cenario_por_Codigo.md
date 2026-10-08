# 07 — Cenário por código (árvores, arbustos, esconderijos de câmera)

Arquivo: `Assets/_HorrorTycoon/Scripts/Editor/SceneryBuilder.cs`
Teste: menu **Horror Tycoon > Cenário > Montar cena de teste (Lab_Cenario)** → `Scenes/Lab_Cenario.unity`

## 1. Seed fixa = `random_set_seed` + `instance_create`

No GameMaker você faria algo assim num evento de Create:

```gml
random_set_seed(1031);
repeat (95) {
    var _x = irandom_range(-430, 430);
    var _y = irandom_range(-430, 430);
    if (point_distance(_x, _y, 0, 0) > 110) instance_create_layer(_x, _y, "Arvores", obj_arvore);
}
```

No `SceneryBuilder` é a mesma ideia:

- `new System.Random(Seed)` é o `random_set_seed`. **Mesma seed → mesma floresta sempre.**
  Mude `Seed` (no topo do arquivo) para sortear outra floresta.
- Usamos `System.Random` (e não `UnityEngine.Random`) para não mexer no gerador global que
  outros sistemas usam. É como ter um gerador só seu.
- "Tentativa e rejeição": sorteia um ponto e **descarta** se cair perto da casa, no caminho,
  em cima de outra árvore ou na linha de visão de um esconderijo. É o mesmo que fazer
  `if (!place_meeting(...))` antes do `instance_create`.
- Atenção: a ordem das chamadas importa. Se você inserir um `c.R(...)` novo no meio, todos os
  sorteios depois dele mudam (igual no GM). Por isso a floresta inteira "muda" quando o código muda.

## 2. Malhas e materiais (o "sprite" do 3D)

- **Mesh (malha)** = a forma: lista de vértices + triângulos + normais (pra onde cada face "olha",
  usada na luz). Não usamos modelos importados: as formas são montadas por código
  (cone, tronco de cone, prisma, "bola" de icosfera achatada).
- **Material** = a "pintura": shader + cor. Usamos `URP/Lit` (recebe a luz da lua) e
  `URP/Unlit` para o que brilha sozinho (rosto das abóboras, olhos no mato).
- Pense em `draw_sprite_ext(..., c_color)`: o sprite é a malha, a cor/blend é o material.
- Os materiais ficam em `Art/Cenario/Materiais/M_Cen_*.mat` e são criados **só se faltarem**
  (cache). Pode mudar as cores no Inspector à vontade: reconstruir não apaga sua mudança.
  Para voltar às cores do código: **Horror Tycoon > Cenário > Restaurar cores dos materiais**.

## 3. Juntar malhas = vertex buffer do GM

No GM, desenhar 300 arbustos com 300 `draw_sprite` custa mais do que montar **um**
`vertex_buffer` com tudo e chamar `vertex_submit` uma vez. Na Unity é igual:

- Cada objeto com material próprio = uma "chamada de desenho" (draw call).
- O `MeshBuilder` acumula os triângulos de **todo** o cenário que usa o mesmo material
  (como `vertex_position_3d` + `vertex_normal` num buffer) e no fim vira **uma** malha.
- Resultado: ~13 malhas (uma por material) em vez de milhares de objetos. As malhas são
  salvas como assets em `Art/Cenario/Malhas/Cen_*.asset` (a cena aponta para elas).
- Custo: não dá para clicar numa árvore e movê-la sozinha. Para mexer, mude o código/seed
  e reconstrua. (Se um dia quiser árvores editáveis à mão, use prefabs em vez disso.)

## 4. Colisores = máscara de colisão

- No GM a máscara de colisão é separada do desenho (`mask_index`). Na Unity também:
  o **Collider** é invisível e independente da malha.
- Aqui **só os troncos** têm colisor (cápsulas de 4,2 m, todas no objeto `Colisores_Troncos`).
  Folhas, arbustos, cerca, túmulos, abóboras: **sem colisor**.
- Por quê? O `CinematicDirector` faz um raycast (tipo `collision_line`) do esconderijo até o
  ator e **recusa** o plano se bater em algo depois dos primeiros 3 m. Se o arbusto tivesse
  colisor, o diretor acharia que a visão está bloqueada.
- O construtor também não deixa nenhuma árvore ficar no "leque" entre um esconderijo e a casa.

## 5. Esconderijos de câmera (`CameraVantage`)

- São objetos vazios com o componente `CameraVantage`: só um **marcador** (como um
  `obj_marker` invisível que o controlador de câmera procura com `with` / `instance_find`).
- 12 esconderijos em volta da casa, a 13–19 m, de 1,7 a 3,6 m de altura, virados para a casa.
- Na frente de cada um (1,5–2,2 m, entre a lente e a casa) há uma moita ou arvoreta cujo topo
  encosta na linha de visão: numa teleobjetiva (FOV ~7–15°) as folhas aparecem desfocadas
  na parte de baixo/lateral do quadro → "alguém espiando escondido no mato".
- No Scene View eles aparecem como uma bolinha laranja com uma linha (gizmo).

## 6. Organização na cena

```
Cenario
├── Colisores_Troncos     (1 objeto, N CapsuleColliders)
├── Esconderijos_Camera   (Esconderijo_01..12 com CameraVantage)
├── Luz_Abobora           (luz laranja fraca, sem sombra)
└── Malhas                (Malha_Casca, Malha_PinheiroA, ..., uma por material)
```

`SceneryBuilder.Build(parent)` apaga o `Cenario` antigo (se existir) e monta tudo de novo.

## 7. Onde mexer para iterar o visual

| Quero...                         | Mexa em                                             |
|----------------------------------|-----------------------------------------------------|
| outra floresta                   | `Seed`                                              |
| mais/menos árvores               | `wanted` em `PlaceTrees`                            |
| cores                            | Inspector dos materiais `M_Cen_*` (ou `MatDefs`)    |
| formato dos pinheiros            | `Pine` (andares, raio, ponta caída)                 |
| árvores mais retorcidas          | `DeadTree` (`bendDir`, galhos)                      |
| esconderijos                     | `VantageAngles`, distância/altura em `PlaceVantages` |
| quanto de folha aparece no quadro| `top` em `VantageFoliage`                           |
