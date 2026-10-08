using System.Collections.Generic;
using HorrorTycoon.Art;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace HorrorTycoon.EditorTools
{
    /// <summary>
    /// Integração dos personagens do Blender (Art/Characters, ver Art/README_Personagens.md):
    ///   - configura os importadores (FBX Humanoid, clipes com loop, paleta Point/sem mip);
    ///   - gera o AnimatorController único (retarget Humanoid: um controller serve os 4 atores);
    ///   - cria os materiais toon (preset de personagem + HT_Palette.png).
    /// Chamado pelo GreyboxSceneBuilder; também no menu "Horror Tycoon/Visual/Configurar personagens".
    /// </summary>
    public static class HTCharacterSetup
    {
        public const string Folder = "Assets/_HorrorTycoon/Art/Characters";
        public const string AnimsPath = Folder + "/HT_Anims.fbx";
        public const string PalettePath = Folder + "/HT_Palette.png";
        public const string ControllerPath = Folder + "/HT_Actor.controller";

        public static readonly string[] ActorModels = { "HT_Atleta", "HT_Nerd", "HT_Popular", "HT_FinalGirl" };
        private static readonly string[] LoopClips = { "Idle", "Walk", "Talk", "Scared_Loop" };

        // Parâmetros do Animator (o runtime usa os mesmos nomes: ActorView / ActorAnimDriver / ActorIdle).
        public const string ParamSpeed = "Speed";
        public const string ParamMoveRate = "MoveRate";
        public const string ParamTalking = "Talking";
        public const string ParamAfraid = "Afraid";
        public const string ParamScared = "Scared";
        public const string ParamDead = "Dead";

        [MenuItem("Horror Tycoon/Visual/Configurar personagens")]
        public static void SetupMenu()
        {
            Setup();
            Debug.Log("[Personagens] Importadores, controller e materiais prontos.");
        }

        /// <summary>Tudo de uma vez. Retorna false se os FBX ainda não estão no projeto.</summary>
        public static bool Setup()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(AnimsPath) == null) return false;
            ConfigureTexture();
            foreach (var name in ActorModels) ConfigureModel($"{Folder}/{name}.fbx", false);
            ConfigureModel(AnimsPath, true);
            BuildController();
            return true;
        }

        /// <summary>Caminho do modelo do ator pelo nome de exibição (Atleta → HT_Atleta, "Final Girl" → HT_FinalGirl).</summary>
        public static GameObject ModelFor(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return null;
            string key = "HT_" + displayName.Replace(" ", "");
            return AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/{key}.fbx");
        }

        // ================================================================== Importadores

        private static void ConfigureTexture()
        {
            var ti = AssetImporter.GetAtPath(PalettePath) as TextureImporter;
            if (ti == null) return;
            bool dirty = ti.filterMode != FilterMode.Point || ti.mipmapEnabled || !ti.sRGBTexture
                         || ti.textureCompression != TextureImporterCompression.Uncompressed
                         || ti.wrapMode != TextureWrapMode.Clamp || ti.npotScale != TextureImporterNPOTScale.None;
            if (!dirty) return;
            ti.textureType = TextureImporterType.Default;
            ti.filterMode = FilterMode.Point;
            ti.mipmapEnabled = false;
            ti.sRGBTexture = true;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.SaveAndReimport();
        }

        private static void ConfigureModel(string path, bool isAnims)
        {
            var mi = AssetImporter.GetAtPath(path) as ModelImporter;
            if (mi == null) return;
            bool dirty = false;
            void Set<T>(T current, T wanted, System.Action<T> apply)
            {
                if (!EqualityComparer<T>.Default.Equals(current, wanted)) { apply(wanted); dirty = true; }
            }

            Set(mi.globalScale, 1f, v => mi.globalScale = v);
            Set(mi.useFileUnits, true, v => mi.useFileUnits = v);
            // Desvio do README dos personagens: com "Bake Axis Conversion" LIGADO, neste FBX (Blender 5 → Unity 6.3)
            // o ator fica olhando para −Z. DESLIGADO ele olha para +Z, esquerda em −X e raiz sem rotação
            // (o −90° fica só nos filhos "Corpo" e "*_Rig", o que não afeta o Humanoid). Conferido no import.
            Set(mi.bakeAxisConversion, false, v => mi.bakeAxisConversion = v);
            Set(mi.importBlendShapes, false, v => mi.importBlendShapes = v);
            Set(mi.importNormals, ModelImporterNormals.Import, v => mi.importNormals = v);
            Set(mi.meshCompression, ModelImporterMeshCompression.Off, v => mi.meshCompression = v);
            Set(mi.materialImportMode, ModelImporterMaterialImportMode.None, v => mi.materialImportMode = v);
            Set(mi.importCameras, false, v => mi.importCameras = v);
            Set(mi.importLights, false, v => mi.importLights = v);
            Set(mi.animationType, ModelImporterAnimationType.Human, v => mi.animationType = v);
            Set(mi.avatarSetup, ModelImporterAvatarSetup.CreateFromThisModel, v => mi.avatarSetup = v);
            Set(mi.importAnimation, isAnims, v => mi.importAnimation = v);

            if (dirty)
            {
                // O mapeamento Humanoid guardado no .meta inclui a pose de TODOS os transforms (ex.: "Rosto",
                // "*_Rig"). Se a conversão de eixos mudou, essa pose fica velha e o Animator a reaplica em
                // runtime (o rosto ia parar na nuca). Zerar faz a Unity remapear com a pose atual.
                var hd = mi.humanDescription;
                hd.skeleton = new SkeletonBone[0];
                hd.human = new HumanBone[0];
                mi.humanDescription = hd;
                mi.SaveAndReimport();
            }

            // O auto-mapeamento do Humanoid "adivinha" olhos e mandíbula com as peças do rosto
            // (ex.: Sobrancelha_E → LeftEye, Boca_Feliz → Jaw). Aí o Animator sobrescreve essas peças a cada
            // quadro e as expressões param de funcionar. O rosto é do ActorView: tira esses três ossos.
            var desc = mi.humanDescription;
            var kept = new List<HumanBone>();
            foreach (var hb in desc.human)
            {
                if (hb.humanName == "LeftEye" || hb.humanName == "RightEye" || hb.humanName == "Jaw") continue;
                kept.Add(hb);
            }
            // E o contrário: o auto-mapeamento às vezes pula Chest e os Toes (o rig do Blender tem os 21 ossos
            // com os nomes do Humanoid). Mapeia explicitamente quem existir no esqueleto.
            bool added = false;
            foreach (var wanted in new[] { "Chest", "LeftToes", "RightToes" })
            {
                bool mapped = kept.Exists(h => h.humanName == wanted);
                bool exists = System.Array.Exists(desc.skeleton, sb => sb.name == wanted);
                if (mapped || !exists) continue;
                var hb = new HumanBone { humanName = wanted, boneName = wanted };
                hb.limit.useDefaultValues = true;
                kept.Add(hb);
                added = true;
            }
            if (added || kept.Count != desc.human.Length)
            {
                desc.human = kept.ToArray();
                mi.humanDescription = desc;
                mi.SaveAndReimport();
            }

            if (!isAnims) return;

            // Clipes: loop nos ciclos; raiz "Bake Into Pose" (o NavMeshAgent move o ator; o pulo/queda ficam na pose).
            var defaults = mi.defaultClipAnimations;
            var clips = new ModelImporterClipAnimation[defaults.Length];
            bool clipsDirty = mi.clipAnimations == null || mi.clipAnimations.Length != defaults.Length;
            for (int i = 0; i < defaults.Length; i++)
            {
                var c = defaults[i];
                c.name = c.takeName;
                c.loopTime = System.Array.IndexOf(LoopClips, c.takeName) >= 0;
                c.loopPose = false;
                c.lockRootRotation = true;
                c.keepOriginalOrientation = true;
                c.lockRootHeightY = true;
                c.keepOriginalPositionY = true;
                c.lockRootPositionXZ = true;
                c.keepOriginalPositionXZ = true;
                clips[i] = c;
                if (!clipsDirty)
                {
                    var cur = mi.clipAnimations[i];
                    clipsDirty = cur.name != c.name || cur.loopTime != c.loopTime || !cur.lockRootRotation || !cur.lockRootHeightY || !cur.lockRootPositionXZ;
                }
            }
            if (clipsDirty)
            {
                mi.clipAnimations = clips;
                mi.SaveAndReimport();
            }
        }

        public static Avatar AvatarOf(string path)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (o is Avatar a) return a;
            }
            return null;
        }

        public static AnimationClip ClipOf(string name)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(AnimsPath))
            {
                if (o is AnimationClip c && c.name == name && !c.name.StartsWith("__preview")) return c;
            }
            return null;
        }

        // ================================================================== Animator

        /// <summary>
        /// Controller único: Idle ⇄ Walk (Speed, velocidade da passada em MoveRate), Talk (Talking),
        /// Scared (gatilho) → Scared_Loop enquanto Afraid → Idle, Death (gatilho, sem saída).
        /// Recriado a cada build (o builder religa a cena).
        /// </summary>
        public static AnimatorController BuildController()
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null) AssetDatabase.DeleteAsset(ControllerPath);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            ctrl.AddParameter(ParamSpeed, AnimatorControllerParameterType.Float);
            ctrl.AddParameter(ParamMoveRate, AnimatorControllerParameterType.Float);
            ctrl.AddParameter(ParamTalking, AnimatorControllerParameterType.Bool);
            ctrl.AddParameter(ParamAfraid, AnimatorControllerParameterType.Bool);
            ctrl.AddParameter(ParamScared, AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter(ParamDead, AnimatorControllerParameterType.Trigger);
            var ps = ctrl.parameters;
            foreach (var p in ps) if (p.name == ParamMoveRate) p.defaultFloat = 1f;
            ctrl.parameters = ps;

            var sm = ctrl.layers[0].stateMachine;
            var idle = sm.AddState("Idle", new Vector3(300, 0, 0));
            var walk = sm.AddState("Walk", new Vector3(300, 120, 0));
            var talk = sm.AddState("Talk", new Vector3(560, 0, 0));
            var scared = sm.AddState("Scared", new Vector3(560, 200, 0));
            var scaredLoop = sm.AddState("Scared_Loop", new Vector3(820, 200, 0));
            var death = sm.AddState("Death", new Vector3(820, 360, 0));
            idle.motion = ClipOf("Idle");
            walk.motion = ClipOf("Walk");
            walk.speedParameterActive = true;
            walk.speedParameter = ParamMoveRate;
            talk.motion = ClipOf("Talk");
            scared.motion = ClipOf("Scared");
            scaredLoop.motion = ClipOf("Scared_Loop");
            death.motion = ClipOf("Death");
            sm.defaultState = idle;

            Link(idle, walk, 0.15f, Cond(AnimatorConditionMode.Greater, 0.15f, ParamSpeed));
            Link(walk, idle, 0.2f, Cond(AnimatorConditionMode.Less, 0.1f, ParamSpeed));
            Link(idle, talk, 0.25f, Cond(AnimatorConditionMode.If, 0f, ParamTalking));
            Link(talk, idle, 0.25f, Cond(AnimatorConditionMode.IfNot, 0f, ParamTalking));
            Link(talk, walk, 0.15f, Cond(AnimatorConditionMode.Greater, 0.15f, ParamSpeed));

            // Susto: de qualquer estado (menos a morte, que não tem saída e nunca recebe o gatilho).
            var toScared = sm.AddAnyStateTransition(scared);
            toScared.AddCondition(AnimatorConditionMode.If, 0f, ParamScared);
            toScared.duration = 0.08f;
            toScared.canTransitionToSelf = false;
            var sToLoop = scared.AddTransition(scaredLoop);
            sToLoop.hasExitTime = true; sToLoop.exitTime = 0.95f; sToLoop.duration = 0.1f;
            sToLoop.AddCondition(AnimatorConditionMode.If, 0f, ParamAfraid);
            var sToIdle = scared.AddTransition(idle);
            sToIdle.hasExitTime = true; sToIdle.exitTime = 0.95f; sToIdle.duration = 0.25f;
            sToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, ParamAfraid);
            Link(scaredLoop, idle, 0.3f, Cond(AnimatorConditionMode.IfNot, 0f, ParamAfraid));
            Link(scaredLoop, walk, 0.15f, Cond(AnimatorConditionMode.Greater, 0.15f, ParamSpeed));

            var toDeath = sm.AddAnyStateTransition(death);
            toDeath.AddCondition(AnimatorConditionMode.If, 0f, ParamDead);
            toDeath.duration = 0.1f;
            toDeath.canTransitionToSelf = false;

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            return ctrl;
        }

        private struct C
        {
            public AnimatorConditionMode Mode;
            public float Threshold;
            public string Param;
        }

        private static C Cond(AnimatorConditionMode mode, float threshold, string param) => new C { Mode = mode, Threshold = threshold, Param = param };

        private static void Link(AnimatorState from, AnimatorState to, float duration, C c)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = duration;
            t.AddCondition(c.Mode, c.Threshold, c.Param);
        }

        // ================================================================== Materiais

        /// <summary>Material toon de personagem com a paleta (um por ator, para ajustes individuais).</summary>
        public static Material ActorMaterial(string actorKey)
        {
            var m = HTVisualSetup.Character($"M_Personagem_{actorKey}", Color.white);
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(PalettePath));
            m.SetFloat("_GradientScale", 0.6f);  // origem nos pés, 1,75 m
            m.SetFloat("_GradientOffset", 0f);
            // Especular duro só faz sentido em cabelo/óculos: no corpo inteiro vira ruído. Fica desligado.
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Peças pequenas do rosto (pupila, sobrancelha, boca): paleta, casca fina.</summary>
        public static Material FaceMaterial()
        {
            var m = HTVisualSetup.Character("M_Personagem_Rosto", Color.white, 1.2f);
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(PalettePath));
            m.SetFloat("_GradientStrength", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Branco do olho: brilha no escuro (gag de Scooby-Doo), com casca fina escura.</summary>
        public static Material EyeMaterial()
        {
            var m = HTVisualSetup.Character("M_Personagem_Olho", Color.white, 1.4f);
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(PalettePath));
            m.SetFloat("_GradientStrength", 0f);
            m.SetFloat("_OutlineFromBase", 0f);
            m.SetColor("_OutlineColor", HTVisualSetup.Hex(0x15121A));
            m.SetColor(ToonMaterials.EmissionColorId, new Color(0.55f, 0.54f, 0.5f));
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
