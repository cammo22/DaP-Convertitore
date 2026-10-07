namespace DaP.Convertitore;

/// <summary>
/// Cosa si fa a un video, deciso prima di cominciare: lo stesso conto serve all'interfaccia (lo slider del peso
/// e la lancetta della qualità) e alla conversione vera. Una cosa sola, uguale ovunque.
/// </summary>
public sealed record PianoVideo(
    string Modo,
    string Codec,
    string Encoder,
    bool Hardware,
    int Larghezza,
    int Altezza,
    double Fps,
    long BitrateVideo,
    int Crf,
    int AudioKbps,
    bool AudioCopia,
    bool SenzaAudio,
    bool SoloPrimaTraccia,
    double Punteggio,
    string Giudizio,
    long PesoStimato,
    long PesoMin,
    long PesoMax,
    bool PuoCopiare,
    bool HdrInSdr,
    bool HdrTenuto,
    IReadOnlyList<string> Note)
{
    /// <summary>Quanto si lascia al contenitore (indici, intestazioni): il peso voluto per il 97%.</summary>
    public const double Margine = 0.97;

    static readonly int[] lati = [2160, 1440, 1080, 720, 540, 480, 360];

    /// <summary>I codec che ogni contenitore regge, il primo è quello di partenza.</summary>
    public static IReadOnlyList<string> CodecPer(string estensione, InfoHardware hw) => estensione switch
    {
        ".webm" => hw.Ha("av1_nvenc") || hw.Ha("av1_amf") || hw.Ha("av1_qsv") ? ["av1", "vp9"] : ["vp9", "av1"],
        ".mov" => ["h264", "hevc", "prores"],
        ".mkv" => ["h264", "hevc", "av1"],
        _ => ["h264", "hevc", "av1"],
    };

    static bool VideoCopiabile(string codec, string est) => est switch
    {
        ".mp4" => codec is "h264" or "hevc" or "av1" or "mpeg4" or "vp9",
        ".mov" => codec is "h264" or "hevc" or "prores" or "mjpeg" or "mpeg4",
        ".webm" => codec is "vp8" or "vp9" or "av1",
        ".mkv" => true,
        _ => false,
    };

    public static bool AudioCopiabile(string codec, string est) => est switch
    {
        ".mp4" => codec is "aac" or "mp3" or "ac3" or "eac3" or "alac" or "opus" or "flac",
        ".mov" => codec is "aac" or "alac" or "mp3" or "ac3" or "pcm_s16le" or "pcm_s24le",
        ".webm" => codec is "opus" or "vorbis",
        ".mkv" => true,
        _ => false,
    };

    /// <summary>Quanti bit servono per pixel per fotogramma per una resa "ottima", codec per codec.</summary>
    static double Soglia(string codec) => codec switch
    {
        "hevc" => 0.055,
        "av1" => 0.042,
        "vp9" => 0.05,
        _ => 0.085,
    };

    /// <summary>Quanto rende un codec rispetto all'H.264 (meno = serve meno spazio per la stessa resa).</summary>
    static double Resa(string codec) => codec switch
    {
        "h264" => 1.0,
        "hevc" => 0.65,
        "av1" => 0.5,
        "vp9" => 0.6,
        "vp8" => 1.1,
        "mpeg4" or "msmpeg4v3" or "wmv3" or "vc1" => 1.4,
        "mpeg2video" or "mpeg1video" => 2.0,
        "prores" or "dnxhd" => 6.0,
        "mjpeg" => 5.0,
        "rawvideo" or "huffyuv" or "ffv1" or "utvideo" => 20.0,
        _ => 1.0,
    };

    public static string GiudizioDi(double p) => p switch
    {
        >= 0.95 => "Come l'originale",
        >= 0.72 => "Ottima",
        >= 0.55 => "Buona",
        >= 0.38 => "Si vede, ma va",
        >= 0.2 => "Bassa",
        _ => "Da buttare",
    };

    /// <summary>Rapporto fra i bit che ci sono e quelli che servirebbero → 0..1. Il doppio del necessario = 1.</summary>
    static double DaRapporto(double r) => Math.Clamp(Math.Log2(Math.Max(r, 1e-6)) * 0.25 + 0.75, 0, 1);

    public static string? ScegliEncoder(string codec, string motore, InfoHardware hw)
    {
        string[] famiglia = codec switch
        {
            "hevc" => ["hevc_nvenc", "hevc_amf", "hevc_qsv", "libx265"],
            "av1" => ["av1_nvenc", "av1_amf", "av1_qsv", "libsvtav1"],
            "vp9" => ["libvpx-vp9"],
            "prores" => ["prores_ks"],
            _ => ["h264_nvenc", "h264_amf", "h264_qsv", "libx264"],
        };
        string? Primo(Func<string, bool> filtro) => famiglia.Where(filtro).FirstOrDefault(hw.Ha);
        return motore switch
        {
            "nvidia" => Primo(e => e.EndsWith("_nvenc")),
            "amd" => Primo(e => e.EndsWith("_amf")),
            "intel" => Primo(e => e.EndsWith("_qsv")),
            "cpu" => Primo(e => !e.Contains('_') || e.StartsWith("lib") || e == "prores_ks"),
            _ => null,
        } ?? Primo(_ => true);
    }

    public static bool EHardware(string encoder) => encoder.EndsWith("_nvenc") || encoder.EndsWith("_amf") || encoder.EndsWith("_qsv");

    public static PianoVideo Calcola(InfoMedia info, long pesoSorgente, string estensioneSorgente, string estensione, OpzioniVideo o, InfoHardware hw)
    {
        var note = new List<string>();
        var v = info.Video ?? throw new ErroreConversione("In questo file non c'è un video.");
        var durata = info.Durata > 0 ? info.Durata : 1;
        var srcFps = v.Fps is > 0 and < 241 ? v.Fps : 30;

        var ammessi = CodecPer(estensione, hw);
        var codec = ammessi.Contains(o.Codec) ? o.Codec : ammessi[0];
        var encoder = ScegliEncoder(codec, o.Motore, hw) ?? "libx264";
        var hardware = EHardware(encoder);
        if (o.Motore is "nvidia" or "amd" or "intel" && !hardware) note.Add("La scheda video scelta non fa questo codec: lavora la CPU.");

        var senzaAudio = o.SenzaAudio || info.Audio.Count == 0;
        var puoCopiare = VideoCopiabile(v.Codec, estensione);
        var modo = o.Modo switch
        {
            "peso" when info.Durata > 0 => "peso",
            "copia" when puoCopiare => "copia",
            "qualita" => "qualita",
            "peso" => "qualita",
            _ => puoCopiare && o.Lato == 0 && o.Fps == 0 && !StessoContenitore(estensioneSorgente, estensione) ? "copia" : "qualita",
        };

        // ——— HDR ———
        var hdrInSdr = false;
        var hdrTenuto = false;
        if (v.Hdr && modo != "copia")
        {
            if (codec is "hevc" or "av1") { hdrTenuto = true; note.Add("HDR tenuto (10 bit)."); }
            else { hdrInSdr = true; note.Add("HDR → SDR: i colori restano giusti sugli schermi normali."); }
        }

        // ——— peso: i limiti dello slider ———
        const int minVideoKbps = 90, minAudioKbps = 48;
        var pesoMin = (long)(durata * (minVideoKbps + (senzaAudio ? 0 : minAudioKbps)) * 1000 / 8 / Margine);
        var pesoMax = Math.Max(pesoSorgente, pesoMin * 2);

        var srcBpp = v.Bitrate > 0 && v.Larghezza > 0 ? v.Bitrate / (double)(v.Larghezza * v.Altezza * srcFps) / Resa(v.Codec) : double.MaxValue;
        double Rapporto(long vbps, int w, int h, double fps) =>
            vbps / (w * (double)h * fps) / (Soglia(codec) * (hardware ? 1.15 : 1.0));

        int lato = v.LatoCorto;
        double fps = o.Fps > 0 && o.Fps < srcFps ? o.Fps : srcFps;
        long vbps = 0;
        int audioKbps = 0;
        var audioCopia = false;
        var soloPrima = false;
        double punteggio;
        int crf = 0;
        long pesoStimato;

        if (modo == "copia")
        {
            audioCopia = !senzaAudio && o.AudioKbps == 0 && info.Audio.All(a => AudioCopiabile(a.Codec, estensione));
            if (!audioCopia && !senzaAudio) audioKbps = o.AudioKbps > 0 ? o.AudioKbps : estensione == ".webm" ? 160 : 192;
            punteggio = 1;
            pesoStimato = pesoSorgente;
            note.Add("Cambia solo la scatola: niente ricodifica, ci mette pochi secondi.");
        }
        else if (modo == "peso")
        {
            var obiettivo = Math.Clamp(o.PesoByte > 0 ? o.PesoByte : pesoSorgente / 2, pesoMin, Math.Max(pesoMax, pesoMin));
            soloPrima = !senzaAudio && info.Audio.Count > 1;
            if (soloPrima) note.Add("Per stare nel peso tengo solo la prima traccia audio.");
            var totale = obiettivo * Margine * 8 / durata;
            if (!senzaAudio)
            {
                audioKbps = o.AudioKbps > 0 ? o.AudioKbps
                    : totale > 2_500_000 ? 160 : totale > 900_000 ? 128 : totale > 350_000 ? 96 : 64;
            }
            vbps = (long)Math.Max(minVideoKbps * 1000, totale - audioKbps * 1000);

            if (o.Lato > 0) lato = Math.Min(o.Lato, v.LatoCorto);
            else
            {
                // si scende di risoluzione finché i bit per pixel bastano per una resa "buona"
                var candidati = new[] { v.LatoCorto }.Concat(lati.Where(l => l < v.LatoCorto)).ToList();
                lato = candidati.Last();
                foreach (var c in candidati)
                {
                    var (w, h) = Dimensioni(v, c);
                    if (Rapporto(vbps, w, h, fps) >= 0.6) { lato = c; break; }
                }
                if (lato < v.LatoCorto) note.Add($"Per starci scendo a {lato}p.");
            }
            {
                var (w, h) = Dimensioni(v, lato);
                if (o.Fps == 0 && srcFps > 31 && Rapporto(vbps, w, h, fps) < 0.4)
                {
                    fps = srcFps / 2;
                    note.Add($"E a {fps:0.#} fotogrammi al secondo.");
                }
            }
            pesoStimato = (long)(obiettivo * Margine);
            if (vbps * durata / 8 > obiettivo) note.Add("Più in basso di così non si scende.");
            var (ww, hh) = Dimensioni(v, lato);
            punteggio = DaRapporto(Rapporto(vbps, ww, hh, fps));
        }
        else
        {
            var q = Math.Clamp(o.Qualita, 0, 100);
            if (o.Lato > 0) lato = Math.Min(o.Lato, v.LatoCorto);
            crf = CrfPer(encoder, q);
            punteggio = Math.Clamp(0.3 + q / 100.0 * 0.72, 0, 1);
            var (w, h) = Dimensioni(v, lato);
            // stima: bit per pixel che quella qualità chiede, mai più di quanti ne aveva l'originale
            var bpp = Soglia(codec) * Math.Pow(2, (q - 70) / 22.0);
            var stimaV = bpp * w * h * fps;
            if (srcBpp < double.MaxValue) stimaV = Math.Min(stimaV, srcBpp * Resa(codec) * 1.1 * w * h * fps);
            vbps = (long)stimaV;
            if (!senzaAudio)
            {
                audioCopia = o.AudioKbps == 0 && info.Audio.All(a => AudioCopiabile(a.Codec, estensione));
                audioKbps = audioCopia ? 0 : o.AudioKbps > 0 ? o.AudioKbps : estensione == ".webm" ? 160 : 192;
            }
            var audioBps = audioCopia ? info.Audio.Sum(a => a.Bitrate > 0 ? a.Bitrate : 160_000) : audioKbps * 1000L;
            pesoStimato = (long)((vbps + audioBps) * durata / 8 / Margine);
        }

        // non si fa meglio dell'originale: se ci sono più bit "equivalenti" di quanti ne aveva, è come l'originale
        if (modo != "copia" && srcBpp < double.MaxValue)
        {
            var (w, h) = Dimensioni(v, lato);
            var outEq = vbps / (w * (double)h * fps) / Resa(codec);
            if (outEq >= srcBpp * 0.9 && lato == v.LatoCorto) punteggio = Math.Max(punteggio, 0.96);
        }
        if (lato < v.LatoCorto && modo != "copia") punteggio *= Math.Pow(lato / (double)v.LatoCorto, 0.25);

        var (larghezza, altezza) = modo == "copia" ? (v.Larghezza, v.Altezza) : Dimensioni(v, lato);
        return new PianoVideo(modo, modo == "copia" ? v.Codec : codec, modo == "copia" ? "copy" : encoder, modo != "copia" && hardware,
            larghezza, altezza, fps, vbps, crf, audioKbps, audioCopia, senzaAudio, soloPrima,
            Math.Round(punteggio, 3), GiudizioDi(punteggio), pesoStimato, pesoMin, pesoMax, puoCopiare, hdrInSdr, hdrTenuto, note);
    }

    /// <summary>MP4 → MP4: copiare non servirebbe a niente, quindi "auto" ricodifica.</summary>
    static bool StessoContenitore(string sorgente, string uscita)
    {
        static string N(string e) => e.ToLowerInvariant() switch { ".m4v" => ".mp4", var x => x };
        return N(sorgente) == N(uscita);
    }

    /// <summary>Le misure d'uscita per un lato corto: proporzioni tenute, sempre pari (lo vuole il 4:2:0).</summary>
    public static (int w, int h) Dimensioni(InfoVideo v, int latoCorto)
    {
        latoCorto = Math.Min(latoCorto, v.LatoCorto);
        double scala = latoCorto / (double)v.LatoCorto;
        int w = Pari(v.Larghezza * scala), h = Pari(v.Altezza * scala);
        return (Math.Max(w, 2), Math.Max(h, 2));
    }

    static int Pari(double x) => (int)Math.Round(x / 2) * 2;

    /// <summary>Qualità 0..100 → il numero che capisce ogni encoder (più basso = più bello).</summary>
    public static int CrfPer(string encoder, int q)
    {
        q = Math.Clamp(q, 0, 100);
        return encoder switch
        {
            "libx264" => (int)Math.Round(30 - q * 0.14),
            "libx265" => (int)Math.Round(32 - q * 0.14),
            "libsvtav1" => (int)Math.Round(46 - q * 0.24),
            "libvpx-vp9" => (int)Math.Round(40 - q * 0.18),
            "av1_nvenc" or "av1_amf" or "av1_qsv" => (int)Math.Round(42 - q * 0.2),
            "prores_ks" => q < 40 ? 1 : q < 75 ? 2 : 3,
            _ => (int)Math.Round(34 - q * 0.16),
        };
    }
}
