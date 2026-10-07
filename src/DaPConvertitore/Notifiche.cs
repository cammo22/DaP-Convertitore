using System.Security;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace DaP.Convertitore.App;

/// <summary>
/// Le notifiche di Windows a fine lavoro, quando la finestra non è davanti. I bottoni usano il protocollo
/// dap-convertitore: (registrato col menu), così funzionano anche ad app chiusa.
/// </summary>
public static class Notifiche
{
    public static void Fatto(IReadOnlyList<Lavoro> ok)
    {
        if (ok.Count == 0) return;
        var ultimo = ok[^1];
        var nome = Path.GetFileName(ultimo.Uscita ?? "");
        var titolo = ok.Count == 1 ? "Fatto!" : $"Fatto! {ok.Count} file convertiti";
        var prima = ok.Sum(l => l.PesoPrima);
        var dopo = ok.Sum(l => l.PesoDopo ?? 0);
        var riga = ok.Count == 1 ? nome : $"L'ultimo: {nome}";
        var cestino = ok.Any(l => l.NelCestino) ? (ok.Count == 1 ? " · l'originale è nel Cestino" : " · gli originali sono nel Cestino") : "";
        Mostra(titolo, riga, $"{Peso(prima)} → {Peso(dopo)}{cestino}", ultimo.Uscita);
    }

    public static void Errore(Lavoro l) =>
        Mostra("Non ce l'ho fatta", Path.GetFileName(l.Sorgente), l.Errore ?? "", null);

    static void Mostra(string titolo, string riga1, string riga2, string? percorso)
    {
        try
        {
            string E(string s) => SecurityElement.Escape(s) ?? "";
            var azioni = percorso is null ? "" : $"""
                <actions>
                  <action content="Apri" activationType="protocol" arguments="{E(Url("apri", percorso))}"/>
                  <action content="Mostra nella cartella" activationType="protocol" arguments="{E(Url("mostra", percorso))}"/>
                </actions>
                """;
            var lancio = percorso is null ? "" : $" activationType=\"protocol\" launch=\"{E(Url("mostra", percorso))}\"";
            var xml = $"""
                <toast{lancio}>
                  <visual><binding template="ToastGeneric">
                    <text>{E(titolo)}</text>
                    <text>{E(riga1)}</text>
                    <text>{E(riga2)}</text>
                  </binding></visual>
                  {azioni}
                </toast>
                """;
            var doc = new XmlDocument();
            doc.LoadXml(xml);
            ToastNotificationManager.CreateToastNotifier(MenuContestuale.Aumid).Show(new ToastNotification(doc));
        }
        catch (Exception e) { Registro.Scrivi($"Notifica non mostrata: {e.Message}"); }
    }

    static string Url(string cosa, string percorso) => $"{MenuContestuale.Protocollo}:{cosa}?p={Uri.EscapeDataString(percorso)}";

    static string Peso(long b)
    {
        string[] u = ["byte", "KB", "MB", "GB", "TB"];
        double v = b;
        var i = 0;
        while (v >= 1000 && i < u.Length - 1) { v /= 1024; i++; }
        return $"{v.ToString(i == 0 ? "0" : v < 10 ? "0.00" : v < 100 ? "0.0" : "0")} {u[i]}";
    }
}
