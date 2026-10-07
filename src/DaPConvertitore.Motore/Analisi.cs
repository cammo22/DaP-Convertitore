using System.Collections.Concurrent;

namespace DaP.Convertitore;

/// <summary>Un file nella coda, come lo vede l'interfaccia.</summary>
public sealed record InfoFile(
    string Id,
    string Percorso,
    string Nome,
    string Cartella,
    string Estensione,
    string Categoria,
    string NomeCategoria,
    long Peso,
    bool EDirectory,
    IReadOnlyList<string> Formati);

/// <summary>
/// Guarda i file che arrivano: categoria e peso subito, i dettagli (durata, risoluzione, pagine…) appena FFprobe o
/// Windows rispondono. Le informazioni dei video restano qui, così il piano dello slider non rilegge il file.
/// </summary>
public sealed class Analisi(Strumenti strumenti)
{
    readonly ConcurrentDictionary<string, InfoMedia> media = new(StringComparer.OrdinalIgnoreCase);
    int contatore;

    public InfoFile Primo(string percorso)
    {
        var dir = Directory.Exists(percorso);
        var c = Catalogo.CategoriaDi(percorso);
        long peso = 0;
        try { if (!dir) peso = new FileInfo(percorso).Length; } catch { }
        var nome = Path.GetFileName(percorso.TrimEnd('\\'));
        return new InfoFile($"F{Interlocked.Increment(ref contatore)}", percorso, nome, Path.GetDirectoryName(percorso.TrimEnd('\\')) ?? "",
            dir ? "" : Path.GetExtension(percorso).ToLowerInvariant(), Catalogo.Chiave(c), Catalogo.NomeCategoria(c), peso, dir,
            Catalogo.FormatiPer(percorso).Select(f => f.Id).ToList());
    }

    public InfoMedia? Media(string percorso) => media.TryGetValue(percorso, out var m) ? m : null;

    public async Task<InfoMedia?> LeggiMedia(string percorso)
    {
        if (media.TryGetValue(percorso, out var m)) return m;
        try
        {
            m = await Sonda.Leggi(strumenti, percorso);
            media[percorso] = m;
            return m;
        }
        catch (Exception e) { Registro.Scrivi($"FFprobe su {Path.GetFileName(percorso)}: {e.Message}"); return null; }
    }

    /// <summary>
    /// Le misure di una foto: prima Windows (gira la foto come la vede il telefono), e se Windows quel formato non lo
    /// legge (WEBP, AVIF, HEIC senza le estensioni dello Store) ci pensa FFprobe.
    /// </summary>
    public async Task<(int w, int h)?> Misure(string percorso)
    {
        if (await Immagini.Misure(percorso) is { } m) return m;
        try
        {
            var info = await Sonda.Leggi(strumenti, percorso);
            return info.Video is { } v ? (v.Larghezza, v.Altezza) : null;
        }
        catch { return null; }
    }

    /// <summary>I dettagli che l'interfaccia mostra sotto il nome.</summary>
    public async Task<Dictionary<string, object?>> Dettagli(InfoFile f)
    {
        var d = new Dictionary<string, object?>();
        switch (f.Categoria)
        {
            case "video":
            case "audio":
            {
                var m = await LeggiMedia(f.Percorso);
                if (m is null) { d["errore"] = "non si legge"; break; }
                d["durata"] = m.Durata;
                if (m.Video is { } v)
                {
                    d["larghezza"] = v.Larghezza;
                    d["altezza"] = v.Altezza;
                    d["fps"] = Math.Round(v.Fps, 3);
                    d["codec"] = v.Codec;
                    d["hdr"] = v.Hdr;
                    d["bitrate"] = v.Bitrate;
                }
                if (m.Audio.Count > 0)
                {
                    d["audio"] = m.Audio[0].Codec;
                    d["canali"] = m.Audio[0].Canali;
                    d["campionamento"] = m.Audio[0].Campionamento;
                    d["tracceAudio"] = m.Audio.Count;
                }
                d["sottotitoli"] = m.Sottotitoli.Count(s => s.Testo);
                break;
            }
            case "immagine":
                if (await Misure(f.Percorso) is { } mis) { d["larghezza"] = mis.w; d["altezza"] = mis.h; }
                break;
            case "pdf":
                d["pagine"] = await Pdf.Pagine(f.Percorso);
                break;
            case "cartella":
                try
                {
                    var files = new DirectoryInfo(f.Percorso).EnumerateFiles("*", SearchOption.AllDirectories).ToList();
                    d["file"] = files.Count;
                    d["peso"] = files.Sum(x => x.Length);
                }
                catch { }
                break;
        }
        return d;
    }
}
