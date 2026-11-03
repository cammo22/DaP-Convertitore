namespace DaP.Convertitore;

/// <summary>
/// Un lavoro per volta: il secondo aspetta che il primo abbia finito.
/// Gli aggiornamenti ne hanno bisogno, perché Velopack tiene un lucchetto
/// (<c>.velopack_lock</c>) e il secondo controllo fallisce se il primo è ancora dentro.
/// </summary>
public sealed class UnoAllaVolta
{
    readonly SemaphoreSlim porta = new(1, 1);

    public async Task<T> Esegui<T>(Func<Task<T>> lavoro, CancellationToken ct = default)
    {
        await porta.WaitAsync(ct).ConfigureAwait(false);
        try { return await lavoro().ConfigureAwait(false); }
        finally { porta.Release(); }
    }

    public async Task Esegui(Func<Task> lavoro, CancellationToken ct = default)
    {
        await Esegui(async () => { await lavoro().ConfigureAwait(false); return 0; }, ct).ConfigureAwait(false);
    }

    /// <summary>Entra se la porta si libera entro <paramref name="attesa"/>; poi va chiamato <see cref="Esci"/>.</summary>
    public bool Prova(TimeSpan attesa) => porta.Wait(attesa);

    public void Esci() => porta.Release();
}
