using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DaP.Convertitore.Lettore;
using Windows.Graphics.Imaging;

namespace DaP.Convertitore.App.Miniature;

/// <summary>
/// Le miniature che mostrano il contenuto: il fotogramma del video (col suo tasto play e la pellicola), la forma
/// d'onda della musica (come i vocali di WhatsApp), il modello 3D su uno sfondo da studio, la foto.
/// </summary>
public static class Multimedia
{
    /// <summary>Aspetta un lavoro asincrono dal thread che disegna, senza bloccarsi su di lui.</summary>
    public static T Aspetta<T>(Func<Task<T>> lavoro) => Task.Run(lavoro).GetAwaiter().GetResult();

    static InfoMedia? Sonda_(Strumenti s, string percorso)
    {
        try { return Aspetta(() => Sonda.Leggi(s, percorso, silenzioso: true)); }
        catch { return null; }
    }

    // ————————————————————————————————————— VIDEO —————————————————————————————————————

    public static Quadro? Video(string percorso, int L, Strumenti s)
    {
        var info = Sonda_(s, percorso);
        if (info is null) return null;
        var durata = info.Durata;
        var t = durata > 4 ? durata * 0.1 : 0;
        var img = Tela.Immagine(Fotogramma(s, percorso, L, t)) ?? (t > 0 ? Tela.Immagine(Fotogramma(s, percorso, L, 0)) : null);
        if (img is null) return null;

        var scala = (double)L / Math.Max(img.PixelWidth, img.PixelHeight);
        int fw = Math.Max(2, (int)Math.Round(img.PixelWidth * scala)), fh = Math.Max(2, (int)Math.Round(img.PixelHeight * scala));
        var minimo = Math.Min(fw, fh);
        var grande = L >= 96;
        return Tela.Fai(fw, fh, dc =>
        {
            var r = new Rect(0, 0, fw, fh);
            Tela.Ritaglia(dc, r, minimo * 0.05);
            dc.DrawImage(img, r);

            // un velo scuro in fondo, perché la durata si legga su qualsiasi fotogramma
            var velo = new LinearGradientBrush(Color.FromArgb(0, 0, 0, 0), Color.FromArgb(150, 5, 3, 12), new Point(0, 0.55), new Point(0, 1));
            velo.Freeze();
            dc.DrawRectangle(velo, null, r);

            // la pellicola: due strisce con i fori, sopra e sotto
            var fascia = minimo * 0.085;
            if (grande)
            {
                var scura = Tavolozza.P(Tavolozza.Fondo, 0.62);
                dc.DrawRectangle(scura, null, new Rect(0, 0, fw, fascia));
                dc.DrawRectangle(scura, null, new Rect(0, fh - fascia, fw, fascia));
                var foro = Tavolozza.P(Colors.White, 0.82);
                double fl = fascia * 0.46, fa = fascia * 0.5, passo = fascia * 1.05;
                for (var x = passo * 0.5; x + fl < fw; x += passo)
                {
                    dc.DrawRoundedRectangle(foro, null, new Rect(x, (fascia - fa) / 2, fl, fa), fa * 0.25, fa * 0.25);
                    dc.DrawRoundedRectangle(foro, null, new Rect(x, fh - fascia + (fascia - fa) / 2, fl, fa), fa * 0.25, fa * 0.25);
                }
            }

            // il tasto play al centro: vetro scuro, anello magenta-oro, triangolo bianco
            var d = Math.Max(minimo * 0.36, 14);
            var c = new Point(fw / 2.0, fh / 2.0);
            dc.DrawEllipse(Tavolozza.P(Colors.Black, 0.28), null, c, d / 2 * 1.16, d / 2 * 1.16);
            dc.DrawEllipse(Tavolozza.P(Tavolozza.Fondo, 0.66), null, c, d / 2, d / 2);
            var anello = new LinearGradientBrush(Tavolozza.Magenta, Tavolozza.Oro, 45);
            anello.Freeze();
            dc.DrawEllipse(null, new Pen(anello, Math.Max(1.2, d * 0.07)), c, d / 2 * 0.94, d / 2 * 0.94);
            var tri = new StreamGeometry();
            using (var g = tri.Open())
            {
                double k = d * 0.2;
                g.BeginFigure(new Point(c.X - k * 0.7, c.Y - k * 1.15), true, true);
                g.LineTo(new Point(c.X + k * 1.25, c.Y), true, false);
                g.LineTo(new Point(c.X - k * 0.7, c.Y + k * 1.15), true, false);
            }
            tri.Freeze();
            dc.DrawGeometry(Tavolozza.P(Colors.White), new Pen(Tavolozza.P(Colors.White), d * 0.04) { LineJoin = PenLineJoin.Round }, tri);

            // la durata, in basso a destra
            if (grande && durata > 0)
            {
                var h = Math.Max(11, minimo * 0.1);
                Tela.Pastiglia(dc, Tela.Durata(durata), fw - minimo * 0.04, fh - fascia - h - minimo * 0.025, h,
                    Tavolozza.P(Colors.Black, 0.72), Tavolozza.P(Colors.White), aDestra: true);
            }
            dc.Pop();
            dc.DrawRoundedRectangle(null, Tavolozza.Penna(Colors.White, 1, 0.14), new Rect(0.5, 0.5, fw - 1, fh - 1), minimo * 0.05, minimo * 0.05);
        });
    }

    static byte[]? Fotogramma(Strumenti s, string percorso, int lato, double secondo)
    {
        var filtro = $"scale='if(gt(iw,ih),min({lato},iw),-2)':'if(gt(iw,ih),-2,min({lato},ih))'";
        return Aspetta(() => Rapido.Uscita(s.Ffmpeg,
            ["-hide_banner", "-loglevel", "error", "-nostdin", "-ss", secondo.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
             "-i", percorso, "-an", "-sn", "-frames:v", "1", "-vf", filtro, "-q:v", "3", "-f", "mjpeg", "-"],
            TimeSpan.FromSeconds(25)));
    }

    // ————————————————————————————————————— AUDIO —————————————————————————————————————

    public static Quadro? Audio(string percorso, int L, Strumenti s)
    {
        int W = L, H = (int)Math.Round(L * 0.7);
        var u = L / 256.0;
        double larBarra = Math.Max(1.6, 3.3 * u), vuoto = larBarra * 0.72;
        double x0 = W * 0.285, x1 = W * 0.93;
        var barre = Math.Max(8, (int)((x1 - x0 + vuoto) / (larBarra + vuoto)));
        var onda = Aspetta(() => Onde.Barre(s, percorso, barre, TimeSpan.FromSeconds(14)));
        if (onda.Length == 0) return null;
        var info = Sonda_(s, percorso);
        var durata = info?.Durata ?? 0;
        var est = Path.GetExtension(percorso).TrimStart('.').ToUpperInvariant();
        var grande = L >= 96;

        return Tela.Fai(W, H, dc =>
        {
            var r = new Rect(0, 0, W, H);
            var raggio = H * 0.1;
            var fondo = new LinearGradientBrush(Tavolozza.ViolaChiaro, Tavolozza.Fondo, 125);
            fondo.Freeze();
            dc.DrawRoundedRectangle(fondo, null, r, raggio, raggio);
            Tela.Ritaglia(dc, r, raggio);
            var luce = new RadialGradientBrush(Color.FromArgb(70, 255, 61, 242), Color.FromArgb(0, 255, 61, 242)) { Center = new Point(0.12, 0.45), GradientOrigin = new Point(0.12, 0.45), RadiusX = 0.6, RadiusY = 0.9 };
            luce.Freeze();
            dc.DrawRectangle(luce, null, r);

            // il tasto play a sinistra, come nei vocali
            double cy = H * (grande ? 0.43 : 0.5), d = H * 0.3;
            var c = new Point(W * 0.145, cy);
            var oro = new LinearGradientBrush(Tavolozza.Oro, Tavolozza.Oro2, 90);
            oro.Freeze();
            dc.DrawEllipse(oro, null, c, d / 2, d / 2);
            var tri = new StreamGeometry();
            using (var g = tri.Open())
            {
                double k = d * 0.2;
                g.BeginFigure(new Point(c.X - k * 0.6, c.Y - k * 1.1), true, true);
                g.LineTo(new Point(c.X + k * 1.2, c.Y), true, false);
                g.LineTo(new Point(c.X - k * 0.6, c.Y + k * 1.1), true, false);
            }
            tri.Freeze();
            dc.DrawGeometry(Tavolozza.P(Tavolozza.Fondo), null, tri);

            // le barre: la forma d'onda vera del brano, magenta all'inizio e oro alla fine
            var sfum = new LinearGradientBrush(Tavolozza.Magenta, Tavolozza.Oro, new Point(x0, 0), new Point(x1, 0)) { MappingMode = BrushMappingMode.Absolute };
            sfum.Freeze();
            double massimo = H * (grande ? 0.31 : 0.4), minimo = larBarra * 0.6;
            for (var i = 0; i < barre; i++)
            {
                var mezza = minimo + onda[i] * (massimo - minimo);
                var x = x0 + i * (larBarra + vuoto);
                dc.DrawRoundedRectangle(sfum, null, new Rect(x, cy - mezza, larBarra, mezza * 2), larBarra / 2, larBarra / 2);
            }

            if (grande)
            {
                var h = Math.Max(11, H * 0.115);
                var y = H - h - H * 0.07;
                var vetro = Tavolozza.P(Colors.Black, 0.38);
                if (est.Length is > 0 and <= 5) Tela.Pastiglia(dc, est, W * 0.045, y, h, vetro, Tavolozza.P(Tavolozza.Oro));
                if (durata > 0) Tela.Pastiglia(dc, Tela.Durata(durata), W * 0.955, y, h, vetro, Tavolozza.P(Colors.White, 0.92), aDestra: true);
            }
            dc.Pop();
            dc.DrawRoundedRectangle(null, Tavolozza.Penna(Colors.White, 1, 0.12), new Rect(0.5, 0.5, W - 1, H - 1), raggio, raggio);
        });
    }

    // ————————————————————————————————————— MODELLO 3D —————————————————————————————————————

    public static Quadro? Modello(string percorso, int L)
    {
        var maglia = Modelli.Leggi(percorso);
        if (maglia is null) return null;
        var d = Studio.Disegna(maglia, L, L, margine: 0.15);
        var bmp = BitmapSource.Create(d.Larghezza, d.Altezza, 96, 96, PixelFormats.Pbgra32, null, d.Pixel, d.Larghezza * 4);
        bmp.Freeze();
        var est = Path.GetExtension(percorso).TrimStart('.').ToUpperInvariant();
        var grande = L >= 96;

        return Tela.Fai(L, L, dc =>
        {
            var r = new Rect(0, 0, L, L);
            var raggio = L * 0.07;
            var fondo = new RadialGradientBrush(Tavolozza.C("#3a2268"), Tavolozza.Fondo) { Center = new Point(0.5, 0.42), GradientOrigin = new Point(0.5, 0.35), RadiusX = 0.85, RadiusY = 0.85 };
            fondo.Freeze();
            dc.DrawRoundedRectangle(fondo, null, r, raggio, raggio);
            Tela.Ritaglia(dc, r, raggio);

            // il pavimento: un'ombra morbida e un anello magenta sotto il modello
            double cx = (d.Sinistra + d.Destra) / 2 * L, base_ = d.Basso * L;
            double rx = Math.Max(L * 0.1, (d.Destra - d.Sinistra) * L * 0.62), ry = Math.Max(3, L * 0.045);
            var cb = new Point(cx, Math.Min(L - ry - 1, base_ - ry * 0.35));
            var ombra = new RadialGradientBrush(Color.FromArgb(150, 0, 0, 0), Color.FromArgb(0, 0, 0, 0));
            ombra.Freeze();
            dc.DrawEllipse(ombra, null, cb, rx * 1.15, ry * 1.5);
            dc.DrawEllipse(null, Tavolozza.Penna(Tavolozza.Magenta, Math.Max(1, L * 0.006), 0.55), cb, rx * 0.95, ry * 0.95);
            dc.DrawEllipse(null, Tavolozza.Penna(Tavolozza.Oro, Math.Max(1, L * 0.004), 0.25), cb, rx * 1.18, ry * 1.2);

            dc.DrawImage(bmp, r);

            if (grande)
            {
                var h = Math.Max(11, L * 0.075);
                var vetro = Tavolozza.P(Colors.Black, 0.4);
                Tela.Pastiglia(dc, "3D", L * 0.045, L * 0.045, h, Tavolozza.P(Tavolozza.Oro), Tavolozza.P(Tavolozza.Fondo));
                if (est.Length is > 0 and <= 5) Tela.Pastiglia(dc, est, L * 0.955, L - h - L * 0.045, h, vetro, Tavolozza.P(Colors.White, 0.9), aDestra: true);
            }
            dc.Pop();
            dc.DrawRoundedRectangle(null, Tavolozza.Penna(Colors.White, 1, 0.12), new Rect(0.5, 0.5, L - 1, L - 1), raggio, raggio);
        });
    }

    // ————————————————————————————————————— FOTO —————————————————————————————————————

    /// <summary>
    /// Le foto che Windows non sa mostrare (HEIC senza estensione, RAW, PSD, TGA, JXL, SVG…): le apre il motore del
    /// convertitore. Quelle che Windows mostra già bene (JPG, PNG…) restano a Windows.
    /// </summary>
    public static Quadro? Foto(string percorso, int L, Strumenti s)
    {
        var est = Path.GetExtension(percorso).TrimStart('.').ToUpperInvariant();
        var etichetta = est is "JPG" or "JPEG" or "PNG" or "JPE" or "JFIF" ? "" : est;
        // la via veloce: il decoder di Windows (WIC) rimpicciolisce mentre legge, e la foto è subito pronta
        if (FotoVeloce(percorso, L) is { } veloce) return Incornicia(veloce, L, etichetta, null);
        var png = Aspetta(async () =>
        {
            var ctx = new Contesto(s, new InfoHardware("", 1, [], []), CancellationToken.None, _ => { });
            try
            {
                var foto = await Immagini.Apri(percorso, ctx, (uint)L);
                if (foto.BitmapAlphaMode != BitmapAlphaMode.Straight) foto = SoftwareBitmap.Convert(foto, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight);
                return await Immagini.Codifica(foto, Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId);
            }
            finally { ctx.Pulisci(); }
        });
        var img = Tela.Immagine(png);
        return img is null ? null : Incornicia(img, L, etichetta, null);
    }

    /// <summary>La foto col decoder di WPF/WIC, già girata come l'ha scattata il telefono (orientamento EXIF). Null se non la legge.</summary>
    static BitmapSource? FotoVeloce(string percorso, int L)
    {
        try
        {
            int larghezza, altezza, orientamento = 1;
            using (var f = File.OpenRead(percorso))
            {
                var dec = System.Windows.Media.Imaging.BitmapDecoder.Create(f, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                var cornice = dec.Frames[0];
                larghezza = cornice.PixelWidth; altezza = cornice.PixelHeight;
                try { if (cornice.Metadata is BitmapMetadata m && m.GetQuery("/app1/ifd/{ushort=274}") is ushort o) orientamento = o; } catch { }
            }
            if (larghezza <= 0 || altezza <= 0) return null;
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.UriSource = new Uri(Path.GetFullPath(percorso));
            bi.CacheOption = BitmapCacheOption.OnLoad;
            if (larghezza >= altezza) { if (larghezza > L) bi.DecodePixelWidth = L; } else if (altezza > L) bi.DecodePixelHeight = L;
            bi.EndInit();
            bi.Freeze();
            BitmapSource r = bi;
            Transform? t = orientamento switch
            {
                2 => new ScaleTransform(-1, 1),
                3 => new RotateTransform(180),
                4 => new ScaleTransform(1, -1),
                5 => new TransformGroup { Children = { new RotateTransform(90), new ScaleTransform(-1, 1) } },
                6 => new RotateTransform(90),
                7 => new TransformGroup { Children = { new RotateTransform(270), new ScaleTransform(-1, 1) } },
                8 => new RotateTransform(270),
                _ => null,
            };
            if (t is not null) { r = new TransformedBitmap(bi, t); r.Freeze(); }
            return r;
        }
        catch { return null; }
    }

    /// <summary>
    /// Una pagina o una foto con la sua cornice: angoli appena tondi, un filo di luce, e se serve la linguetta
    /// colorata del tipo («PDF», «HEIC»…) in alto a destra.
    /// </summary>
    public static Quadro Incornicia(BitmapSource img, int L, string etichetta, Color? tinta)
    {
        var scala = (double)L / Math.Max(img.PixelWidth, img.PixelHeight);
        int w = Math.Max(2, (int)Math.Round(img.PixelWidth * scala)), h = Math.Max(2, (int)Math.Round(img.PixelHeight * scala));
        var minimo = Math.Min(w, h);
        return Tela.Fai(w, h, dc =>
        {
            var r = new Rect(0, 0, w, h);
            var raggio = minimo * (tinta is null ? 0.04 : 0.03);
            Tela.Ritaglia(dc, r, raggio);
            dc.DrawImage(img, r);
            dc.Pop();
            dc.DrawRoundedRectangle(null, Tavolozza.Penna(tinta is null ? Colors.White : Colors.Black, 1, tinta is null ? 0.14 : 0.2), new Rect(0.5, 0.5, w - 1, h - 1), raggio, raggio);
            if (L >= 96 && etichetta.Length is > 0 and <= 5)
            {
                var alt = Math.Max(11, minimo * 0.085);
                if (tinta is { } t) Tela.Pastiglia(dc, etichetta, w - minimo * 0.04, minimo * 0.04, alt, Tavolozza.P(t), Tavolozza.P(Colors.White), aDestra: true);
                else Tela.Pastiglia(dc, etichetta, w - minimo * 0.04, h - alt - minimo * 0.04, alt, Tavolozza.P(Colors.Black, 0.55), Tavolozza.P(Colors.White, 0.92), aDestra: true);
            }
        });
    }
}
