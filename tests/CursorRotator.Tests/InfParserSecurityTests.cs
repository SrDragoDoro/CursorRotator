using CursorRotator.Core;
using Xunit;

namespace CursorRotator.Tests;

/// <summary>
/// Endurecimiento del import: un .inf de origen desconocido no puede hacer que
/// la app copie/registre archivos fuera del paquete ni de tipo no-cursor.
/// </summary>
public class InfParserSecurityTests
{
    private static string Lines(params string[] lines) => string.Join("\r\n", lines);

    [Fact]
    public void RutaRelativaEscapandoElPaquete_Lanza()
    {
        using var t = new TempDir();
        // Archivo legítimo pero FUERA del pack; el .inf intenta alcanzarlo con ..\.
        t.Write("evil/arrow.ani", "x");
        string inf = t.Write("pack/install.inf", Lines(
            "[Scheme.Reg]",
            "HKCU,\"Control Panel\\Cursors\\Schemes\",\"Trampa\",,\"%10%\\Cursors\\arrow.ani\"",
            "",
            "[Scheme.Cur]",
            "arrow.ani,..\\evil\\arrow.ani"));

        var ex = Assert.Throws<InvalidOperationException>(
            () => InfParser.ParseFile(inf, t.Combine("pack")));
        Assert.Contains("fuera del paquete", ex.Message);
    }

    [Fact]
    public void RutaAbsolutaEnSchemeCur_Lanza()
    {
        using var t = new TempDir();
        string secret = t.Write("secret.ani", "x"); // existe, fuera del pack
        string inf = t.Write("pack/install.inf", Lines(
            "[Scheme.Reg]",
            "HKCU,\"Control Panel\\Cursors\\Schemes\",\"Trampa\",,\"%10%\\Cursors\\arrow.ani\"",
            "",
            "[Scheme.Cur]",
            $"arrow.ani,{secret}"));

        var ex = Assert.Throws<InvalidOperationException>(
            () => InfParser.ParseFile(inf, t.Combine("pack")));
        Assert.Contains("fuera del paquete", ex.Message);
    }

    [Fact]
    public void ArchivoNoCursor_Lanza()
    {
        using var t = new TempDir();
        t.Write("evil.exe", "MZ"); // presente en el pack, pero no es un cursor
        string inf = t.Write("install.inf", Lines(
            "[Scheme.Reg]",
            "HKCU,\"Control Panel\\Cursors\\Schemes\",\"Trampa\",,\"%10%\\Cursors\\evil.exe\""));

        var ex = Assert.Throws<InvalidOperationException>(
            () => InfParser.ParseFile(inf, t.Root));
        Assert.Contains("no es un archivo de cursor", ex.Message);
    }

    [Fact]
    public void BatchDentroDelPaquete_Lanza()
    {
        using var t = new TempDir();
        t.Write("run.bat", "echo hi");
        string inf = t.Write("install.inf", Lines(
            "[Scheme.Reg]",
            "HKCU,\"Control Panel\\Cursors\\Schemes\",\"Trampa\",,\"%10%\\Cursors\\run.bat\""));

        var ex = Assert.Throws<InvalidOperationException>(
            () => InfParser.ParseFile(inf, t.Root));
        Assert.Contains("no es un archivo de cursor", ex.Message);
    }

    [Fact]
    public void RutaLegitimaDentroDelPaquete_NoLanza()
    {
        using var t = new TempDir();
        t.Write("cursors/arrow.ani", "a");
        string inf = t.Write("install.inf", Lines(
            "[Scheme.Reg]",
            "HKCU,\"Control Panel\\Cursors\\Schemes\",\"Bueno\",,\"%10%\\Cursors\\arrow.ani\"",
            "",
            "[Scheme.Cur]",
            "arrow.ani,.\\cursors\\arrow.ani"));

        var parsed = InfParser.ParseFile(inf, t.Root);
        Assert.Equal("Bueno", parsed.SchemeName);
        Assert.True(InfParser.IsUnder(parsed.SourceFiles[0], t.Root));
    }
}
