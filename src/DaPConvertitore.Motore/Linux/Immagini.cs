namespace DaP.Convertitore;

/// <summary>
/// Le immagini su Linux: le fa FFmpeg (su Windows le fa WIC, in Immagini.cs). Stesso nome di classe,
/// così il resto del motore non cambia. Questo file si compila solo per net10.0.
/// </summary>
public static class Immagini
{
    public static async Task<(int w, int h)?> Misure(string percorso)
    {
        try
        {
            var info = await Sonda.Leggi(Strumenti.Trova(), percorso, silenzioso: true);
            return info.Video is { } v && v.Larghezza > 0 ? (v.Larghezza, v.Altezza) : null;
        }
        catch { return null; }
    }

    public static async Task Converti(Lavoro l, Contesto ctx)
    {
        var o = l.Opzioni.Immagine;
        var est = l.Formato.Estensione;
        ctx.Fase("Converto l'immagine");
        var qualita = o.Qualita is > 0 and <= 100 ? o.Qualita : 88;
        if (o.Modo == "peso" && o.PesoMaxByte > 0)
            qualita = await QualitaPerPeso(l, ctx, est, o.Lato, o.PesoMaxByte, qualita);
        await Scrivi(ctx, l.Sorgente, l.Bozza, est, o.Lato, qualita);
    }

    /// <summary>Quanto pesa la foto a questa qualità: una codifica vera, per lo slider.</summary>
    public static async Task<(long Peso, int Qualita, int Larghezza, int Altezza)> Stima(string percorso, string est, int lato, int qualita, long pesoMax, CancellationToken ct)
    {
        var ctx = new Contesto(Strumenti.Trova(), new InfoHardware("", 1, [], []), ct, _ => { });
        try
        {
            var q = qualita is > 0 and <= 100 ? qualita : 88;
            if (pesoMax > 0) q = await QualitaPerPeso(null, ctx, est, lato, pesoMax, q, percorso);
            var bozza = Path.Combine(ctx.Temporanea(), "stima" + est);
            await Scrivi(ctx, percorso, bozza, est, lato, q);
            var misure = await Misure(bozza);
            return (new FileInfo(bozza).Length, q, misure?.w ?? 0, misure?.h ?? 0);
        }
        finally { ctx.Pulisci(); }
    }

    static async Task<int> QualitaPerPeso(Lavoro? l, Contesto ctx, string est, int lato, long pesoMax, int partenza, string? sorgente = null)
    {
        var ingresso = sorgente ?? l!.Sorgente;
        var meglio = partenza;
        foreach (var q in new[] { 90, 75, 60, 45, 30 })
        {
            var bozza = Path.Combine(ctx.Temporanea(), $"peso-{q}{est}");
            await Scrivi(ctx, ingresso, bozza, est, lato, q);
            if (new FileInfo(bozza).Length <= pesoMax) { meglio = q; break; }
            meglio = q;
        }
        return meglio;
    }

    static async Task Scrivi(Contesto ctx, string ingresso, string uscita, string est, int lato, int qualita)
    {
        var argomenti = new List<string> { "-i", ingresso };
        if (lato > 0) { argomenti.Add("-vf"); argomenti.Add($"scale='min({lato},iw)':-2"); }
        argomenti.AddRange(est switch
        {
            ".jpg" or ".jpeg" => new[] { "-q:v", Jpeg(qualita) },
            ".webp" => new[] { "-c:v", "libwebp", "-quality", qualita.ToString() },
            ".avif" => new[] { "-c:v", "libaom-av1", "-still-picture", "1", "-cpu-used", "6", "-crf", Avif(qualita) },
            ".gif" => new[] { "-loop", "0" },
            _ => Array.Empty<string>(),
        });
        argomenti.Add(uscita);
        await Ffmpeg.Esegui(ctx, argomenti, 1, fase: "Converto l'immagine");
    }

    /// <summary>FFmpeg vuole 2 (bellissima) … 31 (brutta) per il JPEG.</summary>
    static string Jpeg(int qualita) => Math.Clamp(31 - (qualita - 1) * 29 / 99, 2, 31).ToString();

    static string Avif(int qualita) => Math.Clamp(63 - qualita * 63 / 100, 10, 63).ToString();
}
