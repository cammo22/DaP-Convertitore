using System.Globalization;
using Windows.Graphics.Imaging;

namespace DaP.Convertitore.App;

/// <summary>
/// Prima e dopo: lo stesso fotogramma (o la stessa foto) preso dall'originale e dal convertito, per vedere
/// con gli occhi quanto si è perso. Dal video si prende allo stesso punto in proporzione.
/// </summary>
public static class Confronto
{
    public static async Task<object> Fai(Lavoro l, double posizione, Strumenti s)
    {
        if (l.Uscita is null || !File.Exists(l.Uscita)) throw new ErroreConversione("Il file convertito non c'è più.");
        if (l.Formato.Categoria == Categoria.Video || l.Formato.Motore == Motore.Video)
        {
            var info = await Sonda.Leggi(s, l.Sorgente);
            var t = Math.Max(0, info.Durata * Math.Clamp(posizione, 0, 1));
            return new { prima = await Fotogramma(s, l.Sorgente, t), dopo = await Fotogramma(s, l.Uscita, t), tempo = t };
        }
        var ctx = new Contesto(s, new InfoHardware("", 1, [], []), CancellationToken.None, _ => { });
        try
        {
            var a = await Immagini.Apri(l.Sorgente, ctx, 1800);
            var b = await Immagini.Apri(l.Uscita, ctx, 1800);
            return new { prima = await Png(a), dopo = await Png(b) };
        }
        finally { ctx.Pulisci(); }
    }

    static async Task<string> Png(SoftwareBitmap f)
    {
        var d = SoftwareBitmap.Convert(f, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight);
        return "data:image/png;base64," + Convert.ToBase64String(await Immagini.Codifica(d, BitmapEncoder.PngEncoderId));
    }

    static async Task<string> Fotogramma(Strumenti s, string video, double t)
    {
        var png = Path.Combine(Strumenti.CartellaTemporanea, $"confronto-{Guid.NewGuid():N}.png");
        try
        {
            var r = await Processi.Esegui(s.Ffmpeg, ["-hide_banner", "-loglevel", "error", "-y", "-ss", t.ToString("0.###", CultureInfo.InvariantCulture),
                "-i", video, "-frames:v", "1", "-vf", "scale='min(1920,iw)':-2", "-update", "1", png], CancellationToken.None, tieniUscita: false);
            if (r.Codice != 0 || !File.Exists(png)) throw new ErroreConversione("Non riesco a prendere il fotogramma.", r.Errori);
            return "data:image/png;base64," + Convert.ToBase64String(await File.ReadAllBytesAsync(png));
        }
        finally { try { File.Delete(png); } catch { } }
    }
}
