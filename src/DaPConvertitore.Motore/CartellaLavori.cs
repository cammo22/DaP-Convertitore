namespace DaP.Convertitore;

/// <summary>
/// Dove finisce quello che l'app produce da sé: screenshot, registrazioni, copertine salvate da lì.
/// Di partenza è Immagini\DaPConvertitore. Si crea all'avvio se manca, e all'installazione.
/// </summary>
public static class CartellaLavori
{
    public const string Nome = "DaPConvertitore";

    /// <summary>Immagini\DaPConvertitore, anche se Pictures ha un altro nome nella lingua di Windows.</summary>
    public static string DiSistema()
    {
        var foto = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        if (string.IsNullOrWhiteSpace(foto))
            foto = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Pictures");
        return Path.Combine(foto, Nome);
    }

    /// <summary>La cartella scelta, o quella di sistema. La crea se non c'è.</summary>
    public static string Percorso(string? scelta = null)
    {
        var p = string.IsNullOrWhiteSpace(scelta) ? DiSistema() : scelta.Trim();
        Directory.CreateDirectory(p);
        return p;
    }

    /// <summary>Quella ricordata nelle impostazioni.</summary>
    public static string Assicura() => Percorso(Impostazioni.Comune.CartellaMia);

    public sealed record Voce(string Nome, long Peso, string Percorso);

    /// <summary>Gli ultimi file, i più nuovi prima.</summary>
    public static IReadOnlyList<Voce> Recenti(string? percorso = null, int quanti = 8)
    {
        var dir = percorso ?? Assicura();
        if (!Directory.Exists(dir)) return [];
        try
        {
            return new DirectoryInfo(dir).EnumerateFiles()
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Take(quanti)
                .Select(f => new Voce(f.Name, f.Length, f.FullName))
                .ToList();
        }
        catch { return []; }
    }
}
