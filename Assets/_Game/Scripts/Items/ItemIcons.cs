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

        // 이벤트 그림 (Art/UI/Events/Resources/EventArt/<이벤트 id>, 16:9 한지 바탕)
        public static Texture2D EventArt(string eventId) => string.IsNullOrEmpty(eventId) ? null : Load("EventArt/" + eventId);

        // 시작 화면·프롤로그 (Art/UI/Title/Resources/TitleArt/<이름>)
        //   배경 Bg_Bamboo · Bg_Temple · Bg_IcePalace · Bg_Tomb · Bg_TombSunset, 프롤로그 Prologue_1~4 (16:9)
        //   제목 붓글씨 Title (세로, 투명) · Title_Alt, 낙관 Seal, 메뉴 밑줄 BrushUnderline, 날리는 잎 BambooLeaf
        public static Texture2D TitleArt(string name) => string.IsNullOrEmpty(name) ? null : Load("TitleArt/" + name);

        // 키 그림: 빈 키캡 (글자는 코드로 위에 써요. 키를 바꿔도 맞게), 마우스 좌·우 버튼
        public static Texture2D KeyCap(bool wide = false) => Load(wide ? "KeyIcons/KeyWide" : "KeyIcons/Key");
        public static Texture2D Mouse(bool right) => Load(right ? "KeyIcons/MouseRight" : "KeyIcons/MouseLeft");

        // 빈 키캡을 그리고 가운데에 글자. 키캡 그림이 없으면 false (글자만 쓰면 돼요)
        public static bool DrawKey(Rect rect, string label, GUIStyle style, bool wide = false)
        {
            if (!Draw(rect, KeyCap(wide))) return false;
            GUI.Label(new Rect(rect.x, rect.y - rect.height * 0.06f, rect.width, rect.height), label, style);
            return true;
        }

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
