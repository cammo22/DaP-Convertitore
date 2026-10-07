# Prende makeappx.exe dal pacchetto NuGet ufficiale Microsoft.Windows.SDK.BuildTools (niente installazione):
# serve a impacchettare il manifest del menu di Windows 11 (menu\AppxManifest.xml → DaPConvertitore.Menu.msix).
param([string]$Versione = "10.0.28000.2705")
$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
$cartella = Join-Path $PSScriptRoot "..\motori\sdk"
if (Test-Path (Join-Path $cartella "makeappx.exe")) { Write-Host "makeappx c'è già"; exit 0 }
$lavoro = Join-Path ([IO.Path]::GetTempPath()) "dap-sdk-$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Force $lavoro | Out-Null
try {
  $zip = Join-Path $lavoro "sdk.zip"
  Invoke-WebRequest -Uri "https://www.nuget.org/api/v2/package/Microsoft.Windows.SDK.BuildTools/$Versione" -OutFile $zip -UseBasicParsing
  Expand-Archive -Path $zip -DestinationPath $lavoro -Force
  $bin = Get-ChildItem $lavoro -Recurse -Filter makeappx.exe | Where-Object { $_.FullName -match '\\x64\\' } | Select-Object -First 1
  if (-not $bin) { throw "makeappx.exe non trovato nel pacchetto" }
  New-Item -ItemType Directory -Force $cartella | Out-Null
  # makeappx vuole le sue DLL accanto
  Get-ChildItem $bin.DirectoryName -File | Copy-Item -Destination $cartella
  Write-Host "makeappx pronto in $cartella"
} finally { Remove-Item -Recurse -Force $lavoro -ErrorAction SilentlyContinue }
