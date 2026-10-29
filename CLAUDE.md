# DaP Convertitore — leggi questo per primo

Tasto destro su un file → **DaP Convertitore** → il file convertito finisce accanto all'originale come
`nome (convertito).ext`. App Windows installabile, stile DaProd (viola notte, oro, magenta; Orbitron, Rajdhani, DSEG7).
Cosa fa e come si usa: `README.md` (corto, per l'utente) e `docs/GUIDA.md` (completo). Cosa è cambiato: `CHANGELOG.md`.

- **Si scrive in italiano parlato**: commenti, CHANGELOG, README, interfaccia. Nomi tecnici in inglese dove serve.
- **Niente `&&` nei comandi** (PowerShell 5.1), uno per riga. Commit a nome `cammo22 <dapprod22@gmail.com>`.
- ⚠ **PowerShell 5.1 e UTF-8**: `Get-Content`/`Set-Content` senza `-Encoding` leggono i file UTF-8 come ANSI e
  li riscrivono con le accentate rovinate (è successo il 7 ottobre). Si modifica col tool Edit, o con
  `[IO.File]::ReadAllText/WriteAllText` e `UTF8Encoding`. Gli script `.ps1` si salvano **con il BOM**.
- **Una versione = `<Version>` in `Directory.Build.props` + voce in `CHANGELOG.md`.** Unita su `main`, la CI
  (`.github/workflows/rilascio.yml`) prova, compila, impacchetta con Velopack e pubblica la release da sola.
  Il numero sale di 0.0.1 (di 0.1 per le cose grosse, come il lettore nella 1.1.0).
- **Le prove si fanno girare**: `dotnet test test\DaPConvertitore.Prove` (converte davvero, in `test\.out`).
  Prima di pubblicare si apre l'app vera e si rifà il gesto: `scripts\compila.ps1`, poi l'exe in `bin\Debug`.
- **La prova dell'interfaccia vera** (dalla 2.2.5): `node scripts\prova-interfaccia.mjs` avvia l'app con i dati in una cartella temporanea, la guida
  da WebView2 (debug remoto, porta 9334) e controlla barra, home, impostazioni, simbolo dell'avatar, novità e che non ci siano errori in
  console. L'app non deve essere già aperta (istanza unica). Non fa foto: stampa «ok» o «MALE» per ogni controllo.
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
- **Il menu di Windows 11** (dalla 1.0.2): DLL IExplorerCommand in C++ (`menu/DaPMenu.cpp`) compilata con **Zig**
  (niente MSVC sul PC) + pacchetto sparse `menu/AppxManifest.xml` impacchettato con makeappx (NuGet
  Microsoft.Windows.SDK.BuildTools) e **firmato** col certificato `CN=DaProdProduzioni` (pubblico in
  `risorse/DaProdProduzioni-firma.cer`, la chiave nell'archivio certificati di Cammo e nei segreti
  `DAP_FIRMA_PFX`/`DAP_FIRMA_PASSWORD`). Provato il 7 ottobre: un pacchetto **non firmato** con comandi Windows non
  lo registra per un utente solo (0x80073D2B), e la fiducia nel certificato messa per l'utente non basta
  (0x800B0109): serve «Persone attendibili» del computer, cioè **un permesso di amministratore, una volta**
  (certutil, dal tasto Attiva). Le voci del sottomenu la DLL le legge da `menu.tsv`, scritto dall'app dal Catalogo.
  Se si cambia la DLL si prova con `ProveMenu11` (la carica in-process, niente registrazione).
- **Istanza unica**: Esplora file lancia un processo per file; il primo apre la finestra (mutex
  `Local\DaProd.Convertitore`), gli altri mandano i file da una named pipe. Le azioni rapide aspettano 450 ms che
  arrivino tutti, poi le conversioni che uniscono mettono i file in ordine di nome (`StrCmpLogicalW`).
- **NVENC in VBR sfora dell'1-2%**: col peso si parte dal 97% del bitrate; se sfora lo stesso si rifà più stretto.
- **I pesi sono a 1024** come in Esplora file; i preset sono MiB (il DVD è l'eccezione: 4,7 miliardi di byte).
- **Per guardare dentro l'app vera**: avviala con `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--remote-debugging-port=9333`,
  poi `node scripts\dentro.mjs "espressione"`. `scripts\foto-finestra.ps1` la fotografa, `scripts\menu-vero.ps1`
  legge il menu del tasto destro come lo costruisce Esplora file. `node scripts\schermo.mjs uscita.png "js" lettore|index`
  fotografa la pagina (CDP) dopo aver eseguito del JavaScript.
- **Il lettore** (dalla 1.1.0): `--guarda file` apre `FinestraLettore` con `ui/lettore.html`; `Regia.cs` decide
  (un processo solo: convertitore e lettore sono finestre dello stesso, l'app si chiude con l'ultima). Il doppio
  clic riusa il lettore aperto, come Foto. I file la pagina li prende da `https://dap.file/` (`Risorse.cs`,
  WebResourceRequested con gettoni, mai percorsi): niente server su localhost, che Chromium tratta come rete
  locale. Il video che WebView2 non suona va da FFmpeg in MP4 a frammenti (`Flusso`) e la pagina li mette nel
  Media Source con `timestampOffset` = punto di partenza (`-output_ts_offset` di FFmpeg nel MP4 a frammenti **non**
  vale: provato). `--autoplay-policy=no-user-gesture-required` nell'ambiente WebView2, se no il video non parte
  da solo. Il PDF si apre **in memoria** (aperto dal file, Windows lo blocca e non va nel Cestino).
- **«Apri con» e non predefinito**: Windows non lascia che un'app si faccia predefinita da sola (UserChoice è
  protetto). `ApriCon.cs` mette un ProgID per gruppo, `OpenWithProgids` e Capabilities + RegisteredApplications;
  il tasto «App predefinite» apre `ms-settings:defaultapps?registeredAppUser=DaP Convertitore`. Per provarlo senza
  toccare i menu: `DaPConvertitore.exe --apricon` / `--togli-apricon`.
- **Le variabili CSS nello `style` di `h()`** vanno con `setProperty` (`util.ts`): fino alla 1.0.3 le tinte per
  categoria (`--tinta`) non arrivavano e tutto era oro.
- **Modifiche sul file vero** (`Lettore/Modifiche.cs`, dalla 1.3.0): JPG/TIFF girano riscrivendo il JPEG con
  l'encoder «di trascrizione» di WIC (`CreateForTranscodingAsync` + `System.Photo.Orientation`; le proprietà del file
  via `SavePropertiesAsync` sono di **sola lettura**), PNG/BMP girano i pixel, MP4/MOV con `-display_rotation:v:0`
  (stesso verso di FFprobe: gradi antiorari) e `-c copy`, PDF con `PdfPage.Rotate`. Si scrive sempre una bozza `.~`
  accanto e poi si sposta sull'originale (`Sposta` ritenta con GC: le routine di lettura di Windows lasciano la
  maniglia aperta, e il file si legge in memoria per non tenerla). L'orientamento EXIF si compone con
  `ComponiExif` (tabella «prima si specchia, poi si gira in senso orario»), provato.
- **Scrivere e estrarre** (`Lettore/Scrittura.cs`, dalla 1.4.0): il testo salvato dal lettore torna nella stessa
  codifica (`FormatoDi`: BOM UTF-8/UTF-16, CRLF se il file lo ha, Latin-1 → UTF-8 e lo si dice) con bozza e
  `Sposta`. Le voci scelte dentro un archivio escono con 7-Zip (`SetteZip.Estrai`, vedi sotto): il percorso di una
  cartella (`foto/`) porta con sé anche quello che c'è dentro (provato in `ProveScrittura`).
- **La barra bianca**: `.testa` (convertitore) e `.l-barra` (lettore) hanno uno sfondo chiaro apposta, il resto è
  scuro. Non è la barra di Windows (la finestra è senza cornice): è disegnata dalla pagina.
- **Ritocco delle foto** (dalla 1.5.0): `ui/src/lettore/ritocco.ts` disegna su due fogli canvas (`base` = segni fissati,
  `viva` = segno in corso) grandi quanto la foto, dentro un wrapper che prende lo stesso `transform` della foto
  (`rit.segui`). Si salva componendo foto + segni in un canvas, codificato **dalla pagina** (JPG 0.97, PNG, WEBP) e
  mandato in base64 a `salvaRitocco` → `Lettore/Ritocco.cs`: JPG con l'EXIF dell'originale innestato (orientamento
  rimesso a 1: i pixel sono già dritti), bozza `.~` e `Modifiche.Sostituisci`. Formati che non si riscrivono → PNG accanto.
- **Trascinare fuori dall'archivio** (dalla 1.5.0): l'HTML5 drag non sa portare file fuori da WebView2, quindi si
  guida col mouse (`pointerdown/move` in `archivio.ts`). Dopo 6 px parte `preparaTrascinamento` (`Trascinamento.cs`
  estrae con 7-Zip in `%TEMP%\DaP Convertitore\trascina\<guid>`); quando il mouse esce dalla finestra con il tasto
  premuto la pagina chiama `trascinaFuori` e C# fa `DragDrop.DoDragDrop` col FileDrop (la risposta torna a drop
  finito). Sulla zona in alto si estrae accanto all'archivio. Per provarlo: `mouse_event` (SendInput) dal lettore a una
  finestra di Esplora file; `scripts\clic.mjs` fa i clic veri (non `.click()`) dentro la pagina.
- **Slider del peso delle foto** (dalla 1.5.0): `StimaImmagini.cs` risponde a `stimaImmagine` codificando davvero (JPG/HEIC
  interi con WIC; WEBP/AVIF/JXL su un ritaglio 512×512 al centro riportato all'intera foto, `Esatta=false`). La
  conversione col peso usa la stessa `Immagini.Cerca` con qualità massima 95 (`QualitaPerPeso`, anche in
  `pannelli/immagine.ts`): sopra 95 il WEBP diventa lossless. `pesoPerFile` vale per video e foto (un file è l'uno o
  l'altra). `#app` e `#lettore` hanno `grid-template-columns: minmax(0, 1fr)`: senza, un nome di file lungo e senza spazi
  allargava la pagina oltre la finestra.
- **App predefinite** (dalla 1.6.0, regola dalla 1.7.0): Windows non lascia impostarle da programma (UserChoice ha un hash
  legato a utente e data); l'app **non** lo falsifica. Il modo previsto è la regola per amministratori
  `HKLM\SOFTWARE\Policies\Microsoft\Windows\System\DefaultAssociationsConfiguration` → `Predefiniti.cs`: l'app si rilancia con
  `runas` (`--predefiniti-applica xml` / `--predefiniti-togli`), scrive il file in ProgramData e il valore; Windows lo applica
  all'accesso (non su Home). Le prove non scrivono mai nel sistema: provano solo `Predefiniti.Xml`. `apriPredefinite()` (`ui/src/app/impostazioni.ts`) fa scegliere i gruppi, il comando
  `predefinite` salva `Impostazioni.GruppiPredefiniti`, richiama `ApriCon.Registra(exe, gruppi)` (solo quei gruppi finiscono
  nelle Capabilities, quindi nella pagina di Windows) e apre `ms-settings:defaultapps`. `incolla` legge `Clipboard.GetFileDropList`.
- **Aggiornamenti** (dalla 1.8.1): `Aggiornamenti.cs` ha un ciclo solo per tutta l'app (`Avvia()` da `Programma.cs`): controlla all'avvio e ogni 4 ore
  (20 minuti dopo un errore), manda `Trovata` a tutte le finestre (convertitore e lettore) e scarica in silenzio; il badge «Aggiornamento X disponibile»
  installa col clic. Per provare il badge senza una release vera: `DAP_AGGIORNAMENTO_FINTO=9.9.9` (non installa niente).
- **I pezzi della piastra** parlano fra loro tramite `ui` (`ui/src/app/stato.ts`: `ui.scegli()`, `ui.piede()`…), che
  `main.ts` collega all'avvio: così i moduli non si importano in tondo.
- **Il Cestino a mano** (dalla 1.1.0): `Impostazioni.CestinoDaSolo`, spento di partenza (nome nuovo apposta: la
  1.0.2 salvava `cestino` acceso). Il tasto accanto al risultato chiama `Coda.OriginaleNelCestino`.
- **Archivi con 7-Zip** (dalla prossima versione): `SetteZip.cs` è l'unico posto che lancia `7z.exe` (con `7z.dll` accanto, in
  `motori\7zip`, preso da `scripts\prendi-7zip.ps1`: copia il 7-Zip installato o lo scarica da 7-zip.org). Sempre `-sccUTF-8 -scsUTF-8`
  (il `tar` di Windows scriveva i nomi in ANSI e gli accenti si rovinavano: era il bug del 7z), `-spd` (niente caratteri jolly nei nomi),
  `--` prima dell'archivio **tranne** quando si passa `@elenco` (dopo `--` il `@` non vale più), e una password finta (`-p…`) perché
  un archivio cifrato risponda «password sbagliata» invece di restare ad aspettarla. I `.tar.gz` sono un `.tar` dentro un altro
  archivio: si tira fuori il `.tar` e poi si apre (`EDiTarCompresso`). Il livello dello slider è `OpzioniArchivio.Livello` (-1 solo
  archiviare, 0 il livello di partenza, 1…9); lo ZIP al massimo usa Deflate con `-mfb=258 -mpass=15`. Le miniature dentro gli
  archivi (`MiniatureArchivio.cs`, comando `miniaturaVoce`) estraggono una voce alla volta (foto, video, PDF non enormi) e la buttano.
- **«Aggiusta tutto»** (`Ponte.AggiustaTutto`): menu classico, «Apri con», menu di Windows 11 (permesso una volta) e regola delle app
  predefinite, un passo alla volta con l'esito di ognuno. `Predefiniti.Conta` legge `UserChoice` (sola lettura) e dice quanti tipi
  hanno davvero il lettore: serve a vedere se la regola ha funzionato. La regola di Windows si riapplica a **ogni** accesso (senza
  `Suggested="true"`): chi cambia a mano un tipo se lo ritrova com'era.
- **La home** (`ui/src/app/home.ts`): le sei strade (il selettore si apre già filtrato: comando `scegli` con `categoria`), gli ultimi
  convertiti (`Storico.cs`, `storico.json`, solo l'app lo scrive: la coda lo chiama tramite `Coda.QuandoFatto`) e come sta il PC.
- **Suoni** spenti di partenza: la proprietà si chiama `SuoniAccesi` (nome nuovo apposta, come `CestinoDaSolo`).
