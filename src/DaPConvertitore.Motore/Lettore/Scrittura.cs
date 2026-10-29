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
        var t = Codifiche.Leggi(percorso);
        // l'ANSI si riscrive in UTF-8; UTF-8 e UTF-16 restano come sono, col BOM se c'era
        Encoding codifica = t.Vecchia ? new UTF8Encoding(false) : t.Encoding switch
        {
            UTF8Encoding => new UTF8Encoding(t.Bom),
            UnicodeEncoding u => new UnicodeEncoding(u.CodePage == 1201, t.Bom),
            UTF32Encoding => new UTF32Encoding(false, t.Bom),
            var e => e,
        };
        return new Formato(codifica, t.Bom, t.Testo.Contains("\r\n"), t.Vecchia);
    }

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
    /// (come «Estrai qui», ma di quello che hai scelto, con 7-Zip). Le voci sono i percorsi dentro l'archivio, con «/», e le
    /// cartelle con «/» in fondo.
    /// </summary>
    public static async Task<string> EstraiVoci(Strumenti s, string archivio, IReadOnlyList<string> voci)
    {
        if (voci.Count == 0) throw new ErroreConversione("Non hai scelto niente da estrarre.");
        var dest = Nomi.PrenotaCartella(archivio);
        try
        {
            Directory.CreateDirectory(dest);
            await SetteZip.Estrai(s, archivio, dest, voci, CancellationToken.None);
            if (!Directory.EnumerateFileSystemEntries(dest).Any())
                throw new ErroreConversione("Non ho estratto niente: forse le voci scelte non ci sono più.");
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
