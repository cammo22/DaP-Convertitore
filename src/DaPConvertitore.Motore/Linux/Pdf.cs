using System.Text;
using PdfSharp.Drawing;
using PdfSharp.Pdf.IO;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using PdfNuovo = PdfSharp.Pdf.PdfDocument;
using PdfTesto = UglyToad.PdfPig.PdfDocument;

namespace DaP.Convertitore;

/// <summary>
/// I PDF su Linux. Il testo e l'unione li fanno PdfPig e PDFsharp (gli stessi di Windows, senza l'OCR
/// e senza il disegno pagine di Windows). Questo file si compila solo per net10.0.
/// </summary>
public static class Pdf
{
    public static Task Converti(Lavoro l, Contesto ctx) => l.Formato.Id switch
    {
        "pdf.txt" => Testo(l, ctx),
        "pdf.unisci" => Unisci(l, ctx),
        "img.pdf" => DaImmagini(l, ctx),
        "pdf.jpg" or "pdf.png" => Pagine(l, ctx),
        "img.txt" => throw new ErroreConversione("Leggere il testo dentro una foto (OCR) per ora c'è solo su Windows."),
        _ => throw new ErroreConversione($"Non so fare {l.Formato.Etichetta} da un PDF."),
    };

    public static Task<int?> Pagine(string percorso)
    {
        try
        {
            using var doc = PdfTesto.Open(percorso);
            return Task.FromResult<int?>(doc.NumberOfPages);
        }
        catch { return Task.FromResult<int?>(null); }
    }

    static async Task Testo(Lavoro l, Contesto ctx)
    {
        ctx.Fase("Leggo il testo");
        var testo = new StringBuilder();
        try
        {
            using var doc = PdfTesto.Open(l.Sorgente);
            foreach (var p in doc.GetPages())
            {
                ctx.Ct.ThrowIfCancellationRequested();
                testo.AppendLine(ContentOrderTextExtractor.GetText(p).Trim());
                testo.AppendLine();
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception e)
        {
            throw new ErroreConversione("Questo PDF non si riesce a leggere. Se è una scansione, l'OCR per ora c'è solo su Windows.", e.Message);
        }
        if (testo.ToString().Count(char.IsLetterOrDigit) < 20)
            throw new ErroreConversione("In questo PDF non trovo testo. Se è una scansione, l'OCR per ora c'è solo su Windows.");
        await File.WriteAllTextAsync(l.Bozza, testo.ToString().TrimEnd() + Environment.NewLine, new UTF8Encoding(true), ctx.Ct);
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

    static Task DaImmagini(Lavoro l, Contesto ctx) => Task.Run(() =>
    {
        var nuovo = new PdfNuovo();
        nuovo.Info.Creator = "DaP Convertitore";
        for (var i = 0; i < l.Sorgenti.Count; i++)
        {
            ctx.Ct.ThrowIfCancellationRequested();
            ctx.Riporta(new Avanzamento(i / (double)l.Sorgenti.Count, $"Impagino {Path.GetFileName(l.Sorgenti[i])}"));
            using var img = XImage.FromFile(l.Sorgenti[i]);
            var pagina = nuovo.AddPage();
            var orizzontale = img.PixelWidth > img.PixelHeight;
            pagina.Width = XUnit.FromMillimeter(orizzontale ? 297 : 210);
            pagina.Height = XUnit.FromMillimeter(orizzontale ? 210 : 297);
            const double margine = 18;
            double lw = pagina.Width.Point - 2 * margine, lh = pagina.Height.Point - 2 * margine;
            var scala = Math.Min(lw / img.PixelWidth, lh / img.PixelHeight);
            double w = img.PixelWidth * scala, h = img.PixelHeight * scala;
            using var g = XGraphics.FromPdfPage(pagina);
            g.DrawImage(img, (pagina.Width.Point - w) / 2, (pagina.Height.Point - h) / 2, w, h);
        }
        nuovo.Save(l.Bozza);
    }, ctx.Ct);

    static async Task Pagine(Lavoro l, Contesto ctx)
    {
        if (ctx.Strumenti.LibreOffice is null)
            throw new ErroreConversione("Per tirare fuori le pagine come immagini serve LibreOffice (è gratis).", "libreoffice");
        var est = l.Formato.Estensione;
        Directory.CreateDirectory(l.Bozza);
        var uno = Path.Combine(ctx.Temporanea(), "pagina" + est);
        await Office.ConLibreOffice(ctx, l.Sorgente, est, uno);
        var nome = "pagina 1" + est;
        File.Move(uno, Path.Combine(l.Bozza, nome), true);
    }
}
