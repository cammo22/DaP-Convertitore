# Compila tutto: prima l'interfaccia (ui\dist), poi l'app. Il dist vecchio vince sul sorgente: si rifà sempre.
# Uso: scripts\compila.ps1            (Debug, per provare)
#      scripts\compila.ps1 -Rilascio  (Release in publish\, quello che va nell'installer)
param([switch]$Rilascio)
$ErrorActionPreference = "Stop"
$radice = Resolve-Path (Join-Path $PSScriptRoot "..")

if (-not (Test-Path (Join-Path $radice "motori\ffmpeg\ffmpeg.exe"))) { & (Join-Path $PSScriptRoot "prendi-ffmpeg.ps1") }
# il menu di Windows 11 (DLL + pacchetto firmato) in menu\out
& (Join-Path $PSScriptRoot "compila-menu.ps1")

Push-Location (Join-Path $radice "ui")
try {
  if (-not (Test-Path node_modules)) { npm ci --no-audit --no-fund; if ($LASTEXITCODE -ne 0) { throw "npm ci" } }
  npm run build; if ($LASTEXITCODE -ne 0) { throw "build dell'interfaccia" }
} finally { Pop-Location }

$progetto = Join-Path $radice "src\DaPConvertitore\DaPConvertitore.csproj"
if ($Rilascio) {
  $uscita = Join-Path $radice "publish"
  if (Test-Path $uscita) { Remove-Item -Recurse -Force $uscita }
  dotnet publish $progetto -c Release -o $uscita -nologo
} else {
  dotnet build $progetto -nologo -v q
}
if ($LASTEXITCODE -ne 0) { throw "compilazione dell'app" }
