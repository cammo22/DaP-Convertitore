using System.Net;
using System.Net.Http;

namespace DaP.Convertitore.Prove;

/// <summary>
/// Le correzioni del registro: le righe delle miniature, gli aggiornamenti che non si pestano i piedi,
/// il 403 di GitHub e la stima annullata che non è un errore.
/// </summary>
public class ProveRobustezza
{
    [Fact]
    public void ZeroRigheVuolDireSenzaLimite()
    {
        // WPF rifiuta MaxLineCount <= 0: 0 deve diventare «tutte quelle che ci stanno»
        Assert.Equal(int.MaxValue, RigheTesto.Massimo(0));
        Assert.Equal(int.MaxValue, RigheTesto.Massimo(-3));
        Assert.Equal(2, RigheTesto.Massimo(2));
        Assert.True(RigheTesto.Massimo(0) > 0);
    }

    [Fact]
    public void IlFeedNonPassaDallApiDiGitHub()
    {
        var u = FonteAggiornamenti.Pacchetti;
        Assert.Equal("github.com", u.Host);
        Assert.DoesNotContain("api.github.com", u.AbsoluteUri, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("/releases/latest/download/", u.AbsoluteUri);
        Assert.EndsWith("/releases/latest/download/releases.win.json", u.AbsoluteUri.TrimEnd('/') + "/releases.win.json");
    }

    [Fact]
    public void IlLimiteEIlLucchettoNonSonoUnGuasto()
    {
        var lucchetto = new InvalidOperationException("Failed to acquire exclusive lock file. Is another operation currently running?");
        Assert.Equal(EsitoAggiornamento.Tipo.Presto, EsitoAggiornamento.Di(lucchetto));
        Assert.Equal(EsitoAggiornamento.Tipo.Presto, EsitoAggiornamento.Di(new IOException("no", new Exception("cannot lock .velopack_lock"))));

        var limite = new HttpRequestException("Response status code does not indicate success: 403 (rate limit exceeded).", null, HttpStatusCode.Forbidden);
        Assert.Equal(EsitoAggiornamento.Tipo.Tardi, EsitoAggiornamento.Di(limite));
        Assert.Equal(EsitoAggiornamento.Tipo.Tardi, EsitoAggiornamento.Di(new HttpRequestException("Response status code does not indicate success: 429 (Too Many Requests).", null, HttpStatusCode.TooManyRequests)));
        Assert.Equal(EsitoAggiornamento.Tipo.Tardi, EsitoAggiornamento.Di(new Exception("API rate limit exceeded for 1.2.3.4")));

        Assert.Equal(EsitoAggiornamento.Tipo.Guasto, EsitoAggiornamento.Di(new HttpRequestException("Response status code does not indicate success: 404 (Not Found).", null, HttpStatusCode.NotFound)));
        Assert.Equal(EsitoAggiornamento.Tipo.Presto, EsitoAggiornamento.Di(new HttpRequestException("No such host is known.")));
        Assert.Equal(EsitoAggiornamento.Tipo.Guasto, EsitoAggiornamento.Di(new InvalidOperationException("boom")));

        Assert.DoesNotContain("ERRORE", EsitoAggiornamento.Nota(EsitoAggiornamento.Tipo.Tardi), StringComparison.Ordinal);
        Assert.DoesNotContain("ERRORE", EsitoAggiornamento.Nota(EsitoAggiornamento.Tipo.Presto), StringComparison.Ordinal);
    }

    [Fact]
    public void LaStimaAnnullataNonVaNelRegistro()
    {
        Assert.False(EccezioniPonte.DaRegistro(new TaskCanceledException()));
        Assert.False(EccezioniPonte.DaRegistro(new OperationCanceledException()));
        Assert.True(EccezioniPonte.DaRegistro(new InvalidOperationException("x")));
    }

    [Fact]
    public async Task DueControlliNonVannoInsiemeEDopoUnErroreSiRiparte()
    {
        var porta = new UnoAllaVolta();
        var insieme = 0;
        var massimo = 0;
        async Task Lavoro()
        {
            await porta.Esegui(async () =>
            {
                var n = Interlocked.Increment(ref insieme);
                massimo = Math.Max(massimo, n);
                await Task.Delay(40);
                Interlocked.Decrement(ref insieme);
            });
        }
        await Task.WhenAll(Lavoro(), Lavoro(), Lavoro());
        Assert.Equal(1, massimo);

        await Assert.ThrowsAsync<InvalidOperationException>(() => porta.Esegui(async () =>
        {
            await Task.Yield();
            throw new InvalidOperationException("x");
        }));
        var entrato = false;
        await porta.Esegui(() => { entrato = true; return Task.CompletedTask; });
        Assert.True(entrato);

        var dentro = new TaskCompletionSource();
        var occupata = porta.Esegui(async () => { dentro.SetResult(); await Task.Delay(400); });
        await dentro.Task;
        Assert.False(porta.Prova(TimeSpan.Zero));
        await occupata;
        Assert.True(porta.Prova(TimeSpan.FromSeconds(2)));
        porta.Esci();
    }
}
