using System.Collections.Generic;
using AThousandDawns.Core;
using AThousandDawns.Items;
using UnityEngine;

namespace AThousandDawns.Prototype
{
    // 안내 글 속의 조작 키를 키캡 그림으로 (아트 KeyIcons: 빈 키캡 · 긴 키캡 · 마우스 좌/우).
    // 글에는 Key(bind) 표식을 넣어 두고, 그릴 때 Draw가 지금 묶인 키 이름을 키캡 위에 써요 (설정에서 키를 바꿔도 맞게).
    // 키캡 그림이 없거나 글로만 필요하면 Plain으로 키 이름 글자로 바꿔요.
    public static class KeyHint
    {
        const char Open = '⟦', Close = '⟧'; // ⟦Bind⟧

        static readonly Color KeyText = new Color32(0xEF, 0xE6, 0xD2, 0xFF); // 한지색 (키캡 윗면이 투명이라 밝은 글자)
        static readonly Dictionary<int, GUIStyle> Sized = new Dictionary<int, GUIStyle>();
        static GUIStyle _keyStyle;

        public static string Key(Bind bind) => $"{Open}{bind}{Close}";

        // 표식을 키 이름 글자로 (테스트 · 글만 쓰는 곳)
        public static string Plain(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf(Open) < 0) return text;
            var sb = new System.Text.StringBuilder();
            foreach (var (isKey, part) in Split(text)) sb.Append(isKey ? KeyBindings.Label(ParseBind(part)) : part);
            return sb.ToString();
        }

        static Bind ParseBind(string name) => System.Enum.TryParse(name, out Bind b) ? b : Bind.Interact;

        static IEnumerable<(bool isKey, string part)> Split(string text)
        {
            int i = 0;
            while (i < text.Length)
            {
                int open = text.IndexOf(Open, i);
                if (open < 0)
                {
                    yield return (false, text.Substring(i));
                    yield break;
                }
                int close = text.IndexOf(Close, open + 1);
                if (close < 0)
                {
                    yield return (false, text.Substring(i));
                    yield break;
                }
                if (open > i) yield return (false, text.Substring(i, open - i));
                yield return (true, text.Substring(open + 1, close - open - 1));
                i = close + 1;
            }
        }

        // 한 줄로 rect 가운데에 (글 크기 fontSize). 키는 글 높이의 1.5배 키캡으로.
        public static void Draw(Rect rect, string text, GUIStyle style, int fontSize)
        {
            if (string.IsNullOrEmpty(text)) return;
            var textStyle = SizedStyle(style, fontSize);
            bool art = ItemIcons.KeyCap() != null;
            if (!art || text.IndexOf(Open) < 0)
            {
                GUI.Label(rect, Plain(text), textStyle);
                return;
            }

            float keyH = fontSize * 1.5f;
            var parts = new List<(bool isKey, string part, float width)>();
            float total = 0f;
            foreach (var (isKey, part) in Split(text))
            {
                float width = isKey ? KeyWidth(ParseBind(part), keyH) : textStyle.CalcSize(new GUIContent(part)).x;
                parts.Add((isKey, part, width));
                total += width;
            }

            float x = rect.x + (rect.width - total) * 0.5f, cy = rect.center.y;
            foreach (var (isKey, part, width) in parts)
            {
                if (isKey) DrawKey(new Rect(x, cy - keyH * 0.5f, width, keyH), ParseBind(part), fontSize);
                else
                {
                    var leftAligned = new GUIStyle(textStyle) { alignment = TextAnchor.MiddleLeft };
                    GUI.Label(new Rect(x, rect.y, width + 2f, rect.height), part, leftAligned);
                }
                x += width;
            }
        }

        static bool IsMouse(Bind bind, out bool right)
        {
            string path = KeyBindings.Path(bind);
            right = path == "<Mouse>/rightButton";
            return right || path == "<Mouse>/leftButton";
        }

        static float KeyWidth(Bind bind, float keyH)
        {
            if (IsMouse(bind, out _)) return keyH * 1.1f;
            string label = KeyBindings.Label(bind);
            if (label.Length <= 1) return keyH * 1.15f;
            return Mathf.Max(keyH * 2.2f, KeyStyle(keyH).CalcSize(new GUIContent(label)).x + keyH * 1.2f);
        }

        // 그림 안 모양의 자리 (256px 그림 기준): 키캡 윗면 가운데에 글자
        static void DrawKey(Rect slot, Bind bind, int fontSize)
        {
            float keyH = slot.height;
            if (IsMouse(bind, out bool right))
            {
                ItemIcons.Draw(new Rect(slot.center.x - keyH * 0.6f, slot.y - keyH * 0.1f, keyH * 1.2f, keyH * 1.2f), ItemIcons.Mouse(right));
                return;
            }
            string label = KeyBindings.Label(bind);
            bool wide = label.Length > 1;
            var tex = ItemIcons.KeyCap(wide);
            Rect face;
            if (wide)
            {
                // 긴 키캡: 그림 세로 31~65%에 모양, 윗면 가운데는 42%. 가로로 늘려서 그려요
                float h = keyH / 0.34f;
                var r = new Rect(slot.x - slot.width * 0.05f, slot.center.y - h * 0.48f, slot.width * 1.1f, h);
                GUI.DrawTexture(r, tex, ScaleMode.StretchToFill);
                face = new Rect(r.x, r.y + h * 0.42f - keyH * 0.35f, r.width, keyH * 0.7f);
            }
            else
            {
                // 네모 키캡: 모양이 세로 15~80%, 윗면 가운데는 37%
                float s = keyH * 1.4f;
                var r = new Rect(slot.center.x - s * 0.5f, slot.center.y - s * 0.47f, s, s);
                GUI.DrawTexture(r, tex, ScaleMode.ScaleToFit);
                face = new Rect(r.x, r.y + s * 0.37f - keyH * 0.35f, r.width, keyH * 0.7f);
            }
            GUI.Label(face, label, KeyStyle(keyH));
        }

        static GUIStyle KeyStyle(float keyH)
        {
            _keyStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, clipping = TextClipping.Overflow };
            _keyStyle.fontSize = Mathf.RoundToInt(keyH * 0.5f);
            _keyStyle.normal.textColor = _keyStyle.hover.textColor = KeyText;
            return _keyStyle;
        }

        static GUIStyle SizedStyle(GUIStyle style, int fontSize)
        {
            int key = style.GetHashCode() * 31 + fontSize;
            if (!Sized.TryGetValue(key, out var s) || s == null)
            {
                s = new GUIStyle(style) { fontSize = fontSize, richText = true, clipping = TextClipping.Overflow };
                Sized[key] = s;
            }
            return s;
        }
    }
}
