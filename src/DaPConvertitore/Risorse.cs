using System.Collections.Concurrent;
using System.Globalization;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using DaP.Convertitore.Lettore;

namespace DaP.Convertitore.App;

/// <summary>
/// I file per la pagina del lettore, su https://dap.file/ (non esiste in rete: risponde l'app). La pagina non vede
/// il disco: vede solo i gettoni che le diamo noi, uno per file aperto.
///   /f/{gettone}/{nome}         il file com'è, a pezzi (Range) come vuole il tag &lt;video&gt;
///   /pdf/{gettone}/{pagina}?w=  una pagina PDF disegnata alla larghezza chiesta
///   /flusso/{id}/{n}            il pezzo n del video tradotto al volo
/// </summary>
public sealed class Risorse(Dispatcher ui)
{
    public const string Host = "dap.file";
    readonly ConcurrentDictionary<string, string> gettoni = new();
    readonly ConcurrentDictionary<string, Flusso> flussi = new();
    CoreWebView2Environment env = null!;

    public void Collega(CoreWebView2 w, CoreWebView2Environment e)
    {
        env = e;
        w.AddWebResourceRequestedFilter($"https://{Host}/*", CoreWebView2WebResourceContext.All);
        w.WebResourceRequested += Richiesta;
    }

    /// <summary>Il gettone per un file: sempre lo stesso per lo stesso percorso.</summary>
    public string Gettone(string percorso)
    {
        foreach (var (g, p) in gettoni) if (string.Equals(p, percorso, StringComparison.OrdinalIgnoreCase)) return g;
        var nuovo = Guid.NewGuid().ToString("N")[..16];
        gettoni[nuovo] = percorso;
        return nuovo;
    }

    public string Url(string percorso) => $"https://{Host}/f/{Gettone(percorso)}/{Uri.EscapeDataString(Path.GetFileName(percorso))}";

    public void Aggiungi(Flusso f) => flussi[f.Id] = f;

    public void Ferma(string? id = null)
    {
        foreach (var (k, f) in flussi)
            if (id is null || k == id) { flussi.TryRemove(k, out _); f.Dispose(); }
    }

    void Richiesta(object? _, CoreWebView2WebResourceRequestedEventArgs e)
    {
        var uri = new Uri(e.Request.Uri);
        var parti = uri.AbsolutePath.Trim('/').Split('/');
        var range = e.Request.Headers.Contains("Range") ? e.Request.Headers.GetHeader("Range") : null;
        var differita = e.GetDeferral();
        _ = Task.Run(async () =>
        {
            Risposta r;
            try { r = await Prepara(parti, uri, range); }
            catch (Exception ex)
            {
                Registro.Scrivi($"lettore: {uri.AbsolutePath} → {ex.Message}");
                r = new Risposta(null, 500, "Errore", "text/plain");
            }
            await ui.InvokeAsync(() =>
            {
                try
                {
                    var intestazioni = $"Content-Type: {r.Tipo}\r\nAccess-Control-Allow-Origin: *\r\nCache-Control: no-store";
                    if (r.Altro is not null) intestazioni += "\r\n" + r.Altro;
                    e.Response = env.CreateWebResourceResponse(r.Corpo, r.Codice, r.Motivo, intestazioni);
                }
                finally { differita.Complete(); }
            });
        });
    }

    sealed record Risposta(Stream? Corpo, int Codice, string Motivo, string Tipo, string? Altro = null);

    async Task<Risposta> Prepara(string[] parti, Uri uri, string? range)
    {
        if (parti.Length >= 2 && parti[0] == "f" && gettoni.TryGetValue(parti[1], out var file))
        {
            // un modello 3D chiama i suoi pezzi per nome (scena.bin, texture.png): solo dalla sua cartella in giù
            var resto = string.Join('/', parti.Skip(2).Select(Uri.UnescapeDataString));
            if (resto.Length > 0 && !string.Equals(resto, Path.GetFileName(file), StringComparison.OrdinalIgnoreCase))
            {
                var cartella = Path.GetDirectoryName(file)! + Path.DirectorySeparatorChar;
                var vicino = Path.GetFullPath(Path.Combine(cartella, resto));
                if (!vicino.StartsWith(cartella, StringComparison.OrdinalIgnoreCase) || !File.Exists(vicino))
                    return new Risposta(null, 404, "Non trovato", "text/plain");
                file = vicino;
            }
            return File_(file, range);
        }
        if (parti.Length >= 3 && parti[0] == "pdf" && gettoni.TryGetValue(parti[1], out var pdf) && uint.TryParse(parti[2], out var pagina))
        {
            var q = System.Web.HttpUtility.ParseQueryString(uri.Query);
            var w = uint.TryParse(q["w"], out var x) ? x : 1200;
            var b = await Carte.Pagina(pdf, pagina, w);
            return new Risposta(new MemoryStream(b), 200, "OK", "image/jpeg");
        }
        if (parti.Length >= 3 && parti[0] == "flusso" && flussi.TryGetValue(parti[1], out var f) && int.TryParse(parti[2], out var n))
        {
            var b = await f.Pezzo(n);
            // 204: il video è finito, la pagina chiude la fila
            return b is null ? new Risposta(null, 204, "Fine", f.Mime) : new Risposta(new MemoryStream(b), 200, "OK", "application/octet-stream");
        }
        return new Risposta(null, 404, "Non trovato", "text/plain");
    }

    const long Fetta = 4 * 1024 * 1024;

    static Risposta File_(string percorso, string? range)
    {
        var tipo = Tipo(percorso);
        var fs = new FileStream(percorso, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1 << 16, false);
        var tot = fs.Length;
        if (range is null || !range.StartsWith("bytes="))
            return new Risposta(fs, 200, "OK", tipo, $"Content-Length: {tot}\r\nAccept-Ranges: bytes");
        // "bytes=da-a": si risponde con al massimo 4 MB, il tag <video> chiede il resto quando gli serve
        var pezzi = range[6..].Split(',')[0].Split('-');
        long da, a;
        if (pezzi[0].Length == 0) { da = Math.Max(0, tot - long.Parse(pezzi[1], CultureInfo.InvariantCulture)); a = tot - 1; }
        else
        {
            da = long.Parse(pezzi[0], CultureInfo.InvariantCulture);
            a = pezzi.Length > 1 && pezzi[1].Length > 0 ? long.Parse(pezzi[1], CultureInfo.InvariantCulture) : tot - 1;
        }
        if (da >= tot) { fs.Dispose(); return new Risposta(null, 416, "Range Not Satisfiable", tipo, $"Content-Range: bytes */{tot}"); }
        a = Math.Min(Math.Min(a, tot - 1), da + Fetta - 1);
        var buf = new byte[a - da + 1];
        using (fs)
        {
            fs.Position = da;
            fs.ReadExactly(buf);
        }
        return new Risposta(new MemoryStream(buf), 206, "Partial Content", tipo,
            $"Content-Range: bytes {da}-{a}/{tot}\r\nContent-Length: {buf.Length}\r\nAccept-Ranges: bytes");
    }

    static string Tipo(string percorso) => Path.GetExtension(percorso).ToLowerInvariant() switch
    {
        ".mp4" or ".m4v" or ".mov" => "video/mp4",
        ".webm" => "video/webm",
        ".mkv" => "video/x-matroska",
        ".ogv" => "video/ogg",
        ".mp3" => "audio/mpeg",
        ".m4a" or ".m4b" or ".aac" => "audio/mp4",
        ".flac" => "audio/flac",
        ".wav" => "audio/wav",
        ".ogg" or ".oga" => "audio/ogg",
        ".opus" => "audio/ogg",
        ".weba" => "audio/webm",
        ".jpg" or ".jpeg" or ".jpe" or ".jfif" => "image/jpeg",
        ".png" or ".apng" => "image/png",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".avif" => "image/avif",
        ".bmp" or ".dib" => "image/bmp",
        ".ico" => "image/x-icon",
        ".svg" => "image/svg+xml",
        ".pdf" => "application/pdf",
        ".ttf" => "font/ttf",
        ".otf" => "font/otf",
        ".woff" => "font/woff",
        ".woff2" => "font/woff2",
        ".glb" => "model/gltf-binary",
        ".gltf" => "model/gltf+json",
        ".html" or ".htm" => "text/plain; charset=utf-8",
        _ => "application/octet-stream",
    };
}
