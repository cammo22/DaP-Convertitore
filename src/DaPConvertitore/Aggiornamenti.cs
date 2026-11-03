using Velopack;
using Velopack.Sources;

namespace DaP.Convertitore.App;

/// <summary>
/// Gli aggiornamenti arrivano dal file <c>releases.win.json</c> della release (Velopack: scarica solo la differenza).
/// Non si chiede all'API di GitHub: senza token quella risponde 403 dopo poche richieste.
/// Un ciclo solo per tutta l'app (convertitore e lettore sono finestre dello stesso processo), e un controllo
/// alla volta: Velopack tiene il lucchetto <c>.velopack_lock</c> e il secondo, se parte insieme, fallisce.
/// Guarda all'avvio e poi ogni <see cref="OgniQuanto"/>, senza bloccare niente. Se la rete manca si riprova fra
/// <see cref="DopoUnErrore"/>; se GitHub chiede di aspettare, fra <see cref="DopoIlLimite"/>, e non è un errore.
/// Appena trova una versione nuova lo dice a tutte le finestre («Aggiornamento X.Y.Z disponibile» in alto),
/// poi la scarica in silenzio. Il clic sul badge la installa; se non lo premi, si mette da sola quando chiudi l'app.
/// </summary>
public static class Aggiornamenti
{
    /// <summary>Ogni quanto si ricontrolla se l'app resta aperta a lungo.</summary>
    public static readonly TimeSpan OgniQuanto = TimeSpan.FromHours(4);
    /// <summary>Se il controllo fallisce per la rete si riprova prima.</summary>
    public static readonly TimeSpan DopoUnErrore = TimeSpan.FromMinutes(20);
    /// <summary>Dopo un 403 non si martella: il limite di GitHub dura delle ore.</summary>
    public static readonly TimeSpan DopoIlLimite = TimeSpan.FromHours(6);

    /// <summary>Per le prove a mano: con questa variabile l'app finge che esista quella versione (niente rete, niente installazione).</summary>
    public const string VariabileFinta = "DAP_AGGIORNAMENTO_FINTO";

    static readonly UnoAllaVolta turno = new();
    static readonly CancellationTokenSource fermo = new();
    // 1 mentre si sta già applicando: la chiusura non deve riprendere il lucchetto (si bloccherebbe da sola)
    static int applicazione;
    static UpdateInfo? trovato;
    static VelopackAsset? scaricato;
    static string? disponibile;
    static int avviato;

    /// <summary>La versione nuova trovata, o null. Si legge da qualsiasi thread.</summary>
    public static string? Disponibile => disponibile;

    /// <summary>Scatta (da un thread qualsiasi) quando compare una versione nuova.</summary>
    public static event Action<string>? Trovata;

    static UpdateManager Gestore() => new(new SimpleWebSource(FonteAggiornamenti.Pacchetti));

    static string? Finta => Environment.GetEnvironmentVariable(VariabileFinta) is { Length: > 0 } v ? v : null;

    /// <summary>Fa partire il ciclo di controllo. Si può chiamare quante volte si vuole: parte una volta sola.</summary>
    public static void Avvia()
    {
        if (Interlocked.Exchange(ref avviato, 1) == 1) return;
        _ = Task.Run(() => Ciclo(fermo.Token));
    }

    static async Task Ciclo(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var esito = await ControllaEAvvisa(ct).ConfigureAwait(false);
            var pausa = esito switch
            {
                EsitoAggiornamento.Tipo.Fatto => OgniQuanto,
                EsitoAggiornamento.Tipo.Tardi => DopoIlLimite,
                _ => DopoUnErrore,
            };
            try { await Task.Delay(pausa, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>Un giro: cerca, avvisa, scarica. <see cref="EsitoAggiornamento.Tipo.Fatto"/> anche se non c'è niente di nuovo.</summary>
    static async Task<EsitoAggiornamento.Tipo> ControllaEAvvisa(CancellationToken ct)
    {
        try
        {
            await turno.Esegui(() => Gira(scarica: true, ct), ct).ConfigureAwait(false);
            return EsitoAggiornamento.Tipo.Fatto;
        }
        catch (OperationCanceledException) { return EsitoAggiornamento.Tipo.Fatto; }
        catch (Exception e)
        {
            Annota("aggiornamenti", e);
            return EsitoAggiornamento.Di(e);
        }
    }

    /// <summary>Cerca (e, se serve, scarica). Resta nel turno: non si chiama da fuori.</summary>
    static async Task<string?> Gira(bool scarica, CancellationToken ct)
    {
        if (Finta is { } finta) { Segnala(finta); return finta; }
        var g = Gestore();
        if (!g.IsInstalled) return null; // in sviluppo non c'è niente da aggiornare
        var nuovo = await g.CheckForUpdatesAsync().ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        if (nuovo is null) return null;
        trovato = nuovo;
        var v = nuovo.TargetFullRelease.Version.ToString();
        Segnala(v);
        if (!scarica || scaricato?.Version == nuovo.TargetFullRelease.Version) return v;
        await g.DownloadUpdatesAsync(nuovo, cancelToken: ct).ConfigureAwait(false);
        scaricato = nuovo.TargetFullRelease;
        Registro.Scrivi($"Aggiornamento {scaricato.Version} scaricato");
        return v;
    }

    static void Segnala(string versione)
    {
        if (disponibile == versione) return;
        disponibile = versione;
        Registro.Scrivi($"Aggiornamento {versione} disponibile");
        try { Trovata?.Invoke(versione); } catch (Exception e) { Registro.Errore("aggiornamenti (avviso)", e); }
    }

    static void Annota(string dove, Exception e)
    {
        var tipo = EsitoAggiornamento.Di(e);
        if (tipo == EsitoAggiornamento.Tipo.Guasto) Registro.Errore(dove, e);
        else Registro.Scrivi($"{dove}: {EsitoAggiornamento.Nota(tipo)} ({e.Message})");
    }

    /// <summary>Il tasto «Cerca aggiornamenti» delle novità: guarda subito, adesso. Fuori dall'installazione non c'è mai niente.</summary>
    public static async Task<string?> Controlla()
    {
        try { return await turno.Esegui(() => Gira(scarica: false, CancellationToken.None)).ConfigureAwait(false); }
        catch (Exception e) { Annota("aggiornamenti", e); return null; }
    }

    public static async Task Installa()
    {
        if (Finta is not null) { Registro.Scrivi("Aggiornamento finto: niente da installare"); return; }
        VelopackAsset? pacchetto = null;
        await turno.Esegui(async () =>
        {
            var g = Gestore();
            if (!g.IsInstalled) return;
            if (scaricato is null)
            {
                trovato ??= await g.CheckForUpdatesAsync().ConfigureAwait(false);
                if (trovato is null) return;
                Segnala(trovato.TargetFullRelease.Version.ToString());
                await g.DownloadUpdatesAsync(trovato).ConfigureAwait(false);
                scaricato = trovato.TargetFullRelease;
            }
            pacchetto = scaricato;
            // il riavvio fa scattare la chiusura: lì non si riprende il lucchetto, lo tiene già Velopack
            if (pacchetto is not null) Interlocked.Exchange(ref applicazione, 1);
        }).ConfigureAwait(false);
        if (pacchetto is null) return;
        try { Gestore().ApplyUpdatesAndRestart(pacchetto); }
        catch
        {
            Interlocked.Exchange(ref applicazione, 0);
            throw;
        }
    }

    /// <summary>Alla chiusura: se c'è una versione scaricata, Velopack la mette appena l'app è uscita.</summary>
    public static void AllaChiusura()
    {
        try { fermo.Cancel(); }
        catch (Exception e) { Registro.Scrivi($"Aggiornamenti: stop del ciclo ({e.Message})"); }
        if (Volatile.Read(ref applicazione) == 1) return;
        // il controllo in corso tiene il lucchetto: si aspetta un po', poi ci pensa il prossimo avvio
        if (!turno.Prova(TimeSpan.FromSeconds(25)))
        {
            Registro.Scrivi("Aggiornamento alla chiusura: un controllo è ancora in corso, ci pensa il prossimo avvio");
            return;
        }
        try
        {
            if (scaricato is null) return;
            Gestore().WaitExitThenApplyUpdates(scaricato, true, false, []);
        }
        catch (Exception e) { Annota("aggiornamento alla chiusura", e); }
        finally { turno.Esci(); }
    }
}
