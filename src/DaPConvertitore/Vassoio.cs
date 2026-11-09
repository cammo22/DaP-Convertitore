using System.Drawing;
using System.Windows;
using System.Windows.Forms;

namespace DaP.Convertitore.App;

/// <summary>
/// L'icona nell'area di notifica finché l'app è aperta: apri, screenshot, registra, impostazioni, esci.
/// </summary>
public static class Vassoio
{
    static NotifyIcon? icona;

    public static void Avvia(System.Windows.Application app)
    {
        if (icona is not null) return;
        Icon? ico = null;
        try { if (Environment.ProcessPath is { } exe) ico = Icon.ExtractAssociatedIcon(exe); } catch { /* l'icona di sistema */ }
        icona = new NotifyIcon
        {
            Icon = ico ?? SystemIcons.Application,
            Visible = true,
            Text = "DaP Convertitore",
        };
        var menu = new ContextMenuStrip();
        menu.Items.Add("Apri", null, (_, _) => SulFilo(() => Regia.Gestisci(new Richiesta(null, [], true))));
        menu.Items.Add("Screenshot", null, (_, _) => SulFilo(Schermata.Cattura));
        menu.Items.Add("Registra lo schermo", null, (_, _) => SulFilo(Registrazione.Alterna));
        menu.Items.Add("Impostazioni", null, (_, _) => SulFilo(Regia.ApriImpostazioni));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Esci", null, (_, _) => SulFilo(() => app.Shutdown()));
        icona.ContextMenuStrip = menu;
        icona.DoubleClick += (_, _) => SulFilo(() => Regia.Gestisci(new Richiesta(null, [], true)));
    }

    public static void Dire(string titolo, string testo)
    {
        try { icona?.ShowBalloonTip(4000, titolo, testo, ToolTipIcon.Info); } catch { /* il vassoio può non esserci */ }
    }

    public static void Chiudi()
    {
        if (icona is null) return;
        icona.Visible = false;
        icona.Dispose();
        icona = null;
    }

    static void SulFilo(Action fai)
    {
        var d = System.Windows.Application.Current?.Dispatcher;
        if (d is null || d.CheckAccess()) fai();
        else d.Invoke(fai);
    }
}
