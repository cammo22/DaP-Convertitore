using System.Text.Json;
using Microsoft.Web.WebView2.Core;

namespace DaP.Convertitore.App;

/// <summary>
/// HTML → PDF con il motore di Edge: un WebView2 invisibile apre la pagina e la stampa. Uno alla volta,
/// sul thread della finestra (WebView2 vuole quello).
/// </summary>
public sealed class Stampante(CoreWebView2Environment env, Finestra finestra) : IStampante
{
    readonly SemaphoreSlim unoAllaVolta = new(1, 1);

    public Task Stampa(string fileHtml, string pdf, CancellationToken ct) => Con(fileHtml, ct, async w =>
    {
        var s = env.CreatePrintSettings();
        s.ShouldPrintBackgrounds = true;
        s.ShouldPrintHeaderAndFooter = false;
        s.PageWidth = 8.27; s.PageHeight = 11.69; // A4 in pollici
        s.MarginTop = 0.8; s.MarginBottom = 0.8; s.MarginLeft = 0.75; s.MarginRight = 0.75;
        if (!await w.PrintToPdfAsync(pdf, s)) throw new ErroreConversione("La stampa in PDF non è riuscita.");
        return true;
    });

    public Task<string> Testo(string fileHtml, CancellationToken ct) => Con(fileHtml, ct, async w =>
    {
        var r = await w.ExecuteScriptAsync("document.body ? document.body.innerText : ''");
        return JsonSerializer.Deserialize<string>(r) ?? "";
    });

    async Task<T> Con<T>(string fileHtml, CancellationToken ct, Func<CoreWebView2, Task<T>> fai)
    {
        await unoAllaVolta.WaitAsync(ct);
        try
        {
            return await await finestra.Dispatcher.InvokeAsync(async () =>
            {
                var c = await env.CreateCoreWebView2ControllerAsync(finestra.Maniglia);
                try
                {
                    c.IsVisible = false;
                    c.CoreWebView2.Settings.IsScriptEnabled = true;
                    var caricata = new TaskCompletionSource<bool>();
                    c.CoreWebView2.NavigationCompleted += (_, e) => caricata.TrySetResult(e.IsSuccess);
                    c.CoreWebView2.Navigate(new Uri(Path.GetFullPath(fileHtml)).AbsoluteUri);
                    var ok = await caricata.Task.WaitAsync(TimeSpan.FromSeconds(60), ct);
                    if (!ok) throw new ErroreConversione("La pagina non si è aperta.");
                    // le immagini e i caratteri hanno un attimo per arrivare
                    await c.CoreWebView2.ExecuteScriptAsync("document.fonts ? document.fonts.ready.then(()=>1) : 1");
                    return await fai(c.CoreWebView2);
                }
                finally { c.Close(); }
            });
        }
        finally { unoAllaVolta.Release(); }
    }
}
