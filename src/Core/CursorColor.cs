using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace CursorRotator.Core;

/// <summary>
/// Lee el color dominante del cursor activo (Arrow) desde su .cur/.ani,
/// ignorando píxeles transparentes (histograma RGB cuantizado).
/// .cur = formato ICO (PNG o DIB); .ani = RIFF/ACON, se usa el primer frame.
/// </summary>
public static class CursorColorReader
{
    /// <summary>Ruta del puntero Normal (Arrow) activo.</summary>
    public static string GetActiveArrowPath()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Cursors", writable: false);
        return (key?.GetValue("Arrow") as string)?.Trim();
    }

    public static Color? GetActiveArrowColor()
    {
        string path = GetActiveArrowPath();
        if (string.IsNullOrEmpty(path))
            return null;
        try { return GetDominantColor(SchemeStore.ResolveCursorPath(path)); }
        catch { return null; }
    }

    public static Color? GetDominantColor(string path)
    {
        var palette = GetPalette(path);
        return palette.Length > 0 ? palette[0] : null;
    }

    /// <summary>
    /// Paleta de hasta 3 colores dominantes (bins más frecuentes, distintos).
    /// Comparte filtros estrictos con el modo unicolor.
    /// </summary>
    public static Color[] GetPalette(string path, int maxColors = 3)
    {
        try
        {
            path = SchemeStore.ResolveCursorPath(path);
            byte[] bytes = File.ReadAllBytes(path);
            var hist = new ColorHistogram();
            if (path.EndsWith(".ani", StringComparison.OrdinalIgnoreCase))
            {
                byte[] frame = FirstAniFrame(bytes);
                if (frame is null) return [];
                AccumulateImage(hist, frame);
            }
            else AccumulateImage(hist, bytes);
            return hist.TopColors(Math.Clamp(maxColors, 1, 5));
        }
        catch { return []; }
    }

    private static void AccumulateImage(ColorHistogram hist, byte[] data)
    {
        foreach (var img in EnumerateImages(data))
        {
            try
            {
                if (img.Length > 8 && img[0] == 0x89 && img[1] == 0x50)
                    AccumulatePng(hist, img);
                else
                    AccumulateDib(hist, img, 0);
            }
            catch { /* entrada corrupta: se salta */ }
        }
    }

    private static void AccumulatePng(ColorHistogram hist, byte[] png)
    {
        using var ms = new MemoryStream(png);
        using var bmp = new Bitmap(ms);
        var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
        var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            int bytes = Math.Abs(data.Stride) * data.Height;
            var buf = new byte[bytes];
            Marshal.Copy(data.Scan0, buf, 0, bytes);
            // Memoria 32bpp = orden Blue, Green, Red, Alpha (formato Windows).
            for (int i = 0; i < bytes; i += 4)
                hist.Add(buf[i + 2], buf[i + 1], buf[i], buf[i + 3]);
        }
        finally { bmp.UnlockBits(data); }
    }

    /// <summary>Deriva [base, claro, oscuro] de un color manual.</summary>
    public static Color[] DerivePalette(Color baseColor) =>
    [
        baseColor,
        LerpWhite(baseColor, 0.45f),
        LerpBlack(baseColor, 0.45f),
    ];

    private static Color LerpWhite(Color c, float f) => Color.FromArgb(
        (int)(c.R + (255 - c.R) * f),
        (int)(c.G + (255 - c.G) * f),
        (int)(c.B + (255 - c.B) * f));

    private static Color LerpBlack(Color c, float f) => Color.FromArgb(
        (int)(c.R * (1 - f)), (int)(c.G * (1 - f)), (int)(c.B * (1 - f)));

    // ---------- ANI (RIFF) ----------
    private static byte[] FirstAniFrame(byte[] riff)
    {
        if (riff.Length < 12 || Encoding.ASCII.GetString(riff, 0, 4) != "RIFF"
            || Encoding.ASCII.GetString(riff, 8, 4) != "ACON")
            return null;
        return FindIcon(riff, 12, riff.Length);
    }

    private static byte[] FindIcon(byte[] buf, int start, int end)
    {
        int p = start;
        while (p + 8 <= end)
        {
            string id = Encoding.ASCII.GetString(buf, p, 4);
            int size = BitConverter.ToInt32(buf, p + 4);
            if (size < 0 || p + 8 + size > end + 1) break;
            int data = p + 8;
            if (id == "icon")
                return buf[data..(data + size)];
            if (id == "LIST")
            {
                // Primeros 4 bytes = tipo (ej. "fram"); resto = subchunks.
                var found = FindIcon(buf, data + 4, Math.Min(data + size, end));
                if (found is not null) return found;
            }
            p = data + size + (size % 2); // chunks alineados a palabra
        }
        return null;
    }

    // ---------- ICO / imagen ----------
    /// <summary>
    /// Resuelve cada imagen contenida como bloque independiente (PNG o DIB).
    /// Los .cur modernos traen PNG comprimido: también se devuelven.
    /// </summary>
    private static List<byte[]> EnumerateImages(byte[] data)
    {
        var imgs = new List<byte[]>();
        if (data.Length > 8 && data[0] == 0x89 && data[1] == 0x50) // PNG suelto
        {
            imgs.Add(data);
            return imgs;
        }
        // ¿ICONDIR? reserved=0, type 1/2, count 1..32, offsets coherentes.
        if (data.Length >= 6 && data[0] == 0 && data[1] == 0
            && BitConverter.ToUInt16(data, 2) is 1 or 2
            && BitConverter.ToUInt16(data, 4) is > 0 and <= 32)
        {
            int count = BitConverter.ToUInt16(data, 4);
            for (int i = 0; i < count; i++)
            {
                int e = 6 + i * 16;
                if (e + 16 > data.Length) break;
                int bytesInRes = BitConverter.ToInt32(data, e + 8);
                int imgOff = BitConverter.ToInt32(data, e + 12);
                if (imgOff <= 0 || bytesInRes <= 0 || imgOff + bytesInRes > data.Length)
                    continue;
                imgs.Add(data[imgOff..(imgOff + bytesInRes)]);
            }
            if (imgs.Count > 0) return imgs;
        }
        // ¿ICONDIRENTRY (16 B) + DIB?
        if (data.Length > 16 + 40)
        {
            int bytesInRes = BitConverter.ToInt32(data, 8);
            int imgOff = BitConverter.ToInt32(data, 12);
            if (imgOff == 16 && bytesInRes > 0 && 16 + bytesInRes <= data.Length
                && BitConverter.ToInt32(data, 16) == 40)
            {
                imgs.Add(data[16..(16 + bytesInRes)]);
                return imgs;
            }
        }
        // DIB crudo (BITMAPINFOHEADER).
        if (data.Length > 40 && BitConverter.ToInt32(data, 0) == 40)
            imgs.Add(data);
        return imgs;
    }

    private static void AccumulateDib(ColorHistogram hist, byte[] d, int off)
    {
        int w = BitConverter.ToInt32(d, off + 4);
        int h2 = BitConverter.ToInt32(d, off + 8);
        int bpp = BitConverter.ToUInt16(d, off + 14);
        int comp = BitConverter.ToInt32(d, off + 16);
        int clrUsed = BitConverter.ToInt32(d, off + 32);
        if (w <= 0 || w > 512 || h2 == 0 || Math.Abs(h2) / 2 > 512) return;
        bool topDown = h2 < 0;
        int h = Math.Abs(h2) / 2;
        int header = 40 + (comp == 3 ? 12 : 0); // BI_BITFIELDS trae 3 máscaras
        int palCount = bpp <= 8 ? (clrUsed != 0 ? clrUsed : 1 << bpp) : 0;
        int pxStart = off + header + palCount * 4;
        int xorStride = ((w * bpp + 31) / 32) * 4;
        int maskStride = ((w + 31) / 32) * 4;
        int maskStart = pxStart + xorStride * h;
        if (maskStart > d.Length) return;

        int[] palette = new int[palCount];
        for (int i = 0; i < palCount; i++)
        {
            int p = off + header + i * 4;
            if (p + 4 > d.Length) return;
            palette[i] = (d[p + 2] << 16) | (d[p + 1] << 8) | d[p]; // RGB
        }

        for (int y = 0; y < h; y++)
        {
            int srcY = topDown ? y : h - 1 - y;
            int rowXor = pxStart + srcY * xorStride;
            int rowMask = maskStart + srcY * maskStride;
            for (int x = 0; x < w; x++)
            {
                int r = 0, g = 0, b = 0, a = 255;
                switch (bpp)
                {
                    case 32:
                    {
                        // DIB 32bpp = orden Blue, Green, Red, Alpha en memoria.
                        int p = rowXor + x * 4;
                        if (p + 4 > d.Length) break;
                        b = d[p]; g = d[p + 1]; r = d[p + 2]; a = d[p + 3];
                        break;
                    }
                    case 24:
                    {
                        int p = rowXor + x * 3;
                        if (p + 3 > d.Length) break;
                        b = d[p]; g = d[p + 1]; r = d[p + 2];
                        break;
                    }
                    case 8 or 4 or 1:
                    {
                        int idx;
                        if (bpp == 8)
                        {
                            if (rowXor + x >= d.Length) continue;
                            idx = d[rowXor + x];
                        }
                        else if (bpp == 4)
                        {
                            if (rowXor + x / 2 >= d.Length) continue;
                            idx = (d[rowXor + x / 2] >> (x % 2 == 0 ? 4 : 0)) & 0xF;
                        }
                        else
                        {
                            if (rowXor + x / 8 >= d.Length) continue;
                            idx = (d[rowXor + x / 8] >> (7 - x % 8)) & 1;
                        }
                        if (idx >= palCount) continue;
                        r = (palette[idx] >> 16) & 0xFF;
                        g = (palette[idx] >> 8) & 0xFF;
                        b = palette[idx] & 0xFF;
                        break;
                    }
                    default: return;
                }
                // Máscara AND: bit 1 = transparente (fondo del XOR).
                bool masked = false;
                int mb = rowMask + x / 8;
                if (mb < d.Length)
                    masked = ((d[mb] >> (7 - x % 8)) & 1) == 1;
                if (masked) continue;
                hist.Add(r, g, b, a);
            }
        }
    }

    /// <summary>
    /// Cuantización por frecuencia real: filtra alfa &lt; 200, negros de
    /// contorno/sombra y blancos puros; agrupa RGB/16 y gana el más frecuente.
    /// </summary>
    private sealed class ColorHistogram
    {
        private readonly Dictionary<int, (long R, long G, long B, long N)> _h = new();

        public void Add(int r, int g, int b, int a)
        {
            if (a < 200) return;
            if (r < 20 && g < 20 && b < 20) return;
            if (r > 245 && g > 245 && b > 245) return;
            int key = (r >> 4 << 8) | (g >> 4 << 4) | (b >> 4);
            var e = _h.TryGetValue(key, out var v) ? v : (0, 0, 0, 0);
            e.R += r; e.G += g; e.B += b; e.N++;
            _h[key] = e;
        }

        public Color? Dominant()
        {
            if (_h.Count == 0) return null;
            var best = _h.Values.OrderByDescending(v => v.N).First();
            return Color.FromArgb((int)(best.R / best.N), (int)(best.G / best.N), (int)(best.B / best.N));
        }

        /// <summary>
        /// Top-N colores distintos: ordena por frecuencia y salta bins vecinos
        /// (distancia Chebyshev &lt; 2 en espacio cuantizado) para no repetir tonos.
        /// </summary>
        public Color[] TopColors(int max)
        {
            var result = new List<Color>();
            var picked = new List<(int R, int G, int B)>();
            foreach (var kv in _h.OrderByDescending(kv => kv.Value.N))
            {
                int qr = (kv.Key >> 8) & 0xF, qg = (kv.Key >> 4) & 0xF, qb = kv.Key & 0xF;
                if (picked.Any(p => Math.Abs(p.R - qr) < 2 && Math.Abs(p.G - qg) < 2 && Math.Abs(p.B - qb) < 2))
                    continue;
                picked.Add((qr, qg, qb));
                var v = kv.Value;
                result.Add(Color.FromArgb((int)(v.R / v.N), (int)(v.G / v.N), (int)(v.B / v.N)));
                if (result.Count >= max) break;
            }
            return result.ToArray();
        }
    }
}
