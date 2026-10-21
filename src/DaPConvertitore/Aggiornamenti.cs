using Velopack;
using Velopack.Sources;

namespace DaP.Convertitore.App;

/// <summary>
/// Gli aggiornamenti arrivano dalle release di GitHub (Velopack: scarica solo la differenza).
/// Un ciclo solo per tutta l'app (convertitore e lettore sono finestre dello stesso processo): guarda all'avvio e poi
/// ogni <see cref="OgniQuanto"/>, senza bloccare niente e senza lamentarsi se sei offline. Appena trova una versione
/// nuova lo dice a tutte le finestre («Aggiornamento X.Y.Z disponibile» in alto), poi la scarica in silenzio.
/// Il clic sul badge la installa; se non lo premi, si mette da sola quando chiudi l'app.
/// </summary>
public static class Aggiornamenti
{
    const string Repo = "https://github.com/cammo22/DaP-Convertitore";

    /// <summary>Ogni quanto si ricontrolla se l'app resta aperta a lungo.</summary>
    public static readonly TimeSpan OgniQuanto = TimeSpan.FromHours(4);
    /// <summary>Se il controllo fallisce (offline, GitHub giù) si riprova prima.</summary>
    public static readonly TimeSpan DopoUnErrore = TimeSpan.FromMinutes(20);

    /// <summary>Per le prove a mano: con questa variabile l'app finge che esista quella versione (niente rete, niente installazione).</summary>
    public const string VariabileFinta = "DAP_AGGIORNAMENTO_FINTO";

    static UpdateInfo? trovato;
    static VelopackAsset? scaricato;
    static string? disponibile;
    static int avviato;

    /// <summary>La versione nuova trovata, o null. Si legge da qualsiasi thread.</summary>
    public static string? Disponibile => disponibile;

    /// <summary>Scatta (da un thread qualsiasi) quando compare una versione nuova.</summary>
    public static event Action<string>? Trovata;

    static UpdateManager Gestore() => new(new GithubSource(Repo, null, false));

    static string? Finta => Environment.GetEnvironmentVariable(VariabileFinta) is { Length: > 0 } v ? v : null;

    /// <summary>Fa partire il ciclo di controllo. Si può chiamare quante volte si vuole: parte una volta sola.</summary>
    public static void Avvia()
    {
        if (Interlocked.Exchange(ref avviato, 1) == 1) return;
        _ = Task.Run(Ciclo);
    }

    static async Task Ciclo()
    {
        while (true)
        {
            var riuscito = await ControllaEAvvisa();
            await Task.Delay(riuscito ? OgniQuanto : DopoUnErrore);
        }
    }

    /// <summary>Un giro: cerca, avvisa, scarica. Restituisce false se non si è potuto controllare.</summary>
    static async Task<bool> ControllaEAvvisa()
    {
        try
        {
            if (Finta is { } finta) { Segnala(finta); return true; }
            var g = Gestore();
            if (!g.IsInstalled) return true; // in sviluppo non c'è niente da aggiornare: inutile ricontrollare ogni 20 minuti
            var nuovo = await g.CheckForUpdatesAsync();
            if (nuovo is null) return true;
            trovato = nuovo;
            Segnala(nuovo.TargetFullRelease.Version.ToString());
            if (scaricato?.Version == nuovo.TargetFullRelease.Version) return true;
            await g.DownloadUpdatesAsync(nuovo);
            scaricato = nuovo.TargetFullRelease;
            Registro.Scrivi($"Aggiornamento {scaricato.Version} scaricato");
            return true;
        }
        catch (Exception e) { Registro.Errore("aggiornamenti", e); return false; }
    }

    static void Segnala(string versione)
    {
        if (disponibile == versione) return;
        disponibile = versione;
        Registro.Scrivi($"Aggiornamento {versione} disponibile");
        try { Trovata?.Invoke(versione); } catch (Exception e) { Registro.Errore("aggiornamenti (avviso)", e); }
    }

    /// <summary>Il tasto «Cerca aggiornamenti» delle impostazioni: guarda subito, adesso. Fuori dall'installazione non c'è mai niente.</summary>
    public static async Task<string?> Controlla()
    {
        try
        {
            if (Finta is { } finta) { Segnala(finta); return finta; }
            var g = Gestore();
            if (!g.IsInstalled) return null;
            trovato = await g.CheckForUpdatesAsync();
            var v = trovato?.TargetFullRelease.Version.ToString();
            if (v is not null) Segnala(v);
            return v;
        }
        catch (Exception e) { Registro.Errore("aggiornamenti", e); return null; }
    }

    public static async Task Installa()
    {
        if (Finta is not null) { Registro.Scrivi("Aggiornamento finto: niente da installare"); return; }
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
