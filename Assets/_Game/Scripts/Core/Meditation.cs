namespace AThousandDawns.Core
{
    // 면벽수련 (기획 대화): 회귀록에서 얻은 심득을 심법 6종에 나눠 넣어요. 언제든 무료로 다시 나눌 수 있어요.
    // 심득 = 면벽수련 5 + 심득 +5 봉우리마다 5 (최대 30, 회귀록 시트 심득 합계).
    // 포인트당 작은 보너스 + 5·10·20 단계 보상. Ready = false인 단계는 바탕 콘텐츠가 아직 없어서 "준비 중".
    public static class Meditation
    {
        public enum Art { Swift, Iron, Meridian, Insight, Shadow, Qi } // 쾌 강 맥 오 은 기

        public sealed class ArtInfo
        {
            public string Name, Theme, PerPoint;
            public string[] Milestones; // 5 · 10 · 20
            public bool[] Ready;
        }

        public static readonly int[] Steps = { 5, 10, 20 };

        public static readonly ArtInfo[] Arts =
        {
            new ArtInfo { Name = "쾌(快)", Theme = "속도", PerPoint = "공격 속도 +1%",
                Milestones = new[] { "대시 +1회", "대시 공격 피해 +15%", "대시 중에 초식 사용" }, Ready = new[] { true, true, false } },
            new ArtInfo { Name = "강(剛)", Theme = "생존", PerPoint = "최대 체력 +2",
                Milestones = new[] { "강인함 +3", "층마다 첫 피격 무효", "한 판에 한 번 기사회생 (체력 40%)" }, Ready = new[] { true, true, true } },
            new ArtInfo { Name = "맥(脈)", Theme = "경맥", PerPoint = "수련치 +2%",
                Milestones = new[] { "시작 시 혈자리 1개", "시작 시 부품 1개 고르기", "단전 2개로 시작, 대신 공력 −1" }, Ready = new[] { true, true, false } },
            new ArtInfo { Name = "오(悟)", Theme = "탐험", PerPoint = "행운 +1 (기물 등급)",
                Milestones = new[] { "기연 감지 조건 공력 4 → 3", "갈림길 선택지 +1", "고수 처치 후 3가지 중 2가지" }, Ready = new[] { false, true, true } },
            new ArtInfo { Name = "은(隱)", Theme = "잠행·경제", PerPoint = "은자 획득 +2%",
                Milestones = new[] { "시작 은자 +50", "객잔 가격 −15%", "추적도 오르는 속도 절반" }, Ready = new[] { true, true, true } },
            new ArtInfo { Name = "기(氣)", Theme = "내공", PerPoint = "기세 충전 +1%",
                Milestones = new[] { "초식 쿨타임 −10%", "전투 시작 시 기세 30%", "기세를 200%까지 저장 (절초 2회)" }, Ready = new[] { true, true, false } },
        };

        // 회귀록에서 얻은 심득 합계
        public static int TotalPoints(MetaSave meta)
        {
            int per = UnityEngine.Mathf.RoundToInt(GameConfig.GetOptional("meditation.points_per_bud", 5f));
            int total = 0;
            if (RegressionBook.Bloomed(meta, "meditation")) total += per;
            for (int i = 1; i <= 5; i++) if (RegressionBook.Bloomed(meta, $"insight{i}")) total += per;
            return total;
        }

        public static int Spent(MetaSave meta)
        {
            Ensure(meta);
            int s = 0;
            foreach (int p in meta.meditation) s += p;
            return s;
        }

        public static int Points(MetaSave meta, Art art)
        {
            Ensure(meta);
            return meta.meditation[(int)art];
        }

        // 한 포인트 넣기·빼기 (남은 심득 안에서). 넣는 쪽은 20까지.
        public static bool Add(MetaSave meta, Art art, int delta)
        {
            Ensure(meta);
            int i = (int)art;
            int next = meta.meditation[i] + delta;
            if (next < 0 || next > Steps[Steps.Length - 1]) return false;
            if (delta > 0 && Spent(meta) + delta > TotalPoints(meta)) return false;
            meta.meditation[i] = next;
            return true;
        }

        public static void Reset(MetaSave meta)
        {
            Ensure(meta);
            for (int i = 0; i < meta.meditation.Length; i++) meta.meditation[i] = 0;
        }

        // 심득이 줄었으면 (저장 파일을 고쳤을 때 등) 뒤에서부터 덜어 내요
        public static void Clamp(MetaSave meta)
        {
            Ensure(meta);
            int over = Spent(meta) - TotalPoints(meta);
            for (int i = meta.meditation.Length - 1; i >= 0 && over > 0; i--)
            {
                int cut = System.Math.Min(over, meta.meditation[i]);
                meta.meditation[i] -= cut;
                over -= cut;
            }
        }

        public static bool Reached(MetaSave meta, Art art, int stepIndex) => Points(meta, art) >= Steps[stepIndex];

        // 단계 보상이 실제로 켜지는지 (도달 + 준비됨)
        public static bool Active(MetaSave meta, Art art, int stepIndex) => Reached(meta, art, stepIndex) && Arts[(int)art].Ready[stepIndex];

        static void Ensure(MetaSave meta)
        {
            if (meta.meditation == null || meta.meditation.Length != Arts.Length)
            {
                var fresh = new int[Arts.Length];
                if (meta.meditation != null)
                    for (int i = 0; i < fresh.Length && i < meta.meditation.Length; i++) fresh[i] = meta.meditation[i];
                meta.meditation = fresh;
            }
        }
    }
}
