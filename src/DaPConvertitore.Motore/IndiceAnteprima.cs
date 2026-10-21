using System.Globalization;
using Windows.Graphics.Imaging;

namespace DaP.Convertitore;

/// <summary>
/// La colonna a destra dell'anteprima per i file che non sono testo (come la minimappa di VS Code, ma per tutto il
/// resto): l'indice delle pagine di un PDF, i fotogrammi di un video, le onde di un brano, la foto o l'icona per gli altri.
/// Prima si chiede com'è fatto l'indice (<see cref="Descrivi"/>, istantaneo), poi le figurine una alla volta (<see cref="Voce"/>).
/// </summary>
public static class IndiceAnteprima
{
    /// <summary>Quante figurine al massimo: un PDF da 900 pagine non ne fa 900 (la colonna le campiona).</summary>
    public const int MaxVoci = 400;
    /// <summary>Quanti fotogrammi di un video, quante onde di un brano.</summary>
    public const int PerTempo = 12;

    /// <param name="Tipo">«pagine» (PDF), «tempo» (video e audio), «singola» (tutto il resto).</param>
    /// <param name="Voci">Quante figurine ci sono.</param>
    /// <param name="Totale">Pagine del PDF, o durata in secondi.</param>
    public sealed record Indice(string Tipo, int Voci, double Totale);

    public static async Task<Indice> Descrivi(string percorso, Categoria c, double durata)
    {
        switch (c)
        {
            case Categoria.Pdf:
            {
                var n = await Pdf.Pagine(percorso) ?? 0;
                return n > 0 ? new Indice("pagine", Math.Min(n, MaxVoci), n) : new Indice("singola", 1, 1);
            }
            case Categoria.Video or Categoria.Audio when durata > 0.5:
                return new Indice("tempo", PerTempo, durata);
            default:
                return new Indice("singola", 1, 1);
        }
    }

    /// <summary>Il punto del file a cui punta la figurina `n`: la pagina (da 1) o il secondo.</summary>
    public static double Punto(Indice i, int n)
    {
        n = Math.Clamp(n, 0, Math.Max(0, i.Voci - 1));
        if (i.Tipo == "pagine") return i.Voci >= i.Totale ? n + 1 : Math.Floor(n * i.Totale / i.Voci) + 1;
        if (i.Tipo == "tempo") return Math.Round(i.Totale * (n + 0.5) / i.Voci, 2);
        return 0;
    }

    public static async Task<Anteprima?> Voce(string percorso, Categoria c, Indice i, int n, Strumenti s, uint lato)
    {
        try
        {
            if (i.Tipo == "pagine")
            {
                var p = await Pdf.PaginaPiccola(percorso, (int)Punto(i, n) - 1, lato);
                return p is null ? null : new Anteprima(await Immagini.Codifica(p, BitmapEncoder.JpegEncoderId, 0.85f), "image/jpeg");
            }
            if (i.Tipo == "tempo") return await DelTempo(percorso, c, i, n, s, lato);
        }
        catch (Exception e) { Registro.Scrivi($"Indice di {Path.GetFileName(percorso)}: {e.Message}"); }
        return await Anteprime.Fai(percorso, c, s, i.Totale, lato);
    }

    static async Task<Anteprima?> DelTempo(string percorso, Categoria c, Indice i, int n, Strumenti s, uint lato)
    {
        var inv = CultureInfo.InvariantCulture;
        var fine = Path.Combine(Strumenti.CartellaTemporanea, $"indice-{Guid.NewGuid():N}.{(c == Categoria.Video ? "jpg" : "png")}");
        try
        {
            var t = Punto(i, n);
            string[] args = c == Categoria.Video
                ? ["-hide_banner", "-loglevel", "error", "-y", "-ss", t.ToString("0.###", inv), "-i", percorso, "-frames:v", "1",
                   "-vf", $"scale='min({lato},iw)':-2", "-q:v", "4", "-update", "1", fine]
                // un'onda per ogni tratto del brano: gli stessi 1/12 del tempo che il video mostra a fotogrammi
                : ["-hide_banner", "-loglevel", "error", "-y", "-ss", Math.Max(0, i.Totale * n / i.Voci).ToString("0.###", inv), "-t", (i.Totale / i.Voci).ToString("0.###", inv), "-i", percorso,
                   "-filter_complex", $"aformat=channel_layouts=mono,showwavespic=s={lato}x{lato / 3}:colors=0xffd54a", "-frames:v", "1", fine];
            var r = await Processi.Esegui(s.Ffmpeg, args, CancellationToken.None, tieniUscita: false);
            if (r.Codice == 0 && File.Exists(fine)) return new Anteprima(await File.ReadAllBytesAsync(fine), c == Categoria.Video ? "image/jpeg" : "image/png");
            return null;
        }
        finally { try { File.Delete(fine); } catch { } }
    }
}
