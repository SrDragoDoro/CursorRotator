using System.IO.Compression;
using CursorRotator.Core;
using Xunit;

namespace CursorRotator.Tests;

/// <summary>
/// Instalador: clasificación de archivos, agrupación de cursores sueltos,
/// extracción de comprimidos y examen multi-paquete con deduplicación.
/// </summary>
public class CursorInstallerTests
{
    [Theory]
    [InlineData("arrow.cur", true)]
    [InlineData("busy.ANI", true)]
    [InlineData("readme.txt", false)]
    [InlineData("noext", false)]
    public void IsCursorFile_SoloCurYAni(string path, bool expected) =>
        Assert.Equal(expected, CursorInstaller.IsCursorFile(path));

    [Theory]
    [InlineData("pack.zip", true)]
    [InlineData("pack.RAR", true)]
    [InlineData("pack.7z", true)]
    [InlineData("pack.tar", true)]
    [InlineData("pack.gz", true)]
    [InlineData("pack.txt", false)]
    [InlineData("pack", false)]
    public void IsArchiveFile_ClasificaExtensiones(string path, bool expected) =>
        Assert.Equal(expected, CursorInstaller.IsArchiveFile(path));

    [Fact]
    public void ArchiveFilePattern_IncluyeFormatosClave()
    {
        string pattern = CursorInstaller.ArchiveFilePattern;
        foreach (var ext in new[] { "*.zip", "*.rar", "*.7z", "*.tar", "*.gz" })
            Assert.Contains(ext, pattern);
    }

    [Theory]
    [InlineData(@"C:\pack\MacOS\arrow.cur", true)]
    [InlineData(@"C:\pack\cursors\arrow.cur", false)]
    public void IsExcluded_DetectaCarpetaMacOS(string path, bool expected) =>
        Assert.Equal(expected, CursorInstaller.IsExcluded(path));

    [Fact]
    public void SynthesizeLooseSchemes_AgrupaPorCarpetaYOrdenaRoles()
    {
        using var t = new TempDir();
        t.Write("Neon/wait.ani", "x");
        t.Write("Neon/arrow.ani", "x");
        t.Write("Neon/hand.cur", "x");

        var schemes = CursorInstaller.SynthesizeLooseSchemes(
            t.Root, new HashSet<string>(StringComparer.OrdinalIgnoreCase), "base");

        var neon = Assert.Single(schemes);
        Assert.Equal("Neon", neon.SchemeName);
        Assert.Equal(3, neon.SourceFiles.Count);
        Assert.Equal("arrow.ani", Path.GetFileName(neon.SourceFiles[0])); // rol 0 primero
        Assert.Equal("hand.cur", Path.GetFileName(neon.SourceFiles[^1])); // rol 14 al final
    }

    [Fact]
    public void SynthesizeLooseSchemes_CarpetaRaiz_UsaNombreBase()
    {
        using var t = new TempDir();
        t.Write("arrow.cur", "x");

        var schemes = CursorInstaller.SynthesizeLooseSchemes(
            t.Root, new HashSet<string>(StringComparer.OrdinalIgnoreCase), "MiPack");

        Assert.Single(schemes);
        Assert.Equal("MiPack", schemes[0].SchemeName);
    }

    [Fact]
    public void SynthesizeLooseSchemes_IgnoraArchivosCubiertosPorInf()
    {
        using var t = new TempDir();
        string covered = t.Write("Neon/arrow.cur", "x");
        t.Write("Neon/wait.ani", "x");

        var schemes = CursorInstaller.SynthesizeLooseSchemes(
            t.Root,
            new HashSet<string>(new[] { covered }, StringComparer.OrdinalIgnoreCase),
            "base");

        var neon = Assert.Single(schemes);
        Assert.Single(neon.SourceFiles); // solo el no cubierto
        Assert.Equal("wait.ani", Path.GetFileName(neon.SourceFiles[0]));
    }

    [Fact]
    public void ExtractArchive_Zip_RoundTrip()
    {
        using var t = new TempDir();
        string src = t.Combine("src");
        Directory.CreateDirectory(src);
        File.WriteAllText(Path.Combine(src, "a.cur"), "x");
        string zip = t.Combine("pack.zip");
        ZipFile.CreateFromDirectory(src, zip);

        string dest = t.Combine("out");
        Directory.CreateDirectory(dest);
        CursorInstaller.ExtractArchive(zip, dest);

        Assert.True(File.Exists(Path.Combine(dest, "a.cur")));
    }

    [Fact]
    public async Task ScanManyAsync_DeduplicaEsquemasEntrePaquetes()
    {
        using var t = new TempDir();
        t.Write("A/Neon/arrow.cur", "x");
        t.Write("B/Neon/arrow.cur", "x");
        var src = new CursorInstaller();

        var scan = await src.ScanManyAsync(new[] { t.Combine("A"), t.Combine("B") });

        Assert.Single(scan.Candidates);
        Assert.Contains(scan.Errors, e => e.Contains("repetido"));
        Assert.Empty(scan.StagingDirs); // carpetas: sin temporales
    }

    [Fact]
    public async Task ScanManyAsync_Zip_ExtraeYExaminayLimpia()
    {
        using var t = new TempDir();
        t.Write("src/Neon/arrow.cur", "x");
        string zip = t.Combine("pack.zip");
        ZipFile.CreateFromDirectory(t.Combine("src"), zip);
        var src = new CursorInstaller();

        var scan = await src.ScanManyAsync(new[] { zip });
        try
        {
            var cand = Assert.Single(scan.Candidates);
            Assert.Equal("Neon", cand.Scheme.SchemeName);
            Assert.True(cand.IsLoose);
            Assert.Single(scan.StagingDirs);
            Assert.True(Directory.Exists(scan.StagingDirs[0]));
        }
        finally { CursorInstaller.CleanupStaging(scan.StagingDirs); }

        Assert.False(Directory.Exists(scan.StagingDirs[0])); // se limpió
    }

    [Fact]
    public async Task ScanManyAsync_FormatoNoSoportado_Lanza()
    {
        using var t = new TempDir();
        string txt = t.Write("readme.txt", "hola");
        var src = new CursorInstaller();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => src.ScanManyAsync(new[] { txt }));
    }
}
