namespace DaP.Convertitore.Lettore;

/// <summary>
/// Portare fuori dall'app le voci di un archivio trascinandole nella cartella di Esplora file (o sul desktop, o in
/// un'altra app). Windows vuole file veri: si estraggono in una cartella temporanea quando il trascinamento comincia, e
/// quando il mouse esce dalla finestra l'app li porge a Windows (FileDrop). Esplora file li copia dove li lasci.
/// </summary>
public static class Trascinamento
{
    public static string Cartella => Path.Combine(Strumenti.CartellaTemporanea, "trascina");

    /// <summary>
    /// Estrae le voci (percorsi dentro l'archivio, con «/», le cartelle con «/» in fondo) in una cartella nuova e
    /// restituisce i percorsi dei file e delle cartelle da trascinare: quelle che stanno dentro una cartella scelta non
    /// si contano due volte.
    /// </summary>
    public static async Task<IReadOnlyList<string>> Prepara(Strumenti s, string archivio, IReadOnlyList<string> voci)
    {
        if (voci.Count == 0) throw new ErroreConversione("Non hai scelto niente da trascinare.");
        Pulisci();
        var dest = Path.Combine(Cartella, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dest);
        var argomenti = new List<string> { "-xf", Path.GetFullPath(archivio), "-C", dest };
        argomenti.AddRange(voci.Select(v => v.Replace('\\', '/')));
        var r = await Processi.Esegui(s.Tar, argomenti, CancellationToken.None, tieniUscita: false, silenzioso: true);
        var percorsi = PercorsiInUscita(dest, voci).Where(p => File.Exists(p) || Directory.Exists(p)).ToList();
        if (percorsi.Count == 0)
        {
            try { Directory.Delete(dest, true); } catch { }
            throw new ErroreConversione("Non ho estratto niente: forse le voci scelte non ci sono più.", r.Errori);
        }
        return percorsi;
    }

    /// <summary>Dove finiscono le voci dentro la cartella di estrazione, senza le voci già contenute in una cartella scelta.</summary>
    public static IReadOnlyList<string> PercorsiInUscita(string dest, IEnumerable<string> voci)
    {
        var pulite = voci.Select(v => v.Replace('\\', '/').TrimStart('/')).Where(v => v.Length > 0).Distinct(StringComparer.Ordinal).ToList();
        var cartelle = pulite.Where(v => v.EndsWith('/')).ToList();
        return pulite
            .Where(v => !cartelle.Any(c => c != v && v.StartsWith(c, StringComparison.Ordinal)))
            .Select(v => Path.Combine(dest, v.TrimEnd('/').Replace('/', '\\')))
            .ToList();
    }

    /// <summary>Le cartelle dei trascinamenti vecchi (più di un'ora) si buttano: Esplora file ha già copiato da un pezzo.</summary>
    public static void Pulisci()
    {
        try
        {
            if (!Directory.Exists(Cartella)) return;
            foreach (var d in Directory.EnumerateDirectories(Cartella))
                if (DateTime.UtcNow - Directory.GetCreationTimeUtc(d) > TimeSpan.FromHours(1))
                    try { Directory.Delete(d, true); } catch { /* ancora in uso: la prossima volta */ }
        }
        catch { /* niente cartella, niente da pulire */ }
    }
}
