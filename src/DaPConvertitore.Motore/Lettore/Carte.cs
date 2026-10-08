using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Xml.Linq;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using PdfWindows = Windows.Data.Pdf.PdfDocument;

namespace DaP.Convertitore.Lettore;

/// <summary>
/// Le carte nel lettore: i PDF li disegna Windows pagina per pagina, alla larghezza che serve allo schermo;
/// Word si legge da sé (il DOCX è un pacco di XML: paragrafi, titoli, grassetti, tabelle e foto) e quando c'è
/// Office o LibreOffice si può avere l'impaginato vero, in PDF nella cache.
/// </summary>
public static class Carte
{
    // il documento aperto resta in memoria: scorrendo le pagine non si rilegge il file
    static readonly Dictionary<string, Task<PdfWindows>> aperti = new(StringComparer.OrdinalIgnoreCase);

    static Task<PdfWindows> Documento(string percorso)
    {
        lock (aperti)
        {
            if (!aperti.TryGetValue(percorso, out var t) || t.IsFaulted)
            {
                if (aperti.Count > 8) aperti.Clear();
                aperti[percorso] = t = InMemoria(percorso);
            }
            return t;
        }
    }

    /// <summary>
    /// Il PDF si legge tutto in memoria: aperto dal file, Windows lo terrebbe bloccato e non si potrebbe né
    /// spostare né mettere nel Cestino mentre lo guardi. Quelli enormi (oltre 300 MB) si aprono dal file.
    /// </summary>
    static async Task<PdfWindows> InMemoria(string percorso)
    {
        if (new FileInfo(percorso).Length > 300L * 1024 * 1024) return await Pdf.Apri(percorso);
        var b = await File.ReadAllBytesAsync(percorso);
        var m = new InMemoryRandomAccessStream();
        await m.WriteAsync(b.AsBuffer());
        m.Seek(0);
        try { return await PdfWindows.LoadFromStreamAsync(m); }
        catch (Exception e) when (e.HResult == unchecked((int)0x8007052B) || e.Message.Contains("password", StringComparison.OrdinalIgnoreCase))
        {
            throw new ErroreConversione("Questo PDF è protetto da password.");
        }
    }

    /// <summary>Il file sta per essere spostato o cancellato: il lettore lo lascia andare.</summary>
    public static void Dimentica(string percorso)
    {
        lock (aperti) aperti.Remove(percorso);
    }

    /// <summary>Le misure di ogni pagina (in punti a 96 dpi): la pagina prepara i fogli prima di disegnarli.</summary>
    public static async Task<List<(double w, double h)>> Pagine(string percorso)
    {
        var doc = await Documento(percorso);
        var l = new List<(double, double)>();
        for (uint i = 0; i < doc.PageCount; i++)
        {
            using var p = doc.GetPage(i);
            l.Add((p.Size.Width, p.Size.Height));
        }
        return l;
    }

    /// <summary>Una pagina in JPEG, larga <paramref name="larghezza"/> pixel (la pagina chiede quelli veri dello schermo).</summary>
    public static async Task<byte[]> Pagina(string percorso, uint indice, uint larghezza)
    {
        var doc = await Documento(percorso);
        if (indice >= doc.PageCount) throw new ErroreConversione("Pagina che non c'è.");
        using var pagina = doc.GetPage(indice);
        using var flusso = new InMemoryRandomAccessStream();
        await pagina.RenderToStreamAsync(flusso, new Windows.Data.Pdf.PdfPageRenderOptions
        {
            DestinationWidth = Math.Clamp(larghezza, 64, 4000),
            BackgroundColor = Windows.UI.Color.FromArgb(255, 255, 255, 255),
            BitmapEncoderId = BitmapEncoder.JpegEncoderId,
        });
        var b = new byte[flusso.Size];
        flusso.Seek(0);
        await flusso.ReadAsync(b.AsBuffer(), (uint)b.Length, InputStreamOptions.None);
        return b;
    }

    /// <summary>
    /// L'impaginato vero (Word, LibreOffice): il documento diventa un PDF nella cache del lettore. Ci vuole qualche
    /// secondo la prima volta, poi è subito lì.
    /// </summary>
    public static async Task<string> InPdf(Strumenti s, string percorso)
    {
        var uscita = Media.InCache(percorso, ".pdf");
        if (File.Exists(uscita)) return uscita;
        var cat = Catalogo.CategoriaDi(percorso);
        var f = Catalogo.Formati.First(x => x.Categoria == cat && x.Estensione == ".pdf");
        var l = new Lavoro { Id = "lettore", Sorgenti = [percorso], Formato = f, Opzioni = new Opzioni() };
        l.Bozza = uscita + ".tmp.pdf";
        var ctx = new Contesto(s, new InfoHardware("", 1, [], []), CancellationToken.None, _ => { });
        try
        {
            await Office.Converti(l, ctx);
            if (!File.Exists(l.Bozza)) throw new ErroreConversione("Non è uscito niente.");
            File.Move(l.Bozza, uscita, true);
            return uscita;
        }
        finally { ctx.Pulisci(); try { File.Delete(l.Bozza); } catch { } }
    }

    // ————————————————————————— Word, letto da noi —————————————————————————

    static readonly XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    static readonly XNamespace r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    static readonly XNamespace a = "http://schemas.openxmlformats.org/drawingml/2006/main";
    static readonly XNamespace pr = "http://schemas.openxmlformats.org/package/2006/relationships";

    /// <summary>Il DOCX in HTML semplice: titoli, paragrafi, grassetti, corsivi, elenchi, tabelle e foto.</summary>
    public static string Docx(string percorso)
    {
        using var zip = ZipFile.OpenRead(percorso);
        var doc = XDocument.Load(zip.GetEntry("word/document.xml")?.Open() ?? throw new ErroreConversione("Questo DOCX è vuoto o rovinato."));
        var rel = new Dictionary<string, string>();
        if (zip.GetEntry("word/_rels/document.xml.rels") is { } re)
            foreach (var x in XDocument.Load(re.Open()).Root!.Elements(pr + "Relationship"))
                rel[(string)x.Attribute("Id")!] = (string)x.Attribute("Target")!;
        var corpo = doc.Root!.Element(w + "body");
        if (corpo is null) return "";
        var sb = new StringBuilder();
        var elenco = false;
        foreach (var el in corpo.Elements())
        {
            if (el.Name == w + "p")
            {
                var stile = (string?)el.Element(w + "pPr")?.Element(w + "pStyle")?.Attribute(w + "val") ?? "";
                var puntato = el.Element(w + "pPr")?.Element(w + "numPr") is not null;
                if (puntato && !elenco) { sb.Append("<ul>"); elenco = true; }
                if (!puntato && elenco) { sb.Append("</ul>"); elenco = false; }
                var dentro = Corse(el, zip, rel);
                var tag = puntato ? "li" : Titolo(stile);
                if (dentro.Length == 0 && tag == "p") { sb.Append("<p class=\"vuoto\"></p>"); continue; }
                var allinea = (string?)el.Element(w + "pPr")?.Element(w + "jc")?.Attribute(w + "val");
                var classe = allinea is "center" ? " class=\"centro\"" : allinea is "right" or "end" ? " class=\"destra\"" : allinea is "both" ? " class=\"giusto\"" : "";
                sb.Append($"<{tag}{classe}>{dentro}</{tag}>");
            }
            else if (el.Name == w + "tbl")
            {
                if (elenco) { sb.Append("</ul>"); elenco = false; }
                sb.Append("<table>");
                foreach (var riga in el.Elements(w + "tr"))
                {
                    sb.Append("<tr>");
                    foreach (var cella in riga.Elements(w + "tc"))
                        sb.Append("<td>").Append(string.Join("<br>", cella.Elements(w + "p").Select(p => Corse(p, zip, rel)))).Append("</td>");
                    sb.Append("</tr>");
                }
                sb.Append("</table>");
            }
        }
        if (elenco) sb.Append("</ul>");
        return sb.ToString();
    }

    static string Titolo(string stile)
    {
        var s = stile.ToLowerInvariant();
        if (s is "title" or "titolo") return "h1";
        if (s.StartsWith("heading") || s.StartsWith("titolo"))
        {
            var n = s.LastOrDefault(char.IsDigit);
            return n is >= '1' and <= '5' ? $"h{(char)(n + 1)}" : "h2";
        }
        if (s is "subtitle" or "sottotitolo") return "h3";
        if (s.Contains("quote") || s.Contains("citaz")) return "blockquote";
        return "p";
    }

    static string Corse(XElement p, ZipArchive zip, Dictionary<string, string> rel)
    {
        var sb = new StringBuilder();
        foreach (var run in p.Descendants().Where(e => e.Name == w + "r" || e.Name == w + "hyperlink"))
        {
            if (run.Name == w + "hyperlink") continue;
            var rp = run.Element(w + "rPr");
            bool b = rp?.Element(w + "b") is { } bb && (string?)bb.Attribute(w + "val") is null or "1" or "true";
            bool i = rp?.Element(w + "i") is { } ii && (string?)ii.Attribute(w + "val") is null or "1" or "true";
            bool u = rp?.Element(w + "u") is { } uu && (string?)uu.Attribute(w + "val") is not (null or "none");
            var testo = new StringBuilder();
            foreach (var x in run.Elements())
            {
                if (x.Name == w + "t") testo.Append(WebUtility.HtmlEncode(x.Value));
                else if (x.Name == w + "tab") testo.Append("&emsp;");
                else if (x.Name == w + "br") testo.Append("<br>");
                else if (x.Name == w + "drawing")
                {
                    var id = (string?)x.Descendants(a + "blip").FirstOrDefault()?.Attribute(r + "embed");
                    if (id is not null && rel.TryGetValue(id, out var dove) && zip.GetEntry("word/" + dove.TrimStart('/')) is { } img && img.Length < 15_000_000)
                    {
                        using var m = new MemoryStream();
                        img.Open().CopyTo(m);
                        var tipo = Path.GetExtension(dove).ToLowerInvariant() switch { ".png" => "image/png", ".gif" => "image/gif", ".svg" => "image/svg+xml", _ => "image/jpeg" };
                        testo.Append($"<img src=\"data:{tipo};base64,{Convert.ToBase64String(m.ToArray())}\">");
                    }
                }
            }
            if (testo.Length == 0) continue;
            var t = testo.ToString();
            if (b) t = $"<b>{t}</b>";
            if (i) t = $"<i>{t}</i>";
            if (u) t = $"<u>{t}</u>";
            sb.Append(t);
        }
        return sb.ToString();
    }

    /// <summary>Il PPTX senza PowerPoint: per ogni diapositiva il titolo e i punti.</summary>
    public static List<(string Titolo, List<string> Punti)> Pptx(string percorso)
    {
        using var zip = ZipFile.OpenRead(percorso);
        var diapo = zip.Entries
            .Where(e => e.FullName.StartsWith("ppt/slides/slide") && e.FullName.EndsWith(".xml"))
            .OrderBy(e => int.TryParse(new string(e.Name.Where(char.IsDigit).ToArray()), out var n) ? n : 0)
            .ToList();
        var l = new List<(string, List<string>)>();
        XNamespace p = "http://schemas.openxmlformats.org/presentationml/2006/main";
        foreach (var e in diapo)
        {
            var x = XDocument.Load(e.Open());
            string titolo = "";
            var punti = new List<string>();
            foreach (var sp in x.Descendants(p + "sp"))
            {
                var tipo = (string?)sp.Descendants(p + "ph").FirstOrDefault()?.Attribute("type");
                var paragrafi = sp.Descendants(a + "p").Select(par => string.Concat(par.Descendants(a + "t").Select(t => t.Value)).Trim()).Where(t => t.Length > 0).ToList();
                if (tipo is "title" or "ctrTitle" && titolo.Length == 0) titolo = string.Join(" ", paragrafi);
                else punti.AddRange(paragrafi);
            }
            l.Add((titolo, punti));
        }
        return l;
    }
}
