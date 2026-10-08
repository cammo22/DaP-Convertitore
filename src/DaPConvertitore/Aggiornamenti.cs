using Velopack;
using Velopack.Sources;

namespace DaP.Convertitore.App;

/// <summary>
/// Gli aggiornamenti arrivano dalle release di GitHub (Velopack: scarica solo la differenza).
/// All'avvio si guarda in silenzio e si scarica; in alto compare «Nuova X · riavvia». Se non lo premi,
/// la versione nuova si mette da sola quando chiudi l'app.
/// </summary>
public static class Aggiornamenti
{
    const string Repo = "https://github.com/cammo22/DaP-Convertitore";
    static UpdateInfo? trovato;
    static VelopackAsset? scaricato;

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

    static Task<string?>? preparazione;

    /// <summary>Una volta sola per tutta l'app, chiunque apra per primo (il convertitore o il lettore).</summary>
    public static Task<string?> PreparaUnaVolta() => preparazione ??= Prepara();

    /// <summary>Controlla e scarica senza disturbare. Restituisce la versione pronta da installare.</summary>
    public static async Task<string?> Prepara()
    {
        try
        {
            var g = Gestore();
            if (!g.IsInstalled) return null;
            trovato ??= await g.CheckForUpdatesAsync();
            if (trovato is null) return null;
            await g.DownloadUpdatesAsync(trovato);
            scaricato = trovato.TargetFullRelease;
            Registro.Scrivi($"Aggiornamento {scaricato.Version} scaricato");
            return scaricato.Version.ToString();
        }
        catch (Exception e) { Registro.Errore("aggiornamenti", e); return null; }
    }

    public static async Task Installa()
    {
        var g = Gestore();
        if (!g.IsInstalled) return;
        if (scaricato is null)
        {
            trovato ??= await g.CheckForUpdatesAsync();
            if (trovato is null) return;
            await g.DownloadUpdatesAsync(trovato);
            scaricato = trovato.TargetFullRelease;
        }
        g.ApplyUpdatesAndRestart(scaricato);
    }

    /// <summary>Alla chiusura: se c'è una versione scaricata, Velopack la mette appena l'app è uscita.</summary>
    public static void AllaChiusura()
    {
        if (scaricato is null) return;
        try { Gestore().WaitExitThenApplyUpdates(scaricato, true, false, []); }
        catch (Exception e) { Registro.Errore("aggiornamento alla chiusura", e); }
    }
}
