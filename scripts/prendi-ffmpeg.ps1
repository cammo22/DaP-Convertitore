# Scarica FFmpeg (build GPL "shared" di BtbN) e tiene solo quello che serve:
# ffmpeg.exe, ffprobe.exe e le DLL. Finisce in motori\ffmpeg, che git non vede.
# Lo usano sia lo sviluppo sia la CI: una cosa sola, uguale ovunque.
param(
  [string]$Versione = "n9.0",
  [string]$Cartella = (Join-Path $PSScriptRoot "..\motori\ffmpeg")
)
$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$serie = $Versione.TrimStart("n")
$nome = "ffmpeg-$Versione-latest-win64-gpl-shared-$serie"
$url = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/$nome.zip"

if (Test-Path (Join-Path $Cartella "ffmpeg.exe")) {
  Write-Host "FFmpeg c'e' gia' in $Cartella"
  exit 0
}

$lavoro = Join-Path ([IO.Path]::GetTempPath()) "dap-ffmpeg-$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Force $lavoro | Out-Null
try {
  $zip = Join-Path $lavoro "ffmpeg.zip"
  Write-Host "Scarico $url"
  Invoke-WebRequest -Uri $url -OutFile $zip -UseBasicParsing
  Expand-Archive -Path $zip -DestinationPath $lavoro -Force
  $bin = Join-Path $lavoro "$nome\bin"
  New-Item -ItemType Directory -Force $Cartella | Out-Null
  Get-ChildItem $bin -File | Where-Object { $_.Name -ne "ffplay.exe" } | Copy-Item -Destination $Cartella -Force
  Copy-Item (Join-Path $lavoro "$nome\LICENSE.txt") (Join-Path $Cartella "LICENSE-FFmpeg.txt") -Force
  Write-Host "FFmpeg pronto in $Cartella"
}
finally {
  Remove-Item -Recurse -Force $lavoro -ErrorAction SilentlyContinue
}
