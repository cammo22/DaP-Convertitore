# Il menu di Windows 11: compila la DLL (Zig) e impacchetta il manifest sparse (makeappx) in menu\out\.
#   DaPConvertitore.Menu.dll   ← il comando IExplorerCommand, va accanto all'exe
#   DaPConvertitore.Menu.msix  ← il pacchetto (solo manifest e icone), lo registra l'app all'installazione
# La versione del pacchetto è quella di Directory.Build.props: deve salire a ogni release, se no Windows non lo aggiorna.
$ErrorActionPreference = "Stop"
$radice = Resolve-Path (Join-Path $PSScriptRoot "..")
$zig = Join-Path $radice "motori\zig\zig.exe"
$makeappx = Join-Path $radice "motori\sdk\makeappx.exe"
if (-not (Test-Path $zig)) { & (Join-Path $PSScriptRoot "prendi-zig.ps1") }
if (-not (Test-Path $makeappx)) { & (Join-Path $PSScriptRoot "prendi-sdk.ps1") }

$out = Join-Path $radice "menu\out"
New-Item -ItemType Directory -Force $out | Out-Null

& $zig c++ -target x86_64-windows-gnu -shared -O2 -fno-exceptions -fno-rtti -s `
  -o (Join-Path $out "DaPConvertitore.Menu.dll") (Join-Path $radice "menu\DaPMenu.cpp") (Join-Path $radice "menu\DaPMenu.def") `
  -lole32 -lshell32 -lshlwapi -luuid -luser32
if ($LASTEXITCODE -ne 0) { throw "compilazione della DLL del menu" }
Remove-Item (Join-Path $out "DaPMenu.lib") -ErrorAction SilentlyContinue

# le miniature di Esplora file (IThumbnailProvider): una DLL piccola che lancia l'exe con --miniatura
& $zig c++ -target x86_64-windows-gnu -shared -O2 -fno-exceptions -fno-rtti -s `
  -o (Join-Path $out "DaPConvertitore.Miniature.dll") (Join-Path $radice "menu\DaPMiniature.cpp") (Join-Path $radice "menu\DaPMiniature.def") `
  -lole32 -lshell32 -lshlwapi -luuid -luser32 -lgdi32 -ladvapi32
if ($LASTEXITCODE -ne 0) { throw "compilazione della DLL delle miniature" }
Remove-Item (Join-Path $out "DaPMiniature.lib") -ErrorAction SilentlyContinue

[xml]$props = Get-Content (Join-Path $radice "Directory.Build.props") -Encoding UTF8
$v = ($props.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1) + ".0"
$layout = Join-Path $out "pacchetto"
if (Test-Path $layout) { Remove-Item -Recurse -Force $layout }
New-Item -ItemType Directory -Force $layout | Out-Null
$manifest = [IO.File]::ReadAllText((Join-Path $radice "menu\AppxManifest.xml")).Replace("{{VERSIONE}}", $v)
[IO.File]::WriteAllText((Join-Path $layout "AppxManifest.xml"), $manifest, (New-Object Text.UTF8Encoding($false)))
Copy-Item (Join-Path $radice "risorse\icona-44.png"), (Join-Path $radice "risorse\icona-150.png") $layout

$msix = Join-Path $out "DaPConvertitore.Menu.msix"
& $makeappx pack /d $layout /p $msix /o /nv | Out-Null
if ($LASTEXITCODE -ne 0) { & $makeappx pack /d $layout /p $msix /o /nv; throw "makeappx" }
[IO.Directory]::Delete($layout, $true)

# la firma: in CI col PFX dei segreti del repository, in locale col certificato nell'archivio dell'utente.
# Senza firma il pacchetto c'è lo stesso, ma Windows non lo registra: l'app allora lascia solo il menu classico.
$signtool = Join-Path $radice "motori\sdk\signtool.exe"
if ($env:DAP_FIRMA_PFX) {
  $pfx = Join-Path ([IO.Path]::GetTempPath()) "dap-firma.pfx"
  [IO.File]::WriteAllBytes($pfx, [Convert]::FromBase64String($env:DAP_FIRMA_PFX))
  try { & $signtool sign /q /fd SHA256 /f $pfx /p $env:DAP_FIRMA_PASSWORD $msix } finally { [IO.File]::Delete($pfx) }
  if ($LASTEXITCODE -ne 0) { throw "firma del pacchetto del menu" }
} else {
  $cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq "CN=DaProdProduzioni" -and $_.HasPrivateKey } | Select-Object -First 1
  if ($cert) {
    & $signtool sign /q /fd SHA256 /sha1 $cert.Thumbprint /s My $msix
    if ($LASTEXITCODE -ne 0) { throw "firma del pacchetto del menu" }
  } else { Write-Warning "Nessun certificato DaProdProduzioni: il pacchetto del menu di Windows 11 resta senza firma" }
}
Write-Host "Menu di Windows 11 pronto ($v)"
