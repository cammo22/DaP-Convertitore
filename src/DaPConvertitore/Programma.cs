using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using Velopack;

namespace DaP.Convertitore.App;

/// <summary>Cosa chiede chi ci lancia: dei file, e forse un'azione del menu rapido.</summary>
public sealed record Richiesta(string? Azione, IReadOnlyList<string> Percorsi, bool Finestra)
{
    public static Richiesta Da(string[] args)
    {
        string? azione = null;
        var finestra = false;
        var file = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (a == "--azione" && i + 1 < args.Length) { azione = args[++i]; continue; }
            if (a == "--apri") { finestra = true; continue; }
            if (a.StartsWith("--")) continue;
            var p = a.Trim('"');
            if (File.Exists(p) || Directory.Exists(p)) file.Add(Path.GetFullPath(p));
        }
        return new Richiesta(azione, file, finestra || azione is null);
    }
}

public static class Programma
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern int SetCurrentProcessExplicitAppUserModelID(string id);

    [STAThread]
    public static void Main(string[] args)
    {
        // Velopack per primo: installazione, aggiornamento e disinstallazione passano da qui e finiscono subito
        VelopackApp.Build()
            .SetAppUserModelId(MenuContestuale.Aumid)
            .OnAfterInstallFastCallback(_ => Registra())
            .OnAfterUpdateFastCallback(_ => { if (Impostazioni.Carica().Menu) Registra(); })
            .OnBeforeUninstallFastCallback(_ => MenuContestuale.Rimuovi())
            .Run();

        if (args.Contains("--registra")) { Registra(); return; }
        if (args.Contains("--rimuovi")) { MenuContestuale.Rimuovi(); return; }
        var url = Array.IndexOf(args, "--url");
        if (url >= 0 && url + 1 < args.Length) { Url(args[url + 1]); return; }

        try { SetCurrentProcessExplicitAppUserModelID(MenuContestuale.Aumid); } catch { }
        var richiesta = Richiesta.Da(args);

        using var mutex = new Mutex(true, $@"Local\{MenuContestuale.Aumid}", out var primo);
        // Esplora file lancia un processo per ogni file scelto: il primo apre la finestra, gli altri gli passano i file
        if (!primo && Istanza.Manda(richiesta)) return;

        var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        app.DispatcherUnhandledException += (_, e) => { Registro.Errore("interfaccia", e.Exception); e.Handled = true; };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Registro.Errore("app", (Exception)e.ExceptionObject);
        var finestra = new Finestra(richiesta);
        if (primo) Istanza.Ascolta(r => finestra.Dispatcher.BeginInvoke(() => finestra.Ponte.Gestisci(r)));
        app.Run(finestra);
    }

    static void Registra()
    {
        var exe = Environment.ProcessPath!;
        MenuContestuale.Registra(exe, Path.Combine(Path.GetDirectoryName(exe)!, "icona.png"));
    }

    /// <summary>I bottoni delle notifiche: dap-convertitore:mostra?p=… e dap-convertitore:apri?p=…</summary>
    static void Url(string url)
    {
        try
        {
            var u = new Uri(url);
            var q = System.Web.HttpUtility.ParseQueryString(u.Query);
            var p = q["p"];
            if (string.IsNullOrEmpty(p) || !(File.Exists(p) || Directory.Exists(p))) return;
            var cosa = u.AbsolutePath.Trim('/');
            if (cosa == "apri") Process.Start(new ProcessStartInfo(p) { UseShellExecute = true });
            else Mostra(p);
        }
        catch (Exception e) { Registro.Errore("url", e); }
    }

    public static void Mostra(string percorso) =>
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{percorso}\"") { UseShellExecute = true });
}
