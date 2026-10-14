namespace DaP.Convertitore.Lettore;

/// <summary>
/// Il ritocco della foto (disegni e scritte sopra): la pagina del lettore compone l'immagine e ce la manda già
/// codificata (JPG, PNG o WEBP); qui prende il posto del file vero. Dei dati dello scatto (data, macchina, posizione) di
/// una JPG non si perde niente: il blocco EXIF dell'originale si rimette nel file nuovo, con l'orientamento a «dritto»
/// (la foto nuova è già girata come si vede).
/// </summary>
public static class Ritocco
{
    /// <summary>JPG, PNG o WEBP dal contenuto (non dall'estensione), se è uno di questi.</summary>
    public static string? Tipo(byte[] d)
    {
        if (d.Length > 3 && d[0] == 0xFF && d[1] == 0xD8 && d[2] == 0xFF) return "jpg";
        if (d.Length > 8 && d[0] == 0x89 && d[1] == 'P' && d[2] == 'N' && d[3] == 'G') return "png";
        if (d.Length > 12 && d[0] == 'R' && d[1] == 'I' && d[2] == 'F' && d[3] == 'F' && d[8] == 'W' && d[9] == 'E' && d[10] == 'B' && d[11] == 'P') return "webp";
        return null;
    }

    /// <summary>Il formato del file si può riscrivere con quello che manda la pagina? (JPG, PNG, WEBP: sì; il resto esce accanto, in PNG.)</summary>
    public static string? FormatoDelFile(string percorso) => Path.GetExtension(percorso).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" or ".jpe" or ".jfif" => "jpg",
        ".png" => "png",
        ".webp" => "webp",
        _ => null,
    };

    /// <summary>
    /// Salva il ritocco. Se il file è JPG, PNG o WEBP prende il suo posto; per gli altri formati (HEIC, BMP, GIF…) il
    /// ritocco esce accanto, in PNG, «nome (ritoccata).png», e l'originale non si tocca. Restituisce dove è finito.
    /// </summary>
    public static string Salva(string percorso, byte[] dati)
    {
        var tipo = Tipo(dati) ?? throw new ErroreConversione("Il ritocco non è arrivato intero: non ho salvato niente.");
        var formato = FormatoDelFile(percorso);
        if (formato is not null && formato == tipo)
        {
            if (tipo == "jpg") dati = InnestaExif(File.ReadAllBytes(percorso), dati);
            Modifiche.Sostituisci(percorso, dati);
            return percorso;
        }
        if (tipo != "png") throw new ErroreConversione("Questo formato non si riscrive: il ritocco va accanto, in PNG.");
        var nuovo = Nomi.Prenota(percorso, ".png", "ritoccata");
        try { File.WriteAllBytes(nuovo, dati); }
        finally { Nomi.Libera(nuovo); }
        return nuovo;
    }

    // ——— il blocco EXIF di una JPG ———

    /// <summary>Il segmento APP1 «Exif» (marcatore compreso), se la JPG ce l'ha.</summary>
    static byte[]? Exif(byte[] jpg)
    {
        if (jpg.Length < 4 || jpg[0] != 0xFF || jpg[1] != 0xD8) return null;
        var i = 2;
        while (i + 4 <= jpg.Length && jpg[i] == 0xFF)
        {
            var marcatore = jpg[i + 1];
            if (marcatore is 0xDA or 0xD9) break; // da qui in poi sono i pixel
            var lunghezza = (jpg[i + 2] << 8) | jpg[i + 3];
            if (lunghezza < 2 || i + 2 + lunghezza > jpg.Length) break;
            if (marcatore == 0xE1 && lunghezza >= 8 && jpg[i + 4] == 'E' && jpg[i + 5] == 'x' && jpg[i + 6] == 'i' && jpg[i + 7] == 'f' && jpg[i + 8] == 0 && jpg[i + 9] == 0)
                return jpg[i..(i + 2 + lunghezza)];
            i += 2 + lunghezza;
        }
        return null;
    }

    /// <summary>L'orientamento (tag 0x0112) scritto nel blocco EXIF diventa 1: i pixel nuovi sono già dritti.</summary>
    static void OrientamentoDritto(byte[] app1)
    {
        const int t = 10; // l'intestazione TIFF comincia dopo FFE1, la lunghezza e «Exif\0\0»
        if (app1.Length < t + 8) return;
        var piccolo = app1[t] == 'I';
        int L16(int o) => piccolo ? app1[o] | (app1[o + 1] << 8) : (app1[o] << 8) | app1[o + 1];
        long L32(int o) => piccolo
            ? app1[o] | ((long)app1[o + 1] << 8) | ((long)app1[o + 2] << 16) | ((long)app1[o + 3] << 24)
            : ((long)app1[o] << 24) | ((long)app1[o + 1] << 16) | ((long)app1[o + 2] << 8) | app1[o + 3];
        var ifd = t + L32(t + 4);
        if (ifd < 0 || ifd + 2 > app1.Length) return;
        var voci = L16((int)ifd);
        for (var k = 0; k < voci; k++)
        {
            var e = (int)ifd + 2 + 12 * k;
            if (e + 12 > app1.Length) return;
            if (L16(e) != 0x0112) continue;
            if (piccolo) { app1[e + 8] = 1; app1[e + 9] = 0; } else { app1[e + 8] = 0; app1[e + 9] = 1; }
            return;
        }
    }

    /// <summary>La JPG nuova con il blocco EXIF della vecchia (subito dopo SOI e dell'eventuale JFIF).</summary>
    public static byte[] InnestaExif(byte[] originale, byte[] nuovo)
    {
        if (Exif(nuovo) is not null) return nuovo; // se l'ha già, è suo
        var exif = Exif(originale);
        if (exif is null || nuovo.Length < 4) return nuovo;
        OrientamentoDritto(exif);
        var pos = 2;
        if (nuovo[2] == 0xFF && nuovo[3] == 0xE0 && nuovo.Length > 6) pos = 2 + 2 + ((nuovo[4] << 8) | nuovo[5]); // il JFIF resta per primo
        if (pos > nuovo.Length) return nuovo;
        var fine = new byte[nuovo.Length + exif.Length];
        Buffer.BlockCopy(nuovo, 0, fine, 0, pos);
        Buffer.BlockCopy(exif, 0, fine, pos, exif.Length);
        Buffer.BlockCopy(nuovo, pos, fine, pos + exif.Length, nuovo.Length - pos);
        return fine;
    }
}
