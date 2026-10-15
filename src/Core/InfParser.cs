using System.Text.RegularExpressions;

namespace CursorRotator.Core;

/// <summary>Esquema extraído de un .inf: nombre + archivos fuente resueltos.</summary>
public sealed record ParsedScheme(string SchemeName, string SafeDirName, List<string> SourceFiles);

/// <summary>
/// Parser de install.inf de paquetes de cursores. Soporta dos formatos:
///  - Clásico (pixloen): rutas literales %10%\Cursors\... en [Scheme.Reg].
///  - Moderno (BLZ/VSThemes): variables %SCHEME_NAME%, %CUR_DIR%, %pointer%...
///    definidas en la sección [Strings].
/// </summary>
public static partial class InfParser
{
    [GeneratedRegex(@"""Control Panel\\Cursors\\Schemes"",\s*([^,]+),([^,]*),(.*)",
        RegexOptions.IgnoreCase)]
    private static partial Regex SchemesLineRegex();

    [GeneratedRegex("%([^%]+)%")]
    private static partial Regex VariableRegex();

    public static ParsedScheme ParseFile(string infPath, string packRoot)
    {
        string text = File.ReadAllText(infPath);

        // 1. Sección [Strings] (claves case-insensitive).
        var strings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        bool inStrings = false;
        foreach (var rawLine in text.Split(['\r', '\n']))
        {
            var line = rawLine.Trim();
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                inStrings = line.Equals("[Strings]", StringComparison.OrdinalIgnoreCase);
                continue;
            }
            if (!inStrings || line.Length == 0 || line.StartsWith(';')) continue;
            int eq = line.IndexOf('=');
            if (eq <= 0) continue;
            strings[line[..eq].Trim()] = line[(eq + 1)..].Trim().Trim('"', '\'');
        }

        // 2. Línea HKCU,...Schemes,"nombre",,rutas...
        var m = SchemesLineRegex().Match(text);
        if (!m.Success)
            throw new InvalidOperationException("sin línea de esquema (Schemes)");

        string Expand(string s) => VariableRegex().Replace(s, vm =>
        {
            var k = vm.Groups[1].Value;
            if (k == "10") return @"C:\Windows";
            return strings.TryGetValue(k, out var v) ? v : vm.Value;
        });

        string schemeName = Expand(m.Groups[1].Value.Trim().Trim('"')).Trim().Trim('"');
        if (schemeName.Length == 0)
            throw new InvalidOperationException("nombre de esquema vacío");

        string[] entries = Expand(m.Groups[3].Value.Trim().Trim('"')).Split(',');

        // 3b. Mapa [Scheme.Cur]: destino -> fuente relativa al .inf
        // (los packs pixloen guardan los .ani en .\cursors\, no junto al .inf).
        var curMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        bool inCur = false;
        foreach (var rawLine in text.Split(['\r', '\n']))
        {
            var line = rawLine.Trim();
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                inCur = line.StartsWith("[Scheme.Cur", StringComparison.OrdinalIgnoreCase);
                continue;
            }
            if (!inCur || line.Length == 0 || line.StartsWith(';')) continue;
            int comma = line.IndexOf(',');
            if (comma <= 0) continue; // solo nombre: fuente = dir del .inf
            string dk = line[..comma].Trim().Trim('"');
            string sv = line[(comma + 1)..].Trim().Trim('"');
            if (dk.Length > 0 && sv.Length > 0) curMap[dk] = sv;
        }

        // 3. Resolver cada archivo: mapa [Scheme.Cur], junto al .inf, o
        // búsqueda recursiva ACOTADA al subárbol del .inf (nunca a todo el
        // staging: los packs comparten nombres y se contaminarían entre sí).
        string infDir = Path.GetDirectoryName(infPath)!;
        // Seguridad: ningún archivo resuelto puede vivir fuera de la raíz del
        // paquete (bloquea "..\..\" o rutas absolutas inyectadas en el .inf).
        string packRootFull = Path.GetFullPath(
            string.IsNullOrWhiteSpace(packRoot) ? infDir : packRoot);
        var sources = new List<string>();
        foreach (var entry in entries)
        {
            string leaf = Path.GetFileName(entry.Trim().Trim('"'));
            if (leaf.Length == 0 || leaf.Contains('%'))
                throw new InvalidOperationException($"variable sin resolver: {entry.Trim()}");

            string candidate = null;
            if (curMap.TryGetValue(leaf, out var rel))
            {
                string mapped = Path.Combine(infDir,
                    rel.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(mapped))
                {
                    if (!IsUnder(mapped, packRootFull))
                        throw new InvalidOperationException($"ruta fuera del paquete: {rel}");
                    candidate = mapped;
                }
            }
            candidate ??= Path.Combine(infDir, leaf);
            if (!File.Exists(candidate))
            {
                // Búsqueda por nombre EXACTO en el subárbol del .inf. No se usa
                // patrón comodín: en .NET los corchetes son literales y un leaf
                // con '*'/'?' "capturaría" archivos ajenos. Se salta MacOS (los
                // packs de macOS duplican los mismos nombres).
                string mac = $"{Path.DirectorySeparatorChar}MacOS{Path.DirectorySeparatorChar}";
                candidate = Directory.EnumerateFiles(infDir, "*", SearchOption.AllDirectories)
                    .FirstOrDefault(f => !f.Contains(mac)
                        && string.Equals(Path.GetFileName(f), leaf,
                            StringComparison.OrdinalIgnoreCase));
            }
            if (candidate is null || !File.Exists(candidate))
                throw new InvalidOperationException($"falta archivo: {leaf}");
            // Normaliza ".." para que las comprobaciones por prefijo (contención
            // aquí y anti-contaminación en --self-test) sean fiables.
            candidate = Path.GetFullPath(candidate);
            if (!IsUnder(candidate, packRootFull))
                throw new InvalidOperationException($"ruta fuera del paquete: {leaf}");
            if (!IsCursorExtension(candidate))
                throw new InvalidOperationException(
                    $"no es un archivo de cursor (.cur/.ani): {Path.GetFileName(candidate)}");
            sources.Add(candidate);
        }

        string safe = string.Concat(schemeName.Select(ch =>
            Path.GetInvalidFileNameChars().Contains(ch) ? '-' : ch)).Trim();
        if (safe.Length == 0)
            safe = Path.GetFileName(packRoot) + "_" + Path.GetFileName(infDir);

        return new ParsedScheme(schemeName, safe, sources);
    }

    /// <summary>¿La extensión es de cursor (.cur/.ani)? Puerta de seguridad.</summary>
    public static bool IsCursorExtension(string path)
    {
        string ext = Path.GetExtension(path ?? "");
        return ext.Equals(".cur", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".ani", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// ¿`candidate` cae dentro del árbol de `root`? Rutas normalizadas y
    /// comparación case-insensitive (seguridad: evita escapes con "..\").
    /// </summary>
    public static bool IsUnder(string candidate, string root)
    {
        if (string.IsNullOrEmpty(candidate) || string.IsNullOrEmpty(root)) return false;
        string full, r;
        try { full = Path.GetFullPath(candidate); } catch { return false; }
        try { r = Path.GetFullPath(root); } catch { return false; }
        r = r.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return full.StartsWith(r, StringComparison.OrdinalIgnoreCase);
    }
}
