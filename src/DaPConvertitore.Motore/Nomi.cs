namespace DaP.Convertitore;

/// <summary>
/// Come si chiama quello che esce: accanto all'originale, con la scritta "(convertito)".
/// Se c'è già, "(convertito 2)", "(convertito 3)"… Non si sovrascrive mai niente.
/// </summary>
public static class Nomi
{
    static readonly object chiave = new();
    static readonly HashSet<string> prenotati = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Il nome senza estensione, anche per le doppie come .tar.gz.</summary>
    public static string Radice(string percorso)
    {
        var nome = Path.GetFileName(percorso.TrimEnd('\\', '/'));
        foreach (var doppia in new[] { ".tar.gz", ".tar.xz", ".tar.bz2", ".tar.zst" })
            if (nome.EndsWith(doppia, StringComparison.OrdinalIgnoreCase)) return nome[..^doppia.Length];
        return Directory.Exists(percorso) ? nome : Path.GetFileNameWithoutExtension(nome);
    }

    /// <summary>
    /// Trova il nome libero e lo tiene da parte finché il lavoro non finisce,
    /// così due conversioni insieme non scelgono lo stesso.
    /// </summary>
    public static string Prenota(string sorgente, string estensione, string scritta = "convertito")
    {
        var cartella = Path.GetDirectoryName(Path.GetFullPath(sorgente.TrimEnd('\\', '/')))!;
        var radice = Radice(sorgente);
        lock (chiave)
        {
            for (var n = 1; ; n++)
            {
                var etichetta = n == 1 ? scritta : $"{scritta} {n}";
                var nome = string.IsNullOrWhiteSpace(scritta) && n == 1 ? radice : $"{radice} ({etichetta.Trim()})";
                var candidato = Path.Combine(cartella, nome + estensione);
                if (File.Exists(candidato) || Directory.Exists(candidato) || prenotati.Contains(candidato)) continue;
                prenotati.Add(candidato);
                return candidato;
            }
        }
    }

    /// <summary>Per "estrai qui": la cartella si chiama come l'archivio, e se c'è già "nome (2)".</summary>
    public static string PrenotaCartella(string archivio)
    {
        var cartella = Path.GetDirectoryName(Path.GetFullPath(archivio))!;
        var radice = Radice(archivio);
        lock (chiave)
        {
            for (var n = 1; ; n++)
            {
                var candidato = Path.Combine(cartella, n == 1 ? radice : $"{radice} ({n})");
                if (File.Exists(candidato) || Directory.Exists(candidato) || prenotati.Contains(candidato)) continue;
                prenotati.Add(candidato);
                return candidato;
            }
        }
    }

    public static void Libera(string percorso)
    {
        lock (chiave) prenotati.Remove(percorso);
    }

    /// <summary>
    /// Il file di lavoro: sta nella stessa cartella (così lo spostamento finale è istantaneo),
    /// comincia con ".~" e ha la stessa estensione, che a FFmpeg serve.
    /// </summary>
    public static string Bozza(string finale) =>
        Path.Combine(Path.GetDirectoryName(finale)!, ".~" + Path.GetFileName(finale));
}
