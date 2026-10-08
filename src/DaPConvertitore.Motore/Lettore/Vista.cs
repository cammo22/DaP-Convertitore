namespace DaP.Convertitore.Lettore;

/// <summary>
/// Il lettore: come si guarda un file. Ogni tipo ha la sua pagina (il cinema per i video, il giradischi per la
/// musica, il tavolo luminoso per le foto…). Qui si decide quale, e quali file della cartella gli stanno accanto
/// (le frecce passano dall'uno all'altro, come in Foto).
/// </summary>
public static class Vista
{
    static readonly Dictionary<string, string> extra = new(StringComparer.OrdinalIgnoreCase);

    static void Metti(string tipo, string elenco)
    {
        foreach (var e in elenco.Split(' ', StringSplitOptions.RemoveEmptyEntries)) extra["." + e] = tipo;
    }

    static Vista()
    {
        Metti("font", "ttf otf woff woff2");
        Metti("modello", "glb gltf stl obj ply 3mf");
        Metti("codice", "js mjs cjs ts tsx jsx py cs c h cpp hpp cc java kt go rs rb php swift lua sh bash ps1 psm1 bat cmd sql " +
                        "css scss less xml xaml csproj props targets yml yaml toml ini cfg conf env log gitignore editorconfig " +
                        "vue svelte dart r m pl vb fs json5 jsonc gradle makefile dockerfile reg inf nfo diff patch");
        Metti("markdown", "md markdown");
        Metti("web", "html htm");
        Metti("json", "json geojson webmanifest");
        Metti("tabella", "csv tsv");
    }

    /// <summary>
    /// video, audio, immagine, pdf, documento, presentazione, tabella, markdown, web, json, codice, testo,
    /// sottotitoli, archivio, font, modello, esadecimale (tutto il resto: si guarda dentro byte per byte).
    /// </summary>
    public static string Tipo(string percorso)
    {
        var est = Path.GetExtension(percorso);
        if (extra.TryGetValue(est, out var t)) return t;
        return Catalogo.CategoriaDi(percorso) switch
        {
            Categoria.Video => "video",
            Categoria.Audio => "audio",
            Categoria.Immagine => "immagine",
            Categoria.Pdf => "pdf",
            Categoria.Documento => "documento",
            Categoria.Presentazione => "presentazione",
            Categoria.Foglio => "tabella",
            Categoria.Dati => "tabella",
            Categoria.Testo => "testo",
            Categoria.Sottotitoli => "sottotitoli",
            Categoria.Archivio => "archivio",
            _ => SembraTesto(percorso) ? "testo" : "esadecimale",
        };
    }

    /// <summary>Senza estensione nota: se i primi 8 KB sono UTF-8 senza byte zero, è testo.</summary>
    public static bool SembraTesto(string percorso)
    {
        try
        {
            using var f = File.OpenRead(percorso);
            var b = new byte[8192];
            var n = f.Read(b, 0, b.Length);
            if (n == 0) return true;
            if (b.AsSpan(0, n).IndexOf((byte)0) >= 0) return false;
            // l'ultimo carattere può essere tagliato a metà: si guarda fino a 4 byte prima
            var dec = new System.Text.UTF8Encoding(false, true);
            for (var taglio = 0; taglio < 4 && taglio < n; taglio++)
                try { dec.GetCharCount(b, 0, n - taglio); return true; } catch { }
            return false;
        }
        catch { return false; }
    }

    /// <summary>Le estensioni che il lettore sa aprire: vanno in «Apri con» di Windows.</summary>
    public static IEnumerable<string> Estensioni =>
        Catalogo.TutteLeEstensioni.Concat(extra.Keys)
            // gli archivi e le cartelle li apre Esplora file; script ed eseguibili non si toccano
            .Where(e => e is not (".bat" or ".cmd" or ".ps1" or ".psm1" or ".reg" or ".inf" or ".sh"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order();

    /// <summary>Il gruppo dell'«Apri con»: decide icona e nome del tipo («Video · DaP Convertitore»).</summary>
    public static string Gruppo(string est) => Tipo("x" + est) switch
    {
        "video" => "Video",
        "audio" => "Audio",
        "immagine" => "Immagine",
        "pdf" => "Pdf",
        "documento" => "Documento",
        "presentazione" => "Presentazione",
        "tabella" => "Tabella",
        "archivio" => "Archivio",
        "font" => "Font",
        "modello" => "Modello",
        "codice" or "markdown" or "web" or "json" => "Codice",
        "sottotitoli" => "Sottotitoli",
        _ => "Testo",
    };

    /// <summary>
    /// Le miniature che mostrano sempre le nostre, anche dove Windows ne ha già una (il fotogramma coi suoi comandi,
    /// la forma d'onda, il modello 3D): video, musica e modelli. Per il resto le nostre vanno solo dove Windows non ne ha.
    /// </summary>
    public static bool MiniaturaSempre(string est) => Tipo("x" + est) is "video" or "audio" or "modello" or "immagine";

    /// <summary>
    /// I file accanto, dello stesso genere (foto con foto, musica con musica), in ordine di nome come in Esplora
    /// file. Se sono troppi si tengono i più vicini.
    /// </summary>
    public static IReadOnlyList<string> Fratelli(string percorso, Func<string, string, int> confronta)
    {
        try
        {
            var tipo = Tipo(percorso);
            var cartella = Path.GetDirectoryName(percorso)!;
            var lista = Directory.EnumerateFiles(cartella)
                .Where(f => (File.GetAttributes(f) & (FileAttributes.Hidden | FileAttributes.System)) == 0)
                .Where(f => Simile(Tipo(f), tipo))
                .ToList();
            lista.Sort((a, b) => confronta(Path.GetFileName(a), Path.GetFileName(b)));
            if (!lista.Contains(percorso, StringComparer.OrdinalIgnoreCase)) lista.Insert(0, percorso);
            if (lista.Count <= 2000) return lista;
            var i = lista.FindIndex(f => string.Equals(f, percorso, StringComparison.OrdinalIgnoreCase));
            var da = Math.Clamp(i - 1000, 0, lista.Count - 2000);
            return lista.GetRange(da, 2000);
        }
        catch { return [percorso]; }
    }

    static bool Simile(string a, string b)
    {
        static string Famiglia(string t) => t switch
        {
            "codice" or "testo" or "json" or "markdown" or "web" => "testo",
            "documento" or "presentazione" or "pdf" => "carta",
            _ => t,
        };
        return Famiglia(a) == Famiglia(b);
    }
}
