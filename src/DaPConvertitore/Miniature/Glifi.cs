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
    /// unità della griglia di 24: 1.8 è quello dell'interfaccia. Sotto i 20 pixel il tratto diventa un blob: lì si
    /// usa la sagoma piena, fatta apposta per le misure piccole.
    /// </summary>
    public static void Disegna(DrawingContext dc, string gruppo, Rect dove, Color colore, double spessore = 1.8, double alfa = 1)
    {
        var lato = Math.Min(dove.Width, dove.Height);
        if (lato < 20)
        {
            Pieno(dc, gruppo, dove, colore, alfa);
            return;
        }
        var s = lato / 24.0;
        dc.PushTransform(new TranslateTransform(dove.X + (dove.Width - lato) / 2, dove.Y + (dove.Height - lato) / 2));
        dc.PushTransform(new ScaleTransform(s, s));
        dc.DrawGeometry(null, Tavolozza.Penna(colore, spessore, alfa), Forma(gruppo));
        dc.Pop();
        dc.Pop();
    }

    /// <summary>
    /// La mattonella delle misure piccole (16, 20, 24): un quadrato tondo, viola notte, col bordo del tipo e la
    /// sagoma piena in mezzo. A 16 pixel una pagina piegata non si legge.
    /// </summary>
    public static void Mattonella(DrawingContext dc, string gruppo, Rect area)
    {
        var tinta = Tavolozza.Tinta(gruppo);
        // a 16 pixel il bordo grosso e il segno piccolo si mangiavano il disegno: bordo da un pixel, segno grande
        double m = area.Width <= 16 ? 0.35 : Math.Max(0.5, area.Width * 0.045);
        double bordo = area.Width <= 16 ? 1 : Math.Max(1, area.Width * 0.065);
        var r = new Rect(area.X + m + bordo / 2, area.Y + m + bordo / 2, Math.Max(1, area.Width - 2 * m - bordo), Math.Max(1, area.Height - 2 * m - bordo));
        var fondo = Tavolozza.P(Tavolozza.C("#160e2c"));
        dc.DrawRoundedRectangle(fondo, new Pen(Tavolozza.P(tinta), bordo) { LineJoin = PenLineJoin.Round }, r, r.Width * 0.22, r.Width * 0.22);
        var lato = r.Width * (area.Width <= 20 ? 0.78 : 0.62);
        Pieno(dc, gruppo, new Rect(r.X + (r.Width - lato) / 2, r.Y + (r.Height - lato) / 2, lato, lato), tinta);
    }

    /// <summary>La sagoma piena del tipo, su una griglia di 24. Pochi pezzi grossi: si legge anche a 16 pixel.</summary>
    public static void Pieno(DrawingContext dc, string gruppo, Rect dove, Color colore, double alfa = 1)
    {
        var lato = Math.Min(dove.Width, dove.Height);
        var ox = dove.X + (dove.Width - lato) / 2;
        var oy = dove.Y + (dove.Height - lato) / 2;
        double X(double v) => ox + v / 24.0 * lato;
        double Y(double v) => oy + v / 24.0 * lato;
        double S(double v) => v / 24.0 * lato;
        var b = Tavolozza.P(colore, alfa);

        void Rett(double x, double y, double w, double h)
        {
            var rr = Math.Min(S(w), S(h)) * 0.42;
            dc.DrawRoundedRectangle(b, null, new Rect(X(x), Y(y), S(w), S(h)), rr, rr);
        }

        void Poli(params (double x, double y)[] p)
        {
            var g = new StreamGeometry();
            using (var c = g.Open())
            {
                c.BeginFigure(new Point(X(p[0].x), Y(p[0].y)), true, true);
                for (var i = 1; i < p.Length; i++) c.LineTo(new Point(X(p[i].x), Y(p[i].y)), true, true);
            }
            g.Freeze();
            dc.DrawGeometry(b, null, g);
        }

        // sotto i 14 pixel la cinepresa, le parentesi e il diamante diventano una macchia: un segno solo, grosso
        if (lato < 14)
        {
            switch (gruppo)
            {
                case "Video":
                    Poli((4.2, 3.2), (4.2, 20.8), (21, 12));
                    return;
                case "Codice":
                    Poli((1.4, 5.2), (9.2, 12), (1.4, 18.8));
                    Poli((22.6, 5.2), (14.8, 12), (22.6, 18.8));
                    return;
                case "Immagine":
                    dc.DrawEllipse(b, null, new Point(X(7.2), Y(6.4)), S(2.6), S(2.6));
                    Poli((2, 19.6), (12, 8.4), (22, 19.6));
                    return;
                case "Modello":
                    Poli((12, 1.6), (22.2, 20.4), (1.8, 20.4));
                    return;
            }
        }

        switch (gruppo)
        {
            case "Video":
                Rett(1.6, 6.4, 12.6, 11.2);
                Poli((14.6, 8.4), (21.8, 12), (14.6, 15.6));
                break;
            case "Audio":
                Rett(3.4, 11, 4.2, 8.4);
                Rett(9.9, 4.8, 4.2, 14.6);
                Rett(16.4, 8, 4.2, 11.4);
                break;
            case "Immagine":
                dc.DrawEllipse(b, null, new Point(X(8.2), Y(8)), S(2.5), S(2.5));
                Poli((2.4, 19.4), (10.2, 11.2), (15.2, 16.2), (18.2, 13.4), (21.8, 19.4));
                break;
            case "Pdf":
                Poli((5.5, 2), (13.2, 2), (19.2, 8), (19.2, 22), (5.5, 22));
                var piega = new StreamGeometry();
                using (var c = piega.Open())
                {
                    c.BeginFigure(new Point(X(13.2), Y(2)), true, true);
                    c.LineTo(new Point(X(19.2), Y(8)), true, true);
                    c.LineTo(new Point(X(13.2), Y(8)), true, true);
                }
                piega.Freeze();
                dc.DrawGeometry(Tavolozza.P(Colors.White, alfa * 0.92), null, piega);
                break;
            case "Documento":
                Rett(4, 4.2, 16, 2.5);
                Rett(4, 9.2, 16, 2.5);
                Rett(4, 14.2, 16, 2.5);
                Rett(4, 19.2, 10, 2.5);
                break;
            case "Presentazione":
                Rett(2, 3.6, 20, 12.2);
                Rett(10.4, 16.2, 3.2, 2.6);
                Rett(6.2, 18.8, 11.6, 2.2);
                break;
            case "Tabella":
                Rett(2.6, 2.6, 8.2, 8.2);
                Rett(13.2, 2.6, 8.2, 8.2);
                Rett(2.6, 13.2, 8.2, 8.2);
                Rett(13.2, 13.2, 8.2, 8.2);
                break;
            case "Codice":
                var penna = new Pen(b, Math.Max(1.15, S(2.6))) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
                penna.Freeze();
                dc.DrawLine(penna, new Point(X(9.4), Y(5.2)), new Point(X(3.2), Y(12)));
                dc.DrawLine(penna, new Point(X(3.2), Y(12)), new Point(X(9.4), Y(18.8)));
                dc.DrawLine(penna, new Point(X(14.6), Y(5.2)), new Point(X(20.8), Y(12)));
                dc.DrawLine(penna, new Point(X(20.8), Y(12)), new Point(X(14.6), Y(18.8)));
                break;
            case "Sottotitoli":
                var cornice = new Pen(b, Math.Max(1.1, S(1.9))) { LineJoin = PenLineJoin.Round };
                cornice.Freeze();
                dc.DrawRoundedRectangle(null, cornice, new Rect(X(2.2), Y(5.4), S(19.6), S(13.2)), S(2.2), S(2.2));
                Rett(4.8, 10.4, 5.4, 2.2);
                Rett(12, 10.4, 6.6, 2.2);
                break;
            case "Archivio":
                Rett(6.6, 2.4, 10.8, 3.3);
                Rett(6.6, 7.4, 10.8, 3.3);
                Rett(6.6, 12.4, 10.8, 3.3);
                Rett(9, 17.2, 6, 4.4);
                break;
            case "Font":
                var lettera = Scritte.Testo("A", Scritte.Titolo, lato * 0.82, b);
                dc.DrawText(lettera, new Point(ox + (lato - lettera.Width) / 2, oy + (lato - lettera.Height) / 2 - lato * 0.04));
                break;
            case "Modello":
                Poli((12, 1.8), (21.6, 12), (12, 22.2), (2.4, 12));
                break;
            case "Testo":
                Rett(3.2, 4, 17.6, 2.15);
                Rett(3.2, 8.3, 17.6, 2.15);
                Rett(3.2, 12.6, 14, 2.15);
                Rett(3.2, 16.9, 11, 2.15);
                break;
            default:
                Poli((6, 2), (14, 2), (19.5, 7.5), (19.5, 22), (6, 22));
                break;
        }
    }
}
