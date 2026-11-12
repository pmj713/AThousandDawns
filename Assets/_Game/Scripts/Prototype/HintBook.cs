using System.Collections.Generic;
using AThousandDawns.Core;

namespace AThousandDawns.Prototype
{
    // 무공 요결 (튜토리얼 기획: 본 설명을 전부 모아 둔 도움말 책, 일시정지 메뉴에서 열람).
    // 조작 기본은 처음부터 펼쳐져 있고, 나머지는 그 상황을 한 번 겪어 첫 안내를 본 뒤에 펼쳐져요 (MetaSave.seenHints).
    // Id는 RoomRun.Notify의 id와 같아요.
    public static class HintBook
    {
        // 키 이름 표식 (그릴 때 KeyHint.Plain으로 지금 키 · 패드 버튼 이름이 돼요)
        static string K(Bind bind) => KeyHint.Key(bind);

        public sealed class Entry
        {
            public string Id, Group, Title, Text;
            public bool Always; // 처음부터 펼쳐져 있음
        }

        public static readonly Entry[] All =
        {
            // 조작 (처음부터)
            new Entry { Always = true, Id = "basic_move", Group = "조작", Title = "이동과 대시",
                Text = $"{K(Bind.MoveUp)}{K(Bind.MoveLeft)}{K(Bind.MoveDown)}{K(Bind.MoveRight)}로 움직여요. {K(Bind.Dash)}를 누르면 대시하고, 대시하는 동안은 맞지 않아요.\n적의 공격 예고(주황 바닥)가 보이면 대시로 빠져나가요." },
            new Entry { Always = true, Id = "basic_attack", Group = "조작", Title = "평타",
                Text = $"{K(Bind.Attack)}로 커서 쪽을 베요. 누르고 있으면 1·2·3타로 이어지고, 3타째가 가장 넓고 강해요.\n평타를 휘두른 뒤 쉬는 틈에만 대시로 끊을 수 있어요." },
            new Entry { Always = true, Id = "basic_dash_attack", Group = "조작", Title = "대시 공격",
                Text = $"대시가 끝나자마자 {K(Bind.Attack)}하면 돌진하며 베어요.\n방패를 든 적은 정면 공격을 막지만, 대시 공격에는 방패가 깨져요." },
            new Entry { Always = true, Id = "basic_skill", Group = "조작", Title = "초식과 절초",
                Text = $"{K(Bind.Skill)}는 초식이에요 (검: 회선참, 비도: 회수). 쿨타임이 돌면 다시 써요.\n싸우면 기세가 차고, 가득 차면 {K(Bind.Ultimate)}로 절초를 써요. 절초는 방 하나 크기의 적에게 고르게 떨어져요." },
            new Entry { Always = true, Id = "basic_meridian", Group = "조작", Title = "경맥도와 기물",
                Text = $"{K(Bind.Meridian)}로 경맥도를 열어요. 단전에서 멀수록 칸의 기가 줄어요.\n기물은 기 구간 안에 있어야 힘을 써요 (운기). 끌어서 옮기고 (패드: 십자키 · A로 집고 놓기), 오른쪽 클릭 (패드 X)으로 주머니에 넣어요." },
            new Entry { Always = true, Id = "basic_floor", Group = "조작", Title = "층과 관문",
                Text = $"방을 비우면 문이 열려요. 오른쪽 아래 지도에서 보상 방(보)과 관문(관)을 찾아요.\n관문 가운데에서 {K(Bind.Interact)}를 누르면 다음 층으로 가요. Esc (패드 Start)는 일시정지예요." },

            // 경맥
            new Entry { Id = "qi_lacking", Group = "경맥", Title = "기 부족",
                Text = "기물이 놓인 칸의 기가 최소 기보다 적으면 회색으로 잠들어요. 효과도 키워드도 없어요.\n단전 가까이 옮기거나, 통맥·기혈로 기를 보태면 깨어나요." },
            new Entry { Id = "qi_deviated", Group = "경맥", Title = "주화입마",
                Text = "칸의 기가 최대 기보다 많으면 붉게 주화입마가 돼요. 효과가 1.5배, 키워드 개수가 2가 되지만 부작용이 붙어요.\n점혈로 기를 막거나 한옥·한혈로 최대 기를 올려 견딜 수 있어요." },
            new Entry { Id = "level_up", Group = "경맥", Title = "개혈",
                Text = "수련치가 차면 레벨이 오르고 혈자리를 하나 열 수 있어요 (개혈 대기).\n기혈은 칸의 기 +1, 통혈은 기가 줄지 않고 지나감, 한혈은 그 칸 기물의 최대 기 +1이에요." },
            new Entry { Id = "bag", Group = "경맥", Title = "주머니",
                Text = $"회귀록 주머니 봉우리를 피우면 경맥도 옆에 주머니가 생겨요 (4칸, 확장마다 +2). 넣어 둔 기물은 효과가 없지만 버리지 않고 들고 다녀요.\n경맥도에서 기물을 오른쪽 클릭 (패드 X)하면 주머니로, 끌어서 (패드 Y로 꺼내고 A) 칸에 놓으면 다시 힘을 써요. 경맥도도 주머니도 가득이면 새 기물을 받을 때 하나를 버려야 해요." },
            new Entry { Id = "parts", Group = "경맥", Title = "부품",
                Text = "회귀록에서 연 부품을 경맥도 빈 칸에 놓아요. 통맥은 지나는 기가 줄지 않고, 점혈은 기를 막고, 한옥은 붙은 기물의 최대 기 +2예요.\n부품 칸에는 기물을 놓을 수 없어요." },

            // 전투
            new Entry { Id = "ultimate", Group = "전투", Title = "절초",
                Text = $"기세가 가득 차면 {K(Bind.Ultimate)}로 절초를 써요. 시전하는 동안은 맞지 않아요.\n키워드가 4단계면 절초에 각인이 붙어요 (화상·빙결·연쇄 번개 등)." },
            new Entry { Id = "dagger", Group = "전투", Title = "비도",
                Text = $"비도는 멀리서 던져요. 꽂히거나 떨어진 비도는 6초 남아요.\n{K(Bind.Skill)} 회수로 모두 거둬들이며, 꽂혀 있던 적은 비도 수만큼 한 번에 맞아요. 마을 무기 거치대에서 검과 바꿔요." },

            new Entry { Id = "jinjeon", Group = "전투", Title = "진전",
                Text = "기연 방에서 아주 드물게 진전을 얻어요. 그 판 동안 절초 모양이 바뀌어요 (검: 일검·검진·귀원 / 비도: 폭우·산화·회류).\n판을 새로 시작하면 사라지고, 절초 각인은 그대로 붙어요." },

            // 판
            new Entry { Id = "fork_dense", Group = "판", Title = "갈림길과 기운",
                Text = "층 사이 갈림길에서 길을 골라요. 길마다 보상 종류가 다르고, 짙은 길은 적이 강한 대신 보상이 한 단계 좋아요.\n같은 보상 종류는 한 갈림길에 한 번만 나와요." },
            new Entry { Id = "tracking", Group = "판", Title = "추적도와 살수",
                Text = "전투 방을 비울 때마다 추적도가 올라요. 층에 들어갈 때 5 이상이면 마교 살수 하나, 10 이상이면 둘이 쫓아와요.\n객잔에 들르면 절반, 살수를 쓰러뜨리면 −2예요." },
            new Entry { Id = "assassin", Group = "판", Title = "마교 살수",
                Text = "살수는 지도의 붉은 방에서 방 하나씩 쫓아와요. 따라잡히면 난입하거나 1:1 결투가 돼요.\n관문으로 층을 빠져나가면 따돌릴 수 있어요." },
            new Entry { Id = "event", Group = "판", Title = "이벤트 방",
                Text = "싸움 없는 막다른 방이에요. 고르는 대로 결과가 달라지고, 대가 없는 선택지도 늘 있어요.\n조건이 안 되는 선택지는 흐리게 조건만 보여요." },
            new Entry { Id = "hidden", Group = "판", Title = "기연 방",
                Text = $"층마다 가끔 숨은 방이 생겨요. 벽에 금이 간 곳에서 {K(Bind.Interact)}로 벽을 부수면 열려요.\n공력이 높으면 지도에 희미하게 보이고, 안에서 영약이나 수련 기록을 얻어요." },
            new Entry { Id = "forge", Group = "판", Title = "대장간",
                Text = "대장간 길의 보상 방에서 기물을 녹여 주 키워드를 무기 홈(2개)에 넣어요. 홈마다 키워드 +1이에요.\n같은 키워드를 두 홈에 넣으면 강화판이 돼요. 검과 비도는 효과가 달라요." },
            new Entry { Id = "master", Group = "판", Title = "고수",
                Text = "마지막 층 관문 뒤, 객잔 바로 앞을 고수가 지켜요. 꺾으면 비급 전수(초식이 바뀜) · 흡성(공력 +1, 이번 지역 기물 최대 기 −1) · 방면(추적도 0, 은자) 중 하나를 골라요." },
            new Entry { Id = "inn", Group = "판", Title = "객잔",
                Text = $"일반 층을 다 지나면 객잔이에요. 점소이({K(Bind.Interact)})에게 기물·금창약·부품·이혈·소문(다음 층 기연 방)을 사요.\n들르면 추적도가 절반이 되고, 북쪽 문은 보스 전 정비실이에요." },
            new Entry { Id = "sell", Group = "판", Title = "매각",
                Text = "객잔 진열대의 매각 칸에서 가진 기물을 팔아요. 값은 객잔 판매가의 40%예요.\n떠돌이 상인은 60%로 사 가지만, 상인이 고른 기물 하나만 팔 수 있어요." },
            new Entry { Id = "rest", Group = "판", Title = "정비실",
                Text = "보스 전 마지막 정비예요. 목인장으로 지금 빌드의 피해를 시험해 봐요.\n나가면 목인장에 쓴 기세와 초식 쿨타임이 돌아와요." },

            // 회귀
            new Entry { Id = "book", Group = "회귀", Title = "회귀록",
                Text = "판이 끝나면 회귀의 기억을 얻어요. 마을 매화나무(회귀록)에서 봉우리를 골라 피우면 새 시스템이 열려요.\n봉우리를 누르고 개화를 누르면 바로 적용돼요 (패드: 십자키로 봉우리 · A 개화 · Y 면벽수련)." },
            new Entry { Id = "codex", Group = "회귀", Title = "기물 도감",
                Text = "처음 얻은 기물은 기물 도감에 남아요. 회귀해도 지워지지 않아요.\n일시정지 → 무공 요결에서 Tab으로 넘겨 봐요. 아직 못 만난 기물은 ???로 보여요." },
            new Entry { Id = "relic", Group = "회귀", Title = "유품록",
                Text = "천마를 격파한 판에 지녔던 기물이 마을 사당의 유품록에 남아요.\n기억 용량만큼 골라 두면 다음 판을 그 기물을 지니고 시작해요 (전설은 기억 2). 회귀록 유품 가지로 용량이 늘어요." },
            new Entry { Id = "karma", Group = "회귀", Title = "업보",
                Text = "사당에서 업보 단계를 골라요. 단계는 쌓이고, 판이 험해지는 대신 얻는 기억이 단계마다 10% 늘어요.\n열린 가장 높은 단계로 천마를 격파하면 다음 단계가 열려요 (최대 6)." },
            new Entry { Id = "elite", Group = "전투", Title = "정예와 특성",
                Text = "정예는 체력 3배 · 크고 머리 위 표식이 특성이에요.\n철포삼(네모): 경직 없음 · 회춘(공): 3초 안 맞으면 회복, 화상이면 멈춤 · 열화(기둥): 맞으면 화상, 죽은 자리에 불길 · 분열(캡슐): 쓰러지면 작은 둘로 갈라져요." },
            new Entry { Id = "scroll", Group = "전투", Title = "비급과 비도 초식",
                Text = $"고수의 비급을 전수받으면 {K(Bind.Skill)} 초식이 바뀌어요.\n비도를 들면 비급마다 회수 초식이 달라요: 회수 · 비도 순간이동 · 비도 솟구치기 · 비도 호신륜 · 탁류 소용돌이 · 혈영 잔상." },
            new Entry { Id = "temple_layout", Group = "판", Title = "폐사찰 방 구성",
                Text = "폐사찰은 방마다 돌기둥 · 석불 · 무너진 담이 달라요.\n사람도 화살도 돌진도 막는 엄폐물이에요. 궁수와 돌진하는 적을 그 뒤에서 상대해 봐요." },
            new Entry { Id = "river_water", Group = "판", Title = "장강의 물",
                Text = "장강 수채 방에는 여울 · 웅덩이 · 물길이 있어요. 물에 들어가면 느려지고, 흐르는 물은 물살 쪽으로 밀어요.\n대시 중엔 밀리지 않아요. 수적과 수귀는 물에 익숙해서 그대로예요." },
            new Entry { Id = "tomb_layout", Group = "판", Title = "천마총 방 구성",
                Text = "천마총은 방마다 무너진 관 · 마기 기둥 · 혈지가 달라요.\n마기 기둥은 싸우는 동안 6초마다 둘레에 마기를 뿜어요 (예고 1초). 혈지 위에 선 적은 회복하니 끌어내서 싸워요." },
            new Entry { Id = "jar", Group = "판", Title = "항아리",
                Text = "방 벽가의 항아리는 부딪히거나 공격하면 깨지고 은자가 나와요. 싸우는 중에도 돼요.\n한 번 깬 항아리는 그 층에서 다시 나오지 않아요." },
            new Entry { Id = "meditation", Group = "회귀", Title = "면벽수련",
                Text = "뒷산 동굴에서 심득을 심법 6종(쾌·강·맥·오·은·기)에 나눠 넣어요. 언제든 무료로 다시 나눌 수 있어요.\n5·10·20 포인트에서 단계 보상이 열려요." },
        };

        // 지금 펼쳐진 요결 (조작 + 본 안내)
        public static List<Entry> Unlocked(MetaSave meta)
        {
            var list = new List<Entry>();
            foreach (var e in All)
                if (e.Always || (meta?.seenHints != null && meta.seenHints.Contains(e.Id))) list.Add(e);
            return list;
        }
    }
}
