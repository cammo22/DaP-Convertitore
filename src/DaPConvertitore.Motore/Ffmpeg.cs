using System.Globalization;

namespace DaP.Convertitore;

/// <summary>
/// Lancia FFmpeg e trasforma il suo "-progress pipe:1" in avanzamento. Annullare manda "q" (FFmpeg chiude
/// il file per bene) e poi, se non basta, lo spegne.
/// </summary>
public static class Ffmpeg
{
    public static async Task Esegui(Contesto ctx, IEnumerable<string> argomenti, double durata,
        double da = 0, double ampiezza = 1, string? fase = null)
    {
        var tutti = new List<string> { "-hide_banner", "-nostats", "-loglevel", "error", "-progress", "pipe:1", "-y" };
        tutti.AddRange(argomenti);
        double tempo = 0, fps = 0, velocita = 0;
        var r = await Processi.Esegui(ctx.Strumenti.Ffmpeg, tutti, ctx.Ct, riga =>
        {
            var i = riga.IndexOf('=');
            if (i <= 0) return;
            var chiave = riga[..i];
            var valore = riga[(i + 1)..].Trim();
            switch (chiave)
            {
                case "out_time_us" when long.TryParse(valore, out var us) && us > 0: tempo = us / 1e6; break;
                case "fps" when double.TryParse(valore, NumberStyles.Float, CultureInfo.InvariantCulture, out var f): fps = f; break;
                case "speed" when double.TryParse(valore.TrimEnd('x'), NumberStyles.Float, CultureInfo.InvariantCulture, out var s): velocita = s; break;
                case "progress":
                    var frazione = durata > 0 ? Math.Clamp(tempo / durata, 0, 1) : 0;
                    if (valore == "end") frazione = 1;
                    ctx.Riporta(new Avanzamento(da + frazione * ampiezza, fase, velocita > 0 ? velocita : null, fps > 0 ? fps : null));
                    break;
            }
        }, tieniUscita: false, quandoAnnulli: async p =>
        {
            try { await p.StandardInput.WriteAsync('q'); await p.StandardInput.FlushAsync(); } catch { }
        });
        if (r.Codice != 0) throw new ErroreConversione(Spiega(r.Errori), r.Errori);
    }

    /// <summary>Gli errori di FFmpeg detti come li direbbe una persona.</summary>
    public static string Spiega(string errori)
    {
        var e = errori.ToLowerInvariant();
        if (e.Contains("no space left")) return "Il disco è pieno.";
        if (e.Contains("permission denied") || e.Contains("access is denied")) return "Windows non mi lascia scrivere in quella cartella.";
        if (e.Contains("invalid data found") || e.Contains("moov atom not found")) return "Il file è rovinato o incompleto.";
        if (e.Contains("openencodesessionex failed") || e.Contains("nvenc") && e.Contains("failed")) return "La scheda video non è riuscita a codificare.";
        if (e.Contains("subtitle") && (e.Contains("bitmap") || e.Contains("not supported"))) return "Questi sottotitoli sono immagini: non diventano testo.";
        if (e.Contains("does not contain any stream") || e.Contains("matches no streams")) return "Nel file manca la traccia che serve.";
        var ultima = errori.Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.Trim();
        return string.IsNullOrEmpty(ultima) ? "FFmpeg si è fermato senza dire perché." : $"FFmpeg si è fermato: {ultima}";
    }

    public static string Muxer(string estensione) => estensione switch
    {
        ".mp4" => "mp4",
        ".mov" => "mov",
        ".mkv" => "matroska",
        ".webm" => "webm",
        ".gif" => "gif",
        ".mp3" => "mp3",
        ".m4a" => "ipod",
        ".opus" => "opus",
        ".ogg" => "ogg",
        ".flac" => "flac",
        ".wav" => "wav",
        ".aiff" => "aiff",
        ".wma" => "asf",
        ".srt" => "srt",
        ".vtt" => "webvtt",
        ".ass" => "ass",
        ".webp" => "webp",
        ".avif" => "avif",
        _ => "",
    };
}
