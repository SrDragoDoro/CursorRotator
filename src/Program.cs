using System.Runtime.InteropServices;
using System.Text;
using CursorRotator.Core;
using CursorRotator.Core.Trails;
using CursorRotator.UI;

namespace CursorRotator;

internal static class Program
{
    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();

    [STAThread]
    private static int Main(string[] args)
    {
        bool cli = args.Contains("--self-test") || args.Contains("--install")
                || args.Contains("--install-test") || args.Contains("--install-many-test")
                || args.Contains("--trail-test") || args.Contains("--cursorcolor-test")
                || args.Contains("--schemes-test") || args.Contains("--resolve-test")
                || args.Contains("--apply-test") || args.Contains("--plan-test")
                || args.Contains("--dump-ui")
                || args.Contains("--diag-scroll") || args.Contains("--roles-test")
                || args.Contains("--settings-test") || args.Contains("--queue-test")
                || args.Contains("--cards-test") || args.Contains("--update-test")
                || args.Contains("--mapping-test");
        if (cli) AllocConsole(); // El .exe es WinExe: crea consola para el modo CLI.

        // Capturador global: en GUI escribe crash.log + MessageBox en vez de morir en silencio.
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => ShowCrash(e.Exception, cli);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            ShowCrash(e.ExceptionObject as Exception, cli);

        try
        {
            if (cli) return RunCli(args);

            ApplicationConfiguration.Initialize();
            bool minimized = args.Contains("--minimized");

            // Instancia única: si ya hay una corriendo (típico: quedó en la
            // bandeja), la 2ª solo la trae al frente y sale. Así dos procesos
            // nunca cargan/guardian el settings.json por turnos (pérdidas).
            if (!AcquireSingleInstance(out bool isFirst))
                return 0; // sin IPC disponible: no duplicar tampoco
            if (!isFirst)
            {
                SignalFirstInstance();
                return 0;
            }

            var bootSettings = AppSettings.Load();
            bool noOnboarding = args.Contains("--no-onboarding"); // automatización/tests
            if (bootSettings.ShowSplash && !minimized && !noOnboarding)
                RunSplash();

            var main = new MainForm(minimized);
            WatchSingleInstanceSignal(main);
            if (bootSettings.ShowTutorial && !minimized && !noOnboarding)
                TutorialForm.ShowBeside(main, AppSettings.Load(), bootSettings.DarkMode);
            Application.Run(main);
            return 0;
        }
        catch (Exception ex)
        {
            ShowCrash(ex, cli);
            return 99;
        }
    }

    /// <summary>
    /// Splash con inicialización REAL (lee ajustes + esquemas + primer .ani).
    /// Instancia única (Mutex + evento con nombre, ámbito de sesión).
    private static Mutex _singleLock;
    private const string SingleMutexName = @"Local\CursorRotator_SingleInstance";
    private const string SingleEventName = @"Local\CursorRotator_Show";

    /// <returns>false solo si ni siquiera se pudo evaluar (no duplicar).</returns>
    private static bool AcquireSingleInstance(out bool isFirst)
    {
        isFirst = true;
        try
        {
            _singleLock = new Mutex(true, SingleMutexName, out bool created);
            isFirst = created;
            return true;
        }
        catch (AbandonedMutexException)
        {
            // El dueño anterior murió sin liberar: somos los primeros.
            isFirst = true;
            try { _singleLock = Mutex.OpenExisting(SingleMutexName); } catch { }
            return true;
        }
        catch
        {
            isFirst = false;
            return false;
        }
    }

    /// <summary>Pide a la primera instancia que muestre su ventana.</summary>
    private static void SignalFirstInstance()
    {
        try
        {
            using var ev = EventWaitHandle.OpenExisting(SingleEventName);
            ev.Set();
        }
        catch { /* nadie escuchando: nada que mostrar */ }
    }

    /// <summary>Hilo de fondo: al recibir la señal, trae la ventana al frente.</summary>
    private static void WatchSingleInstanceSignal(MainForm main)
    {
        var t = new Thread(() =>
        {
            try
            {
                using var ev = new EventWaitHandle(false, EventResetMode.AutoReset, SingleEventName);
                while (!main.IsDisposed)
                {
                    ev.WaitOne();
                    try { main.BeginInvoke((Action)main.ShowMainWindow); }
                    catch { }
                }
            }
            catch { }
        })
        { IsBackground = true, Name = "SingleInstance" };
        t.Start();
    }    /// Patrón modal: el Timer interno de SplashForm anima la barra mientras
    /// el BootTask hace el trabajo; al 100% cierra con DialogResult.OK y el
    /// flujo continúa con Application.Run(new MainForm(...)).
    /// </summary>
    private static void RunSplash()
    {
        using var splash = new SplashForm();
        splash.BootTask = async report =>
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                report(15, "Inicializando motores de estelas...");
                await Task.Delay(120).ConfigureAwait(true);
                var schemes = SchemeStore.GetSchemeNames();
                report(45, $"Buscando esquemas de cursores registrados... ({schemes.Count})");
                await Task.Delay(120).ConfigureAwait(true);
                string arrow = CursorColorReader.GetActiveArrowPath();
                if (!string.IsNullOrEmpty(arrow))
                {
                    report(70, "Cargando presets de partículas...");
                    await Task.Run(() => CursorColorReader.GetActiveArrowColor())
                        .ConfigureAwait(true);
                }
            }
            catch { /* el splash jamás debe tumbar el arranque */ }
            int remain = 1200 - (int)sw.ElapsedMilliseconds;
            if (remain > 0)
            {
                report(90, "Listo...");
                await Task.Delay(remain).ConfigureAwait(true);
            }
            report(100, "Listo");
            await Task.Delay(120).ConfigureAwait(true);
        };
        splash.ShowDialog();
    }

    private static void ShowCrash(Exception ex, bool cli)
    {
        string msg = $"Error:\n{ex?.Message}\n\nStack Trace:\n{ex?.StackTrace}";
        try
        {
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "crash.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {msg}{Environment.NewLine}");
        }
        catch { /* si ni el log se puede escribir, al menos mostrar */ }
        if (cli) Console.WriteLine(msg);
        else MessageBox.Show(msg, "Error - CursorRotator",
            MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private static int RunCli(string[] args)
    {
        if (args.Contains("--self-test"))
        {
            string dir = args.SkipWhile(a => a != "--self-test").Skip(1).FirstOrDefault()
                         ?? Environment.CurrentDirectory;
            return SelfTest(dir);
        }

        if (args.Contains("--trail-test"))
            return TrailTest();

        if (args.Contains("--cursorcolor-test"))
        {
            string dir = args.SkipWhile(a => a != "--cursorcolor-test").Skip(1).FirstOrDefault()
                         ?? CursorInstaller.DefaultStoreDir;
            return CursorColorTest(dir);
        }

        if (args.Contains("--install-test"))
            return InstallTest();

        if (args.Contains("--install-many-test"))
            return InstallManyTest();

        if (args.Contains("--update-test"))
        {
            string repo = args.SkipWhile(a => a != "--update-test").Skip(1).FirstOrDefault()
                ?? "nadie/noexiste12345";
            return UpdateTest(repo);
        }

        if (args.Contains("--mapping-test"))
            return MappingTest();

        if (args.Contains("--install"))
        {
            string src = args.SkipWhile(a => a != "--install").Skip(1).FirstOrDefault()
                         ?? CursorInstaller.AutoDetectSource();
            if (src is null)
            {
                Console.WriteLine("No se encontró material instalable.");
                return 2;
            }
            return HeadlessInstall(src, args.Contains("--include-static"));
        }

        if (args.Contains("--schemes-test"))
            return SchemesTest();

        if (args.Contains("--dump-ui"))
        {
            var rest = args.SkipWhile(a => a != "--dump-ui").Skip(1).ToArray();
            int w = rest.Length > 0 && int.TryParse(rest[0], out var ww) ? ww : 680;
            int h = rest.Length > 1 && int.TryParse(rest[1], out var hh) ? hh : 600;
            string outFile = rest.Length > 2 ? rest[2] : "uidump.txt";
            return DumpUi(w, h, outFile);
        }

        if (args.Contains("--roles-test"))
        {
            string name = args.SkipWhile(a => a != "--roles-test").Skip(1).FirstOrDefault();
            if (string.IsNullOrEmpty(name))
            {
                Console.WriteLine("Uso: --roles-test \"Nombre del esquema\"");
                return 2;
            }
            return RolesTest(name);
        }

        if (args.Contains("--settings-test"))
            return SettingsTest();

        if (args.Contains("--queue-test"))
            return QueueTest();

        if (args.Contains("--cards-test"))
            return CardsTest();

        if (args.Contains("--diag-scroll"))
            return DiagScroll();

        if (args.Contains("--resolve-test"))
            return ResolveTest();

        if (args.Contains("--apply-test"))
        {
            string name = args.SkipWhile(a => a != "--apply-test").Skip(1).FirstOrDefault();
            if (string.IsNullOrEmpty(name))
            {
                Console.WriteLine("Uso: --apply-test \"Nombre del esquema\"");
                return 2;
            }
            return ApplyTest(name);
        }

        if (args.Contains("--plan-test"))
        {
            string name = args.SkipWhile(a => a != "--plan-test").Skip(1).FirstOrDefault();
            if (string.IsNullOrEmpty(name))
            {
                Console.WriteLine("Uso: --plan-test \"Nombre del esquema\"");
                return 2;
            }
            return PlanTest(name);
        }

        Console.WriteLine("Modo CLI no reconocido.");
        return 2;
    }

    /// <summary>
    /// Diagnóstico del bug [pixloen]: resuelve las rutas de TODOS los esquemas
    /// registrados (variables + relativas) y reporta archivos faltantes.
    /// No modifica nada: solo lectura del registro + File.Exists.
    /// </summary>
    private static int ResolveTest()
    {
        var names = SchemeStore.GetSchemeNames();
        int okSchemes = 0, badSchemes = 0, okFiles = 0, missingFiles = 0;
        var missing = new List<string>();
        foreach (var name in names)
        {
            string[] raw, resolved;
            try
            {
                // Crudo vs resuelto para evidenciar el problema.
                raw = SchemeStore.GetSchemePaths(name);
                resolved = raw.Select(SchemeStore.ResolveCursorPath).ToArray();
            }
            catch (Exception ex)
            {
                badSchemes++;
                Console.WriteLine($"FALLO {name} :: {ex.Message}");
                continue;
            }
            bool schemeOk = true;
            for (int i = 0; i < raw.Length; i++)
            {
                bool rawExists = File.Exists(raw[i]);
                bool resExists = File.Exists(resolved[i]);
                if (resExists) okFiles++;
                else
                {
                    missingFiles++;
                    schemeOk = false;
                    missing.Add($"{name} :: crudo='{raw[i]}' resuelto='{resolved[i]}'");
                }
                if (raw[i] != resolved[i] && !rawExists && resExists)
                    Console.WriteLine($"SANADO {name} :: '{raw[i]}' -> '{resolved[i]}'");
            }
            if (schemeOk) okSchemes++;
            else badSchemes++;
        }
        Console.WriteLine($"=== Esquemas OK={okSchemes} CON FALLOS={badSchemes} | Archivos OK={okFiles} FALTANTES={missingFiles} ===");
        foreach (var m in missing.Take(20)) Console.WriteLine("FALTA: " + m);
        return badSchemes == 0 ? 0 : 1;
    }

    /// <summary>
    /// Aplica un esquema por nombre exacto (comparación ordinal, sin patrones:
    /// los corchetes [pixloen] nunca se interpretan como wildcard/regex).
    /// Pensado para verificación E2E con restauración posterior.
    /// </summary>
    private static int ApplyTest(string name)
    {
        var names = SchemeStore.GetSchemeNames();
        string match = names.FirstOrDefault(n =>
            string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            Console.WriteLine($"No existe el esquema '{name}'.");
            return 2;
        }
        try
        {
            SchemeStore.ApplyScheme(match);
            Console.WriteLine($"Aplicado OK: {match}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("[Error] " + ex.Message);
            return 1;
        }
    }

    /// <summary>
    /// Dry-run: muestra EXACTAMENTE qué valores escribiría en
    /// HKCU\Control Panel\Cursors al aplicar un esquema (rol → ruta), sin
    /// tocar el registro. Señala archivos faltantes (ExitCode 1 si hay).
    /// </summary>
    private static int PlanTest(string name)
    {
        var names = SchemeStore.GetSchemeNames();
        string match = names.FirstOrDefault(n =>
            string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            Console.WriteLine($"No existe el esquema '{name}'.");
            return 2;
        }
        try
        {
            var plan = SchemeStore.PlanApply(match);
            int missing = 0;
            Console.WriteLine($"Plan para '{match}' ({plan.Count} valor(es) de registro):");
            foreach (var (role, path) in plan)
            {
                bool ok = File.Exists(path);
                if (!ok) missing++;
                Console.WriteLine($"  {(ok ? "OK " : "FAL")} {role,-12} -> {path}");
            }
            Console.WriteLine($"=== PLAN {(missing == 0 ? "OK" : $"CON {missing} FALTANTE(S)")} ===");
            return missing == 0 ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine("[Error] " + ex.Message);
            return 1;
        }
    }

    /// <summary>
    /// Volcado managed del árbol de MainForm a un tamaño dado (diagnóstico
    /// de layout sin mostrar nada en pantalla).
    /// </summary>
    private static int DumpUi(int w, int h, string outFile)
    {
        using var form = new MainForm(true);
        form.CreateControl();
        form.Size = new Size(w, h);
        form.PerformLayout();
        Application.DoEvents();
        using var sw = new StreamWriter(outFile, append: false, Encoding.UTF8);
        DumpControl(form, 0, sw);
        return 0;
    }

    /// <summary>
    /// Diagnóstico del diálogo Ver roles: resuelve rutas, decodifica cada
    /// preview y construye el diálogo sin mostrarlo (ctorea excepciones).
    /// </summary>
    private static int RolesTest(string name)
    {
        try
        {
            var paths = SchemeStore.GetResolvedSchemePaths(name);
            Console.WriteLine($"rutas={paths.Length}");
            int ok = 0, nulls = 0;
            foreach (var p in paths.Take(15))
            {
                bool exists = File.Exists(p);
                var bmp = CursorPreviewCache.Get(p);
                if (bmp is null)
                {
                    nulls++;
                    Console.WriteLine($"NULL existe={exists} :: {p}");
                }
                else { ok++; bmp.Dispose(); }
            }
            using var dlg = new SchemePreviewDialog(name, true);
            dlg.CreateControl();
            dlg.Size = new Size(660, 540);
            dlg.PerformLayout();
            Application.DoEvents();
            using var sw = new StreamWriter("rolesdump.txt", append: false, Encoding.UTF8);
            DumpControl(dlg, 0, sw);
            Console.WriteLine($"OK previews={ok} nulos={nulls} aplicado={SchemePreviewDialog.IsAppliedScheme(name)}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine("FALLO: " + ex);
            return 1;
        }
    }

    /// <summary>
    /// Ida y vuelta de settings.json: escribe todos los campos (incluidos
    /// geometría y estela secundaria), recarga y compara. Restaura lo previo.
    /// </summary>
    private static int SettingsTest()
    {
        var backup = AppSettings.Load();
        try
        {
            var s = AppSettings.Load();
            s.IntervalMinutes = 42;
            s.TrailPreset = 7;
            s.TrailEngineMode = 3;
            s.TrailOrigin = 1;
            s.SecondaryTrailEnabled = true;
            s.SecondaryTrailPreset = 12;
            s.ColorMode = 2;
            s.ManualColorHex = "#123ABC";
            s.WindowX = 11; s.WindowY = 22; s.WindowW = 666; s.WindowH = 777;
            s.IntervalSeconds = 90.5; s.IntervalMode = 1; s.ScheduledTime = "21:30";
            s.ScheduledTimes = ["08:00", "21:30"];
            s.Schedule = [new ScheduledEntry { Time = "08:00" },
                          new ScheduledEntry { Time = "21:30", Scheme = "X" }];
            s.CardAnimation = true;
            s.UpdateRepo = "yo/CursorRotator";
            s.CheckUpdates = false;
            s.ExcludedFromRotation = ["a", "b"];
            s.Save();

            var r = AppSettings.Load();
            bool ok = true;
            void Check(string n, object a, object b)
            {
                bool same = Equals(a, b);
                ok &= same;
                Console.WriteLine($"{(same ? "ok  " : "DIF ")} {n}: guardado={a} leido={b}");
            }
            Check("Interval", 42, r.IntervalMinutes);
            Check("Preset", 7, r.TrailPreset);
            Check("Engine", 3, r.TrailEngineMode);
            Check("Origin", 1, r.TrailOrigin);
            Check("SecOn", true, r.SecondaryTrailEnabled);
            Check("SecPreset", 12, r.SecondaryTrailPreset);
            Check("ColorMode", 2, r.ColorMode);
            Check("Hex", "#123ABC", r.ManualColorHex);
            Check("Geo", "11,22,666,777", $"{r.WindowX},{r.WindowY},{r.WindowW},{r.WindowH}");
            Check("Secs", 90.5, r.IntervalSeconds);
            Check("Mode", 1, r.IntervalMode);
            Check("Hora", "21:30", r.ScheduledTime);
            Check("Horas", "08:00|21:30", string.Join("|", r.ScheduledTimes));
            Check("Agenda", "08:00→|21:30→X", string.Join("|",
                r.Schedule.Select(x => $"{x.Time}→{x.Scheme}")));
            Check("Anim", true, r.CardAnimation);
            Check("Repo", "yo/CursorRotator", r.UpdateRepo);
            Check("AutoUpd", false, r.CheckUpdates);
            Check("Excl", "a|b", string.Join("|", r.ExcludedFromRotation));
            Console.WriteLine((ok ? "OK   " : "FALLO") + " settings round-trip");
            Console.WriteLine(ok ? "=== ALL PASS ===" : "=== FAIL ===");
            return ok ? 0 : 1;
        }
        finally
        {
            try { backup.Save(); } catch { }
        }
    }

    /// <summary>
    /// Contrato vista-previa == botón: lo que PeekNext muestra como
    /// "Siguiente" es lo que TakeNext entrega. Restaura el cursor previo.
    /// </summary>
    private static int QueueTest()
    {
        var names = SchemeStore.GetSchemeNames();
        if (names.Count < 2)
        {
            Console.WriteLine("Se necesitan 2+ esquemas instalados.");
            return 2;
        }
        string prevScheme = FindSchemeOfArrow(CursorColorReader.GetActiveArrowPath());
        var pool = names.Take(2).ToList();
        bool ok = true;
        using var r = new CursorRotator.Core.CursorRotator();
        try
        {
            r.Start(pool, TimeSpan.FromMinutes(60));
            r.Stop(); // sin timer: la cola queda determinista
            Thread.Sleep(300); // drena un tick en vuelo, si lo hubo
            string peekA = r.PeekNext();
            string takeA = r.TakeNext();
            string peekB = r.PeekNext();
            bool contractOk = takeA is not null && takeA == peekA && peekB is not null;
            ok &= contractOk;
            Console.WriteLine($"{(contractOk ? "OK   " : "FALLO")} " +
                $"preview==boton: peek={peekA} take={takeA} next={peekB}");

            // Agenda: planificador one-shot debe disparar 2+ veces en ~8 s.
            r.Stop();
            int fires = 0;
            var applied = new System.Collections.Concurrent.ConcurrentQueue<string>();
            using var gate = new ManualResetEventSlim(false);
            Action<string> count = name =>
            {
                applied.Enqueue(name);
                if (Interlocked.Increment(ref fires) >= 2) gate.Set();
            };
            r.SchemeApplied += count;
            try
            {
                // Primera hora con destino fijo: debe aplicar pool[0] sí o sí.
                r.Start(pool, (DateTime now) => (now.AddSeconds(2), pool[0]));
                bool schedOk = gate.Wait(9000) && fires >= 2;
                ok &= schedOk;
                Console.WriteLine($"{(schedOk ? "OK   " : "FALLO")} reagenda: disparos={fires}");
                bool forcedOk = applied.TryPeek(out var first) && first == pool[0];
                ok &= forcedOk;
                Console.WriteLine($"{(forcedOk ? "OK   " : "FALLO")} destino-fijo: primero={first}");
            }
            finally { r.SchemeApplied -= count; }
        }
        finally
        {
            try { r.Stop(); } catch { }
            try { if (prevScheme is not null) SchemeStore.ApplyScheme(prevScheme); } catch { }
        }
        Console.WriteLine(ok ? "=== ALL PASS ===" : "=== FAIL ===");
        return ok ? 0 : 1;
    }

    private static string FindSchemeOfArrow(string arrowPath)
    {
        if (string.IsNullOrEmpty(arrowPath)) return null;
        try
        {
            string norm = SchemeStore.ResolveCursorPath(arrowPath);
            foreach (var name in SchemeStore.GetSchemeNames())
            {
                var paths = SchemeStore.GetResolvedSchemePaths(name);
                if (paths.Length > 0 && string.Equals(paths[0], norm,
                    StringComparison.OrdinalIgnoreCase))
                    return name;
            }
        }
        catch { }
        return null;
    }

    /// <summary>
    /// Galería de tarjetas sin mostrar: construye el gestor real (decodifica
    /// los 60 iconos + oscurecidos) y vuelca el árbol a cardsdump.txt.
    /// </summary>
    private static int CardsTest()
    {
        try
        {
            var settings = AppSettings.Load();
            using var dlg = new SchemesDialog(CursorInstaller.DefaultStoreDir, settings, true);
            dlg.CreateControl();
            dlg.Size = new Size(640, 600);
            dlg.PerformLayout();
            Application.DoEvents();
            int cards = 0, withImage = 0;
            CountCards(dlg, ref cards, ref withImage);
            using var sw = new StreamWriter("cardsdump.txt", append: false, Encoding.UTF8);
            DumpControl(dlg, 0, sw);
            bool ok = cards >= settings.ExcludedFromRotation.Count && cards > 0 && withImage > 0;
            Console.WriteLine($"{(ok ? "OK   " : "FALLO")} tarjetas={cards} conIcono={withImage}");

            // Selector: 60 esquemas + tarjeta Aleatorio, sin botones por tarjeta.
            using var pick = new SchemePickerDialog(SchemeStore.GetSchemeNames(), "", settings, true);
            pick.CreateControl();
            pick.Size = new Size(640, 560);
            pick.PerformLayout();
            Application.DoEvents();
            int pcards = 0, pbuttons = 0;
            CountPicker(pick, ref pcards, ref pbuttons);
            bool pok = pcards == cards + 1 && pbuttons == 0;
            ok &= pok;
            Console.WriteLine($"{(pok ? "OK   " : "FALLO")} selector={pcards} botonesPorTarjeta={pbuttons}");

            // Toggle in-situ: misma tarjeta, sin reconstruir, estado flip + restore.
            var exclBackup = settings.ExcludedFromRotation.ToList();
            try
            {
                using var dlg2 = new SchemesDialog(CursorInstaller.DefaultStoreDir, settings, true);
                dlg2.CreateControl();
                // Visible de verdad: Control.Visible exige cadena visible
                // (headless también: parpadea un instante en el test).
                dlg2.Show();
                Application.DoEvents();
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                var flow = (FlowLayoutPanel)dlg2.GetType().GetField("_flow", flags).GetValue(dlg2);
                int beforeCount = flow.Controls.Count;
                var card = flow.Controls[0];
                string cardName = (string)card.Tag;
                bool beforeExcl = settings.ExcludedFromRotation.Contains(cardName);
                var toggle = dlg2.GetType().GetMethod("ToggleCard", flags);
                toggle.Invoke(dlg2, new object[] { cardName });
                Control cardAfter = null;
                foreach (Control c in flow.Controls)
                    if (Equals(c.Tag, cardName)) cardAfter = c;
                bool afterExcl = settings.ExcludedFromRotation.Contains(cardName);
                bool okToggle = flow.Controls.Count == beforeCount
                    && ReferenceEquals(card, cardAfter)
                    && afterExcl == !beforeExcl;
                toggle.Invoke(dlg2, new object[] { cardName }); // restore
                ok &= okToggle;
                Console.WriteLine($"{(okToggle ? "OK   " : "FALLO")} toggle-in-situ: mismaTarjeta={ReferenceEquals(card, cardAfter)}");

                // Batch marcar/desmarcar: tampoco reconstruye (mismas refs).
                var refsBefore = flow.Controls.Cast<Control>().ToList();
                var batch = dlg2.GetType().GetMethod("SetAllChecked", flags);
                batch.Invoke(dlg2, new object[] { false });
                var refsAfter = flow.Controls.Cast<Control>().ToList();
                bool allOut = settings.ExcludedFromRotation.Count >= flow.Controls.Count;
                batch.Invoke(dlg2, new object[] { true });
                bool okBatch = refsBefore.SequenceEqual(refsAfter) && allOut;
                ok &= okBatch;
                Console.WriteLine($"{(okBatch ? "OK   " : "FALLO")} batch-in-situ: mismasTarjetas={refsBefore.SequenceEqual(refsAfter)}");

                // Marcos animados: FlowFramePanel, Animated == en-rotación, paleta 2+.
                int frames = 0, animOk = 0, palOk = 0;
                foreach (Control cd in flow.Controls)
                {
                    if (cd.GetType().Name != "FlowFramePanel") continue;
                    frames++;
                    bool anim = (bool)cd.GetType().GetProperty("Animated").GetValue(cd);
                    string nm = (string)cd.Tag;
                    bool shouldAnim = !settings.ExcludedFromRotation.Contains(nm);
                    if (anim == shouldAnim) animOk++;
                    var pal = cd.GetType().GetField("_palette",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                        .GetValue(cd) as System.Collections.IList;
                    if (pal is not null && pal.Count >= 2) palOk++;
                }
                bool okFrames = frames == flow.Controls.Count && animOk == frames && palOk == frames;
                ok &= okFrames;
                Console.WriteLine($"{(okFrames ? "OK   " : "FALLO")} marcos: frames={frames} animOk={animOk} palOk={palOk}");

                // Marco estático en degradado: dos renders idénticos, con
                // píxeles de ambos colores de la paleta en el borde.
                var ffType = Type.GetType("CursorRotator.UI.FlowFramePanel, CursorRotator");
                using var frame = (Control)Activator.CreateInstance(ffType);
                frame.Size = new Size(100, 100);
                frame.BackColor = Color.Black;
                ffType.GetMethod("SetPalette").Invoke(frame,
                    new object[] { new List<Color> { Color.Red, Color.Blue } });
                ffType.GetProperty("Animated").SetValue(frame, true);
                frame.CreateControl();
                static long BorderColorCount(Control w, Func<Color, bool> pred)
                {
                    using var bmp = new Bitmap(w.Width, w.Height);
                    w.DrawToBitmap(bmp, new Rectangle(0, 0, w.Width, w.Height));
                    long n = 0;
                    for (int y = 0; y < 6; y++)
                        for (int x = 0; x < w.Width; x++)
                            if (pred(bmp.GetPixel(x, y))) n++;
                    for (int y = 0; y < w.Height; y++)
                        for (int x = 0; x < 6; x++)
                            if (pred(bmp.GetPixel(x, y))) n++;
                    return n;
                }
                long reds = BorderColorCount(frame, c => c.R > 150 && c.R > c.B + 40);
                long blues = BorderColorCount(frame, c => c.B > 150 && c.B > c.R + 40);
                bool huesOk = reds > 10 && blues > 10;
                // Órbita: dos renders separados deben diferir (movimiento).
                bool orbitOk;
                using (var a = new Bitmap(frame.Width, frame.Height))
                using (var b = new Bitmap(frame.Width, frame.Height))
                {
                    frame.DrawToBitmap(a, new Rectangle(0, 0, frame.Width, frame.Height));
                    var swO = System.Diagnostics.Stopwatch.StartNew();
                    while (swO.ElapsedMilliseconds < 600)
                    {
                        Application.DoEvents();
                        Thread.Sleep(50);
                    }
                    frame.DrawToBitmap(b, new Rectangle(0, 0, frame.Width, frame.Height));
                    long diff = 0;
                    for (int y = 0; y < a.Height; y += 2)
                        for (int x = 0; x < a.Width; x += 2)
                            if (a.GetPixel(x, y) != b.GetPixel(x, y)) diff++;
                    orbitOk = diff > 20;
                    Console.WriteLine($"  [dato] pixelesMovidos={diff}");
                }
                bool okFrame = huesOk && orbitOk;
                ok &= okFrame;
                Console.WriteLine($"{(okFrame ? "OK   " : "FALLO")} marco-orbita: rojos={reds} azules={blues} orbita={orbitOk}");

                // Sentido horario: el rojo va de la izquierda hacia arriba.
                static (double X, double Y) RedCentroid(Bitmap bmp)
                {
                    long sx = 0, sy = 0, n = 0;
                    for (int y = 0; y < bmp.Height; y++)
                        for (int x = 0; x < bmp.Width; x++)
                        {
                            var c = bmp.GetPixel(x, y);
                            if (c.R > 150 && c.R > c.B + 40) { sx += x; sy += y; n++; }
                        }
                    return n == 0 ? (-1, -1) : (sx / (double)n, sy / (double)n);
                }
                var animType = Type.GetType("CursorRotator.UI.FlowAnimator, CursorRotator");
                var phaseProp = animType.GetProperty("Phase");
                float p0 = (float)phaseProp.GetValue(null);
                using (var fa = new Bitmap(frame.Width, frame.Height))
                {
                    frame.DrawToBitmap(fa, new Rectangle(0, 0, frame.Width, frame.Height));
                    try { fa.Save("orbit0.png"); } catch { }
                    var (ax, ay) = RedCentroid(fa);
                    var swO2 = System.Diagnostics.Stopwatch.StartNew();
                    while ((float)phaseProp.GetValue(null) - p0 < 0.2f && swO2.ElapsedMilliseconds < 8000)
                    {
                        Application.DoEvents();
                        Thread.Sleep(50);
                    }
                    using var fb = new Bitmap(frame.Width, frame.Height);
                    frame.DrawToBitmap(fb, new Rectangle(0, 0, frame.Width, frame.Height));
                    try { fb.Save("orbit1.png"); } catch { }
                    var (bx, by) = RedCentroid(fb);
                    bool okDir = ax >= 0 && bx >= 0 && bx > ax && by < ay;
                    ok &= okDir;
                    Console.WriteLine($"{(okDir ? "OK   " : "FALLO")} sentido-horario: rojo=({ax:0},{ay:0})→({bx:0},{by:0})");
                }

                // Toast: aparece y se autocierra (~1.8 s).
                Toast.ShowToast(dlg2, $"Cursor \"{cardName}\" activado", true);
                int open1 = 0, toastW = 0;
                bool topRight = false;
                int ownerTop = dlg2.PointToScreen(System.Drawing.Point.Empty).Y;
                foreach (Form f in Application.OpenForms)
                {
                    if (f.GetType().Name != "Toast") continue;
                    open1++;
                    toastW = f.Width;
                    topRight = (f.Top - ownerTop) < dlg2.Height / 2;
                }
                // Espera bombeando mensajes (el timer del toast vive del pump,
                // como en la app real con Application.Run). Con pump grueso
                // cada tick tarda ~100 ms: margen de 6 s.
                var sw2 = System.Diagnostics.Stopwatch.StartNew();
                while (sw2.ElapsedMilliseconds < 6000)
                {
                    Application.DoEvents();
                    Thread.Sleep(50);
                }
                int open2 = 0;
                foreach (Form f in Application.OpenForms)
                    if (f.GetType().Name == "Toast") open2++;
                bool okToast = open1 >= 1 && open2 == 0 && topRight && toastW < 300 && toastW >= 200;
                ok &= okToast;
                Console.WriteLine($"{(okToast ? "OK   " : "FALLO")} toast: abierto={open1} arriba={topRight} ancho={toastW} autocerrado={open2 == 0}");

                // Filtro sin reconstruir: mismas tarjetas, menos visibles.
                var txtSearch = (TextBox)dlg2.GetType().GetField("_txtSearch", flags).GetValue(dlg2);
                var refsBeforeFilter = flow.Controls.Cast<Control>().ToList();
                txtSearch.Text = "acheron";
                Application.DoEvents();
                int visCount = flow.Controls.Cast<Control>().Count(c => c.Visible);
                int totalCount = flow.Controls.Count;
                bool sameRefs = refsBeforeFilter.SequenceEqual(flow.Controls.Cast<Control>().ToList());
                bool okFilter = sameRefs && totalCount == refsBeforeFilter.Count
                    && visCount >= 1 && visCount < totalCount;
                ok &= okFilter;
                Console.WriteLine($"{(okFilter ? "OK   " : "FALLO")} filtro: visibles={visCount}/{totalCount} sinReconstruir={sameRefs}");
                txtSearch.Text = "";
                Application.DoEvents();

                // Actualizar: tampoco reconstruye (mismas refs, mismo conteo).
                var refsBeforeSync = flow.Controls.Cast<Control>().ToList();
                var refresh = dlg2.GetType().GetMethod("RefreshList", flags);
                refresh.Invoke(dlg2, null);
                Application.DoEvents();
                bool okSync = refsBeforeSync.SequenceEqual(flow.Controls.Cast<Control>().ToList())
                    && flow.Controls.Count == refsBeforeSync.Count;
                ok &= okSync;
                Console.WriteLine($"{(okSync ? "OK   " : "FALLO")} actualizar-in-situ: mismasTarjetas={okSync}");
            Console.WriteLine(ok ? "=== ALL PASS ===" : "=== FAIL ===");
            return ok ? 0 : 1;
        }
        finally
        {
            settings.ExcludedFromRotation.Clear();
            settings.ExcludedFromRotation.AddRange(exclBackup);
            settings.Save();
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine("FALLO: " + ex);
        return 1;
    }
}

    private static void CountCards(Control c, ref int cards, ref int withImage)
    {
        foreach (Control child in c.Controls)
        {
            if (child is Panel p && p.Size == new Size(164, 142))
            {
                cards++;
                if (HasImageDeep(p)) withImage++;
            }
            else CountCards(child, ref cards, ref withImage);
        }
    }

    private static bool HasImageDeep(Control c)
    {
        if (c is PictureBox pb && pb.Image is not null) return true;
        foreach (Control child in c.Controls)
            if (HasImageDeep(child)) return true;
        return false;
    }

    private static void CountPicker(Control c, ref int cards, ref int buttons)
    {
        foreach (Control child in c.Controls)
        {
            if (child is Panel p && p.Size == new Size(150, 128))
            {
                cards++;
                foreach (Control cc in p.Controls)
                    if (cc is Button) buttons++;
            }
            CountPicker(child, ref cards, ref buttons);
        }
    }

    /// <summary>
    /// Lógica de versiones (pura, sin red) + ruta amable sin internet.
    /// </summary>
    private static int UpdateTest(string repo)
    {
        bool ok = true;
        void Check(string n, bool cond)
        {
            ok &= cond;
            Console.WriteLine($"{(cond ? "ok  " : "DIF ")} {n}");
        }
        Check("nueva v2.6.0 > 2.5.0", UpdateChecker.IsNewer("v2.6.0", "2.5.0"));
        Check("igual no es nueva", !UpdateChecker.IsNewer("v2.5.0", "2.5.0"));
        Check("vieja no es nueva", !UpdateChecker.IsNewer("2.4.9", "2.5.0"));
        Check("major nuevo", UpdateChecker.IsNewer("v3", "2.5.0"));
        Check("basura no es nueva", !UpdateChecker.IsNewer("abc", "2.5.0"));
        var info = UpdateChecker.CheckAsync(repo).GetAwaiter().GetResult();
        Check($"repo '{repo}' sin crash (hasUpdate={info.HasUpdate})", true);
        Console.WriteLine($"actual={UpdateChecker.CurrentVersion}");

        // Script de auto-reemplazo: se genera, contiene el flujo y NO se ejecuta.
        string tmpBat = Path.Combine(Path.GetTempPath(),
            "CursorRotator_Test_" + Guid.NewGuid().ToString("N")[..8] + ".bat");
        string script = AutoUpdater.GenerateUpdateScript(
            @"C:\Tmp\Ext", @"A:\App", tmpBat, @"C:\Tmp\Upd.zip");
        string bat = File.ReadAllText(script);
        Check("bat-timeout", bat.Contains("timeout /t 2"));
        Check("bat-xcopy", bat.Contains("xcopy /Y /E /C")
            && bat.Contains(@"""C:\Tmp\Ext\*""") && bat.Contains(@"""A:\App\"""));
        Check("bat-rearranque", bat.Contains("start \"\"")
            && bat.Contains(Path.Combine(@"A:\App", "CursorRotator.exe")));
        Check("bat-limpieza", bat.Contains("rmdir /S /Q") && bat.Contains("del \"%~f0\""));
        try { File.Delete(tmpBat); } catch { }
        Console.WriteLine(ok ? "=== ALL PASS ===" : "=== FAIL ===");
        return ok ? 0 : 1;
    }

    /// <summary>
    /// Pixel-Perfect Tracking sin hardware: matemática del mapper con un
    /// escritorio virtual sintético (ultrawide + secundario a la izquierda)
    /// y parser Raw Input con buffers RAWINPUT fabricados (absoluto,
    /// relativo y no-ratón).
    /// </summary>
    private static int MappingTest()
    {
        bool ok = true;
        void Check(string n, bool cond, string detail = "")
        {
            ok &= cond;
            Console.WriteLine($"{(cond ? "ok  " : "DIF ")} {n} {detail}");
        }

        // Ultrawide 5120×1440 + secundario 1920 a la izquierda (origen -1920).
        var vs = new Rectangle(-1920, 0, 7040, 1440);
        var primary = new Rectangle(0, 0, 5120, 1440);

        var c = CursorPointMapper.MapAbsoluteToScreen(32768, 32768, true, vs, primary);
        Check("absoluto-centro", c.X == 1600 && c.Y == 720, $"=({c.X},{c.Y})");
        var o = CursorPointMapper.MapAbsoluteToScreen(0, 0, true, vs, primary);
        Check("absoluto-origen", o.X == -1920 && o.Y == 0, $"=({o.X},{o.Y})");
        var f = CursorPointMapper.MapAbsoluteToScreen(65535, 65535, true, vs, primary);
        Check("absoluto-fin", f.X == 5120 && f.Y == 1440, $"=({f.X},{f.Y})");
        var p = CursorPointMapper.MapAbsoluteToScreen(0, 0, false, vs, primary);
        Check("absoluto-primaria", p.X == 0 && p.Y == 0, $"=({p.X},{p.Y})");
        var l = CursorPointMapper.ToLocal(new Point(1600, 720), vs);
        Check("pantalla→local", l.X == 3520 && l.Y == 720, $"=({l.X},{l.Y})");
        var t = CursorPointMapper.ToLayerOffset(100, 50, 1.5f);
        Check("offset-layer", t.X == -150f && t.Y == -75f, $"=({t.X},{t.Y})");

        // Núcleo puro con escritorio sintético (sin P/Invoke).
        bool gotA = RawInputMouse.MapRawMouse(0x0003, 0, 0, vs, primary, out var pa);
        Check("raw-absoluto", gotA && pa.X == -1920 && pa.Y == 0, $"=({pa.X},{pa.Y})");
        bool gotF = RawInputMouse.MapRawMouse(0x0003, 65535, 65535, vs, primary, out var pf);
        Check("raw-fin", gotF && pf.X == 5120 && pf.Y == 1440, $"=({pf.X},{pf.Y})");
        Check("raw-relativo-rechazado",
            !RawInputMouse.MapRawMouse(0x0000, 50, 60, vs, primary, out _));
        // Handle falso: degradación elegante, sin excepciones.
        nint fake = Marshal.AllocHGlobal(52);
        try
        {
            Check("handle-falso-abs",
                !RawInputMouse.TryGetAbsolutePosition(fake, out _, out _));
            Check("handle-falso-deltas", !RawInputMouse.HandleMessage(fake));
        }
        finally { Marshal.FreeHGlobal(fake); }

        Console.WriteLine(ok ? "=== ALL PASS ===" : "=== FAIL ===");
        return ok ? 0 : 1;
    }

    private static void DumpControl(Control c, int depth, StreamWriter sw)
    {
        string text = (c.Text ?? "").Replace("\r", " ").Replace("\n", " ");
        if (text.Length > 44) text = text[..44];
        sw.WriteLine($"{new string(' ', depth)}[{c.GetType().Name}] '{text}' " +
            $"loc={c.Location} size={c.Size} vis={c.Visible} " +
            $"back=#{c.BackColor.R:X2}{c.BackColor.G:X2}{c.BackColor.B:X2} " +
            $"dock={c.Dock} anchor={c.Anchor}");
        foreach (Control child in c.Controls)
            DumpControl(child, depth + 1, sw);
    }

    /// <summary>
    /// Diagnóstico del scroll de estelas en ventana MOSTRADA y redimensionada:
    /// vuelca AutoScrollPosition, DisplayRectangle, VScroll y foco real.
    /// </summary>
    private static int DiagScroll()
    {
        MainForm form = null;
        var shown = new ManualResetEvent(false);
        var ui = new Thread(() =>
        {
            form = new MainForm(false);
            form.Shown += (_, _) => shown.Set();
            Application.Run(form);
        });
        ui.SetApartmentState(ApartmentState.STA);
        ui.Start();
        if (!shown.WaitOne(15000)) { Console.WriteLine("TIMEOUT esperando Shown"); return 2; }
        Thread.Sleep(500);
        string report = "";
        form.Invoke(() =>
        {
            form.Size = new Size(680, 600);
            form.PerformLayout();
            Application.DoEvents();
            Thread.Sleep(400);
            var sb = new StringBuilder();
            FindScrollPanel(form, sb, 0);
            var focused = FindFocused(form);
            sb.AppendLine("FOCUSED=" + (focused is null
                ? "(ninguno)"
                : focused.GetType().Name + " '" + focused.Text + "'"));
            report = sb.ToString();
        });
        Console.WriteLine(report);
        form.Invoke(() => Application.ExitThread());
        ui.Join(5000);
        return 0;
    }

    private static void FindScrollPanel(Control c, StringBuilder sb, int depth)
    {
        if (c is Panel p && p.AutoScroll)
        {
            sb.AppendLine($"{new string(' ', depth)}[AutoScrollPanel] client={p.ClientSize} " +
                $"display={p.DisplayRectangle.Size} pos={p.AutoScrollPosition} " +
                $"vVis={p.VerticalScroll.Visible} vVal={p.VerticalScroll.Value} " +
                $"vMax={p.VerticalScroll.Maximum} vLarge={p.VerticalScroll.LargeChange}");
            foreach (Control child in p.Controls)
            {
                sb.AppendLine($"{new string(' ', depth + 1)}child [{child.GetType().Name}] " +
                    $"'{Trim(child.Text)}' loc={child.Location} size={child.Size} vis={child.Visible}");
            }
        }
        if (c is TableLayoutPanel t && c.Controls.Count > 8)
        {
            sb.AppendLine($"{new string(' ', depth)}[Table] size={t.Size} pref={t.PreferredSize} loc={t.Location} rows={t.RowCount}");
            try
            {
                sb.AppendLine($"{new string(' ', depth)}rowHeights=[{string.Join(",", t.GetRowHeights())}]");
            }
            catch (Exception ex) { sb.AppendLine("rowHeights ERR " + ex.Message); }
        }
        foreach (Control child in c.Controls)
            FindScrollPanel(child, sb, depth + 1);
    }

    private static string Trim(string s)
    {
        s ??= "";
        s = s.Replace("\r", " ").Replace("\n", " ");
        return s.Length > 30 ? s[..30] : s;
    }

    private static Control FindFocused(Control root)
    {
        foreach (Control c in root.Controls)
        {
            if (c.Focused) return c;
            var f = FindFocused(c);
            if (f is not null) return f;
        }
        return root.Focused ? root : null;
    }

    private static int SchemesTest()
    {
        string store = CursorInstaller.DefaultStoreDir;
        string name = "zzTestDelete_" + Guid.NewGuid().ToString("N")[..8];
        string dir = Path.Combine(store, name);
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "x.cur"), "test");
            SchemeStore.SetScheme(name, [Path.Combine(dir, "x.cur")]);
            bool created = SchemeStore.Exists(name);
            bool deleted = SchemeStore.DeleteScheme(name, store);
            bool goneReg = !SchemeStore.Exists(name);
            bool goneDir = !Directory.Exists(dir);
            bool ok = created && deleted && goneReg && goneDir;
            Console.WriteLine((ok ? "OK   " : "FALLO") +
                $" Schemes alta/borrado (registro+carpeta): {name}");
            Console.WriteLine(ok ? "=== ALL PASS ===" : "=== FAIL ===");
            return ok ? 0 : 1;
        }
        finally
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Control Panel\Cursors\Schemes", writable: true);
                key?.DeleteValue(name, throwOnMissingValue: false);
                if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            }
            catch { }
        }
    }

    /// <summary>
    /// Prueba sin escribir nada: parsea todos los .inf y verifica que cada
    /// archivo referenciado exista. Ideal para CI / verificación local.
    /// </summary>
    private static int SelfTest(string packsDir)
    {
        var infs = Directory.EnumerateFiles(packsDir, "*.inf", SearchOption.AllDirectories)
            .Where(f => !CursorInstaller.IsExcluded(f)).ToList();
        int ok = 0;
        var fails = new List<string>();
        foreach (var inf in infs)
        {
            try
            {
                var parsed = InfParser.ParseFile(inf, packsDir);
                // Anti-contaminación: todo fuente debe vivir bajo el dir del .inf.
                string infDir = Path.GetDirectoryName(inf)! + Path.DirectorySeparatorChar;
                string outside = parsed.SourceFiles.FirstOrDefault(s =>
                    !s.StartsWith(infDir, StringComparison.OrdinalIgnoreCase));
                if (outside is not null)
                    throw new InvalidOperationException(
                        $"contaminación entre packs: '{outside}' fuera de '{infDir}'");
                Console.WriteLine($"OK: {parsed.SchemeName} ({parsed.SourceFiles.Count})");
                ok++;
            }
            catch (Exception ex)
            {
                fails.Add($"{inf} :: {ex.Message}");
                Console.WriteLine($"FALLO: {inf} :: {ex.Message}");
            }
        }
        Console.WriteLine($"=== SELF-TEST OK={ok} FALLOS={fails.Count} ===");
        return fails.Count == 0 ? 0 : 1;
    }

    /// <summary>
    /// Prueba el motor de estelas sin ventana: simula movimiento, renderiza
    /// y verifica que haya píxeles y que todo se apague en reposo.
    /// </summary>
    private static int TrailTest()
    {
        bool allOk = true;
        foreach (TrailPreset preset in Enum.GetValues<TrailPreset>())
        {
            var engine = new TrailEngine();
            engine.Options.Preset = preset;
            using var bmp = new System.Drawing.Bitmap(800, 600,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using var g = System.Drawing.Graphics.FromImage(bmp);
            var sw = System.Diagnostics.Stopwatch.StartNew();

            // Fase 1: 90 frames moviéndose en diagonal.
            for (int i = 0; i < 90; i++)
            {
                engine.Update(100 + i * 6, 100 + i * 4, 1f / 60);
                g.Clear(System.Drawing.Color.Transparent);
                engine.Render(g);
            }
            long movePixels = CountAlphaPixels(bmp);
            int alive = engine.AliveCount;

            // Fase 2: mínimo 60 frames quieto (los Ripples nacen al detenerse)
            // y luego hasta apagarse (máx. 300). Se registra el pico.
            int stillFrames = 0;
            long stillPeak = 0;
            while (stillFrames < 300 && (stillFrames < 60 || engine.HasLive))
            {
                engine.Update(640, 436, 1f / 60);
                stillFrames++;
                g.Clear(System.Drawing.Color.Transparent);
                engine.Render(g);
                stillPeak = Math.Max(stillPeak, CountAlphaPixels(bmp));
            }
            g.Clear(System.Drawing.Color.Transparent);
            engine.Render(g);
            long restPixels = CountAlphaPixels(bmp);
            sw.Stop();

            // Neón/DNA/Tesla/Ribbon no usan partículas (usan historial).
            bool historyBased = preset is TrailPreset.LineaNeon or TrailPreset.Dna
                or TrailPreset.Tesla or TrailPreset.LightRibbon;
            bool aliveOk = historyBased
                ? alive == 0
                : alive is >= 0 and <= 60;
            bool ok = Math.Max(movePixels, stillPeak) > 200 && aliveOk
                      && !engine.HasLive && restPixels == 0;
            allOk &= ok;
            Console.WriteLine($"{(ok ? "OK  " : "FALLO")} {preset,-10} " +
                $"pxMov={movePixels,7} vivos={alive,3} apagadoEn={stillFrames,3} frames " +
                $"pxReposo={restPixels} t={sw.ElapsedMilliseconds,4} ms");
        }
        Console.WriteLine(allOk ? "=== TRAIL-TEST PASS ===" : "=== TRAIL-TEST FAIL ===");

        // Origen punta vs cola: el punto de emisión debe desplazarse (+16,+20).
        var eng = new TrailEngine();
        eng.Update(100, 100, 1f / 60);
        eng.Update(140, 140, 1f / 60);
        var tip = eng.LastEmitPoint;
        eng.Options.Origin = TrailOrigin.Cola;
        eng.Update(140, 140, 1f / 60);
        eng.Update(180, 180, 1f / 60);
        var tail = eng.LastEmitPoint;
        bool originOk = Math.Abs(tip.X - 140) < 0.01 && Math.Abs(tip.Y - 140) < 0.01
                     && Math.Abs(tail.X - 196) < 0.01 && Math.Abs(tail.Y - 200) < 0.01;
        Console.WriteLine((originOk ? "OK   " : "FALLO") +
            $" Origen punta=({tip.X},{tip.Y}) cola=({tail.X},{tail.Y})");
        allOk &= originOk;

        // Giros rápidos: círculo a alta velocidad, el trazo no debe cortarse.
        foreach (var preset in new[] { TrailPreset.Cometa, TrailPreset.LineaNeon })
        {
            var circle = new TrailEngine();
            circle.Options.Preset = preset;
            using var bmp2 = new System.Drawing.Bitmap(800, 600,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using var g2 = System.Drawing.Graphics.FromImage(bmp2);
            for (int i = 0; i < 120; i++)
            {
                float a = i / 120f * MathF.PI * 4; // dos vueltas rápidas
                circle.PushPoint(400 + MathF.Cos(a) * 150, 300 + MathF.Sin(a) * 150);
                circle.Advance(1f / 120);
                g2.Clear(System.Drawing.Color.Transparent);
                circle.Render(g2);
            }
            long px = CountAlphaPixels(bmp2);
            bool circleOk = px > 1500;
            allOk &= circleOk;
            Console.WriteLine((circleOk ? "OK   " : "FALLO") +
                $" Giro-rapido {preset,-10} px={px}");
        }

        // Color heredado: debe dominar los píxeles renderizados en TODOS los presets.
        foreach (TrailPreset preset in Enum.GetValues<TrailPreset>())
        {
            var tinted = new TrailEngine();
            tinted.Options.Preset = preset;
            tinted.Options.UseCursorColor = true;
            tinted.Options.CursorColor = Color.FromArgb(255, 30, 30); // rojo puro
            using var bmp3 = new System.Drawing.Bitmap(800, 600,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using var g3 = System.Drawing.Graphics.FromImage(bmp3);
            for (int i = 0; i < 60; i++)
            {
                tinted.Update(200 + i * 6, 200 + i * 4, 1f / 60);
                g3.Clear(System.Drawing.Color.Transparent);
                tinted.Render(g3);
            }
            if (preset == TrailPreset.Ripples)
            {
                // Los anillos nacen al detenerse: fase de parada + fundido.
                for (int i = 0; i < 40; i++)
                {
                    tinted.Update(560, 440, 1f / 60);
                    g3.Clear(System.Drawing.Color.Transparent);
                    tinted.Render(g3);
                }
            }
            var (ar, ag, ab, n) = AvgColor(bmp3);
            // ADN usa complementario y DarkFog es negro por diseño: solo presencia.
            // Umbral 150 (no 200): el ponderado por alfa castiga parpadeos/jitter.
            bool tintOk = preset is TrailPreset.Dna or TrailPreset.DarkFog
                ? n > 150
                : n > 150 && ar > ag + 20 && ar > ab + 20;
            allOk &= tintOk;
            Console.WriteLine((tintOk ? "OK   " : "FALLO") +
                $" Tinte-rojo {preset,-13} px={n,6} avg=({ar},{ag},{ab})");
        }
        try
        {
            using var hook = new MouseHook();
            hook.Install();
            bool installed = hook.IsInstalled;
            hook.Uninstall();
            bool uninstalled = !hook.IsInstalled;
            bool hookOk = installed && uninstalled;
            allOk &= hookOk;
            Console.WriteLine((hookOk ? "OK   " : "FALLO") + " MouseHook install/uninstall");
        }
        catch (Exception ex)
        {
            allOk = false;
            Console.WriteLine("FALLO MouseHook: " + ex.Message);
        }

        // Los 6 motores: activar, alimentar, avanzar, desactivar, renderizar.
        foreach (TrailCaptureMode mode in Enum.GetValues<TrailCaptureMode>())
        {
            try
            {
                using var core = new TrailEngine();
                core.Options.Preset = TrailPreset.Cometa;
                using var motor = TrailEngineFactory.Create(mode);
                bool modeOk = motor.Mode == mode;
                motor.Activate(core);
                if (mode == TrailCaptureMode.Inertial)
                    modeOk &= core.BounceEdges && core.GravityY > 0 && core.MomentumBoost > 1;
                if (motor is AsyncEngine ae)
                {
                    ae.Start(core, 360, 1f);
                    for (int i = 0; i < 30; i++)
                        ae.Push(core, 200 + i * 6, 200 + i * 4);
                    modeOk &= ae.WaitFrame(3000);
                    using var bmp4 = new System.Drawing.Bitmap(800, 600,
                        System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    using var g4 = System.Drawing.Graphics.FromImage(bmp4);
                    ae.Blit(frame =>
                    {
                        g4.Clear(System.Drawing.Color.Transparent);
                        g4.DrawImageUnscaled(frame, 0, 0);
                    });
                    modeOk &= CountAlphaPixels(bmp4) > 200;
                    motor.Deactivate(core);
                }
                else if (mode == TrailCaptureMode.Predictive)
                {
                    // Movimiento acelerado: la predicción debe anticiparse.
                    for (int i = 0; i < 10; i++)
                        motor.Push(core, 100 + i * i * 0.8f, 100 + i * 4);
                    core.Advance(1f / 60);
                    var last = core.LastEmitPoint;
                    float lead = MathF.Sqrt((last.X - 164.8f) * (last.X - 164.8f));
                    modeOk &= lead > 0.5f;
                    motor.Deactivate(core);
                }
                else
                {
                    for (int i = 0; i < 30; i++)
                        motor.Push(core, 200 + i * 6, 200 + i * 4);
                    core.Advance(1f / 60);
                    using var bmp5 = new System.Drawing.Bitmap(800, 600,
                        System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    using var g5 = System.Drawing.Graphics.FromImage(bmp5);
                    g5.Clear(System.Drawing.Color.Transparent);
                    core.Render(g5);
                    modeOk &= CountAlphaPixels(bmp5) > 200;
                    motor.Deactivate(core);
                    modeOk &= !core.BounceEdges && core.GravityY == 0 && core.MomentumBoost == 1f;
                }
                allOk &= modeOk;
                Console.WriteLine((modeOk ? "OK   " : "FALLO") + $" Motor {mode}");
            }
            catch (Exception ex)
            {
                allOk = false;
                Console.WriteLine($"FALLO Motor {mode}: " + ex.Message);
            }
        }

        // Densidad: 0.1× debe emitir claramente menos que 3.0× (por píxeles,
        // pues Confeti usa lista propia y AliveCount solo cuenta compartidas).
        {
            static long RunDensity(float density)
            {
                var e = new TrailEngine();
                e.Options.Preset = TrailPreset.Confetti;
                e.Options.Density = density;
                using var bmp = new System.Drawing.Bitmap(800, 600,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using var g = System.Drawing.Graphics.FromImage(bmp);
                for (int i = 0; i < 60; i++)
                {
                    e.Update(200 + i * 6, 200 + i * 4, 1f / 60);
                    g.Clear(System.Drawing.Color.Transparent);
                    e.Render(g);
                }
                return CountAlphaPixels(bmp);
            }
            long low = RunDensity(0.1f);
            long mid = RunDensity(1f);
            long high = RunDensity(3f);
            bool densityOk = low < mid && mid <= high;
            allOk &= densityOk;
            Console.WriteLine((densityOk ? "OK   " : "FALLO") +
                $" Densidad 0.1×={low}px 1.0×={mid}px 3.0×={high}px");
        }
        // Frame pacing: matemática pura + tasa real del monitor.
        {
            bool paceOk = true;
            paceOk &= FramePacer.GetFrameBudgetMs(new Point(0, 0), 60) == 16;
            paceOk &= FramePacer.GetFrameBudgetMs(new Point(0, 0), 144) == 6;
            paceOk &= FramePacer.GetFrameBudgetMs(new Point(0, 0), 240) == 4;
            paceOk &= FramePacer.GetFrameBudgetMs(new Point(0, 0), -1) == 4;
            int hz = DisplayInfo.GetRefreshRate(new Point(10, 10));
            paceOk &= hz is >= 30 and <= 480;
            int auto = FramePacer.GetFrameBudgetMs(new Point(10, 10), 0);
            paceOk &= auto == Math.Max(1, 1000 / Math.Clamp(hz, 30, 240));
            allOk &= paceOk;
            Console.WriteLine((paceOk ? "OK   " : "FALLO") +
                $" Pacing 60→16ms 144→6ms 240→4ms uncapped→4ms monitor={hz}Hz auto→{auto}ms");
        }

        // 20 efectos de clic: disparo, píxeles y fundido total.
        {
            bool clicksOk = true;
            var palette = new[] { Color.FromArgb(255, 40, 40), Color.FromArgb(40, 255, 40), Color.FromArgb(60, 60, 255) };
            for (int i = 0; i < ClickEffectFactory.Names.Length; i++)
            {
                using var fx = ClickEffectFactory.Create(i);
                using var bmp = new System.Drawing.Bitmap(800, 600,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using var g = System.Drawing.Graphics.FromImage(bmp);
                fx.Trigger(400, 300, palette);
                for (int f = 0; f < 20; f++)
                {
                    fx.Update(1f / 60);
                    g.Clear(System.Drawing.Color.Transparent);
                    fx.Draw(g);
                }
                long peak = CountAlphaPixels(bmp);
                int frames = 0;
                while (fx.HasActivity && frames < 300)
                {
                    fx.Update(1f / 60);
                    frames++;
                }
                g.Clear(System.Drawing.Color.Transparent);
                fx.Draw(g);
                long rest = CountAlphaPixels(bmp);
                bool ok = peak > 150 && !fx.HasActivity && rest == 0;
                clicksOk &= ok;
                if (!ok)
                    Console.WriteLine($"FALLO Clic {ClickEffectFactory.Names[i]} px={peak} resto={rest}");
            }
            // ClickEngine: selección + on/off + trigger global.
            using var engine = new ClickEngine();
            engine.SelectedIndex = 11;
            engine.Enabled = true;
            engine.Trigger(400, 300, palette);
            engine.Update(1f / 60);
            bool engOk = engine.HasActivity && engine.CurrentName.Contains("Supernova");
            engine.Enabled = false;
            engOk &= !engine.HasActivity;
            clicksOk &= engOk;
            Console.WriteLine((clicksOk ? "OK   " : "FALLO") + " 20 efectos de clic + ClickEngine");
            allOk &= clicksOk;
        }

        Console.WriteLine(allOk ? "=== ALL PASS ===" : "=== FAIL ===");
        return allOk ? 0 : 1;
    }

    private static (long R, long G, long B, long N) AvgColor(System.Drawing.Bitmap bmp)
    {
        var rect = new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height);
        var data = bmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try
        {
            int bytes = Math.Abs(data.Stride) * data.Height;
            var buf = new byte[bytes];
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0, buf, 0, bytes);
            long r = 0, g = 0, b = 0, n = 0;
            for (int i = 0; i < bytes; i += 4)
            {
                if (buf[i + 3] < 32) continue;
                // Promedio ponderado por alfa para no dejarse engañar por halos tenues.
                int a = buf[i + 3];
                r += buf[i + 2] * a; g += buf[i + 1] * a; b += buf[i] * a; n += a;
            }
            return n == 0 ? (0, 0, 0, 0) : (r / n, g / n, b / n, n / 255);
        }
        finally { bmp.UnlockBits(data); }
    }

    private static long CountAlphaPixels(System.Drawing.Bitmap bmp)
    {
        var rect = new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height);
        var data = bmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try
        {
            int bytes = Math.Abs(data.Stride) * data.Height;
            var buf = new byte[bytes];
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0, buf, 0, bytes);
            long n = 0;
            for (int i = 3; i < bytes; i += 4)
                if (buf[i] > 8) n++;
            return n;
        }
        finally { bmp.UnlockBits(data); }
    }
    /// <summary>
    /// Verifica el lector de color dominante sobre archivos .cur/.ani reales.
    /// </summary>
    private static int CursorColorTest(string dir)
    {
        var files = Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".cur", StringComparison.OrdinalIgnoreCase)
                     || f.EndsWith(".ani", StringComparison.OrdinalIgnoreCase))
            .ToList();
        int ok = 0, ninguno = 0;
        var fails = new List<string>();
        foreach (var f in files)
        {
            try
            {
                var c = CursorColorReader.GetDominantColor(f);
                if (c.HasValue) ok++;
                // null = ningún píxel supera los filtros estrictos (diseño, no error).
                else ninguno++;
            }
            catch (Exception ex) { fails.Add(f + " :: " + ex.Message); }
        }
        Console.WriteLine($"Archivos: {files.Count} | OK={ok} | SinColor(filtros)={ninguno} | EXCEPCIONES={fails.Count}");
        foreach (var f in fails.Take(10)) Console.WriteLine("FALLO: " + f);
        var arrow = CursorColorReader.GetActiveArrowColor();
        Console.WriteLine("Arrow activo: " + CursorColorReader.GetActiveArrowPath());
        Console.WriteLine("Color dominante Arrow: " + (arrow.HasValue
            ? $"#{arrow.Value.R:X2}{arrow.Value.G:X2}{arrow.Value.B:X2}" : "no detectado"));

        // Paleta top-3 sobre un archivo real colorido + derivada manual.
        {
            string colorful = files.FirstOrDefault(f =>
            {
                var p = CursorColorReader.GetPalette(f);
                return p.Length >= 2;
            });
            if (colorful is null)
            {
                fails.Add("paleta :: ningún archivo con 2+ colores");
            }
            else
            {
                var pal = CursorColorReader.GetPalette(colorful);
                bool palOk = pal.Length is >= 2 and <= 3 &&
                             pal.Distinct().Count() == pal.Length;
                Console.WriteLine($"Paleta ({System.IO.Path.GetFileName(colorful)}): " +
                    string.Join(" ", pal.Select(c => $"#{c.R:X2}{c.G:X2}{c.B:X2}")));
                if (!palOk) fails.Add("paleta :: top-3 inválida");
            }
            var derived = CursorColorReader.DerivePalette(Color.FromArgb(200, 100, 50));
            int b0 = derived[0].R + derived[0].G + derived[0].B;
            int b1 = derived[1].R + derived[1].G + derived[1].B;
            int b2 = derived[2].R + derived[2].G + derived[2].B;
            bool derOk = derived.Length == 3 &&
                         derived[0].ToArgb() == Color.FromArgb(200, 100, 50).ToArgb() &&
                         b1 > b0 && b0 > b2; // claro > base > oscuro
            Console.WriteLine((derOk ? "OK   " : "FALLO") + " Paleta derivada manual [base,claro,oscuro]");
            if (!derOk) fails.Add("paleta :: derivada manual inválida");
        }

        return fails.Count == 0 ? 0 : 1;
    }

    private static int HeadlessInstall(string source, bool includeStatic)
    {
        var installer = new CursorInstaller();
        var progress = new Progress<(int Percent, string Message)>(t =>
            Console.WriteLine($"[{t.Percent,3}%] {t.Message}"));
        var result = installer
            .InstallAsync(source, CursorInstaller.DefaultStoreDir, progress,
                CancellationToken.None, skipStatic: !includeStatic)
            .GetAwaiter().GetResult();
        Console.WriteLine($"=== INSTALADOS={result.Installed.Count} ERRORES={result.Errors.Count} OMITIDOS={result.Skipped.Count} ===");
        foreach (var e in result.Errors) Console.WriteLine("FALLO: " + e);
        return result.Errors.Count == 0 ? 0 : 1;
    }

    /// <summary>
    /// Prueba del flujo de variantes+duplicados con un pack sintético:
    /// 3 variantes (V1/V2/V3), V2 y V3 pre-registrados. Selección = todas;
    /// primer duplicado → Omitir todos. Debe instalar solo V1.
    /// Limpia registro y carpetas al terminar.
    /// </summary>
    private static int InstallTest()
    {
        string pack = Path.Combine(Path.GetTempPath(),
            "CursorRotator_VT_" + Guid.NewGuid().ToString("N")[..8]);
        var createdSchemes = new List<string>();
        try
        {
            string Inf(string dir, string scheme, string file)
            {
                Directory.CreateDirectory(dir);
                File.WriteAllBytes(Path.Combine(dir, file), [0x42, 0x4D]);
                string inf =
                    "[Version]\nsignature=\"$CHICAGO$\"\n" +
                    "[DefaultInstall]\nCopyFiles = Scheme.Cur\n" +
                    "[Scheme.Reg]\nHKCU,\"Control Panel\\Cursors\\Schemes\"," +
                    $"\"{scheme}\",,\"%10%\\Cursors\\zz\\{file}\"\n";
                string p = Path.Combine(dir, "install.inf");
                File.WriteAllText(p, inf);
                return p;
            }
            Inf(Path.Combine(pack, "V1"), "zzTestVariantA", "a.ani");
            Inf(Path.Combine(pack, "V2"), "zzTestVariantB", "b.ani");
            Inf(Path.Combine(pack, "V3"), "zzTestVariantC", "c.ani");
            // Pre-registra B y C para forzar duplicados.
            SchemeStore.SetScheme("zzTestVariantB", ["C:\\Windows\\Cursors\\zz\\b.ani"]);
            SchemeStore.SetScheme("zzTestVariantC", ["C:\\Windows\\Cursors\\zz\\c.ani"]);
            createdSchemes.AddRange(["zzTestVariantB", "zzTestVariantC"]);

            var installer = new CursorInstaller();
            int selectCalls = 0, confirmCalls = 0;
            var result = installer.InstallAsync(pack, CursorInstaller.DefaultStoreDir,
                null, CancellationToken.None, skipStatic: true,
                selectVariantsAsync: list =>
                {
                    selectCalls++;
                    Console.WriteLine($"candidatos={list.Count} " +
                        $"grupos={string.Join(",", list.Select(c => c.Group).Distinct())}");
                    return Task.FromResult(list);
                },
                confirmDuplicateAsync: name =>
                {
                    confirmCalls++;
                    Console.WriteLine($"duplicado: {name} → Omitir todos");
                    return Task.FromResult(CursorInstaller.DuplicateAction.SkipAll);
                }).GetAwaiter().GetResult();

            bool ok = selectCalls == 1
                && result.Installed.Count == 1 && result.Installed[0] == "zzTestVariantA"
                && result.Skipped.Count == 2 && result.Errors.Count == 0
                && SchemeStore.Exists("zzTestVariantA");
            Console.WriteLine($"{(ok ? "OK   " : "FALLO")} " +
                $"instalados=[{string.Join(",", result.Installed)}] " +
                $"omitidos=[{string.Join(",", result.Skipped)}] " +
                $"confirms={confirmCalls}");
            createdSchemes.Add("zzTestVariantA");
            Console.WriteLine(ok ? "=== ALL PASS ===" : "=== FAIL ===");
            return ok ? 0 : 1;
        }
        finally
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Control Panel\Cursors\Schemes", writable: true);
                foreach (var s in createdSchemes)
                    key?.DeleteValue(s, throwOnMissingValue: false);
                foreach (var s in createdSchemes)
                {
                    string d = Path.Combine(CursorInstaller.DefaultStoreDir, s);
                    if (Directory.Exists(d)) Directory.Delete(d, recursive: true);
                }
                if (Directory.Exists(pack)) Directory.Delete(pack, recursive: true);
            }
            catch { }
        }
    }

    /// <summary>
    /// Prueba del flujo MULTI-paquete: arma tres .zip sintéticos (dos con
    /// esquemas distintos y uno con un nombre repetido), los examina juntos
    /// (debe fusionar y desduplicar) y luego instala dos. Limpia registro,
    /// carpetas y temporales al terminar.
    /// </summary>
    private static int InstallManyTest()
    {
        string root = Path.Combine(Path.GetTempPath(),
            "CursorRotator_MT_" + Guid.NewGuid().ToString("N")[..8]);
        var created = new List<string>();
        try
        {
            Directory.CreateDirectory(root);

            static string MakePack(string dir, string name, string scheme, string file)
            {
                string sub = Path.Combine(dir, name);
                Directory.CreateDirectory(sub);
                File.WriteAllBytes(Path.Combine(sub, file), [0x42, 0x4D]);
                string inf = "[Version]\nsignature=\"$CHICAGO$\"\n" +
                    "[Scheme.Reg]\n" +
                    $"HKCU,\"Control Panel\\Cursors\\Schemes\",\"{scheme}\",," +
                    $"\"%10%\\Cursors\\zz\\{file}\"\n";
                File.WriteAllText(Path.Combine(sub, "install.inf"), inf);
                string zip = Path.Combine(dir, name + ".zip");
                System.IO.Compression.ZipFile.CreateFromDirectory(sub, zip);
                return zip;
            }

            string a = MakePack(root, "packA", "zzManyTestA", "a.ani");
            string b = MakePack(root, "packB", "zzManyTestB", "b.ani");
            string dup = MakePack(root, "packDup", "zzManyTestA", "a2.ani"); // repetido

            var installer = new CursorInstaller();
            var scan = installer.ScanManyAsync([a, b, dup], null, CancellationToken.None)
                .GetAwaiter().GetResult();
            int found = scan.Candidates.Count;
            bool merged = found == 2;
            bool dedupNote = scan.Errors.Any(e =>
                e.Contains("repetido", StringComparison.OrdinalIgnoreCase));
            CursorInstaller.CleanupStaging(scan.StagingDirs);

            var result = installer.InstallAsync([a, b], CursorInstaller.DefaultStoreDir,
                null, CancellationToken.None,
                selectVariantsAsync: list => Task.FromResult(list))
                .GetAwaiter().GetResult();
            created.AddRange(["zzManyTestA", "zzManyTestB"]);
            bool installed = result.Installed.Count == 2 && result.Errors.Count == 0;

            bool ok = merged && dedupNote && installed;
            Console.WriteLine($"{(ok ? "OK   " : "FALLO")} multi-paquete: " +
                $"candidatos={found} dedup={dedupNote} " +
                $"instalados=[{string.Join(",", result.Installed)}] " +
                $"errores={result.Errors.Count}");
            if (!ok)
                foreach (var e in scan.Errors.Concat(result.Errors).Take(10))
                    Console.WriteLine("FALLO: " + e);
            Console.WriteLine(ok ? "=== ALL PASS ===" : "=== FAIL ===");
            return ok ? 0 : 1;
        }
        finally
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Control Panel\Cursors\Schemes", writable: true);
                foreach (var s in created)
                    key?.DeleteValue(s, throwOnMissingValue: false);
                foreach (var s in created)
                {
                    string d = Path.Combine(CursorInstaller.DefaultStoreDir, s);
                    if (Directory.Exists(d)) Directory.Delete(d, recursive: true);
                }
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
            catch { }
        }
    }
}

