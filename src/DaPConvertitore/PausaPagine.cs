using System.Windows;
using Microsoft.Web.WebView2.Wpf;

namespace DaP.Convertitore.App;

/// <summary>
/// WebView2, se la finestra è minimizzata o non è davanti, non deve continuare a animare.
/// Il logo della home gira all'infinito e tiene sveglio il compositore: insieme all'avatar
/// trasparente, dopo un po' il cursore sfarfalla. In pausa le animazioni CSS si fermano.
/// Minimizzata, il browser si sospende davvero.
/// </summary>
public static class PausaPagine
{
    public static void Aggiorna(WebView2 vista, Window finestra)
    {
        try
        {
            var core = vista.CoreWebView2;
            if (core is null) return;
            var nascosta = finestra.WindowState == WindowState.Minimized || !finestra.IsVisible;
            var inPausa = nascosta || !finestra.IsActive;
            _ = core.ExecuteScriptAsync(inPausa
                ? "document.documentElement.classList.add('in-pausa')"
                : "document.documentElement.classList.remove('in-pausa')");
            if (nascosta) _ = core.TrySuspendAsync();
            else core.Resume();
        }
        catch { /* la pagina non è ancora pronta */ }
    }
}
