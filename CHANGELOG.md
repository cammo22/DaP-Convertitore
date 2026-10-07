# Changelog

Le novità di ogni versione, dette come stanno. La più nuova sta in alto.

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
