using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace DaP.Convertitore;

/// <summary>
/// Le immagini passano da WIC, il motore di Windows: legge HEIC, RAW delle fotocamere, WEBP, AVIF con le estensioni
/// di Windows, e gira la foto come la vede il telefono (orientamento EXIF). Quello che WIC non legge (TGA, EXR, PSD,
/// SVG, JXL…) lo legge FFmpeg. JPG, PNG, TIFF, BMP e HEIC li scrive WIC; WEBP, AVIF, JXL e GIF FFmpeg; l'ICO lo
/// componiamo noi con tutte le misure dentro.
/// I dati nascosti (posizione GPS compresa) non passano: le date del file sì.
/// </summary>
public static class Immagini
{
    public static async Task Converti(Lavoro l, Contesto ctx)
    {
        var o = l.Opzioni.Immagine;
        var est = l.Formato.Estensione;
        ctx.Fase("Apro l'immagine");
        var foto = await Apri(l.Sorgente, ctx, (uint)Math.Max(0, o.Lato));
        ctx.Fase("Scrivo", 0.5);
        await Scrivi(foto, est, l.Bozza, o, ctx);
    }

    /// <summary>Apre un'immagine qualsiasi in BGRA, già girata per il verso giusto e, se serve, rimpicciolita.</summary>
    public static async Task<SoftwareBitmap> Apri(string percorso, Contesto ctx, uint latoMax = 0)
    {
        try
        {
            return await ApriConWic(percorso, latoMax);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            Registro.Scrivi($"WIC non legge {Path.GetFileName(percorso)} ({e.Message}): provo con FFmpeg");
        }
        var png = Path.Combine(ctx.Temporanea(), "sorgente.png");
        var r = await Processi.Esegui(ctx.Strumenti.Ffmpeg,
            ["-hide_banner", "-loglevel", "error", "-y", "-i", percorso, "-frames:v", "1", "-update", "1", "-c:v", "png", "-f", "image2", png], ctx.Ct);
        if (r.Codice != 0 || !File.Exists(png))
            throw new ErroreConversione("Questa immagine non si riesce ad aprire.", r.Errori);
        return await ApriConWic(png, latoMax);
    }

    static async Task<SoftwareBitmap> ApriConWic(string percorso, uint latoMax)
    {
        var file = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(percorso));
        using var flusso = await file.OpenAsync(FileAccessMode.Read);
        var dec = await BitmapDecoder.CreateAsync(flusso);
        var t = new BitmapTransform { InterpolationMode = BitmapInterpolationMode.Fant };
        var lungo = Math.Max(dec.OrientedPixelWidth, dec.OrientedPixelHeight);
        if (latoMax > 0 && lungo > latoMax)
        {
            var scala = latoMax / (double)lungo;
            t.ScaledWidth = (uint)Math.Max(1, Math.Round(dec.PixelWidth * scala));
            t.ScaledHeight = (uint)Math.Max(1, Math.Round(dec.PixelHeight * scala));
        }
        return await dec.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, t,
            ExifOrientationMode.RespectExifOrientation, ColorManagementMode.ColorManageToSRgb);
    }

    public static async Task Scrivi(SoftwareBitmap foto, string est, string uscita, OpzioniImmagine o, Contesto ctx)
    {
        var q = Math.Clamp(o.Qualita, 1, 100);
        switch (est)
        {
            case ".jpg":
            case ".heic":
            {
                var id = est == ".jpg" ? BitmapEncoder.JpegEncoderId : BitmapEncoder.HeifEncoderId;
                var piena = SuBianco(foto);
                byte[] Fai(int qq, double scala) => Codifica(piena, id, qq / 100f, scala).GetAwaiter().GetResult();
                byte[] dati;
                try { dati = o.PesoMaxByte > 0 ? CercaPeso(Fai, q, o.PesoMaxByte) : Fai(q, 1); }
                catch (Exception e) when (est == ".heic")
                {
                    throw new ErroreConversione("Per scrivere HEIC Windows vuole le «Estensioni video HEVC» dallo Store.", e.Message);
                }
                await File.WriteAllBytesAsync(uscita, dati, ctx.Ct);
                break;
            }
            case ".png":
                await File.WriteAllBytesAsync(uscita, await Codifica(Diritta(foto), BitmapEncoder.PngEncoderId), ctx.Ct);
                break;
            case ".bmp":
                await File.WriteAllBytesAsync(uscita, await Codifica(SuBianco(foto), BitmapEncoder.BmpEncoderId), ctx.Ct);
                break;
            case ".tif":
            case ".tiff":
                await File.WriteAllBytesAsync(uscita, await Codifica(Diritta(foto), BitmapEncoder.TiffEncoderId, null, 1,
                    ("TiffCompressionMethod", new BitmapTypedValue((byte)4, Windows.Foundation.PropertyType.UInt8))), ctx.Ct);
                break;
            case ".ico":
                await File.WriteAllBytesAsync(uscita, await Icona(foto), ctx.Ct);
                break;
            case ".webp":
            case ".avif":
            case ".jxl":
            case ".gif":
                await ConFfmpeg(foto, est, uscita, o, ctx);
                break;
            default:
                throw new ErroreConversione($"Non so scrivere {est}.");
        }
    }

    /// <summary>WEBP, AVIF, JXL e GIF: si passa a FFmpeg un PNG pulito.</summary>
    static async Task ConFfmpeg(SoftwareBitmap foto, string est, string uscita, OpzioniImmagine o, Contesto ctx)
    {
        var cartella = ctx.Temporanea();
        var png = Path.Combine(cartella, "mezzo.png");
        // l'AVIF di FFmpeg non porta la trasparenza: si mette su bianco invece di lasciarla nera
        await File.WriteAllBytesAsync(png, await Codifica(est == ".avif" ? SuBianco(foto) : Diritta(foto), BitmapEncoder.PngEncoderId), ctx.Ct);

        async Task<byte[]> Fai(int q, double scala)
        {
            var dest = Path.Combine(cartella, "uscita" + est);
            var a = new List<string> { "-hide_banner", "-loglevel", "error", "-y", "-i", png };
            if (scala < 1) a.AddRange(["-vf", $"scale=trunc(iw*{scala.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)}/2)*2:-2:flags=lanczos"]);
            switch (est)
            {
                case ".webp":
                    a.AddRange(["-c:v", "libwebp", "-compression_level", "6", "-preset", "photo"]);
                    a.AddRange(q >= 100 ? ["-lossless", "1"] : ["-quality", $"{q}"]);
                    break;
                case ".avif":
                    // da RGB a YUV con la matrice dichiarata (BT.709, gamma piena, sRGB): senza, FFmpeg usa la BT.601
                    // senza dirlo e chi apre il file vede i colori più chiari
                    if (scala >= 1) a.AddRange(["-vf", "scale=out_color_matrix=bt709:out_range=pc"]);
                    else a[^1] += ":out_color_matrix=bt709:out_range=pc";
                    a.AddRange(["-c:v", "libaom-av1", "-still-picture", "1", "-crf", $"{(int)Math.Round(55 - q * 0.35)}", "-b:v", "0",
                        "-cpu-used", "4", "-row-mt", "1", "-pix_fmt", "yuv420p",
                        "-colorspace", "bt709", "-color_primaries", "bt709", "-color_trc", "iec61966-2-1", "-color_range", "pc", "-f", "avif"]);
                    break;
                case ".jxl":
                    a.AddRange(["-c:v", "libjxl", "-distance", $"{Math.Max(0, (100 - q) * 0.1):0.0}".Replace(',', '.'), "-effort", "7"]);
                    break;
                case ".gif":
                    a.AddRange(["-filter_complex", "split[a][b];[a]palettegen=max_colors=256[p];[b][p]paletteuse=dither=sierra2_4a", "-f", "gif"]);
                    break;
            }
            a.AddRange(["-frames:v", "1", dest]);
            var r = await Processi.Esegui(ctx.Strumenti.Ffmpeg, a, ctx.Ct);
            if (r.Codice != 0) throw new ErroreConversione(Ffmpeg.Spiega(r.Errori), r.Errori);
            return await File.ReadAllBytesAsync(dest, ctx.Ct);
        }

        var q0 = Math.Clamp(o.Qualita, 1, 100);
        var dati = o.PesoMaxByte > 0 && est != ".gif"
            ? CercaPeso((qq, s) => Fai(qq, s).GetAwaiter().GetResult(), q0, o.PesoMaxByte)
            : await Fai(q0, 1);
        await File.WriteAllBytesAsync(uscita, dati, ctx.Ct);
    }

    /// <summary>
    /// "Sotto i 2 MB": si cerca la qualità più alta che ci sta, dimezzando l'intervallo (al massimo 7 prove).
    /// Sotto 35 la foto si rovina: piuttosto si rimpicciolisce, di quanto serve, e si ricerca. Ci sta di sicuro.
    /// </summary>
    public static byte[] CercaPeso(Func<int, double, byte[]> fai, int qualitaMax, long pesoMax)
    {
        const int pavimento = 35;
        double scala = 1;
        byte[] ultimo = [];
        for (var giro = 0; giro < 8; giro++)
        {
            var d = fai(qualitaMax, scala);
            if (d.LongLength <= pesoMax) return d;
            int basso = Math.Min(pavimento, qualitaMax), alto = qualitaMax - 1;
            byte[]? trovato = null;
            for (var i = 0; i < 7 && basso <= alto; i++)
            {
                var mezzo = (basso + alto) / 2;
                var prova = fai(mezzo, scala);
                if (prova.LongLength <= pesoMax) { trovato = prova; basso = mezzo + 1; }
                else alto = mezzo - 1;
            }
            if (trovato is not null) return trovato;
            ultimo = fai(Math.Min(pavimento, qualitaMax), scala);
            if (ultimo.LongLength <= pesoMax) return ultimo;
            // il peso va col numero di pixel: si scende della radice del rapporto, con un po' di margine
            scala *= Math.Clamp(Math.Sqrt(pesoMax / (double)ultimo.LongLength) * 0.95, 0.3, 0.9);
        }
        return ultimo;
    }

    public static async Task<byte[]> Codifica(SoftwareBitmap foto, Guid encoder, float? qualita = null, double scala = 1, params (string chiave, BitmapTypedValue valore)[] altro)
    {
        using var flusso = new InMemoryRandomAccessStream();
        var proprieta = new BitmapPropertySet();
        if (qualita is { } q) proprieta.Add("ImageQuality", new BitmapTypedValue(q, Windows.Foundation.PropertyType.Single));
        foreach (var (k, v) in altro) proprieta.Add(k, v);
        var enc = await BitmapEncoder.CreateAsync(encoder, flusso, proprieta);
        enc.SetSoftwareBitmap(foto);
        if (scala < 1)
        {
            enc.BitmapTransform.ScaledWidth = (uint)Math.Max(1, Math.Round(foto.PixelWidth * scala));
            enc.BitmapTransform.ScaledHeight = (uint)Math.Max(1, Math.Round(foto.PixelHeight * scala));
            enc.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;
        }
        await enc.FlushAsync();
        var dati = new byte[flusso.Size];
        flusso.Seek(0);
        await flusso.ReadAsync(dati.AsBuffer(), (uint)dati.Length, InputStreamOptions.None);
        return dati;
    }

    static SoftwareBitmap Diritta(SoftwareBitmap foto) =>
        foto.BitmapAlphaMode == BitmapAlphaMode.Straight ? foto : SoftwareBitmap.Convert(foto, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight);

    /// <summary>Per i formati senza trasparenza: i pixel trasparenti diventano bianchi, non neri.</summary>
    public static SoftwareBitmap SuBianco(SoftwareBitmap foto)
    {
        var pre = foto.BitmapAlphaMode == BitmapAlphaMode.Premultiplied ? foto : SoftwareBitmap.Convert(foto, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        var px = new byte[4 * pre.PixelWidth * pre.PixelHeight];
        pre.CopyToBuffer(px.AsBuffer());
        var trasparente = false;
        for (var i = 0; i < px.Length; i += 4)
        {
            var a = px[i + 3];
            if (a == 255) continue;
            trasparente = true;
            var resto = 255 - a;
            px[i] = (byte)Math.Min(255, px[i] + resto);
            px[i + 1] = (byte)Math.Min(255, px[i + 1] + resto);
            px[i + 2] = (byte)Math.Min(255, px[i + 2] + resto);
            px[i + 3] = 255;
        }
        if (!trasparente) return pre;
        return SoftwareBitmap.CreateCopyFromBuffer(px.AsBuffer(), BitmapPixelFormat.Bgra8, pre.PixelWidth, pre.PixelHeight, BitmapAlphaMode.Premultiplied);
    }

    /// <summary>Un .ico vero, con dentro 16, 24, 32, 48, 64, 128 e 256 pixel: Windows prende quella giusta.</summary>
    public static async Task<byte[]> Icona(SoftwareBitmap foto)
    {
        int[] misure = [16, 24, 32, 48, 64, 128, 256];
        var immagini = new List<(int lato, byte[] png)>();
        var dritta = Diritta(foto);
        foreach (var lato in misure)
        {
            var quadra = await Quadrata(dritta, lato);
            immagini.Add((lato, await Codifica(quadra, BitmapEncoder.PngEncoderId)));
        }
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write((short)0); w.Write((short)1); w.Write((short)immagini.Count);
        var posto = 6 + 16 * immagini.Count;
        foreach (var (lato, png) in immagini)
        {
            w.Write((byte)(lato >= 256 ? 0 : lato)); w.Write((byte)(lato >= 256 ? 0 : lato));
            w.Write((byte)0); w.Write((byte)0); w.Write((short)1); w.Write((short)32);
            w.Write(png.Length); w.Write(posto);
            posto += png.Length;
        }
        foreach (var (_, png) in immagini) w.Write(png);
        return ms.ToArray();
    }

    /// <summary>L'immagine rimpicciolita dentro un quadrato trasparente, centrata.</summary>
    static async Task<SoftwareBitmap> Quadrata(SoftwareBitmap foto, int lato)
    {
        var scala = lato / (double)Math.Max(foto.PixelWidth, foto.PixelHeight);
        uint w = (uint)Math.Max(1, Math.Round(foto.PixelWidth * scala)), h = (uint)Math.Max(1, Math.Round(foto.PixelHeight * scala));
        // si rimpicciolisce con WIC (Fant) passando da un PNG in memoria
        var png = await Codifica(foto, BitmapEncoder.PngEncoderId);
        using var flusso = new InMemoryRandomAccessStream();
        await flusso.WriteAsync(png.AsBuffer());
        flusso.Seek(0);
        var dec = await BitmapDecoder.CreateAsync(flusso);
        var piccola = await dec.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight,
            new BitmapTransform { ScaledWidth = w, ScaledHeight = h, InterpolationMode = BitmapInterpolationMode.Fant },
            ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage);
        var sorg = new byte[4 * w * h];
        piccola.CopyToBuffer(sorg.AsBuffer());
        var dest = new byte[4 * lato * lato];
        int ox = (lato - (int)w) / 2, oy = (lato - (int)h) / 2;
        for (var y = 0; y < h; y++)
            System.Buffer.BlockCopy(sorg, (int)(y * w * 4), dest, ((oy + y) * lato + ox) * 4, (int)w * 4);
        return SoftwareBitmap.CreateCopyFromBuffer(dest.AsBuffer(), BitmapPixelFormat.Bgra8, lato, lato, BitmapAlphaMode.Straight);
    }

    /// <summary>Le misure di un'immagine, già girate, senza decodificarla tutta.</summary>
    public static async Task<(int w, int h)?> Misure(string percorso)
    {
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(percorso));
            using var flusso = await file.OpenAsync(FileAccessMode.Read);
            var dec = await BitmapDecoder.CreateAsync(flusso);
            return ((int)dec.OrientedPixelWidth, (int)dec.OrientedPixelHeight);
        }
        catch { return null; }
    }
}
