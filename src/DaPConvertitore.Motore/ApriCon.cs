using System.Runtime.InteropServices;
using Microsoft.Win32;
using DaP.Convertitore.Lettore;

namespace DaP.Convertitore;

/// <summary>
/// Il lettore in «Apri con» di Windows e fra le app predefinite di Impostazioni, senza prendersi niente da solo:
/// Windows non lascia (giustamente) che un'app si faccia predefinita da sé. È Cammo che sceglie, una volta, da
/// «Apri con → Scegli un'altra app → Sempre» o da Impostazioni → App → App predefinite → DaP Convertitore.
///
/// Tutto in HKCU sotto nomi nostri:
///   Software\Classes\DaProd.Lettore.Video\shell\open\command   "exe" --guarda "%1"   (uno per gruppo: Video, Audio…)
///   Software\Classes\.mp4\OpenWithProgids\DaProd.Lettore.Video
///   Software\Classes\Applications\DaPConvertitore.exe           (nome e tipi supportati)
///   Software\DaProd\Convertitore\Capabilities + Software\RegisteredApplications   (la pagina in App predefinite)
///
/// E le icone e le miniature (dalla 1.2.0): ogni gruppo ha la sua icona (un .ico disegnato dall'app in
/// %LocalAppData%\DaProd\Convertitore\icone) e i file hanno la nostra miniatura di Esplora file:
///   Software\Classes\CLSID\{B7E3D9A1-…}\InprocServer32          DaPConvertitore.Miniature.dll (accanto all'exe)
///   Software\Classes\.mp4\ShellEx\{e357fccd-…}                  ← quel CLSID (video, musica e modelli: sempre;
///                                                                  il resto solo dove Windows non ne ha già una)
/// </summary>
public static class ApriCon
{
    public const string NomeApp = "DaP Convertitore";
    const string Classi = @"Software\Classes";
    const string Capacita = @"Software\DaProd\Convertitore\Capabilities";
    const string Originali = @"Software\DaProd\Convertitore\MiniatureOriginali";

    public const string DllMiniature = "DaPConvertitore.Miniature.dll";
    public const string ClsidMiniature = "{B7E3D9A1-4C52-4F08-9A6D-3E1F5C8B2D74}";
    const string ChiaveMiniatura = "{e357fccd-a995-4576-b01f-234630154e96}"; // IThumbnailProvider

    static string ProgId(string gruppo) => $"DaProd.Lettore.{gruppo}";

    static readonly Dictionary<string, string> nomiGruppo = new()
    {
        ["Video"] = "Video", ["Audio"] = "Audio", ["Immagine"] = "Immagine", ["Pdf"] = "Documento PDF",
        ["Documento"] = "Documento", ["Presentazione"] = "Presentazione", ["Tabella"] = "Tabella", ["Archivio"] = "Archivio",
        ["Font"] = "Carattere", ["Modello"] = "Modello 3D", ["Testo"] = "Testo", ["Codice"] = "Codice", ["Sottotitoli"] = "Sottotitoli",
    };

    public static IEnumerable<string> Gruppi => nomiGruppo.Keys;

    /// <summary>
    /// Chi disegna le icone dei gruppi (l'app, con WPF): riceve la cartella e il gruppo e restituisce il file .ico.
    /// Senza (nelle prove) tutti i tipi hanno l'icona dell'app.
    /// </summary>
    public static Func<string, string, string?>? FaiIcona { get; set; }

    public static void Registra(string exe)
    {
        var icona = $"\"{exe}\",0";
        var comando = $"\"{exe}\" --guarda \"%1\"";
        var icone = DisegnaIcone();
        using var classi = Registry.CurrentUser.CreateSubKey(Classi);
        foreach (var (gruppo, nome) in nomiGruppo)
        {
            using var k = classi.CreateSubKey(ProgId(gruppo));
            k.SetValue(null, $"{nome} · {NomeApp}");
            k.SetValue("FriendlyTypeName", $"{nome} · {NomeApp}");
            using (var di = k.CreateSubKey("DefaultIcon")) di.SetValue(null, icone.TryGetValue(gruppo, out var ico) ? ico : icona);
            using (var sh = k.CreateSubKey(@"shell\open"))
            {
                sh.SetValue("FriendlyAppName", NomeApp);
                using var c = sh.CreateSubKey("command");
                c.SetValue(null, comando);
            }
        }

        using (var app = classi.CreateSubKey(@"Applications\DaPConvertitore.exe"))
        {
            app.SetValue("FriendlyAppName", NomeApp);
            using (var c = app.CreateSubKey(@"shell\open\command")) c.SetValue(null, comando);
            using var tipi = app.CreateSubKey("SupportedTypes");
            foreach (var est in Vista.Estensioni) tipi.SetValue(est, "");
        }

        using (var cap = Registry.CurrentUser.CreateSubKey(Capacita))
        {
            cap.SetValue("ApplicationName", NomeApp);
            cap.SetValue("ApplicationDescription", "Guarda e converte qualsiasi file: video, musica, foto, PDF, documenti, archivi.");
            cap.SetValue("ApplicationIcon", icona);
            cap.DeleteSubKeyTree("FileAssociations", false);
            using var fa = cap.CreateSubKey("FileAssociations");
            foreach (var est in Vista.Estensioni)
            {
                var id = ProgId(Vista.Gruppo(est));
                fa.SetValue(est, id);
                using var owp = classi.CreateSubKey($@"{est}\OpenWithProgids");
                owp.SetValue(id, Array.Empty<byte>(), RegistryValueKind.None);
                // i gruppi cambiano da una versione all'altra (la 1.2.0 ha separato codice e presentazioni): niente doppioni
                foreach (var altro in nomiGruppo.Keys)
                    if (ProgId(altro) != id) owp.DeleteValue(ProgId(altro), false);
            }
        }
        using (var reg = Registry.CurrentUser.CreateSubKey(@"Software\RegisteredApplications"))
            reg.SetValue(NomeApp, Capacita);

        try { RegistraMiniature(exe, classi); }
        catch (Exception e) { Registro.Errore("miniature", e); }

        Aggiorna();
        Registro.Scrivi($"«Apri con» registrato per {exe}");
    }

    public static void Rimuovi()
    {
        using var classi = Registry.CurrentUser.OpenSubKey(Classi, true);
        if (classi is not null)
        {
            foreach (var gruppo in nomiGruppo.Keys)
                classi.DeleteSubKeyTree(ProgId(gruppo), false);
            using (var originali = Registry.CurrentUser.OpenSubKey(Originali))
                foreach (var est in Vista.Estensioni)
                {
                    using (var owp = classi.OpenSubKey($@"{est}\OpenWithProgids", true))
                        if (owp is not null) foreach (var gruppo in nomiGruppo.Keys) owp.DeleteValue(ProgId(gruppo), false);
                    TogliMiniatura(classi, originali, est);
                }
            classi.DeleteSubKeyTree(@"Applications\DaPConvertitore.exe", false);
            classi.DeleteSubKeyTree($@"CLSID\{ClsidMiniature}", false);
        }
        using (var ap = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Shell Extensions\Approved", true))
            ap?.DeleteValue(ClsidMiniature, false);
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\DaProd\Convertitore", false);
        try { Directory.Delete(Path.Combine(Strumenti.CartellaDati, "miniature"), true); } catch { /* Esplora file la tiene aperta: resta, è piccola */ }
        using (var reg = Registry.CurrentUser.OpenSubKey(@"Software\RegisteredApplications", true))
            reg?.DeleteValue(NomeApp, false);
        Aggiorna();
        Registro.Scrivi("«Apri con» tolto");
    }

    public static bool Registrato()
    {
        using var k = Registry.CurrentUser.OpenSubKey(Capacita);
        return k is not null;
    }

    // ——— le icone ———

    /// <summary>Le icone dei gruppi (gruppo → percorso del .ico). Quelle delle versioni vecchie si buttano.</summary>
    static Dictionary<string, string> DisegnaIcone()
    {
        var risultato = new Dictionary<string, string>();
        if (FaiIcona is null) return risultato;
        var cartella = Path.Combine(Strumenti.CartellaDati, "icone");
        try
        {
            foreach (var gruppo in nomiGruppo.Keys)
                if (FaiIcona(cartella, gruppo) is { } p && File.Exists(p)) risultato[gruppo] = p;
            var attuali = risultato.Values.ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var vecchio in Directory.EnumerateFiles(cartella, "*.ico").Where(f => !attuali.Contains(f)))
                try { File.Delete(vecchio); } catch { }
        }
        catch (Exception e) { Registro.Errore("icone dei tipi", e); }
        return risultato;
    }

    // ——— le miniature ———

    static string? Valore(RegistryKey? k) => k?.GetValue(null) as string;

    static bool Nostra(string? clsid) => clsid is not null && clsid.Equals(ClsidMiniature, StringComparison.OrdinalIgnoreCase);

    /// <summary>Windows ha già una miniatura per questa estensione (sua, di Office, di un altro programma)?</summary>
    static bool HaMiniatura(string est)
    {
        static bool Altrui(string percorso)
        {
            using var k = Registry.ClassesRoot.OpenSubKey(percorso + @"\ShellEx\" + ChiaveMiniatura);
            return Valore(k) is { Length: > 0 } v && !Nostra(v);
        }
        if (Altrui(est) || Altrui(@"SystemFileAssociations\" + est)) return true;
        using var chiave = Registry.ClassesRoot.OpenSubKey(est);
        if (Valore(chiave) is { Length: > 0 } progid && !progid.StartsWith("DaProd.Lettore.", StringComparison.Ordinal) && Altrui(progid)) return true;
        if (chiave?.GetValue("PerceivedType") is string tipo && Altrui(@"SystemFileAssociations\" + tipo)) return true;
        using var scelta = Registry.CurrentUser.OpenSubKey($@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\{est}\UserChoice");
        if (scelta?.GetValue("ProgId") is string pid && !pid.StartsWith("DaProd.Lettore.", StringComparison.Ordinal) && Altrui(pid)) return true;
        return false;
    }

    /// <summary>
    /// La DLL gira dentro Esplora file e lo tiene bloccato finché non riparte: per questo non la si usa dalla cartella
    /// dell'app (che un aggiornamento deve poter sostituire) ma da una copia in %LocalAppData%\DaProd\Convertitore\miniature    /// «versione-impronta», una cartella per ogni DLL diversa. Le copie vecchie si buttano, se non sono più in uso.
    /// </summary>
    static string? CopiaDll(string dll)
    {
        try
        {
            var impronta = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(dll)))[..8].ToLowerInvariant();
            var radice = Path.Combine(Strumenti.CartellaDati, "miniature");
            var cartella = Path.Combine(radice, impronta);
            var copia = Path.Combine(cartella, DllMiniature);
            if (!File.Exists(copia))
            {
                Directory.CreateDirectory(cartella);
                File.Copy(dll, copia, true);
            }
            foreach (var vecchia in Directory.EnumerateDirectories(radice).Where(d => !string.Equals(Path.GetFileName(d), impronta, StringComparison.OrdinalIgnoreCase)))
                try { Directory.Delete(vecchia, true); } catch { /* ancora in uso da Esplora file: la prossima volta */ }
            return copia;
        }
        catch (Exception e)
        {
            Registro.Errore("copia della DLL delle miniature", e);
            return null;
        }
    }

    static void RegistraMiniature(string exe, RegistryKey classi)
    {
        var dll = Path.Combine(Path.GetDirectoryName(exe)!, DllMiniature);
        if (!File.Exists(dll)) { Registro.Scrivi($"Miniature: manca {DllMiniature}, niente anteprime"); return; }
        var copia = CopiaDll(dll);
        if (copia is null) return;

        using (var k = classi.CreateSubKey($@"CLSID\{ClsidMiniature}"))
        {
            k.SetValue(null, NomeApp + " · miniature");
            // senza questo Windows ci darebbe solo un flusso di byte, senza il percorso del file: a FFmpeg il percorso serve
            k.SetValue("DisableProcessIsolation", 1, RegistryValueKind.DWord);
            using var ips = k.CreateSubKey("InprocServer32");
            ips.SetValue(null, copia);
            ips.SetValue("ThreadingModel", "Both");
        }
        // dove sta l'exe, per la DLL (che gira da una copia)
        using (var radice = Registry.CurrentUser.CreateSubKey(@"Software\DaProd\Convertitore")) radice.SetValue("Exe", exe);
        using (var ap = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Shell Extensions\Approved"))
            ap.SetValue(ClsidMiniature, NomeApp + " · miniature");

        using var originali = Registry.CurrentUser.CreateSubKey(Originali);
        var messe = 0;
        foreach (var est in Vista.Estensioni)
        {
            if (!Vista.MiniaturaSempre(est) && HaMiniatura(est))
            {
                // Windows (o un altro programma) la sa già fare: la sua resta, e la nostra, se c'era, si toglie
                TogliMiniatura(classi, originali, est);
                continue;
            }
            using var k = classi.CreateSubKey($@"{est}\ShellEx\{ChiaveMiniatura}");
            var prima = Valore(k);
            if (prima is { Length: > 0 } && !Nostra(prima) && originali.GetValue(est) is null)
                originali.SetValue(est, prima);
            k.SetValue(null, ClsidMiniature);
            messe++;
        }
        foreach (var gruppo in nomiGruppo.Keys)
        {
            using var k = classi.CreateSubKey($@"{ProgId(gruppo)}\ShellEx\{ChiaveMiniatura}");
            k.SetValue(null, ClsidMiniature);
        }
        Registro.Scrivi($"Miniature registrate per {messe} estensioni");
    }

    /// <summary>Toglie la nostra miniatura da un'estensione e rimette quella che c'era prima, se c'era.</summary>
    static void TogliMiniatura(RegistryKey classi, RegistryKey? originali, string est)
    {
        using (var k = classi.OpenSubKey($@"{est}\ShellEx\{ChiaveMiniatura}"))
            if (k is null || !Nostra(Valore(k))) return;
        if (originali?.GetValue(est) is string prima)
        {
            using var k = classi.CreateSubKey($@"{est}\ShellEx\{ChiaveMiniatura}");
            k.SetValue(null, prima);
        }
        else classi.DeleteSubKeyTree($@"{est}\ShellEx\{ChiaveMiniatura}", false);
    }

    /// <summary>
    /// Windows tiene le miniature e le icone già disegnate in file di cache e non si accorge che sono cambiate: si
    /// buttano. Va fatto con Esplora file fermo (i file sono aperti), quindi lo chiama il riavvio di Esplora file.
    /// </summary>
    public static void SvuotaCache()
    {
        try
        {
            var cartella = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Explorer");
            foreach (var f in Directory.EnumerateFiles(cartella).Where(f => Path.GetFileName(f).StartsWith("thumbcache_", StringComparison.OrdinalIgnoreCase)
                                                                     || Path.GetFileName(f).StartsWith("iconcache_", StringComparison.OrdinalIgnoreCase)))
                try { File.Delete(f); } catch { /* ancora aperto: pazienza */ }
        }
        catch { /* niente cartella, niente cache */ }
    }

    [DllImport("shell32.dll")]
    static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);

    static void Aggiorna()
    {
        try { SHChangeNotify(0x08000000, 0x1000, IntPtr.Zero, IntPtr.Zero); } catch { }
    }
}
