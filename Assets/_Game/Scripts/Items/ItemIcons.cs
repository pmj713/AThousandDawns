using System.Collections.Generic;
using UnityEngine;

namespace AThousandDawns.Items
{
    // 기물 아이콘 (Art/UI/Icons/Resources/ItemIcons/<id>.png, 투명 바탕 수묵). 없는 기물은 null이라 글자만 보여요.
    public static class ItemIcons
    {
        static readonly Dictionary<string, Texture2D> Cache = new();

        public static Texture2D Get(ItemDefinition def) => def == null ? null : Get(def.id);

        public static Texture2D Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (!Cache.TryGetValue(id, out var tex))
            {
                tex = Resources.Load<Texture2D>("ItemIcons/" + id);
                Cache[id] = tex;
            }
            return tex;
        }

        // IMGUI: 아이콘이 있으면 rect 안에 비율을 지켜 그리고 true
        public static bool Draw(Rect rect, ItemDefinition def, float alpha = 1f)
        {
            var tex = Get(def);
            if (tex == null) return false;
            var old = GUI.color;
            GUI.color = new Color(old.r, old.g, old.b, old.a * alpha);
            GUI.DrawTexture(rect, tex, ScaleMode.ScaleToFit, true);
            GUI.color = old;
            return true;
        }
    }
}
