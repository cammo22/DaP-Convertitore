namespace DaP.Convertitore;

/// <summary>
/// Da dove arrivano gli aggiornamenti. Non è l'API di GitHub (<c>api.github.com</c>): senza token
/// quella si ferma dopo 60 richieste l'ora e risponde 403. Si legge il file <c>releases.win.json</c>
/// allegato alla release, lo stesso che pubblica Velopack.
/// </summary>
public static class FonteAggiornamenti
{
    public const string Repo = "https://github.com/cammo22/DaP-Convertitore";

    /// <summary>La cartella della release: ci stanno <c>releases.win.json</c> e i pacchetti.</summary>
    public static Uri Pacchetti { get; } = new(Repo + "/releases/latest/download/");
}
