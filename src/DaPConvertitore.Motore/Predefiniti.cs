using System.ComponentModel;
using System.Diagnostics;
using System.Security;
using System.Text;
using Microsoft.Win32;

namespace DaP.Convertitore;

/// <summary>
/// «Aprire tutto con DaProd»: Windows non lascia che un'app si faccia predefinita da sola (la scelta dell'utente è
/// protetta), ma un amministratore può dargli un file di associazioni, il modo che Windows prevede per i computer
/// aziendali: la regola «Imposta un file di configurazione delle associazioni predefinite»
/// (HKLM\SOFTWARE\Policies\Microsoft\Windows\System\DefaultAssociationsConfiguration). Windows lo applica a ogni
/// accesso, quindi dopo OK ci si disconnette e si riaccede. Serve il permesso di amministratore, una volta: l'app si
/// rilancia con la finestra di Windows (UAC) e fa solo questo. Non c'è niente di nascosto: «Togli la regola» la cancella.
/// Sulle edizioni Home Windows ignora la regola: lì resta la pagina delle impostazioni.
/// </summary>
public static class Predefiniti
{
    const string ChiavePolicy = @"SOFTWARE\Policies\Microsoft\Windows\System";
    const string Valore = "DefaultAssociationsConfiguration";

    public const int Fatto = 0, Errore = 1, GiaUsata = 2;

    public static string FileRegola => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "DaProd", "Convertitore", "associazioni.xml");

    /// <summary>La regola vale su Windows Pro, Enterprise ed Education; non su Home.</summary>
    public static bool Supportata()
    {
        using var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
        var edizione = k?.GetValue("EditionID") as string ?? "";
        return !edizione.StartsWith("Core", StringComparison.OrdinalIgnoreCase);
    }

    static string? Corrente()
    {
        using var k = Registry.LocalMachine.OpenSubKey(ChiavePolicy);
        return k?.GetValue(Valore) as string;
    }

    /// <summary>La regola c'è ed è la nostra.</summary>
    public static bool Attiva() => string.Equals(Corrente(), FileRegola, StringComparison.OrdinalIgnoreCase);

    /// <summary>Un'altra regola di associazioni già in vigore (un'azienda, un altro programma): non la si tocca.</summary>
    public static bool DiAltri() => Corrente() is { Length: > 0 } c && !string.Equals(c, FileRegola, StringComparison.OrdinalIgnoreCase);

    /// <summary>Il file delle associazioni: ogni estensione dei gruppi scelti col suo programma.</summary>
    public static string Xml(IEnumerable<string> gruppi)
    {
        var scelti = gruppi.ToHashSet(StringComparer.Ordinal);
        var sb = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n<DefaultAssociations>\r\n");
        foreach (var est in Lettore.Vista.Estensioni)
        {
            var g = Lettore.Vista.Gruppo(est);
            if (!scelti.Contains(g)) continue;
            sb.Append($"  <Association Identifier=\"{SecurityElement.Escape(est)}\" ProgId=\"{ApriCon.ProgIdDi(g)}\" ApplicationName=\"{ApriCon.NomeApp}\" />\r\n");
        }
        return sb.Append("</DefaultAssociations>\r\n").ToString();
    }

    // ——— quello che gira con i permessi di amministratore (l'app rilanciata con «runas») ———

    public static int ApplicaDaAdmin(string xmlTemporaneo)
    {
        try
        {
            if (DiAltri()) return GiaUsata;
            Directory.CreateDirectory(Path.GetDirectoryName(FileRegola)!);
            File.Copy(xmlTemporaneo, FileRegola, true);
            using var k = Registry.LocalMachine.CreateSubKey(ChiavePolicy);
            k.SetValue(Valore, FileRegola, RegistryValueKind.String);
            Registro.Scrivi($"Regola delle app predefinite scritta: {FileRegola}");
            return Fatto;
        }
        catch (Exception e) { Registro.Errore("regola delle app predefinite", e); return Errore; }
    }

    public static int TogliDaAdmin()
    {
        try
        {
            if (Attiva())
                using (var k = Registry.LocalMachine.OpenSubKey(ChiavePolicy, true)) k?.DeleteValue(Valore, false);
            if (File.Exists(FileRegola)) File.Delete(FileRegola);
            Registro.Scrivi("Regola delle app predefinite tolta");
            return Fatto;
        }
        catch (Exception e) { Registro.Errore("togli la regola delle app predefinite", e); return Errore; }
    }

    // ——— quello che chiede l'app normale ———

    /// <summary>
    /// Rilancia l'app con la richiesta di amministratore e aspetta. Restituisce il codice (Fatto, Errore, GiaUsata) o
    /// null se non si è dato il permesso.
    /// </summary>
    public static async Task<int?> ChiediAdmin(string exe, string argomenti)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo(exe, argomenti) { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden });
            if (p is null) return Errore;
            await p.WaitForExitAsync();
            return p.ExitCode;
        }
        catch (Win32Exception e) when (e.NativeErrorCode == 1223) { return null; } // la finestra del permesso è stata chiusa con «No»
    }
}
