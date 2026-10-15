using CursorRotator.Core;

namespace CursorRotator.Tests;

/// <summary>Carpeta temporal auto-limpiada (patrón para tests que escriben en disco).</summary>
public sealed class TempDir : IDisposable
{
    public string Root { get; }

    public TempDir()
    {
        Root = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "CursorRotatorTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string Combine(params string[] parts) =>
        System.IO.Path.Combine(new[] { Root }.Concat(parts).ToArray());

    /// <summary>Escribe un archivo relativo (acepta '/' como separador de ruta).</summary>
    public string Write(string relative, string content)
    {
        string full = Combine(relative.Split('/'));
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
        return full;
    }

    public void Dispose()
    {
        try { if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true); }
        catch { }
    }
}

/// <summary>Ayudas para esquemas reales en HKCU (con limpieza garantizada).</summary>
public static class TestRegistry
{
    /// <summary>Crea N esquemas temporales (nombre único) y devuelve nombres y carpetas.</summary>
    public static (List<string> Names, List<string> Dirs) RegisterSchemes(int count, string storeRoot)
    {
        var names = new List<string>();
        var dirs = new List<string>();
        for (int i = 0; i < count; i++)
        {
            string name = "zzUnitScheme_" + Guid.NewGuid().ToString("N")[..8];
            string dir = System.IO.Path.Combine(storeRoot, name);
            Directory.CreateDirectory(dir);
            string cur = System.IO.Path.Combine(dir, "arrow.cur");
            File.WriteAllText(cur, "x");
            SchemeStore.SetScheme(name, new[] { cur });
            names.Add(name);
            dirs.Add(dir);
        }
        return (names, dirs);
    }

    public static void Unregister(IEnumerable<string> names, string storeRoot)
    {
        foreach (var n in names)
        {
            try { SchemeStore.DeleteScheme(n, storeRoot); } catch { }
        }
    }
}
