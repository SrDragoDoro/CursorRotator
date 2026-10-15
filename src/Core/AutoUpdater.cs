using System.Diagnostics;
using System.IO.Compression;

namespace CursorRotator.Core;

/// <summary>
/// Auto-Updater integrado: descarga el .zip del release, lo extrae y se
/// reemplaza solo. Como Windows bloquea el .exe en ejecución, el reemplazo
/// lo hace un .bat generado al vuelo (espera, xcopy, relanza, se borra).
/// Sin UI aquí dentro: todo testeable headless (el .bat NO se ejecuta).
/// </summary>
public static class AutoUpdater
{
    public const string ExeName = "CursorRotator.exe";

    /// <summary>Rutas de staging en %TEMP% (limpia restos anteriores).</summary>
    public static (string ZipPath, string ExtractDir, string BatPath) PrepareStaging()
    {
        string zip = Path.Combine(Path.GetTempPath(), "CursorRotator_Update.zip");
        string dir = Path.Combine(Path.GetTempPath(), "CursorRotator_Update");
        try
        {
            if (File.Exists(zip)) File.Delete(zip);
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            Directory.CreateDirectory(dir);
        }
        catch { }
        return (zip, dir, Path.Combine(dir, "Update.bat"));
    }

    /// <summary>
    /// Genera el Update.bat que aplica la actualización. Devuelve su ruta.
    /// Flujo: esperar salida → xcopy encima → relanzar → borrar restos.
    /// </summary>
    public static string GenerateUpdateScript(string extractDir, string appDir,
        string batPath, string zipPath)
    {
        var lines = new[]
        {
            "@echo off",
            "rem Auto-Updater de CursorRotator: aplica la descarga y se borra.",
            "timeout /t 2 /nobreak > NUL",
            $"xcopy /Y /E /C \"{extractDir}\\*\" \"{appDir}\\\"",
            $"start \"\" \"{Path.Combine(appDir, ExeName)}\"",
            $"rmdir /S /Q \"{extractDir}\"",
            $"del \"{zipPath}\"",
            "del \"%~f0\"",
        };
        File.WriteAllLines(batPath, lines, System.Text.Encoding.Default);
        return batPath;
    }

    /// <summary>Lanza el .bat oculto (sin consola negra a la vista).</summary>
    public static void LaunchUpdate(string batPath)
    {
        using var p = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = batPath,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            },
        };
        p.Start();
        p.Dispose();
    }
}
