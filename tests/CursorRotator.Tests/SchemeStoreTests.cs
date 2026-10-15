using CursorRotator.Core;
using Xunit;

namespace CursorRotator.Tests;

/// <summary>
/// Resolución de rutas, plan de roles (dry-run) y ciclo alta/baja de esquemas
/// en HKCU (con nombres únicos y limpieza garantizada).
/// </summary>
public class SchemeStoreTests
{
    [Fact]
    public void ResolveCursorPath_AbsolutaSeMantiene()
    {
        string p = SchemeStore.ResolveCursorPath(@"C:\cur\arrow.cur");
        Assert.Equal(@"C:\cur\arrow.cur", p);
    }

    [Fact]
    public void ResolveCursorPath_RelativaSeResuelveContraWindowsCursors()
    {
        string p = SchemeStore.ResolveCursorPath("arrow.cur");
        Assert.True(Path.IsPathRooted(p));
        Assert.EndsWith(Path.Combine("Cursors", "arrow.cur"), p);
    }

    [Fact]
    public void ResolveCursorPath_ExpandeVariablesDeEntorno()
    {
        string p = SchemeStore.ResolveCursorPath(@"%TEMP%\x.cur");
        Assert.DoesNotContain("%", p);
        Assert.EndsWith("x.cur", p);
    }

    [Fact]
    public void ResolveCursorPath_QuitaComillasYEspacios()
    {
        string p = SchemeStore.ResolveCursorPath("  \"arrow.cur\"  ");
        Assert.EndsWith(Path.Combine("Cursors", "arrow.cur"), p);
    }

    [Fact]
    public void BuildRolePlan_Mapea15RolesPorPosicion()
    {
        var paths = SchemeStore.StandardRoles.Select(r => @"C:\x\" + r + ".cur").ToArray();
        var plan = SchemeStore.BuildRolePlan(paths);

        Assert.Equal(SchemeStore.StandardRoles.Length, plan.Count);
        for (int i = 0; i < plan.Count; i++)
        {
            Assert.Equal(SchemeStore.StandardRoles[i], plan[i].Role);
            Assert.Equal(paths[i], plan[i].Path);
        }
    }

    [Fact]
    public void BuildRolePlan_ExtrasPersonYPinPorNombreDeArchivo()
    {
        // Los extras (Person/Pin) solo aplican a partir del rol 16 (tras los 15).
        var paths = SchemeStore.StandardRoles.Select(r => @"C:\x\" + r + ".cur")
            .Concat(new[] { @"C:\x\person.ani", @"C:\x\pin.ani", @"C:\x\extra.ani" })
            .ToArray();
        var plan = SchemeStore.BuildRolePlan(paths);

        Assert.Equal(17, plan.Count);
        Assert.Contains(("Person", @"C:\x\person.ani"), plan);
        Assert.Contains(("Pin", @"C:\x\pin.ani"), plan);
        Assert.DoesNotContain(plan, p => p.Path == @"C:\x\extra.ani");
    }

    [Fact]
    public void PlanApply_DevuelveRolesSinTocarElRegistroActivo()
    {
        using var t = new TempDir();
        string store = t.Combine("store");
        Directory.CreateDirectory(store);
        var (names, _) = TestRegistry.RegisterSchemes(1, store);
        string name = names[0];
        try
        {
            string[] registered = SchemeStore.GetSchemePaths(name);
            var plan = SchemeStore.PlanApply(name);
            Assert.Single(plan);
            Assert.Equal("Arrow", plan[0].Role);
            Assert.Equal(registered[0], plan[0].Path);
        }
        finally { TestRegistry.Unregister(names, store); }
    }

    [Fact]
    public void SetScheme_GetSchemePaths_Exists_Delete_RoundTrip()
    {
        using var t = new TempDir();
        string store = t.Combine("store");
        Directory.CreateDirectory(store);
        string name = "zzRoundTrip_" + Guid.NewGuid().ToString("N")[..8];
        string dir = Path.Combine(store, name);
        Directory.CreateDirectory(dir);
        string cur = Path.Combine(dir, "a.cur");
        File.WriteAllText(cur, "x");
        try
        {
            SchemeStore.SetScheme(name, new[] { cur });
            Assert.True(SchemeStore.Exists(name));
            Assert.Equal(new[] { cur }, SchemeStore.GetSchemePaths(name));
            Assert.Contains(name, SchemeStore.GetSchemeNames());

            Assert.True(SchemeStore.DeleteScheme(name, store));
            Assert.False(SchemeStore.Exists(name));
            Assert.False(Directory.Exists(dir));
        }
        finally
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    SchemeStore.SchemesKeyPath, writable: true);
                key?.DeleteValue(name, throwOnMissingValue: false);
                if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            }
            catch { }
        }
    }

    [Fact]
    public void DeleteScheme_NoBorraCarpetaFueraDelAlmacen()
    {
        using var t = new TempDir();
        string store = t.Combine("store");
        string outside = t.Combine("outside");
        Directory.CreateDirectory(store);
        Directory.CreateDirectory(outside);
        string name = "zzOutside_" + Guid.NewGuid().ToString("N")[..8];
        string cur = Path.Combine(outside, "a.cur");
        File.WriteAllText(cur, "x");
        try
        {
            SchemeStore.SetScheme(name, new[] { cur });
            SchemeStore.DeleteScheme(name, store);

            Assert.False(SchemeStore.Exists(name));       // el registro sí se borra
            Assert.True(Directory.Exists(outside));       // la carpeta externa NO
            Assert.True(File.Exists(cur));
        }
        finally
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    SchemeStore.SchemesKeyPath, writable: true);
                key?.DeleteValue(name, throwOnMissingValue: false);
            }
            catch { }
        }
    }
}
