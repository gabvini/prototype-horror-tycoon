using HorrorTycoon.Rooms.Building;
using UnityEditor;
using UnityEngine;

namespace HorrorTycoon.EditorTools
{
    /// <summary>
    /// Cria o KIT DE ARTE PROVISÓRIO da casa gerada (HouseArtKit) com os MESMOS materiais da casa fixa do P0
    /// (M_Parede, M_Piso, M_Batente, névoas, lâmpada, janelas, varanda...), criados pelo HTVisualSetup.
    /// Idempotente: se o asset já existe, NÃO mexe nele (as trocas de arte do Gabriel ficam salvas).
    /// Para voltar ao provisório: apague o asset e rode o menu de novo.
    /// </summary>
    public static class HouseArtKitSetup
    {
        public const string KitFolder = "Assets/_HorrorTycoon/Art/Kits";
        public const string KitPath = KitFolder + "/HouseArtKit_Provisorio.asset";

        [MenuItem("Horror Tycoon/Visual/Criar kit de arte da casa (não sobrescreve)")]
        public static void CreateMenu()
        {
            var kit = EnsureDefaultKit();
            Selection.activeObject = kit;
            Debug.Log($"[Visual] Kit de arte da casa pronto em {KitPath}");
        }

        public static HouseArtKit EnsureDefaultKit()
        {
            var existing = AssetDatabase.LoadAssetAtPath<HouseArtKit>(KitPath);
            if (existing != null) return existing;

            HTVisualSetup.EnsureFolder("Assets", "_HorrorTycoon");
            HTVisualSetup.EnsureFolder("Assets/_HorrorTycoon", "Art");
            HTVisualSetup.EnsureFolder("Assets/_HorrorTycoon/Art", "Kits");
            HTVisualSetup.EnsureFolder("Assets/_HorrorTycoon", "Materials");

            var kit = ScriptableObject.CreateInstance<HouseArtKit>();
            Fill(kit);
            AssetDatabase.CreateAsset(kit, KitPath);
            AssetDatabase.SaveAssets();
            return kit;
        }

        /// <summary>Mesmos nomes e parâmetros usados pelo GreyboxSceneBuilder (os assets são compartilhados).</summary>
        private static void Fill(HouseArtKit k)
        {
            Color darkWood = HTVisualSetup.Hex(0x4A3328);

            k.wallMaterial = HTVisualSetup.Wall("M_Parede");
            k.floorMaterial = HTVisualSetup.Environment("M_Piso", Color.white, 0f);
            k.foundationMaterial = HTVisualSetup.Environment("M_Fundacao", HTVisualSetup.Hex(0x3A3440), 0.25f);
            k.trimMaterial = HTVisualSetup.Environment("M_Batente", darkWood, 0.1f);
            k.eaveMaterial = HTVisualSetup.Environment("M_Beiral", HTVisualSetup.Hex(0x2A2236), 0f);

            k.fogVolumeMaterial = HTVisualSetup.Fx("M_FX_NevoaSala", new Color(0.135f, 0.15f, 0.23f, 0.8f),
                HTVisualSetup.FxBlend.Alpha, 1.1f, new Vector3(0.06f, 0.03f, 0.04f), 0.75f, 0.25f, 1f, 0.45f);
            k.fogLayerMaterial = HTVisualSetup.Fx("M_FX_NevoaCamada", new Color(0.17f, 0.18f, 0.27f, 0.55f),
                HTVisualSetup.FxBlend.Alpha, 0.8f, new Vector3(-0.05f, 0.02f, 0.06f), 0.9f, 0.2f);
            k.fogTapeMaterial = HTVisualSetup.Fx("M_FX_FitaApagada", new Color(HTVisualSetup.Tape.r, HTVisualSetup.Tape.g, HTVisualSetup.Tape.b, 0.25f),
                HTVisualSetup.FxBlend.Alpha, 1f, Vector3.zero, 0f, 0f);

            k.lampCordMaterial = HTVisualSetup.Environment("M_Fio", HTVisualSetup.Hex(0x15121A), 0f);
            k.lampShadeMaterial = HTVisualSetup.Emissive("M_Cupula", HTVisualSetup.Hex(0x8A6A40), new Color(0.3f, 0.18f, 0.07f));
            k.lampBulbMaterial = HTVisualSetup.Emissive("M_Bulbo", HTVisualSetup.Hex(0xFFE2B0), new Color(2.4f, 1.7f, 0.9f));
            k.lampShaftMaterial = HTVisualSetup.Fx("M_FX_RaioLampada", new Color(1f, 0.7f, 0.36f, 0.075f), HTVisualSetup.FxBlend.Additive,
                0.9f, new Vector3(0f, -0.05f, 0f), 0.25f, 0.4f, 0f, 1f, 1.4f, true);
            k.lampShaftMesh = HTVisualSetup.ConeMesh();

            k.windowFrameMaterial = HTVisualSetup.Environment("M_Janela_Moldura", darkWood, 0f);
            k.windowPaneMaterial = HTVisualSetup.Emissive("M_Janela_Vidro", HTVisualSetup.Hex(0x1E2A44), new Color(0.02f, 0.025f, 0.05f));
            k.windowBeamMaterial = HTVisualSetup.Fx("M_FX_RaioJanela", new Color(1f, 0.7f, 0.36f, 0.07f), HTVisualSetup.FxBlend.Additive,
                0.8f, new Vector3(0.02f, -0.03f, 0f), 0.3f, 0.35f, 0f, 1f, 0.9f, true);

            k.deckMaterial = HTVisualSetup.Environment("M_Deque", HTVisualSetup.Hex(0x5C4033), 0.1f);
            k.postMaterial = HTVisualSetup.Environment("M_Coluna", HTVisualSetup.Hex(0x7A6A80), 0.2f);
            k.porchFixtureMaterial = HTVisualSetup.Environment("M_Luminaria", HTVisualSetup.Hex(0x3A2A22), 0f);
            k.pathMaterial = HTVisualSetup.Environment("M_Caminho", HTVisualSetup.Hex(0x4A3B30), 0f);
        }
    }
}
