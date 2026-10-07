namespace DaP.Convertitore;

/// <summary>
/// Il quaderno dell'app: ogni comando lanciato e ogni errore, in registro.txt nella cartella dati.
/// Quando passa 2 MB si ricomincia (il vecchio resta come registro-prima.txt).
/// </summary>
public static class Registro
{
    static readonly object chiave = new();
    public static string Percorso => Path.Combine(Strumenti.CartellaDati, "registro.txt");

    public static void Scrivi(string riga)
    {
        try
        {
            lock (chiave)
            {
                var p = Percorso;
                if (File.Exists(p) && new FileInfo(p).Length > 2_000_000)
                    File.Move(p, Path.Combine(Strumenti.CartellaDati, "registro-prima.txt"), true);
                File.AppendAllText(p, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {riga}{Environment.NewLine}");
            }
        }
        catch { /* il registro non deve mai fermare una conversione */ }
    }

    public static void Errore(string dove, Exception e) => Scrivi($"ERRORE {dove}: {e}");
}
