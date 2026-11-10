# 흰 바탕 수묵 아이콘 → 투명 바탕 256px PNG.
# 흰색을 알파로 빼요 (GIMP "Color to Alpha"와 같은 식): 알파 = 흰색에서 가장 먼 채널, 색 = 흰색을 걷어낸 값.
# 사용: powershell -File icon_alpha.ps1 <원본.png> <결과.png> [크기=256] [-NoHalo]
#   -NoHalo: 한지 바탕 위에 올릴 그림(제목 붓글씨 등). 둘레 번짐 없이, 가로 크기만 맞추고 비율은 원본대로
#   -Solid : 연한 색(분홍 꽃잎 등)도 비치지 않게. 흰색에서 조금만 멀어도 불투명, 원래 색 그대로 (가장자리만 부드럽게)
param([string]$Src, [string]$Dst, [int]$Size = 256, [switch]$NoHalo, [switch]$Solid)

Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class IconAlpha
{
    // 종이 얼룩이 남지 않게 흰색에 가까운 값(Floor 이하)은 완전 투명으로 봐요.
    const int Floor = 14;

    public static void Run(string src, string dst, int size, bool halo, bool solid)
    {
        using (var input = new Bitmap(src))
        using (var bmp = new Bitmap(size, halo ? size : (int)Math.Round((double)size * input.Height / input.Width), PixelFormat.Format32bppArgb))
        {
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(input, new Rectangle(0, 0, bmp.Width, bmp.Height));
            }
            var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            var data = bmp.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            var px = new byte[data.Stride * bmp.Height];
            Marshal.Copy(data.Scan0, px, 0, px.Length);
            for (int i = 0; i < px.Length; i += 4)
            {
                int b = px[i], g = px[i + 1], r = px[i + 2];
                int d = Math.Max(255 - r, Math.Max(255 - g, 255 - b));
                int a = Math.Max(0, Math.Min(255, (d - Floor) * (solid ? 1020 : 255) / (255 - Floor)));
                if (a == 0) { px[i] = px[i + 1] = px[i + 2] = px[i + 3] = 0; continue; }
                if (solid) { px[i + 3] = (byte)a; continue; } // 색은 원본 그대로
                float af = d / 255f;
                px[i]     = Unmix(b, af);
                px[i + 1] = Unmix(g, af);
                px[i + 2] = Unmix(r, af);
                px[i + 3] = (byte)a;
            }
            if (halo) AddHalo(px, size);
            Marshal.Copy(px, 0, data.Scan0, px.Length);
            bmp.UnlockBits(data);
            bmp.Save(dst, ImageFormat.Png);
        }
    }

    // 검은 먹이 어두운 UI에서도 보이게, 실루엣 둘레에 한지색(#EFE6D2) 번짐을 깔아요.
    const int HaloRadius = 5;
    const float HaloStrength = 0.9f;

    static void AddHalo(byte[] px, int size)
    {
        var mask = new float[size * size];
        for (int i = 0; i < mask.Length; i++) mask[i] = px[i * 4 + 3] / 255f;
        mask = Blur(Blur(mask, size), size);
        for (int i = 0; i < mask.Length; i++)
        {
            float h = Math.Min(1f, mask[i] * 3f) * HaloStrength;
            float a = px[i * 4 + 3] / 255f;
            float outA = a + h * (1f - a);
            if (outA <= 0f) continue;
            px[i * 4]     = Over(px[i * 4], 0xD2, a, h, outA);
            px[i * 4 + 1] = Over(px[i * 4 + 1], 0xE6, a, h, outA);
            px[i * 4 + 2] = Over(px[i * 4 + 2], 0xEF, a, h, outA);
            px[i * 4 + 3] = (byte)Math.Round(outA * 255f);
        }
    }

    static byte Over(int c, int under, float a, float h, float outA)
    {
        return (byte)Math.Round((c * a + under * h * (1f - a)) / outA);
    }

    // 가로·세로 상자 흐림
    static float[] Blur(float[] src, int size)
    {
        var tmp = new float[src.Length];
        var dst = new float[src.Length];
        int n = HaloRadius * 2 + 1;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float s = 0f;
                for (int k = -HaloRadius; k <= HaloRadius; k++) { int xx = x + k; if (xx >= 0 && xx < size) s += src[y * size + xx]; }
                tmp[y * size + x] = s / n;
            }
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float s = 0f;
                for (int k = -HaloRadius; k <= HaloRadius; k++) { int yy = y + k; if (yy >= 0 && yy < size) s += tmp[yy * size + x]; }
                dst[y * size + x] = s / n;
            }
        return dst;
    }

    // c = a*색 + (1-a)*흰색 → 색 = (c - (1-a)*255) / a
    static byte Unmix(int c, float a)
    {
        float v = (c - (1f - a) * 255f) / a;
        return (byte)Math.Max(0, Math.Min(255, Math.Round(v)));
    }
}
"@

[IconAlpha]::Run((Resolve-Path $Src).Path, [IO.Path]::GetFullPath($Dst), $Size, -not $NoHalo, [bool]$Solid)
