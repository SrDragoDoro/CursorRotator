using Microsoft.Win32;

namespace CursorRotator.Core;

/// <summary>Esquema de cursores: nombre visible + rutas de archivos en orden.</summary>
public sealed record CursorScheme(string Name, string[] Paths);

/// <summary>
/// Lectura/escritura de esquemas en HKCU\Control Panel\Cursors\Schemes
/// y aplicación del esquema activo en HKCU\Control Panel\Cursors.
/// Todo en HKCU: no requiere permisos de administrador (sin UAC).
/// </summary>
public static class SchemeStore
{
    public const string CursorsKeyPath = @"Control Panel\Cursors";
    public const string SchemesKeyPath = @"Control Panel\Cursors\Schemes";

    /// <summary>15 roles estándar de Windows, en el orden del valor del esquema.</summary>
    public static readonly string[] StandardRoles =
    [
        "Arrow", "Help", "AppStarting", "Wait", "Crosshair",
        "IBeam", "NWPen", "No", "SizeNS", "SizeWE",
        "SizeNWSE", "SizeNESW", "SizeAll", "UpArrow", "Hand",
    ];

    public static List<string> GetSchemeNames()
    {
        using var key = Registry.CurrentUser.OpenSubKey(SchemesKeyPath, writable: false);
        return key?.GetValueNames().OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList()
               ?? new List<string>();
    }

    public static bool Exists(string schemeName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(SchemesKeyPath, writable: false);
        return key?.GetValue(schemeName) is string;
    }

    public static string[] GetSchemePaths(string schemeName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(SchemesKeyPath, writable: false)
            ?? throw new InvalidOperationException("No existe la clave de esquemas en el registro.");
        var raw = key.GetValue(schemeName) as string
            ?? throw new InvalidOperationException($"El esquema '{schemeName}' no está registrado.");
        return raw.Split(',').Select(p => p.Trim())
            .Where(p => p.Length > 0).ToArray();
    }

    /// <summary>
    /// Sanitiza una ruta de esquema: expande %variables% y, si no es absoluta
    /// (algunos .inf registran rutas relativas al dir de cursores del sistema),
    /// la resuelve contra %WINDIR%\Cursors. Los corchetes ([pixloen]) se
    /// conservan tal cual: nunca se interpretan como patrón.
    /// </summary>
    public static string ResolveCursorPath(string raw)
    {
        string path = Environment.ExpandEnvironmentVariables((raw ?? "").Trim().Trim('"'));
        if (!Path.IsPathRooted(path))
        {
            string cursorsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Cursors");
            path = Path.Combine(cursorsDir, path);
        }
        return path;
    }

    /// <summary>Rutas del esquema ya sanitizadas (variables + relativas).</summary>
    public static string[] GetResolvedSchemePaths(string schemeName) =>
        GetSchemePaths(schemeName).Select(ResolveCursorPath).ToArray();

    public static void SetScheme(string schemeName, IEnumerable<string> paths)
    {
        using var key = Registry.CurrentUser.CreateSubKey(SchemesKeyPath)
            ?? throw new InvalidOperationException("No se pudo crear la clave de esquemas.");
        key.SetValue(schemeName, string.Join(",", paths));
    }

    /// <summary>
    /// Elimina un esquema del registro y su carpeta — solo si está dentro del
    /// almacén (%LOCALAPPDATA%\Cursors). Nunca borra fuera de ahí.
    /// </summary>
    /// <returns>true si se eliminó el registro (la carpeta es best-effort).</returns>
    public static bool DeleteScheme(string schemeName, string storeDir)
    {
        string[] paths;
        try { paths = GetSchemePaths(schemeName); }
        catch { paths = []; }
        bool removed = false;
        using (var key = Registry.CurrentUser.OpenSubKey(SchemesKeyPath, writable: true))
        {
            if (key?.GetValue(schemeName) is not null)
            {
                key.DeleteValue(schemeName);
                removed = true;
            }
        }
        try
        {
            string first = paths.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p));
            string dir = !string.IsNullOrEmpty(first)
                ? Path.GetDirectoryName(first) : null;
            string store = Path.GetFullPath(storeDir).TrimEnd(Path.DirectorySeparatorChar)
                         + Path.DirectorySeparatorChar;
            if (!string.IsNullOrEmpty(dir))
            {
                string full = Path.GetFullPath(dir);
                if (full.StartsWith(store, StringComparison.OrdinalIgnoreCase) &&
                    full.Length > store.Length && Directory.Exists(full))
                    Directory.Delete(full, recursive: true);
            }
        }
        catch { /* carpeta best-effort */ }
        return removed;
    }

    /// <summary>
    /// Aplica un esquema como cursor activo: 15 roles por posición y los
    /// extras (Person/Pin/...) por nombre de archivo, igual que los .inf.
    /// Valida cada archivo ANTES de escribir: si falta, lanza
    /// FileNotFoundException con la ruta evaluada (nada silencioso).
    /// </summary>
    public static void ApplyScheme(string schemeName)
    {
        var paths = GetResolvedSchemePaths(schemeName);
        foreach (var p in paths)
        {
            if (!File.Exists(p))
                throw new FileNotFoundException(
                    $"No se pudo aplicar '{schemeName}': " +
                    $"No se encontró el archivo en la ruta '{p}'.", p);
        }
        using var key = Registry.CurrentUser.CreateSubKey(CursorsKeyPath)
            ?? throw new InvalidOperationException("No se pudo abrir la clave de cursores.");

        int n = Math.Min(paths.Length, StandardRoles.Length);
        for (int i = 0; i < n; i++)
            key.SetValue(StandardRoles[i], paths[i]);

        for (int i = StandardRoles.Length; i < paths.Length; i++)
        {
            var leaf = Path.GetFileName(paths[i]).ToLowerInvariant();
            string role = null;
            if (leaf.Contains("person")) role = "Person";
            else if (leaf.Contains("pin")) role = "Pin";
            if (role is not null)
                key.SetValue(role, paths[i]);
        }

        key.SetValue("Scheme Source", 2, RegistryValueKind.DWord);
        NativeMethods.RefreshCursors();
    }

    /// <summary>Abre el panel de propiedades del mouse (cambio manual).</summary>
    public static void OpenMouseProperties()
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "rundll32.exe",
            Arguments = "shell32.dll,Control_RunDLL main.cpl,,1",
            UseShellExecute = true,
        });
    }
}
