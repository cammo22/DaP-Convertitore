# Scarica Zig (compilatore C/C++ portatile, niente installazione) in motori\zig.
# Serve solo per compilare menu\DaPConvertitore.Menu.dll, il comando del tasto destro di Windows 11.
param([string]$Versione = "0.17.0")
$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
$cartella = Join-Path $PSScriptRoot "..\motori\zig"
if (Test-Path (Join-Path $cartella "zig.exe")) { Write-Host "Zig c'è già"; exit 0 }
$nome = "zig-x86_64-windows-$Versione"
$lavoro = Join-Path ([IO.Path]::GetTempPath()) "dap-zig-$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Force $lavoro | Out-Null
try {
  $zip = Join-Path $lavoro "zig.zip"
  Invoke-WebRequest -Uri "https://ziglang.org/download/$Versione/$nome.zip" -OutFile $zip -UseBasicParsing
  Expand-Archive -Path $zip -DestinationPath $lavoro -Force
  New-Item -ItemType Directory -Force (Split-Path $cartella) | Out-Null
  Move-Item (Join-Path $lavoro $nome) $cartella
  Write-Host "Zig pronto in $cartella"
} finally { Remove-Item -Recurse -Force $lavoro -ErrorAction SilentlyContinue }
