using System.Collections.Generic;
using HorrorTycoon.Actors;
using HorrorTycoon.Core;
using HorrorTycoon.Rooms;
using HorrorTycoon.Rooms.Building;
using HorrorTycoon.Rooms.Generation;
using HorrorTycoon.Run;
using HorrorTycoon.Scoring;
using UnityEditor;
using UnityEngine;

namespace HorrorTycoon.EditorTools
{
    /// <summary>
    /// Cria os assets de conteúdo do Protótipo 1 em Assets/_HorrorTycoon/Data/P1.
    /// NÃO sobrescreve: se um asset já existe, ele é mantido (suas edições no Inspector ficam salvas).
    /// Para "resetar" um item, apague o asset e rode o menu de novo.
    /// Todos os nomes e números são PLACEHOLDERS do GDD Vivo.
    /// </summary>
    public static class ContentBuilder
    {
        public const string DataRoot = "Assets/_HorrorTycoon/Data/P1";
        public const string ContentPath = DataRoot + "/Conteudo_P1.asset";
        /// <summary>Parâmetros da casa gerada. NÃO fica no Catálogo: a cena da casa procedural recebe por campo próprio.</summary>
        public const string HouseGenPath = DataRoot + "/Geracao/Casa_Padrao.asset";

        [MenuItem("Horror Tycoon/Criar Conteúdo P1 (não sobrescreve)")]
        public static void CreateMenu()
        {
            var content = EnsureP1Content();
            Selection.activeObject = content;
            Debug.Log($"[HorrorTycoon] Conteúdo P1 pronto em {ContentPath}");
        }

        public static GameContentDef EnsureP1Content()
        {
            EnsureFolder("Assets/_HorrorTycoon", "Data");
            EnsureFolder("Assets/_HorrorTycoon/Data", "P1");
            foreach (var f in new[] { "Tags", "Atores", "Salas", "Encontros", "Elementos", "Cenas", "Ferramentas", "Viloes" })
            {
                EnsureFolder(DataRoot, f);
            }

            // ---------------------------------------------------------- Subgêneros (tags)
            TagDef slasher = Tag("Slasher", new Color(0.85f, 0.25f, 0.25f));
            TagDef sobrenatural = Tag("Sobrenatural", new Color(0.55f, 0.45f, 0.95f));

            // ---------------------------------------------------------- Cenas dirigidas (payoffs)
            PayoffDef susto = Make<PayoffDef>("Cenas/Cena_Susto", p => p.Setup("Susto",
                "Um susto encenado. Não mata, mas assusta o ator.", new Color(0.6f, 0.4f, 0.9f), 60, 25, false, 0));
            PayoffDef morte = Make<PayoffDef>("Cenas/Cena_Morte", p => p.Setup("Morte",
                "A cena de morte. O ator sai do filme. Só a partir do Ato 2.", new Color(0.9f, 0.2f, 0.2f), 120, 0, true, 1));
            var both = new List<PayoffDef> { susto, morte };
            var onlyDeath = new List<PayoffDef> { morte };

            // ---------------------------------------------------------- Elementos de cena
            ElementDef arma = Element("Arma", "Uma faca. Faz toda a diferença numa cena de morte.",
                new Color(0.9f, 0.4f, 0.35f), slasher, ElementHolder.Actor, 1, onlyDeath);
            ElementDef luz = Element("LuzApagada", "A luz da sala queimou.",
                new Color(0.75f, 0.75f, 0.55f), null, ElementHolder.Room, 1, both, "Luz apagada");
            ElementDef presenca = Element("Presenca", "Algo habita esta sala.",
                new Color(0.65f, 0.55f, 1f), sobrenatural, ElementHolder.Room, 1, both, "Presença");
            ElementDef boneca = Element("BonecaAntiga", "Um objeto amaldiçoado que o ator carrega.",
                new Color(0.8f, 0.6f, 1f), sobrenatural, ElementHolder.Actor, 1, both, "Boneca antiga");
            ElementDef armadilha = Element("Armadilha", "Uma armadilha de urso escondida.",
                new Color(0.95f, 0.55f, 0.3f), slasher, ElementHolder.Room, 1, onlyDeath);
            ElementDef machado = Element("Machado", "Estava escondido no alçapão. Forte.",
                new Color(1f, 0.3f, 0.3f), slasher, ElementHolder.Actor, 2, onlyDeath);
            ElementDef isolado = Element("Isolado", "O ator está sozinho na sala (automático).",
                new Color(0.95f, 0.7f, 0.5f), slasher, ElementHolder.Actor, 1, both, null, ElementAutoRule.AloneInRoom, 0);
            ElementDef apavorado = Element("Apavorado", "O ator está com pavor 60+ (automático).",
                new Color(0.9f, 0.9f, 0.9f), null, ElementHolder.Actor, 1, both, null, ElementAutoRule.PavorAtLeast, 60);

            // ---------------------------------------------------------- Ferramenta
            ToolDef chave = Make<ToolDef>("Ferramentas/Ferramenta_ChaveDeFenda",
                t => t.Setup("Chave de fenda", "Abre coisas aparafusadas. Tem algo no porão..."));
            // Protótipo 2: ferramentas contra o vilão (gastas ao repelir).
            ToolDef facaTool = Make<ToolDef>("Ferramentas/Ferramenta_Faca",
                t => t.Setup("Faca", "Quem estiver com ela (ou junto de quem está) repele o Slasher. Gasta ao usar.", slasher));
            ToolDef crucifixo = Make<ToolDef>("Ferramentas/Ferramenta_Crucifixo",
                t => t.Setup("Crucifixo", "Afasta presenças sobrenaturais. Gasta ao usar.", sobrenatural));

            // ---------------------------------------------------------- Encontros
            EncounterDef nada = Encounter("Nada", "Nada de mais", "Só poeira e silêncio.", 20, 0, null, null);
            EncounterDef barulho = Encounter("Barulho", "Barulho estranho", "Algo bateu no andar de cima.", 40, 15, null, null);
            EncounterDef faca = Encounter("Faca", "Faca na gaveta", "Uma faca afiada esquecida.", 30, 0, arma, null);
            EncounterDef luzQueima = Encounter("LuzQueima", "A luz queima", "A lâmpada estoura. Escuridão.", 30, 5, luz, null);
            EncounterDef sussurro = Encounter("Sussurro", "Sussurros", "Vozes vindas da parede.", 40, 10, presenca, null);
            EncounterDef bonecaEnc = Encounter("Boneca", "Boneca antiga", "Os olhos dela parecem seguir você.", 30, 5, boneca, null);
            EncounterDef armadilhaEnc = Encounter("Armadilha", "Armadilha de urso", "Alguém preparou isso de propósito.", 30, 5, armadilha, null);
            EncounterDef conversa = Encounter("Conversa", "Conversa", "Um momento de calma entre amigos.", 30, -15, null, null);
            EncounterDef chaveEnc = Encounter("Chave", "Caixa de ferramentas", "Uma chave de fenda!", 20, 0, null, chave);
            EncounterDef crucifixoEnc = Encounter("Crucifixo", "Crucifixo na parede",
                "Um crucifixo antigo pendurado na parede. Alguém o leva.", 20, -5, null, crucifixo);

            RoomDef.WeightedEncounter W(EncounterDef e, float w) => new RoomDef.WeightedEncounter { encounter = e, weight = w };

            // ---------------------------------------------------------- Salas (ordem = grade: frente E→D, depois fundos E→D)
            var salas = new List<RoomDef>
            {
                Room("Cozinha", "Cozinha", "Cheiro de comida velha. Gavetas cheias.", new Color(0.45f, 0.38f, 0.3f),
                    new List<string> { "Gavetas e utensílios: pode haver algo afiado", "Bom lugar para conversar e se acalmar" },
                    new List<RoomDef.WeightedEncounter> { W(nada, 1), W(faca, 3), W(armadilhaEnc, 1), W(conversa, 2), W(chaveEnc, 2) },
                    new List<PayoffDef>(), null),
                Room("SalaDeEstar", "Sala de estar", "A entrada da casa. Parece segura… por enquanto.", new Color(0.38f, 0.4f, 0.45f),
                    new List<string> { "Parece o lugar mais seguro da casa", "Coisas foram deixadas para trás" },
                    new List<RoomDef.WeightedEncounter> { W(nada, 2), W(barulho, 1), W(conversa, 3), W(chaveEnc, 2), W(bonecaEnc, 1) },
                    new List<PayoffDef>(), null),
                Room("Banheiro", "Banheiro", "O espelho está rachado.", new Color(0.36f, 0.42f, 0.42f),
                    new List<string> { "A luz falha por aqui", "Algo parece observar pelo espelho" },
                    new List<RoomDef.WeightedEncounter> { W(barulho, 2), W(luzQueima, 2), W(sussurro, 2), W(nada, 1) },
                    new List<PayoffDef> { susto }, null),
                Room("Quarto", "Quarto", "Brinquedos antigos por todo lado.", new Color(0.42f, 0.35f, 0.38f),
                    new List<string> { "Objetos antigos, alguns estranhos", "Há uma presença no ar" },
                    new List<RoomDef.WeightedEncounter> { W(bonecaEnc, 3), W(sussurro, 2), W(chaveEnc, 1), W(barulho, 1), W(crucifixoEnc, 2) },
                    new List<PayoffDef> { susto }, null),
                Room("Porao", "Porão", "Úmido, escuro, e alguém esteve aqui.", new Color(0.25f, 0.24f, 0.28f),
                    new List<string> { "Perigoso: dá medo só de descer", "Escuro, com coisas velhas espalhadas" },
                    new List<RoomDef.WeightedEncounter> { W(luzQueima, 2), W(armadilhaEnc, 3), W(barulho, 2), W(faca, 1) },
                    new List<PayoffDef> { morte },
                    new RoomDef.LockedSpot { name = "Alçapão", requiredTool = chave, reward = machado, text = "Os parafusos cedem. Dentro do alçapão… um machado." }),
                Room("Sotao", "Sótão", "Ninguém sobe aqui há anos.", new Color(0.33f, 0.3f, 0.25f),
                    new List<string> { "Há uma presença aqui em cima", "Escuro e abafado" },
                    new List<RoomDef.WeightedEncounter> { W(sussurro, 3), W(bonecaEnc, 2), W(luzQueima, 2), W(barulho, 1), W(crucifixoEnc, 2) },
                    new List<PayoffDef> { susto, morte }, null),
            };

            // ---------------------------------------------------------- Atores
            var atores = new List<ActorDef>
            {
                Actor("Atleta", "Atleta", "Rápido. (Protótipo 2: habilidade de andar desligada; andar é grátis.)", new Color(0.8f, 0.25f, 0.25f), 2),
                Actor("Nerd", "Nerd", "Curioso. (habilidade a definir)", new Color(0.25f, 0.45f, 0.85f), 1),
                Actor("Popular", "Popular", "Popular. (habilidade a definir)", new Color(0.9f, 0.5f, 0.75f), 1),
                Actor("FinalGirl", "Final Girl", "Resistente. (habilidade a definir)", new Color(0.9f, 0.75f, 0.2f), 1),
            };

            // ---------------------------------------------------------- Vilões
            var viloes = new List<VillainDef>
            {
                Make<VillainDef>("Viloes/Vilao_Mascarado", v => v.Setup("O Mascarado",
                    "Slasher. Armas, armadilhas e vítimas isoladas valem em dobro.", new Color(0.85f, 0.25f, 0.25f), slasher)),
                Make<VillainDef>("Viloes/Vilao_Entidade", v => v.Setup("A Entidade",
                    "Sobrenatural. Presenças e objetos amaldiçoados valem em dobro.", new Color(0.55f, 0.45f, 0.95f), sobrenatural, VillainLook.Ghost)),
            };

            // ---------------------------------------------------------- Formato, regras, catálogo
            var formato = Make<FilmFormatDef>("Formato_Curta", null);
            var regras = Make<GameRulesDef>("Regras", null);

            // Visual por cômodo: estilo de móveis, cor da luz e se ela pisca.
            void Visual(RoomDef r, FurnitureStyle style, Color light, bool flicker)
            {
                if (r.Furniture == FurnitureStyle.None) { r.SetupVisual(style, light, flicker); EditorUtility.SetDirty(r); }
            }
            Color warm = new Color(1f, 0.8f, 0.55f), cold = new Color(0.65f, 0.75f, 1f), sick = new Color(0.75f, 0.9f, 0.6f);
            Visual(salas[0], FurnitureStyle.Kitchen, warm, false);
            Visual(salas[1], FurnitureStyle.Living, warm, false);
            Visual(salas[2], FurnitureStyle.Bathroom, sick, true);
            Visual(salas[3], FurnitureStyle.Bedroom, warm, false);
            Visual(salas[4], FurnitureStyle.Basement, cold, true);
            Visual(salas[5], FurnitureStyle.Attic, cold, false);

            RoomDef corredor = Room("Corredor", "Corredor", "Liga a entrada a todos os cômodos. Os atores se encontram aqui.",
                new Color(0.3f, 0.27f, 0.25f), new List<string>(), new List<RoomDef.WeightedEncounter>(), new List<PayoffDef>(), null);
            Visual(corredor, FurnitureStyle.Hallway, warm, true);

            var content = Make<GameContentDef>("Conteudo_P1", c => c.Setup(formato, regras, atores, salas, corredor, true, viloes,
                new List<ElementDef> { isolado, apavorado }));

            EnsureHouseGen(salas, corredor, warm);
            UpgradePrototype2(regras, morte, faca, facaTool, crucifixo, crucifixoEnc, salas[3], salas[5], viloes[1], slasher, sobrenatural);
            UpgradePrototype3(content, formato, salas, atores, viloes, presenca);
            AssetDatabase.SaveAssets();
            return content;
        }

        // ------------------------------------------------------------------ Protótipo 3 (upgrade idempotente)

        /// <summary>Metas por ato do Protótipo 3 (simulação de 2.000 runs; valores INICIAIS, ajustar no playtest).</summary>
        public static readonly int[] P3Goals = { 400, 2000, 4000 };

        /// <summary>
        /// Protótipo 3 — "Build do Filme" (Build_do_Filme_v1.md): plots, cenas de dupla, classes, Fantasma, artefatos.
        /// Cria só o que falta (Plots/, Combos/, Artefatos/) e preenche só campos VAZIOS (listas vazias, papel None, família Auto).
        /// Valores que não dá para saber se o Gabriel mexeu (pavor por cena das salas, Porão lacrado, metas, descrições
        /// dos atores) são aplicados UMA VEZ só: quando o asset Plots/Plot_CasalQuarto ainda não existia.
        /// </summary>
        private static void UpgradePrototype3(GameContentDef content, FilmFormatDef formato, List<RoomDef> salas,
            List<ActorDef> atores, List<VillainDef> viloes, ElementDef presenca)
        {
            foreach (var f in new[] { "Plots", "Combos", "Artefatos" }) EnsureFolder(DataRoot, f);
            bool firstTime = AssetDatabase.LoadAssetAtPath<PlotDef>($"{DataRoot}/Plots/Plot_CasalQuarto.asset") == null;

            RoomDef cozinha = salas[0], salaEstar = salas[1], banheiro = salas[2], quarto = salas[3], porao = salas[4], sotao = salas[5];

            // ---------------------------------------------------------- Artefatos (Jokers v1)
            ArtefatoDef Art(string id, string name, string desc, Color c, params ArtefatoDef.Effect[] fx) =>
                Make<ArtefatoDef>($"Artefatos/Artefato_{id}", a => a.Setup(name, desc, c, new List<ArtefatoDef.Effect>(fx)));

            // ---------------------------------------------------------- Cenas de dupla
            DuoComboDef casal = Make<DuoComboDef>("Combos/Combo_Casal", c => c.Setup("Casal",
                "Atleta + Popular na mesma sala: romance! Mais audiência a cada cena. (O clichê perfeito para virar vítima.)",
                new List<ActorRole> { ActorRole.Atleta, ActorRole.Popular }, 0, true, 30));
            DuoComboDef investigacao = Make<DuoComboDef>("Combos/Combo_Investigacao", c => c.Setup("Investigação",
                "Nerd + Final Girl na mesma sala: os plots dali andam 1 cena mais rápido.",
                new List<ActorRole> { ActorRole.Nerd, ActorRole.FinalGirl }, 0, true, 10, 1));
            DuoComboDef grupo = Make<DuoComboDef>("Combos/Combo_Grupo", c => c.Setup("Grupo",
                "3 ou mais juntos: ninguém fica isolado (o vilão não ataca), mas a cena fica parada: menos audiência.",
                new List<ActorRole>(), 3, false, 0, 0, 0.5f, true));

            var artefatos = new List<ArtefatoDef>
            {
                Art("TrilhaDeViolinos", "Trilha de violinos", "Sustos rendem +50% (Grande Susto e cenas de susto).", new Color(0.7f, 0.55f, 1f),
                    ArtefatoDef.Fx(ArtefatoEffectType.ScareScoreMult, 1.5f)),
                Art("CameraNaMao", "Câmera na mão", "Cenas de dupla rendem ×1,5.", new Color(1f, 0.6f, 0.8f),
                    ArtefatoDef.Fx(ArtefatoEffectType.ComboScoreMult, 1.5f)),
                Art("RoteiroAmarrado", "Roteiro amarrado", "Plots cumpridos rendem +50%.", new Color(1f, 0.85f, 0.4f),
                    ArtefatoDef.Fx(ArtefatoEffectType.PlotRewardMult, 1.5f)),
                Art("MaquinaDeNeblina", "Máquina de neblina", "A tensão do fantasma sobe 50% mais rápido.", new Color(0.75f, 0.8f, 0.9f),
                    ArtefatoDef.Fx(ArtefatoEffectType.GhostTensionGainMult, 1.5f)),
                Art("ChaDeCamomila", "Chá de camomila no set", "O elenco ganha 25% menos pavor.", new Color(0.6f, 0.9f, 0.6f),
                    ArtefatoDef.Fx(ArtefatoEffectType.PavorGainMult, 0.75f)),
                Art("CloseNoVilao", "Close no vilão", "Ficar perto do vilão rende o dobro.", new Color(1f, 0.4f, 0.35f),
                    ArtefatoDef.Fx(ArtefatoEffectType.NearVillainScoreMult, 2f)),
                Art("SangueCenografico", "Sangue cenográfico", "Mortes rendem +50%.", new Color(0.85f, 0.15f, 0.15f),
                    ArtefatoDef.Fx(ArtefatoEffectType.DeathScoreMult, 1.5f)),
                Art("HoraExtra", "Hora extra da equipe", "+1 cena em cada ato.", new Color(0.95f, 0.95f, 0.7f),
                    ArtefatoDef.Fx(ArtefatoEffectType.ExtraScenesPerAct, 1f)),
            };

            // ---------------------------------------------------------- Plots de permanência
            PlotDef casalQuarto = Make<PlotDef>("Plots/Plot_CasalQuarto", p =>
            {
                p.Setup("Casal no Quarto", "O casal (Atleta + Popular) fica 1 cena no Quarto.",
                    new List<ActorRole> { ActorRole.Atleta, ActorRole.Popular }, 1, 120);
                p.SetupReward(text: "Romance no quarto: a plateia suspira (e o vilão percebe).");
            });
            PlotDef diario = Make<PlotDef>("Plots/Plot_DiarioSotao", p =>
            {
                p.Setup("O diário do Sótão", "A Final Girl fica 2 cenas no Sótão e acha o diário do caseiro.",
                    new List<ActorRole> { ActorRole.FinalGirl }, 2, 100, null, true);
                p.SetupReward(unlock: porao, text: "Entre as páginas do diário: a chave do Porão!");
            });
            PlotDef misterio = Make<PlotDef>("Plots/Plot_MisterioNerd", p =>
            {
                p.Setup("O mistério da casa", "O Nerd investiga a Sala de estar (com ele, 1 cena).",
                    new List<ActorRole> { ActorRole.Nerd }, 2, 90, new List<RoomDef> { salaEstar }, true);
                p.SetupReward(randomArtefato: true, text: "O Nerd liga os pontos: a casa guarda um segredo (ganha um artefato).");
            });
            PlotDef conversa = Make<PlotDef>("Plots/Plot_ConversaCozinha", p =>
            {
                p.Setup("Conversa na Cozinha", "Dois ou mais atores ficam 1 cena na Cozinha.",
                    new List<ActorRole>(), 1, 60, null, false, 2);
                p.SetupReward(pavor: -15, text: "Um momento de calma entre amigos (o pavor baixa).");
            });
            PlotDef espelho = Make<PlotDef>("Plots/Plot_EspelhoBanheiro", p =>
            {
                p.Setup("A lenda do espelho", "A Popular fica 1 cena SOZINHA no Banheiro.",
                    new List<ActorRole> { ActorRole.Popular }, 1, 100, null, false, 0, 1);
                p.SetupReward(element: presenca, text: "Ela diz o nome três vezes no espelho… e algo responde (Presença na sala).");
            });

            // ---------------------------------------------------------- Catálogo: listas vazias
            var cso = new SerializedObject(content);
            FillListIfEmpty(cso, "duoCombos", new Object[] { casal, investigacao, grupo });
            FillListIfEmpty(cso, "artefatos", artefatos.ToArray());
            cso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(content);

            // ---------------------------------------------------------- Salas: plots (se a lista estiver vazia)
            void RoomPlots(RoomDef r, PlotDef plot)
            {
                var so = new SerializedObject(r);
                if (FillListIfEmpty(so, "plots", new Object[] { plot }))
                {
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(r);
                }
            }
            RoomPlots(quarto, casalQuarto);
            RoomPlots(sotao, diario);
            RoomPlots(cozinha, conversa);
            RoomPlots(banheiro, espelho);

            // ---------------------------------------------------------- Atores: papel (se None) e plots
            void Role(ActorDef a, ActorRole role, string title, string passive, string desc, PlotDef plot)
            {
                var so = new SerializedObject(a);
                bool changed = false;
                var r = so.FindProperty("role");
                if (r.enumValueIndex == (int)ActorRole.None)
                {
                    r.enumValueIndex = (int)role;
                    so.FindProperty("roleTitle").stringValue = title;
                    so.FindProperty("passiveText").stringValue = passive;
                    changed = true;
                }
                if (plot != null && FillListIfEmpty(so, "plots", new Object[] { plot })) changed = true;
                if (firstTime) { so.FindProperty("description").stringValue = desc; changed = true; }
                if (changed)
                {
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(a);
                }
            }
            Role(atores[0], ActorRole.Atleta, "Tanque", "Quem está com ele ganha metade do pavor. 1× por ato: segura a porta (o vilão não entra na próxima cena).",
                "O Atleta. Protege quem está junto.", null);
            Role(atores[1], ActorRole.Nerd, "Mago", "Plots de investigação levam 1 cena a menos com ele. Frágil: ganha +50% de pavor.",
                "O Nerd. Resolve mistérios, mas se assusta fácil.", misterio);
            Role(atores[2], ActorRole.Popular, "Isca", "Cenas com ela rendem mais audiência. É o alvo preferido do vilão.",
                "A Popular. A plateia ama; o vilão também.", null);
            Role(atores[3], ActorRole.FinalGirl, "Suporte", "Acalma quem está com ela. Testemunhar uma morte a deixa determinada (pavor zera).",
                "A Final Girl. Sobrevive e segura o grupo.", null);

            // ---------------------------------------------------------- Vilões: família (se Auto)
            void Family(VillainDef v, VillainFamily f)
            {
                var so = new SerializedObject(v);
                var prop = so.FindProperty("family");
                if (prop == null || prop.enumValueIndex != (int)VillainFamily.Auto) return;
                prop.enumValueIndex = (int)f;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(v);
            }
            Family(viloes[0], VillainFamily.Slasher);
            Family(viloes[1], VillainFamily.Fantasma);

            if (!firstTime) return;

            // ---------------------------------------------------------- Uma vez só (primeira vez do P3)
            void RoomBuild(RoomDef r, int pavor, bool seal, string sealText)
            {
                var so = new SerializedObject(r);
                so.FindProperty("pavorPerScene").intValue = pavor;
                if (seal)
                {
                    so.FindProperty("startsSealed").boolValue = true;
                    so.FindProperty("sealedText").stringValue = sealText;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(r);
            }
            RoomBuild(porao, 6, true, "A porta do porão está trancada por dentro. Alguém deve ter anotado como abrir… talvez no Sótão.");
            RoomBuild(sotao, 4, false, "");
            RoomBuild(banheiro, 3, false, "");

            var vEnt = new SerializedObject(viloes[1]);
            vEnt.FindProperty("description").stringValue =
                "Fantasma. Assombra uma sala e enche a TENSÃO a cada cena; no máximo, GRANDE SUSTO em quem estiver lá. Presenças e objetos amaldiçoados valem em dobro.";
            vEnt.ApplyModifiedPropertiesWithoutUndo();
            var vMas = new SerializedObject(viloes[0]);
            vMas.FindProperty("description").stringValue =
                "Slasher. Anda 1 sala por cena, anuncia para onde vai e caça quem está sozinho. Armas, armadilhas e vítimas isoladas valem em dobro.";
            vMas.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(viloes[0]);
            EditorUtility.SetDirty(viloes[1]);

            var fso = new SerializedObject(formato);
            var acts = fso.FindProperty("acts");
            for (int i = 0; i < acts.arraySize && i < P3Goals.Length; i++)
                acts.GetArrayElementAtIndex(i).FindPropertyRelative("goal").intValue = P3Goals[i];
            fso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(formato);
            Debug.Log("[HorrorTycoon] Protótipo 3: conteúdo criado (plots, combos, artefatos), papéis, Fantasma, Porão lacrado e metas " +
                      string.Join(" / ", P3Goals));
        }

        /// <summary>Preenche uma lista de referências SÓ se estiver vazia. Devolve true se mexeu (falta ApplyModifiedProperties).</summary>
        private static bool FillListIfEmpty(SerializedObject so, string field, Object[] values)
        {
            var list = so.FindProperty(field);
            if (list == null || list.arraySize > 0) return false;
            for (int i = 0; i < values.Length; i++)
            {
                list.InsertArrayElementAtIndex(i);
                list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
            return true;
        }

        // ------------------------------------------------------------------ Protótipo 2 (upgrade idempotente)

        /// <summary>
        /// Protótipo 2 (ações, ferramentas por ator, vilão NPC) em assets que JÁ existiam.
        /// Só preenche o que está vazio / acrescenta o que falta: nunca troca um valor que o Gabriel mudou.
        ///   - Encontro "Faca na gaveta": passa a dar também a ferramenta Faca (se ainda não dá ferramenta);
        ///   - Quarto e Sótão: ganham o encontro "Crucifixo na parede" (se ainda não têm);
        ///   - Ferramentas Faca/Crucifixo: 'counters' preenchido se estiver vazio;
        ///   - A Entidade: aparência de fantasma (se ainda estiver em Auto);
        ///   - Regras: cena da pega do vilão = Morte (se vazia).
        /// </summary>
        private static void UpgradePrototype2(GameRulesDef regras, PayoffDef morte, EncounterDef facaEnc, ToolDef facaTool,
            ToolDef crucifixo, EncounterDef crucifixoEnc, RoomDef quarto, RoomDef sotao, VillainDef entidade,
            TagDef slasher, TagDef sobrenatural)
        {
            SetIfEmpty(facaEnc, "tool", facaTool);
            SetIfEmpty(facaTool, "counters", slasher);
            SetIfEmpty(crucifixo, "counters", sobrenatural);
            SetIfEmpty(regras, "villainCatchPayoff", morte);
            AddEncounterIfMissing(quarto, crucifixoEnc, 2f);
            AddEncounterIfMissing(sotao, crucifixoEnc, 2f);

            var vso = new SerializedObject(entidade);
            var look = vso.FindProperty("look");
            if (look != null && look.enumValueIndex == (int)VillainLook.Auto)
            {
                look.enumValueIndex = (int)VillainLook.Ghost;
                vso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(entidade);
            }
        }

        private static void SetIfEmpty(Object owner, string field, Object value)
        {
            if (owner == null || value == null) return;
            var so = new SerializedObject(owner);
            var prop = so.FindProperty(field);
            if (prop == null || prop.objectReferenceValue != null) return;
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(owner);
        }

        private static void AddEncounterIfMissing(RoomDef room, EncounterDef enc, float weight)
        {
            if (room == null || enc == null) return;
            foreach (var we in room.Encounters) if (we != null && we.encounter == enc) return;
            var so = new SerializedObject(room);
            var list = so.FindProperty("encounters");
            int i = list.arraySize;
            list.InsertArrayElementAtIndex(i);
            var item = list.GetArrayElementAtIndex(i);
            item.FindPropertyRelative("encounter").objectReferenceValue = enc;
            item.FindPropertyRelative("weight").floatValue = weight;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(room);
        }

        // ------------------------------------------------------------------ Geração da casa

        /// <summary>
        /// Casa procedural (Proposta_Geracao_Casa): tamanhos das salas, convivências e o HouseGenDef padrão.
        /// Só ACRESCENTA: tamanho só é ajustado se ainda estiver no padrão 4x4; tipo do Corredor só se ainda for Sala.
        /// </summary>
        private static void EnsureHouseGen(List<RoomDef> salas, RoomDef corredor, Color warm)
        {
            EnsureFolder(DataRoot, "Geracao");

            // Tamanhos (metros) na ordem de 'salas': Cozinha, Sala de estar, Banheiro, Quarto, Porão, Sótão.
            // Set em grid (10/10/2026): 1 célula = 1 cômodo de 6 × 6 m; a Sala de estar ocupa 2 × 1.
            var sizes = new[]
            {
                new Vector2Int(6, 6), new Vector2Int(12, 6), new Vector2Int(6, 6),
                new Vector2Int(6, 6), new Vector2Int(6, 6), new Vector2Int(6, 6),
            };
            for (int i = 0; i < salas.Count && i < sizes.Length; i++)
            {
                var so = new SerializedObject(salas[i]);
                var size = so.FindProperty("size");
                if (size.vector2IntValue == new Vector2Int(4, 4) && sizes[i] != size.vector2IntValue)
                {
                    size.vector2IntValue = sizes[i];
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(salas[i]);
                }
            }

            if (corredor.Kind == SpaceKind.Room)
            {
                var so = new SerializedObject(corredor);
                so.FindProperty("kind").enumValueIndex = (int)SpaceKind.Corridor;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(corredor);
            }

            // Convivências: atores param aqui, sem encontro. O Hall é o espaço INICIAL (obrigatório).
            // ("Sala de estar" já existe como SALA com encontros no P0, então a convivência inicial é o Hall.)
            RoomDef hall = Social("HallDeEntrada", "Hall de entrada", "A porta da frente dá aqui. O elenco se reúne antes de cada cena.",
                new Color(0.4f, 0.36f, 0.33f), new Vector2Int(6, 6), true, FurnitureStyle.Hall, warm);
            RoomDef jantar = Social("SalaDeJantar", "Sala de jantar", "Uma mesa posta para ninguém.",
                new Color(0.42f, 0.33f, 0.28f), new Vector2Int(6, 6), false, FurnitureStyle.Dining, warm);
            RoomDef tv = Social("SalaDeTV", "Sala de TV", "O sofá afundado e a TV chiando.",
                new Color(0.34f, 0.36f, 0.42f), new Vector2Int(12, 6), false, FurnitureStyle.Living, new Color(0.7f, 0.8f, 1f));

            var gen = Make<HouseGenDef>("Geracao/Casa_Padrao", g =>
            {
                g.corridorDef = corredor;
                g.socialPool = new List<RoomDef> { hall, jantar, tv };
                // Set em grid: as peças nascem quando um ator abre uma porta para o vazio. Terreno 7 × 6 cômodos de 6 m.
                // (Corredores em L/T/cruzamento: assets Sala_CorredorL/T e Sala_Cruzamento, ligados no Casa_Padrao.)
                g.growByDraft = true;
                g.gridCell = 6;
                g.bounds = new Vector2Int(42, 36);
                g.doorCornerMargin = 0.4f;
            });

            UpgradeCinemaScale(gen, salas, sizes, new[] { hall, jantar, tv },
                new[] { new Vector2Int(8, 6), new Vector2Int(6, 6), new Vector2Int(8, 6) });
        }

        /// <summary>
        /// Escala de cinema ×1,5 (08/10/2026) para conteúdo criado antes. Aplicada UMA vez: o marcador é o
        /// corredor do Casa_Padrao ainda com menos de 3 m. Sobrescreve tamanhos das salas/convivências e as
        /// medidas da casa (planta + kit de arte). A casa fixa do P0 não usa nada disso.
        /// </summary>
        private static void UpgradeCinemaScale(HouseGenDef gen, List<RoomDef> salas, Vector2Int[] roomSizes,
            RoomDef[] socials, Vector2Int[] socialSizes)
        {
            if (gen == null || gen.corridorWidth >= 3 || gen.growByDraft) return; // set em grid: medidas próprias

            void SetSize(RoomDef r, Vector2Int s)
            {
                if (r == null) return;
                var so = new SerializedObject(r);
                so.FindProperty("size").vector2IntValue = s;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(r);
            }
            for (int i = 0; i < salas.Count && i < roomSizes.Length; i++) SetSize(salas[i], roomSizes[i]);
            for (int i = 0; i < socials.Length; i++) SetSize(socials[i], socialSizes[i]);

            var defaults = ScriptableObject.CreateInstance<HouseGenDef>();
            gen.bounds = defaults.bounds;
            gen.corridorWidth = defaults.corridorWidth;
            gen.segmentLength = defaults.segmentLength;
            gen.doorWidth = defaults.doorWidth;
            gen.doorCornerMargin = defaults.doorCornerMargin;
            gen.minSharedWall = defaults.minSharedWall;
            Object.DestroyImmediate(defaults);
            EditorUtility.SetDirty(gen);

            var kit = AssetDatabase.LoadAssetAtPath<HouseArtKit>(HouseArtKitSetup.KitPath);
            if (kit != null)
            {
                kit.wallHeight = 3f;
                kit.doorHeight = 2.3f;
                EditorUtility.SetDirty(kit);
            }
            Debug.Log("[HorrorTycoon] Escala de cinema ×1,5 aplicada (salas, convivências, Casa_Padrao, kit). Reconstrua a cena P2.");
        }

        private static RoomDef Social(string id, string name, string desc, Color color, Vector2Int size, bool required,
            FurnitureStyle style, Color light)
        {
            return Make<RoomDef>($"Salas/Conv_{id}", r =>
            {
                r.Setup(name, desc, color, new List<string>(), new List<RoomDef.WeightedEncounter>(), new List<PayoffDef>(), null);
                r.SetupGen(SpaceKind.Social, size, 1f, 1, required);
                r.SetupVisual(style, light, false);
            });
        }

        [MenuItem("Horror Tycoon/Geração/Estatísticas da casa (500 casas)")]
        public static void HouseGenStatsMenu()
        {
            var gen = AssetDatabase.LoadAssetAtPath<HouseGenDef>(HouseGenPath);
            var content = AssetDatabase.LoadAssetAtPath<GameContentDef>(ContentPath);
            if (gen == null || content == null)
            {
                Debug.LogWarning("[HorrorTycoon] Rode 'Criar Conteúdo P1' antes (falta Casa_Padrao ou Conteudo_P1).");
                return;
            }
            Debug.Log(HouseGenStats.Run(gen, RoomPool(content, gen), 500));
        }

        /// <summary>Pool de salas como o FilmRun monta: salas do Catálogo + extras do HouseGenDef, só tipo Sala.</summary>
        public static List<RoomDef> RoomPool(GameContentDef content, HouseGenDef gen)
        {
            var pool = new List<RoomDef>();
            foreach (var r in content.Rooms) if (r != null && r.Kind == SpaceKind.Room && !pool.Contains(r)) pool.Add(r);
            foreach (var r in gen.extraRoomPool) if (r != null && r.Kind == SpaceKind.Room && !pool.Contains(r)) pool.Add(r);
            return pool;
        }

        // ------------------------------------------------------------------ Fábricas

        private static TagDef Tag(string id, Color color)
        {
            return Make<TagDef>($"Tags/Tag_{id}", t =>
            {
                var so = new SerializedObject(t);
                so.FindProperty("displayName").stringValue = id;
                so.FindProperty("color").colorValue = color;
                so.ApplyModifiedPropertiesWithoutUndo();
            });
        }

        private static ElementDef Element(string id, string desc, Color color, TagDef tag, ElementHolder holder,
            int strength, List<PayoffDef> validFor, string displayName = null,
            ElementAutoRule rule = ElementAutoRule.None, int ruleValue = 0)
        {
            return Make<ElementDef>($"Elementos/Elemento_{id}",
                e => e.Setup(displayName ?? id, desc, color, tag, holder, strength, new List<PayoffDef>(validFor), rule, ruleValue));
        }

        private static EncounterDef Encounter(string id, string name, string desc, int pts, int pavor, ElementDef el, ToolDef tool)
        {
            return Make<EncounterDef>($"Encontros/Encontro_{id}", e => e.Setup(name, desc, pts, pavor, el, tool));
        }

        private static RoomDef Room(string id, string name, string desc, Color color, List<string> hints,
            List<RoomDef.WeightedEncounter> encounters, List<PayoffDef> stage, RoomDef.LockedSpot locked)
        {
            return Make<RoomDef>($"Salas/Sala_{id}", r => r.Setup(name, desc, color, hints, encounters, stage, locked));
        }

        private static ActorDef Actor(string id, string name, string desc, Color color, int doorsPerStep)
        {
            return Make<ActorDef>($"Atores/Ator_{id}", a => a.Setup(name, desc, color, new List<TagDef>(), doorsPerStep, 0));
        }

        /// <summary>Carrega o asset se existir; senão cria, configura e salva. 'relative' é relativo a DataRoot, sem .asset.</summary>
        private static T Make<T>(string relative, System.Action<T> setup) where T : ScriptableObject
        {
            string path = $"{DataRoot}/{relative}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            var asset = ScriptableObject.CreateInstance<T>();
            setup?.Invoke(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{name}"))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }
}
