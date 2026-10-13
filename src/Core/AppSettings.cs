using System.Text.Json;
using Microsoft.Win32;

namespace CursorRotator.Core;

/// <summary>Entrada de agenda: hora "HH:mm" + cursor destino (vacío = aleatorio).</summary>
public sealed class ScheduledEntry
{
    public string Time { get; set; } = "09:00";
    public string Scheme { get; set; } = "";

    public TimeSpan TimeOfDay
    {
        get
        {
            try
            {
                var p = (Time ?? "09:00").Split(':');
                return new TimeSpan(int.Parse(p[0]), int.Parse(p[1]), 0);
            }
            catch { return new TimeSpan(9, 0, 0); }
        }
    }

    public override string ToString() =>
        $"{Time} → {(string.IsNullOrEmpty(Scheme) ? "🎲 Aleatorio" : Scheme)}";
}

/// <summary>Preferencias persistidas en %LOCALAPPDATA%\CursorRotator\settings.json.</summary>
public sealed class AppSettings
{
    public int IntervalMinutes { get; set; } = 15;
    public bool MinimizeToTrayOnClose { get; set; } = true;
    public bool StartWithWindows { get; set; } = false;
    public bool DarkMode { get; set; } = true;
    public bool StartRotationOnLaunch { get; set; } = false;
    public bool TrailEnabled { get; set; } = false;
    public int TrailPreset { get; set; } = 2;
    public int TrailSize { get; set; } = 10;
    public int TrailLife { get; set; } = 10;
    public int TrailDensity { get; set; } = 10;
    public bool TrailUseColor { get; set; } = false;
    public string ManualColorHex { get; set; } = "";
    /// <summary>Esquemas desmarcados del gestor: no entran en la rotación.</summary>
    public List<string> ExcludedFromRotation { get; set; } = new();
    /// <summary>0 = predeterminado, 1 = heredado, 2 = manual.</summary>
    public int ColorMode { get; set; } = 0;
    public int TrailOrigin { get; set; } = 0;
    public int TrailEngineMode { get; set; } = 0;
    public bool IncludeStatic { get; set; } = false;
    public bool ForceTopmost { get; set; } = false;
    /// <summary>0 = auto-detect por monitor, -1 = uncapped, otro = tope en Hz.</summary>
    public int RefreshCapHz { get; set; } = 0;
    public bool ClickEnabled { get; set; } = false;
    public int ClickEffect { get; set; } = 0;
    public int ClickSize { get; set; } = 10;
    public bool ClickInheritColor { get; set; } = true;
    public bool ShowTooltips { get; set; } = true;
    public bool ShowTutorial { get; set; } = true;
    public bool ShowSplash { get; set; } = true;
    /// <summary>Estela secundaria concurrente (dual trail). Apagada por defecto (coste CPU/GPU).</summary>
    public bool SecondaryTrailEnabled { get; set; } = false;
    /// <summary>Preset de la estela secundaria (índice de TrailPreset).</summary>
    public int SecondaryTrailPreset { get; set; } = 0;
    /// <summary>Geometría de la ventana principal (0 = sin guardar).</summary>
    public int WindowX { get; set; } = 0;
    public int WindowY { get; set; } = 0;
    public int WindowW { get; set; } = 0;
    public int WindowH { get; set; } = 0;
    /// <summary>Intervalo en segundos (decimales). Fuente de verdad.</summary>
    public double IntervalSeconds { get; set; } = 900;
    /// <summary>0 = cada X (relativo), 1 = horas fijas diarias.</summary>
    public int IntervalMode { get; set; } = 0;
    /// <summary>Hora fija diaria "HH:mm" (legado: una sola hora).</summary>
    public string ScheduledTime { get; set; } = "09:00";
    /// <summary>Horas fijas diarias "HH:mm" (legado: sin destino).</summary>
    public List<string> ScheduledTimes { get; set; } = new() { "09:00" };
    /// <summary>Agenda: hora + cursor destino (vacío = aleatorio).</summary>
    public List<ScheduledEntry> Schedule { get; set; } = new() { new ScheduledEntry { Time = "09:00" } };
    /// <summary>Animación orbital de los marcos de tarjetas.</summary>
    public bool CardAnimation { get; set; } = true;
    /// <summary>Repo GitHub "dueño/nombre" para buscar actualizaciones.</summary>
    public string UpdateRepo { get; set; } = "";
    /// <summary>Avisar automáticamente al abrir (máx. 1 vez al día).</summary>
    public bool CheckUpdates { get; set; } = true;
    /// <summary>Último aviso consultado (yyyy-MM-dd).</summary>
    public string LastUpdateCheck { get; set; } = "";
    /// <summary>Versión omitida por el usuario (no volver a ofrecer).</summary>
    public string SkippedVersion { get; set; } = "";

    private static string FilePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CursorRotator", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath))
                        ?? new AppSettings();
                // Migración (solo legado): la vieja casilla equivalía a modo
                // heredado. El modo explícito siempre gana: si ya hay un
                // ColorMode distinto de 0, se respeta y no se pisa.
                if (s.TrailUseColor && s.ColorMode == 0) s.ColorMode = 1;
                // Migración de intervalo: antes solo minutos enteros.
                if (s.IntervalSeconds <= 0)
                    s.IntervalSeconds = Math.Max(5, s.IntervalMinutes * 60.0);
                if (string.IsNullOrWhiteSpace(s.ScheduledTime)) s.ScheduledTime = "09:00";
                // Migración de agenda: de una hora a lista de horas.
                if (s.ScheduledTimes is null || s.ScheduledTimes.Count == 0)
                    s.ScheduledTimes = new List<string> { s.ScheduledTime };
                // Migración a agenda con destino (vacío = aleatorio).
                if (s.Schedule is null || s.Schedule.Count == 0)
                    s.Schedule = s.ScheduledTimes
                        .Where(t => !string.IsNullOrWhiteSpace(t)).Distinct()
                        .Select(t => new ScheduledEntry { Time = t.Trim() }).ToList();
                if (s.Schedule.Count == 0)
                    s.Schedule.Add(new ScheduledEntry { Time = "09:00" });
                s.Audit("LOAD");
                return s;
            }
        }
        catch
        {
            // Ajustes corruptos: valores por defecto (se audita para cazar
            // reseteos: si ves LOAD-DEFAULTS, el archivo no se pudo leer).
            var dflt = new AppSettings();
            dflt.Audit("LOAD-DEFAULTS");
            return dflt;
        }
        return new AppSettings();
    }

    public void Save()
    {
        string dir = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(dir);
        // Escritura atómica: si matan el proceso a mitad, el anterior queda
        // intacto (un settings.json a medias provocaba "reseteo" al reabrir).
        string tmp = Path.Combine(dir, "settings.tmp");
        File.WriteAllText(tmp, JsonSerializer.Serialize(this,
            new JsonSerializerOptions { WriteIndented = true }));
        File.Move(tmp, FilePath, overwrite: true);
        Audit("SAVE");
    }

    /// <summary>Traza libre al audit log (diagnóstico). Jamás lanza.</summary>
    public static void Trace(string msg)
    {
        try
        {
            string log = Path.Combine(
                Path.GetDirectoryName(FilePath)!, "settings-audit.log");
            var lines = File.Exists(log) ? File.ReadAllLines(log).ToList() : new List<string>();
            lines.Add($"{DateTime.Now:HH:mm:ss} TRACE pid={Environment.ProcessId} {msg}");
            while (lines.Count > 300) lines.RemoveAt(0);
            File.WriteAllLines(log, lines);
        }
        catch { }
    }

    /// <summary>
    /// Auditoría de persistencia (settings-audit.log, máx. 300 líneas): cada
    /// guardado/carga anota exe, pid y valores clave. Para diagnosticar
    /// pérdidas. Jamás lanza.
    /// </summary>
    private void Audit(string action)
    {
        try
        {
            string log = Path.Combine(
                Path.GetDirectoryName(FilePath)!, "settings-audit.log");
            string line = $"{DateTime.Now:HH:mm:ss} {action} " +
                $"exe={Environment.ProcessPath ?? "?"} " +
                $"pid={Environment.ProcessId} " +
                $"size={TrailSize} life={TrailLife} dens={TrailDensity} " +
                $"color={ColorMode} clicks={ClickEnabled}/{ClickInheritColor} " +
                $"preset={TrailPreset} eng={TrailEngineMode} org={TrailOrigin} " +
                $"sec={SecondaryTrailEnabled}/{SecondaryTrailPreset} " +
                $"interval={IntervalMinutes} sched={Schedule.Count}";
            var lines = File.Exists(log) ? File.ReadAllLines(log).ToList() : new List<string>();
            lines.Add(line);
            while (lines.Count > 300) lines.RemoveAt(0);
            File.WriteAllLines(log, lines);
        }
        catch { }
    }

    public void ApplyAutostart()
    {
        const string runPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        using var key = Registry.CurrentUser.CreateSubKey(runPath);
        if (key is null) return;
        if (StartWithWindows)
            key.SetValue("CursorRotator",
                $"\"{Environment.ProcessPath ?? Application.ExecutablePath}\" --minimized");
        else if (key.GetValue("CursorRotator") is not null)
            key.DeleteValue("CursorRotator");
    }
}
