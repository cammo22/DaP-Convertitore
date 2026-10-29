# Prende 7-Zip (7z.exe + 7z.dll + licenza) e lo mette in motori\7zip, che git non vede.
# E' il motore di tutti gli archivi (ZIP, 7Z, RAR, TAR, GZ, XZ...). Lo usano sia lo sviluppo sia la CI.
# Si copia da un 7-Zip gia' installato (i runner di GitHub ce l'hanno); se non c'e' si scarica l'installer
# da 7-zip.org e lo si apre con il 7zr.exe ufficiale.
param(
  [string]$Cartella = (Join-Path $PSScriptRoot "..\motori\7zip"),
  [string]$Versione = "2603"
)
$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

if ((Test-Path (Join-Path $Cartella "7z.exe")) -and (Test-Path (Join-Path $Cartella "7z.dll"))) {
  Write-Host "7-Zip c'e' gia' in $Cartella"
  exit 0
}

New-Item -ItemType Directory -Force $Cartella | Out-Null
$installato = Join-Path ${env:ProgramFiles} "7-Zip"
if (Test-Path (Join-Path $installato "7z.exe")) {
  foreach ($f in "7z.exe", "7z.dll", "License.txt") {
    if (Test-Path (Join-Path $installato $f)) { Copy-Item (Join-Path $installato $f) (Join-Path $Cartella $f) -Force }
  }
  Write-Host "7-Zip copiato da $installato in $Cartella"
  exit 0
}

$lavoro = Join-Path ([IO.Path]::GetTempPath()) "dap-7zip-$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Force $lavoro | Out-Null
try {
  $base = "https://www.7-zip.org/a"
  $zr = Join-Path $lavoro "7zr.exe"
  $setup = Join-Path $lavoro "setup.exe"
  Write-Host "Scarico 7zr.exe e 7z$Versione-x64.exe da 7-zip.org"
  Invoke-WebRequest -Uri "$base/7zr.exe" -OutFile $zr -UseBasicParsing
  Invoke-WebRequest -Uri "$base/7z$Versione-x64.exe" -OutFile $setup -UseBasicParsing
  & $zr x $setup "-o$lavoro\dentro" 7z.exe 7z.dll License.txt -y | Out-Null
  foreach ($f in "7z.exe", "7z.dll", "License.txt") { Copy-Item (Join-Path $lavoro "dentro\$f") (Join-Path $Cartella $f) -Force }
  Write-Host "7-Zip pronto in $Cartella"
}
finally {
  Remove-Item -Recurse -Force $lavoro -ErrorAction SilentlyContinue
}
