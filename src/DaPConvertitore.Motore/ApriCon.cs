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
/// </summary>
public static class ApriCon
{
    public const string NomeApp = "DaP Convertitore";
    const string Classi = @"Software\Classes";
    const string Capacita = @"Software\DaProd\Convertitore\Capabilities";

    static string ProgId(string gruppo) => $"DaProd.Lettore.{gruppo}";

    static readonly Dictionary<string, string> nomiGruppo = new()
    {
        ["Video"] = "Video", ["Audio"] = "Audio", ["Immagine"] = "Immagine", ["Pdf"] = "Documento PDF",
        ["Documento"] = "Documento", ["Tabella"] = "Tabella", ["Archivio"] = "Archivio", ["Font"] = "Carattere",
        ["Modello"] = "Modello 3D", ["Testo"] = "Testo",
    };

    public static void Registra(string exe)
    {
        var icona = $"\"{exe}\",0";
        var comando = $"\"{exe}\" --guarda \"%1\"";
        using var classi = Registry.CurrentUser.CreateSubKey(Classi);
        foreach (var (gruppo, nome) in nomiGruppo)
        {
            using var k = classi.CreateSubKey(ProgId(gruppo));
            k.SetValue(null, $"{nome} · {NomeApp}");
            k.SetValue("FriendlyTypeName", $"{nome} · {NomeApp}");
            using (var di = k.CreateSubKey("DefaultIcon")) di.SetValue(null, icona);
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
            }
        }
        using (var reg = Registry.CurrentUser.CreateSubKey(@"Software\RegisteredApplications"))
            reg.SetValue(NomeApp, Capacita);

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
            foreach (var est in Vista.Estensioni)
            {
                using var owp = classi.OpenSubKey($@"{est}\OpenWithProgids", true);
                if (owp is null) continue;
                foreach (var gruppo in nomiGruppo.Keys) owp.DeleteValue(ProgId(gruppo), false);
            }
            classi.DeleteSubKeyTree(@"Applications\DaPConvertitore.exe", false);
        }
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\DaProd\Convertitore", false);
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

    [DllImport("shell32.dll")]
    static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);

    static void Aggiorna()
    {
        try { SHChangeNotify(0x08000000, 0x1000, IntPtr.Zero, IntPtr.Zero); } catch { }
    }
}
