using System.Collections.Generic;
using HorrorTycoon.Art;
using UnityEngine;
using UnityEngine.Rendering;

namespace HorrorTycoon.Rooms
{
    /// <summary>Estilo de mobília de um cômodo (decide o que o FurnitureKit monta).</summary>
    public enum FurnitureStyle
    {
        None,
        Hallway,
        Kitchen,
        Living,
        Bathroom,
        Bedroom,
        Basement,
        Attic,
        // Novos estilos SEMPRE no fim (valor salvo como número nos assets).
        Hall,
        Dining
    }

    /// <summary>
    /// "Kit de móveis por código": monta móveis com caixas CHANFRADAS (BevelMesh) e cilindros, em escala real.
    /// Visual do guia de arte (§5): grossos, "de massinha", levemente TORTOS (1–3°), armários afunilados,
    /// escala exagerada no que conta a história (geladeira, banheira, relógio de pé) e fita crepe no chão
    /// (marcas de ator — é um set de filmagem).
    /// Cada móvel é uma função curta — fácil de trocar depois por um modelo 3D de verdade.
    /// Coordenadas LOCAIS do cômodo: (0,0,0) = centro do piso; 'size' = largura (X) × profundidade (Z) em metros.
    /// PORTA: por convenção fica no lado +X local, no meio (z = 0). O construtor gira o 'Interior' dos cômodos
    /// do outro lado do corredor, então nenhum móvel deve ocupar a faixa x > hw-1, |z| < 0.8.
    /// Casa gerada: o HouseBuilder gira o Interior para a porta PRINCIPAL ficar no +X local (qualquer lado),
    /// passa o tamanho já girado e depois remove o que ficar na frente de outras portas (ou fora do meio).
    /// Sem colliders: não atrapalham o clique na sala nem o NavMesh.
    /// Materiais: shader HorrorTycoon/Toon (preset de ambiente), um por cor, em cache.
    /// </summary>
    public static class FurnitureKit
    {
        private static readonly Dictionary<Color, Material> cache = new Dictionary<Color, Material>();
        private static readonly Dictionary<long, Material> emissiveCache = new Dictionary<long, Material>();

        // Paleta do guia (§3.1) — valores baixos, puxados para azul/roxo.
        private static readonly Color Wood = Hex(0x8A5A3C);      // madeira clara
        private static readonly Color DarkWood = Hex(0x4A3328);  // rodapé, batente, madeira escura
        private static readonly Color Fabric = Hex(0x7A3B47);    // tecido/tapete vinho
        private static readonly Color Porcelain = Hex(0xA8A6A0); // louça (o mais claro do cenário)
        private static readonly Color Tile = Hex(0x6F8580);      // azulejo verde-acinzentado
        private static readonly Color Metal = Hex(0x55606E);
        private static readonly Color Cardboard = Hex(0x8A6E48);
        private static readonly Color Sheet = Hex(0xB5AFA8);     // lençol "fantasma"
        private static readonly Color Rug = Hex(0x5E2A35);
        private static readonly Color Ink = Hex(0x15121A);
        private static readonly Color Tape = Hex(0xC9B458);      // fita crepe de marcação
        private static readonly Color Canvas = Hex(0x2E2A3A);    // lona da cadeira de diretor
        private static readonly Color Brass = Hex(0xB08A3E);
        private static readonly Color Mirror = Hex(0x6F7F8A);

        public static void Build(FurnitureStyle style, Transform parent, Vector2 size, int seed)
        {
            var rng = new System.Random(seed);
            float hw = size.x * 0.5f;
            float hd = size.y * 0.5f;

            switch (style)
            {
                case FurnitureStyle.Hallway:
                    Box(parent, "Passadeira", new Vector3(0, 0.026f, 0), new Vector3(0.9f, 0.012f, size.y * 0.8f), Rug);
                    Box(parent, "Passadeira borda", new Vector3(0, 0.024f, 0), new Vector3(1.0f, 0.008f, size.y * 0.8f + 0.1f), Brass);
                    // Corredor: portas nas duas paredes a cada 4 m (z = -4, 0, 4). Móveis ficam ENTRE as portas.
                    Frame(parent, new Vector3(-hw + 0.08f, 1.6f, -2f), new Vector2(0.45f, 0.55f), 90f, -4f, Hex(0x3B4A6B));
                    Frame(parent, new Vector3(-hw + 0.08f, 1.55f, 2f), new Vector2(0.4f, 0.4f), 90f, 3f, Hex(0x6B3B3B));
                    Bevel(parent, "Aparador", new Vector3(hw - 0.22f, 0.42f, 2f), new Vector3(0.32f, 0.84f, 0.95f), Wood, 0.03f, 0.06f, new Vector3(0, 0, 1.5f));
                    Lamp(parent, new Vector3(hw - 0.22f, 0.84f, 2.25f), 0.7f);
                    Cylinder(parent, "Cabideiro", new Vector3(hw - 0.25f, 0.9f, -hd + 0.6f), new Vector3(0.06f, 0.9f, 0.06f), DarkWood).localRotation = Quaternion.Euler(0, 0, 3f);
                    DirectorChair(parent, new Vector3(-0.62f, 0, -hd + 0.5f), 135f);
                    TapeX(parent, new Vector3(0.1f, 0, -1.0f), 25f);
                    TapeX(parent, new Vector3(-0.1f, 0, 1.2f), -10f);
                    break;

                case FurnitureStyle.Kitchen:
                    Bevel(parent, "Balcão", new Vector3(-0.35f, 0.45f, hd - 0.32f), new Vector3(size.x - 1.3f, 0.9f, 0.62f), Porcelain, 0.03f);
                    Bevel(parent, "Tampo", new Vector3(-0.35f, 0.93f, hd - 0.31f), new Vector3(size.x - 1.2f, 0.06f, 0.68f), DarkWood, 0.02f);
                    Bevel(parent, "Fogão", new Vector3(-hw + 0.6f, 0.47f, hd - 0.33f), new Vector3(0.72f, 0.94f, 0.64f), Metal, 0.04f);
                    for (int i = 0; i < 4; i++)
                    {
                        float bx = -hw + 0.45f + (i % 2) * 0.3f, bz = hd - 0.45f + (i / 2) * 0.22f;
                        Cylinder(parent, "Boca", new Vector3(bx, 0.95f, bz), new Vector3(0.16f, 0.01f, 0.16f), Ink);
                    }
                    // Geladeira exagerada (1,2×), um pouco torta e com puxador.
                    var fridge = Bevel(parent, "Geladeira", new Vector3(hw - 0.48f, 1.08f, hd - 0.45f), new Vector3(0.8f, 2.16f, 0.76f), Porcelain, 0.09f, 0.05f, new Vector3(0, 0, -2f));
                    Bevel(fridge, "Puxador", new Vector3(-0.3f, 0.25f, -0.4f), new Vector3(0.05f, 0.5f, 0.05f), Metal, 0.015f);
                    Table(parent, new Vector3(0, 0, -0.35f), new Vector2(1.3f, 0.85f));
                    Chair(parent, new Vector3(-0.85f, 0, -0.35f), 90f + 8f);
                    Chair(parent, new Vector3(0.85f, 0, -0.45f), -90f - 12f);
                    TapeX(parent, new Vector3(0.2f, 0, 0.5f), 15f);
                    break;

                case FurnitureStyle.Living:
                    Box(parent, "Tapete", new Vector3(0, 0.026f, 0), new Vector3(size.x * 0.6f, 0.012f, size.y * 0.5f), Rug);
                    Sofa(parent, new Vector3(0, 0, -hd + 0.55f), 0f);
                    Bevel(parent, "Mesa de centro", new Vector3(0, 0.22f, -0.25f), new Vector3(1.0f, 0.44f, 0.56f), Wood, 0.04f, 0.08f);
                    // Estante torta com livros coloridos.
                    var shelf = Bevel(parent, "Estante", new Vector3(-hw + 0.22f, 1.0f, 0.6f), new Vector3(0.4f, 2.0f, 1.15f), DarkWood, 0.04f, 0.06f, new Vector3(0, 0, -2.5f));
                    for (int row = 0; row < 3; row++)
                    {
                        float z = -0.42f;
                        while (z < 0.42f)
                        {
                            float w = 0.06f + (float)rng.NextDouble() * 0.07f;
                            float bh = 0.22f + (float)rng.NextDouble() * 0.12f;
                            var book = Box(shelf, "Livro", new Vector3(0.12f, -0.62f + row * 0.55f + bh * 0.5f, z + w * 0.5f), new Vector3(0.22f, bh, w), BookColor(rng));
                            book.localRotation = Quaternion.Euler((float)rng.NextDouble() * 10f - 5f, 0, 0);
                            book.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                            z += w + 0.012f;
                        }
                    }
                    Bevel(parent, "Rack", new Vector3(0, 0.26f, hd - 0.27f), new Vector3(1.4f, 0.52f, 0.46f), DarkWood, 0.03f);
                    var tv = Bevel(parent, "TV velha", new Vector3(0, 0.82f, hd - 0.3f), new Vector3(0.66f, 0.56f, 0.5f), Hex(0x2A2730), 0.07f, 0.05f, new Vector3(0, 6f, 0));
                    Emissive(tv, "Tela", new Vector3(0, 0.02f, -0.255f), new Vector3(0.48f, 0.38f, 0.02f), Hex(0x1C3A44), new Color(0.35f, 0.95f, 1.15f));
                    Cylinder(tv, "Antena E", new Vector3(-0.1f, 0.42f, 0.05f), new Vector3(0.015f, 0.18f, 0.015f), Metal).localRotation = Quaternion.Euler(0, 0, 25f);
                    Cylinder(tv, "Antena D", new Vector3(0.1f, 0.42f, 0.05f), new Vector3(0.015f, 0.18f, 0.015f), Metal).localRotation = Quaternion.Euler(0, 0, -25f);
                    Armchair(parent, new Vector3(-hw + 0.6f, 0, -0.95f), 70f);
                    GrandfatherClock(parent, new Vector3(hw - 0.3f, 0, hd - 0.35f), -90f);
                    TapeX(parent, new Vector3(0.3f, 0, 0.45f), -20f);
                    break;

                case FurnitureStyle.Bathroom:
                    Box(parent, "Azulejo", new Vector3(-hw + 0.5f, 0.027f, 0.2f), new Vector3(1.0f, 0.014f, 2.1f), Tile);
                    // Banheira exagerada (1,15×) com pés.
                    var tub = Bevel(parent, "Banheira", new Vector3(-hw + 0.5f, 0.36f, 0.2f), new Vector3(0.86f, 0.62f, 1.95f), Porcelain, 0.12f, 0.08f);
                    Box(tub, "Água escura", new Vector3(0, 0.305f, 0), new Vector3(0.62f, 0.02f, 1.62f), Hex(0x1E2B26));
                    foreach (var o in new[] { new Vector3(-0.3f, -0.33f, -0.8f), new Vector3(0.3f, -0.33f, -0.8f), new Vector3(-0.3f, -0.33f, 0.8f), new Vector3(0.3f, -0.33f, 0.8f) })
                    {
                        Cylinder(tub, "Pé", o, new Vector3(0.09f, 0.05f, 0.09f), Brass);
                    }
                    Bevel(parent, "Pia", new Vector3(hw - 0.32f, 0.45f, hd - 0.6f), new Vector3(0.52f, 0.9f, 0.62f), Porcelain, 0.06f, 0.12f);
                    var mirror = Bevel(parent, "Espelho rachado", new Vector3(hw - 0.04f, 1.55f, hd - 0.6f), new Vector3(0.05f, 0.75f, 0.55f), DarkWood, 0.015f, 0f, new Vector3(4f, 0, 0));
                    Box(mirror, "Vidro", new Vector3(-0.02f, 0, 0), new Vector3(0.02f, 0.62f, 0.44f), Mirror);
                    Box(mirror, "Rachadura", new Vector3(-0.031f, 0.05f, 0.04f), new Vector3(0.005f, 0.5f, 0.012f), Ink).localRotation = Quaternion.Euler(28f, 0, 0);
                    var toilet = Bevel(parent, "Vaso", new Vector3(hw - 0.38f, 0.22f, -hd + 0.45f), new Vector3(0.42f, 0.44f, 0.56f), Porcelain, 0.1f, 0.15f);
                    Bevel(toilet, "Caixa", new Vector3(0.12f, 0.42f, 0f), new Vector3(0.18f, 0.5f, 0.5f), Porcelain, 0.05f);
                    TapeX(parent, new Vector3(0.35f, 0, -0.4f), 5f);
                    break;

                case FurnitureStyle.Bedroom:
                    var bed = Bevel(parent, "Cama", new Vector3(-hw + 1.1f, 0.25f, hd - 1.1f), new Vector3(1.45f, 0.5f, 2.05f), DarkWood, 0.05f);
                    Bevel(bed, "Cabeceira", new Vector3(0, 0.45f, 1.0f), new Vector3(1.55f, 1.4f, 0.1f), DarkWood, 0.04f, 0f, new Vector3(0, 0, 2f));
                    Bevel(bed, "Colchão", new Vector3(0, 0.31f, -0.02f), new Vector3(1.38f, 0.16f, 1.96f), Sheet, 0.06f);
                    Bevel(bed, "Cobertor", new Vector3(0, 0.38f, -0.35f), new Vector3(1.44f, 0.08f, 1.25f), Hex(0x3E4F78), 0.035f);
                    Bevel(bed, "Travesseiro", new Vector3(0, 0.45f, 0.78f), new Vector3(1.0f, 0.14f, 0.36f), Porcelain, 0.06f).localRotation = Quaternion.Euler(-6f, 2f, 0);
                    Bevel(parent, "Criado-mudo", new Vector3(-hw + 0.25f, 0.3f, hd - 0.28f), new Vector3(0.42f, 0.6f, 0.42f), Wood, 0.03f, 0.08f);
                    Lamp(parent, new Vector3(-hw + 0.25f, 0.6f, hd - 0.28f), 0.6f);
                    // Guarda-roupa afunilado e torto (o "monstro do armário").
                    var wardrobe = Bevel(parent, "Guarda-roupa", new Vector3(hw - 0.38f, 1.1f, hd - 0.8f), new Vector3(0.64f, 2.2f, 1.25f), DarkWood, 0.05f, 0.08f, new Vector3(0, 0, 2.5f));
                    Box(wardrobe, "Fresta", new Vector3(-0.33f, 0f, 0f), new Vector3(0.01f, 1.7f, 0.03f), Ink);
                    Bevel(parent, "Baú de brinquedos", new Vector3(hw - 0.42f, 0.26f, -hd + 0.42f), new Vector3(0.72f, 0.52f, 0.52f), Wood, 0.05f);
                    Doll(parent, new Vector3(hw - 0.42f, 0.52f, -hd + 0.42f));
                    TapeX(parent, new Vector3(0.25f, 0, -0.3f), -30f);
                    break;

                case FurnitureStyle.Basement:
                    Bevel(parent, "Bancada", new Vector3(0, 0.46f, hd - 0.36f), new Vector3(2.0f, 0.92f, 0.72f), Wood, 0.03f, 0.04f);
                    Bevel(parent, "Prateleira", new Vector3(-hw + 0.26f, 0.92f, 0), new Vector3(0.46f, 1.84f, 1.8f), Metal, 0.025f, 0.03f, new Vector3(0, 0, -2f));
                    for (int i = 0; i < 5; i++)
                    {
                        var jar = Cylinder(parent, "Pote", new Vector3(-hw + 0.3f, 0.55f + (i % 2) * 0.6f, -0.6f + i * 0.3f), new Vector3(0.14f, 0.1f, 0.14f), i % 2 == 0 ? Hex(0x6E8B3D) : Hex(0x8A4A3C));
                        jar.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                    }
                    var boiler = Cylinder(parent, "Caldeira", new Vector3(hw - 0.55f, 1.0f, hd - 0.65f), new Vector3(0.82f, 1.0f, 0.82f), Metal);
                    Emissive(boiler, "Brasa", new Vector3(0, -0.45f, -0.5f), new Vector3(0.35f, 0.12f, 0.05f), Hex(0x3A1A10), new Color(2.2f, 0.7f, 0.2f));
                    Cylinder(parent, "Cano", new Vector3(hw - 0.55f, 2.3f, hd - 0.65f), new Vector3(0.18f, 0.35f, 0.18f), Metal);
                    ScatterBoxes(parent, rng, hw, hd, 6, Cardboard);
                    Cylinder(parent, "Barril", new Vector3(hw - 0.42f, 0.45f, -hd + 0.5f), new Vector3(0.58f, 0.45f, 0.58f), DarkWood).localRotation = Quaternion.Euler(3f, 0, -4f);
                    TapeX(parent, new Vector3(-0.2f, 0, -0.5f), 40f);
                    break;

                case FurnitureStyle.Attic:
                    ScatterBoxes(parent, rng, hw, hd, 8, Cardboard);
                    // Móvel coberto por lençol: caixa chanfrada bem arredondada + "barra" do lençol.
                    var covered = Bevel(parent, "Móvel coberto", new Vector3(-hw + 0.62f, 0.55f, 0.3f), new Vector3(0.95f, 1.1f, 1.45f), Sheet, 0.18f, 0.12f, new Vector3(0, 0, 3f));
                    Bevel(covered, "Barra", new Vector3(0, -0.5f, 0), new Vector3(1.05f, 0.12f, 1.55f), Sheet, 0.05f);
                    var trunk = Bevel(parent, "Baú antigo", new Vector3(hw - 0.52f, 0.31f, hd - 0.52f), new Vector3(0.95f, 0.62f, 0.58f), DarkWood, 0.06f);
                    Box(trunk, "Fecho", new Vector3(0, 0.12f, -0.3f), new Vector3(0.12f, 0.14f, 0.03f), Brass);
                    Cylinder(parent, "Manequim", new Vector3(0.6f, 0.95f, hd - 0.5f), new Vector3(0.36f, 0.6f, 0.36f), Sheet);
                    Cylinder(parent, "Manequim haste", new Vector3(0.6f, 0.2f, hd - 0.5f), new Vector3(0.05f, 0.2f, 0.05f), DarkWood);
                    Primitive(PrimitiveType.Sphere, parent, "Manequim cabeça", new Vector3(0.6f, 1.72f, hd - 0.5f), new Vector3(0.24f, 0.28f, 0.24f), Sheet);
                    RockingChair(parent, new Vector3(0.1f, 0, -0.9f), 200f);
                    TapeX(parent, new Vector3(0.4f, 0, 0.3f), 12f);
                    break;

                case FurnitureStyle.Hall:
                    // Hall de entrada (convivência inicial): o elenco se reúne aqui. Centro livre para os atores.
                    Box(parent, "Tapete", new Vector3(0, 0.026f, 0), new Vector3(size.x * 0.55f, 0.012f, size.y * 0.45f), Rug);
                    Box(parent, "Capacho", new Vector3(hw - 0.45f, 0.028f, 0), new Vector3(0.55f, 0.012f, 0.9f), Cardboard);
                    Bevel(parent, "Aparador", new Vector3(-hw + 0.22f, 0.42f, -0.2f), new Vector3(0.34f, 0.84f, 1.0f), Wood, 0.03f, 0.06f, new Vector3(0, 0, 1.5f));
                    Lamp(parent, new Vector3(-hw + 0.22f, 0.84f, -0.45f), 0.6f);
                    Frame(parent, new Vector3(-hw + 0.08f, 1.62f, -0.2f), new Vector2(0.6f, 0.75f), 90f, -3f, Mirror);
                    Bevel(parent, "Banco", new Vector3(-0.3f, 0.23f, -hd + 0.26f), new Vector3(1.2f, 0.46f, 0.42f), DarkWood, 0.03f, 0.05f);
                    Bevel(parent, "Almofada do banco", new Vector3(-0.3f, 0.49f, -hd + 0.26f), new Vector3(1.1f, 0.07f, 0.36f), Fabric, 0.03f);
                    var rack = Cylinder(parent, "Cabideiro", new Vector3(hw - 0.3f, 0.9f, hd - 0.35f), new Vector3(0.06f, 0.9f, 0.06f), DarkWood);
                    rack.localRotation = Quaternion.Euler(0, 0, 3f);
                    Box(parent, "Ganchos", new Vector3(hw - 0.27f, 1.72f, hd - 0.35f), new Vector3(0.36f, 0.04f, 0.04f), DarkWood).localRotation = Quaternion.Euler(0, 35f, 3f);
                    Bevel(parent, "Casaco", new Vector3(hw - 0.22f, 1.35f, hd - 0.38f), new Vector3(0.3f, 0.65f, 0.16f), Hex(0x3E4F78), 0.06f, 0.2f, new Vector3(0, 0, 4f));
                    Cylinder(parent, "Porta-guarda-chuva", new Vector3(hw - 0.3f, 0.25f, -hd + 0.3f), new Vector3(0.22f, 0.25f, 0.22f), Metal);
                    Cylinder(parent, "Guarda-chuva", new Vector3(hw - 0.33f, 0.62f, -hd + 0.3f), new Vector3(0.03f, 0.4f, 0.03f), Ink).localRotation = Quaternion.Euler(6f, 0, -8f);
                    GrandfatherClock(parent, new Vector3(-hw + 0.3f, 0, hd - 0.35f), 90f);
                    DirectorChair(parent, new Vector3(0.4f, 0, hd - 0.6f), 200f);
                    TapeX(parent, new Vector3(0.2f, 0, 0.3f), 20f);
                    break;

                case FurnitureStyle.Dining:
                {
                    // Sala de jantar (convivência): "uma mesa posta para ninguém". A mesa fica afastada da porta (+X).
                    float tx = -0.25f;
                    float tl = Mathf.Clamp(size.x - 1.9f, 1.0f, 1.8f);
                    float tw = Mathf.Clamp(size.y - 1.9f, 0.7f, 0.95f);
                    Box(parent, "Tapete", new Vector3(tx, 0.026f, 0), new Vector3(tl + 1.2f, 0.012f, tw + 1.3f), Rug);
                    Table(parent, new Vector3(tx, 0, 0), new Vector2(tl, tw));
                    var places = new List<Vector3>();
                    for (int k = -1; k <= 1; k += 2)
                    {
                        float cx = tx + k * tl * 0.25f;
                        Chair(parent, new Vector3(cx, 0, -tw * 0.5f - 0.35f), (float)rng.NextDouble() * 10f - 5f);
                        Chair(parent, new Vector3(cx, 0, tw * 0.5f + 0.35f), 180f + (float)rng.NextDouble() * 10f - 5f);
                        places.Add(new Vector3(cx, 0, -tw * 0.5f + 0.2f));
                        places.Add(new Vector3(cx, 0, tw * 0.5f - 0.2f));
                    }
                    // Cabeceira do lado oposto à porta.
                    Chair(parent, new Vector3(tx - tl * 0.5f - 0.35f, 0, 0), 90f + (float)rng.NextDouble() * 8f - 4f);
                    places.Add(new Vector3(tx - tl * 0.5f + 0.2f, 0, 0));
                    foreach (var pl in places)
                    {
                        var plate = Cylinder(parent, "Prato", pl + new Vector3(0, 0.795f, 0), new Vector3(0.24f, 0.006f, 0.24f), Porcelain);
                        plate.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                        Cylinder(parent, "Copo", pl + new Vector3(0.14f, 0.85f, 0.08f), new Vector3(0.06f, 0.05f, 0.06f), Mirror)
                            .GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                    }
                    Cylinder(parent, "Castiçal", new Vector3(tx, 0.9f, 0), new Vector3(0.08f, 0.1f, 0.08f), Brass);
                    Emissive(parent, "Chama", new Vector3(tx, 1.04f, 0), new Vector3(0.03f, 0.06f, 0.03f), Tape, new Color(2.2f, 1.2f, 0.4f));
                    // Cristaleira torta encostada na parede +Z, com vidro na frente.
                    var cabinet = Bevel(parent, "Cristaleira", new Vector3(-hw + 0.65f, 0.95f, hd - 0.25f), new Vector3(1.0f, 1.9f, 0.42f), DarkWood, 0.04f, 0.05f, new Vector3(0, 0, -1.5f));
                    Box(cabinet, "Vidro", new Vector3(0, 0.25f, -0.215f), new Vector3(0.8f, 0.9f, 0.01f), Mirror);
                    Frame(parent, new Vector3(0.6f, 1.65f, hd - 0.08f), new Vector2(0.7f, 0.5f), 180f, 2f, Hex(0x3B4A6B));
                    TapeX(parent, new Vector3(tx + 0.2f, 0, -tw * 0.5f - 0.9f), 10f);
                    break;
                }
            }
        }

        /// <summary>
        /// Corredor da casa gerada (passagem): só uma passadeira no sentido mais comprido e uma marca de fita.
        /// Nada encosta nas paredes laterais, porque as portas das salas podem estar em qualquer ponto delas.
        /// (O estilo Hallway continua sendo o do corredor fixo do P0, com portas a cada 4 m.)
        /// </summary>
        public static void BuildPassage(Transform parent, Vector2 size, int seed)
        {
            var rng = new System.Random(seed);
            bool alongX = size.x >= size.y;
            float length = (alongX ? size.x : size.y) * 0.8f;
            float width = Mathf.Min(0.9f, (alongX ? size.y : size.x) - 0.6f);
            if (width > 0.3f)
            {
                Box(parent, "Passadeira", new Vector3(0, 0.026f, 0), alongX ? new Vector3(length, 0.012f, width) : new Vector3(width, 0.012f, length), Rug);
                Box(parent, "Passadeira borda", new Vector3(0, 0.024f, 0),
                    alongX ? new Vector3(length + 0.1f, 0.008f, width + 0.1f) : new Vector3(width + 0.1f, 0.008f, length + 0.1f), Brass);
            }
            float t = ((float)rng.NextDouble() - 0.5f) * length * 0.5f;
            TapeX(parent, alongX ? new Vector3(t, 0, 0.1f) : new Vector3(0.1f, 0, t), (float)rng.NextDouble() * 60f - 30f);
        }

        // ------------------------------------------------------------------ Móveis compostos

        private static void Table(Transform p, Vector3 at, Vector2 top)
        {
            Bevel(p, "Mesa", at + new Vector3(0, 0.75f, 0), new Vector3(top.x, 0.07f, top.y), Wood, 0.025f);
            float lx = top.x * 0.5f - 0.08f, lz = top.y * 0.5f - 0.08f;
            foreach (var o in new[] { new Vector3(lx, 0, lz), new Vector3(-lx, 0, lz), new Vector3(lx, 0, -lz), new Vector3(-lx, 0, -lz) })
            {
                Bevel(p, "Perna", at + o + new Vector3(0, 0.36f, 0), new Vector3(0.09f, 0.72f, 0.09f), DarkWood, 0.015f, 0.25f);
            }
        }

        private static void Chair(Transform p, Vector3 at, float yaw)
        {
            var c = Group(p, "Cadeira", at, yaw);
            Bevel(c, "Assento", new Vector3(0, 0.46f, 0), new Vector3(0.46f, 0.07f, 0.46f), Wood, 0.02f);
            Bevel(c, "Encosto", new Vector3(0, 0.82f, -0.2f), new Vector3(0.44f, 0.66f, 0.06f), Wood, 0.02f, 0.1f, new Vector3(-6f, 0, 0));
            foreach (var o in new[] { new Vector3(0.18f, 0, 0.18f), new Vector3(-0.18f, 0, 0.18f), new Vector3(0.18f, 0, -0.18f), new Vector3(-0.18f, 0, -0.18f) })
            {
                Bevel(c, "Perna", o + new Vector3(0, 0.215f, 0), new Vector3(0.05f, 0.43f, 0.05f), DarkWood, 0.01f, 0.2f);
            }
        }

        private static void Sofa(Transform p, Vector3 at, float yaw)
        {
            var s = Group(p, "Sofá", at, yaw);
            Bevel(s, "Base", new Vector3(0, 0.22f, 0), new Vector3(2.1f, 0.44f, 0.95f), Fabric, 0.1f);
            Bevel(s, "Encosto", new Vector3(0, 0.66f, -0.36f), new Vector3(2.1f, 0.6f, 0.26f), Fabric, 0.11f, 0f, new Vector3(-8f, 0, 0));
            Bevel(s, "Braço E", new Vector3(-0.98f, 0.5f, 0.02f), new Vector3(0.24f, 0.36f, 0.95f), Fabric, 0.1f);
            Bevel(s, "Braço D", new Vector3(0.98f, 0.5f, 0.02f), new Vector3(0.24f, 0.36f, 0.95f), Fabric, 0.1f);
            Bevel(s, "Almofada E", new Vector3(-0.42f, 0.48f, 0.05f), new Vector3(0.8f, 0.14f, 0.75f), Hex(0x8A4652), 0.06f);
            Bevel(s, "Almofada D", new Vector3(0.42f, 0.48f, 0.05f), new Vector3(0.8f, 0.14f, 0.75f), Hex(0x8A4652), 0.06f).localRotation = Quaternion.Euler(0, 0, -3f);
        }

        private static void Armchair(Transform p, Vector3 at, float yaw)
        {
            var a = Group(p, "Poltrona", at, yaw);
            Bevel(a, "Base", new Vector3(0, 0.22f, 0), new Vector3(0.85f, 0.44f, 0.85f), Fabric, 0.1f);
            Bevel(a, "Encosto", new Vector3(0, 0.75f, -0.34f), new Vector3(0.85f, 0.75f, 0.2f), Fabric, 0.09f, 0.1f, new Vector3(-10f, 0, 0));
            Bevel(a, "Braço E", new Vector3(-0.38f, 0.5f, 0), new Vector3(0.16f, 0.3f, 0.8f), Fabric, 0.07f);
            Bevel(a, "Braço D", new Vector3(0.38f, 0.5f, 0), new Vector3(0.16f, 0.3f, 0.8f), Fabric, 0.07f);
        }

        /// <summary>Relógio de pé exagerado (1,3×): caixa alta afunilada com mostrador claro e pêndulo de latão.</summary>
        private static void GrandfatherClock(Transform p, Vector3 at, float yaw)
        {
            var c = Group(p, "Relógio de pé", at, yaw);
            c.localRotation *= Quaternion.Euler(0, 0, 2f);
            Bevel(c, "Corpo", new Vector3(0, 1.1f, 0), new Vector3(0.55f, 2.2f, 0.4f), DarkWood, 0.04f, 0.12f);
            Bevel(c, "Topo", new Vector3(0, 2.3f, 0), new Vector3(0.62f, 0.22f, 0.46f), DarkWood, 0.06f);
            Cylinder(c, "Mostrador", new Vector3(0, 1.85f, 0.2f), new Vector3(0.36f, 0.02f, 0.36f), Hex(0xD9CDA8)).localRotation = Quaternion.Euler(90f, 0, 0);
            Box(c, "Ponteiro", new Vector3(0.03f, 1.88f, 0.225f), new Vector3(0.02f, 0.13f, 0.01f), Ink).localRotation = Quaternion.Euler(0, 0, -35f);
            Cylinder(c, "Pêndulo", new Vector3(0, 1.0f, 0.205f), new Vector3(0.16f, 0.01f, 0.16f), Brass).localRotation = Quaternion.Euler(90f, 0, 0);
        }

        private static void DirectorChair(Transform p, Vector3 at, float yaw)
        {
            var c = Group(p, "Cadeira de diretor", at, yaw);
            foreach (float sx in new[] { -0.24f, 0.24f })
            {
                Box(c, "X frente", new Vector3(sx, 0.25f, 0.2f), new Vector3(0.04f, 0.6f, 0.04f), Wood).localRotation = Quaternion.Euler(0, 0, sx > 0 ? 30f : -30f);
                Box(c, "X trás", new Vector3(sx, 0.25f, -0.2f), new Vector3(0.04f, 0.6f, 0.04f), Wood).localRotation = Quaternion.Euler(0, 0, sx > 0 ? -30f : 30f);
                Box(c, "Braço", new Vector3(sx, 0.68f, 0), new Vector3(0.05f, 0.04f, 0.5f), Wood);
                Box(c, "Haste", new Vector3(sx, 0.75f, -0.22f), new Vector3(0.04f, 0.6f, 0.04f), Wood);
            }
            Box(c, "Assento (lona)", new Vector3(0, 0.5f, 0), new Vector3(0.48f, 0.03f, 0.42f), Canvas);
            Box(c, "Encosto (lona)", new Vector3(0, 0.92f, -0.22f), new Vector3(0.5f, 0.2f, 0.02f), Canvas);
            Box(c, "Nome (fita)", new Vector3(0, 0.92f, -0.208f), new Vector3(0.32f, 0.06f, 0.01f), Tape);
            Bevel(c, "Megafone", new Vector3(0.05f, 0.56f, 0.05f), new Vector3(0.12f, 0.12f, 0.3f), Hex(0xC8612A), 0.03f, 0.35f, new Vector3(90f, 20f, 0));
        }

        private static void RockingChair(Transform p, Vector3 at, float yaw)
        {
            var c = Group(p, "Cadeira de balanço", at, yaw);
            c.localRotation *= Quaternion.Euler(-5f, 0, 0);
            Bevel(c, "Assento", new Vector3(0, 0.45f, 0), new Vector3(0.5f, 0.06f, 0.5f), DarkWood, 0.02f);
            Bevel(c, "Encosto", new Vector3(0, 0.95f, -0.25f), new Vector3(0.5f, 0.95f, 0.05f), DarkWood, 0.02f, 0.15f, new Vector3(-12f, 0, 0));
            foreach (float sx in new[] { -0.22f, 0.22f })
            {
                Box(c, "Arco", new Vector3(sx, 0.06f, 0), new Vector3(0.04f, 0.05f, 0.85f), DarkWood).localRotation = Quaternion.Euler(-6f, 0, 0);
                Box(c, "Perna", new Vector3(sx, 0.26f, 0.18f), new Vector3(0.04f, 0.4f, 0.04f), DarkWood);
                Box(c, "Perna", new Vector3(sx, 0.26f, -0.18f), new Vector3(0.04f, 0.4f, 0.04f), DarkWood);
            }
        }

        /// <summary>Quadro torto na parede: moldura + "pintura" chapada.</summary>
        private static void Frame(Transform p, Vector3 at, Vector2 size, float yaw, float tilt, Color art)
        {
            var f = Group(p, "Quadro", at, yaw);
            f.localRotation *= Quaternion.Euler(0, 0, tilt);
            Bevel(f, "Moldura", Vector3.zero, new Vector3(size.x, size.y, 0.05f), DarkWood, 0.015f);
            Box(f, "Pintura", new Vector3(0, 0, 0.026f), new Vector3(size.x - 0.1f, size.y - 0.1f, 0.01f), art);
            Box(f, "Olhos", new Vector3(0, size.y * 0.1f, 0.032f), new Vector3(size.x * 0.35f, 0.025f, 0.005f), Hex(0xE8D9A0));
        }

        /// <summary>Abajur: base + cúpula que brilha (a luz real é a lâmpada do teto).</summary>
        private static void Lamp(Transform p, Vector3 at, float scale)
        {
            var l = Group(p, "Abajur", at, 0f);
            l.localScale = Vector3.one * scale;
            Cylinder(l, "Base", new Vector3(0, 0.03f, 0), new Vector3(0.18f, 0.03f, 0.18f), Brass);
            Cylinder(l, "Haste", new Vector3(0, 0.22f, 0), new Vector3(0.03f, 0.2f, 0.03f), Brass);
            var shade = Cylinder(l, "Cúpula", new Vector3(0, 0.47f, 0), new Vector3(0.32f, 0.11f, 0.32f), Hex(0x8A6A40));
            shade.GetComponent<Renderer>().sharedMaterial = EmissiveMat(Hex(0x8A6A40), new Color(1.4f, 0.85f, 0.38f));
        }

        private static void Doll(Transform p, Vector3 at)
        {
            var d = Group(p, "Boneca", at, -25f);
            d.localRotation *= Quaternion.Euler(0, 0, 12f);
            Cylinder(d, "Vestido", new Vector3(0, 0.12f, 0), new Vector3(0.18f, 0.12f, 0.18f), Fabric);
            Primitive(PrimitiveType.Sphere, d, "Cabeça", new Vector3(0, 0.32f, 0), new Vector3(0.15f, 0.15f, 0.15f), Hex(0xE8D2C0));
            Emissive(d, "Olho E", new Vector3(-0.03f, 0.33f, 0.07f), new Vector3(0.025f, 0.025f, 0.01f), Ink, new Color(0.9f, 0.1f, 0.1f));
            Emissive(d, "Olho D", new Vector3(0.03f, 0.33f, 0.07f), new Vector3(0.025f, 0.025f, 0.01f), Ink, new Color(0.9f, 0.1f, 0.1f));
        }

        /// <summary>Marca de ator em fita crepe ("X" no chão).</summary>
        public static void TapeX(Transform p, Vector3 at, float yaw)
        {
            var x = Group(p, "Marca (fita)", at + new Vector3(0, 0.036f, 0), yaw);
            var a = Box(x, "Fita", Vector3.zero, new Vector3(0.38f, 0.004f, 0.05f), Tape);
            var b = Box(x, "Fita", Vector3.zero, new Vector3(0.38f, 0.004f, 0.05f), Tape);
            a.localRotation = Quaternion.Euler(0, 45f, 0);
            b.localRotation = Quaternion.Euler(0, -45f, 0);
        }

        private static void ScatterBoxes(Transform p, System.Random rng, float hw, float hd, int count, Color color)
        {
            for (int i = 0; i < count; i++)
            {
                // ao longo das paredes laterais, deixando o centro livre
                bool left = rng.NextDouble() < 0.5;
                float x = left ? -hw + 0.4f + (float)rng.NextDouble() * 0.5f : hw - 0.4f - (float)rng.NextDouble() * 0.5f;
                float z = -hd + 0.4f + (float)rng.NextDouble() * (hd * 2f - 0.8f);
                if (!left && Mathf.Abs(z) < 0.9f) z = z < 0 ? -0.9f - s0(rng) : 0.9f + s0(rng); // não tapa a porta
                float s = 0.35f + (float)rng.NextDouble() * 0.3f;
                var b = Bevel(p, "Caixa", new Vector3(x, s * 0.5f, z), new Vector3(s, s * 0.9f, s * 1.1f), color, 0.03f, 0.04f);
                b.localRotation = Quaternion.Euler((float)rng.NextDouble() * 4f - 2f, (float)rng.NextDouble() * 40f - 20f, (float)rng.NextDouble() * 4f - 2f);
                Box(b, "Fita", new Vector3(0, s * 0.45f + 0.002f, 0), new Vector3(s * 0.25f, 0.006f, s * 1.12f), Tape).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        private static float s0(System.Random rng) => (float)rng.NextDouble() * 0.6f;

        private static Color BookColor(System.Random rng)
        {
            Color[] books = { Hex(0x7A3B47), Hex(0x3E4F78), Hex(0x6E8B3D), Hex(0xC9A23B), Hex(0x5E5068), Hex(0x8A5A3C) };
            return books[rng.Next(books.Length)];
        }

        // ------------------------------------------------------------------ Primitivas

        private static Transform Group(Transform parent, string name, Vector3 localPos, float yaw)
        {
            var g = new GameObject(name).transform;
            g.SetParent(parent, false);
            g.localPosition = localPos;
            g.localRotation = Quaternion.Euler(0, yaw, 0);
            return g;
        }

        /// <summary>
        /// Caixa chanfrada (sem escala no Transform). 'taper' afunila o topo; 'tilt' = graus (torto de propósito).
        /// Peças pequenas não projetam sombra (guia §7).
        /// </summary>
        public static Transform Bevel(Transform parent, string name, Vector3 localPos, Vector3 size, Color color,
                                      float bevel = 0.03f, float taper = 0f, Vector3 tilt = default)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(tilt);
            go.AddComponent<MeshFilter>().sharedMesh = BevelMesh.Get(size, bevel, taper);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = Mat(color);
            if (Mathf.Max(size.x, Mathf.Max(size.y, size.z)) < 0.5f) r.shadowCastingMode = ShadowCastingMode.Off;
            return go.transform;
        }

        /// <summary>Cubo da Unity escalado (peças finas: fitas, tapetes, ponteiros).</summary>
        public static Transform Box(Transform parent, string name, Vector3 localPos, Vector3 size, Color color)
        {
            return Primitive(PrimitiveType.Cube, parent, name, localPos, size, color);
        }

        /// <summary>Cilindro: size.y é METADE da altura (padrão do cilindro da Unity).</summary>
        public static Transform Cylinder(Transform parent, string name, Vector3 localPos, Vector3 size, Color color)
        {
            return Primitive(PrimitiveType.Cylinder, parent, name, localPos, size, color);
        }

        private static Transform Emissive(Transform parent, string name, Vector3 localPos, Vector3 size, Color baseColor, Color emission)
        {
            var t = Box(parent, name, localPos, size, baseColor);
            var r = t.GetComponent<Renderer>();
            r.sharedMaterial = EmissiveMat(baseColor, emission);
            r.shadowCastingMode = ShadowCastingMode.Off;
            return t;
        }

        private static Transform Primitive(PrimitiveType type, Transform parent, string name, Vector3 localPos, Vector3 size, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var col = go.GetComponent<Collider>();
            Object.DestroyImmediate(col); // imediato: o NavMesh é calculado logo em seguida
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = Mat(color);
            if (Mathf.Max(size.x, Mathf.Max(size.y * (type == PrimitiveType.Cylinder ? 2f : 1f), size.z)) < 0.5f) r.shadowCastingMode = ShadowCastingMode.Off;
            return go.transform;
        }

        /// <summary>Material toon de ambiente por cor, criado uma vez e reaproveitado.</summary>
        public static Material Mat(Color color)
        {
            if (cache.TryGetValue(color, out var m) && m != null) return m;
            m = ToonMaterials.NewEnvironment(color);
            m.name = "HT_Movel_" + ColorUtility.ToHtmlStringRGB(color);
            ToonMaterials.SetSeeThrough(m, true); // móvel alto na frente do ator abre o buraco de visão
            ToonMaterials.SetSeeThroughKeepTop(m, false);
            cache[color] = m;
            return m;
        }

        private static Material EmissiveMat(Color baseColor, Color emission)
        {
            long key = ((long)ColorUtility.ToHtmlStringRGB(baseColor).GetHashCode() << 32) ^ (uint)emission.GetHashCode();
            if (emissiveCache.TryGetValue(key, out var m) && m != null) return m;
            m = ToonMaterials.NewEmissive(baseColor, emission);
            m.name = "HT_Brilho_" + ColorUtility.ToHtmlStringRGB(baseColor);
            ToonMaterials.SetSeeThrough(m, true);
            ToonMaterials.SetSeeThroughKeepTop(m, false);
            emissiveCache[key] = m;
            return m;
        }

        private static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
        }
    }
}
