using System.Text;

namespace DaP.Convertitore.Lettore;

/// <summary>
/// Scrivere: il testo modificato nel lettore torna nel file con la sua codifica, col BOM se c'era, e con le sue fini
/// riga (CRLF o LF). E l'estrazione di quello che si sceglie dentro un archivio, non di tutto.
/// </summary>
public static class Scrittura
{
    /// <summary>Come è scritto un file di testo: la codifica, se c'è il BOM, se le righe finiscono in CRLF.
    /// «Vecchia» vuol dire che non è né UTF-8 né Unicode: si salva in UTF-8, e lo si dice.</summary>
    public sealed record Formato(Encoding Codifica, bool Bom, bool CrLf, bool Vecchia);

    public static Formato FormatoDi(string percorso)
    {
        var b = File.ReadAllBytes(percorso);
        if (b.Length >= 2 && b[0] == 0xFF && b[1] == 0xFE) return Riga(new UnicodeEncoding(false, true), true, b);
        if (b.Length >= 2 && b[0] == 0xFE && b[1] == 0xFF) return Riga(new UnicodeEncoding(true, true), true, b);
        if (b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF) return Riga(new UTF8Encoding(true), true, b);
        try
        {
            var testo = new UTF8Encoding(false, true).GetString(b);
            return new Formato(new UTF8Encoding(false), false, testo.Contains("\r\n"), false);
        }
        catch (DecoderFallbackException)
        {
            return new Formato(new UTF8Encoding(false), false, Encoding.Latin1.GetString(b).Contains("\r\n"), true);
        }
    }

    static Formato Riga(Encoding e, bool bom, byte[] b) => new(e, bom, e.GetString(b).Contains("\r\n"), false);

    /// <summary>
    /// Riscrive il file col testo del lettore, nella sua codifica e con le sue fini riga. La bozza sta accanto e prende
    /// il posto del file solo a scrittura finita: se qualcosa va storto il file resta com'era. Restituisce la nota per
    /// chi ha salvato, se c'è qualcosa da dire (codifica cambiata).
    /// </summary>
    public static string? SalvaTesto(string percorso, string testo)
    {
        var f = FormatoDi(percorso);
        var t = testo.Replace("\r\n", "\n");
        if (f.CrLf) t = t.Replace("\n", "\r\n");
        var codifica = f.Vecchia ? new UTF8Encoding(false) : f.Codifica;
        var pre = f.Bom && !f.Vecchia ? codifica.GetPreamble() : Array.Empty<byte>();
        var corpo = codifica.GetBytes(t);
        var tutto = new byte[pre.Length + corpo.Length];
        pre.CopyTo(tutto, 0);
        corpo.CopyTo(tutto, pre.Length);

        var creato = File.GetCreationTime(percorso);
        var bozza = Nomi.Bozza(percorso);
        try
        {
            File.WriteAllBytes(bozza, tutto);
            Modifiche.Sposta(bozza, percorso);
            File.SetCreationTime(percorso, creato);
        }
        finally { try { if (File.Exists(bozza)) File.Delete(bozza); } catch { } }
        return f.Vecchia ? "salvato in UTF-8: il file era in una codifica vecchia" : null;
    }

    /// <summary>
    /// Estrae solo le voci scelte (una cartella intera o dei file) in una cartella accanto all'archivio, col suo nome
    /// (come «Estrai qui», ma di quello che hai scelto). Le voci sono i percorsi dentro l'archivio, con «/», e le
    /// cartelle con «/» in fondo.
    /// </summary>
    public static async Task<string> EstraiVoci(Strumenti s, string archivio, IReadOnlyList<string> voci)
    {
        if (voci.Count == 0) throw new ErroreConversione("Non hai scelto niente da estrarre.");
        var dest = Nomi.PrenotaCartella(archivio);
        try
        {
            Directory.CreateDirectory(dest);
            var argomenti = new List<string> { "-xf", Path.GetFullPath(archivio), "-C", dest };
            argomenti.AddRange(voci.Select(v => v.Replace('\\', '/')));
            var r = await Processi.Esegui(s.Tar, argomenti, CancellationToken.None, tieniUscita: false);
            if (r.Codice != 0 || !Directory.EnumerateFileSystemEntries(dest).Any())
                throw new ErroreConversione("Non ho estratto niente: forse le voci scelte non ci sono più.", r.Errori);
            return dest;
        }
        catch
        {
            try { if (Directory.Exists(dest)) Directory.Delete(dest, true); } catch { }
            throw;
        }
        finally { Nomi.Libera(dest); }
    }
}
