# DaP Convertitore su Linux

È lo stesso programma di Windows: la stessa finestra. Si apre un file, si sceglie in cosa diventa, si preme Converti.

## Cosa c'è

- Video, musica e sottotitoli (FFmpeg, è dentro il programma).
- Foto (JPG, PNG, WEBP e gli altri che FFmpeg sa fare).
- Testo, tabelle semplici, archivi (7-Zip è dentro il programma).
- PDF: il testo e l'unione di più PDF. Le pagine come immagini, se c'è LibreOffice.
- Documenti di Word, fogli e presentazioni, se c'è LibreOffice (`soffice`).
- Il tasto destro in Nautilus, Nemo e Dolphin.

L'avatar, l'OCR delle scansioni e il lettore completo restano su Windows.

## Come si installa

1. Scarica `DaPConvertitore-linux.AppImage` dalla pagina delle release.
2. Rendilo avviabile: tasto destro sul file → Proprietà → Permetti l'esecuzione come programma.
   Oppure, nel terminale: `chmod +x DaPConvertitore-linux.AppImage`
3. Serve WebKit, che quasi tutte le distribuzioni hanno. Se la finestra non si apre:
   - Ubuntu o Debian: `sudo apt install libwebkit2gtk-4.1-0`
   - Fedora: `sudo dnf install webkit2gtk4.1`
4. Doppio clic sull'AppImage.

Il tasto destro sui file: apri il programma e, nelle Impostazioni, «Aggiusta tutto». Oppure, nel terminale, una volta:

`./DaPConvertitore-linux.AppImage --menu`

Poi riavvia il gestore dei file se la voce non compare subito.

Gli aggiornamenti: se ne esce uno, compare il tasto in alto, come su Windows. Non si chiede niente all'API di GitHub: si guarda solo la pagina della release.
