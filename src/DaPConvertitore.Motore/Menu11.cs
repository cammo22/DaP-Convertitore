using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Windows.Management.Deployment;

namespace DaP.Convertitore;

/// <summary>
/// Il tasto destro nuovo di Windows 11 (quello senza «Mostra altre opzioni»). Ci entrano solo i comandi
/// IExplorerCommand di un pacchetto con identità: la DLL è menu\DaPMenu.cpp, il pacchetto sparse
/// DaPConvertitore.Menu.msix (solo manifest e icone, i file restano nella cartella dell'app).
///
/// Il pacchetto è firmato col certificato di DaProd. Windows lo registra solo se quel certificato è fidato per
/// tutto il computer (Persone attendibili del computer locale): è l'unico passo che vuole l'amministratore, e si fa
/// una volta sola (certutil, col permesso chiesto da Windows). Da lì in poi registrare, aggiornare e togliere il
/// pacchetto si fa per l'utente, senza chiedere niente.
/// </summary>
public static class Menu11
{
    public const string NomePacchetto = "DaProd.Convertitore.Menu";
    public const string Dll = "DaPConvertitore.Menu.dll";
    public const string Msix = "DaPConvertitore.Menu.msix";
    public const string Certificato = "DaProdProduzioni-firma.cer";

    /// <summary>Windows 11 (build 22000 e dopo): su Windows 10 il menu classico basta e i comandi comparirebbero due volte.</summary>
    public static bool Windows11 => Environment.OSVersion.Version.Build >= 22000;

    public static bool Presente(string cartella) =>
        File.Exists(Path.Combine(cartella, Dll)) && File.Exists(Path.Combine(cartella, Msix)) && File.Exists(Path.Combine(cartella, Certificato));

    public static bool Registrato() => Pacchetti().Any();

    static IEnumerable<Windows.ApplicationModel.Package> Pacchetti()
    {
        try { return new PackageManager().FindPackagesForUser("").Where(p => p.Id.Name == NomePacchetto).ToList(); }
        catch { return []; }
    }

    static X509Certificate2? Cert(string cartella)
    {
        try { return X509CertificateLoader.LoadCertificateFromFile(Path.Combine(cartella, Certificato)); }
        catch { return null; }
    }

    /// <summary>Il certificato di DaProd è già fra le Persone attendibili del computer?</summary>
    public static bool Fidato(string cartella)
    {
        var c = Cert(cartella);
        if (c is null) return false;
        try
        {
            using var s = new X509Store(StoreName.TrustedPeople, StoreLocation.LocalMachine);
            s.Open(OpenFlags.ReadOnly);
            return s.Certificates.Find(X509FindType.FindByThumbprint, c.Thumbprint, false).Count > 0;
        }
        catch { return false; }
    }

    /// <summary>
    /// L'unico passo da amministratore: certutil mette il certificato (solo la parte pubblica) fra le Persone
    /// attendibili del computer. Windows mostra la sua finestra del permesso; se Cammo dice no, non succede niente.
    /// </summary>
    public static async Task<bool> RendiFidato(string cartella)
    {
        if (Fidato(cartella)) return true;
        try
        {
            var psi = new ProcessStartInfo("certutil.exe", $"-addstore TrustedPeople \"{Path.Combine(cartella, Certificato)}\"")
            {
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            using var p = Process.Start(psi);
            if (p is not null) await p.WaitForExitAsync();
        }
        catch (System.ComponentModel.Win32Exception) { return false; } // permesso negato
        Registro.Scrivi($"Certificato del menu di Windows 11 fidato: {Fidato(cartella)}");
        return Fidato(cartella);
    }

    /// <summary>
    /// Scrive menu.tsv (le voci, prese dal Catalogo) e registra il pacchetto con la cartella dell'app come posto dei
    /// file. Restituisce null se è andata, se no il perché.
    /// </summary>
    public static async Task<string?> Registra(string cartella)
    {
        if (!Windows11) return "Serve Windows 11.";
        if (!Presente(cartella)) return "In questa versione manca il pacchetto del menu.";
        ScriviMenu(cartella);
        if (!Fidato(cartella)) return "Il certificato di DaProd non è ancora fidato su questo PC.";
        try
        {
            var opzioni = new AddPackageOptions
            {
                ExternalLocationUri = new Uri(cartella.TrimEnd('\\') + "\\"),
                ForceUpdateFromAnyVersion = true,
            };
            var r = await new PackageManager().AddPackageByUriAsync(new Uri(Path.Combine(cartella, Msix)), opzioni);
            if (r.ExtendedErrorCode is not null)
            {
                Registro.Scrivi($"Menu di Windows 11 non registrato: {r.ErrorText}");
                return r.ErrorText;
            }
            Registro.Scrivi($"Menu di Windows 11 registrato da {cartella}");
            return null;
        }
        catch (Exception e)
        {
            Registro.Errore("menu di Windows 11", e);
            return e.Message;
        }
    }

    public static async Task Rimuovi()
    {
        FermaSurrogato();
        foreach (var p in Pacchetti())
        {
            try { await new PackageManager().RemovePackageAsync(p.Id.FullName); Registro.Scrivi($"Menu di Windows 11 tolto ({p.Id.Version.Major}.{p.Id.Version.Minor}.{p.Id.Version.Build})"); }
            catch (Exception e) { Registro.Errore("togli menu di Windows 11", e); }
        }
    }

    /// <summary>
    /// La DLL gira dentro un dllhost (il "surrogato" COM) che la tiene aperta: prima di aggiornare o disinstallare si
    /// ferma, se no la cartella dell'app non si può sostituire. Si fermano solo i dllhost che hanno dentro la nostra DLL.
    /// </summary>
    public static void FermaSurrogato()
    {
        foreach (var p in Process.GetProcessesByName("dllhost"))
        {
            try
            {
                if (p.Modules.Cast<ProcessModule>().Any(m => string.Equals(m.ModuleName, Dll, StringComparison.OrdinalIgnoreCase)))
                {
                    p.Kill();
                    p.WaitForExit(2000);
                }
            }
            catch { /* processi d'altri o già chiusi */ }
            finally { p.Dispose(); }
        }
    }

    /// <summary>
    /// menu.tsv accanto alla DLL: le estensioni con la loro categoria e le voci del sottomenu, dal Catalogo.
    /// Una cosa sola, uguale ovunque: la DLL non sa niente dei formati, legge da qui.
    /// </summary>
    public static void ScriviMenu(string cartella)
    {
        var sb = new StringBuilder();
        sb.Append("T\tapri\tDaP Convertitore\n");
        sb.Append("T\tconverti\tConverti al volo\n");
        foreach (var est in Catalogo.TutteLeEstensioni.OrderBy(e => e))
            sb.Append($"E\t{est}\t{Catalogo.Chiave(Catalogo.CategoriaDi("x" + est))}\n");
        foreach (var r in Catalogo.Rapide)
            sb.Append($"V\t{Catalogo.Chiave(r.Categoria)}\t{r.Id}\t{r.Etichetta}\n");
        try { File.WriteAllText(Path.Combine(cartella, "menu.tsv"), sb.ToString(), new UTF8Encoding(false)); }
        catch (Exception e) { Registro.Errore("menu.tsv", e); }
    }
}
