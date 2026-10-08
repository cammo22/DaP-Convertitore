# Changelog

Le novità di ogni versione, dette come stanno. La più nuova sta in alto.

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
