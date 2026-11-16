namespace AThousandDawns.Enemies
{
    // 다크 엘프 검사 (차원 겹침, 판타지 고수): 혈영검마와 같은 규칙 (잔상 · 순간이동 검객, 50%에서 2페이즈)에 흡혈이 없고 더 빨라요.
    //   월광 베기 (가까울 때, 그림자 분신이 0.5초 뒤 같은 베기) · 그림자 질주 (중간 거리 직선 돌진 베기) · 그림자 걸음 (멀리서 버티면 등 뒤로 순간이동)
    //   값은 darkelf.* (혈영검마 bloodshadow.*와 같은 이름)
    public sealed class DarkElfBrain : BloodShadowBrain
    {
        public override EnemyKind Kind => EnemyKind.DarkElf;
        protected override string Key => "darkelf";
        protected override string SlashName => "월광 베기";
        protected override string LineName => "그림자 질주";
        protected override string BlinkName => "그림자 걸음";
    }
}
