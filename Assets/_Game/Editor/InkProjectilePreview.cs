using AThousandDawns.Enemies;
using UnityEditor;
using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // 투사체·폭발·예고 테두리 미리보기: 수묵 씬 플레이 중에 메뉴로 띄워요 (플레이어 앞을 가로질러 날아가서 맞지 않아요)
    public static class InkProjectilePreview
    {
        const string Menu = "AThousandDawns/아트/투사체 미리보기/";

        [MenuItem(Menu + "궁수 화살")] static void Arrow() => Shoot(false);
        [MenuItem(Menu + "살수 표창")] static void Shuriken() => Shoot(true);

        [MenuItem(Menu + "화약 폭발")]
        static void Burst()
        {
            var (director, player) = Find();
            if (director == null) return;
            director.SpawnBurst(player.position + player.forward * 4f, 2.2f);
        }

        [MenuItem(Menu + "공격 예고 테두리")]
        static void Telegraph()
        {
            var (director, player) = Find();
            if (director == null) return;
            var v = director.Visuals;
            var marker = Enemies.Telegraph.Create(null, v.telegraphBase, v.telegraphFill, v.telegraphOutline);
            marker.Show(TelegraphShapes.Circle(2f), true);
            marker.SetPose(player.position + player.forward * 4f, Vector3.forward);
            marker.SetProgress(0.6f);
            Object.Destroy(marker.gameObject, 3f);
        }

        [MenuItem(Menu + "궁수 화살", true)]
        [MenuItem(Menu + "살수 표창", true)]
        [MenuItem(Menu + "화약 폭발", true)]
        [MenuItem(Menu + "공격 예고 테두리", true)]
        static bool CanPlay() => Application.isPlaying;

        static void Shoot(bool shuriken)
        {
            var (director, player) = Find();
            if (director == null) return;
            var right = player.right;
            var origin = player.position + player.forward * 3f - right * 6f + Vector3.up * 1.2f;
            var arrow = director.SpawnArrow(origin, right, shuriken ? 14f : 18f, 12f, 0.2f, 0f);
            Object.FindFirstObjectByType<AThousandDawns.Art.InkProjectiles>()?.DressNow(arrow.gameObject, shuriken);
        }

        static (EnemyDirector, Transform) Find()
        {
            var director = Object.FindFirstObjectByType<EnemyDirector>();
            var player = Object.FindFirstObjectByType<AThousandDawns.Player.PlayerController>();
            if (director == null || player == null) { Debug.LogWarning("판 안에서 플레이 중일 때만 돼요"); return (null, null); }
            return (director, player.transform);
        }
    }
}
