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
        var registra = new ToolStripMenuItem("Registra…");
        registra.Click += (_, _) => SulFilo(() =>
        {
            if (Registrazione.Attiva) BarraRegistrazione.PortaDavanti();
            else MenuRegistrazione.Mostra(null);
        });
        menu.Items.Add(registra);
        var sistema = false;
        var avatar = new ToolStripMenuItem("Avatar") { CheckOnClick = true, Checked = Regia.AvatarAcceso };
        avatar.CheckedChanged += (_, _) =>
        {
            if (sistema) return;
            SulFilo(() => Regia.Avatar(avatar.Checked));
        };
        Regia.AvatarCambiato += acceso => SulFilo(() =>
        {
            sistema = true;
            avatar.Checked = acceso;
            sistema = false;
        });
        menu.Opening += (_, _) =>
        {
            registra.Text = Registrazione.Attiva ? "La registrazione è in corso" : "Registra…";
            sistema = true;
            avatar.Checked = Regia.AvatarAcceso;
            sistema = false;
        };
        menu.Items.Add(avatar);
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
