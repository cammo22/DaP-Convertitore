# Fa l'installer: compila in Release, poi Velopack impacchetta in Releases\
#   DaPConvertitore-win-Setup.exe     ← quello da dare alla gente
#   DaPConvertitore-win-Portable.zip  ← senza installazione
#   *.nupkg + releases.win.json       ← servono agli aggiornamenti automatici
# La versione si legge da Directory.Build.props, le note dal CHANGELOG. Uno script solo per locale e CI.
param([switch]$SenzaCompilare)
$ErrorActionPreference = "Stop"
$radice = Resolve-Path (Join-Path $PSScriptRoot "..")
Push-Location $radice
try {
  [xml]$props = Get-Content (Join-Path $radice "Directory.Build.props") -Encoding UTF8
  $versione = $props.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
  if (-not $versione) { throw "Versione non trovata in Directory.Build.props" }
  Write-Host "DaP Convertitore $versione"

  if (-not $SenzaCompilare) { & (Join-Path $PSScriptRoot "compila.ps1") -Rilascio }

  # le note della versione: la sua voce del CHANGELOG, fino alla voce dopo
  $changelog = Get-Content (Join-Path $radice "CHANGELOG.md") -Encoding UTF8
  $dentro = $false
  $note = foreach ($riga in $changelog) {
    if ($riga -match '^## ') { if ($dentro) { break }; $dentro = $riga -match [regex]::Escape($versione); continue }
    if ($dentro) { $riga }
  }
  $fileNote = Join-Path ([IO.Path]::GetTempPath()) "dap-note.md"
  [IO.File]::WriteAllText($fileNote, (($note -join "`n").Trim()), (New-Object Text.UTF8Encoding($false)))

  dotnet tool restore | Out-Null
  dotnet vpk pack -u DaPConvertitore -v $versione -p publish -e DaPConvertitore.exe `
    --packTitle "DaP Convertitore" --packAuthors "DaProd" `
    -i risorse\icona.ico -s risorse\splash.png --splashProgressColor "#FFD54A" `
    --releaseNotes $fileNote -o Releases
  if ($LASTEXITCODE -ne 0) { throw "vpk pack" }
  Get-ChildItem Releases | Format-Table Name, @{ n = 'MB'; e = { [math]::Round($_.Length / 1MB, 1) } } -AutoSize
} finally { Pop-Location }
