# L'immagine dell'installazione (risorse\splash.png): sfondo viola notte, icona, scritta oro e magenta.
# I caratteri si copiano accanto, così il filtro di FFmpeg non deve fare i conti coi "C:" dei percorsi.
$ErrorActionPreference = "Stop"
$radice = Resolve-Path (Join-Path $PSScriptRoot "..")
$ffmpeg = Join-Path $radice "motori\ffmpeg\ffmpeg.exe"
$lavoro = Join-Path ([IO.Path]::GetTempPath()) "dap-splash"
New-Item -ItemType Directory -Force $lavoro | Out-Null
Copy-Item "$env:WINDIR\Fonts\seguibl.ttf" (Join-Path $lavoro "nero.ttf")
Copy-Item "$env:WINDIR\Fonts\seguisb.ttf" (Join-Path $lavoro "semi.ttf")
Copy-Item (Join-Path $radice "risorse\icona-256.png") (Join-Path $lavoro "icona.png")
$filtro = @"
[1:v]scale=132:132[ic];
[0:v][ic]overlay=(W-w)/2:34,
drawtext=fontfile=nero.ttf:text=DaP:fontsize=50:fontcolor=0xFFD54A:x=(w-text_w)/2:y=176,
drawtext=fontfile=nero.ttf:text=CONVERTITORE:fontsize=28:fontcolor=0xFF3DF2:x=(w-text_w)/2:y=240,
drawtext=fontfile=semi.ttf:text=Qualsiasi file in qualsiasi formato:fontsize=18:fontcolor=0xA19DB0:x=(w-text_w)/2:y=300
"@
Set-Content (Join-Path $lavoro "filtro.txt") $filtro -Encoding ascii
Push-Location $lavoro
try {
  & $ffmpeg -hide_banner -loglevel error -y -f lavfi -i "gradients=s=640x360:c0=0x2a1748:c1=0x09080D:x0=320:y0=0:x1=320:y1=400:type=radial:d=1" `
    -i icona.png -/filter_complex filtro.txt -frames:v 1 -update 1 splash.png
  if ($LASTEXITCODE -ne 0) { throw "FFmpeg non ha fatto lo splash" }
  Copy-Item splash.png (Join-Path $radice "risorse\splash.png") -Force
} finally { Pop-Location; Remove-Item -Recurse -Force $lavoro }
Write-Host "Splash pronto"
