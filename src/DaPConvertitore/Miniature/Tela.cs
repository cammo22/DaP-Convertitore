using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DaP.Convertitore.App.Miniature;

/// <summary>Un disegno finito: pixel BGRA premoltiplicati, riga per riga dall'alto.</summary>
public sealed record Quadro(int Larghezza, int Altezza, byte[] Pixel);

/// <summary>I colori DaProd: viola notte, oro, magenta. E una tinta per ogni tipo di file (le stesse dell'interfaccia).</summary>
public static class Tavolozza
{
    public static Color C(string esadecimale) => (Color)ColorConverter.ConvertFromString(esadecimale);

    public static readonly Color Fondo = C("#0d0a1a");
    public static readonly Color Viola = C("#1d1236");
    public static readonly Color ViolaChiaro = C("#35205f");
    public static readonly Color Oro = C("#ffd54a");
    public static readonly Color Oro2 = C("#ffab00");
    public static readonly Color Magenta = C("#ff3df2");
    public static readonly Color Ciano = C("#35e8ff");
    public static readonly Color Bianco = Colors.White;

    public static SolidColorBrush P(Color c, double alfa = 1)
    {
        var b = new SolidColorBrush(Color.FromArgb((byte)Math.Clamp(alfa * 255, 0, 255), c.R, c.G, c.B));
        b.Freeze();
        return b;
    }

    public static Pen Penna(Color c, double spessore, double alfa = 1, bool tonda = true)
    {
        var p = new Pen(P(c, alfa), spessore);
        if (tonda) { p.StartLineCap = PenLineCap.Round; p.EndLineCap = PenLineCap.Round; p.LineJoin = PenLineJoin.Round; }
        p.Freeze();
        return p;
    }

    public static LinearGradientBrush Sfumatura(Color da, Color a, double angolo = 90)
    {
        var rad = angolo * Math.PI / 180;
        var b = new LinearGradientBrush(da, a, new Point(0.5 - Math.Cos(rad) / 2, 0.5 - Math.Sin(rad) / 2), new Point(0.5 + Math.Cos(rad) / 2, 0.5 + Math.Sin(rad) / 2));
        b.Freeze();
        return b;
    }

    /// <summary>La tinta di un gruppo di «Apri con» (Video, Audio, Modello…).</summary>
    public static Color Tinta(string gruppo) => gruppo switch
    {
        "Video" => C("#ff3df2"),
        "Audio" => C("#5dffb4"),
        "Immagine" => C("#35e8ff"),
        "Pdf" => C("#ff4d6d"),
        "Documento" => C("#6ea8ff"),
        "Presentazione" => C("#ff9f43"),
        "Tabella" => C("#4dd17a"),
        "Testo" => C("#ebe7f4"),
        "Codice" => C("#8fd0ff"),
        "Sottotitoli" => C("#ffe58a"),
        "Archivio" => C("#d9ae6e"),
        "Font" => C("#b48cff"),
        "Modello" => C("#ffb62e"),
        _ => C("#a19db0"),
    };
}

/// <summary>Carattere e testo: Bahnschrift (c'è in Windows da anni) per le scritte, Consolas per il codice.</summary>
public static class Scritte
{
    public static readonly Typeface Titolo = new(new FontFamily("Bahnschrift"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
    public static readonly Typeface Corpo = new(new FontFamily("Bahnschrift"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    public static readonly Typeface Codice = new(new FontFamily("Cascadia Mono, Consolas, Courier New"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

    public static FormattedText Testo(string s, Typeface tf, double dimensione, Brush pennello, double larghezzaMax = 0)
    {
        var t = new FormattedText(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, tf, dimensione, pennello, 1.0);
        if (larghezzaMax > 0)
        {
            t.MaxTextWidth = larghezzaMax;
            t.MaxLineCount = 1;
            t.Trimming = TextTrimming.CharacterEllipsis;
        }
        return t;
    }
}

/// <summary>Il foglio su cui si disegna: un DrawingVisual reso in un bitmap.</summary>
public static class Tela
{
    public static Quadro Fai(int larghezza, int altezza, Action<DrawingContext> disegna)
    {
        larghezza = Math.Max(1, larghezza);
        altezza = Math.Max(1, altezza);
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen()) disegna(dc);
        var rtb = new RenderTargetBitmap(larghezza, altezza, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        var px = new byte[larghezza * altezza * 4];
        rtb.CopyPixels(px, larghezza * 4, 0);
        return new Quadro(larghezza, altezza, px);
    }

    public static byte[] Png(Quadro q)
    {
        var src = BitmapSource.Create(q.Larghezza, q.Altezza, 96, 96, PixelFormats.Pbgra32, null, q.Pixel, q.Larghezza * 4);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(src));
        using var m = new MemoryStream();
        enc.Save(m);
        return m.ToArray();
    }

    /// <summary>Un'immagine da byte (JPEG, PNG…), già pronta e «congelata».</summary>
    public static BitmapSource? Immagine(byte[]? dati)
    {
        if (dati is not { Length: > 0 }) return null;
        try
        {
            using var m = new MemoryStream(dati);
            var dec = BitmapDecoder.Create(m, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var f = dec.Frames[0];
            f.Freeze();
            return f;
        }
        catch { return null; }
    }

    /// <summary>Un angolo tondo: il disegno si ritaglia con questo.</summary>
    public static void Ritaglia(DrawingContext dc, Rect r, double raggio) =>
        dc.PushClip(new RectangleGeometry(r, raggio, raggio));

    /// <summary>
    /// Una pastiglia con una scritta (estensione, durata…). Si ancora a sinistra o a destra di <paramref name="x"/>.
    /// Restituisce il suo rettangolo.
    /// </summary>
    public static Rect Pastiglia(DrawingContext dc, string testo, double x, double y, double altezza, Brush sfondo, Brush colore, bool aDestra = false, Typeface? tf = null)
    {
        var t = Scritte.Testo(testo, tf ?? Scritte.Titolo, altezza * 0.62, colore);
        var w = t.Width + altezza * 0.8;
        var r = new Rect(aDestra ? x - w : x, y, w, altezza);
        dc.DrawRoundedRectangle(sfondo, null, r, altezza * 0.3, altezza * 0.3);
        dc.DrawText(t, new Point(r.X + altezza * 0.4, r.Y + (altezza - t.Height) / 2));
        return r;
    }

    public static string Durata(double secondi)
    {
        if (secondi < 0 || double.IsNaN(secondi) || double.IsInfinity(secondi)) return "";
        var t = TimeSpan.FromSeconds(Math.Round(secondi));
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes}:{t.Seconds:00}";
    }
}
