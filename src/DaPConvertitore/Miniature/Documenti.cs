using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using Windows.Graphics.Imaging;

namespace DaP.Convertitore.App.Miniature;

/// <summary>
/// Le miniature dei documenti: la prima pagina del PDF, la prima pagina di Word/Excel/PowerPoint (l'anteprima che
/// c'è dentro il file, o una pagina disegnata col suo testo vero), il testo e il codice colorato, le tabelle,
/// i caratteri («Aa» nel carattere stesso), il contenuto degli archivi.
/// </summary>
public static class Documenti
{
    // ————————————————————————————————————— PDF —————————————————————————————————————

    public static Quadro? Pdf(string percorso, int L)
    {
        var png = Multimedia.Aspetta(async () =>
        {
            var pagina = await DaP.Convertitore.Pdf.Copertina(percorso, (uint)L);
            if (pagina is null) return null;
            if (pagina.BitmapAlphaMode != BitmapAlphaMode.Straight) pagina = SoftwareBitmap.Convert(pagina, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight);
            return await Immagini.Codifica(pagina, Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId);
        });
        var img = Tela.Immagine(png);
        return img is null ? null : Multimedia.Incornicia(img, L, "PDF", Tavolozza.Tinta("Pdf"));
    }

    // ————————————————————————————————————— WORD, EXCEL, POWERPOINT —————————————————————————————————————

    public static Quadro? Ufficio(string percorso, int L, string gruppo)
    {
        var est = Path.GetExtension(percorso).TrimStart('.').ToUpperInvariant();
        var tinta = Tavolozza.Tinta(gruppo);
        ZipArchive? zip = null;
        try
        {
            try { zip = ZipFile.OpenRead(percorso); } catch { zip = null; }
            // 1. l'anteprima che il programma ha messo dentro al file
            if (zip is not null)
                foreach (var nome in new[] { "docProps/thumbnail.jpeg", "docProps/thumbnail.jpg", "docProps/thumbnail.png", "Thumbnails/thumbnail.png" })
                {
                    var voce = zip.GetEntry(nome);
                    if (voce is null || voce.Length > 6_000_000) continue;
                    using var s = voce.Open();
                    using var m = new MemoryStream();
                    s.CopyTo(m);
                    if (Tela.Immagine(m.ToArray()) is { } img) return Multimedia.Incornicia(img, L, est, tinta);
                }

            // 2. si disegna la pagina, col testo vero se si riesce a leggerlo
            return gruppo switch
            {
                "Presentazione" => Diapositiva(zip, L, est, tinta),
                "Tabella" => Foglio(percorso, L, est, tinta),
                _ => Pagina(zip, percorso, L, est, tinta),
            };
        }
        finally { zip?.Dispose(); }
    }

    /// <summary>I paragrafi di un file XML dentro lo zip (Word: w:p/w:t, LibreOffice: text:p), i primi che ci sono.</summary>
    static List<string> Paragrafi(ZipArchive? zip, string voce, string nomeParagrafo, string? nomeTesto, int massimo)
    {
        var elenco = new List<string>();
        try
        {
            var e = zip?.GetEntry(voce);
            if (e is null || e.Length > 12_000_000) return elenco;
            using var s = e.Open();
            var doc = XDocument.Load(s);
            foreach (var p in doc.Descendants().Where(x => x.Name.LocalName == nomeParagrafo))
            {
                var testo = nomeTesto is null
                    ? p.Value
                    : string.Concat(p.Descendants().Where(x => x.Name.LocalName == nomeTesto).Select(x => x.Value));
                testo = Regex.Replace(testo, @"\s+", " ").Trim();
                if (testo.Length > 0) elenco.Add(testo);
                if (elenco.Count >= massimo) break;
            }
        }
        catch { /* se non si legge, si disegnano le righe finte */ }
        return elenco;
    }

    static Quadro Pagina(ZipArchive? zip, string percorso, int L, string est, Color tinta)
    {
        var righe = Paragrafi(zip, "word/document.xml", "p", "t", 30);
        if (righe.Count == 0) righe = Paragrafi(zip, "content.xml", "p", null, 30);
        int W = (int)Math.Round(L * 0.74), H = L;
        return Tela.Fai(W, H, dc =>
        {
            var r = new Rect(0, 0, W, H);
            var raggio = W * 0.04;
            dc.DrawRoundedRectangle(Tavolozza.P(Tavolozza.C("#fbfaff")), null, r, raggio, raggio);
            Tela.Ritaglia(dc, r, raggio);
            var banda = H * 0.12;
            dc.DrawRectangle(Tavolozza.P(tinta), null, new Rect(0, 0, W, banda));
            var margine = W * 0.09;
            if (L >= 96) dc.DrawText(Scritte.Testo(est, Scritte.Titolo, banda * 0.52, Tavolozza.P(Colors.White)), new Point(margine, banda * 0.24));

            var scuro = Tavolozza.P(Tavolozza.C("#2a2540"));
            var fs = Math.Max(5, W * 0.058);
            var y = banda + H * 0.06;
            if (righe.Count > 0)
            {
                var titolo = Scritte.Testo(righe[0], Scritte.Titolo, fs * 1.45, Tavolozza.P(Tavolozza.C("#15102a")), W - margine * 2);
                titolo.MaxLineCount = 2;
                dc.DrawText(titolo, new Point(margine, y));
                y += titolo.Height + fs * 0.7;
                var corpo = Scritte.Testo(string.Join("\n", righe.Skip(1)), Scritte.Corpo, fs, scuro, W - margine * 2);
                corpo.MaxLineCount = 0;
                corpo.MaxTextWidth = W - margine * 2;
                corpo.MaxTextHeight = Math.Max(fs, H - y - margine * 0.5);
                corpo.Trimming = TextTrimming.CharacterEllipsis;
                dc.DrawText(corpo, new Point(margine, y));
            }
            else RigheFinte(dc, margine, y, W - margine * 2, H - y - margine, fs * 0.5, Tavolozza.C("#d9d4ea"));
            dc.Pop();
            dc.DrawRoundedRectangle(null, Tavolozza.Penna(Colors.Black, 1, 0.2), new Rect(0.5, 0.5, W - 1, H - 1), raggio, raggio);
        });
    }

    static void RigheFinte(DrawingContext dc, double x, double y, double larghezza, double altezza, double spessore, Color colore)
    {
        var pennello = Tavolozza.P(colore);
        var passo = spessore * 2.2;
        var corte = new[] { 0.55, 1, 0.94, 1, 0.82, 1, 0.97, 0.4, 1, 0.9, 1, 0.7 };
        for (var i = 0; y + i * passo + spessore < y + altezza; i++)
            dc.DrawRoundedRectangle(pennello, null, new Rect(x, y + i * passo, larghezza * corte[i % corte.Length], spessore), spessore / 2, spessore / 2);
    }

    static Quadro Diapositiva(ZipArchive? zip, int L, string est, Color tinta)
    {
        var righe = Paragrafi(zip, "ppt/slides/slide1.xml", "p", "t", 6);
        if (righe.Count == 0) righe = Paragrafi(zip, "content.xml", "p", null, 6);
        int W = L, H = (int)Math.Round(L * 0.5625);
        return Tela.Fai(W, H, dc =>
        {
            var r = new Rect(0, 0, W, H);
            var raggio = H * 0.06;
            var fondo = new LinearGradientBrush(Tavolozza.ViolaChiaro, Tavolozza.Fondo, 120);
            fondo.Freeze();
            dc.DrawRoundedRectangle(fondo, null, r, raggio, raggio);
            Tela.Ritaglia(dc, r, raggio);
            dc.DrawRectangle(Tavolozza.P(tinta), null, new Rect(0, 0, W * 0.025, H));
            var m = W * 0.1;
            if (righe.Count > 0)
            {
                var t = Scritte.Testo(righe[0], Scritte.Titolo, H * 0.15, Tavolozza.P(Colors.White), W - m * 1.6);
                t.MaxLineCount = 3;
                dc.DrawText(t, new Point(m, H * 0.2));
                if (righe.Count > 1)
                {
                    var sotto = Scritte.Testo(string.Join("\n", righe.Skip(1)), Scritte.Corpo, H * 0.085, Tavolozza.P(Colors.White, 0.7), W - m * 1.6);
                    sotto.MaxLineCount = 0;
                    sotto.MaxTextHeight = H * 0.32;
                    sotto.Trimming = TextTrimming.CharacterEllipsis;
                    dc.DrawText(sotto, new Point(m, H * 0.2 + Math.Min(t.Height, H * 0.45) + H * 0.06));
                }
            }
            else
            {
                RigheFinte(dc, m, H * 0.22, W * 0.55, H * 0.1, H * 0.07, Color.FromArgb(200, 255, 255, 255));
                RigheFinte(dc, m, H * 0.42, W * 0.7, H * 0.4, H * 0.045, Color.FromArgb(90, 255, 255, 255));
            }
            if (L >= 96) Tela.Pastiglia(dc, est, W - W * 0.035, H - H * 0.19, H * 0.12, Tavolozza.P(tinta), Tavolozza.P(Tavolozza.Fondo), aDestra: true);
            dc.Pop();
            dc.DrawRoundedRectangle(null, Tavolozza.Penna(Colors.White, 1, 0.14), new Rect(0.5, 0.5, W - 1, H - 1), raggio, raggio);
        });
    }

    static Quadro Foglio(string percorso, int L, string est, Color tinta)
    {
        string[][]? celle = null;
        if (est is "XLSX" or "XLSM")
        {
            try
            {
                celle = MiniExcelLibs.MiniExcel.Query(percorso, useHeaderRow: false).Take(9)
                    .Select(riga => ((IDictionary<string, object>)riga).Values.Take(5).Select(v => v?.ToString() ?? "").ToArray()).ToArray();
            }
            catch { celle = null; }
        }
        return Griglia(L, est, tinta, celle);
    }

    /// <summary>Un foglio di calcolo: la riga di intestazione colorata e le celle (con i valori veri se li abbiamo).</summary>
    static Quadro Griglia(int L, string est, Color tinta, string[][]? celle)
    {
        int W = L, H = (int)Math.Round(L * 0.78);
        return Tela.Fai(W, H, dc =>
        {
            var r = new Rect(0, 0, W, H);
            var raggio = H * 0.05;
            dc.DrawRoundedRectangle(Tavolozza.P(Tavolozza.C("#fbfaff")), null, r, raggio, raggio);
            Tela.Ritaglia(dc, r, raggio);
            int colonne = Math.Clamp(celle?.Max(x => x.Length) ?? 4, 3, 5), righe = 8;
            double ci = W * 0.07, cw = (W - ci) / colonne, rh = (H - 0.0) / (righe + 0.25);
            var linea = Tavolozza.Penna(Tavolozza.C("#d3cde6"), Math.Max(1, L / 256.0));
            dc.DrawRectangle(Tavolozza.P(tinta), null, new Rect(0, 0, W, rh));
            dc.DrawRectangle(Tavolozza.P(Tavolozza.C("#efecf8")), null, new Rect(0, 0, ci, H));
            for (var i = 1; i <= righe; i++) dc.DrawLine(linea, new Point(0, i * rh), new Point(W, i * rh));
            for (var j = 0; j <= colonne; j++) dc.DrawLine(linea, new Point(ci + j * cw, 0), new Point(ci + j * cw, H));
            var fs = Math.Max(5, rh * 0.5);
            var nero = Tavolozza.P(Tavolozza.C("#1d1a2e"));
            for (var i = 0; i < righe; i++)
            {
                for (var j = 0; j < colonne; j++)
                {
                    var testo = celle is not null && i < celle.Length && j < celle[i].Length ? celle[i][j] : null;
                    var x = ci + j * cw + cw * 0.08;
                    if (testo is { Length: > 0 })
                        dc.DrawText(Scritte.Testo(testo, i == 0 ? Scritte.Titolo : Scritte.Corpo, fs, i == 0 ? Tavolozza.P(Colors.White) : nero, cw * 0.84),
                            new Point(x, (i == 0 ? 0 : i * rh + 0) + (rh - fs * 1.2) / 2 + (i == 0 ? 0 : rh * 0.0)));
                    else if (celle is null && i > 0 && (i * 7 + j * 3) % 5 != 0)
                        dc.DrawRoundedRectangle(Tavolozza.P(Tavolozza.C("#d9d4ea")), null, new Rect(x, i * rh + rh * 0.3, cw * (0.35 + ((i * 5 + j * 3) % 5) * 0.1), rh * 0.34), rh * 0.17, rh * 0.17);
                }
            }
            if (L >= 96) Tela.Pastiglia(dc, est, W - W * 0.03, H - rh * 0.9, rh * 0.62, Tavolozza.P(tinta), Tavolozza.P(Colors.White), aDestra: true);
            dc.Pop();
            dc.DrawRoundedRectangle(null, Tavolozza.Penna(Colors.Black, 1, 0.2), new Rect(0.5, 0.5, W - 1, H - 1), raggio, raggio);
        });
    }

    // ————————————————————————————————————— TESTO, CODICE, CSV, SOTTOTITOLI —————————————————————————————————————

    static string? Leggi(string percorso, int byteMax = 24_000)
    {
        try
        {
            using var f = File.OpenRead(percorso);
            var b = new byte[byteMax];
            var n = f.Read(b, 0, b.Length);
            if (n == 0) return "";
            if (b.AsSpan(0, n).IndexOf((byte)0) >= 0) return null; // binario
            var inizio = n >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF ? 3 : 0;
            try { return new UTF8Encoding(false, true).GetString(b, inizio, n - inizio); }
            catch
            {
                // l'ultimo carattere può essere tagliato a metà: si prova senza gli ultimi byte, poi come testo di Windows
                for (var taglio = 1; taglio < 4 && n - taglio > inizio; taglio++)
                    try { return new UTF8Encoding(false, true).GetString(b, inizio, n - inizio - taglio); } catch { }
                return Encoding.GetEncoding(1252).GetString(b, inizio, n - inizio);
            }
        }
        catch { return null; }
    }

    static readonly HashSet<string> parole = new(StringComparer.Ordinal)
    {
        "function","return","if","else","for","while","do","switch","case","break","continue","class","struct","interface","enum","public","private","protected",
        "static","const","let","var","new","this","import","export","from","using","namespace","package","def","self","async","await","try","catch","finally",
        "throw","void","int","string","bool","true","false","null","None","True","False","in","is","not","and","or","fn","pub","mod","use","impl","type","go",
        "func","end","then","elif","lambda","yield","extends","implements","override","readonly","final","abstract","typeof","delete","select","where","table",
        "SELECT","FROM","WHERE","INSERT","UPDATE","DELETE","CREATE","TABLE","JOIN","ORDER","GROUP","BY","INTO","VALUES","AND","OR","NOT","NULL",
    };

    static readonly Regex lessico = new(@"(?<c>//.*|/\*.*?(\*/|$)|<!--.*?(-->|$))|(?<s>""(?:[^""\\]|\\.)*(""|$)|'(?:[^'\\]|\\.)*('|$))|(?<n>\b\d[\d_.]*\b|\b0x[0-9a-fA-F]+\b)|(?<k>\b[A-Za-z_][A-Za-z0-9_]*\b)|(?<a>[<>/=]+)", RegexOptions.Compiled);
    static readonly Regex tempo = new(@"^\s*\d{1,2}:\d{2}:\d{2}[.,]\d+\s*-->", RegexOptions.Compiled);

    public static Quadro? Testo(string percorso, int L, string gruppo, string tipo)
    {
        var testo = Leggi(percorso);
        if (testo is null) return null;
        var est = Path.GetExtension(percorso).TrimStart('.').ToLowerInvariant();
        if (tipo == "tabella" && est is "csv" or "tsv") return Csv(testo, L, est);
        var tinta = Tavolozza.Tinta(gruppo);
        var righe = testo.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        int W = L, H = (int)Math.Round(L * 0.8);
        var commentoDiesis = est is "py" or "sh" or "bash" or "yml" or "yaml" or "toml" or "ini" or "cfg" or "conf" or "rb" or "r" or "pl" or "env" or "gitignore" or "dockerfile" or "makefile";
        var colorato = tipo is "codice" or "json" or "web" or "markdown";

        return Tela.Fai(W, H, dc =>
        {
            var r = new Rect(0, 0, W, H);
            var raggio = H * 0.06;
            dc.DrawRoundedRectangle(Tavolozza.P(Tavolozza.C("#15101f")), null, r, raggio, raggio);
            Tela.Ritaglia(dc, r, raggio);
            var barra = H * 0.1;
            dc.DrawRectangle(Tavolozza.P(Tavolozza.C("#201833")), null, new Rect(0, 0, W, barra));
            for (var i = 0; i < 3; i++)
                dc.DrawEllipse(Tavolozza.P(i == 0 ? Tavolozza.Magenta : i == 1 ? Tavolozza.Oro : Tavolozza.Ciano, 0.85), null, new Point(barra * (0.7 + i * 0.62), barra / 2), barra * 0.17, barra * 0.17);
            if (L >= 96) Tela.Pastiglia(dc, est.ToUpperInvariant(), W - W * 0.03, barra * 0.16, barra * 0.68, Tavolozza.P(tinta, 0.95), Tavolozza.P(Tavolozza.Fondo), aDestra: true);

            var fs = Math.Max(5, W * 0.047);
            var riga = fs * 1.42;
            var colonna = Scritte.Testo("M", Scritte.Codice, fs, Brushes.White).WidthIncludingTrailingWhitespace;
            var gutter = colorato ? colonna * 3 : 0;
            var x0 = W * 0.04 + gutter;
            var y0 = barra + H * 0.035;
            var chiaro = Tavolozza.P(Tavolozza.C("#e9e4f7"));
            var grigio = Tavolozza.P(Tavolozza.C("#6c6585"));
            var verde = Tavolozza.P(Tavolozza.C("#7bd88f"));
            var oro = Tavolozza.P(Tavolozza.Oro);
            var mag = Tavolozza.P(Tavolozza.C("#ff6fe8"));
            var cia = Tavolozza.P(Tavolozza.Ciano);
            var righeVisibili = (int)((H - y0 - H * 0.02) / riga);
            var colonneMax = (int)((W - x0) / colonna) + 1;
            var n = 0;
            if (tipo == "testo")
            {
                // il testo semplice va a capo come in un blocco note
                var tutto = Scritte.Testo(string.Join("\n", righe.Take(righeVisibili * 3)).Replace("\t", "    "), Scritte.Codice, fs, chiaro);
                tutto.MaxTextWidth = W - x0 - W * 0.04;
                tutto.MaxTextHeight = H - y0 - H * 0.03;
                tutto.Trimming = TextTrimming.CharacterEllipsis;
                dc.DrawText(tutto, new Point(x0, y0));
                righeVisibili = 0;
            }
            foreach (var cruda in righe)
            {
                if (n >= righeVisibili) break;
                var s = cruda.Replace("\t", "  ");
                if (s.Length > colonneMax) s = s[..colonneMax];
                var y = y0 + n * riga;
                if (colorato)
                    dc.DrawText(Scritte.Testo((n + 1).ToString(), Scritte.Codice, fs, Tavolozza.P(Tavolozza.C("#4b4468"))), new Point(W * 0.04, y));
                if (!colorato && tempo.IsMatch(s)) dc.DrawText(Scritte.Testo(s, Scritte.Codice, fs, cia), new Point(x0, y));
                else if (!colorato) dc.DrawText(Scritte.Testo(s, Scritte.Codice, fs, chiaro), new Point(x0, y));
                else if (tipo == "markdown" && s.StartsWith('#')) dc.DrawText(Scritte.Testo(s, Scritte.Codice, fs, oro), new Point(x0, y));
                else if (commentoDiesis && s.TrimStart().StartsWith('#')) dc.DrawText(Scritte.Testo(s, Scritte.Codice, fs, verde), new Point(x0, y));
                else
                {
                    var ultimo = 0;
                    foreach (Match m in lessico.Matches(s))
                    {
                        if (m.Index > ultimo) dc.DrawText(Scritte.Testo(s[ultimo..m.Index], Scritte.Codice, fs, chiaro), new Point(x0 + ultimo * colonna, y));
                        Brush b = chiaro;
                        if (m.Groups["c"].Success) b = verde;
                        else if (m.Groups["s"].Success) b = tipo == "json" && s.Length > m.Index + m.Length && s[(m.Index + m.Length)..].TrimStart().StartsWith(':') ? cia : oro;
                        else if (m.Groups["n"].Success) b = cia;
                        else if (m.Groups["k"].Success) b = parole.Contains(m.Value) ? mag : chiaro;
                        else if (m.Groups["a"].Success) b = grigio;
                        dc.DrawText(Scritte.Testo(m.Value, Scritte.Codice, fs, b), new Point(x0 + m.Index * colonna, y));
                        ultimo = m.Index + m.Length;
                    }
                    if (ultimo < s.Length) dc.DrawText(Scritte.Testo(s[ultimo..], Scritte.Codice, fs, chiaro), new Point(x0 + ultimo * colonna, y));
                }
                n++;
            }
            dc.Pop();
            dc.DrawRoundedRectangle(null, Tavolozza.Penna(tinta, 1, 0.35), new Rect(0.5, 0.5, W - 1, H - 1), raggio, raggio);
        });
    }

    static Quadro Csv(string testo, int L, string est)
    {
        var righe = testo.Replace("\r\n", "\n").Split('\n').Where(r => r.Length > 0).Take(9).ToList();
        var sep = est == "tsv" ? '\t' : (righe.FirstOrDefault()?.Count(c => c == ';') > righe.FirstOrDefault()?.Count(c => c == ',') ? ';' : ',');
        var celle = righe.Select(r => Dividi(r, sep).Take(5).ToArray()).ToArray();
        return Griglia(L, est.ToUpperInvariant(), Tavolozza.Tinta("Tabella"), celle.Length > 0 ? celle : null);
    }

    static IEnumerable<string> Dividi(string riga, char sep)
    {
        var sb = new StringBuilder();
        var tra = false;
        foreach (var c in riga)
        {
            if (c == '"') { tra = !tra; continue; }
            if (c == sep && !tra) { yield return sb.ToString(); sb.Clear(); continue; }
            sb.Append(c);
        }
        yield return sb.ToString();
    }

    // ————————————————————————————————————— CARATTERI —————————————————————————————————————

    public static Quadro? Carattere(string percorso, int L)
    {
        GlyphTypeface gt;
        try { gt = new GlyphTypeface(new Uri(Path.GetFullPath(percorso))); }
        catch { return null; }
        var famiglia = gt.Win32FamilyNames.TryGetValue(CultureInfo.GetCultureInfo("en-us"), out var nome) ? nome : gt.Win32FamilyNames.Values.FirstOrDefault() ?? Path.GetFileNameWithoutExtension(percorso);
        var est = Path.GetExtension(percorso).TrimStart('.').ToUpperInvariant();
        var tinta = Tavolozza.Tinta("Font");

        GlyphRun? Corsa(string s, double dim, double x, double baseline)
        {
            var indici = new List<ushort>();
            var avanzi = new List<double>();
            foreach (var ch in s)
            {
                if (!gt.CharacterToGlyphMap.TryGetValue(ch, out var gi)) gi = 0;
                indici.Add(gi);
                avanzi.Add(gt.AdvanceWidths[gi] * dim);
            }
            return new GlyphRun(gt, 0, false, dim, 1.0f, indici, new Point(x, baseline), avanzi, null, null, null, null, null, null);
        }

        return Tela.Fai(L, L, dc =>
        {
            var r = new Rect(0, 0, L, L);
            var raggio = L * 0.07;
            var fondo = new LinearGradientBrush(Tavolozza.ViolaChiaro, Tavolozza.Fondo, 125);
            fondo.Freeze();
            dc.DrawRoundedRectangle(fondo, null, r, raggio, raggio);
            Tela.Ritaglia(dc, r, raggio);
            var grande = Corsa("Aa", L * 0.46, 0, L * 0.56);
            if (grande is not null)
            {
                var larghezza = grande.ComputeAlignmentBox().Width;
                grande = Corsa("Aa", L * 0.46, (L - larghezza) / 2, L * 0.56);
                dc.DrawGlyphRun(Tavolozza.P(Colors.White), grande);
            }
            var piccolo = Corsa("Abc 123", L * 0.1, 0, L * 0.76);
            if (piccolo is not null)
            {
                var larghezza = piccolo.ComputeAlignmentBox().Width;
                piccolo = Corsa("Abc 123", L * 0.1, Math.Max(L * 0.05, (L - larghezza) / 2), L * 0.76);
                dc.DrawGlyphRun(Tavolozza.P(tinta, 0.9), piccolo);
            }
            if (L >= 96)
            {
                var t = Scritte.Testo(famiglia, Scritte.Corpo, L * 0.055, Tavolozza.P(Colors.White, 0.7), L * 0.64);
                dc.DrawText(t, new Point(L * 0.06, L - L * 0.1));
                Tela.Pastiglia(dc, est, L * 0.945, L - L * 0.115, L * 0.075, Tavolozza.P(tinta), Tavolozza.P(Tavolozza.Fondo), aDestra: true);
            }
            dc.Pop();
            dc.DrawRoundedRectangle(null, Tavolozza.Penna(tinta, 1, 0.35), new Rect(0.5, 0.5, L - 1, L - 1), raggio, raggio);
        });
    }

    // ————————————————————————————————————— ARCHIVI —————————————————————————————————————

    public static Quadro? Archivio(string percorso, int L)
    {
        var est = Path.GetExtension(percorso).TrimStart('.').ToUpperInvariant();
        if (est != "ZIP") return null;
        List<string> nomi;
        int totale;
        try
        {
            using var zip = ZipFile.OpenRead(percorso);
            var tutti = zip.Entries.Where(e => e.Name.Length > 0).ToList();
            totale = tutti.Count;
            nomi = tutti.Take(9).Select(e => e.FullName).ToList();
        }
        catch { return null; }
        var tinta = Tavolozza.Tinta("Archivio");
        int W = L, H = (int)Math.Round(L * 0.86);
        return Tela.Fai(W, H, dc =>
        {
            var r = new Rect(0, 0, W, H);
            var raggio = H * 0.06;
            var fondo = new LinearGradientBrush(Tavolozza.C("#2a1d3d"), Tavolozza.Fondo, 125);
            fondo.Freeze();
            dc.DrawRoundedRectangle(fondo, null, r, raggio, raggio);
            Tela.Ritaglia(dc, r, raggio);
            // la cerniera lungo il bordo sinistro
            var zx = W * 0.075;
            var passo = H * 0.045;
            for (var y = passo * 0.5; y < H; y += passo)
                dc.DrawRoundedRectangle(Tavolozza.P(tinta, ((int)(y / passo) % 2 == 0) ? 0.85 : 0.35), null, new Rect(zx - passo * 0.45 + (((int)(y / passo) % 2 == 0) ? -passo * 0.15 : passo * 0.15), y, passo * 0.9, passo * 0.5), passo * 0.15, passo * 0.15);
            dc.DrawRoundedRectangle(Tavolozza.P(tinta), null, new Rect(zx - passo * 0.9, H * 0.12, passo * 1.8, passo * 3.2), passo * 0.5, passo * 0.5);
            dc.DrawRoundedRectangle(Tavolozza.P(Tavolozza.Fondo), null, new Rect(zx - passo * 0.4, H * 0.12 + passo * 1.2, passo * 0.8, passo * 1.5), passo * 0.3, passo * 0.3);

            var fs = Math.Max(5, W * 0.058);
            var riga = fs * 1.55;
            var x = W * 0.17;
            var y0 = H * 0.09;
            var max = (int)((H * 0.78 - y0) / riga);
            for (var i = 0; i < Math.Min(nomi.Count, max); i++)
            {
                dc.DrawRoundedRectangle(Tavolozza.P(tinta, 0.9), null, new Rect(x, y0 + i * riga + fs * 0.18, fs * 0.75, fs * 0.95), fs * 0.12, fs * 0.12);
                dc.DrawText(Scritte.Testo(nomi[i], Scritte.Corpo, fs, Tavolozza.P(Colors.White, 0.9), W - x - fs * 1.5 - W * 0.05), new Point(x + fs * 1.1, y0 + i * riga));
            }
            if (L >= 96)
            {
                var h = Math.Max(11, H * 0.09);
                Tela.Pastiglia(dc, "ZIP", W * 0.04 + W * 0.13, H - h - H * 0.045, h, Tavolozza.P(tinta), Tavolozza.P(Tavolozza.Fondo));
                Tela.Pastiglia(dc, totale == 1 ? "1 file" : $"{totale} file", W * 0.955, H - h - H * 0.045, h, Tavolozza.P(Colors.Black, 0.4), Tavolozza.P(Colors.White, 0.9), aDestra: true);
            }
            dc.Pop();
            dc.DrawRoundedRectangle(null, Tavolozza.Penna(tinta, 1, 0.35), new Rect(0.5, 0.5, W - 1, H - 1), raggio, raggio);
        });
    }

    // ————————————————————————————————————— QUELLI SENZA ANTEPRIMA —————————————————————————————————————

    /// <summary>La carta DaProd col disegnino del tipo e la sua estensione: per i RAR, i 7z, i file che non si aprono…</summary>
    public static Quadro Generica(string gruppo, string estensione, int L)
    {
        var tinta = Tavolozza.Tinta(gruppo);
        var est = estensione.TrimStart('.').ToUpperInvariant();
        return Tela.Fai(L, L, dc =>
        {
            var r = new Rect(0, 0, L, L);
            var raggio = L * 0.07;
            var fondo = new LinearGradientBrush(Tavolozza.ViolaChiaro, Tavolozza.Fondo, 125);
            fondo.Freeze();
            dc.DrawRoundedRectangle(fondo, null, r, raggio, raggio);
            Tela.Ritaglia(dc, r, raggio);
            var luce = new RadialGradientBrush(Color.FromArgb(80, tinta.R, tinta.G, tinta.B), Color.FromArgb(0, tinta.R, tinta.G, tinta.B)) { Center = new Point(0.5, 0.42), GradientOrigin = new Point(0.5, 0.42), RadiusX = 0.55, RadiusY = 0.55 };
            luce.Freeze();
            dc.DrawRectangle(luce, null, r);
            Glifi.Disegna(dc, gruppo, new Rect(L * 0.27, L * 0.16, L * 0.46, L * 0.46), tinta, 1.5);
            if (L >= 64 && est.Length is > 0 and <= 6)
            {
                var t = Scritte.Testo(est, Scritte.Titolo, L * (est.Length > 4 ? 0.1 : 0.13), Tavolozza.P(Colors.White, 0.95));
                dc.DrawText(t, new Point((L - t.Width) / 2, L * 0.7));
            }
            dc.Pop();
            dc.DrawRoundedRectangle(null, Tavolozza.Penna(tinta, 1, 0.35), new Rect(0.5, 0.5, L - 1, L - 1), raggio, raggio);
        });
    }
}
