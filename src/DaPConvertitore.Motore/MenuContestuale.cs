using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace DaP.Convertitore;

/// <summary>
/// La voce "DaP Convertitore" nel tasto destro di Esplora file, col sottomenu delle conversioni al volo.
/// Tutto sta in HKCU (niente permessi di amministratore) e sotto nomi nostri: togliendolo non si tocca altro.
///
/// Come è fatto: per ogni estensione che sappiamo convertire,
///   HKCU\Software\Classes\SystemFileAssociations\.mp4\shell\DaPConvertitore  (MUIVerb, Icon, ExtendedSubCommandsKey)
/// e il sottomenu della sua categoria sta una volta sola in
///   HKCU\Software\Classes\DaPConvertitore.Menu.video\shell\...\command
/// Per le cartelle c'è Directory\shell. Più "Invia a", il protocollo dap-convertitore: (i bottoni delle notifiche)
/// e il nome dell'app per le notifiche (AppUserModelId).
///
/// Su Windows 11 queste voci stanno sotto «Mostra altre opzioni» (o Maiusc + tasto destro): il menu nuovo vuole un
/// pacchetto firmato con una DLL apposta.
/// </summary>
public static class MenuContestuale
{
    public const string Verbo = "DaPConvertitore";
    public const string Aumid = "DaProd.Convertitore";
    public const string Protocollo = "dap-convertitore";
    const string Classi = @"Software\Classes";

    static string Sottomenu(Categoria c) => $"{Verbo}.Menu.{Catalogo.Chiave(c)}";

    static readonly Categoria[] conMenu =
    [
        Categoria.Video, Categoria.Audio, Categoria.Immagine, Categoria.Pdf, Categoria.Documento, Categoria.Foglio,
        Categoria.Presentazione, Categoria.Testo, Categoria.Dati, Categoria.Sottotitoli, Categoria.Archivio,
    ];

    public static void Registra(string exe, string? iconaPng = null)
    {
        var icona = $"\"{exe}\",0";
        using var classi = Registry.CurrentUser.CreateSubKey(Classi);

        // i sottomenu, uno per categoria
        foreach (var c in conMenu.Append(Categoria.Cartella))
        {
            classi.DeleteSubKeyTree(Sottomenu(c), false);
            using var shell = classi.CreateSubKey($@"{Sottomenu(c)}\shell");
            var n = 0;
            Voce(shell, $"{n++:00}apri", c == Categoria.Cartella ? "Apri nel convertitore…" : "Scegli tu…  formato, peso, qualità", $"\"{exe}\" --apri \"%1\"", icona, separatoreDopo: true);
            foreach (var r in Catalogo.Rapide.Where(r => r.Categoria == c))
                Voce(shell, $"{n++:00}{r.Id}", r.Etichetta, $"\"{exe}\" --azione {r.Id} \"%1\"", null);
        }

        // la voce principale su ogni estensione
        foreach (var c in conMenu)
            foreach (var est in Catalogo.EstensioniDi(c))
                Principale(classi, $@"SystemFileAssociations\{est}\shell\{Verbo}", Sottomenu(c), icona);
        Principale(classi, $@"Directory\shell\{Verbo}", Sottomenu(Categoria.Cartella), icona);

        // dap-convertitore:mostra?… → i bottoni delle notifiche
        using (var p = classi.CreateSubKey(Protocollo))
        {
            p.SetValue(null, "URL:DaP Convertitore");
            p.SetValue("URL Protocol", "");
            using var di = p.CreateSubKey("DefaultIcon");
            di.SetValue(null, icona);
            using var cmd = p.CreateSubKey(@"shell\open\command");
            cmd.SetValue(null, $"\"{exe}\" --url \"%1\"");
        }

        // il nome e l'icona che Windows mostra sulle notifiche
        using (var a = classi.CreateSubKey($@"AppUserModelId\{Aumid}"))
        {
            a.SetValue("DisplayName", "DaP Convertitore");
            if (iconaPng is not null) a.SetValue("IconUri", iconaPng);
            a.SetValue("IconBackgroundColor", "FF150E24");
        }

        InviaA(exe, true);
        Aggiorna();
        Registro.Scrivi($"Menu del tasto destro registrato per {exe}");
    }

    public static void Rimuovi()
    {
        using var classi = Registry.CurrentUser.OpenSubKey(Classi, true);
        if (classi is null) return;
        foreach (var c in conMenu.Append(Categoria.Cartella))
            classi.DeleteSubKeyTree(Sottomenu(c), false);
        foreach (var est in Catalogo.TutteLeEstensioni)
            classi.DeleteSubKeyTree($@"SystemFileAssociations\{est}\shell\{Verbo}", false);
        classi.DeleteSubKeyTree($@"Directory\shell\{Verbo}", false);
        classi.DeleteSubKeyTree(Protocollo, false);
        classi.DeleteSubKeyTree($@"AppUserModelId\{Aumid}", false);
        InviaA("", false);
        Aggiorna();
        Registro.Scrivi("Menu del tasto destro tolto");
    }

    public static bool Registrato()
    {
        using var k = Registry.CurrentUser.OpenSubKey($@"{Classi}\SystemFileAssociations\.mp4\shell\{Verbo}");
        return k is not null;
    }

    static void Principale(RegistryKey classi, string percorso, string sottomenu, string icona)
    {
        using var k = classi.CreateSubKey(percorso);
        k.SetValue("MUIVerb", "DaP Convertitore");
        k.SetValue("Icon", icona);
        k.SetValue("ExtendedSubCommandsKey", sottomenu);
        k.SetValue("MultiSelectModel", "Player");
        k.DeleteValue("SubCommands", false);
    }

    static void Voce(RegistryKey shell, string nome, string etichetta, string comando, string? icona, bool separatoreDopo = false)
    {
        using var k = shell.CreateSubKey(nome);
        k.SetValue("MUIVerb", etichetta);
        k.SetValue("MultiSelectModel", "Player");
        if (icona is not null) k.SetValue("Icon", icona);
        if (separatoreDopo) k.SetValue("CommandFlags", 0x40, RegistryValueKind.DWord);
        using var c = k.CreateSubKey("command");
        c.SetValue(null, comando);
    }

    /// <summary>"Invia a → DaP Convertitore": passa tutti i file in un colpo solo, anche quelli che il menu non conosce.</summary>
    static void InviaA(string exe, bool metti)
    {
        try
        {
            var lnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.SendTo), "DaP Convertitore.lnk");
            if (!metti) { if (File.Exists(lnk)) File.Delete(lnk); return; }
            var tipo = Type.GetTypeFromProgID("WScript.Shell");
            if (tipo is null) return;
            dynamic shell = Activator.CreateInstance(tipo)!;
            dynamic s = shell.CreateShortcut(lnk);
            s.TargetPath = exe;
            s.IconLocation = exe + ",0";
            s.Description = "Converti con DaP Convertitore";
            s.Save();
        }
        catch (Exception e) { Registro.Errore("Invia a", e); }
    }

    [DllImport("shell32.dll")]
    static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);

    /// <summary>Dice a Esplora file di rileggere le associazioni, così la voce compare subito.</summary>
    static void Aggiorna()
    {
        try { SHChangeNotify(0x08000000, 0x1000, IntPtr.Zero, IntPtr.Zero); } catch { }
    }
}
