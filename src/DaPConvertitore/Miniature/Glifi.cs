using System.Windows;
using System.Windows.Media;

namespace DaP.Convertitore.App.Miniature;

/// <summary>
/// Le figurine dei tipi di file, disegnate a tratto come quelle dell'interfaccia (griglia di 24, angoli tondi).
/// Servono alle icone e alle miniature che non hanno niente da mostrare (un archivio RAR, un file strano).
/// </summary>
public static class Glifi
{
    static readonly Dictionary<string, Geometry> cache = [];

    const string Pagina = "M6 2h8l5 5v15H6z M14 2v5h5";

    static readonly Dictionary<string, string> tracciati = new()
    {
        ["Video"] = "M5 6h9a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2z M16 10l5-3v10l-5-3z",
        ["Audio"] = "M9 18V5l11-2v13 M9 18a3 3 0 1 1-6 0 3 3 0 0 1 6 0z M20 16a3 3 0 1 1-6 0 3 3 0 0 1 6 0z",
        ["Immagine"] = "M5 4h14a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2z M11 10a2 2 0 1 1-4 0 2 2 0 0 1 4 0z M21 16l-5-5-9 9",
        ["Pdf"] = Pagina + " M9 14h6 M9 17h4",
        ["Documento"] = Pagina + " M9 12h7 M9 15h7 M9 18h5",
        ["Presentazione"] = "M5 4h14a2 2 0 0 1 2 2v9a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2z M12 17v4 M8 21h8 M7 12l3-3 2 2 4-4",
        ["Tabella"] = "M5 4h14a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2z M3 10h18 M3 15h18 M9 4v16 M15 4v16",
        ["Testo"] = "M5 6h14 M5 10h14 M5 14h10 M5 18h7",
        ["Codice"] = "M8 7l-5 5 5 5 M16 7l5 5-5 5 M14 4l-4 16",
        ["Sottotitoli"] = "M5 5h14a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V7a2 2 0 0 1 2-2z M7 15h4 M13 15h4 M7 11h10",
        ["Archivio"] = "M6 3h12a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2z M12 3v2 M12 7v2 M12 11v2 M10 14h4v4h-4z",
        ["Font"] = "M2.5 19L8 5l5.5 14 M4.6 14h6.8 M21 15.5a3.2 3.2 0 1 1-6.4 0 3.2 3.2 0 0 1 6.4 0z M21 12.3V19",
        ["Modello"] = "M12 2l9 5v10l-9 5-9-5V7z M3 7l9 5 9-5 M12 12v10",
        ["Altro"] = Pagina,
    };

    static Geometry Forma(string gruppo)
    {
        lock (cache)
        {
            if (!cache.TryGetValue(gruppo, out var g))
            {
                g = Geometry.Parse(tracciati.TryGetValue(gruppo, out var d) ? d : tracciati["Altro"]);
                g.Freeze();
                cache[gruppo] = g;
            }
            return g;
        }
    }

    /// <summary>
    /// Disegna la figurina del gruppo dentro <paramref name="dove"/> (un quadrato). <paramref name="spessore"/> è in
    /// unità della griglia di 24: 1.8 è quello dell'interfaccia.
    /// </summary>
    public static void Disegna(DrawingContext dc, string gruppo, Rect dove, Color colore, double spessore = 1.8, double alfa = 1)
    {
        var lato = Math.Min(dove.Width, dove.Height);
        var s = lato / 24.0;
        dc.PushTransform(new TranslateTransform(dove.X + (dove.Width - lato) / 2, dove.Y + (dove.Height - lato) / 2));
        dc.PushTransform(new ScaleTransform(s, s));
        dc.DrawGeometry(null, Tavolozza.Penna(colore, spessore, alfa), Forma(gruppo));
        dc.Pop();
        dc.Pop();
    }
}
