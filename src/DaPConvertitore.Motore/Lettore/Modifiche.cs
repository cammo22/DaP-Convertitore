using System.Globalization;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using Windows.Graphics.Imaging;
using Windows.Storage;
using PdfSharp.Pdf.IO;

namespace DaP.Convertitore.Lettore;

/// <summary>
/// Le piccole modifiche che si fanno guardando un file: girare e specchiare le foto, girare i video, girare le
/// pagine di un PDF, tagliare un video. Tutto va sul file vero, con una bozza accanto che prende il posto
/// dell'originale solo a lavoro finito: se qualcosa va storto l'originale resta com'era.
/// Quando si può si cambia solo l'etichetta (l'orientamento EXIF di una JPG, la rotazione dentro un MP4): niente
/// ricodifica, niente perdite, un attimo anche su un film da 20 GB.
/// </summary>
public static class Modifiche
{
    // ————————————————————————— foto —————————————————————————

    /// <summary>
    /// Un'orientazione EXIF (1…8) come «prima si specchia, poi si gira in senso orario»: (gradi, specchio).
    /// </summary>
    static readonly (int Gradi, bool Specchio)[] exif =
    [
        (0, false), (0, false), (0, true), (180, false), (180, true), (270, true), (90, false), (90, true), (270, false),
    ];

    static int DaExif(int gradi, bool specchio)
    {
        for (var i = 1; i <= 8; i++) if (exif[i].Gradi == gradi && exif[i].Specchio == specchio) return i;
        return 1;
    }

    /// <summary>
    /// A cosa porta girare di <paramref name="gradi"/> (orari) e specchiare (prima), partendo dall'orientazione
    /// <paramref name="o"/>. Specchiare dopo una rotazione inverte il verso della rotazione.
    /// </summary>
    public static int ComponiExif(int o, int gradi, bool specchio)
    {
        var (r, f) = exif[Math.Clamp(o, 1, 8)];
        if (specchio) { r = (360 - r) % 360; f = !f; }
        r = ((r + gradi) % 360 + 360) % 360;
        return DaExif(r, f);
    }

    /// <summary>Il formato si può girare e salvare? (se no la pagina lo dice, e la rotazione resta solo da guardare)</summary>
    public static string? SalvaFoto(string percorso) => Path.GetExtension(percorso).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" or ".jpe" or ".jfif" or ".tif" or ".tiff" or ".png" or ".bmp" => null,
        ".gif" or ".webp" or ".avif" => "Questo formato (GIF, WEBP, AVIF) non tiene la rotazione: converti in JPG o PNG con Ctrl+E.",
        ".heic" or ".heif" => "Per l'HEIC la rotazione si vede soltanto: converti in JPG con Ctrl+E.",
        _ => "Questo formato non si salva girato: converti in JPG o PNG con Ctrl+E.",
    };

    /// <summary>Gira (gradi orari: 0, 90, 180, 270) e specchia (prima) la foto, e la salva nel file.</summary>
    public static async Task RuotaFoto(string percorso, int gradi, bool specchio)
    {
        if (SalvaFoto(percorso) is { } no) throw new ErroreConversione(no);
        gradi = ((gradi % 360) + 360) % 360;
        if (gradi == 0 && !specchio) return;
        var est = Path.GetExtension(percorso).ToLowerInvariant();
        if (est is ".jpg" or ".jpeg" or ".jpe" or ".jfif" or ".tif" or ".tiff")
        {
            // JPEG e TIFF: si cambia solo l'orientamento scritto nel file, senza toccare i pixel
            // il file si legge in memoria: Windows non ne tiene aperto niente, e la sostituzione finale non trova ostacoli
            byte[] riscritto;
            using (var dentro = new Windows.Storage.Streams.InMemoryRandomAccessStream())
            {
                await dentro.WriteAsync((await File.ReadAllBytesAsync(percorso)).AsBuffer());
                dentro.Seek(0);
                var dec = await BitmapDecoder.CreateAsync(dentro);
                var attuale = 1;
                var p = await dec.BitmapProperties.GetPropertiesAsync(["System.Photo.Orientation"]);
                if (p.TryGetValue("System.Photo.Orientation", out var v) && v?.Value is not null) attuale = Convert.ToInt32(v.Value, CultureInfo.InvariantCulture);
                var nuovo = ComponiExif(attuale, gradi, specchio);
                // l'encoder «di trascrizione» ricopia l'immagine com'è e cambia solo i metadati (le proprietà del file sono di sola lettura)
                using var mem = new Windows.Storage.Streams.InMemoryRandomAccessStream();
                var enc = await BitmapEncoder.CreateForTranscodingAsync(mem, dec);
                await enc.BitmapProperties.SetPropertiesAsync(new BitmapPropertySet
                {
                    ["System.Photo.Orientation"] = new BitmapTypedValue((ushort)nuovo, Windows.Foundation.PropertyType.UInt16),
                });
                await enc.FlushAsync();
                riscritto = new byte[mem.Size];
                mem.Seek(0);
                await mem.ReadAsync(riscritto.AsBuffer(), (uint)riscritto.Length, Windows.Storage.Streams.InputStreamOptions.None);
            }
            Sostituisci(percorso, riscritto);
            return;
        }
        // PNG e BMP: i pixel si girano davvero e si riscrivono senza perdite
        var ctx = new Contesto(Strumenti.Trova(), new InfoHardware("", 1, [], []), CancellationToken.None, _ => { });
        try
        {
            var foto = await Immagini.Apri(percorso, ctx);
            var girata = Gira(foto, gradi, specchio);
            var enc = est == ".bmp" ? BitmapEncoder.BmpEncoderId : BitmapEncoder.PngEncoderId;
            var dati = await Immagini.Codifica(girata.BitmapAlphaMode == BitmapAlphaMode.Straight ? girata : SoftwareBitmap.Convert(girata, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight), enc);
            Sostituisci(percorso, dati);
        }
        finally { ctx.Pulisci(); }
    }

    /// <summary>I pixel girati: prima lo specchio (sinistra-destra), poi la rotazione oraria. Su BGRA a 4 byte per pixel.</summary>
    public static SoftwareBitmap Gira(SoftwareBitmap foto, int gradi, bool specchio)
    {
        var bgra = foto.BitmapPixelFormat == BitmapPixelFormat.Bgra8 ? foto : SoftwareBitmap.Convert(foto, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        int w = bgra.PixelWidth, h = bgra.PixelHeight;
        var src = new byte[w * h * 4];
        bgra.CopyToBuffer(src.AsBuffer());
        var (nw, nh) = gradi % 180 == 0 ? (w, h) : (h, w);
        var dst = new byte[src.Length];
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var sx = specchio ? w - 1 - x : x;
                // dove va a finire il pixel (sx, y) ruotando in senso orario
                int dx, dy;
                switch (gradi)
                {
                    case 90: dx = h - 1 - y; dy = sx; break;
                    case 180: dx = w - 1 - sx; dy = h - 1 - y; break;
                    case 270: dx = y; dy = w - 1 - sx; break;
                    default: dx = sx; dy = y; break;
                }
                // il pixel di origine è (x, y) nel file: lo specchio vale sulla colonna
                Buffer.BlockCopy(src, (y * w + x) * 4, dst, (dy * nw + dx) * 4, 4);
            }
        return SoftwareBitmap.CreateCopyFromBuffer(dst.AsBuffer(), BitmapPixelFormat.Bgra8, nw, nh, bgra.BitmapAlphaMode);
    }

    /// <summary>I byte nuovi prendono il posto del file; la data di creazione resta quella di prima.</summary>
    static void Sostituisci(string percorso, byte[] dati)
    {
        var bozza = Nomi.Bozza(percorso);
        var creato = File.GetCreationTime(percorso);
        try
        {
            File.WriteAllBytes(bozza, dati);
            Sposta(bozza, percorso);
            File.SetCreationTime(percorso, creato);
        }
        finally { try { if (File.Exists(bozza)) File.Delete(bozza); } catch { } }
    }

    /// <summary>
    /// La bozza prende il posto del file. Se qualcuno lo tiene ancora aperto (Windows lascia le maniglie delle sue
    /// routine di lettura finché non passa il garbage collector) si fa pulizia e si riprova per qualche secondo.
    /// </summary>
    static void Sposta(string bozza, string percorso)
    {
        for (var i = 0; ; i++)
        {
            try { File.Move(bozza, percorso, true); return; }
            catch (Exception e) when (i < 12 && e is UnauthorizedAccessException or IOException)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Thread.Sleep(150);
            }
        }
    }

    // ————————————————————————— video —————————————————————————

    public static string? SalvaVideo(string percorso) => Path.GetExtension(percorso).ToLowerInvariant() switch
    {
        ".mp4" or ".m4v" or ".mov" => null,
        _ => "Solo MP4 e MOV tengono la rotazione senza ricodificare. Per questo video: converti in MP4 con Ctrl+E.",
    };

    /// <summary>La rotazione già scritta nel video, in gradi antiorari (come la dà FFprobe); 0 se non ce n'è.</summary>
    public static async Task<int> RotazioneVideo(Strumenti s, string percorso)
    {
        var r = await Processi.Esegui(s.Ffprobe, ["-v", "error", "-select_streams", "v:0", "-print_format", "json", "-show_entries", "stream_side_data=rotation", "-i", percorso], CancellationToken.None);
        if (r.Codice != 0) return 0;
        try
        {
            using var d = JsonDocument.Parse(r.Uscita);
            foreach (var st in d.RootElement.GetProperty("streams").EnumerateArray())
                if (st.TryGetProperty("side_data_list", out var l))
                    foreach (var x in l.EnumerateArray())
                        if (x.TryGetProperty("rotation", out var rot)) return (int)Math.Round(rot.GetDouble());
        }
        catch { }
        return 0;
    }

    /// <summary>
    /// Gira il video di <paramref name="gradi"/> (orari) scrivendo la rotazione nel file: si copiano i flussi così
    /// come sono, quindi nessuna perdita e pochi secondi anche per un film grosso.
    /// </summary>
    public static async Task RuotaVideo(Strumenti s, string percorso, int gradi, CancellationToken ct = default)
    {
        if (SalvaVideo(percorso) is { } no) throw new ErroreConversione(no);
        gradi = ((gradi % 360) + 360) % 360;
        if (gradi == 0) return;
        var attuale = await RotazioneVideo(s, percorso);
        // FFprobe dice «gradi antiorari per vederlo giusto»: girare in senso orario li toglie
        var nuovo = (((attuale - gradi) % 360) + 360) % 360;
        if (nuovo > 180) nuovo -= 360;
        var bozza = Nomi.Bozza(percorso);
        try
        {
            var r = await Processi.Esegui(s.Ffmpeg, ["-hide_banner", "-loglevel", "error", "-y",
                "-display_rotation:v:0", nuovo.ToString(CultureInfo.InvariantCulture), "-i", percorso,
                "-map", "0", "-c", "copy", "-map_metadata", "0", "-movflags", "+faststart", "-f", Path.GetExtension(percorso).ToLowerInvariant() == ".mov" ? "mov" : "mp4", bozza], ct);
            if (r.Codice != 0 || !File.Exists(bozza)) throw new ErroreConversione("Non sono riuscito a girare il video.", r.Errori);
            var creato = File.GetCreationTime(percorso);
            Sposta(bozza, percorso);
            File.SetCreationTime(percorso, creato);
        }
        finally { try { if (File.Exists(bozza)) File.Delete(bozza); } catch { } }
    }

    /// <summary>
    /// Taglia il video fra due momenti senza ricodificare (si parte dal fotogramma chiave più vicino, quindi
    /// l'inizio può slittare di un attimo). Il pezzo va accanto, col nome «(tagliato)»; l'originale non si tocca.
    /// </summary>
    public static async Task<string> TagliaVideo(Strumenti s, string percorso, double da, double a, CancellationToken ct = default)
    {
        if (a <= da) throw new ErroreConversione("La fine deve venire dopo l'inizio.");
        var uscita = Nomi.Prenota(percorso, Path.GetExtension(percorso), "tagliato");
        var bozza = Nomi.Bozza(uscita);
        try
        {
            string N(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
            var r = await Processi.Esegui(s.Ffmpeg, ["-hide_banner", "-loglevel", "error", "-y", "-ss", N(da), "-to", N(a), "-i", percorso,
                "-map", "0", "-c", "copy", "-map_metadata", "0", "-map_chapters", "-1", "-avoid_negative_ts", "make_zero", "-f", FormatoFfmpeg(percorso), bozza], ct);
            if (r.Codice != 0 || !File.Exists(bozza)) throw new ErroreConversione("Non sono riuscito a tagliare il video.", r.Errori);
            File.Move(bozza, uscita);
            return uscita;
        }
        catch
        {
            try { if (File.Exists(bozza)) File.Delete(bozza); } catch { }
            Nomi.Libera(uscita);
            throw;
        }
        finally { Nomi.Libera(uscita); }
    }

    static string FormatoFfmpeg(string p) => Path.GetExtension(p).ToLowerInvariant() switch
    {
        ".mp4" or ".m4v" => "mp4", ".mov" => "mov", ".mkv" => "matroska", ".webm" => "webm", ".avi" => "avi", ".ts" or ".m2ts" or ".mts" => "mpegts",
        _ => "matroska",
    };

    // ————————————————————————— PDF —————————————————————————

    /// <summary>Gira una pagina (o tutte, con <paramref name="pagina"/> = -1) del PDF di <paramref name="gradi"/> orari, e salva.</summary>
    public static void RuotaPdf(string percorso, int pagina, int gradi)
    {
        gradi = ((gradi % 360) + 360) % 360;
        if (gradi == 0) return;
        var bozza = Nomi.Bozza(percorso);
        try
        {
            PdfSharp.Pdf.PdfDocument doc;
            try { doc = PdfReader.Open(percorso, PdfDocumentOpenMode.Modify); }
            catch (PdfReaderException) { throw new ErroreConversione("Questo PDF è protetto: non lo posso modificare."); }
            using (doc)
            {
                for (var i = 0; i < doc.PageCount; i++)
                    if (pagina < 0 || i == pagina)
                        doc.Pages[i].Rotate = (doc.Pages[i].Rotate + gradi) % 360;
                doc.Save(bozza);
            }
            var creato = File.GetCreationTime(percorso);
            Sposta(bozza, percorso);
            File.SetCreationTime(percorso, creato);
        }
        finally { try { if (File.Exists(bozza)) File.Delete(bozza); } catch { } }
    }
}
