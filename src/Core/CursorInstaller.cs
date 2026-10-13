using System.IO.Compression;
using Microsoft.Win32;

namespace CursorRotator.Core;

/// <summary>
/// Instalador en 1 clic: detecta el bundle (.zip/.rar o carpetas ya
/// extraídas), copia los .ani/.cur a %LOCALAPPDATA%\Cursors y registra
/// los esquemas en HKCU. Sin UAC: todo es por-usuario.
/// </summary>
public sealed class CursorInstaller
{
    public static string DefaultStoreDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Cursors");

    private static readonly string[] WinRarCandidates =
    [
        @"C:\Program Files\WinRAR\UnRAR.exe",
        @"C:\Program Files\WinRAR\Rar.exe",
        @"C:\Program Files\WinRAR\WinRAR.exe",
        @"C:\Program Files (x86)\WinRAR\UnRAR.exe",
        @"C:\Program Files (x86)\WinRAR\Rar.exe",
        @"C:\Program Files (x86)\WinRAR\WinRAR.exe",
    ];

    public record InstallResult(List<string> Installed, List<string> Errors, List<string> Skipped);

    /// <summary>Decisión ante un esquema duplicado (ya instalado).</summary>
    public enum DuplicateAction
    {
        Overwrite,
        Skip,
        SkipAll,
    }

    /// <summary>
    /// Candidato de instalación: .inf origen, grupo del pack, esquema
    /// parseado y si ya estaba instalado al escanear.
    /// </summary>
    public sealed record InstallCandidate(
        string InfPath, string Group, ParsedScheme Scheme, bool AlreadyInstalled);

    /// <summary>Busca material instalable junto al exe, su padre o cwd.</summary>
    public static string AutoDetectSource()
    {
        string exeDir = AppContext.BaseDirectory;
        var dirs = new[]
        {
            exeDir,
            Directory.GetParent(exeDir.TrimEnd(Path.DirectorySeparatorChar))?.FullName,
            Environment.CurrentDirectory,
        }.Where(d => d is not null).Distinct();

        foreach (var d in dirs)
        {
            // 1. Archivos comprimidos de primer nivel (no anidados tipo MacOS).
            var bundle = Directory.EnumerateFiles(d, "*.*", SearchOption.TopDirectoryOnly)
                .FirstOrDefault(f => f.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                                  || f.EndsWith(".rar", StringComparison.OrdinalIgnoreCase));
            if (bundle is not null) return bundle;
            // 2. Carpeta con packs ya extraídos (contiene .inf).
            if (Directory.EnumerateFiles(d, "*.inf", SearchOption.AllDirectories)
                    .Any(f => !IsExcluded(f)))
                return d;
        }
        return null;
    }

    public static bool IsExcluded(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}MacOS{Path.DirectorySeparatorChar}",
            StringComparison.OrdinalIgnoreCase);

    public async Task<InstallResult> InstallAsync(
        string source,
        string storeDir,
        IProgress<(int Percent, string Message)> progress = null,
        CancellationToken ct = default,
        bool skipStatic = true,
        Func<List<InstallCandidate>, Task<List<InstallCandidate>>> selectVariantsAsync = null,
        Func<string, Task<DuplicateAction>> confirmDuplicateAsync = null)
    {
        var installed = new List<string>();
        var errors = new List<string>();
        var skipped = new List<string>();
        string workDir = source;

        try
        {
            if (File.Exists(source))
            {
                string staging = Path.Combine(Path.GetTempPath(),
                    "CursorRotator_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(staging);
                progress?.Report((5, $"Extrayendo {Path.GetFileName(source)}..."));
                await Task.Run(() => ExtractArchive(source, staging), ct);
                workDir = staging;
            }

            var infs = Directory.EnumerateFiles(workDir, "*.inf", SearchOption.AllDirectories)
                .Where(f => !IsExcluded(f)).ToList();

            // Si el .zip trae otro .zip dentro (ej. "ani file.zip"), extraerlo también.
            foreach (var nested in Directory.EnumerateFiles(workDir, "*.zip", SearchOption.AllDirectories)
                         .Where(f => !IsExcluded(f)).ToList())
            {
                try
                {
                    string dest = Path.Combine(Path.GetDirectoryName(nested)!,
                        Path.GetFileNameWithoutExtension(nested));
                    ZipFile.ExtractToDirectory(nested, dest, overwriteFiles: true);
                }
                catch (Exception ex) { errors.Add($"{nested} :: no se pudo extraer anidado: {ex.Message}"); }
            }
            infs = Directory.EnumerateFiles(workDir, "*.inf", SearchOption.AllDirectories)
                .Where(f => !IsExcluded(f)).ToList();

            if (infs.Count == 0)
                throw new InvalidOperationException("No se encontraron instaladores (.inf) en el paquete.");

            // Fase 1: parsear todo (sin copiar) para poder elegir variantes
            // y detectar duplicados antes de tocar nada.
            var parsed = new List<(string Inf, ParsedScheme Scheme)>();
            int scanned = 0;
            foreach (var inf in infs)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var scheme = await Task.Run(() => InfParser.ParseFile(inf, workDir), ct);
                    if (skipStatic && scheme.SchemeName.StartsWith("[Static]",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        progress?.Report((5 + 40 * ++scanned / infs.Count,
                            $"Omitido (Static): {scheme.SchemeName}"));
                        continue;
                    }
                    parsed.Add((inf, scheme));
                }
                catch (Exception ex)
                {
                    errors.Add($"{inf} :: {ex.Message}");
                }
                progress?.Report((5 + 40 * ++scanned / infs.Count,
                    $"Analizado: {Path.GetFileName(inf)}"));
            }

            var candidates = parsed
                .Select(p => new InstallCandidate(p.Inf, GroupOf(workDir, p.Inf),
                    p.Scheme, SchemeStore.Exists(p.Scheme.SchemeName)))
                .ToList();

            // Fase 2: si hay varias variantes (o duplicados), el usuario elige.
            List<InstallCandidate> toInstall = candidates;
            if (selectVariantsAsync is not null && candidates.Count > 0
                && (candidates.GroupBy(c => c.Group).Any(g => g.Count() > 1)
                    || (candidates.Any(c => c.AlreadyInstalled) && candidates.Count > 1)))
            {
                toInstall = await selectVariantsAsync(candidates) ?? new();
                foreach (var left in candidates.Except(toInstall))
                    skipped.Add(left.Scheme.SchemeName + " (no seleccionado)");
                if (toInstall.Count == 0)
                {
                    progress?.Report((100, "Instalación cancelada por el usuario."));
                    return new InstallResult(installed, errors, skipped);
                }
            }

            // Fase 3: copiar + registrar, validando duplicados.
            Directory.CreateDirectory(storeDir);
            bool skipAllDup = false;
            var installedThisRun = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int done = 0;
            foreach (var cand in toInstall)
            {
                ct.ThrowIfCancellationRequested();
                string name = cand.Scheme.SchemeName;
                try
                {
                    if (SchemeStore.Exists(name) || installedThisRun.Contains(name))
                    {
                        var action = skipAllDup ? DuplicateAction.Skip
                            : (confirmDuplicateAsync is not null
                                ? await confirmDuplicateAsync(name)
                                : DuplicateAction.Overwrite);
                        if (action == DuplicateAction.SkipAll) skipAllDup = true;
                        if (action != DuplicateAction.Overwrite)
                        {
                            skipped.Add(name + " (ya instalado)");
                            progress?.Report((45 + 55 * ++done / toInstall.Count,
                                $"Omitido (ya instalado): {name}"));
                            continue;
                        }
                    }
                    string destDir = Path.Combine(storeDir, cand.Scheme.SafeDirName);
                    Directory.CreateDirectory(destDir);
                    var destPaths = new List<string>();
                    foreach (var src in cand.Scheme.SourceFiles)
                    {
                        string dest = Path.Combine(destDir, Path.GetFileName(src));
                        File.Copy(src, dest, overwrite: true);
                        destPaths.Add(dest);
                    }
                    SchemeStore.SetScheme(name, destPaths);
                    installedThisRun.Add(name);
                    installed.Add(name);
                    progress?.Report((45 + 55 * ++done / toInstall.Count, $"OK: {name}"));
                }
                catch (Exception ex)
                {
                    errors.Add($"{cand.InfPath} :: {ex.Message}");
                    progress?.Report((45 + 55 * ++done / toInstall.Count, $"Error: {name}"));
                }
            }
        }
        catch (Exception ex)
        {
            errors.Add("General :: " + ex.Message);
        }
        return new InstallResult(installed, errors, skipped);
    }

    /// <summary>Grupo del pack: primer segmento bajo el workDir (o raíz).</summary>
    private static string GroupOf(string workDir, string inf)
    {
        try
        {
            string rel = Path.GetRelativePath(workDir,
                Path.GetDirectoryName(inf) ?? workDir);
            string first = rel.Split(Path.DirectorySeparatorChar,
                StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            return string.IsNullOrEmpty(first) || first == "." ? "(raíz)" : first;
        }
        catch { return "(raíz)"; }
    }

    private static void ExtractArchive(string archive, string destDir)
    {
        if (archive.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            ZipFile.ExtractToDirectory(archive, destDir, overwriteFiles: true);
            return;
        }
        if (archive.EndsWith(".rar", StringComparison.OrdinalIgnoreCase))
        {
            string rar = WinRarCandidates.FirstOrDefault(File.Exists)
                ?? FindWinRarInRegistry()
                ?? throw new InvalidOperationException(
                    "El paquete es .rar y no se encontró WinRAR. Usa la versión .zip del paquete.");
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = rar,
                Arguments = $"x -o+ -y \"{archive}\" \"{destDir.TrimEnd(Path.DirectorySeparatorChar)}{Path.DirectorySeparatorChar}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = System.Diagnostics.Process.Start(psi)
                ?? throw new InvalidOperationException("No se pudo iniciar WinRAR.");
            p.WaitForExit();
            if (p.ExitCode != 0)
                throw new InvalidOperationException($"WinRAR terminó con código {p.ExitCode}.");
            return;
        }
        throw new InvalidOperationException("Formato no soportado: " + Path.GetExtension(archive));
    }

    private static string FindWinRarInRegistry()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\WinRAR.exe");
            var v = key?.GetValue(null) as string;
            return File.Exists(v) ? v : null;
        }
        catch { return null; }
    }
}
