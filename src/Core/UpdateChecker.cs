using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace CursorRotator.Core;

/// <summary>Resultado de consultar el último release publicado.</summary>
public sealed record UpdateInfo(
    bool HasUpdate, string Tag, string Notes, string DownloadUrl, string PageUrl);

/// <summary>
/// Actualizaciones vía GitHub Releases (sin dependencias): compara el tag
/// con la versión del ensamblado y descarga el .zip del paquete.
/// Sin internet o sin repo configurado = sin actualización, sin errores.
/// </summary>
public static class UpdateChecker
{
    public static string CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "?";

    public static async Task<UpdateInfo> CheckAsync(string repo,
        CancellationToken ct = default)
    {
        var none = new UpdateInfo(false, "", "", "", "");
        try
        {
            repo = (repo ?? "").Trim().Trim('/');
            if (repo.Length == 0 || !repo.Contains('/')) return none;
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("CursorRotator");
            http.Timeout = TimeSpan.FromSeconds(15);
            using var resp = await http.GetAsync(
                $"https://api.github.com/repos/{repo}/releases/latest", ct);
            if (!resp.IsSuccessStatusCode) return none;
            using var doc = JsonDocument.Parse(
                await resp.Content.ReadAsStringAsync(ct));
            var root = doc.RootElement;
            string tag = root.TryGetProperty("tag_name", out var t)
                ? t.GetString() ?? "" : "";
            string notes = root.TryGetProperty("body", out var b)
                ? b.GetString() ?? "" : "";
            if (notes.Length > 1500) notes = notes[..1500] + "…";
            string page = root.TryGetProperty("html_url", out var h)
                ? h.GetString() ?? "" : "";
            string zip = "";
            if (root.TryGetProperty("assets", out var assets))
            {
                foreach (var a in assets.EnumerateArray())
                {
                    string url = a.TryGetProperty("browser_download_url", out var u)
                        ? u.GetString() ?? "" : "";
                    if (url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        zip = url;
                        break;
                    }
                }
            }
            if (!IsNewer(tag, CurrentVersion)) return none;
            return new UpdateInfo(true, tag, notes, zip, page);
        }
        catch { return none; }
    }

    /// <summary>¿Es tag (vX.Y.Z) más nuevo que la versión actual?</summary>
    public static bool IsNewer(string tag, string current)
    {
        try
        {
            int[] Parts(string s) => s.Trim().TrimStart('v', 'V')
                .Split('.', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => int.TryParse(new string(
                    p.TakeWhile(char.IsDigit).ToArray()), out int n) ? n : 0)
                .ToArray();
            var a = Parts(tag);
            var b = Parts(current);
            if (a.Length == 0 || b.Length == 0) return false;
            for (int i = 0; i < Math.Max(a.Length, b.Length); i++)
            {
                int x = i < a.Length ? a[i] : 0;
                int y = i < b.Length ? b[i] : 0;
                if (x != y) return x > y;
            }
            return false;
        }
        catch { return false; }
    }

    public static async Task DownloadAsync(string url, string dest,
        IProgress<int> progress = null, CancellationToken ct = default)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("CursorRotator");
        http.Timeout = TimeSpan.FromMinutes(10);
        using var resp = await http.GetAsync(url,
            HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        long total = resp.Content.Headers.ContentLength ?? -1;
        using var src = await resp.Content.ReadAsStreamAsync(ct);
        using var dst = new FileStream(dest, FileMode.Create,
            FileAccess.Write, FileShare.None);
        var buf = new byte[81920];
        long read = 0;
        int n;
        while ((n = await src.ReadAsync(buf, ct)) > 0)
        {
            await dst.WriteAsync(buf.AsMemory(0, n), ct);
            read += n;
            if (total > 0)
                progress?.Report((int)(read * 100 / total));
        }
        progress?.Report(100);
    }
}
