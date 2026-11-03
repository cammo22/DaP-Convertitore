using System.Net;
using System.Net.Http;

namespace DaP.Convertitore;

/// <summary>
/// Come è andato un controllo aggiornamenti. Il limite di GitHub e il lucchetto di Velopack
/// (due controlli insieme) non sono un guasto: si riprova più tardi, senza scriverli come errore.
/// </summary>
public static class EsitoAggiornamento
{
    public enum Tipo { Fatto, Presto, Tardi, Guasto }

    public static Tipo Di(Exception e)
    {
        Tipo? rete = null;
        for (Exception? x = e; x is not null; x = x.InnerException)
        {
            var nome = x.GetType().Name;
            var msg = x.Message ?? "";
            if (nome.Contains("AcquireLock", StringComparison.Ordinal)
                || msg.Contains("exclusive lock", StringComparison.OrdinalIgnoreCase)
                || msg.Contains(".velopack_lock", StringComparison.OrdinalIgnoreCase))
                return Tipo.Presto;
            if (x is HttpRequestException http)
            {
                if (http.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests) return Tipo.Tardi;
                if (http.StatusCode is HttpStatusCode.NotFound) return Tipo.Guasto;
                rete ??= Tipo.Presto;
            }
            if (msg.Contains("rate limit", StringComparison.OrdinalIgnoreCase) || ÈCodice(msg, 403) || ÈCodice(msg, 429))
                return Tipo.Tardi;
            if (ÈCodice(msg, 404)) return Tipo.Guasto;
            if (nome is "SocketException" or "TimeoutException" or "WebException") rete ??= Tipo.Presto;
        }
        return rete ?? Tipo.Guasto;
    }

    /// <summary>La riga nel registro, per i casi che non sono un errore dell'app.</summary>
    public static string Nota(Tipo tipo) => tipo switch
    {
        Tipo.Tardi => "GitHub ha chiesto di aspettare, riprovo più tardi",
        Tipo.Presto => "lo riprovo fra un po' (rete o un altro controllo già in corso)",
        Tipo.Fatto => "fatto",
        _ => "è andato storto",
    };

    static bool ÈCodice(string msg, int codice) =>
        msg.Contains("status code", StringComparison.OrdinalIgnoreCase) && msg.Contains(codice.ToString(), StringComparison.Ordinal);
}
