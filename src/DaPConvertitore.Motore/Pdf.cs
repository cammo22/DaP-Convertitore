using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using PdfSharp.Drawing;
using PdfSharp.Pdf.IO;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.Storage.Streams;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using PdfNuovo = PdfSharp.Pdf.PdfDocument;
using PdfWindows = Windows.Data.Pdf.PdfDocument;
using PdfTesto = UglyToad.PdfPig.PdfDocument;

namespace DaP.Convertitore;

/// <summary>
/// I PDF. Le pagine diventano immagini col motore PDF di Windows; il testo si legge con PdfPig e, se il PDF è
/// una scansione, con l'OCR di Windows (nella lingua del PC). Unire PDF e mettere foto in un PDF lo fa PDFsharp.
/// Anche "foto → testo" passa da qui: è lo stesso OCR.
/// </summary>
public static class Pdf
{
    public static Task Converti(Lavoro l, Contesto ctx) => l.Formato.Id switch
    {
        "pdf.jpg" or "pdf.png" => Pagine(l, ctx),
        "pdf.txt" => Testo(l, ctx),
        "pdf.unisci" => Unisci(l, ctx),
        "img.pdf" => DaImmagini(l, ctx),
        "img.txt" => TestoDaImmagine(l, ctx),
        _ => throw new ErroreConversione($"Non so fare {l.Formato.Etichetta} da un PDF."),
    };

    internal static async Task<PdfWindows> Apri(string percorso)
    {
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(percorso));
            return await PdfWindows.LoadFromFileAsync(file);
        }
        catch (Exception e) when (e.HResult == unchecked((int)0x8007052B) || e.Message.Contains("password", StringComparison.OrdinalIgnoreCase))
        {
            throw new ErroreConversione("Questo PDF è protetto da password.");
        }
    }

    /// <summary>Una pagina disegnata ai DPI voluti, sfondo bianco.</summary>
    static async Task<SoftwareBitmap> Disegna(PdfWindows doc, uint indice, int dpi, uint latoMax = 0)
    {
        using var pagina = doc.GetPage(indice);
        var larghezza = pagina.Size.Width * dpi / 96.0;
        var altezza = pagina.Size.Height * dpi / 96.0;
        if (latoMax > 0 && Math.Max(larghezza, altezza) > latoMax)
            larghezza *= latoMax / Math.Max(larghezza, altezza);
        using var flusso = new InMemoryRandomAccessStream();
        await pagina.RenderToStreamAsync(flusso, new Windows.Data.Pdf.PdfPageRenderOptions
        {
            DestinationWidth = (uint)Math.Max(1, Math.Round(larghezza)),
            BackgroundColor = Windows.UI.Color.FromArgb(255, 255, 255, 255),
            BitmapEncoderId = BitmapEncoder.PngEncoderId,
        });
        flusso.Seek(0);
        var dec = await BitmapDecoder.CreateAsync(flusso);
        return await dec.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
    }

    static async Task Pagine(Lavoro l, Contesto ctx)
    {
        var jpg = l.Formato.Id == "pdf.jpg";
        var doc = await Apri(l.Sorgente);
        Directory.CreateDirectory(l.Bozza);
        var cifre = Math.Max(2, doc.PageCount.ToString().Length);
        for (uint i = 0; i < doc.PageCount; i++)
        {
            ctx.Ct.ThrowIfCancellationRequested();
            ctx.Riporta(new Avanzamento(i / (double)doc.PageCount, $"Pagina {i + 1} di {doc.PageCount}"));
            var foto = await Disegna(doc, i, Math.Clamp(l.Opzioni.Pdf.Dpi, 50, 600));
            var dati = jpg
                ? await Immagini.Codifica(foto, BitmapEncoder.JpegEncoderId, Math.Clamp(l.Opzioni.Immagine.Qualita, 1, 100) / 100f)
                : await Immagini.Codifica(foto, BitmapEncoder.PngEncoderId);
            var nome = $"pagina {(i + 1).ToString().PadLeft(cifre, '0')}{(jpg ? ".jpg" : ".png")}";
            await File.WriteAllBytesAsync(Path.Combine(l.Bozza, nome), dati, ctx.Ct);
        }
    }

    static async Task Testo(Lavoro l, Contesto ctx)
    {
        ctx.Fase("Leggo il testo");
        var testo = new StringBuilder();
        int pagine;
        try
        {
            using var doc = PdfTesto.Open(l.Sorgente);
            pagine = doc.NumberOfPages;
            foreach (var p in doc.GetPages())
            {
                ctx.Ct.ThrowIfCancellationRequested();
                testo.AppendLine(ContentOrderTextExtractor.GetText(p).Trim());
                testo.AppendLine();
            }
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            Registro.Scrivi($"PdfPig: {e.Message}");
            pagine = 1;
            testo.Clear();
        }

        // poche lettere per pagina = è una scansione: si legge con l'OCR
        var lettere = testo.ToString().Count(char.IsLetterOrDigit);
        if (lettere < 40 * Math.Max(1, pagine))
        {
            var doc = await Apri(l.Sorgente);
            var ocr = Ocr();
            testo.Clear();
            for (uint i = 0; i < doc.PageCount; i++)
            {
                ctx.Ct.ThrowIfCancellationRequested();
                ctx.Riporta(new Avanzamento(i / (double)doc.PageCount, $"Leggo la scansione (OCR) · pagina {i + 1} di {doc.PageCount}"));
                var foto = await Disegna(doc, i, 300, OcrEngine.MaxImageDimension);
                testo.AppendLine(await Leggi(ocr, foto));
                testo.AppendLine();
            }
        }
        await File.WriteAllTextAsync(l.Bozza, testo.ToString().TrimEnd() + Environment.NewLine, new UTF8Encoding(true), ctx.Ct);
    }

    static async Task TestoDaImmagine(Lavoro l, Contesto ctx)
    {
        ctx.Fase("Leggo il testo nella foto (OCR)");
        var foto = await Immagini.Apri(l.Sorgente, ctx, OcrEngine.MaxImageDimension);
        var testo = await Leggi(Ocr(), foto);
        if (string.IsNullOrWhiteSpace(testo)) throw new ErroreConversione("In questa immagine non trovo testo.");
        await File.WriteAllTextAsync(l.Bozza, testo + Environment.NewLine, new UTF8Encoding(true), ctx.Ct);
    }

    static OcrEngine Ocr() =>
        OcrEngine.TryCreateFromUserProfileLanguages()
        ?? OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language("en-US"))
        ?? throw new ErroreConversione("Per l'OCR Windows vuole una lingua col riconoscimento del testo: Impostazioni → Ora e lingua → Lingua.");

    static async Task<string> Leggi(OcrEngine ocr, SoftwareBitmap foto)
    {
        var r = await ocr.RecognizeAsync(foto);
        return string.Join(Environment.NewLine, r.Lines.Select(riga => riga.Text));
    }

    static Task Unisci(Lavoro l, Contesto ctx) => Task.Run(() =>
    {
        var nuovo = new PdfNuovo();
        for (var i = 0; i < l.Sorgenti.Count; i++)
        {
            ctx.Ct.ThrowIfCancellationRequested();
            ctx.Riporta(new Avanzamento(i / (double)l.Sorgenti.Count, $"Unisco {Path.GetFileName(l.Sorgenti[i])}"));
            try
            {
                using var dentro = PdfReader.Open(l.Sorgenti[i], PdfDocumentOpenMode.Import);
                foreach (var pagina in dentro.Pages) nuovo.AddPage(pagina);
            }
            catch (PdfReaderException e)
            {
                throw new ErroreConversione($"{Path.GetFileName(l.Sorgenti[i])} è protetto o rovinato: non si può unire.", e.Message);
            }
        }
        nuovo.Save(l.Bozza);
    }, ctx.Ct);

    /// <summary>Le foto in un PDF: una per pagina A4, girata come la foto, centrata coi margini.</summary>
    static async Task DaImmagini(Lavoro l, Contesto ctx)
    {
        var nuovo = new PdfNuovo();
        nuovo.Info.Creator = "DaP Convertitore";
        for (var i = 0; i < l.Sorgenti.Count; i++)
        {
            ctx.Ct.ThrowIfCancellationRequested();
            ctx.Riporta(new Avanzamento(i / (double)l.Sorgenti.Count, $"Impagino {Path.GetFileName(l.Sorgenti[i])}"));
            var foto = await Immagini.Apri(l.Sorgenti[i], ctx, 3000);
            var jpeg = await Immagini.Codifica(Immagini.SuBianco(foto), BitmapEncoder.JpegEncoderId, 0.9f);
            using var ms = new MemoryStream(jpeg);
            using var img = XImage.FromStream(ms);
            var pagina = nuovo.AddPage();
            var orizzontale = foto.PixelWidth > foto.PixelHeight;
            pagina.Width = XUnit.FromMillimeter(orizzontale ? 297 : 210);
            pagina.Height = XUnit.FromMillimeter(orizzontale ? 210 : 297);
            const double margine = 18;
            double lw = pagina.Width.Point - 2 * margine, lh = pagina.Height.Point - 2 * margine;
            var scala = Math.Min(lw / foto.PixelWidth, lh / foto.PixelHeight);
            double w = foto.PixelWidth * scala, h = foto.PixelHeight * scala;
            using var g = XGraphics.FromPdfPage(pagina);
            g.DrawImage(img, (pagina.Width.Point - w) / 2, (pagina.Height.Point - h) / 2, w, h);
        }
        nuovo.Save(l.Bozza);
    }

    public static async Task<int?> Pagine(string percorso)
    {
        try { return (int)(await Apri(percorso)).PageCount; }
        catch { return null; }
    }

    /// <summary>La prima pagina in piccolo, per l'anteprima.</summary>
    public static async Task<SoftwareBitmap?> Copertina(string percorso, uint lato)
    {
        try
        {
            var doc = await Apri(percorso);
            return doc.PageCount > 0 ? await Disegna(doc, 0, 96, lato) : null;
        }
        catch { return null; }
    }
}
