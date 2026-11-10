using DaP.Convertitore.Lettore;

namespace DaP.Convertitore;

/// <summary>
/// L'audio, anche quello tirato fuori da un video. Copertina e titoli restano dove il formato li regge.
/// </summary>
public static class Audio
{
    /// <summary>Il bitrate di partenza di ogni formato (0 = senza perdite o a qualità variabile).</summary>
    public static int KbpsDiPartenza(string estensione) => estensione switch
    {
        ".mp3" => 256,
        ".m4a" => 256,
        ".opus" => 160,
        ".ogg" => 192,
        ".wma" => 192,
        _ => 0,
    };

    public static bool SenzaPerdite(string estensione) => estensione is ".flac" or ".wav" or ".aiff";

    public static async Task Converti(Lavoro l, Contesto ctx)
    {
        var est = l.Formato.Estensione;
        var o = l.Opzioni.Audio;
        ctx.Fase("Leggo l'audio");
        var info = await Sonda.Leggi(ctx.Strumenti, l.Sorgente, ctx.Ct);
        if (info.Audio.Count == 0) throw new ErroreConversione("In questo file non c'è audio.");

        // la copertina nuova (un disegno o un file scelto) sostituisce quella del brano; «nessuna» la toglie
        var tieneCover = est is ".mp3" or ".m4a" or ".flac";
        string? nuova = null;
        var togliCover = false;
        var scheda = l.Opzioni.Scheda;
        if (scheda is { Scrivi: true } && tieneCover && scheda.Copertina is { Length: > 0 } scelta)
        {
            if (scelta == "nessuna") togliCover = true;
            else if (scelta.StartsWith("generata:", StringComparison.Ordinal))
            {
                var n = int.TryParse(scelta["generata:".Length..], out var v) ? v : 0;
                var png = Path.Combine(ctx.Temporanea(), "copertina.png");
                await File.WriteAllBytesAsync(png, Copertine.Genera(scheda.Titolo ?? "", n, null), ctx.Ct);
                nuova = png;
            }
            else if (File.Exists(scelta)) nuova = scelta;
        }

        var a = new List<string> { "-i", l.Sorgente };
        if (nuova is not null) a.AddRange(["-i", nuova]);
        a.AddRange(["-map", "0:a:0"]);
        if (nuova is not null)
            a.AddRange(["-map", "1:0", "-frames:v", "1", "-c:v", est == ".flac" ? "png" : "mjpeg", "-disposition:v:0", "attached_pic",
                "-metadata:s:v", "title=Album cover", "-metadata:s:v", "comment=Cover (front)"]);
        else if (!togliCover && info.Copertina && tieneCover)
            a.AddRange(["-map", "0:v:0", "-c:v", "copy", "-disposition:v:0", "attached_pic"]);
        else a.Add("-vn");
        a.AddRange(["-map_metadata", "0", "-sn", "-dn"]);
        Metadati.SulComando(a, scheda, est);

        var filtri = new List<string>();
        if (o.Normalizza) filtri.Add("loudnorm=I=-14:TP=-1:LRA=11");
        if (filtri.Count > 0) a.AddRange(["-af", string.Join(',', filtri)]);
        if (o.Mono) a.AddRange(["-ac", "1"]);
        else if (info.Audio[0].Canali > 2 && est is ".mp3" or ".wma" or ".ogg") a.AddRange(["-ac", "2"]);

        var kbps = o.Kbps > 0 ? o.Kbps : KbpsDiPartenza(est);
        var bit24 = o.Bit >= 24;
        switch (est)
        {
            case ".mp3": a.AddRange(["-c:a", "libmp3lame", "-b:a", $"{kbps}k", "-id3v2_version", "3"]); break;
            case ".m4a": a.AddRange(["-c:a", "aac", "-b:a", $"{kbps}k"]); break;
            case ".opus": a.AddRange(["-c:a", "libopus", "-b:a", $"{kbps}k"]); break;
            case ".ogg": a.AddRange(["-c:a", "libvorbis", "-b:a", $"{kbps}k"]); break;
            case ".wma": a.AddRange(["-c:a", "wmav2", "-b:a", $"{kbps}k"]); break;
            case ".flac": a.AddRange(["-c:a", "flac", "-compression_level", "8", "-sample_fmt", bit24 ? "s32" : "s16"]); if (bit24) a.AddRange(["-bits_per_raw_sample", "24"]); break;
            case ".wav": a.AddRange(["-c:a", bit24 ? "pcm_s24le" : "pcm_s16le"]); break;
            case ".aiff": a.AddRange(["-c:a", bit24 ? "pcm_s24be" : "pcm_s16be"]); break;
        }
        // l'Opus vuole 48 kHz; il WMA al massimo 48
        if (est == ".opus") a.AddRange(["-ar", "48000"]);
        else if (est == ".wma" && info.Audio[0].Campionamento > 48000) a.AddRange(["-ar", "48000"]);

        a.AddRange(["-f", Ffmpeg.Muxer(est), l.Bozza]);
        await Ffmpeg.Esegui(ctx, a, info.Durata, fase: o.Normalizza ? "Converto e pareggio il volume" : "Converto l'audio");
    }
}
