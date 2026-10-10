using System.Collections.Generic;
using UnityEngine;

namespace HorrorTycoon.Rooms.Generation
{
    /// <summary>
    /// Parâmetros da geração procedural da casa (Proposta_Geracao_Casa §3 e §4).
    /// Unidade: METROS (1 célula da grade = 1 m). Faixas "mín–máx" usam Vector2Int (x = mín, y = máx, inclusivo).
    /// Criar: Create > Horror Tycoon > Geração de Casa. Sem este asset, a run usa a planta fixa (P0).
    /// </summary>
    [CreateAssetMenu(menuName = "Horror Tycoon/Geração de Casa", fileName = "Casa_Nova")]
    public class HouseGenDef : ScriptableObject
    {
        [Header("Terreno")]
        [Tooltip("Retângulo máximo da casa em metros (largura x profundidade). A porta da frente fica na borda sul (y = 0).")]
        public Vector2Int bounds = new Vector2Int(33, 30);

        [Header("Corredores")]
        [Tooltip("RoomDef usado em TODO segmento de corredor (tipo Corredor).")]
        public RoomDef corridorDef;
        [Tooltip("Largura do corredor em metros.")]
        [Min(1)] public int corridorWidth = 3;
        [Tooltip("Quantos segmentos de corredor (mín, máx).")]
        public Vector2Int corridorSegments = new Vector2Int(2, 4);
        [Tooltip("Comprimento de cada segmento em metros (mín, máx).")]
        public Vector2Int segmentLength = new Vector2Int(6, 14);
        [Tooltip("Chance de o próximo segmento virar 90° (senão segue reto).")]
        [Range(0f, 1f)] public float turnChance = 0.5f;
        [Tooltip("Chance de o próximo segmento sair do meio de um segmento antigo (ramificação).")]
        [Range(0f, 1f)] public float branchChance = 0.3f;

        [Header("Convivência")]
        [Tooltip("Espaços de convivência (tipo Convivência). O 1º marcado como obrigatório é o INICIAL (colado à porta da frente).")]
        public List<RoomDef> socialPool = new List<RoomDef>();
        [Tooltip("Quantas convivências extras (além da inicial), presas a corredores (mín, máx).")]
        public Vector2Int extraSocials = new Vector2Int(0, 1);

        [Header("Salas")]
        [Tooltip("Salas extras só desta casa. O pool principal são as salas (tipo Sala) do Catálogo.")]
        public List<RoomDef> extraRoomPool = new List<RoomDef>();
        [Tooltip("Quantas salas por casa (mín, máx). Limitado pelo que o pool consegue oferecer.")]
        public Vector2Int roomCount = new Vector2Int(6, 8);
        [Tooltip("Chance de uma sala abrir para DENTRO de outra sala (sala 'funda', +1 porta).")]
        [Range(0f, 1f)] public float roomInsideRoomChance = 0.25f;

        [Header("Portas e passagens (custos em portas)")]
        [Tooltip("Custo de atravessar uma porta.")]
        [Min(0)] public int doorCost = 1;
        [Tooltip("Custo de atravessar uma passagem aberta (corredor↔corredor, corredor↔convivência). 0 = mantém a economia atual.")]
        [Min(0)] public int openingCost = 0;
        [Tooltip("Largura do vão da porta em metros.")]
        [Min(0.5f)] public float doorWidth = 1.2f;
        [Tooltip("Distância mínima entre a porta e o canto da parede em comum (metros).")]
        [Min(0f)] public float doorCornerMargin = 0.45f;
        [Tooltip("Parede em comum mínima (metros inteiros) para ligar dois espaços. Precisa caber porta + 2 margens.")]
        [Min(1)] public int minSharedWall = 3;

        [Header("Set em grid (a casa cresce peça a peça)")]
        [Tooltip("Liga: a casa começa só com a convivência inicial (o Hall). Cada porta para o vazio, ao ser aberta, oferece " +
                 "peças (salas, convivências, corredores) que cabem ali; a escolhida se monta, girada para encaixar, e traz as portas dela. " +
                 "O terreno é 'bounds' dividido em células de 'gridCell' m. Desligado: a casa gerada inteira (corredores + salas).")]
        public bool growByDraft;
        [Tooltip("Lado da célula do grid em metros: 1 célula = 1 cômodo (como Blue Prince). Peças grandes ocupam 2 × 1 etc.")]
        [Min(2)] public int gridCell = 6;
        [Tooltip("Corredores extras do set em grid (formas de porta: em L, em T, cruzamento). O 'corridorDef' é o reto.")]
        public List<RoomDef> corridorPieces = new List<RoomDef>();
        [Tooltip("Quantas peças são oferecidas a cada porta aberta.")]
        [Min(1)] public int draftOptions = 3;

        [Header("Geração")]
        [Tooltip("Tentativas antes de aceitar a melhor casa encontrada (cada tentativa usa uma sub-seed).")]
        [Min(1)] public int maxAttempts = 30;
        [Tooltip("Atores começam DENTRO, na convivência inicial (senão, lá fora).")]
        public bool startInside = true;

        private void OnValidate()
        {
            float need = doorWidth + 2f * doorCornerMargin;
            if (minSharedWall < need) minSharedWall = Mathf.CeilToInt(need);
            if (corridorSegments.y < corridorSegments.x) corridorSegments.y = corridorSegments.x;
            if (segmentLength.y < segmentLength.x) segmentLength.y = segmentLength.x;
            if (roomCount.y < roomCount.x) roomCount.y = roomCount.x;
            if (extraSocials.y < extraSocials.x) extraSocials.y = extraSocials.x;
        }
    }
}
