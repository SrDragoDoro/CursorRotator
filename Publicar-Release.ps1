# Genera el .zip distribuible para un release de GitHub:
# dist\CursorRotator-vX.Y.Z.zip con el contenido de paquete\.
# La versión sale de <Version> en src\CursorRotator.csproj.
# Uso: powershell -ExecutionPolicy Bypass -File Publicar-Release.ps1

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$pack = Join-Path $root "paquete"
$dist = Join-Path $root "dist"

if (-not (Test-Path $pack)) {
  throw "No existe $pack. Ejecuta antes Empaquetar.ps1."
}

[xml]$csproj = Get-Content (Join-Path $root "src\CursorRotator.csproj")
$ver = $csproj.Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($ver)) { $ver = "dev" }

$zip = Join-Path $dist "CursorRotator-v$ver.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $pack "*") -DestinationPath $zip -CompressionLevel Optimal

Write-Output "=== Release listo: $zip ==="
Write-Output "Súbelo a GitHub → Releases → Tag v$ver → Attach binaries."
Get-Item $zip | Select-Object Name, @{n = 'MB'; e = { [math]::Round($_.Length / 1MB, 1) } }
