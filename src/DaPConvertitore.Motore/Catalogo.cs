namespace DaP.Convertitore;

/// <summary>Che tipo di file è. Decide quali formati si offrono e quale motore li fa.</summary>
public enum Categoria { Video, Audio, Immagine, Pdf, Documento, Foglio, Presentazione, Testo, Dati, Sottotitoli, Archivio, Cartella, Altro }

/// <summary>Chi fa il lavoro.</summary>
public enum Motore { Video, Audio, Immagini, Pdf, Office, Testo, Dati, Sottotitoli, Archivi }

/// <summary>Un formato d'uscita. L'id è "categoria.cosa" ed è lo stesso nell'interfaccia, nel menu e nelle impostazioni.</summary>
public sealed record Formato(
    string Id,
    Categoria Categoria,
    string Etichetta,
    string Descrizione,
    string Estensione,
    Motore Motore,
    bool Unisce = false,
    IReadOnlySet<string>? SoloPer = null)
{
    /// <summary>L'uscita è una cartella (pagine di un PDF, archivio estratto).</summary>
    public bool Cartella => Estensione.Length == 0;

    /// <summary>
    /// Il convertito prende il posto dell'originale (MOV → MP4, PNG → JPG, DOCX → PDF): solo allora, se l'opzione è
    /// accesa, l'originale va nel Cestino. Non lo fanno le cose che ne tirano fuori un pezzo (l'audio di un video, il
    /// testo di un PDF, le pagine in JPG), quelle che uniscono tanti file, gli archivi estratti e le cartelle compresse.
    /// </summary>
    public bool Sostituisce => !Unisce && Categoria is not (Categoria.Cartella or Categoria.Altro) && !Catalogo.Derivati.Contains(Id);
}

/// <summary>Una voce del menu del tasto destro: un formato con le sue scelte già fatte.</summary>
public sealed record Rapida(string Id, Categoria Categoria, string Etichetta, string Formato, string? Preset = null);

/// <summary>
/// L'elenco di tutto quello che il convertitore sa fare. È l'unico posto dove si dichiara:
/// l'interfaccia, il menu del tasto destro e le prove leggono da qui.
/// </summary>
public static class Catalogo
{
    static readonly Dictionary<string, Categoria> estensioni = new(StringComparer.OrdinalIgnoreCase);

    static void Metti(Categoria c, string elenco)
    {
        foreach (var e in elenco.Split(' ', StringSplitOptions.RemoveEmptyEntries)) estensioni["." + e] = c;
    }

    static Catalogo()
    {
        Metti(Categoria.Video, "mp4 m4v mov mkv webm avi wmv flv mpg mpeg m2ts mts ts m2t 3gp 3g2 ogv vob mxf f4v asf divx rmvb rm dv y4m");
        Metti(Categoria.Audio, "mp3 wav flac aac m4a m4b ogg oga opus wma aiff aif aifc ape wv amr ac3 eac3 dts mka caf au ra tta mpc weba");
        Metti(Categoria.Immagine, "jpg jpeg jpe jfif png apng webp bmp dib gif tif tiff heic heif hif avif jxl ico tga psd dds exr hdr jxr wdp svg qoi pbm pgm ppm pnm " +
                                  "cr2 cr3 crw nef nrw arw srf sr2 dng orf rw2 raf srw pef x3f 3fr erf kdc mos mrw rwl iiq");
        Metti(Categoria.Pdf, "pdf");
        Metti(Categoria.Documento, "doc docx docm dot dotx odt ott rtf wpd wps pages");
        Metti(Categoria.Foglio, "xls xlsx xlsm xlsb ods numbers");
        Metti(Categoria.Presentazione, "ppt pptx pps ppsx odp key");
        Metti(Categoria.Testo, "txt md markdown html htm");
        Metti(Categoria.Dati, "csv tsv json");
        Metti(Categoria.Sottotitoli, "srt vtt ass ssa");
        Metti(Categoria.Archivio, "zip 7z rar tar gz tgz bz2 tbz2 xz txz zst tzst cab iso");
    }

    /// <summary>I formati che tirano fuori un pezzo dell'originale: l'originale serve ancora, non va nel Cestino.</summary>
    public static readonly IReadOnlySet<string> Derivati = new HashSet<string>
    {
        "video.gif", "video.mp3", "video.wav", "video.srt", "img.txt",
        "pdf.jpg", "pdf.png", "pdf.txt", "foglio.csv", "foglio.json", "arch.estrai",
    };

    static IReadOnlySet<string> Solo(string elenco) =>
        elenco.Split(' ').Select(e => "." + e).ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static readonly IReadOnlyList<Formato> Formati =
    [
        // ——— video ———
        new("video.mp4", Categoria.Video, "MP4", "va ovunque", ".mp4", Motore.Video),
        new("video.mkv", Categoria.Video, "MKV", "tiene tutte le tracce", ".mkv", Motore.Video),
        new("video.webm", Categoria.Video, "WEBM", "per il web", ".webm", Motore.Video),
        new("video.mov", Categoria.Video, "MOV", "Apple e montaggio", ".mov", Motore.Video),
        new("video.gif", Categoria.Video, "GIF", "animata, per le chat", ".gif", Motore.Video),
        new("video.mp3", Categoria.Video, "MP3", "solo l'audio", ".mp3", Motore.Audio),
        new("video.wav", Categoria.Video, "WAV", "l'audio, senza perdite", ".wav", Motore.Audio),
        new("video.srt", Categoria.Video, "SRT", "i sottotitoli che ha dentro", ".srt", Motore.Sottotitoli),

        // ——— audio ———
        new("audio.mp3", Categoria.Audio, "MP3", "va ovunque", ".mp3", Motore.Audio),
        new("audio.m4a", Categoria.Audio, "M4A", "AAC, iPhone e Mac", ".m4a", Motore.Audio),
        new("audio.opus", Categoria.Audio, "OPUS", "leggerissimo", ".opus", Motore.Audio),
        new("audio.ogg", Categoria.Audio, "OGG", "Vorbis, giochi e web", ".ogg", Motore.Audio),
        new("audio.flac", Categoria.Audio, "FLAC", "senza perdite, compresso", ".flac", Motore.Audio),
        new("audio.wav", Categoria.Audio, "WAV", "senza perdite, per il montaggio", ".wav", Motore.Audio),
        new("audio.aiff", Categoria.Audio, "AIFF", "senza perdite, Mac", ".aiff", Motore.Audio),
        new("audio.wma", Categoria.Audio, "WMA", "Windows Media", ".wma", Motore.Audio),

        // ——— immagini ———
        new("img.jpg", Categoria.Immagine, "JPG", "va ovunque", ".jpg", Motore.Immagini),
        new("img.png", Categoria.Immagine, "PNG", "senza perdite, con trasparenza", ".png", Motore.Immagini),
        new("img.webp", Categoria.Immagine, "WEBP", "leggera, per il web", ".webp", Motore.Immagini),
        new("img.avif", Categoria.Immagine, "AVIF", "la più leggera", ".avif", Motore.Immagini),
        new("img.jxl", Categoria.Immagine, "JXL", "JPEG XL, il futuro", ".jxl", Motore.Immagini),
        new("img.heic", Categoria.Immagine, "HEIC", "come l'iPhone", ".heic", Motore.Immagini),
        new("img.tiff", Categoria.Immagine, "TIFF", "per la stampa", ".tif", Motore.Immagini),
        new("img.bmp", Categoria.Immagine, "BMP", "grezza", ".bmp", Motore.Immagini),
        new("img.gif", Categoria.Immagine, "GIF", "256 colori", ".gif", Motore.Immagini),
        new("img.ico", Categoria.Immagine, "ICO", "icona di Windows", ".ico", Motore.Immagini),
        new("img.pdf", Categoria.Immagine, "PDF", "un PDF solo con tutte", ".pdf", Motore.Pdf, Unisce: true),
        new("img.txt", Categoria.Immagine, "TXT", "il testo che c'è scritto (OCR)", ".txt", Motore.Pdf),
        new("img.mp4", Categoria.Immagine, "MP4", "la GIF diventa video", ".mp4", Motore.Video, SoloPer: Solo("gif apng")),
        new("img.webm", Categoria.Immagine, "WEBM", "la GIF diventa video", ".webm", Motore.Video, SoloPer: Solo("gif apng")),

        // ——— PDF ———
        new("pdf.jpg", Categoria.Pdf, "JPG", "una foto per pagina", "", Motore.Pdf),
        new("pdf.png", Categoria.Pdf, "PNG", "una immagine per pagina", "", Motore.Pdf),
        new("pdf.txt", Categoria.Pdf, "TXT", "il testo (anche delle scansioni)", ".txt", Motore.Pdf),
        new("pdf.docx", Categoria.Pdf, "DOCX", "da modificare in Word", ".docx", Motore.Office),
        new("pdf.unisci", Categoria.Pdf, "UNISCI", "tutti in un PDF solo", ".pdf", Motore.Pdf, Unisce: true),

        // ——— documenti, fogli, presentazioni ———
        new("doc.pdf", Categoria.Documento, "PDF", "da mandare e stampare", ".pdf", Motore.Office),
        new("doc.docx", Categoria.Documento, "DOCX", "Word", ".docx", Motore.Office),
        new("doc.odt", Categoria.Documento, "ODT", "LibreOffice", ".odt", Motore.Office),
        new("doc.rtf", Categoria.Documento, "RTF", "lo apre chiunque", ".rtf", Motore.Office),
        new("doc.txt", Categoria.Documento, "TXT", "solo il testo", ".txt", Motore.Office),
        new("doc.html", Categoria.Documento, "HTML", "pagina web", ".html", Motore.Office),

        new("foglio.pdf", Categoria.Foglio, "PDF", "da mandare e stampare", ".pdf", Motore.Office),
        new("foglio.xlsx", Categoria.Foglio, "XLSX", "Excel", ".xlsx", Motore.Office),
        new("foglio.ods", Categoria.Foglio, "ODS", "LibreOffice", ".ods", Motore.Office),
        new("foglio.csv", Categoria.Foglio, "CSV", "il primo foglio, in testo", ".csv", Motore.Dati),
        new("foglio.json", Categoria.Foglio, "JSON", "il primo foglio, per i programmi", ".json", Motore.Dati),

        new("pres.pdf", Categoria.Presentazione, "PDF", "da mandare e stampare", ".pdf", Motore.Office),
        new("pres.pptx", Categoria.Presentazione, "PPTX", "PowerPoint", ".pptx", Motore.Office),
        new("pres.odp", Categoria.Presentazione, "ODP", "LibreOffice", ".odp", Motore.Office),

        // ——— testi ———
        new("testo.pdf", Categoria.Testo, "PDF", "impaginato e pulito", ".pdf", Motore.Testo),
        new("testo.html", Categoria.Testo, "HTML", "pagina web", ".html", Motore.Testo),
        new("testo.docx", Categoria.Testo, "DOCX", "Word", ".docx", Motore.Testo),
        new("testo.txt", Categoria.Testo, "TXT", "solo il testo", ".txt", Motore.Testo),

        // ——— dati ———
        new("dati.xlsx", Categoria.Dati, "XLSX", "si apre in Excel", ".xlsx", Motore.Dati),
        new("dati.csv", Categoria.Dati, "CSV", "testo a colonne", ".csv", Motore.Dati),
        new("dati.json", Categoria.Dati, "JSON", "per i programmi", ".json", Motore.Dati),
        new("dati.tsv", Categoria.Dati, "TSV", "colonne col tab", ".tsv", Motore.Dati),

        // ——— sottotitoli ———
        new("sub.srt", Categoria.Sottotitoli, "SRT", "va ovunque", ".srt", Motore.Sottotitoli),
        new("sub.vtt", Categoria.Sottotitoli, "VTT", "per il web", ".vtt", Motore.Sottotitoli),
        new("sub.ass", Categoria.Sottotitoli, "ASS", "con lo stile", ".ass", Motore.Sottotitoli),
        new("sub.txt", Categoria.Sottotitoli, "TXT", "solo le battute", ".txt", Motore.Sottotitoli),

        // ——— archivi, cartelle e tutto il resto ———
        new("arch.estrai", Categoria.Archivio, "ESTRAI", "in una cartella qui", "", Motore.Archivi),
        new("arch.zip", Categoria.Archivio, "ZIP", "lo apre chiunque", ".zip", Motore.Archivi),
        new("arch.7z", Categoria.Archivio, "7Z", "il più piccolo", ".7z", Motore.Archivi),
        new("arch.tgz", Categoria.Archivio, "TAR.GZ", "Linux e Mac", ".tar.gz", Motore.Archivi),
        new("arch.txz", Categoria.Archivio, "TAR.XZ", "Linux, compresso forte", ".tar.xz", Motore.Archivi),

        new("cartella.zip", Categoria.Cartella, "ZIP", "lo apre chiunque", ".zip", Motore.Archivi, Unisce: true),
        new("cartella.7z", Categoria.Cartella, "7Z", "il più piccolo", ".7z", Motore.Archivi, Unisce: true),

        new("altro.zip", Categoria.Altro, "ZIP", "compresso, lo apre chiunque", ".zip", Motore.Archivi, Unisce: true),
        new("altro.7z", Categoria.Altro, "7Z", "compresso, il più piccolo", ".7z", Motore.Archivi, Unisce: true),
    ];

    /// <summary>Il sottomenu del tasto destro, in ordine. "apri" apre la finestra con tutte le scelte.</summary>
    public static readonly IReadOnlyList<Rapida> Rapide =
    [
        new("video-mp4", Categoria.Video, "MP4 · va ovunque", "video.mp4"),
        new("video-meta", Categoria.Video, "Metà peso · H.265", "video.mp4", "meta"),
        new("video-mp3", Categoria.Video, "Solo l'audio · MP3", "video.mp3"),
        new("video-gif", Categoria.Video, "GIF animata", "video.gif"),

        new("audio-mp3", Categoria.Audio, "MP3", "audio.mp3"),
        new("audio-m4a", Categoria.Audio, "M4A · iPhone", "audio.m4a"),
        new("audio-wav", Categoria.Audio, "WAV", "audio.wav"),
        new("audio-flac", Categoria.Audio, "FLAC", "audio.flac"),

        new("img-jpg", Categoria.Immagine, "JPG", "img.jpg"),
        new("img-png", Categoria.Immagine, "PNG", "img.png"),
        new("img-webp", Categoria.Immagine, "WEBP", "img.webp"),
        new("img-piccola", Categoria.Immagine, "Più piccola · 1920 px", "img.jpg", "piccola"),
        new("img-pdf", Categoria.Immagine, "Un PDF solo", "img.pdf"),

        new("pdf-jpg", Categoria.Pdf, "Pagine in JPG", "pdf.jpg"),
        new("pdf-txt", Categoria.Pdf, "Solo il testo · TXT", "pdf.txt"),
        new("pdf-docx", Categoria.Pdf, "Word · DOCX", "pdf.docx"),
        new("pdf-unisci", Categoria.Pdf, "Unisci in un PDF solo", "pdf.unisci"),

        new("doc-pdf", Categoria.Documento, "PDF", "doc.pdf"),
        new("doc-docx", Categoria.Documento, "Word · DOCX", "doc.docx"),
        new("foglio-pdf", Categoria.Foglio, "PDF", "foglio.pdf"),
        new("foglio-xlsx", Categoria.Foglio, "Excel · XLSX", "foglio.xlsx"),
        new("foglio-csv", Categoria.Foglio, "CSV", "foglio.csv"),
        new("pres-pdf", Categoria.Presentazione, "PDF", "pres.pdf"),
        new("pres-pptx", Categoria.Presentazione, "PowerPoint · PPTX", "pres.pptx"),

        new("testo-pdf", Categoria.Testo, "PDF", "testo.pdf"),
        new("testo-docx", Categoria.Testo, "Word · DOCX", "testo.docx"),
        new("testo-html", Categoria.Testo, "Pagina web · HTML", "testo.html"),

        new("dati-xlsx", Categoria.Dati, "Excel · XLSX", "dati.xlsx"),
        new("dati-json", Categoria.Dati, "JSON", "dati.json"),
        new("dati-csv", Categoria.Dati, "CSV", "dati.csv"),

        new("sub-srt", Categoria.Sottotitoli, "SRT", "sub.srt"),
        new("sub-vtt", Categoria.Sottotitoli, "VTT", "sub.vtt"),
        new("sub-txt", Categoria.Sottotitoli, "Solo le battute · TXT", "sub.txt"),

        new("arch-estrai", Categoria.Archivio, "Estrai qui", "arch.estrai"),
        new("arch-7z", Categoria.Archivio, "7Z", "arch.7z"),
        new("arch-zip", Categoria.Archivio, "ZIP", "arch.zip"),

        new("cartella-zip", Categoria.Cartella, "Comprimi in ZIP", "cartella.zip"),
        new("cartella-7z", Categoria.Cartella, "Comprimi in 7Z", "cartella.7z"),

        new("altro-zip", Categoria.Altro, "Comprimi in ZIP", "altro.zip"),
        new("altro-7z", Categoria.Altro, "Comprimi in 7Z", "altro.7z"),
    ];

    public static IEnumerable<string> EstensioniDi(Categoria c) =>
        estensioni.Where(kv => kv.Value == c).Select(kv => kv.Key);

    public static IEnumerable<string> TutteLeEstensioni => estensioni.Keys;

    public static Categoria CategoriaDi(string percorso)
    {
        if (Directory.Exists(percorso)) return Categoria.Cartella;
        var nome = Path.GetFileName(percorso);
        // .tar.gz & co. sono archivi anche se l'ultima estensione dice "gz"
        if (nome.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) || nome.EndsWith(".tar.xz", StringComparison.OrdinalIgnoreCase)
            || nome.EndsWith(".tar.bz2", StringComparison.OrdinalIgnoreCase) || nome.EndsWith(".tar.zst", StringComparison.OrdinalIgnoreCase))
            return Categoria.Archivio;
        return estensioni.TryGetValue(Path.GetExtension(percorso), out var c) ? c : Categoria.Altro;
    }

    public static Formato? Trova(string id) => Formati.FirstOrDefault(f => f.Id == id);

    public static Rapida? TrovaRapida(string id) => Rapide.FirstOrDefault(r => r.Id == id);

    /// <summary>I formati che hanno senso per questo file (alcuni valgono solo per certe estensioni, come GIF → MP4).</summary>
    public static IEnumerable<Formato> FormatiPer(string percorso)
    {
        var c = CategoriaDi(percorso);
        var est = Path.GetExtension(percorso);
        return Formati.Where(f => f.Categoria == c && (f.SoloPer is null || f.SoloPer.Contains(est)));
    }

    public static string NomeCategoria(Categoria c) => c switch
    {
        Categoria.Video => "Video",
        Categoria.Audio => "Audio",
        Categoria.Immagine => "Immagini",
        Categoria.Pdf => "PDF",
        Categoria.Documento => "Documenti",
        Categoria.Foglio => "Fogli di calcolo",
        Categoria.Presentazione => "Presentazioni",
        Categoria.Testo => "Testi",
        Categoria.Dati => "Dati",
        Categoria.Sottotitoli => "Sottotitoli",
        Categoria.Archivio => "Archivi",
        Categoria.Cartella => "Cartelle",
        _ => "Altri file",
    };

    public static string Chiave(Categoria c) => c.ToString().ToLowerInvariant();
}
