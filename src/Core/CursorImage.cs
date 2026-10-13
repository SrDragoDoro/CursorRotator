using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;

namespace CursorRotator.Core;

/// <summary>
/// Convierte el primer frame de un .cur/.ani en Bitmap (para previsualizar).
/// PNG directo; DIB 32/24/8/4/1bpp con máscara AND. Null si no se puede.
/// </summary>
public static class CursorImage
{
    public static Bitmap LoadFirstFrame(string path)
    {
        try
        {
            path = SchemeStore.ResolveCursorPath(path);
            byte[] bytes = File.ReadAllBytes(path);
            byte[] img = path.EndsWith(".ani", StringComparison.OrdinalIgnoreCase)
                ? FirstFrame(bytes) ?? bytes
                : bytes;
            return DecodeImage(img);
        }
        catch { return null; }
    }

    private static byte[] FirstFrame(byte[] riff)
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
                var found = FindIcon(buf, data + 4, Math.Min(data + size, end));
                if (found is not null) return found;
            }
            p = data + size + (size % 2);
        }
        return null;
    }

    private static Bitmap DecodeImage(byte[] data)
    {
        if (data.Length > 8 && data[0] == 0x89 && data[1] == 0x50)
            return new Bitmap(new MemoryStream(data));
        if (data.Length >= 6 && data[0] == 0 && data[1] == 0
            && BitConverter.ToUInt16(data, 2) is 1 or 2
            && BitConverter.ToUInt16(data, 4) is > 0 and <= 32)
        {
            int count = BitConverter.ToUInt16(data, 4);
            int best = -1;
            for (int i = 0; i < count; i++)
            {
                int e = 6 + i * 16;
                if (e + 16 > data.Length) break;
                int bytesInRes = BitConverter.ToInt32(data, e + 8);
                int imgOff = BitConverter.ToInt32(data, e + 12);
                if (imgOff > 0 && bytesInRes > 0 && imgOff + bytesInRes <= data.Length
                    && (best < 0 || bytesInRes > BitConverter.ToInt32(data, 6 + best * 16 + 8)))
                    best = i;
            }
            if (best >= 0)
            {
                int e = 6 + best * 16;
                int off = BitConverter.ToInt32(data, e + 12);
                int len = Math.Min(BitConverter.ToInt32(data, e + 8), data.Length - off);
                return DecodeImage(data[off..(off + len)]);
            }
            return null;
        }
        if (data.Length > 16 + 40 && BitConverter.ToInt32(data, 12) == 16
            && BitConverter.ToInt32(data, 16) == 40)
            return DecodeDib(data, 16);
        if (data.Length > 40 && BitConverter.ToInt32(data, 0) == 40)
            return DecodeDib(data, 0);
        return null;
    }

    private static Bitmap DecodeDib(byte[] d, int off)
    {
        int w = BitConverter.ToInt32(d, off + 4);
        int h2 = BitConverter.ToInt32(d, off + 8);
        int bpp = BitConverter.ToUInt16(d, off + 14);
        int comp = BitConverter.ToInt32(d, off + 16);
        int clrUsed = BitConverter.ToInt32(d, off + 32);
        if (w <= 0 || w > 512 || h2 == 0 || Math.Abs(h2) / 2 > 512) return null;
        bool topDown = h2 < 0;
        int h = Math.Abs(h2) / 2;
        int header = 40 + (comp == 3 ? 12 : 0);
        int palCount = bpp <= 8 ? (clrUsed != 0 ? clrUsed : 1 << bpp) : 0;
        int pxStart = off + header + palCount * 4;
        int xorStride = ((w * bpp + 31) / 32) * 4;
        int maskStride = ((w + 31) / 32) * 4;
        int maskStart = pxStart + xorStride * h;
        if (maskStart > d.Length) return null;

        int[] palette = new int[palCount];
        for (int i = 0; i < palCount; i++)
        {
            int p = off + header + i * 4;
            if (p + 4 > d.Length) return null;
            palette[i] = (d[p + 2] << 16) | (d[p + 1] << 8) | d[p];
        }

        var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        var rect = new Rectangle(0, 0, w, h);
        var data = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            int stride = Math.Abs(data.Stride);
            var buf = new byte[stride * h];
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
                        default: bmp.UnlockBits(data); bmp.Dispose(); return null;
                    }
                    int mb = rowMask + x / 8;
                    if (mb < d.Length && ((d[mb] >> (7 - x % 8)) & 1) == 1)
                        a = 0;
                    else if (bpp == 32 && a == 0)
                        a = 255; // DIB sin canal alfa real: opaco salvo máscara
                    int o = y * stride + x * 4;
                    buf[o] = (byte)b; buf[o + 1] = (byte)g;
                    buf[o + 2] = (byte)r; buf[o + 3] = (byte)a;
                }
            }
            Marshal.Copy(buf, 0, data.Scan0, buf.Length);
        }
        finally
        {
            try { bmp.UnlockBits(data); } catch { }
        }
        return bmp;
    }
}
