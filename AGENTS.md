# AGENTS.md — CursorRotator: contexto para agentes IA

Documento vivo de traspaso. Si continúas este proyecto en otro chat,
lee esto primero. Interfaz y mensajes siempre en **español**.

## 1. Qué es

App de escritorio Windows (C# WinForms, .NET 10, `WinExe`, single-file
autocontenida) para **instalar en 1 clic y rotar automáticamente**
esquemas de cursores. Sin UAC: todo por usuario
(`%LOCALAPPDATA%\Cursors`, `HKCU\Control Panel\Cursors`).
Extras: estelas de partículas (40 presets + dual trail), 20 efectos de
clic, splash, agenda (intervalo o varias horas/día con destino fijo o
aleatorio), galerías con tarjetas y marcos en degradado orbital.

## 2. Mapa del repo (`A:\CursorRotator\`)

```text
README.md            # manual de usuario (ES)
AGENTS.md            # este archivo (traspaso IA)
CONTRIBUTING.md      # guía del mantenedor (publicar releases)
LICENSE              # MIT | .gitignore (sin binarios ni zips)
Empaquetar.ps1       # publish Release → dist\ → arma paquete\
Publicar-Release.ps1 # paquete\ → dist\CursorRotator-vX.Y.Z.zip (para Releases)
assets\              # app.ico, icon-src.png, splash.jpg (embebidos)
dist\                # exe publicado   |  paquete\  # .exe + Cursores\ + LEEME.txt
src\
  CursorRotator.csproj   # net10.0-windows, Version 2.5.0, PublishSingleFile
  app.manifest           # asInvoker (sin UAC)
  Program.cs             # entrada GUI + modos CLI de diagnóstico
  Core\
    AppSettings.cs       # settings.json + auditoría + Save atómico
    SchemeStore.cs       # registro HKCU (leer/aplicar/borrar, sanitiza rutas)
    InfParser.cs         # .inf clásico y moderno ([Strings]); ParsedScheme
    CursorInstaller.cs   # InstallAsync por fases + variantes + duplicados
    CursorRotator.cs     # cola barajada, Peek/TakeNext, timer, agenda one-shot
    CursorImage.cs       # .cur/.ani → Bitmap (sin bloquear archivos)
    CursorColor.cs       # paleta top-3 del cursor (histograma)
    CursorPreviewCache.cs# caché LRU de previews (devuelve CLONES)
    NativeMethods.cs     # SPI_SETCURSORS, topmost, título oscuro
    MouseHook.cs         # WH_MOUSE_LL | TrailEngine.cs # coordinador
    TrailEngine.cs       # (Preset/Origin/Size/Life/Density/Color/Origin)
    Trails\              # ITrailStyle, Classic/Advanced(2)/ClickEffects,
                         # TrailCatalog, TrailEngines (7 motores), RawInputMouse…
    FramePacer.cs DisplayInfo.cs LogStore.cs UpdateChecker.cs (releases)
  UI\                  # todo code-only, sin .Designer (layout manual)
    MainForm.cs          # ventana + update-check (TaskDialog, descarga a Downloads)
                         # instalación, estelas, accesos; layout en LayoutRoot
    SchemesDialog.cs     # GALERÍA tarjetas (brillo=rota, 🔍/✔, menú eliminar,
                         # toasts, checkbox animación). Todo in-situ, sin rebuild
    SchemePickerDialog.cs# gemela para elegir destino (clic=elige, +Aleatorio)
    SchemePreviewDialog.cs # rejilla de 15 roles de un esquema
    SplashForm.cs        # splash modal + BootTask + DialogResult.OK
    TrailGalleryDialog.cs ClickGalleryDialog.cs VariantSelectDialog.cs
    DuplicateConfirmDialog.cs PrefsDialog.cs LogDialog.cs TutorialForm.cs
    TrailOverlay.cs      # overlay layered click-through (dual trail dentro)
    TrayManager.cs Theme.cs Toast.cs FlowFrame.cs (marco orbital)
    DarkListBox.cs DarkScrollBar.cs DarkScrollPanel.cs
```

## 3. Flujos clave

- **Arranque** (`Program.Main`): mutex instancia única → 2ª instancia señaliza
  y sale → splash modal con `BootTask` (init real) → `MainForm` → tutorial
  no-modal al lado (`TutorialForm.ShowBeside`) → `Application.Run`.
- **Persistencia**: TODO live-apply (cada cambio guarda). `FlushSettings()`
  al cerrar/salir. `Save()` atómica (tmp+Move). `settings-audit.log`
  (300 líneas) anota cada LOAD/SAVE con exe/pid/valores.
- **Rotación**: `CursorRotator` baraja pool y recorre sin repetir.
  `PeekNext` = vista previa; `TakeNext` = botón Aplicar siguiente.
  Agenda: `Start(pool, scheduler)` one-shot que se reprograma en `Tick`.
- **Instalación** (`InstallAsync`, 3 fases): extraer → parsear todo →
  diálogo de variantes (solo si hay multi-grupo o duplicados) → copiar con
  confirmación por duplicado (`Overwrite/Skip/SkipAll`). Headless = todo + sobrescribe.
- **Estelas**: `MainForm` empuja opciones al `TrailOverlay` (primaria +
  secundaria concurrente, mismos parámetros globales).

## 4. Batería CLI (¡úsala! el exe es WinExe + AllocConsole en CLI)

```powershell
dotnet build src\CursorRotator.csproj -c Release
.\src\bin\Release\net10.0-windows\win-x64\CursorRotator.exe --trail-test      # motor
.\src\bin\Release\net10.0-windows\win-x64\CursorRotator.exe --settings-test   # round-trip ajustes
.\src\bin\Release\net10.0-windows\win-x64\CursorRotator.exe --queue-test      # preview==botón + reagenda + destino
.\src\bin\Release\net10.0-windows\win-x64\CursorRotator.exe --cards-test      # galería/toggle/batch/marcos/toast/filtro
.\src\bin\Release\net10.0-windows\win-x64\CursorRotator.exe --roles-test "Nombre"  # diálogo de roles
.\src\bin\Release\net10.0-windows\win-x64\CursorRotator.exe --install-test    # variantes+duplicados (limpia todo)
.\src\bin\Release\net10.0-windows\win-x64\CursorRotator.exe --update-test [repo]  # versiones + ruta amable sin red
.\src\bin\Release\net10.0-windows\win-x64\CursorRotator.exe --dump-ui 680 600 uidump.txt  # árbol de MainForm
.\src\bin\Release\net10.0-windows\win-x64\CursorRotator.exe --self-test "dir" # parsea .inf sin escribir
.\src\bin\Release\net10.0-windows\win-x64\CursorRotator.exe --resolve-test    # rutas crudas vs resueltas
.\src\bin\Release\net10.0-windows\win-x64\CursorRotator.exe --apply-test "Nombre"  # aplica esquema (restaura después)
powershell -ExecutionPolicy Bypass -File Empaquetar.ps1   # publica + rearma paquete\ (MATA procesos CursorRotator)
powershell -ExecutionPolicy Bypass -File Publicar-Release.ps1  # zipea paquete\ → dist\CursorRotator-vX.Y.Z.zip
```

Protocolo de cambio: editar → build (0 errores, 0 warnings) → flags
afectados → `--dump-ui` si tocó layout → lanzamiento GUI 9 s
(`--no-onboarding`, sin `crash.log`) → `Empaquetar.ps1`.
**OJO**: `Empaquetar.ps1` y los tests de lanzamiento matan procesos del
usuario; los tests CLI que mutan (queue/apply/install) restauran al salir.

## 5. Reglas duras (lecciones pagadas)

1. **NUNCA `Get-Content … | Set-Content` en UTF-8 sin BOM** (PS 5.1 lo
   corrompe a doble-encoding/mojibake). Solo la herramienta `edit`;
   para scripts usa `[IO.File]::ReadAllBytes/WriteAllBytes`. Verificación:
   buscar bytes `C3 83|C3 82|C3 B0|C3 9E|C3 A2` (debe dar 0) y compilar.
2. **`_suspendDirty` cubre TODOS los eventos que guardan**: `TrackBar`
   dispara `ValueChanged` al asignar en `LoadSettingsToUi`; si su handler
   guarda/aplica, envenena `_settings` y el archivo (bug real cazado por
   auditoría: duración 89→10). Patrón: handlers consultan el flag.
3. **WFO1000 es error**: propiedades públicas en controles/formularios
   llevan `[Browsable(false)]+[DesignerSerializationVisibility(Hidden)]`.
4. **WinForms Timer vive del pump**: en tests headless, bombear con
   `Application.DoEvents()` en bucle (los ticks llegan ~2× más lentos).
5. **Hilos**: el `Tick` del rotador y el render del overlay corren fuera
   de UI; proveedores/callbacks al motor NO deben tocar controles
   (fotos inmutables). `BeginInvoke` + `try/catch` en todo cruce.
6. **Clones, no maestros**: `CursorPreviewCache.Get` devuelve clones;
   quien recibe `Image` dispone la anterior. Dones verdes ●/dots por
   comparación ordinal de nombres.
7. **CLI nuevo ⇒ registrarlo en DOS sitios**: la lista `cli` de `Main`
   (si no, ¡abre el GUI y "se cuelga"!) y la rama de `RunCli`.
8. **settings.json**: lectura tolerante + migración con guarda
   (`ColorMode==0`), escritura atómica, `FlushSettings` al cerrar,
   instancia única con `Local\` (no `Global\`), `Quit` con flag `_exiting`
   (si no, Salir se cancela a bandeja).
9. **Sin diseñador**: todo UI es código; `TableLayoutPanel.AutoSize`
   encoge al contenido (para ancho de ventana usar `AutoSize=false`
   + `Bounds` en `LayoutRoot`). Verificar layout con `--dump-ui` a dos
   anchos (560/900). `FlowLayoutPanel` + `ScrollAlwaysVisible` para
   listas con scroll garantizado.
10. **ToolTip único** por formulario (`_tip` + `_tipTexts`, owner-drawn);
    no crear varios. `CheckBox/RadioButton` en claro: `Theme.WireCheckState`
    (negrita + pastilla; refrescar tras `ApplyTheme`).

## 6. Estado actual (2026-10-12)

Hecho reciente: marcos orbitales con interruptor `CardAnimation`,
agenda multi-hora con destino, instalador con variantes+duplicados,
galerías in-situ con toasts, tutorial no-modal arrastrable, intervalo
segundos/decimales/hora(s) fija(s), auditoría de settings.
Pendiente sugerido: editor de sonidos de clic (estilo CursorFX),
perfiles por juego (estilo YoloMouse), empaquetado `.cape`-like.
Settings sensibles: `%LOCALAPPDATA%\CursorRotator\settings.json`.
