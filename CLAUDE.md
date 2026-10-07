# DaP Convertitore — leggi questo per primo

Tasto destro su un file → **DaP Convertitore** → il file convertito finisce accanto all'originale come
`nome (convertito).ext`. App Windows installabile, stile DaProd (viola notte, oro, magenta; Orbitron, Rajdhani, DSEG7).
Cosa fa e come si usa: `README.md`. Cosa è cambiato: `CHANGELOG.md`.

- **Si scrive in italiano parlato**: commenti, CHANGELOG, README, interfaccia. Nomi tecnici in inglese dove serve.
- **Niente `&&` nei comandi** (PowerShell 5.1), uno per riga. Commit a nome `cammo22 <dapprod22@gmail.com>`.
- ⚠ **PowerShell 5.1 e UTF-8**: `Get-Content`/`Set-Content` senza `-Encoding` leggono i file UTF-8 come ANSI e
  li riscrivono con le accentate rovinate (è successo il 7 ottobre). Si modifica col tool Edit, o con
  `[IO.File]::ReadAllText/WriteAllText` e `UTF8Encoding`. Gli script `.ps1` si salvano **con il BOM**.
- **Una versione = `<Version>` in `Directory.Build.props` + voce in `CHANGELOG.md`.** Unita su `main`, la CI
  (`.github/workflows/rilascio.yml`) prova, compila, impacchetta con Velopack e pubblica la release da sola.
  Il numero sale di 0.0.1.
- **Le prove si fanno girare**: `dotnet test test\DaPConvertitore.Prove` (converte davvero, in `test\.out`).
  Prima di pubblicare si apre l'app vera e si rifà il gesto: `scripts\compila.ps1`, poi l'exe in `bin\Debug`.
- **Il `ui\dist` vecchio vince sul sorgente**: `scripts\compila.ps1` rifà sempre prima l'interfaccia.

## Com'è fatta

- `src/DaPConvertitore.Motore` — tutto il lavoro, senza finestre. **`Catalogo.cs` è l'unico posto** dove si dice
  cosa si converte in cosa: lo leggono l'interfaccia, il menu del tasto destro e le prove. `PianoVideo.cs` fa il
  conto dello slider del peso e lo stesso conto serve alla conversione vera.
- `src/DaPConvertitore` — WPF + WebView2. `Ponte.cs` parla con l'interfaccia (`{id, cmd, args}` → risposta;
  eventi `{evento, dati}`). ⚠ Fra C# e JavaScript gli **oggetti perdono l'ordine delle chiavi** (Chromium le
  riordina): quando l'ordine conta si manda un elenco.
- `ui/` — TypeScript senza framework. `npm run dev` la apre nel browser col finto motore (`finto.ts`).
- FFmpeg 9.0 (BtbN gpl-shared) con `scripts\prendi-ffmpeg.ps1` in `motori\ffmpeg`, fuori da git.

## Le scelte che non si vedono dal codice

- **.NET e non Tauri** perché sul PC di Cammo non c'è MSVC (niente Rust). .NET dà gratis WIC, Windows.Data.Pdf,
  l'OCR e il registro.
- **Il menu di Windows 11** (quello corto, senza «Mostra altre opzioni») vuole un pacchetto sparse firmato + una
  DLL IExplorerCommand nativa: serve MSVC e un certificato fidato sulla macchina. Per ora la voce sta nel menu
  classico (Maiusc + tasto destro). È il prossimo passo grosso, da decidere con Cammo.
- **Istanza unica**: Esplora file lancia un processo per file; il primo apre la finestra (mutex
  `Local\DaProd.Convertitore`), gli altri mandano i file da una named pipe. Le azioni rapide aspettano 450 ms che
  arrivino tutti, poi le conversioni che uniscono mettono i file in ordine di nome (`StrCmpLogicalW`).
- **NVENC in VBR sfora dell'1-2%**: col peso si parte dal 97% del bitrate; se sfora lo stesso si rifà più stretto.
- **I pesi sono a 1024** come in Esplora file; i preset sono MiB (il DVD è l'eccezione: 4,7 miliardi di byte).
- **Per guardare dentro l'app vera**: avviala con `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--remote-debugging-port=9333`,
  poi `node scripts\dentro.mjs "espressione"`. `scripts\foto-finestra.ps1` la fotografa, `scripts\menu-vero.ps1`
  legge il menu del tasto destro come lo costruisce Esplora file.
