# Changelog

Le novità di ogni versione, dette come stanno. La più nuova sta in alto.

## 2.1.0 — 29 ottobre 2026

- **La finestra si apre sempre davanti.** Prima, aprendo un file da Esplora file, la finestra a volte restava dietro alle altre.
  Adesso il programma già aperto riceve il permesso di passare in primo piano da chi lo ha lanciato e, se Windows fa i capricci,
  la finestra si riporta davanti da sola (anche se era ridotta a icona). Vale per il lettore e per il convertitore.
- **La scaletta, di nuovo a portata di mano, per la musica e per i video.** Sul bordo destro c'è la linguetta **SCALETTA** (e un tasto
  nei comandi, **L** nella musica, **E** nei video): un clic e scorre la lista dei file della cartella, in vetro trasparente, sopra il
  video o accanto al disco; un clic su un titolo lo suona. Resta chiusa finché non la apri, e passando da un file all'altro resta come
  l'hai lasciata.
- **Sulla linea del tempo del video, l'anteprima è fluida come in QuickTime.** Passa il cursore e il fotogramma di quel punto cambia a
  ogni pixel, senza aspettare: appena ti avvicini il programma prepara un fotogramma ogni 1,5 secondi circa (al massimo 160), e sotto il
  cursore si vede subito il più vicino; appena rallenti arriva quello esatto. **Trascinando, il video vero segue il cursore in tempo
  reale.** Funziona anche con i video che il programma traduce da solo.
- **I popup in vetro trasparente**, come la scaletta: impostazioni, avvisi, menu, pannelli e finestrelle sfumano quello che c'è dietro.
- **I popup si chiudono da soli.** La finestrella della conversione rapida si chiude 6 secondi dopo la fine (20 se c'è un errore,
  il tempo di leggerlo); se il mouse ci sta sopra aspetta, e quando lo togli si chiude dopo 4 secondi. La notifica di Windows sparisce
  anche dal centro notifiche dopo mezzo minuto.
- **Novità e aggiornamenti nella barra in alto.** Il tasto ✦ accanto all'ingranaggio apre le novità della versione in uso e cerca
  l'aggiornamento (c'è anche il tasto «Aggiornamento X disponibile» di prima, che si accende quando ne trova uno). Dopo un aggiornamento
  le novità si aprono da sole, una volta. Il «Cerca aggiornamenti» è uscito dalle Impostazioni.
- **README nuovo**: gli screen nuovi (home, impostazioni, archivi con le miniature, slider della compressione, lettore) e i **download
  diretti** (il file parte subito, senza passare dalla pagina delle release).
- Dentro: `Davanti.cs`, `Novita.cs`, `ui/src/lettore/scaletta.ts` (la scaletta condivisa da musica e video), `ui/src/app/novita.ts`;
  comando `fotogrammaA` del lettore; `DAP_DATI` sposta la cartella dei dati (per le prove e le foto del README) e `DAP_DIM` dà la misura
  alla pagina in `scripts/schermo.mjs`.

## 2.0.0 — 29 ottobre 2026

- **Gli archivi ora li apre 7-Zip, quello vero.** Dentro il programma c'è 7-Zip: ZIP, 7Z, RAR, TAR, GZ, XZ, ZST, CAB e ISO si guardano
  dentro e si estraggono senza intoppi, **anche con le accentate nei nomi** (prima un 7z con «è» o «à» non si riusciva a estrarre dal lettore)
  e anche i `.tar.gz`. «Estrai qui» e il trascinamento fuori dall'archivio estraggono una volta sola, nel posto giusto.
  Gli archivi con la password (7Z, ZIP, RAR cifrati) li riconosce e te lo dice; inserire la password arriverà dopo.
- **Le miniature dentro gli archivi.** Aprendo un archivio nel lettore, le foto, i video e i PDF che ci sono dentro hanno la loro
  figurina (si chiede solo quando la riga si vede, e solo per file non enormi).
- **Lo slider della compressione.** Scegliendo ZIP, 7Z, TAR.GZ o TAR.XZ c'è una slider da *Nessuna* (archivia soltanto) a *Massima*.
  Vale anche per un archivio che hai già: uno ZIP debole, ricompresso al massimo (ZIP o 7Z), pesa meno con dentro le stesse cose; l'originale non si tocca.
- **La musica suona solo il brano che apri.** Prima, aprendo una canzone in una cartella con altre, partivano anche tutte le altre. Adesso
  si ferma a fine brano; il tasto **Di seguito** (T) fa suonare anche gli altri uno dopo l'altro. La scaletta a destra resta chiusa finché non la
  apri (L), non si riapre da sola al prossimo avvio, ha il suo tasto «Di seguito», si scorre con le frecce su e giù e non schiaccia più il player.
- **Impostazioni più semplici: un tasto «Aggiusta tutto».** Mette a posto in un colpo il tasto destro (classico e di Windows 11), il menu
  «Apri con» e le app predefinite, e per ogni passo dice com'è andata. Dice anche quanti tipi di file si aprono davvero col lettore
  (prima era un'incognita). Spariscono «Menu classico ovunque», «Riavvia Esplora file» e le righe doppie: su Windows 11 resta un solo
  interruttore, **Mostra tutti i menu del tasto destro** (poi chiede di riavviare Esplora file). **I suoni sono spenti di partenza**
  (anche per chi li aveva accesi), si riaccendono da Impostazioni.
- **La home.** Restano la colonna a sinistra, il logo che gira e il riassunto; in più: le sei strade (video, musica, foto, PDF,
  documenti, archivi: il selettore si apre già sul tipo giusto), «apri un file nel lettore», gli **ultimi file convertiti** con quanto pesavano
  e quanto pesano (e un tasto per guardarli o mostrarli nella cartella) e **come sta il PC** (scheda video, 7-Zip, Word o LibreOffice,
  sistema, versione).
- Dentro: `SetteZip.cs`, `MiniatureArchivio.cs`, `Storico.cs`, `ui/src/app/home.ts`, `ui/src/app/pannelli/archivio.ts`,
  `scripts/prendi-7zip.ps1` (la CI prende 7-Zip da sola). `System.Security.Cryptography.Pkcs` aggiornato (la 8.0.1 aveva due vulnerabilità note).
  Il numero salta a 2.0.0 perché il motore degli archivi è tutto nuovo e questa è la base per Linux e Mac.

## 1.9.1 — 21 ottobre 2026

- **Video e brani si scorrono col cursore, come in Finder.** Nell'anteprima a destra non ci sono più solo le 12 figurine:
  passa il cursore sulla figura grande (o sulla barra sotto) e il **fotogramma cambia in tempo reale** in base alla posizione.
  Sui **brani** la figura è l'**onda di tutto il pezzo** con un cursore magenta che ci scorre sopra, e **si sente il suono
  del punto dove sta il cursore**. Un **clic riproduce** da lì (video con l'audio, brani), un altro clic mette in pausa;
  doppio clic apre il lettore. Dalla tastiera: sulla barra frecce ±5 s (Maiusc ±30 s), Home/Fine, Spazio o Invio per
  riprodurre. Se la pagina non sa suonare quel video (certi codec) lo scrub passa a FFmpeg, un fotogramma per volta
  (conta sempre l'ultimo posto dove sei); se non sa suonare il brano, il clic apre il lettore come prima.
- **Passare da una parte all'altra senza intoppi.** Il clic su un file della coda non riporta più la lista in cima e non
  perde il tasto messo a fuoco (prima ogni ridisegno li azzerava: con la tastiera non si andava avanti). Mentre si
  converte, o a lavoro finito, scegliere un altro file cambia solo la selezione e l'anteprima: la piastra con le bobine
  non si rifà più da capo. I file della coda ora si raggiungono con Tab e si scelgono con Invio o Spazio; **frecce su/giù**
  passano da un file all'altro, **frecce destra/sinistra** fra categorie, formati e bottoni delle regolazioni.
  **Invio** non fa più partire la conversione quando sei su un tasto o su un file (prima premere Invio su «Aggiungi file»
  o su un formato convertiva); da qualunque punto si converte con **Ctrl+Invio**.
- Dentro: `colonna.ts` (lo scrub), comandi `fonteAnteprima` e `scrubAnteprima`, `IndiceAnteprima.Fotogramma` e
  `OndaIntera`; il convertitore ora serve i file all'anteprima da `https://dap.file/` come il lettore (`Risorse`);
  `conservaVista` in `scheletro.ts`.

## 1.9.0 — 21 ottobre 2026

- **La minimappa, come in VS Code.** Nell'anteprima a destra dei testi (e nel lettore, sui TXT, Markdown, registri e codice) a
  lato del testo c'è la sua **miniatura**: una riga ogni due pixel, un pixel per carattere. Il **riquadro** viola è quello che
  vedi; **clic o trascinamento** ti portano lì. I risultati di Ctrl+F sono **segni dorati** sulla miniatura (quello scelto è
  arancione). Le righe lunghe vanno a capo anche nella miniatura (una riga sola da 1 KB, o da sei milioni di caratteri, si
  vede come un blocco, non come un filo), e un registro da 100.000 righe si schiaccia in 30 ms circa.
- **Anteprima per tutti i file, non solo i testi.** Scegli un file qualunque nella coda e a destra c'è la stessa colonna con le
  figurine cliccabili: le **pagine** di un PDF (fino a 400), **12 fotogrammi** di un video, **12 onde** di un brano; foto,
  documenti, cartelle e il resto hanno una figura sola (l'anteprima o l'icona di Esplora file). Il clic sulla figurina la
  porta in grande, le frecce e PagUp/PagGiù scorrono l'elenco, il clic sulla figura grande apre il lettore. Le figurine si
  chiedono solo quando entrano in vista, una alla volta.
- Dentro: `IndiceAnteprima.cs` (motore), comandi `indiceAnteprima` e `voceAnteprima`, `ui/src/app/minimappa.ts` (i conti,
  senza finestre), `minimappa-vista.ts` (il disegno), `colonna.ts` (le figurine). Prove: `scripts/prova-minimappa.mjs`
  (node, 10 prove) e `ProveIndice.cs` (5 prove che fanno davvero PDF, video, brano, foto e file qualunque).

## 1.8.1 — 21 ottobre 2026

- **Il controllo aggiornamenti non si ferma più all'avvio.** L'app guarda le release di GitHub appena si apre, poi ogni
  4 ore finché resta aperta (e riprova dopo 20 minuti se eri offline). Non blocca niente e se non c'è rete sta zitta.
- **Il badge in alto dice la versione.** «Aggiornamento 1.9.0 disponibile» compare appena la versione è trovata (prima
  aspettava il download, e nel convertitore la scritta aveva perso il numero), sia nel convertitore sia nel lettore; un
  clic la installa e riapre. Il lettore lo mostra anche se la versione era già stata trovata prima che lo aprissi.
- Per provare il badge senza una release vera: `DAP_AGGIORNAMENTO_FINTO=9.9.9` avviando l'app (non installa niente).

## 1.8.0 — 21 ottobre 2026

- **L'anteprima a destra, come in VS Code.** Scegli un testo nella coda (TXT, Markdown, registri, codice, CSV, JSON,
  sottotitoli…) e a destra si apre il suo contenuto, coi **numeri di riga**. Scorre liscio anche su un registro da
  6 MB e più di centomila righe (si disegnano solo le righe che si vedono; le righe lunghissime si spezzano). **Ctrl+F**
  (o la lente) apre la ricerca: evidenzia tutti i risultati, dice «3 di 1.200», **Invio / Maiusc+Invio** (o F3 e le
  frecce) vanno al prossimo e al precedente, **Aa** distingue maiuscole e minuscole, Esc chiude. Si allarga trascinando il
  bordo sinistro (doppio clic: larghezza di partenza); chiusa, resta una linguetta per riaprirla. In fondo dice la
  codifica del file (UTF-8, UTF-16, Windows-1252…), CRLF o LF, righe e peso. Il tasto destro sul testo scelto fa Copia.
- **I file di testo si leggono giusti.** I TXT UTF-16 (quelli «Unicode» del Blocco note, l'output di PowerShell con `>`)
  uscivano pieni di caratteri strani in PDF, DOCX e TXT, e i CSV in UTF-16 diventavano colonne di byte vuoti. I vecchi
  TXT ANSI non perdono più €, virgolette e trattini (si leggevano come Latin-1 invece che Windows-1252). Ora c'è un posto
  solo che decide la codifica (`Codifiche.cs`): lo usano convertitore, lettore e salvataggio.
- **Più file sono testi**: `.log .ini .yaml .xml .py .cs .nfo` e simili prima erano «altri file» (si potevano solo
  comprimere), ora si convertono in PDF, HTML, DOCX e TXT. Anche i file senza estensione (LICENSE, LEGGIMI) se dentro
  c'è testo.
- **Il testo non si schiaccia più**: rientri, tab e spazi doppi restano com'erano in PDF, DOCX e HTML; registri e codice
  escono a spaziatura fissa. TXT → TXT non taglia più il rientro della prima riga.

## 1.7.1 — 14 ottobre 2026

- **Ogni file si apre in una finestra nuova.** Prima, se il lettore era già aperto (per esempio con un Markdown), aprire
  una foto sostituiva quello che stavi leggendo. Ora il vecchio resta com'è e il nuovo si apre accanto, un po' più in
  basso a destra. Più file scelti insieme in Esplora file restano nella stessa finestra, uno dopo l'altro.
- **Tasto destro → Copia** nel lettore: sul testo scelto (e dentro il campo di modifica) compare il menu con Copia,
  Taglia, Incolla e Seleziona tutto. Fuori dal testo non compare niente.
- **Il testo scelto si vede**: sulla carta bianca dei documenti e dei Markdown era quasi invisibile, ora è viola pieno
  con le lettere bianche; sui fondi scuri è più carico.

## 1.7.0 — 14 ottobre 2026

- **«Aprire tutto con DaProd?» applica davvero**, col modo che Windows prevede per gli amministratori: con **OK** Windows
  chiede il permesso (una volta) e l'app scrive la lista dei tipi accesi come *regola delle associazioni predefinite*.
  Si applica quando ti **disconnetti e riaccedi**. Finché la regola c'è, quei tipi tornano al lettore a ogni accesso anche
  se li cambi a mano: **Togli la regola** (nella stessa finestra) la cancella. Se hai detto no al permesso non cambia
  niente; se c'è già una regola di altri (un'azienda) non la tocca; su Windows Home, che la ignora, si apre la pagina
  delle impostazioni come prima. Il file sta in `C:\ProgramData\DaProd\Convertitore\associazioni.xml`.

## 1.6.1 — 14 ottobre 2026

- La finestra «Aprire tutto con DaProd?» dice le cose come stanno: l'app registra i tipi accesi e apre la pagina di
  Windows con quei tipi in lista, ma la scelta finale di ogni predefinito è di Windows e resta a te. Il tasto si
  chiama «OK, registra e vai a Windows» (prima prometteva «applica»).

## 1.6.0 — 14 ottobre 2026

- **Ritagliare le foto.** Nel ritocco (**D**) c'è lo strumento *Ritaglia*: trascini il rettangolo che resta (con Maiusc
  è un quadrato), fuori la foto si scurisce con il bordo tratteggiato. Il ritaglio entra in **Salva le modifiche** e in
  **Copia** come tutti gli altri segni (annulla e rifai compresi): il file o gli appunti prendono solo il pezzo scelto.
- **Incollare i file nella piastra.** Tasto destro → **Incolla** (o **Ctrl+V**): entrano in coda i file copiati in
  Esplora file, come se li trascinassi. Nello stesso menu c'è *Aggiungi file…*.
- **«Aprire tutto con DaProd?»**: il tasto *App predefinite* delle impostazioni non butta più nella pagina di Windows.
  Mostra una lista ordinata dei tipi (video, audio, foto, PDF, documenti…) con un interruttore ciascuno; con **OK**
  l'app registra solo i tipi accesi e apre la pagina di Windows già pronta. Windows le app predefinite le fa scegliere
  a te: l'ultimo clic su «Imposta predefinita» resta tuo, e l'app non aggira la protezione.
- **Markdown sistemato.** L'indice a sinistra, con tante intestazioni, si schiacciava in puntini illeggibili: ora le righe
  hanno la loro altezza. E ogni blocco di codice ha il tasto **Copia** in alto a destra, come su GitHub.

## 1.5.0 — 14 ottobre 2026

- **Ritoccare le foto nel lettore.** **D** (o la matita in alto) apre la barra del ritocco: penna, evidenziatore,
  freccia, riquadro, ovale e **testo** (si clicca dove scrivere, Invio lo fissa; col contorno si legge su qualsiasi
  foto), otto colori più uno a scelta, lo spessore, annulla e rifai (**Ctrl Z**, **Ctrl Y**). Due tasti chiari:
  - **Salva le modifiche** (Ctrl S): scrive i segni sulla foto e **sostituisce il file originale**, dopo una conferma.
    Nelle JPG i dati dello scatto (data, fotocamera, posizione) restano. HEIC, BMP, GIF e gli altri formati che non si
    riscrivono salvano **accanto**, in PNG, e l'originale non si tocca;
  - **Copia** (Ctrl C): mette la foto ritoccata **negli appunti, senza salvare** niente.
  Finché i segni non sono salvati compare «Foto ritoccata · non ancora salvata», e il lettore chiede prima di cambiare
  foto o di chiudere. Girare e ritoccare insieme non si può (i segni stanno sulla foto dritta): il lettore lo dice.
- **Archivi: si trascina fuori davvero.** Nel lettore scegli cartelle e file dentro lo ZIP (o 7Z, RAR…) e li trascini
  **fuori dalla finestra**, nella cartella di Esplora file, sul desktop o in un'altra app: escono come file veri, dove
  li lasci (una cartella porta con sé tutto quello che c'è dentro). Mentre trascini si estrae in una cartella temporanea;
  sulla zona in alto escono accanto all'archivio, come prima. Provato col mouse vero, dal lettore a Esplora file.
- **Lo slider del peso è tornato anche per le foto** (JPG, WEBP, AVIF, JXL, HEIC): come nel video c'è **PESO
  FINALE** (dici quanto deve pesare e la qualità viene da sé) e **QUALITÀ** (scegli la qualità e leggi quanto peserà),
  col display a sette segmenti, la lancetta e i preset (metà, un quarto, 100 KB, 300 KB, 1 MB…). Il peso non è
  un'indovinata: il motore codifica davvero la foto, e il file che esce pesa quanto detto (provato). Se per stare nel
  peso va rimpicciolita, lo dice. Il peso di partenza è quello della qualità solita.
- **La finestra non si allarga più oltre lo schermo.** Un file dal nome lunghissimo e senza spazi (come quelli scaricati
  da certi siti) allargava tutta la pagina oltre la finestra: sparivano i tasti a destra e **CONVERTI**, e si vedevano
  solo mettendo lo schermo intero. Ora il nome si accorcia coi puntini. Vale anche per il lettore.
- **Refactoring.** La piastra (main.ts, 1100 righe) è divisa in pezzi in ui/src/app/: stato, scheletro, controlli,
  i pannelli di video e foto, lavoro, risultati, impostazioni, modo rapido. Lo slider del peso è un controllo solo
  per video e foto. Nel motore la ricerca del peso (Immagini.Cerca) è una sola per la conversione e per la stima, le
  conversioni di FFmpeg per le foto stanno in una funzione sola e la «alfa diritta» delle foto in un posto solo.
  Prove nuove: stima del peso, ritocco, trascinamento.

## 1.4.0 — 14 ottobre 2026

- **Svuotare la coda in un colpo**: il tasto *svuota* accanto a SORGENTE, oppure **Ctrl+Canc**. **Canc** toglie
  solo il file scelto. I file sul disco non si toccano, si tolgono solo dalla lista.
- **Archivi, si estrae quello che serve**: nel lettore si scelgono cartelle e file (clic, **Ctrl** per più) e si
  trascinano sulla zona in alto, *Trascina qui cartelle e file per estrarli subito*: escono in una cartella accanto
  all'archivio, e si apre Esplora file. Il pulsante *Estrai* estrae la scelta, o tutto se non hai scelto niente.
  Non è ancora possibile trascinarli fuori dall'app, nella cartella di Esplora file: si trascinano sulla zona.
- **I testi si leggono bene**: TXT e Markdown hanno il carattere da libro (16 px, interlinea larga, a capo), con
  testo chiaro su fondo scuro.
- **I testi si modificano e si salvano**: **Modifica** (F2) apre il testo come campo di scrittura; **Salva** (Ctrl+S)
  scrive nel file. Il file torna con la sua codifica (UTF-8, UTF-16, col BOM se c'era) e con le sue fini riga
  (CRLF o LF). Un file in una codifica vecchia si salva in UTF-8, e il lettore lo dice. Con modifiche non salvate
  il lettore chiede prima di cambiare file, di chiudere o di tornare a leggere. Non si modificano i testi troppo
  grandi (oltre 6 MB).
- **Lettore musicale riscritto**: niente più disco e braccio. Copertina a sinistra (o una mattonella col formato
  se non c'è), titolo e artista a destra, la forma d'onda che è anche la barra di avanzamento, i comandi, il volume,
  e sotto lo spettro che balla.
- **La rotella cambia il volume** nel lettore musicale, ovunque sulla pagina (sulla scaletta scorre la lista).
- Nel lettore si corregge il contrasto della pillola del formato sulla barra bianca, e la percentuale di compressione
  degli archivi appare solo quando è davvero un risparmio.

## 1.3.0 — 9 ottobre 2026

- **Girare e salvare nel file.** Nel lettore **R** gira a destra, **Maiusc R** a sinistra, **M** specchia (foto); si
  vede subito e compare «non ancora salvata» con **Salva nel file** (Ctrl S) e **Annulla**. Il file si riscrive solo
  se premi Salva:
  - **foto JPG e TIFF**: cambia solo l'orientamento scritto nel file, senza ricodificare (stessi pixel, stesso peso);
  - **foto PNG e BMP**: i pixel si girano davvero, senza perdite;
  - **video MP4 e MOV**: la rotazione si scrive dentro il file copiando i flussi, quindi niente ricodifica e pochi
    secondi anche per un film grosso; il video ricaricato è già in piedi anche negli altri programmi;
  - **PDF**: giri la pagina (R) o tutte (Ctrl R) e il PDF si salva girato, dopo una conferma;
  - dove il formato non tiene la rotazione (GIF, WEBP, AVIF, HEIC, AVI…) il lettore lo dice e la rotazione resta
    solo da guardare; per salvarla si converte (Ctrl E).
- **Tagliare un video senza perdere niente**: **Z** segna l'inizio, **X** la fine (si vede il pezzo sulla linea del
  tempo), **Salva il pezzo** lo mette accanto come «(tagliato)». Si copiano i flussi, quindi parte dal fotogramma
  chiave più vicino. L'originale non si tocca.
- **Fai subito (U)**: in alto nel lettore il fulmine apre le conversioni rapide del tipo di file (MP4, Metà peso,
  JPG, Più piccola, Solo il testo…): partono nella finestrella in basso a destra senza lasciare il lettore.
- **La barra in alto è bianca** anche col tema scuro, nel convertitore e nel lettore; il resto resta scuro. La barra
  del lettore adesso è una riga sua: la foto e il video non ci finiscono più sotto, e a schermo intero sparisce.
- Nel video «Riprendi da dove eri» passa da R a **Invio** (R adesso gira). **Maiusc + lettera** ora funziona per
  davvero nei tasti rapidi (prima «Maiusc R» scattava come «R»).

## 1.2.0 — 8 ottobre 2026

- **Icone vere, una per ogni tipo di file.** Non è più la stessa icona dell'app per tutto: video, musica, foto, PDF,
  Word, presentazioni, tabelle, testo, codice, sottotitoli, archivi, caratteri e modelli 3D hanno ognuno la sua
  (un foglio viola notte col disegnino nella tinta del tipo), nitida da 16 a 256 pixel.
- **Anteprime vere in Esplora file** (viste a icone medie, grandi ed extra grandi):
  - **video**: il fotogramma vero al 10%, con la pellicola sopra e sotto, il tasto play e la durata; i video in
    verticale (9:16) e quelli girati dal telefono restano in verticale;
  - **musica**: la **forma d'onda reale** del brano, barrette come i vocali di WhatsApp, col tasto play e la durata;
  - **foto**: la foto, girata come l'ha scattata il telefono (anche HEIC, RAW, PSD, TGA, AVIF…);
  - **modelli 3D** (GLB, GLTF, STL, OBJ con i colori del .mtl, PLY, 3MF): il modello disegnato a tre quarti su uno
    sfondo da studio con l'anello magenta sotto;
  - **PDF**, **Word/Excel/PowerPoint e LibreOffice** (l'anteprima che c'è dentro il file, se no una pagina disegnata col
    testo vero), **testo e codice** colorato, **CSV** a tabella, **caratteri** con «Aa» nel carattere stesso,
    **ZIP** con l'elenco di quello che c'è dentro. Dove Windows o Office ne hanno già una, resta la loro.
  - Si registrano insieme a «Apri con» (stessa leva nelle impostazioni). «Riavvia Esplora file» butta le anteprime
    e le icone vecchie, così ripartono nuove.
- **Il lettore e i video 9:16**: un video in verticale usciva dalla finestra e si vedeva tagliato (il video
  prendeva l'altezza che avrebbe a tutta larghezza invece di quella della finestra). Ora ci sta tutto, intero, anche
  quello girato col telefono e quello che passa da FFmpeg.
- Nuovi gruppi in «Apri con»: presentazioni, codice e sottotitoli hanno il loro nome e la loro icona.

## 1.1.0 — 8 ottobre 2026

- **Il lettore**: DaP Convertitore adesso guarda anche, qualsiasi file. Doppio clic (o *Apri con → DaP
  Convertitore*, o l'occhio accanto a un file nella piastra) e si apre una finestra nera e leggera con una pagina
  fatta per quel tipo di file:
  - **video** nel cinema: comandi che spariscono, la luce intorno, riprende da dove eri, l'episodio dopo,
    sottotitoli (dentro o accanto), tracce audio, velocità, fotogramma per fotogramma, S salva il fotogramma in PNG.
    AVI, WMV e i codec che WebView2 non conosce li traduce FFmpeg al volo con la scheda video, e si scorrono lo
    stesso;
  - **musica** sul giradischi: copertina sul disco, braccio che avanza, forma d'onda da scorrere, analizzatore di
    spettro, scaletta della cartella, un brano dopo l'altro. WMA, APE, ALAC passano in FLAC;
  - **foto** sul tavolo luminoso: zoom col mouse, trascina, doppio clic al 100%, frecce per la dopo, presentazione,
    gira, copia, sfondo del desktop, dati dello scatto e istogramma. HEIC, RAW, TIFF, PSD compresi;
  - **PDF** sulla scrivania, **Word** letto da sé (o impaginato con Office/LibreOffice), **presentazioni**, **fogli**
    Excel/CSV, **codice** coi colori, **Markdown**, **JSON** ad albero, **pagine web**, **sottotitoli**, **archivi**
    senza estrarli (con «Estrai qui»), **caratteri** con «Installa», **modelli 3D** (GLB, STL, OBJ…), e tutto il
    resto byte per byte;
  - tasti rapidi ovunque (premi **?**), Pag su/giù per il file prima e dopo, F11 schermo intero, Canc nel Cestino,
    Ctrl+E nel convertitore.
- Il lettore si mette in **«Apri con»** di Windows per tutti i tipi che conosce, senza prendersi niente da solo.
  Per farlo diventare quello del doppio clic, al posto di Foto e Lettore multimediale: *Impostazioni → App
  predefinite*, e scegli tu.
- **L'originale non va più nel Cestino da solo**: accanto al risultato c'è il cestino (primo clic «sicuro?»,
  secondo clic lo sposta), anche nella finestrella del menu rapido; con più file «ORIGINALI NEL CESTINO». Chi lo
  vuole automatico lo riaccende in *Impostazioni → L'originale nel Cestino da solo*.
- **Sistemato lo schermo nero delle impostazioni**: dopo «Attiva», «Togli» o «Riavvia» restava un velo scuro sotto
  l'altro, e ci volevano più clic per tornare alla piastra. Ora il velo è uno, il secondo non scurisce, Esc chiude
  quello sopra.
- I colori per categoria (ciano le foto, magenta i video, verde l'audio…) adesso si vedono davvero: prima era
  tutto oro per un errore nel modo di passarli.
- Se arriva un file nuovo mentre la piastra dice «FATTO», riparte da sola con quello.

## 1.0.3 — 8 ottobre 2026

- **Un comando solo nel tasto destro di Windows 11**, come fa VS Code: **DaP Convertitore ›**, e dentro
  **Apri nel convertitore…** in cima e sotto, dopo una linea, le conversioni al volo del tipo di file. Con due
  comandi separati il menu nuovo non li mostrava.
- Il sottomenu non resta più vuoto quando Esplora file chiede lo stato a un'istanza del comando e le voci a
  un'altra.
- **Impostazioni → Tasto destro**, tutto in un posto:
  - **Menu classico ovunque**: il tasto destro apre subito il menu di Windows 10, tutte le voci a un clic.
    Spegnendolo si torna al menu di Windows 11.
  - **Riavvia Esplora file**: Esplora file legge le voci del menu solo quando parte. Dopo «Attiva» (e dopo il
    menu classico) il convertitore propone da solo di riavviarlo.
- Un diario per capire cosa chiede Esplora file al menu: si accende mettendo un file `menu.debug` accanto
  all'app, scrive in `%TEMP%\DaP Convertitore\menu-diario.txt`.

## 1.0.2 — 7 ottobre 2026

- **Nel tasto destro di Windows 11**, quello nuovo, senza passare da «Mostra altre opzioni»: in cima
  **DaP Convertitore** apre la piastra coi file scelti, sotto **Converti al volo ›** ha le conversioni rapide
  del tipo di file. Si accende col tasto **Attiva** (alla prima apertura o in Impostazioni): Windows chiede il
  permesso **una volta sola**, per fidarsi del certificato di DaProd; poi aggiornamenti compresi non chiede più
  niente. Sulle versioni più nuove di Windows 11 le voci delle app possono stare sotto «Estensioni app»: da
  *Impostazioni → Personalizzazione → Menu contestuale* le porti in cima.
- Dal menu nuovo tutti i file scelti arrivano in un colpo solo, anche centinaia.
- **L'originale nel Cestino**: a conversione riuscita, quando il convertito ne prende il posto (MOV → MP4,
  PNG → JPG, DOCX → PDF…), l'originale va nel Cestino di Windows, da dove lo ripeschi se serve. È acceso di
  partenza e si spegne in Impostazioni. Non succede quando ne tiri fuori un pezzo (l'audio di un video, il testo
  di un PDF, le pagine in JPG), quando unisci più file, e su chiavette e dischi di rete (lì Windows il Cestino
  non ce l'ha e cancellerebbe davvero). Prima di premere CONVERTI lo dice, e alla fine pure.
- «Comprimi in ZIP / 7Z» anche per i file che il convertitore non conosce.

## 1.0.1 — 7 ottobre 2026

- Le misure delle foto in coda (larghezza × altezza) ci sono anche per i formati che Windows da solo non legge,
  come WEBP e AVIF sui PC senza le estensioni dello Store: le chiede a FFprobe.
- **Gli aggiornamenti arrivano da soli**: all'avvio il convertitore guarda se c'è una versione nuova e la
  scarica in silenzio (solo la differenza). In alto compare **«Nuova X · riavvia»**; se non lo premi, la versione
  nuova si mette da sola quando chiudi l'app. Dalla 1.0.0 si passa una volta a mano: *Impostazioni → Cerca
  aggiornamenti* (la 1.0.0 non guardava da sola).
- Nelle GitHub Actions, se qualche prova non passa compare un avviso giallo invece di un verde finto.
- La prima release che esce da sola da GitHub Actions: da qui in avanti ogni versione nuova si pubblica così.

## 1.0.0 — 7 ottobre 2026

La prima. Tasto destro su un file → **DaP Convertitore**, e il file convertito compare accanto all'originale
col nome `(convertito)`.

- **Video**: MP4, MKV, WEBM, MOV, GIF, e l'audio in MP3 o WAV. Lo **slider del peso finale** decide quanto
  deve pesare (10 MB per Discord, 4 GB per la chiavetta, metà, un quarto…): il convertitore calcola il bitrate,
  scende di risoluzione se serve, e la **lancetta della qualità** dice come verrà prima di premere. Se a fine
  lavoro ha sforato anche di poco, lo rifà più stretto da solo: **ci sta di sicuro**.
- **Scheda video**: NVENC per NVIDIA (H.264, H.265 e AV1 sulle RTX 40), AMF per AMD, Quick Sync per Intel,
  provati davvero all'avvio. Se la scheda si rifiuta, si riprova con meno pretese e poi con la CPU.
- **VELOCE**: quando il video può passare tale e quale (MKV → MP4, MOV → MP4…) cambia solo la scatola, in
  pochi secondi e senza perdere niente.
- **HDR**: in H.265 e AV1 resta HDR a 10 bit; in H.264 diventa SDR coi colori giusti.
- **Audio**: MP3, M4A, OPUS, OGG, FLAC, WAV, AIFF, WMA, con volume uniforme (-14 LUFS) e copertina tenuta.
- **Foto**: JPG, PNG, WEBP, AVIF, JPEG XL, HEIC, TIFF, BMP, GIF, ICO (con tutte le misure dentro) e PDF.
  Legge anche HEIC dell'iPhone e i RAW delle fotocamere col motore di Windows, e gira la foto come la vede il
  telefono. **Peso massimo**: la qualità scende (e se non basta la foto si rimpicciolisce) finché ci sta.
- **OCR**: il testo scritto in una foto o in un PDF scansionato diventa TXT, con l'OCR di Windows.
- **PDF**: pagine in JPG o PNG, testo, Word (DOCX), e più PDF uniti in uno. Tante foto in un PDF solo.
- **Documenti, fogli, presentazioni**: con Microsoft Office se c'è, se no con LibreOffice, senza finestre.
- **Testi**: Markdown, TXT e HTML in PDF impaginato pulito, Word o pagina web.
- **Dati**: CSV, TSV, JSON ed Excel avanti e indietro; il CSV esce col punto e virgola, come lo vuole Excel in Italia.
- **Sottotitoli**: SRT, VTT, ASS, solo le battute in TXT, e i sottotitoli tirati fuori da un video.
- **Archivi**: ZIP, 7Z, RAR, TAR, GZ, XZ, ISO… estrai qui, o rifai in ZIP, 7Z, TAR.GZ, TAR.XZ. Cartelle in ZIP o 7Z.
- **La piastra**: le bobine girano veloci quanto la conversione, display a sette segmenti, strumenti a
  lancetta per CPU, encoder e decoder (gli stessi contatori di Gestione attività), confronto **prima/dopo**
  col divisore da trascinare.
- **Il menu rapido**: dal sottomenu del tasto destro la conversione parte subito in una finestrella in
  basso a destra, e alla fine arriva la notifica di Windows con «Apri» e «Mostra nella cartella».
- Avanzamento anche sull'icona nella barra delle applicazioni, «Invia a → DaP Convertitore», trascina i
  file nella finestra, aggiornamenti automatici dalle release.
