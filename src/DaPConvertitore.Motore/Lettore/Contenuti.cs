using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using Markdig;
using MiniExcelLibs;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace DaP.Convertitore.Lettore;

/// <summary>Una voce di un archivio, per l'albero del lettore.</summary>
public sealed record VoceArchivio(string Percorso, long Peso, bool Cartella, DateTime? Data);

/// <summary>
/// Quello che c'è dentro i file che non sono né video né carta: testi, tabelle, archivi, foto (con i dati della
/// macchina fotografica) e, per tutto il resto, i byte.
/// </summary>
public static partial class Contenuti
{
    public const int MaxTesto = 6 * 1024 * 1024;
    public const int MaxRighe = 20000;

    /// <summary>Il testo, fino a 6 MB (oltre si legge l'inizio: un log da un giga non serve tutto a schermo).</summary>
    public static (string Testo, bool Troncato) Testo(string percorso)
    {
        using var f = File.OpenRead(percorso);
        var troncato = f.Length > MaxTesto;
        var b = new byte[Math.Min(f.Length, MaxTesto)];
        f.ReadExactly(b);
        string t;
        try { t = new UTF8Encoding(false, true).GetString(b); }
        catch (DecoderFallbackException)
        {
            // UTF-16 col BOM (i file di Windows), se no Latin-1 (i vecchi TXT con le accentate)
            t = b.Length > 1 && b[0] == 0xFF && b[1] == 0xFE ? Encoding.Unicode.GetString(b)
                : b.Length > 1 && b[0] == 0xFE && b[1] == 0xFF ? Encoding.BigEndianUnicode.GetString(b)
                : troncato ? new UTF8Encoding(false, false).GetString(b) : Encoding.Latin1.GetString(b);
        }
        return (t.TrimStart('﻿'), troncato);
    }

    static readonly MarkdownPipeline md = new MarkdownPipelineBuilder().UseAdvancedExtensions().UseEmojiAndSmiley().Build();

    public static string Markdown(string testo) => Markdig.Markdown.ToHtml(testo, md);

    // ————————————————————————— tabelle —————————————————————————

    public sealed record Foglio(IReadOnlyList<string> Fogli, string Attivo, IReadOnlyList<string> Colonne, IReadOnlyList<object?[]> Righe, int Totale);

    public static async Task<Foglio> Tabella(Strumenti s, string percorso, string? foglio)
    {
        var est = Path.GetExtension(percorso).ToLowerInvariant();
        if (est is ".csv" or ".tsv")
        {
            var t = Dati.LeggiCsv(Testo(percorso).Testo, est == ".tsv" ? '\t' : null);
            return new Foglio([], "", t.Colonne, t.Righe.Take(MaxRighe).ToList(), t.Righe.Count);
        }
        var xlsx = percorso;
        if (est is not (".xlsx" or ".xlsm"))
        {
            // XLS, ODS, NUMBERS: LibreOffice li fa diventare XLSX una volta, poi stanno in cache
            xlsx = Media.InCache(percorso, ".xlsx");
            if (!File.Exists(xlsx))
            {
                if (s.LibreOffice is null) throw new ErroreConversione("Per leggere questo foglio serve LibreOffice (gratis) o un file XLSX.", "libreoffice");
                var ctx = new Contesto(s, new InfoHardware("", 1, [], []), CancellationToken.None, _ => { });
                try { await Office.ConLibreOffice(ctx, percorso, ".xlsx", xlsx); }
                finally { ctx.Pulisci(); }
            }
        }
        var fogli = MiniExcel.GetSheetNames(xlsx);
        var nome = foglio is not null && fogli.Contains(foglio) ? foglio : fogli.FirstOrDefault() ?? "";
        var righe = new List<object?[]>();
        var colonne = new List<string>();
        var totale = 0;
        foreach (IDictionary<string, object?> r in MiniExcel.Query(xlsx, useHeaderRow: false, sheetName: nome))
        {
            totale++;
            if (righe.Count >= MaxRighe) continue;
            if (colonne.Count < r.Count) colonne = r.Keys.ToList();
            righe.Add(r.Values.Select(Pulito).ToArray());
        }
        return new Foglio(fogli, nome, colonne, righe, totale);
    }

    static object? Pulito(object? v) => v switch
    {
        DateTime d => d.TimeOfDay == TimeSpan.Zero ? d.ToString("dd/MM/yyyy") : d.ToString("dd/MM/yyyy HH:mm"),
        double x when double.IsNaN(x) || double.IsInfinity(x) => null,
        _ => v,
    };

    // ————————————————————————— archivi —————————————————————————

    /// <summary>Cosa c'è dentro, senza estrarre: lo ZIP lo legge .NET (coi nomi giusti), il resto il tar di Windows.</summary>
    public static async Task<List<VoceArchivio>> Archivio(Strumenti s, string percorso)
    {
        if (Path.GetExtension(percorso).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                using var z = ZipFile.OpenRead(percorso);
                return z.Entries.Select(e => new VoceArchivio(e.FullName.Replace('\\', '/'), e.Length, e.FullName.EndsWith('/') || e.FullName.EndsWith('\\'),
                    e.LastWriteTime.Year > 1980 ? e.LastWriteTime.LocalDateTime : null)).ToList();
            }
            catch (InvalidDataException) { /* ZIP strano: ci prova il tar */ }
        }
        var r = await Processi.Esegui(s.Tar, ["-tvf", Path.GetFullPath(percorso)], CancellationToken.None);
        if (r.Codice != 0 && r.Uscita.Length == 0)
            throw new ErroreConversione(r.Errori.Contains("passphrase", StringComparison.OrdinalIgnoreCase) || r.Errori.Contains("encrypt", StringComparison.OrdinalIgnoreCase)
                ? "L'archivio è protetto da password." : "Questo archivio non si riesce ad aprire.", r.Errori);
        var l = new List<VoceArchivio>();
        foreach (var riga in r.Uscita.Split('\n'))
        {
            var m = RigaTar().Match(riga.TrimEnd('\r'));
            if (!m.Success) continue;
            var nome = m.Groups[3].Value;
            l.Add(new VoceArchivio(nome, long.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture), m.Groups[1].Value.StartsWith('d') || nome.EndsWith('/'), null));
        }
        return l;
    }

    // -rw-rw-rw-  0 0      0           6 ott 08 03:12 a b.txt
    [GeneratedRegex(@"^(\S+)\s+\d+\s+\S+\s+\S+\s+(\d+)\s+\S+\s+\d+\s+\S+\s(.+)$")]
    private static partial Regex RigaTar();

    // ————————————————————————— byte —————————————————————————

    public static byte[] Byte(string percorso, long da, int quanti)
    {
        using var f = File.OpenRead(percorso);
        if (da >= f.Length) return [];
        f.Position = da;
        var b = new byte[(int)Math.Min(quanti, f.Length - da)];
        f.ReadExactly(b);
        return b;
    }

    public static async Task<string> Impronta(string percorso)
    {
        await using var f = new FileStream(percorso, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1 << 20, true);
        return Convert.ToHexString(await System.Security.Cryptography.SHA256.HashDataAsync(f)).ToLowerInvariant();
    }

    // ————————————————————————— foto —————————————————————————

    /// <summary>I formati che WebView2 mostra da sé: tutto il resto passa da Windows (o FFmpeg) e diventa PNG o JPG.</summary>
    public static bool FotoNativa(string percorso) =>
        Path.GetExtension(percorso).ToLowerInvariant() is ".jpg" or ".jpeg" or ".jpe" or ".jfif" or ".png" or ".apng" or ".webp" or ".gif"
            or ".bmp" or ".dib" or ".ico" or ".svg" or ".avif";

    /// <summary>HEIC, RAW, TIFF, PSD… in un formato che la pagina mostra, in cache. Le foto in JPEG, il resto in PNG (con la trasparenza).</summary>
    public static async Task<string> FotoVisibile(Strumenti s, string percorso)
    {
        var est = Path.GetExtension(percorso).ToLowerInvariant();
        var foto = Catalogo.EstensioniDi(Categoria.Immagine).Contains(est) && est is not (".tif" or ".tiff" or ".psd" or ".tga" or ".dds" or ".exr" or ".hdr" or ".qoi" or ".pbm" or ".pgm" or ".ppm" or ".pnm" or ".jxl");
        var uscita = Media.InCache(percorso, foto ? ".jpg" : ".png");
        if (File.Exists(uscita)) return uscita;
        var ctx = new Contesto(s, new InfoHardware("", 1, [], []), CancellationToken.None, _ => { });
        try
        {
            var bmp = await Immagini.Apri(percorso, ctx);
            var b = foto
                ? await Immagini.Codifica(SoftwareBitmap.Convert(bmp, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore), BitmapEncoder.JpegEncoderId, 0.93f)
                : await Immagini.Codifica(bmp.BitmapAlphaMode == BitmapAlphaMode.Straight ? bmp : SoftwareBitmap.Convert(bmp, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight), BitmapEncoder.PngEncoderId);
            await File.WriteAllBytesAsync(uscita + ".tmp", b);
            File.Move(uscita + ".tmp", uscita, true);
            return uscita;
        }
        finally { ctx.Pulisci(); }
    }

    static readonly (string Chiave, string Nome)[] campiExif =
    [
        ("System.Photo.CameraManufacturer", "Marca"),
        ("System.Photo.CameraModel", "Fotocamera"),
        ("System.Photo.LensModel", "Obiettivo"),
        ("System.Photo.FocalLength", "Focale"),
        ("System.Photo.FNumber", "Diaframma"),
        ("System.Photo.ExposureTime", "Tempo"),
        ("System.Photo.ISOSpeed", "ISO"),
        ("System.Photo.Flash", "Flash"),
        ("System.Photo.DateTaken", "Scattata"),
        ("System.GPS.Latitude", "Latitudine"),
        ("System.GPS.LatitudeRef", "LatRif"),
        ("System.GPS.Longitude", "Longitudine"),
        ("System.GPS.LongitudeRef", "LonRif"),
        ("System.ApplicationName", "Programma"),
    ];

    /// <summary>I dati della macchina fotografica, già scritti per le persone: «f/1.8», «1/120 s», «26 mm».</summary>
    public static async Task<List<(string Nome, string Valore)>> Exif(string percorso)
    {
        var l = new List<(string, string)>();
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(percorso));
            using var flusso = await file.OpenAsync(FileAccessMode.Read);
            var dec = await BitmapDecoder.CreateAsync(flusso);
            var props = await file.Properties.RetrievePropertiesAsync(campiExif.Select(c => c.Chiave));
            string? Val(string k) => props.TryGetValue(k, out var v) && v is not null ? Testo(v) : null;
            var marca = Val("System.Photo.CameraManufacturer");
            var modello = Val("System.Photo.CameraModel");
            if (modello is not null) l.Add(("Fotocamera", marca is not null && !modello.StartsWith(marca, StringComparison.OrdinalIgnoreCase) ? $"{marca} {modello}" : modello));
            if (Val("System.Photo.LensModel") is { } ob) l.Add(("Obiettivo", ob));
            var scatto = new List<string>();
            if (props.TryGetValue("System.Photo.FNumber", out var fn) && fn is double f && f > 0) scatto.Add($"f/{f.ToString("0.#", CultureInfo.GetCultureInfo("it-IT"))}");
            if (props.TryGetValue("System.Photo.ExposureTime", out var et) && et is double t && t > 0) scatto.Add(t >= 1 ? $"{t:0.#} s" : $"1/{Math.Round(1 / t)} s");
            if (props.TryGetValue("System.Photo.ISOSpeed", out var iso) && iso is not null) scatto.Add($"ISO {iso}");
            if (props.TryGetValue("System.Photo.FocalLength", out var fl) && fl is double mm && mm > 0) scatto.Add($"{mm:0} mm");
            if (scatto.Count > 0) l.Add(("Scatto", string.Join(" · ", scatto)));
            if (props.TryGetValue("System.Photo.DateTaken", out var dt) && dt is DateTimeOffset d) l.Add(("Scattata", d.LocalDateTime.ToString("d MMMM yyyy, HH:mm", CultureInfo.GetCultureInfo("it-IT"))));
            if (props.TryGetValue("System.GPS.Latitude", out var la) && la is double[] lat && props.TryGetValue("System.GPS.Longitude", out var lo) && lo is double[] lon && lat.Length == 3 && lon.Length == 3)
            {
                var y = (lat[0] + lat[1] / 60 + lat[2] / 3600) * (Val("System.GPS.LatitudeRef") == "S" ? -1 : 1);
                var x = (lon[0] + lon[1] / 60 + lon[2] / 3600) * (Val("System.GPS.LongitudeRef") == "W" ? -1 : 1);
                l.Add(("Posizione", string.Create(CultureInfo.InvariantCulture, $"{y:0.00000}, {x:0.00000}")));
            }
            if (Val("System.ApplicationName") is { } app) l.Add(("Programma", app));
            l.Add(("Colori", $"{dec.BitmapPixelFormat switch { BitmapPixelFormat.Rgba16 => "16 bit", BitmapPixelFormat.Gray8 => "bianco e nero", BitmapPixelFormat.Gray16 => "bianco e nero 16 bit", _ => "8 bit" }}{(dec.BitmapAlphaMode == BitmapAlphaMode.Ignore ? "" : " · trasparenza")}"));
        }
        catch (Exception e) { Registro.Scrivi($"Exif di {Path.GetFileName(percorso)}: {e.Message}"); }
        return l;
    }

    static string Testo(object v) => v switch
    {
        string s => s.Trim(),
        string[] a => string.Join(", ", a),
        _ => Convert.ToString(v, CultureInfo.InvariantCulture) ?? "",
    };
}
