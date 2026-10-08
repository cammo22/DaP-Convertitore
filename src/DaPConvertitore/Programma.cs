using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using Velopack;

namespace DaP.Convertitore.App;

/// <summary>Cosa chiede chi ci lancia: dei file, e forse un'azione del menu rapido, o di guardarli nel lettore.</summary>
public sealed record Richiesta(string? Azione, IReadOnlyList<string> Percorsi, bool Finestra, bool Guarda = false)
{
    public static Richiesta Da(string[] args)
    {
        string? azione = null;
        var finestra = false;
        var guarda = false;
        var file = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (a == "--azione" && i + 1 < args.Length) { azione = args[++i]; continue; }
            if (a == "--apri") { finestra = true; continue; }
            if (a == "--guarda") { guarda = true; continue; }
            // il menu di Windows 11 passa tutti i file in un file di testo, uno per riga: niente limite di lunghezza
            if (a == "--lista" && i + 1 < args.Length)
            {
                var lista = args[++i].Trim('"');
                try
                {
                    foreach (var riga in File.ReadAllLines(lista))
                        if (riga.Trim() is { Length: > 0 } p2 && (File.Exists(p2) || Directory.Exists(p2))) file.Add(Path.GetFullPath(p2));
                    File.Delete(lista);
                }
                catch (Exception e) { Registro.Errore("lista dal menu", e); }
                continue;
            }
            if (a.StartsWith("--")) continue;
            var p = a.Trim('"');
            if (File.Exists(p) || Directory.Exists(p)) file.Add(Path.GetFullPath(p));
        }
        return new Richiesta(azione, file, finestra || azione is null, guarda && azione is null);
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
            .OnAfterInstallFastCallback(_ => { Registra(); RegistraWindows11(); RegistraApriCon(); })
            .OnAfterUpdateFastCallback(_ =>
            {
                var imp = Impostazioni.Carica();
                if (imp.Menu) { Registra(); RegistraWindows11(); }
                if (imp.ApriCon) RegistraApriCon();
            })
            // la DLL del menu di Windows 11 sta aperta in un dllhost: si ferma, se no la cartella non si sostituisce
            .OnBeforeUpdateFastCallback(_ => Menu11.FermaSurrogato())
            .OnBeforeUninstallFastCallback(_ => { MenuContestuale.Rimuovi(); ApriCon.Rimuovi(); Menu11.Rimuovi().GetAwaiter().GetResult(); })
            .Run();

        if (args.Contains("--registra")) { Registra(); RegistraWindows11(); RegistraApriCon(); return; }
        if (args.Contains("--rimuovi")) { MenuContestuale.Rimuovi(); ApriCon.Rimuovi(); Menu11.Rimuovi().GetAwaiter().GetResult(); return; }
        // solo «Apri con», senza toccare i menu del tasto destro (per le prove)
        if (args.Contains("--apricon")) { RegistraApriCon(); return; }
        if (args.Contains("--togli-apricon")) { ApriCon.Rimuovi(); return; }
        var url = Array.IndexOf(args, "--url");
        if (url >= 0 && url + 1 < args.Length) { Url(args[url + 1]); return; }

        try { SetCurrentProcessExplicitAppUserModelID(MenuContestuale.Aumid); } catch { }
        var richiesta = Richiesta.Da(args);

        using var mutex = new Mutex(true, $@"Local\{MenuContestuale.Aumid}", out var primo);
        // Esplora file lancia un processo per ogni file scelto: il primo apre la finestra, gli altri gli passano i file
        if (!primo && Istanza.Manda(richiesta)) return;

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.DispatcherUnhandledException += (_, e) => { Registro.Errore("interfaccia", e.Exception); e.Handled = true; };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Registro.Errore("app", (Exception)e.ExceptionObject);
        app.Exit += (_, _) => Aggiornamenti.AllaChiusura();
        Regia.Avvia(app);
        Regia.Gestisci(richiesta);
        if (primo) Istanza.Ascolta(r => app.Dispatcher.BeginInvoke(() => Regia.Gestisci(r)));
        _ = Task.Run(Lettore.Media.PulisciCache);
        app.Run();
    }

    static void Registra()
    {
        var exe = Environment.ProcessPath!;
        MenuContestuale.Registra(exe, Path.Combine(Path.GetDirectoryName(exe)!, "icona.png"));
    }

    /// <summary>Il lettore in «Apri con» e fra le app predefinite: si offre, non si impone.</summary>
    static void RegistraApriCon()
    {
        try { ApriCon.Registra(Environment.ProcessPath!); }
        catch (Exception e) { Registro.Errore("Apri con", e); }
    }

    /// <summary>
    /// Il menu nuovo di Windows 11, se il certificato di DaProd è già fidato su questo PC (se no lo chiede l'app,
    /// una volta, con la finestra del permesso: qui durante l'installazione non si disturba).
    /// </summary>
    static void RegistraWindows11()
    {
        var cartella = AppContext.BaseDirectory;
        if (Menu11.Windows11 && Menu11.Presente(cartella) && Menu11.Fidato(cartella))
            Menu11.Registra(cartella).GetAwaiter().GetResult();
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
