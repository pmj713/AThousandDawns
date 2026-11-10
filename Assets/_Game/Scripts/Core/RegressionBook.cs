using System.Collections.Generic;

namespace AThousandDawns.Core
{
    // 회귀록 (회귀록 시트): 회귀의 기억으로 봉우리를 피워 해금하는 매화나무.
    // 비용 = 시트 실제 비용 (기본 비용 × 1.8). 선행 봉우리가 피어 있어야 피울 수 있어요.
    // 주머니 가지는 시트에 없던 것 (기획 대화: 뿌리 바로 위, 30 · 처음 4칸, 확장마다 +2).
    // Ready = false인 봉우리는 그 콘텐츠가 아직 없어서 피울 수 없어요 (회귀록 화면에 "준비 중").
    public sealed class BookNode
    {
        public string Id, Name, Branch, Prereq, Condition, Effect;
        public int DefaultCost;
        public bool Ready;
    }

    public static class RegressionBook
    {
        public const string Root = "root";

        public static readonly BookNode[] Nodes =
        {
            // 주머니 (뿌리 바로 위)
            new BookNode { Id = "bag", Name = "주머니", Branch = "주머니", Prereq = Root, DefaultCost = 30, Ready = true, Effect = "경맥도에 놓지 못한 기물을 넣어 두는 주머니 4칸. 없으면 꽉 찼을 때 하나를 버려야 받아요." },
            new BookNode { Id = "bag_plus1", Name = "주머니 확장 (1차)", Branch = "주머니", Prereq = "bag", DefaultCost = 90, Ready = true, Effect = "주머니 +2칸 (6칸)" },
            new BookNode { Id = "bag_plus2", Name = "주머니 확장 (2차)", Branch = "주머니", Prereq = "bag_plus1", DefaultCost = 160, Ready = true, Effect = "주머니 +2칸 (8칸)" },

            // 경맥 → 무기
            new BookNode { Id = "tongmaek", Name = "통맥", Branch = "경맥", Prereq = Root, DefaultCost = 40, Ready = true, Effect = "부품 통맥: 기가 이 칸을 지날 때 줄지 않아요 (기를 멀리 보냄). 갈림길 부품 길·객잔에서 얻어요." },
            new BookNode { Id = "jeomhyeol", Name = "점혈", Branch = "경맥", Prereq = "tongmaek", DefaultCost = 70, Ready = true, Effect = "부품 점혈: 기가 이 칸을 지나가지 못해요 (넘치는 기를 막음)." },
            new BookNode { Id = "hanok", Name = "한옥", Branch = "경맥", Prereq = "jeomhyeol", DefaultCost = 110, Ready = true, Effect = "부품 한옥: 붙어 있는 기물의 최대 기 +2 (주화입마를 견딤)." },
            new BookNode { Id = "forge", Name = "대장간", Branch = "무기", Prereq = "hanok", DefaultCost = 140, Ready = true, Effect = "갈림길에 대장간 길이 생겨요: 기물을 녹여 주 키워드를 무기 홈(2개)에 넣어요. 홈마다 키워드 +1, 같은 키워드 둘이면 강화판." },
            new BookNode { Id = "flying_dagger", Name = "비도", Branch = "무기", Prereq = "forge", DefaultCost = 270, Ready = true, Effect = "두 번째 무기 비도: 마을 무기 거치대에서 골라요. 멀리서 던지고, 초식 회수로 꽂힌 비도를 거둬들여요." },

            // 줄기 → 생존 · 견문
            new BookNode { Id = "meditation", Name = "면벽수련", Branch = "줄기", Prereq = Root, DefaultCost = 180, Ready = true, Effect = "면벽수련이 열려요: 심득 5를 심법 6종(쾌·강·맥·오·은·기)에 나눠 넣어요. 회귀록에서 M." },
            new BookNode { Id = "potion_kit", Name = "금창약 지참", Branch = "생존", Prereq = "meditation", DefaultCost = 220, Ready = true, Effect = "판을 금창약 1개를 들고 시작" },
            new BookNode { Id = "inn_stock", Name = "객잔 진열 +1", Branch = "생존", Prereq = "potion_kit", DefaultCost = 230, Ready = true, Effect = "객잔 진열 기물 4개" },
            new BookNode { Id = "insight1", Name = "심득 +5 (1차)", Branch = "줄기", Prereq = "meditation", DefaultCost = 180, Ready = true, Effect = "면벽수련 심득 +5" },
            new BookNode { Id = "item_pack1", Name = "새 기물 묶음 1", Branch = "견문", Prereq = "insight1", DefaultCost = 180, Effect = "보상에 새 기물 묶음 추가" },
            new BookNode { Id = "item_pack2", Name = "새 기물 묶음 2", Branch = "견문", Prereq = "item_pack1", DefaultCost = 320, Effect = "보상에 새 기물 묶음 추가" },
            new BookNode { Id = "insight2", Name = "심득 +5 (2차)", Branch = "줄기", Prereq = "insight1", DefaultCost = 270, Ready = true, Effect = "면벽수련 심득 +5" },
            new BookNode { Id = "insight3", Name = "심득 +5 (3차)", Branch = "줄기", Prereq = "insight2", DefaultCost = 360, Ready = true, Effect = "면벽수련 심득 +5" },
            new BookNode { Id = "insight4", Name = "심득 +5 (4차)", Branch = "줄기", Prereq = "insight3", DefaultCost = 450, Ready = true, Effect = "면벽수련 심득 +5" },
            new BookNode { Id = "insight5", Name = "심득 +5 (5차)", Branch = "줄기", Prereq = "insight4", DefaultCost = 540, Ready = true, Effect = "면벽수련 심득 +5" },

            // 유품 (겨울 가지, 첫 천마 격파 후 개화)
            new BookNode { Id = "relic1", Name = "기억 용량 +1 (1차)", Branch = "유품", Prereq = Root, DefaultCost = 110, Condition = "첫 천마 격파 후", Effect = "전생의 유품 기억 용량 +1" },
            new BookNode { Id = "relic2", Name = "기억 용량 +1 (2차)", Branch = "유품", Prereq = "relic1", DefaultCost = 160, Condition = "첫 천마 격파 후", Effect = "전생의 유품 기억 용량 +1" },
            new BookNode { Id = "relic3", Name = "기억 용량 +1 (3차)", Branch = "유품", Prereq = "relic2", DefaultCost = 220, Condition = "첫 천마 격파 후", Effect = "전생의 유품 기억 용량 +1" },
            new BookNode { Id = "relic4", Name = "기억 용량 +1 (4차)", Branch = "유품", Prereq = "relic3", DefaultCost = 270, Condition = "첫 천마 격파 후", Effect = "전생의 유품 기억 용량 +1" },
        };

        static Dictionary<string, BookNode> _byId;

        public static BookNode Find(string id)
        {
            if (_byId == null)
            {
                _byId = new Dictionary<string, BookNode>();
                foreach (var n in Nodes) _byId[n.Id] = n;
            }
            return _byId.TryGetValue(id, out var node) ? node : null;
        }

        public static int Cost(BookNode node) => UnityEngine.Mathf.RoundToInt(GameConfig.GetOptional($"book.{node.Id}", node.DefaultCost));

        public static bool Bloomed(MetaSave meta, string id) => id == Root || (meta.bloomed != null && meta.bloomed.Contains(id));

        // 피울 수 없는 이유 (피울 수 있으면 null)
        public static string Blocker(MetaSave meta, BookNode node)
        {
            if (Bloomed(meta, node.Id)) return "피었어요";
            if (!Bloomed(meta, node.Prereq)) return $"먼저 {Find(node.Prereq)?.Name ?? "뿌리"}";
            if (node.Condition != null && meta.finalBossKills <= 0) return node.Condition;
            if (!node.Ready) return "준비 중 (아직 콘텐츠가 없어요)";
            if (meta.memory < Cost(node)) return "기억이 모자라요";
            return null;
        }

        // 개화: 기억을 쓰고 피워요. 저장은 부르는 쪽에서.
        public static bool Bloom(MetaSave meta, string id)
        {
            var node = Find(id);
            if (node == null || Blocker(meta, node) != null) return false;
            meta.memory -= Cost(node);
            meta.bloomed.Add(id);
            return true;
        }

        // ── 판에 적용

        public static int BagCapacity(MetaSave meta)
        {
            if (!Bloomed(meta, "bag")) return 0;
            int slots = UnityEngine.Mathf.RoundToInt(GameConfig.GetOptional("bag.base_slots", 4f));
            int plus = UnityEngine.Mathf.RoundToInt(GameConfig.GetOptional("bag.plus_slots", 2f));
            if (Bloomed(meta, "bag_plus1")) slots += plus;
            if (Bloomed(meta, "bag_plus2")) slots += plus;
            return slots;
        }

        // 해금한 부품 종류 (부품 길·객잔 진열에 나와요)
        public static System.Collections.Generic.List<AThousandDawns.Player.PartKind> UnlockedParts(MetaSave meta)
        {
            var list = new System.Collections.Generic.List<AThousandDawns.Player.PartKind>();
            if (Bloomed(meta, "tongmaek")) list.Add(AThousandDawns.Player.PartKind.Tongmaek);
            if (Bloomed(meta, "jeomhyeol")) list.Add(AThousandDawns.Player.PartKind.Jeomhyeol);
            if (Bloomed(meta, "hanok")) list.Add(AThousandDawns.Player.PartKind.Hanok);
            return list;
        }

        public static int StartPotions(MetaSave meta) => Bloomed(meta, "potion_kit") ? 1 : 0;

        public static int InnStockBonus(MetaSave meta) => Bloomed(meta, "inn_stock") ? 1 : 0;

        public static bool ForgeUnlocked(MetaSave meta) => Bloomed(meta, "forge");

        public static bool DaggerUnlocked(MetaSave meta) => Bloomed(meta, "flying_dagger");
    }
}
