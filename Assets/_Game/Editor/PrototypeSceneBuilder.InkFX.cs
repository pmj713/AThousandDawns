using UnityEditor;
using UnityEngine;

namespace AThousandDawns.EditorTools
{
    // 수묵 시험판 전투 이펙트: 평타 붓 획(InkSlashFX), 맞을 때 먹물 튀김(InkHitSplatter), 공격 예고 붓 농담.
    // 모두 아트 쪽 컴포넌트·재질이라 게임 코드는 그대로예요.
    public static partial class PrototypeSceneBuilder
    {
        static void AddInkCombatFX()
        {
            // 평타: 판정 부채꼴(SwingFlash) 대신 먹 붓 획
            var flash = Object.FindFirstObjectByType<AThousandDawns.Player.SwingFlash>(FindObjectsInactive.Include);
            if (flash != null)
            {
                var slash = flash.gameObject.AddComponent<AThousandDawns.Art.InkSlashFX>();
                SetRef(slash, "slashMaterial", GetShaderMaterial("Ink_Slash", "AThousandDawns/InkSlash"));
            }

            // 맞을 때 바닥 먹물 (적은 먹, 플레이어는 붉은 물감)
            var splatter = new GameObject("Ink Hit Splatter").AddComponent<AThousandDawns.Art.InkHitSplatter>();
            SetRef(splatter, "splatMaterial", GetShaderMaterial("Ink_Splat", "AThousandDawns/InkSplat"));
        }

        // 공격 예고: 원래 재질의 색을 그대로 가져와 붓 자국 농담만 얹어요 (원래 색이 바뀌면 씬을 다시 만들 때 따라가요)
        static Material GetInkTelegraphMaterial(Material original)
        {
            var mat = GetShaderMaterial($"Ink_{original.name}", "AThousandDawns/InkTelegraph");
            if (original.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", original.GetColor("_BaseColor"));
            return mat;
        }
    }
}
