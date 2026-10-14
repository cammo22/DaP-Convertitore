using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;

namespace DaP.Convertitore;

/// <summary>
/// Quanto peserà la foto: si codifica davvero (JPG e HEIC interi, WEBP, AVIF e JXL su un ritaglio di 512 pixel al centro,
/// riportato all'intera foto) e si misura. Serve allo slider del peso: «Peso finale 1,2 MB», e a dire a che qualità
/// ci si arriva e se la foto va rimpicciolita per starci. Lo stesso conto fa la conversione vera (<see cref="Immagini.Cerca{T}"/>).
/// </summary>
public sealed record StimaImmagine(long Peso, int Qualita, double Scala, long Min, long Max, int Larghezza, int Altezza, bool Esatta);

public static class StimaImmagini
{
    /// <summary>La qualità massima quando si chiede un peso: sopra, il WEBP diventa «senza perdite» e pesa il triplo per niente.</summary>
    public const int QualitaPerPeso = 95;

    static readonly object chiave = new();
    static (string Percorso, DateTime Quando, int Lato, SoftwareBitmap Foto)? ultima;

    /// <param name="qualita">Se pesoMax è 0: quanto pesa a questa qualità.</param>
    /// <param name="pesoMax">Se &gt; 0: a che qualità (e scala) ci si arriva per stare in questo peso.</param>
    /// <param name="intervallo">Calcola anche il più leggero e il più pesante possibile (per gli estremi dello slider).</param>
    public static async Task<StimaImmagine> Fai(Strumenti s, string percorso, string est, int lato, int qualita, long pesoMax, bool intervallo, CancellationToken ct)
    {
        var ctx = new Contesto(s, new InfoHardware("", 1, [], []), ct, _ => { });
        try
        {
            var foto = await Foto(percorso, lato, ctx);
            var w = foto.PixelWidth; var h = foto.PixelHeight;
            var conWic = est is ".jpg" or ".heic";

            Func<int, double, long> misura;
            var esatta = conWic;
            if (conWic)
            {
                var id = est == ".jpg" ? BitmapEncoder.JpegEncoderId : BitmapEncoder.HeifEncoderId;
                var piena = Immagini.SuBianco(foto);
                misura = (q, sc) => Immagini.Codifica(piena, id, q / 100f, sc).GetAwaiter().GetResult().LongLength;
            }
            else
            {
                // un ritaglio al centro, a risoluzione piena: il peso per pixel vero, senza codificare tutta la foto
                var ritaglio = Ritaglia(est == ".avif" ? Immagini.SuBianco(foto) : Immagini.Diritta(foto), 512);
                esatta = ritaglio.PixelWidth == w && ritaglio.PixelHeight == h;
                var cartella = ctx.Temporanea();
                var png = Path.Combine(cartella, "stima.png");
                await File.WriteAllBytesAsync(png, await Immagini.Codifica(ritaglio, BitmapEncoder.PngEncoderId), ct);
                var pixelRitaglio = (double)ritaglio.PixelWidth * ritaglio.PixelHeight;
                misura = (q, sc) =>
                {
                    var dati = Immagini.CodificaFfmpeg(png, est, q, sc, ctx, cartella, silenzioso: true).GetAwaiter().GetResult();
                    return esatta ? dati.LongLength : (long)(dati.LongLength * ((double)w * h * sc * sc / pixelRitaglio));
                };
            }

            var provati = new Dictionary<(int, double), long>();
            (long, byte) Misura(int q, double sc)
            {
                if (!provati.TryGetValue((q, sc), out var p)) provati[(q, sc)] = p = misura(q, sc);
                return (p, 0);
            }

            int qScelta = Math.Clamp(qualita, 1, 100);
            double scala = 1;
            long peso;
            if (pesoMax > 0)
            {
                var e = await Task.Run(() => Immagini.Cerca(Misura, QualitaPerPeso, pesoMax), ct);
                (qScelta, scala, peso) = (e.Qualita, e.Scala, e.Peso);
            }
            else peso = await Task.Run(() => Misura(qScelta, 1).Item1, ct);

            long min = 0, max = 0;
            if (intervallo)
            {
                max = await Task.Run(() => Misura(QualitaPerPeso, 1).Item1, ct);
                // più in giù di qualità 35 la foto si rimpicciolisce (come la conversione): un terzo del peso a 35 è raggiungibile
                min = (long)(await Task.Run(() => Misura(35, 1).Item1, ct) * 0.3);
            }
            return new StimaImmagine(peso, qScelta, scala, min, max, (int)Math.Round(w * scala), (int)Math.Round(h * scala), esatta);
        }
        finally { ctx.Pulisci(); }
    }

    /// <summary>La foto aperta (girata, rimpicciolita al lato voluto), tenuta a mente: lo slider chiede dieci stime di fila.</summary>
    static async Task<SoftwareBitmap> Foto(string percorso, int lato, Contesto ctx)
    {
        var quando = File.GetLastWriteTimeUtc(percorso);
        lock (chiave)
            if (ultima is { } u && u.Percorso == percorso && u.Quando == quando && u.Lato == lato) return u.Foto;
        var foto = await Immagini.Apri(percorso, ctx, (uint)Math.Max(0, lato));
        lock (chiave) ultima = (percorso, quando, lato, foto);
        return foto;
    }

    /// <summary>Il quadrato di lato massimo <paramref name="lato"/> preso dal centro (tutta la foto se è più piccola).</summary>
    static SoftwareBitmap Ritaglia(SoftwareBitmap f, int lato)
    {
        int w = Math.Min(lato, f.PixelWidth), h = Math.Min(lato, f.PixelHeight);
        if (w == f.PixelWidth && h == f.PixelHeight) return f;
        var px = new byte[4 * f.PixelWidth * f.PixelHeight];
        f.CopyToBuffer(px.AsBuffer());
        var dest = new byte[4 * w * h];
        int x0 = (f.PixelWidth - w) / 2, y0 = (f.PixelHeight - h) / 2;
        for (var y = 0; y < h; y++)
            Buffer.BlockCopy(px, ((y0 + y) * f.PixelWidth + x0) * 4, dest, y * w * 4, w * 4);
        return SoftwareBitmap.CreateCopyFromBuffer(dest.AsBuffer(), f.BitmapPixelFormat, w, h, f.BitmapAlphaMode);
    }
}
