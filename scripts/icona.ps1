# Rifà l'icona: da risorse\icona.svg escono icona.ico (16…256 px dentro) e icona-256.png.
# Disegna FFmpeg (librsvg), impacchetta questo script. Si lancia solo quando cambia il disegno.
$ErrorActionPreference = "Stop"
$radice = Join-Path $PSScriptRoot ".."
$ffmpeg = Join-Path $radice "motori\ffmpeg\ffmpeg.exe"
$svg = Join-Path $radice "risorse\icona.svg"
$lavoro = Join-Path ([IO.Path]::GetTempPath()) "dap-icona"
New-Item -ItemType Directory -Force $lavoro | Out-Null

$misure = 16, 24, 32, 48, 64, 128, 256
$png = @{}
foreach ($m in $misure) {
  $f = Join-Path $lavoro "$m.png"
  & $ffmpeg -hide_banner -loglevel error -y -i $svg -vf "scale=${m}:${m}:flags=lanczos" -frames:v 1 -update 1 $f
  if ($LASTEXITCODE -ne 0) { throw "FFmpeg non ha disegnato $m px" }
  $png[$m] = [IO.File]::ReadAllBytes($f)
}

$ico = Join-Path $radice "risorse\icona.ico"
$fs = [IO.File]::Create($ico)
$w = New-Object IO.BinaryWriter($fs)
$w.Write([int16]0); $w.Write([int16]1); $w.Write([int16]$misure.Count)
$posto = 6 + 16 * $misure.Count
foreach ($m in $misure) {
  $lato = if ($m -ge 256) { 0 } else { $m }
  $w.Write([byte]$lato); $w.Write([byte]$lato); $w.Write([byte]0); $w.Write([byte]0)
  $w.Write([int16]1); $w.Write([int16]32); $w.Write([int32]$png[$m].Length); $w.Write([int32]$posto)
  $posto += $png[$m].Length
}
foreach ($m in $misure) { $w.Write($png[$m]) }
$w.Close()
Copy-Item (Join-Path $lavoro "256.png") (Join-Path $radice "risorse\icona-256.png") -Force
Remove-Item -Recurse -Force $lavoro
Write-Host "Icona pronta: $ico"
