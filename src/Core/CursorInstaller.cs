using System.IO.Compression;
using Microsoft.Win32;

namespace CursorRotator.Core;

/// <summary>
/// Instalador en 1 clic: acepta un .zip/.rar elegido por el usuario o una
/// carpeta ya extraída, examina TODO el contenido (esquemas con .inf y
/// cursores sueltos .cur/.ani sin .inf), muestra lo encontrado para que el
/// usuario instale todos o solo algunos, y los copia a
/// %LOCALAPPDATA%\Cursors registrando los esquemas en HKCU.
/// Sin UAC: todo es por-usuario.
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

    private static readonly string[] SevenZipCandidates =
    [
        @"C:\Program Files\7-Zip\7z.exe",
        @"C:\Program Files (x86)\7-Zip\7z.exe",
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
    /// Candidato de instalación: .inf origen (o carpeta origen si es un grupo
    /// de archivos sueltos sin .inf), grupo del pack, esquema parseado, si
    /// ya estaba instalado al escanear y si viene de archivos sueltos.
    /// </summary>
    public sealed record InstallCandidate(
        string InfPath, string Group, ParsedScheme Scheme, bool AlreadyInstalled, bool IsLoose = false);

    /// <summary>
    /// Examen sin instalar: extrae (si es un comprimido soportado) a uno o
    /// varios temporales, lista los .inf + los .cur/.ani sueltos y devuelve
    /// los candidatos. El llamador debe liberar con
    /// <see cref="CleanupStaging(IEnumerable{string})"/>.
    /// </summary>
    public sealed record ScanPreview(
        List<InstallCandidate> Candidates, List<string> Errors, List<string> StagingDirs);

    /// <summary>¿Es un archivo de cursor instalable?</summary>
    public static bool IsCursorFile(string path) =>
        path.EndsWith(".cur", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".ani", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Extensiones comprimidas soportadas. .zip y .rar tienen extractor
    /// nativo (o WinRAR); el resto (7z/tar/gz/...) requiere 7-Zip.
    /// </summary>
    private static readonly string[] ArchiveExtensions =
        [".zip", ".rar", ".7z", ".tar", ".gz", ".tgz", ".bz2", ".xz"];

    /// <summary>Filtro de diálogo "Archivos" con todos los formatos aceptados.</summary>
    public static string ArchiveFilePattern =>
        string.Join(";", ArchiveExtensions.Select(e => "*" + e));

    /// <summary>¿Es un archivo comprimido soportado?</summary>
    public static bool IsArchiveFile(string path) =>
        ArchiveExtensions.Contains(Path.GetExtension(path ?? ""), StringComparer.OrdinalIgnoreCase);

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
                .FirstOrDefault(f => IsArchiveFile(f));
            if (bundle is not null) return bundle;
            // 2. Carpeta con packs ya extraídos (contiene .inf o sueltos).
            try
            {
                if (Directory.EnumerateFiles(d, "*.inf", SearchOption.AllDirectories)
                        .Any(f => !IsExcluded(f)))
                    return d;
                if (Directory.EnumerateFiles(d, "*.cur", SearchOption.AllDirectories)
                        .Concat(Directory.EnumerateFiles(d, "*.ani", SearchOption.AllDirectories))
                        .Any(f => !IsExcluded(f)))
                    return d;
            }
            catch { }
        }
        return null;
    }

    public static bool IsExcluded(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}MacOS{Path.DirectorySeparatorChar}",
            StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Examina UN origen (.zip/.rar/... o carpeta) y devuelve TODOS los
    /// cursores disponibles sin copiar nada. Soporta .inf clásicos/modernos
    /// y carpetas con .cur/.ani sueltos (cada carpeta = un esquema).
    /// </summary>
    public Task<ScanPreview> ScanAsync(
        string source,
        IProgress<(int Percent, string Message)> progress = null,
        CancellationToken ct = default,
        bool skipStatic = true)
        => ScanManyAsync([source], progress, ct, skipStatic);

    /// <summary>
    /// Examina VARIOS orígenes (varios .zip/.rar/... o carpetas) y fusiona
    /// los candidatos desduplicando por nombre de esquema (gana el primero).
    /// Devuelve todos los temporales a liberar con CleanupStaging.
    /// </summary>
    public async Task<ScanPreview> ScanManyAsync(
        IReadOnlyList<string> sources,
        IProgress<(int Percent, string Message)> progress = null,
        CancellationToken ct = default,
        bool skipStatic = true)
    {
        var errors = new List<string>();
        var candidates = new List<InstallCandidate>();
        var stagings = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int total = sources?.Count ?? 0;
        for (int i = 0; i < total; i++)
        {
            string prefix = total > 1 ? $"({i + 1}/{total}) " : "";
            var part = await ScanOneAsync(sources[i], progress, prefix, ct, skipStatic);
            errors.AddRange(part.Errors);
            stagings.AddRange(part.StagingDirs);
            foreach (var c in part.Candidates)
            {
                if (!seen.Add(c.Scheme.SchemeName))
                {
                    errors.Add($"{c.Scheme.SchemeName} :: repetido entre paquetes, se conserva el primero.");
                    continue;
                }
                candidates.Add(c);
            }
        }
        candidates.Sort((a, b) => string.Compare(
            a.Scheme.SchemeName, b.Scheme.SchemeName, StringComparison.OrdinalIgnoreCase));
        return new ScanPreview(candidates, errors, stagings);
    }

    private async Task<ScanPreview> ScanOneAsync(
        string source,
        IProgress<(int Percent, string Message)> progress,
        string prefix,
        CancellationToken ct,
        bool skipStatic)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(source))
            throw new InvalidOperationException("Elige un archivo comprimido o una carpeta con cursores.");
        if (!File.Exists(source) && !Directory.Exists(source))
            throw new FileNotFoundException($"No se encontró el origen: {source}", source);

        string workDir = source;
        string staging = null;
        string displayBase = Directory.Exists(source)
            ? Path.GetFileName(Path.GetFullPath(source).TrimEnd(Path.DirectorySeparatorChar))
            : Path.GetFileNameWithoutExtension(source);

        if (File.Exists(source))
        {
            if (!IsArchiveFile(source))
                throw new InvalidOperationException(
                    "Formato no soportado: " + Path.GetExtension(source) +
                    " (usa .zip, .rar, .7z... o una carpeta).");
            staging = Path.Combine(Path.GetTempPath(),
                "CursorRotator_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            progress?.Report((5, $"{prefix}Extrayendo {Path.GetFileName(source)}..."));
            await Task.Run(() => ExtractArchive(source, staging), ct);
            workDir = staging;
        }

        // Anidados: "ani file.zip", packs dentro de packs.
        ExtractNestedArchives(workDir, errors);

        var infs = Directory.EnumerateFiles(workDir, "*.inf", SearchOption.AllDirectories)
            .Where(f => !IsExcluded(f)).ToList();

        // Fase 1: parsear todo (sin copiar).
        var parsed = new List<(string Inf, ParsedScheme Scheme)>();
        int scanned = 0;
        int totalHints = Math.Max(1, infs.Count);
        foreach (var inf in infs)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var scheme = await Task.Run(() => InfParser.ParseFile(inf, workDir), ct);
                if (skipStatic && scheme.SchemeName.StartsWith("[Static]",
                        StringComparison.OrdinalIgnoreCase))
                {
                    progress?.Report((5 + 40 * ++scanned / totalHints,
                        $"{prefix}Omitido (Static): {scheme.SchemeName}"));
                    continue;
                }
                parsed.Add((inf, scheme));
            }
            catch (Exception ex)
            {
                errors.Add($"{inf} :: {ex.Message}");
            }
            progress?.Report((5 + 40 * ++scanned / totalHints,
                $"{prefix}Analizado: {Path.GetFileName(inf)}"));
        }

        var covered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in parsed)
            foreach (var s in p.Scheme.SourceFiles)
            {
                try { covered.Add(Path.GetFullPath(s)); } catch { covered.Add(s); }
            }

        var candidates = parsed
            .Select(p => new InstallCandidate(p.Inf, GroupOf(workDir, p.Inf),
                p.Scheme, SchemeStore.Exists(p.Scheme.SchemeName), IsLoose: false))
            .ToList();

        // Fase 1b: grupos sueltos (.cur/.ani sin .inf): cada carpeta = 1 esquema.
        foreach (var loose in SynthesizeLooseSchemes(workDir, covered, displayBase))
        {
            if (skipStatic && loose.SchemeName.StartsWith("[Static]",
                    StringComparison.OrdinalIgnoreCase))
                continue;
            // Evita duplicar un esquema que ya aporta un .inf con el mismo nombre.
            if (candidates.Any(c => string.Equals(c.Scheme.SchemeName, loose.SchemeName,
                    StringComparison.OrdinalIgnoreCase)))
            {
                errors.Add($"{loose.SafeDirName} :: nombre duplicado de .inf, se usa el .inf.");
                continue;
            }
            string originDir = Path.GetDirectoryName(loose.SourceFiles[0]) ?? workDir;
            candidates.Add(new InstallCandidate(originDir, GroupOf(workDir, originDir),
                loose, SchemeStore.Exists(loose.SchemeName), IsLoose: true));
        }

        candidates.Sort((a, b) => string.Compare(
            a.Scheme.SchemeName, b.Scheme.SchemeName, StringComparison.OrdinalIgnoreCase));
        var stagings = staging is null
            ? new List<string>() : new List<string> { staging };
        return new ScanPreview(candidates, errors, stagings);
    }

    /// <summary>Libera un temporal creado por el examen (best-effort).</summary>
    public static void CleanupStaging(string stagingDir)
    {
        if (string.IsNullOrEmpty(stagingDir)) return;
        try { if (Directory.Exists(stagingDir)) Directory.Delete(stagingDir, recursive: true); }
        catch { }
    }

    /// <summary>Libera todos los temporales de un examen múltiple (best-effort).</summary>
    public static void CleanupStaging(IEnumerable<string> stagingDirs)
    {
        if (stagingDirs is null) return;
        foreach (var d in stagingDirs) CleanupStaging(d);
    }

    /// <summary>
    /// Instala desde UN origen (.zip/.rar/... o carpeta). Atajo de la
    /// sobrecarga múltiple.
    /// </summary>
    public Task<InstallResult> InstallAsync(
        string source,
        string storeDir,
        IProgress<(int Percent, string Message)> progress = null,
        CancellationToken ct = default,
        bool skipStatic = true,
        Func<List<InstallCandidate>, Task<List<InstallCandidate>>> selectVariantsAsync = null,
        Func<string, Task<DuplicateAction>> confirmDuplicateAsync = null)
        => InstallAsync([source], storeDir, progress, ct, skipStatic,
            selectVariantsAsync, confirmDuplicateAsync);

    /// <summary>
    /// Instala desde VARIOS orígenes (varios paquetes .zip/.rar/... y/o
    /// carpetas): examina todos, fusiona los cursores y deja al usuario
    /// elegir (todos o solo algunos), validando duplicados.
    /// </summary>
    public async Task<InstallResult> InstallAsync(
        IReadOnlyList<string> sources,
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
        ScanPreview scan = null;

        try
        {
            scan = await ScanManyAsync(sources, progress, ct, skipStatic);
            errors.AddRange(scan.Errors);
            var candidates = scan.Candidates;

            if (candidates.Count == 0)
            {
                if (errors.Count > 0)
                    throw new InvalidOperationException(
                        "No se encontraron cursores instalables. Revisa los errores del examen.");
                throw new InvalidOperationException(
                    "No se encontraron cursores (.inf ni .cur/.ani sueltos) en el paquete.");
            }

            progress?.Report((45, $"Examen: {candidates.Count} cursor(es) disponible(s) " +
                $"({candidates.Count(c => !c.IsLoose)} con instalador, {candidates.Count(c => c.IsLoose)} sueltos)."));

            // Fase 2: el usuario elige cuáles instalar (todos por defecto).
            // Con 2+ candidatos SIEMPRE se pregunta: todos o solo algunos.
            List<InstallCandidate> toInstall = candidates;
            if (selectVariantsAsync is not null && candidates.Count > 1)
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
                            progress?.Report((45 + 55 * ++done / Math.Max(1, toInstall.Count),
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
                    progress?.Report((45 + 55 * ++done / Math.Max(1, toInstall.Count), $"OK: {name}"));
                }
                catch (Exception ex)
                {
                    errors.Add($"{cand.InfPath} :: {ex.Message}");
                    progress?.Report((45 + 55 * ++done / Math.Max(1, toInstall.Count), $"Error: {name}"));
                }
            }
        }
        catch (Exception ex)
        {
            errors.Add("General :: " + ex.Message);
        }
        finally
        {
            if (scan?.StagingDirs is not null)
                CleanupStaging(scan.StagingDirs);
        }
        return new InstallResult(installed, errors, skipped);
    }

    /// <summary>Grupo del pack: primer segmento bajo el workDir (o raíz).</summary>
    private static string GroupOf(string workDir, string infOrDir)
    {
        try
        {
            string dir = Directory.Exists(infOrDir) ? infOrDir
                : Path.GetDirectoryName(infOrDir) ?? workDir;
            string rel = Path.GetRelativePath(workDir, dir);
            string first = rel.Split(Path.DirectorySeparatorChar,
                StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            return string.IsNullOrEmpty(first) || first == "." ? "(raíz)" : first;
        }
        catch { return "(raíz)"; }
    }

    /// <summary>
    /// Extrae anidados (.zip y .rar dentro del pack) hasta 3 pasadas:
    /// cubre "ani file.zip" y packs dentro de packs.
    /// </summary>
    private static void ExtractNestedArchives(string workDir, List<string> errors)
    {
        for (int pass = 0; pass < 3; pass++)
        {
            var nested = Directory.EnumerateFiles(workDir, "*.*", SearchOption.AllDirectories)
                .Where(f => !IsExcluded(f) && IsArchiveFile(f)).ToList();
            if (nested.Count == 0) return;
            bool anyOk = false;
            foreach (var n in nested)
            {
                // No re-extraer el mismo archivo dos veces: marca por carpeta.
                string dest = Path.Combine(Path.GetDirectoryName(n)!,
                    Path.GetFileNameWithoutExtension(n));
                string marker = dest + ".extracted";
                if (Directory.Exists(dest) || File.Exists(marker)) continue;
                try
                {
                    Directory.CreateDirectory(dest);
                    ExtractArchive(n, dest);
                    try { File.WriteAllText(marker, DateTime.UtcNow.ToString("o")); } catch { }
                    anyOk = true;
                }
                catch (Exception ex)
                {
                    errors.Add($"{n} :: no se pudo extraer anidado: {ex.Message}");
                    try { File.WriteAllText(marker, "error:" + ex.Message); } catch { }
                }
            }
            if (!anyOk) return;
        }
    }

    internal static void ExtractArchive(string archive, string destDir)
    {
        if (archive.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                ZipFile.ExtractToDirectory(archive, destDir, overwriteFiles: true);
                return;
            }
            catch
            {
                // Un .zip corrupto para ZipFile a veces lo abre 7z: reintenta.
                string seven = SevenZipCandidates.FirstOrDefault(File.Exists);
                if (seven is null) throw;
                RunExtractor(seven, $"x -y -o\"{destDir}\" \"{archive}\"", "7-Zip");
                return;
            }
        }
        if (archive.EndsWith(".rar", StringComparison.OrdinalIgnoreCase))
        {
            string rar = WinRarCandidates.FirstOrDefault(File.Exists)
                ?? FindWinRarInRegistry();
            if (rar is not null)
            {
                // UnRAR.exe usa sintaxis "x -o+ archivo destino"; WinRAR/Rar la aceptan.
                string args = rar.EndsWith("UnRAR.exe", StringComparison.OrdinalIgnoreCase)
                    ? $"x -o+ \"{archive}\" \"{destDir.TrimEnd(Path.DirectorySeparatorChar)}{Path.DirectorySeparatorChar}\""
                    : $"x -o+ -y \"{archive}\" \"{destDir.TrimEnd(Path.DirectorySeparatorChar)}{Path.DirectorySeparatorChar}\"";
                RunExtractor(rar, args, "WinRAR");
                return;
            }
            string z = SevenZipCandidates.FirstOrDefault(File.Exists);
            if (z is not null)
            {
                RunExtractor(z, $"x -y -o\"{destDir}\" \"{archive}\"", "7-Zip");
                return;
            }
            throw new InvalidOperationException(
                "El paquete es .rar y no se encontró WinRAR ni 7-Zip. Instala WinRAR/7-Zip o usa la versión .zip del paquete.");
        }
        // Otros formatos (.7z/.tar/.gz/...) requieren 7-Zip.
        string sevenZip = SevenZipCandidates.FirstOrDefault(File.Exists);
        if (sevenZip is not null)
        {
            RunExtractor(sevenZip, $"x -y -o\"{destDir}\" \"{archive}\"", "7-Zip");
            return;
        }
        throw new InvalidOperationException(
            $"El formato {Path.GetExtension(archive)} necesita 7-Zip instalado " +
            "(o usa un paquete .zip/.rar).");
    }

    private static void RunExtractor(string exe, string args, string label)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var p = System.Diagnostics.Process.Start(psi)
            ?? throw new InvalidOperationException($"No se pudo iniciar {label}.");
        p.WaitForExit();
        if (p.ExitCode != 0)
            throw new InvalidOperationException($"{label} terminó con código {p.ExitCode}.");
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

    // ---------- Esquemas sueltos (.cur/.ani sin .inf) ----------

    /// <summary>15 roles estándar + palabras clave típicas en nombres de archivo.</summary>
    private static readonly (string Role, string[] Keys)[] RoleHints =
    [
        ("Arrow", ["arrow", "normal", "pointer", "default", "puntero", "flecha"]),
        ("Help", ["help", "ayuda", "question"]),
        ("AppStarting", ["appstarting", "appstart", "working"]),
        ("Wait", ["wait", "busy", "espera"]),
        ("Crosshair", ["cross", "precision", "crosshair"]),
        ("IBeam", ["ibeam", "beam", "text", "texto"]),
        ("NWPen", ["nwpen", "pen", "pluma"]),
        ("No", ["unavailable", "notavailable", "forbidden", "prohibido"]),
        ("SizeNS", ["sizens", "size_ns", "sizens", "vertical", "ns"]),
        ("SizeWE", ["sizewe", "size_we", "sizewe", "horizontal", "we"]),
        ("SizeNWSE", ["sizenwse", "size_nwse", "diagonal1", "nwse"]),
        ("SizeNESW", ["sizenesw", "size_nesw", "diagonal2", "nesw"]),
        ("SizeAll", ["sizeall", "size_all", "move", "mover"]),
        ("UpArrow", ["uparrow", "up_arrow", "up", "arriba"]),
        ("Hand", ["hand", "link", "mano", "enlace"]),
    ];

    private static int RoleIndex(string fileName)
    {
        string stem = Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant();
        for (int i = 0; i < RoleHints.Length; i++)
            foreach (var k in RoleHints[i].Keys)
                if (stem.Contains(k, StringComparison.Ordinal))
                    return i;
        return int.MaxValue;
    }

    /// <summary>
    /// Agrupa los .cur/.ani no cubiertos por ningún .inf por carpeta:
    /// cada carpeta con 1+ cursores = 1 esquema instalable.
    /// </summary>
    internal static List<ParsedScheme> SynthesizeLooseSchemes(
        string workDir, HashSet<string> covered, string displayBase)
    {
        var result = new List<ParsedScheme>();
        List<string> all;
        try
        {
            all = Directory.EnumerateFiles(workDir, "*.*", SearchOption.AllDirectories)
                .Where(f => !IsExcluded(f) && IsCursorFile(f)).ToList();
        }
        catch { return result; }

        var remaining = all.Where(f =>
        {
            try { return !covered.Contains(Path.GetFullPath(f)); }
            catch { return !covered.Contains(f); }
        }).ToList();
        if (remaining.Count == 0) return result;

        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var g in remaining.GroupBy(f => Path.GetDirectoryName(f)!).OrderBy(g => g.Key))
        {
            var files = g.OrderBy(RoleIndex)
                .ThenBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase).ToList();
            if (files.Count == 0) continue;

            string folder = Path.GetFileName(g.Key.TrimEnd(Path.DirectorySeparatorChar));
            string baseName;
            if (string.IsNullOrEmpty(folder) || g.Key.TrimEnd(Path.DirectorySeparatorChar)
                    .Equals(workDir.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            {
                baseName = string.IsNullOrWhiteSpace(displayBase) ? "Cursores sueltos" : displayBase.Trim();
            }
            else
            {
                baseName = folder;
            }
            if (baseName.Length == 0) baseName = "Cursores sueltos";
            string name = baseName;
            int n = 2;
            while (usedNames.Contains(name))
                name = $"{baseName} ({n++})";
            usedNames.Add(name);

            string safe = string.Concat(name.Select(ch =>
                Path.GetInvalidFileNameChars().Contains(ch) ? '-' : ch)).Trim();
            if (safe.Length == 0) safe = "suelto_" + Guid.NewGuid().ToString("N")[..8];
            // Desempata la carpeta destino si ya existe el nombre.
            string safeUnique = safe;
            int m = 2;
            while (result.Any(r => string.Equals(r.SafeDirName, safeUnique, StringComparison.OrdinalIgnoreCase)))
                safeUnique = $"{safe} ({m++})";

            result.Add(new ParsedScheme(name, safeUnique, files));
        }
        return result;
    }
}
