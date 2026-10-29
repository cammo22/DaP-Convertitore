namespace DaP.Convertitore.Lettore;

/// <summary>
/// Le figurine dei file che stanno dentro un archivio (foto, video, PDF): la voce si estrae da sola in una cartella
/// di lavoro, ne esce la miniatura e la cartella si butta. Due alla volta, e solo per file non enormi.
/// </summary>
public static class MiniatureArchivio
{
    static readonly SemaphoreSlim due = new(2);
    const long MaxFoto = 80L << 20, MaxVideo = 600L << 20, MaxPdf = 120L << 20;

    /// <summary>Quali voci hanno una figurina: foto, video e PDF non troppo pesanti. Il resto resta con la sua icona.</summary>
    public static bool Ammessa(string voce, long peso) => Catalogo.CategoriaDi(voce) switch
    {
        Categoria.Immagine => peso is > 0 and <= MaxFoto,
        Categoria.Video => peso is > 0 and <= MaxVideo,
        Categoria.Pdf => peso is > 0 and <= MaxPdf,
        _ => false,
    };

    /// <summary>La miniatura come «data:» URL, o null se non si riesce (la pagina tiene l'icona).</summary>
    public static async Task<string?> Fai(Strumenti s, string archivio, string voce, long peso, uint lato, CancellationToken ct = default)
    {
        if (!Ammessa(voce, peso) || !s.HaSetteZip) return null;
        await due.WaitAsync(ct);
        var cartella = Path.Combine(Strumenti.CartellaTemporanea, "miniatura-" + Guid.NewGuid().ToString("N"));
        try
        {
            await SetteZip.Estrai(s, archivio, cartella, [voce], ct);
            var file = Directory.EnumerateFiles(cartella, "*", SearchOption.AllDirectories).FirstOrDefault();
            if (file is null) return null;
            var r = await Anteprime.Fai(file, Catalogo.CategoriaDi(voce), s, 0, lato);
            return r?.DataUrl;
        }
        catch (ErroreConversione) { return null; }
        finally
        {
            due.Release();
            try { Directory.Delete(cartella, true); } catch { /* ancora in uso: la pulizia dei temporanei lo prende */ }
        }
    }
}
