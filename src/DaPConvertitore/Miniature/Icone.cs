using System.Windows;
using System.Windows.Media;

namespace DaP.Convertitore.App.Miniature;

/// <summary>
/// Le icone dei tipi di file (quelle che si vedono nell'elenco, nei file piccoli, sul desktop): un foglio col
/// bordo piegato, viola notte, col disegnino del tipo nella sua tinta (magenta i video, verde la musica, ciano le foto…).
/// Si disegnano al volo a tutte le misure e si scrivono in file .ico accanto ai dati dell'app.
/// </summary>
public static class Icone
{
    static readonly int[] misure = [16, 20, 24, 32, 40, 48, 64, 128, 256];

    /// <summary>Il disegno di un'icona a una misura.</summary>
    static Quadro Disegna(string gruppo, int S)
    {
        var tinta = Tavolozza.Tinta(gruppo);
        return Tela.Fai(S, S, dc =>
        {
            double x = S * 0.14, y = S * 0.05, w = S * 0.72, h = S * 0.9, taglio = S * 0.24;
            var pagina = new StreamGeometry();
            using (var g = pagina.Open())
            {
                g.BeginFigure(new Point(x, y), true, true);
                g.LineTo(new Point(x + w - taglio, y), true, true);
                g.LineTo(new Point(x + w, y + taglio), true, true);
                g.LineTo(new Point(x + w, y + h), true, true);
                g.LineTo(new Point(x, y + h), true, true);
            }
            pagina.Freeze();
            var fondo = new LinearGradientBrush(Tavolozza.C("#3a2268"), Tavolozza.C("#150f27"), 100);
            fondo.Freeze();
            var bordo = Math.Max(1, S * 0.035);
            dc.DrawGeometry(fondo, new Pen(Tavolozza.P(tinta, 0.95), bordo) { LineJoin = PenLineJoin.Round }, pagina);

            // l'angolo piegato
            var piega = new StreamGeometry();
            using (var g = piega.Open())
            {
                g.BeginFigure(new Point(x + w - taglio, y), true, true);
                g.LineTo(new Point(x + w, y + taglio), true, true);
                g.LineTo(new Point(x + w - taglio, y + taglio), true, true);
            }
            piega.Freeze();
            dc.DrawGeometry(Tavolozza.P(tinta, 0.9), null, piega);

            // il disegnino del tipo, grande: nelle misure piccole deve restare leggibile
            var lato = S * (S <= 24 ? 0.5 : 0.46);
            var scala = lato / 24.0;
            var spessore = Math.Max(1.8, 1.45 / scala);
            Glifi.Disegna(dc, gruppo, new Rect(x + (w - lato) / 2 - (S <= 24 ? 0 : 0), y + h * 0.4, lato, lato), tinta, spessore);
        });
    }

    /// <summary>
    /// Un'immagine di un .ico: da 256 in su un PNG, sotto un DIB a 32 bit (alfa non premoltiplicata) con la maschera vuota,
    /// come vuole il formato (i PNG nelle misure piccole non li legge ogni programma).
    /// </summary>
    static byte[] Immagine(Quadro q)
    {
        if (q.Larghezza >= 256) return Tela.Png(q);
        using var m = new MemoryStream();
        using var w = new BinaryWriter(m);
        int n = q.Larghezza;
        w.Write(40); w.Write(n); w.Write(n * 2); w.Write((ushort)1); w.Write((ushort)32);
        w.Write(0); w.Write(n * n * 4); w.Write(0); w.Write(0); w.Write(0); w.Write(0);
        for (var y = n - 1; y >= 0; y--) // dal basso
            for (var x = 0; x < n; x++)
            {
                var o = (y * n + x) * 4;
                int a = q.Pixel[o + 3];
                byte Pieno(int v) => a == 0 ? (byte)0 : (byte)Math.Min(255, v * 255 / a);
                w.Write(Pieno(q.Pixel[o])); w.Write(Pieno(q.Pixel[o + 1])); w.Write(Pieno(q.Pixel[o + 2])); w.Write((byte)a);
            }
        var riga = ((n + 31) / 32) * 4;
        w.Write(new byte[riga * n]); // la maschera AND: tutta zero, conta l'alfa
        return m.ToArray();
    }

    /// <summary>Scrive il file .ico del gruppo (tutte le misure) e restituisce il percorso.</summary>
    public static string Scrivi(string cartella, string gruppo, string etichetta)
    {
        Directory.CreateDirectory(cartella);
        var percorso = Path.Combine(cartella, $"{gruppo}-{etichetta}.ico");
        if (File.Exists(percorso) && new FileInfo(percorso).Length > 1000 && etichetta != "prova") return percorso; // già fatta per questa versione
        var dati = misure.Select(m => (m, dati: Immagine(Disegna(gruppo, m)))).ToList();
        using var f = File.Create(percorso);
        using var w = new BinaryWriter(f);
        w.Write((ushort)0);
        w.Write((ushort)1);
        w.Write((ushort)dati.Count);
        var offset = 6 + 16 * dati.Count;
        foreach (var (m, img) in dati)
        {
            w.Write((byte)(m >= 256 ? 0 : m));
            w.Write((byte)(m >= 256 ? 0 : m));
            w.Write((byte)0);
            w.Write((byte)0);
            w.Write((ushort)1);
            w.Write((ushort)32);
            w.Write(img.Length);
            w.Write(offset);
            offset += img.Length;
        }
        foreach (var (_, img) in dati) w.Write(img);
        return percorso;
    }

    public static readonly string[] Gruppi = ApriCon.Gruppi.ToArray();

    public static void ScriviTutte(string cartella, string etichetta)
    {
        foreach (var g in Gruppi) Scrivi(cartella, g, etichetta);
    }
}
