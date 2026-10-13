# CursorRotator 🖱️

App de escritorio (Windows, C# WinForms, .NET 10) para **instalar en 1 clic**
y **rotar automáticamente** esquemas de cursores personalizados. Sin UAC:
todo se guarda en `%LOCALAPPDATA%\Cursors` y `HKCU`.

## Estructura

```text
A:\CursorRotator\
├─ src\
│  ├─ CursorRotator.csproj      # net10.0-windows, WinExe, single-file
│  ├─ app.manifest              # asInvoker (sin UAC)
│  ├─ Program.cs                # entrada GUI + modos CLI (--self-test, --install, --minimized)
│  ├─ Core\
│  │  ├─ NativeMethods.cs       # SystemParametersInfo (refresco instantáneo) + título oscuro
│  │  ├─ SchemeStore.cs         # registro HKCU: leer/registrar/aplicar/borrar;
│  │  │                          # sanitiza rutas (%vars%, relativas→C:\Windows\Cursors)
│  │  │                          # y valida existencia (FileNotFound ruidoso, no silencioso)
│  │  ├─ InfParser.cs           # parser .inf clásico (pixloen) y moderno ([Strings] BLZ/VSThemes)
│  │  ├─ CursorInstaller.cs     # extracción .zip/.rar + copia + registro, con progreso
│  │  ├─ CursorRotator.cs       # temporizador en hilo de fondo (sin repetir el último)
│  │  ├─ TrailEngine.cs         # coordinador (captura, historial, estilos, física)
│  │  ├─ Trails/                # ITrailStyle + 40 estilos + 7 motores + 20 clics
│  │  │  ├─ TrailContext.cs     # contexto: partículas, historial, color, densidad
│  │  │  ├─ ITrailStyle.cs      # contrato Update/Draw/HasActivity/GetBounds
│  │  │  ├─ ClassicStyles.cs    # 8 presets originales (misma matemática)
│  │  │  ├─ AdvancedStyles.cs   # órbita→moebius + telaraña/agujero detallados
│  │  │  ├─ AdvancedStyles2.cs  # 15 nuevos: espiral, constelación, dragón, láser,
│  │  │  │                       # hex, holograma, monarcas, niebla, caleidoscopio,
│  │  │  │                       # retro, blockchain, blizzard, cuerda, bio, blancos
│  │  │  ├─ ClickEffects.cs     # IClickEffect + 20 efectos + ClickEngine
│  │  │  ├─ TrailCatalog.cs     # galería: 40 estilos en 4 pestañas + búsqueda
│  │  │  ├─ TrailEngines.cs     # Timer, Hook, Spline, Inercial, Async, Predictivo, Raw
│  │  │  └─ RawInputMouse.cs    # motor Raw Input HID (WM_INPUT, cero latencia)
│  │  ├─ DisplayInfo.cs         # Hz real por monitor (EnumDisplaySettings + caché)
│  │  ├─ FramePacer.cs          # budget de frame según monitor/topes
│  │  ├─ LogStore.cs            # bitácora compartida dashboard ↔ modal
│  │  ├─ CursorColor.cs         # color dominante .cur/.ani (RIFF/DIB propio, sin Pillow)
│  │  └─ AppSettings.cs         # ajustes JSON + inicio con Windows
│  └─ UI\
│     ├─ MainForm.cs            # ventana única Top/Bottom/Fill + ToolTip singleton
│     ├─ SchemesDialog.cs       # modal: buscar + checks de rotación + borrar
│     ├─ TrailGalleryDialog.cs  # galería 40 estilos + búsqueda plana con lupa
│     ├─ ClickGalleryDialog.cs  # galería 20 clics + búsqueda plana con lupa
│     ├─ TutorialForm.cs        # onboarding 5 pasos (sin bordes, arrastrable)
│     ├─ SplashForm.cs          # splash con init real + progreso
│     ├─ PrefsDialog.cs         # modal con binding (topmost, pacing, tips, tuto, splash)
│     ├─ LogDialog.cs           # modal del registro (vive de LogStore)
│     ├─ DarkScroll*.cs         # scroll oscuro propio (panel, lista)
│     ├─ TrailOverlay.cs        # overlay layered click-through (7 motores + topmost)
│     └─ TrayManager.cs         # NotifyIcon con menú (mostrar/pausar/cambiar/salir)
├─ dist\                        # .exe publicado (se genera)
├─ Empaquetar.ps1               # genera paquete distribuible en .\paquete\
└─ README.md
```

## Requisitos

- **Para usar:** Windows 10/11 de 64 bits. Nada más (el .exe es autocontenido).
- **Para compilar:** [.NET 10 SDK](https://dotnet.microsoft.com/download) (lleva WinForms incluido).

Sin dependencias NuGet: `NotifyIcon`, registro, `SystemParametersInfoW(0x0057,…)`
y extracción ZIP usan solo la BCL. Los `.rar` se extraen con WinRAR si está
instalado; si no, la app lo indica y basta con usar la versión `.zip`.

## Compilar y publicar

```powershell
# Solo compilar
dotnet build src\CursorRotator.csproj -c Release

# .exe portable único (~110 MB, autocontenido) en .\dist\
dotnet publish src\CursorRotator.csproj -c Release -o dist
```

## Verificación (sin GUI)

```powershell
# Parsea todos los .inf del paquete sin escribir nada (exit 1 si algo falla)
.\dist\CursorRotator.exe --self-test "A:\Cursores"

# Diagnóstico del bug [pixloen]: rutas crudas vs resueltas de los 60 esquemas
.\dist\CursorRotator.exe --resolve-test

# Aplica un esquema por nombre exacto (los [] no son wildcard)
.\dist\CursorRotator.exe --apply-test "[pixloen] Sakura Chiyono O"

# Instalación silenciosa (misma lógica que el botón 1-clic).
# Por defecto omite variantes [Static] (sin animación); añade --include-static para traerlas.
.\dist\CursorRotator.exe --install "A:\Cursores"
```

## Distribuir (descomprimir → doble clic → instalar)
```powershell
powershell -ExecutionPolicy Bypass -File Empaquetar.ps1
```

Genera `.\paquete\` con `CursorRotator.exe` + `Cursores\*.zip|*.rar` + `LEEME.txt`.
Quien lo reciba solo ejecuta el .exe y pulsa **Instalación en 1 clic**:
la app autodetecta el material junto al ejecutable.

## Compartir y mantener actualizado

1. **Publica en GitHub:** crea un repo (p. ej. `tu-usuario/CursorRotator`),
   súbelo, y crea un Release con tag `vX.Y.Z` (igual que `<Version>` del
   `.csproj`). Genera el adjunto con:
   ```powershell
   powershell -ExecutionPolicy Bypass -File Empaquetar.ps1
   powershell -ExecutionPolicy Bypass -File Publicar-Release.ps1
   ```
   Sube `dist\CursorRotator-vX.Y.Z.zip` al release. Comparte el enlace.
2. **Aviso en la app:** en ⚙️ Opciones Avanzadas escribe tu repo
   (`dueño/CursorRotator`) y pulsa **🔄 Buscar ahora**. La app compara el
   tag con su versión, muestra las novedades y descarga el `.zip`;
   además avisa sola al abrir (máx. 1 vez al día, omisible por versión).
   Quien la reciba solo pone el mismo repo una vez y listo.

## Diagnóstico

- Cualquier excepción no controlada genera `crash.log` junto al `.exe`
  y muestra un `MessageBox` en vez de cerrarse en silencio (ver `Program.cs`).
- El `SplitContainer` fija `PanelMinSize`/`SplitterDistance` en `OnShown`
  (hacerlo en el constructor lanza `ArgumentOutOfRangeException` porque el
  ancho aún es 0).
- El hilo del overlay captura sus propios errores con throttle (1/s) a
  `crash.log` sin tumbar el proceso.

## Uso de la app

| Control | Qué hace |
|---|---|
| ▶ Activar / ⏸ Pausar | Inicia/detiene el hilo de rotación. Al pausar, el cursor queda libre para cambio manual |
| Cambiar cada + Minutos/Horas | Intervalo (cambiarlo reinicia el temporizador) |
| 🎲 Cambiar ahora | Aplica un esquema aleatorio al instante |
| 🖱️ Ajuste manual | Pausa y abre `main.cpl` (propiedades del mouse) |
| 📦 Instalación en 1 clic | Extrae + copia + registra; muestra progreso y log. La casilla *Incluir variantes estáticas* (apagada por defecto) controla los esquemas `[Static]` (`.cur` sin animación). Si el paquete trae varios instaladores (ej. Normal/2x Speed) te deja marcar variantes; si un esquema ya existe, pregunta si reinstalarlo u omitirlo (con *omitir todos*) |
| Lista de esquemas | Doble clic = aplicar ese esquema |
| ☀️/🌙 | Tema claro/oscuro (incluye barra de título) |
| Bandeja | Minimizar/cerrar envía a la bandeja; menú: mostrar, pausar, cambiar, salir |
| Opciones | Iniciar con Windows (HKCU Run), rotar al abrir |

## Detalles técnicos

- **Refresco instantáneo:** `user32.dll!SystemParametersInfoW(SPI_SETCURSORS=0x57, 0, NULL, SPIF_UPDATEINIFILE|SPIF_SENDCHANGE)`.
- **Aplicar esquema:** 15 roles estándar por posición + extras por nombre de archivo
  (`*person*`→Person, `*pin*`→Pin), igual que los `.inf` originales; `Scheme Source=2`.
- **Formatos .inf:** clásico con rutas `%10%\Cursors\…` y moderno con variables
  `%SCHEME_NAME%/%CUR_DIR%/%pointer%…` de `[Strings]`. Carpetas `MacOS` ignoradas;
  `.zip` anidados (ej. `ani file.zip`) extraídos automáticamente.
- **Anti-contaminación entre packs:** se respeta el mapa `[Scheme.Cur]`
  (destino→fuente, ej. `cursors\*.ani` de pixloen) y el fallback solo busca
  bajo el dir del `.inf`, nunca en todo el staging (así los 18 pixloen dejaron
  de compartir los archivos de Agnes Tachyon). `--self-test` lo audita.
- **Rutas de esquemas:** `ExpandEnvironmentVariables` + relativas→`%WINDIR%\Cursors`;
  `ApplyScheme` valida `File.Exists` y falla en voz alta (`[Error] …ruta…`).
- **Ajustes:** `%LOCALAPPDATA%\CursorRotator\settings.json`.

## Estelas del ratón ✨ (nuevo)

Overlay flotante 100% click-through (`WS_EX_TRANSPARENT | WS_EX_LAYERED |
WS_EX_TOPMOST | WS_EX_NOACTIVATE`): una ventana de 360×360 px que sigue al
cursor y dibuja con alfa por píxel (`UpdateLayeredWindow`), así los clics,
arrastres y selección de texto pasan intactos.

- **Presets (40 + 20 clics):** los 25 anteriores más espiral áurea (Fibonacci),
  constelación (5 vecinos), aliento de dragón (humo+chispas), láser táctico
  (núcleo+blur), hex dump, holograma RGB (desfase por velocidad), monarcas
  (aleteo), niebla oscura (bloquea luz), caleidoscopio (4 ejes), retro 8-bits
  (snap 45°/90°), blockchain (eslabones), blizzard (viento), cuerda vibrante
  (onda estacionaria), bioluminiscencia (pulso), agujeros blancos. Clics:
  shockwave, splat, implosión, fragmentación, cuánticos, binario, grieta, EMP,
  soldadura (rebote), orbital, burbuja (2 fases), supernova, mandala, shuriken,
  fantasma, nieve, gusano, confeti, magnética, radar.
- **Color híbrido (trifecta):** Predeterminado / Heredado (paleta top-3) /
  Manual [base,claro,oscuro] con exclusión mutua en UI; reparto secuencial.
- **Motores (7):** Timer (sondeo, predet.), Hook LL, Spline Catmull-Rom,
  Inercial (momentum ×1.35, gravedad, rebote), Async (worker + doble búfer,
  la UI solo blitea), Predictivo Hermite (anticipa ~30 ms), Raw Input HID
  (`RegisterRawInputDevices` + `WM_INPUT`, cero latencia, con fallback).
- **Multi-monitor asimétrico:** física 100% basada en `DeltaTime` (Stopwatch);
  `FramePacer` ajusta el budget al monitor bajo el cursor (`EnumDisplaySettings`,
  caché 5 s): Auto-Detect, topes 60/144 o uncapped (piso 4 ms anti-spin).
- **Forzar topmost (juegos):** timer de 2 s con `SetWindowPos(HWND_TOPMOST,
  SWP_NOMOVE|NOSIZE|NOACTIVATE)`. Vale para borderless; en fullscreen
  **exclusivo** el DWM no compone overlays (limitación de Windows, no del app).
- **Motores (6):** Timer (sondeo, predet.), Hook LL, Spline Catmull-Rom,
  Inercial (momentum ×1.35, gravedad, rebote), Async (worker + doble búfer,
  la UI solo blitea), Predictivo Hermite (anticipa ~30 ms).
- **Captura de alta frecuencia:** hook de bajo nivel `WH_MOUSE_LL`
  (`SetWindowsHookEx`): cada movimiento del sistema se encola (rápido, sin
  trabajo pesado en el callback) y el hilo de render lo consume. Si el hook
  falla, fallback automático a sondeo `GetCursorPos`. Sin lerp: emisión
  estable por punto capturado.
- **Motores conmutables en vivo** (combo *Motor*, predet. Clásico/Timer):
  `Timer` (sondeo ~60 Hz), `Hook` (eventos del SO) y `Spline` (hook +
  remuestreo Catmull-Rom entre muestras + neón con `DrawCurve`).
- **Neón con splines:** la línea se dibuja con curvas cardinales
  (`Graphics.DrawCurve`, tensión 0.5) sobre ventanas de 4 puntos del
  historial, con alfa por antigüedad: trazo curvo y orgánico en círculos.
- **Render independiente:** hilo de fondo con `DeltaTime` real (`Stopwatch`)
  a ~60 FPS; física basada en tiempo, no en ticks. Reposo (>1 s quieto y sin
  partículas): el hilo duerme, CPU ≈ 0.
- Verificación: giros rápidos (2 vueltas en 120 muestras) dan trazo denso
  (Cometa ~10k px, Neón ~6.8k px) — `CursorRotator.exe --trail-test`.
- **Origen de la estela:** *Desde la punta* (X,Y exactos) o *Desde la cola*
  (offset +16,+20 para cursor de 32×32). Verificado en test dedicado.
- **Color del cursor:** la casilla *Heredar color del cursor actual* lee el
  `.cur`/`.ani` del puntero activo (primer frame en `.ani`), calcula su color
  dominante con cuantización por frecuencia (filtra alfa <200, negros de
  contorno y blancos puros; bins RGB/16, gana el más frecuente) y tiñe los 18
  presets; se re-detecta al cambiar de esquema. Verificación sobre 1016
  archivos reales: `CursorRotator.exe --cursorcolor-test [carpeta]`.
- **Densidad (Cantidad):** slider 0.1×–3.0× con acumulador fraccional
  (determinista en conteo) que escala tasa de emisión y topes en los 18
  estilos; medido 128→1176→2501 px.
- **Sin clipping:** overlay de caja dinámica (`GetLiveBounds` por estilo +
  historial) que crece para encerrar confeti en caída, ripples y órbitas;
  muerte solo por alfa ≤ 0 (sin kill-floor ni ClipBounds); fundido progresivo
  en reposo verificado (reposo → 0 px).
- **Scroll oscuro propio** (`UI/DarkScroll*.cs`): estelas en `DarkScrollPanel`
  de altura fija (nada se recorta), lista y registro con barras nativas
  ocultas y thumb redondeado GDI+ sincronizado (`LB_GETTOPINDEX`, `EM_*`).
- **Rendimiento medido (`--trail-test`):** ~25-35 partículas vivas (tope 40),
  render <1 ms/frame a 60 FPS, fundido total al detenerse. En reposo (>1 s
  quieto y sin partículas) el hilo duerme (sondeo cada 250 ms): CPU ≈ 0.
- **Multimonitor:** la caja se recorta a la pantalla virtual (orígenes
  negativos soportados). Verificación: `CursorRotator.exe --trail-test`.
