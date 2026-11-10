using System.Collections.Generic;
using AThousandDawns.Player;
using UnityEngine;

namespace AThousandDawns.Items
{
    // 수묵 아이콘 (Art/UI/Icons/Resources/, 투명 바탕). 없으면 null이라 글자만 보여요.
    //   기물 ItemIcons/<기물 id> · 키워드 KeywordIcons/<Keyword> · 부품 PartIcons/<PartKind> · 혈자리 AcupointIcons/<Acupoint>
    public static class ItemIcons
    {
        static readonly Dictionary<string, Texture2D> Cache = new();

        public static Texture2D Get(ItemDefinition def) => def == null ? null : Get(def.id);
        public static Texture2D Get(string id) => string.IsNullOrEmpty(id) ? null : Load("ItemIcons/" + id);
        public static Texture2D Get(Keyword keyword) => Load("KeywordIcons/" + keyword);
        public static Texture2D Get(PartKind part) => part == PartKind.None ? null : Load("PartIcons/" + part);
        public static Texture2D Get(Acupoint point) => point == Acupoint.None ? null : Load("AcupointIcons/" + point);

        static Texture2D Load(string path)
        {
            if (!Cache.TryGetValue(path, out var tex))
            {
                tex = Resources.Load<Texture2D>(path);
                Cache[path] = tex;
            }
            return tex;
        }

        // IMGUI: 아이콘이 있으면 rect 안에 비율을 지켜 그리고 true
        public static bool Draw(Rect rect, ItemDefinition def, float alpha = 1f) => Draw(rect, Get(def), alpha);

        public static bool Draw(Rect rect, Texture2D tex, float alpha = 1f)
        {
            if (tex == null) return false;
            var old = GUI.color;
            GUI.color = new Color(old.r, old.g, old.b, old.a * alpha);
            GUI.DrawTexture(rect, tex, ScaleMode.ScaleToFit, true);
            GUI.color = old;
            return true;
        }
    }
}
