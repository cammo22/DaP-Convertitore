using Velopack;
using Velopack.Sources;

namespace DaP.Convertitore.App;

/// <summary>Gli aggiornamenti arrivano dalle release di GitHub (Velopack: scarica solo la differenza).</summary>
public static class Aggiornamenti
{
    const string Repo = "https://github.com/cammo22/DaP-Convertitore";
    static UpdateInfo? trovato;

    static UpdateManager Gestore() => new(new GithubSource(Repo, null, false));

    /// <summary>La versione nuova, se c'è. Fuori dall'installazione (sviluppo) non c'è mai.</summary>
    public static async Task<string?> Controlla()
    {
        try
        {
            var g = Gestore();
            if (!g.IsInstalled) return null;
            trovato = await g.CheckForUpdatesAsync();
            return trovato?.TargetFullRelease.Version.ToString();
        }
        catch (Exception e) { Registro.Errore("aggiornamenti", e); return null; }
    }

    public static async Task Installa()
    {
        var g = Gestore();
        if (!g.IsInstalled) return;
        trovato ??= await g.CheckForUpdatesAsync();
        if (trovato is null) return;
        await g.DownloadUpdatesAsync(trovato);
        g.ApplyUpdatesAndRestart(trovato.TargetFullRelease);
    }
}
