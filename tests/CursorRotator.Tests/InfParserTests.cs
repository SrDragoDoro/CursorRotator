using CursorRotator.Core;
using Xunit;

namespace CursorRotator.Tests;

/// <summary>
/// Parser de install.inf: formatos clásico (pixloen) y moderno (BLZ/VSThemes),
/// expansión de variables y resolución de archivos.
/// </summary>
public class InfParserTests
{
    private static string Lines(params string[] lines) => string.Join("\r\n", lines);

    [Fact]
    public void Clasico_Pixloen_ResuelveFuentesViaSchemeCur()
    {
        using var t = new TempDir();
        t.Write("cursors/arrow.ani", "a");
        t.Write("cursors/help.ani", "h");
        string inf = t.Write("install.inf", Lines(
            "[Version]",
            "Signature=\"$Windows NT$\"",
            "",
            "[Scheme.Reg]",
            "HKCU,\"Control Panel\\Cursors\\Schemes\",\"Mi Esquema\",,\"%10%\\Cursors\\arrow.ani,%10%\\Cursors\\help.ani\"",
            "",
            "[Scheme.Cur]",
            "arrow.ani,.\\cursors\\arrow.ani",
            "help.ani,.\\cursors\\help.ani"));

        var parsed = InfParser.ParseFile(inf, t.Root);

        Assert.Equal("Mi Esquema", parsed.SchemeName);
        Assert.Equal(2, parsed.SourceFiles.Count);
        Assert.Equal("arrow.ani", Path.GetFileName(parsed.SourceFiles[0]));
        Assert.Equal("help.ani", Path.GetFileName(parsed.SourceFiles[1]));
        Assert.All(parsed.SourceFiles, f => Assert.True(File.Exists(f)));
        // Todo fuente vive bajo el paquete (anti-contaminación).
        Assert.All(parsed.SourceFiles, f => Assert.True(InfParser.IsUnder(f, t.Root)));
    }

    [Fact]
    public void Moderno_Strings_ExpandeVariablesDelNombreYLosArchivos()
    {
        using var t = new TempDir();
        t.Write("arrow.ani", "a");
        string inf = t.Write("install.inf", Lines(
            "[Strings]",
            "SCHEME_NAME=Punk Neon",
            "pointer=arrow.ani",
            "",
            "[Scheme.Reg]",
            "HKCU,\"Control Panel\\Cursors\\Schemes\",\"%SCHEME_NAME%\",,\"%10%\\Cursors\\%pointer%\""));

        var parsed = InfParser.ParseFile(inf, t.Root);

        Assert.Equal("Punk Neon", parsed.SchemeName);
        Assert.Single(parsed.SourceFiles);
        Assert.Equal("arrow.ani", Path.GetFileName(parsed.SourceFiles[0]));
    }

    [Fact]
    public void ArchivosJuntoAlInf_SinSchemeCur_SeResuelven()
    {
        using var t = new TempDir();
        t.Write("arrow.cur", "a");
        string inf = t.Write("install.inf", Lines(
            "[Scheme.Reg]",
            "HKCU,\"Control Panel\\Cursors\\Schemes\",\"Suelto\",,\"%10%\\Cursors\\arrow.cur\""));

        var parsed = InfParser.ParseFile(inf, t.Root);

        Assert.Equal("Suelto", parsed.SchemeName);
        Assert.Equal("arrow.cur", Path.GetFileName(parsed.SourceFiles[0]));
    }

    [Fact]
    public void BusquedaRecursiva_LocalizaEnSubcarpeta()
    {
        using var t = new TempDir();
        t.Write("deep/inner/arrow.ani", "a");
        string inf = t.Write("install.inf", Lines(
            "[Scheme.Reg]",
            "HKCU,\"Control Panel\\Cursors\\Schemes\",\"Anidado\",,\"%10%\\Cursors\\arrow.ani\""));

        var parsed = InfParser.ParseFile(inf, t.Root);

        Assert.True(InfParser.IsUnder(parsed.SourceFiles[0], t.Root));
        Assert.Equal("arrow.ani", Path.GetFileName(parsed.SourceFiles[0]));
    }

    [Fact]
    public void SinLineaDeEsquema_Lanza()
    {
        using var t = new TempDir();
        string inf = t.Write("install.inf", Lines("[Version]", "Signature=\"$Windows NT$\""));
        Assert.Throws<InvalidOperationException>(() => InfParser.ParseFile(inf, t.Root));
    }

    [Fact]
    public void NombreDeEsquemaVacio_Lanza()
    {
        using var t = new TempDir();
        t.Write("arrow.ani", "a");
        string inf = t.Write("install.inf", Lines(
            "[Scheme.Reg]",
            "HKCU,\"Control Panel\\Cursors\\Schemes\",\"\",,\"%10%\\Cursors\\arrow.ani\""));
        var ex = Assert.Throws<InvalidOperationException>(() => InfParser.ParseFile(inf, t.Root));
        Assert.Contains("nombre de esquema", ex.Message);
    }

    [Fact]
    public void VariableSinDefinir_Lanza()
    {
        using var t = new TempDir();
        t.Write("arrow.ani", "a");
        string inf = t.Write("install.inf", Lines(
            "[Scheme.Reg]",
            "HKCU,\"Control Panel\\Cursors\\Schemes\",\"X\",,\"%10%\\Cursors\\%NOPE%.ani\""));
        var ex = Assert.Throws<InvalidOperationException>(() => InfParser.ParseFile(inf, t.Root));
        Assert.Contains("variable sin resolver", ex.Message);
    }

    [Fact]
    public void ArchivoInexistente_Lanza()
    {
        using var t = new TempDir();
        string inf = t.Write("install.inf", Lines(
            "[Scheme.Reg]",
            "HKCU,\"Control Panel\\Cursors\\Schemes\",\"X\",,\"%10%\\Cursors\\missing.ani\""));
        var ex = Assert.Throws<InvalidOperationException>(() => InfParser.ParseFile(inf, t.Root));
        Assert.Contains("falta archivo", ex.Message);
    }

    [Fact]
    public void NombreSeguro_ReemplazaCaracteresInvalidos()
    {
        using var t = new TempDir();
        t.Write("arrow.ani", "a");
        string inf = t.Write("install.inf", Lines(
            "[Scheme.Reg]",
            "HKCU,\"Control Panel\\Cursors\\Schemes\",\"A/B:C*?\",,\"%10%\\Cursors\\arrow.ani\""));
        var parsed = InfParser.ParseFile(inf, t.Root);
        Assert.DoesNotContain('/', parsed.SafeDirName);
        Assert.DoesNotContain(':', parsed.SafeDirName);
        Assert.DoesNotContain('*', parsed.SafeDirName);
        Assert.DoesNotContain('?', parsed.SafeDirName);
    }

    [Fact]
    public void BusquedaPorNombreExacto_EncuentraArchivoConCorchetes()
    {
        using var t = new TempDir();
        t.Write("sub/a[b].cur", "x");
        string inf = t.Write("install.inf", Lines(
            "[Scheme.Reg]",
            "HKCU,\"Control Panel\\Cursors\\Schemes\",\"Corchetes\",,\"%10%\\Cursors\\a[b].cur\""));

        var parsed = InfParser.ParseFile(inf, t.Root);

        Assert.Equal("a[b].cur", Path.GetFileName(parsed.SourceFiles[0]));
    }

    [Fact]
    public void BusquedaPorNombreExacto_NoCapturaConComodines()
    {
        using var t = new TempDir();
        t.Write("sub/other.ani", "x"); // existe, pero NO es el leaf pedido
        string inf = t.Write("install.inf", Lines(
            "[Scheme.Reg]",
            "HKCU,\"Control Panel\\Cursors\\Schemes\",\"Comodin\",,\"%10%\\Cursors\\*.ani\""));

        var ex = Assert.Throws<InvalidOperationException>(() => InfParser.ParseFile(inf, t.Root));
        Assert.Contains("falta archivo", ex.Message);
    }

    [Fact]
    public void FuentesSeNormalizan_EliminaPuntosDobles()
    {
        using var t = new TempDir();
        t.Write("cursors/arrow.ani", "x");
        string inf = t.Write("pack/install.inf", Lines(
            "[Scheme.Reg]",
            "HKCU,\"Control Panel\\Cursors\\Schemes\",\"Norm\",,\"%10%\\Cursors\\arrow.ani\"",
            "",
            "[Scheme.Cur]",
            "arrow.ani,..\\cursors\\arrow.ani"));

        // packRoot = raíz: el ".." sigue DENTRO del paquete, así que se permite
        // pero la ruta guardada queda normalizada (sin "..").
        var parsed = InfParser.ParseFile(inf, t.Root);

        Assert.DoesNotContain("..", parsed.SourceFiles[0]);
        Assert.Equal(t.Combine("cursors", "arrow.ani"), parsed.SourceFiles[0]);
        Assert.True(Path.IsPathRooted(parsed.SourceFiles[0]));
    }

    [Theory]
    [InlineData(@"C:\a\b\c.cur", @"C:\a\b", true)]
    [InlineData(@"C:\a\bX\c.cur", @"C:\a\b", false)]
    [InlineData(@"C:\a\c.cur", @"C:\a\b", false)]
    [InlineData(@"C:\a\b\c.cur", @"c:\A\B", true)]
    public void IsUnder_DetectaContencion(string candidate, string root, bool expected) =>
        Assert.Equal(expected, InfParser.IsUnder(candidate, root));

    [Theory]
    [InlineData("arrow.cur", true)]
    [InlineData("busy.ANI", true)]
    [InlineData("arrow.ani", true)]
    [InlineData("readme.txt", false)]
    [InlineData("evil.exe", false)]
    [InlineData("noext", false)]
    public void IsCursorExtension_SoloCurYAni(string path, bool expected) =>
        Assert.Equal(expected, InfParser.IsCursorExtension(path));
}
