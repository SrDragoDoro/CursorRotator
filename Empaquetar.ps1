# Empaqueta CursorRotator para distribuir: publica el .exe y copia los
# archivos de cursores junto a él. Quien lo reciba solo descomprime y
# ejecuta CursorRotator.exe -> "Instalación en 1 clic".
# Uso: powershell -ExecutionPolicy Bypass -File Empaquetar.ps1

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

# Cierra instancias previas que bloquearían la copia del .exe.
Get-Process CursorRotator -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2
$src  = Join-Path $root "src\CursorRotator.csproj"
$dist = Join-Path $root "dist"
$pack = Join-Path $root "paquete"

Write-Output "=== 1/3 Publicando .exe ==="
dotnet publish $src -c Release --nologo -v minimal -o $dist

Write-Output "=== 2/3 Armando paquete ==="
if (Test-Path $pack) { Remove-Item $pack -Recurse -Force }
New-Item -ItemType Directory -Path $pack | Out-Null
Copy-Item (Join-Path $dist "CursorRotator.exe") $pack
$packsDest = Join-Path $pack "Cursores"
New-Item -ItemType Directory -Path $packsDest | Out-Null

# Origen de los cursores: carpeta Cursores junto al proyecto, o A:\Cursores (legacy).
$candidates = @(
  (Join-Path $root "Cursores"),
  "A:\Cursores"
)
$found = 0
foreach ($c in $candidates) {
  if (-not (Test-Path $c)) { continue }
  foreach ($f in Get-ChildItem -Path $c -File | Where-Object { $_.Extension -match '^\.(zip|rar)$' }) {
    Copy-Item $f.FullName $packsDest -Force
    $found++
  }
}
if ($found -eq 0) {
  Write-Output "AVISO: no se encontraron .zip/.rar. El .exe igual sirve (pedirá la carpeta al instalar)."
} else {
  Write-Output ("Paquetes copiados: " + $found)
}

@"
CursorRotator — paquete listo
============================
1. Ejecuta CursorRotator.exe
2. Pulsa "Instalación en 1 clic"
3. Activa la rotación y ajusta el intervalo a tu gusto.

No requiere permisos de administrador.
"@ | Out-File (Join-Path $pack "LEEME.txt") -Encoding utf8

Write-Output "=== 3/3 Listo: $pack ==="
Get-ChildItem $pack | Select-Object Name
