using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace HorrorTycoon.EditorTools
{
    /// <summary>
    /// Assets da HUD nova (UI Toolkit), criados por código e de forma idempotente (só cria o que falta):
    ///   - Font Assets (TextCore, dinâmicos) a partir dos .ttf de Art/UI/Fonts → "Nome_SDF.asset"
    ///     (o HudTheme.uss aponta para eles);
    ///   - PanelSettings da HUD (Scale With Screen Size, 1920×1080, match na altura) + tema de runtime (.tss);
    ///   - ícones SVG de Art/UI/Icons importados como Vector Image do UI Toolkit (HudIconPostprocessor).
    /// Menu: Horror Tycoon/HUD/Criar assets da HUD. O GreyboxSceneBuilder chama antes de montar as cenas.
    /// </summary>
    public static class HudAssetsSetup
    {
        public const string UiFolder = "Assets/_HorrorTycoon/Art/UI";
        public const string FontsFolder = UiFolder + "/Fonts";
        public const string IconsFolder = UiFolder + "/Icons";
        public const string PanelSettingsPath = UiFolder + "/HudPanelSettings.asset";
        public const string ThemePath = UiFolder + "/HudRuntimeTheme.tss";
        public const string ThemeUssPath = UiFolder + "/HudTheme.uss";
        public const string HudUssPath = UiFolder + "/Hud.uss";
        public const string CompositeShaderPath = UiFolder + "/HudComposite.shader";

        [MenuItem("Horror Tycoon/HUD/Criar assets da HUD")]
        public static void EnsureHudAssets()
        {
            bool fontsCreated = EnsureFontAssets();
            EnsureTheme();
            EnsurePanelSettings();
            EnsureIconImport();
            if (fontsCreated)
            {
                // As folhas de estilo apontam para os Font Assets: reimporta para resolver as referências.
                AssetDatabase.ImportAsset(ThemeUssPath, ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset(HudUssPath, ImportAssetOptions.ForceUpdate);
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>Um Font Asset dinâmico por .ttf (Lilita One, Atkinson Hyperlegible Next, Permanent Marker).</summary>
        private static bool EnsureFontAssets()
        {
            bool created = false;
            if (!AssetDatabase.IsValidFolder(FontsFolder)) return false;
            foreach (var guid in AssetDatabase.FindAssets("t:Font", new[] { FontsFolder }))
            {
                string ttf = AssetDatabase.GUIDToAssetPath(guid);
                if (!ttf.EndsWith(".ttf") && !ttf.EndsWith(".otf")) continue;
                string target = Path.Combine(Path.GetDirectoryName(ttf), Path.GetFileNameWithoutExtension(ttf) + "_SDF.asset").Replace('\\', '/');
                if (AssetDatabase.LoadAssetAtPath<FontAsset>(target) != null) continue;
                var font = AssetDatabase.LoadAssetAtPath<Font>(ttf);
                if (font == null) continue;
                var fa = FontAsset.CreateFontAsset(font);
                if (fa == null)
                {
                    Debug.LogWarning($"[HorrorTycoon] Não deu para criar Font Asset de {ttf}.");
                    continue;
                }
                fa.name = Path.GetFileNameWithoutExtension(target);
                AssetDatabase.CreateAsset(fa, target);
                if (fa.atlasTextures != null)
                {
                    for (int i = 0; i < fa.atlasTextures.Length; i++)
                    {
                        var tex = fa.atlasTextures[i];
                        if (tex == null) continue;
                        tex.name = $"{fa.name} Atlas{(i > 0 ? " " + i : "")}";
                        AssetDatabase.AddObjectToAsset(tex, fa);
                    }
                }
                if (fa.material != null)
                {
                    fa.material.name = fa.name + " Material";
                    AssetDatabase.AddObjectToAsset(fa.material, fa);
                }
                EditorUtility.SetDirty(fa);
                created = true;
                Debug.Log($"[HorrorTycoon] Font Asset criado: {target}");
            }
            if (created) AssetDatabase.SaveAssets();
            return created;
        }

        private static void EnsureTheme()
        {
            if (AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath) != null) return;
            File.WriteAllText(ThemePath, "@import url(\"unity-theme://default\");\n");
            AssetDatabase.ImportAsset(ThemePath, ImportAssetOptions.ForceUpdate);
        }

        private static void EnsurePanelSettings()
        {
            var ps = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (ps == null)
            {
                ps = ScriptableObject.CreateInstance<PanelSettings>();
                ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                ps.referenceResolution = new Vector2Int(1920, 1080);
                ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                ps.match = 1f; // casa pela altura
                ps.sortingOrder = 10;
                AssetDatabase.CreateAsset(ps, PanelSettingsPath);
            }
            if (ps.themeStyleSheet == null)
            {
                ps.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
                EditorUtility.SetDirty(ps);
            }
        }

        /// <summary>Reimporta ícones que ainda não são Vector Image (o postprocessor ajusta o tipo).</summary>
        private static void EnsureIconImport()
        {
            if (!AssetDatabase.IsValidFolder(IconsFolder)) return;
            foreach (var guid in AssetDatabase.FindAssets("", new[] { IconsFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".svg")) continue;
                if (AssetDatabase.LoadAssetAtPath<VectorImage>(path) != null) continue;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
        }

        public static PanelSettings LoadPanelSettings() => AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);

        public static StyleSheet[] LoadStyleSheets() => new[]
        {
            AssetDatabase.LoadAssetAtPath<StyleSheet>(ThemeUssPath),
            AssetDatabase.LoadAssetAtPath<StyleSheet>(HudUssPath)
        };
    }

    /// <summary>SVG da pasta de ícones da HUD → "UI Toolkit Vector Image" (nítido em qualquer escala, tinta pelo USS).</summary>
    public class HudIconPostprocessor : AssetPostprocessor
    {
        private void OnPreprocessAsset()
        {
            if (!assetPath.StartsWith(HudAssetsSetup.IconsFolder) || !assetPath.EndsWith(".svg")) return;
            var imp = assetImporter;
            var prop = imp.GetType().GetProperty("SvgType");
            if (prop == null || !prop.PropertyType.IsEnum) return;
            var value = System.Enum.Parse(prop.PropertyType, "VectorImage");
            if (!Equals(prop.GetValue(imp), value)) prop.SetValue(imp, value);
        }
    }
}
