using System.Text;

namespace DaP.Convertitore;

/// <summary>Un testo letto dal disco, con la codifica in cui era scritto.</summary>
/// <param name="Codifica">Il nome per le persone: «UTF-8», «UTF-16 LE», «Windows-1252 (ANSI)»…</param>
/// <param name="Bom">Il file comincia col BOM (EF BB BF, FF FE, FE FF).</param>
/// <param name="Vecchia">Non è né UTF-8 né Unicode: è un vecchio testo di Windows (ANSI).</param>
/// <param name="Troncato">Il file è più lungo di quanto si è letto.</param>
public sealed record TestoLetto(string Testo, Encoding Encoding, string Codifica, bool Bom, bool Vecchia, bool Troncato);

/// <summary>
/// Che codifica ha un file di testo. Un posto solo per il convertitore, il lettore, l'anteprima e chi salva: fino
/// alla 1.7 ognuno faceva a modo suo, e il convertitore leggeva l'UTF-16 (i TXT «Unicode» del Blocco note, quelli
/// che escono da PowerShell 5.1 con «&gt;») come byte a caso, e i vecchi TXT di Windows come Latin-1, che sbaglia
/// € e le virgolette curve.
/// </summary>
public static class Codifiche
{
    static Codifiche() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    /// <summary>La codepage dei vecchi TXT di Windows in Italia (e nell'Europa occidentale): Latin-1 con €, “ ” ‘ ’ – —.</summary>
    public static Encoding Ansi => Encoding.GetEncoding(1252);

    /// <summary>Legge il file (fino a <paramref name="max"/> byte) e lo decodifica.</summary>
    public static TestoLetto Leggi(string percorso, int max = int.MaxValue)
    {
        using var f = new FileStream(percorso, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var troncato = f.Length > max;
        var b = new byte[troncato ? max : f.Length];
        f.ReadExactly(b);
        return Decodifica(b, troncato);
    }

    /// <summary>
    /// I byte diventano testo. In ordine: il BOM, l'UTF-16 senza BOM (un byte zero ogni due), l'UTF-8 vero, e se
    /// non è niente di questo la codepage di Windows. Se il pezzo è troncato, l'ultimo carattere può essere a metà.
    /// </summary>
    public static TestoLetto Decodifica(byte[] b, bool troncato = false)
    {
        if (Inizia(b, 0xEF, 0xBB, 0xBF)) return Fai(new UTF8Encoding(false), "UTF-8", true, false, b, 3, troncato);
        if (Inizia(b, 0xFF, 0xFE, 0x00, 0x00) && b.Length % 4 == 0) return Fai(new UTF32Encoding(false, false), "UTF-32 LE", true, false, b, 4, troncato);
        if (Inizia(b, 0xFF, 0xFE)) return Fai(new UnicodeEncoding(false, false), "UTF-16 LE", true, false, b, 2, troncato);
        if (Inizia(b, 0xFE, 0xFF)) return Fai(new UnicodeEncoding(true, false), "UTF-16 BE", true, false, b, 2, troncato);
        if (Utf16SenzaBom(b) is { } be) return Fai(new UnicodeEncoding(be, false), be ? "UTF-16 BE" : "UTF-16 LE", false, false, b, 0, troncato);
        if (Utf8(b, troncato, out var taglio)) return Fai(new UTF8Encoding(false), "UTF-8", false, false, b[..^taglio], 0, troncato);
        return Fai(Ansi, "Windows-1252 (ANSI)", false, true, b, 0, troncato);
    }

    static TestoLetto Fai(Encoding e, string nome, bool bom, bool vecchia, byte[] b, int salta, bool troncato)
    {
        var n = b.Length - salta;
        // un carattere UTF-16 o UTF-32 tagliato a metà non si decodifica: si lascia fuori
        if (e is UnicodeEncoding) n -= n % 2;
        else if (e is UTF32Encoding) n -= n % 4;
        return new TestoLetto(e.GetString(b, salta, n), e, nome, bom, vecchia, troncato);
    }

    static bool Inizia(byte[] b, params byte[] inizio) => b.Length >= inizio.Length && b.AsSpan(0, inizio.Length).SequenceEqual(inizio);

    /// <summary>
    /// UTF-16 senza BOM (lo scrivono certi programmi e le esportazioni di Excel «Testo Unicode» passate da altre
    /// mani): nel testo occidentale un byte su due è zero, sempre dalla stessa parte. Si guardano i primi 4 KB.
    /// </summary>
    static bool? Utf16SenzaBom(byte[] b)
    {
        var n = Math.Min(b.Length, 4096) & ~1;
        if (n < 4) return null;
        int zeriPari = 0, zeriDispari = 0;
        for (var i = 0; i < n; i += 2)
        {
            if (b[i] == 0) zeriPari++;
            if (b[i + 1] == 0) zeriDispari++;
        }
        var coppie = n / 2;
        bool? be = zeriDispari > coppie * 0.4 && zeriPari < coppie * 0.05 ? false // 'c' 00 'i' 00: little endian
                 : zeriPari > coppie * 0.4 && zeriDispari < coppie * 0.05 ? true : null;
        if (be is null) return null;
        // i numeri a 16 bit di un file binario hanno anche loro tanti zeri: nel testo vero, dove il byte alto è
        // zero, il basso è una lettera, una cifra o un a capo
        int ascii = 0, leggibili = 0;
        for (var i = 0; i < n; i += 2)
        {
            var (alto, basso) = be.Value ? (b[i], b[i + 1]) : (b[i + 1], b[i]);
            if (alto != 0) continue;
            ascii++;
            if (basso >= 0x20 || basso is 0x09 or 0x0A or 0x0D) leggibili++;
        }
        return leggibili >= ascii * 0.95 ? be : null;
    }

    /// <summary>È UTF-8 valido? Se il pezzo è troncato, gli ultimi 1-3 byte possono essere un carattere a metà.</summary>
    static bool Utf8(byte[] b, bool troncato, out int taglio)
    {
        var dec = new UTF8Encoding(false, true);
        for (taglio = 0; taglio <= (troncato ? 3 : 0) && taglio <= b.Length; taglio++)
        {
            try { dec.GetCharCount(b, 0, b.Length - taglio); return true; }
            catch (DecoderFallbackException) { }
        }
        taglio = 0;
        return false;
    }

    /// <summary>
    /// Un file senza estensione nota è testo? I primi 8 KB: niente byte zero (a parte l'UTF-16, che li ha per forza)
    /// e decodificabili. Un file vuoto è testo.
    /// </summary>
    public static bool SembraTesto(string percorso)
    {
        try
        {
            if (!File.Exists(percorso)) return false;
            using var f = new FileStream(percorso, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var b = new byte[Math.Min(8192, f.Length)];
            f.ReadExactly(b);
            if (b.Length == 0) return true;
            if (Inizia(b, 0xFF, 0xFE) || Inizia(b, 0xFE, 0xFF) || Utf16SenzaBom(b) is not null) return true;
            if (b.AsSpan().IndexOf((byte)0) >= 0) return false;
            if (Utf8(b, f.Length > b.Length, out _)) return true;
            // ANSI: si accetta solo se i caratteri di controllo sono pochi (tab, a capo a parte)
            var strani = b.Count(x => x < 0x20 && x is not (0x09 or 0x0A or 0x0D or 0x0C or 0x1A));
            return strani <= b.Length / 100;
        }
        catch { return false; }
    }
}
