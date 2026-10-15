using CursorRotator.Core;
using Xunit;

namespace CursorRotator.Tests;

/// <summary>
/// Persistencia de settings.json: round-trip, tolerancia a corrupción,
/// escritura atómica y migraciones de compatibilidad.
/// </summary>
public class AppSettingsTests
{
    [Fact]
    public void Load_ArchivoAusente_DevuelveDefaults()
    {
        using var t = new TempDir();
        var s = AppSettings.Load(t.Combine("no-existe.json"));
        Assert.Equal(15, s.IntervalMinutes);
        Assert.Equal(0, s.ColorMode);
        Assert.Equal("SrDragoDoro/CursorRotator", s.UpdateRepo);
    }

    [Fact]
    public void SaveYLoad_RoundTrip_ConservaValores()
    {
        using var t = new TempDir();
        string path = t.Combine("settings.json");

        var s = new AppSettings
        {
            IntervalMinutes = 42,
            IntervalSeconds = 90.5,
            TrailPreset = 7,
            SecondaryTrailEnabled = true,
            SecondaryTrailPreset = 12,
            ColorMode = 2,
            ManualColorHex = "#123ABC",
            Schedule = [new ScheduledEntry { Time = "08:00" },
                        new ScheduledEntry { Time = "21:30", Scheme = "X" }],
            ExcludedFromRotation = ["a", "b"],
            LastInstallDir = @"D:\Packs",
        };
        s.Save(path);

        var r = AppSettings.Load(path);
        Assert.Equal(42, r.IntervalMinutes);
        Assert.Equal(90.5, r.IntervalSeconds);
        Assert.Equal(7, r.TrailPreset);
        Assert.True(r.SecondaryTrailEnabled);
        Assert.Equal(12, r.SecondaryTrailPreset);
        Assert.Equal(2, r.ColorMode);
        Assert.Equal("#123ABC", r.ManualColorHex);
        Assert.Equal(2, r.Schedule.Count);
        Assert.Equal("21:30", r.Schedule[1].Time);
        Assert.Equal("X", r.Schedule[1].Scheme);
        Assert.Equal(new[] { "a", "b" }, r.ExcludedFromRotation);
        Assert.Equal(@"D:\Packs", r.LastInstallDir);
    }

    [Fact]
    public void Load_JsonCorrupto_DevuelveDefaultsSinLanzar()
    {
        using var t = new TempDir();
        string path = t.Write("settings.json", "{ esto no es json valido");
        var s = AppSettings.Load(path);
        Assert.Equal(15, s.IntervalMinutes);
    }

    [Fact]
    public void Save_EsAtomico_NoDejaArchivoTemporal()
    {
        using var t = new TempDir();
        string path = t.Combine("settings.json");
        new AppSettings().Save(path);

        Assert.True(File.Exists(path));
        Assert.False(File.Exists(t.Combine("settings.tmp")));
    }

    // ---------- Migraciones (puras) ----------

    [Fact]
    public void Migrate_TrailUseColor_ActivaModoHeredado()
    {
        var s = new AppSettings { TrailUseColor = true, ColorMode = 0 };
        AppSettings.Migrate(s);
        Assert.Equal(1, s.ColorMode);
    }

    [Fact]
    public void Migrate_ColorModeExplicito_NoSePisa()
    {
        var s = new AppSettings { TrailUseColor = true, ColorMode = 2 };
        AppSettings.Migrate(s);
        Assert.Equal(2, s.ColorMode);
    }

    [Theory]
    [InlineData(3, 180)]
    [InlineData(0, 5)]
    public void Migrate_IntervaloCero_DerivaDeMinutosConMinimo5(int minutes, double expected)
    {
        var s = new AppSettings { IntervalMinutes = minutes, IntervalSeconds = 0 };
        AppSettings.Migrate(s);
        Assert.Equal(expected, s.IntervalSeconds);
    }

    [Fact]
    public void Migrate_IntervaloPositivo_NoCambia()
    {
        var s = new AppSettings { IntervalSeconds = 12.5 };
        AppSettings.Migrate(s);
        Assert.Equal(12.5, s.IntervalSeconds);
    }

    [Fact]
    public void Migrate_RepoVacio_PoneDefault()
    {
        var s = new AppSettings { UpdateRepo = "   " };
        AppSettings.Migrate(s);
        Assert.Equal("SrDragoDoro/CursorRotator", s.UpdateRepo);
    }

    [Fact]
    public void Migrate_ScheduledTimeVacio_Pone09()
    {
        var s = new AppSettings { ScheduledTime = "", Schedule = [new ScheduledEntry()] };
        AppSettings.Migrate(s);
        Assert.Equal("09:00", s.ScheduledTime);
    }

    [Fact]
    public void Migrate_AgendaVacia_DerivaDeScheduledTimes()
    {
        var s = new AppSettings
        {
            ScheduledTimes = ["07:00", "19:30", "07:00"],
            Schedule = new(),
        };
        AppSettings.Migrate(s);
        Assert.Equal(2, s.Schedule.Count);
        Assert.Equal("07:00", s.Schedule[0].Time);
        Assert.Equal("19:30", s.Schedule[1].Time);
    }

    [Fact]
    public void Migrate_TodoVacio_DejaAgendaConUnaHora()
    {
        var s = new AppSettings { ScheduledTimes = new(), Schedule = new() };
        AppSettings.Migrate(s);
        Assert.Single(s.Schedule);
        Assert.Equal("09:00", s.Schedule[0].Time);
    }
}
