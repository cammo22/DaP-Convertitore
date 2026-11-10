using System.Globalization;

namespace DaP.Convertitore;

/// <summary>
/// Dove mettere una finestra accanto all'avatar, dentro l'area di lavoro del monitor dove sta lui.
/// Le coordinate sono tutte nello stesso sistema (pixel veri dello schermo, anche negativi):
/// chi chiama converte i DPI. Così una finestra non finisce sul monitor sbagliato né fuori.
/// </summary>
public static class Ancoraggio
{
    public const int Spazio = 12;

    /// <summary>Un rettangolo in pixel. X e Y possono essere negativi (monitor a sinistra del principale).</summary>
    public readonly record struct Riquadro(int X, int Y, int L, int A)
    {
        public int Destra => X + L;
        public int Fondo => Y + A;
    }

    /// <summary>Accanto all'ancora, sul lato dove ci sta meglio, poi bloccata dentro l'area di lavoro.</summary>
    public static Riquadro Accanto(Riquadro ancora, int larghezza, int altezza, Riquadro area)
    {
        if (area.L < 2 || area.A < 2) return new Riquadro(area.X, area.Y, Math.Max(1, larghezza), Math.Max(1, altezza));
        var l = Math.Clamp(larghezza, 1, area.L);
        var a = Math.Clamp(altezza, 1, area.A);
        var lati = new[]
        {
            new Riquadro(ancora.Destra + Spazio, Centra(ancora.Y, ancora.A, a), l, a),
            new Riquadro(ancora.X - Spazio - l, Centra(ancora.Y, ancora.A, a), l, a),
            new Riquadro(Centra(ancora.X, ancora.L, l), ancora.Y - Spazio - a, l, a),
            new Riquadro(Centra(ancora.X, ancora.L, l), ancora.Fondo + Spazio, l, a),
        };
        var scelta = lati[0];
        var meglio = -1;
        foreach (var c in lati)
        {
            var visibile = Sovrapposizione(c, area);
            if (visibile > meglio) { meglio = visibile; scelta = c; }
        }
        return Blocca(scelta, area);
    }

    /// <summary>Al centro dell'area. Serve quando l'avatar è spento: il monitor principale.</summary>
    public static Riquadro Centro(int larghezza, int altezza, Riquadro area)
    {
        if (area.L < 2 || area.A < 2) return new Riquadro(area.X, area.Y, Math.Max(1, larghezza), Math.Max(1, altezza));
        var l = Math.Clamp(larghezza, 1, area.L);
        var a = Math.Clamp(altezza, 1, area.A);
        return new Riquadro(area.X + (area.L - l) / 2, area.Y + (area.A - a) / 2, l, a);
    }

    public static Riquadro Blocca(Riquadro r, Riquadro area)
    {
        var l = Math.Clamp(r.L, 1, Math.Max(1, area.L));
        var a = Math.Clamp(r.A, 1, Math.Max(1, area.A));
        var x = Math.Clamp(r.X, area.X, Math.Max(area.X, area.Destra - l));
        var y = Math.Clamp(r.Y, area.Y, Math.Max(area.Y, area.Fondo - a));
        return new Riquadro(x, y, l, a);
    }

    public static bool Dentro(Riquadro r, Riquadro area) =>
        r.L > 0 && r.A > 0 && r.X >= area.X && r.Y >= area.Y && r.Destra <= area.Destra && r.Fondo <= area.Fondo;

    /// <summary>
    /// Tiene la finestra dentro uno schermo vero. Se ne tocca più di uno, resta in quello dove sta
    /// di più. Se è finita nel vuoto, va in quello che contiene il centro, o nel primo.
    /// </summary>
    public static Riquadro DentroSchermi(Riquadro finestra, IReadOnlyList<Riquadro> schermi)
    {
        if (schermi is null || schermi.Count == 0) return finestra;
        var meglio = schermi[0];
        var area = -1;
        foreach (var s in schermi)
        {
            if (s.L < 2 || s.A < 2) continue;
            var a = Sovrapposizione(finestra, s);
            if (a > area) { area = a; meglio = s; }
        }
        if (area <= 0)
        {
            var cx = finestra.X + Math.Max(1, finestra.L) / 2;
            var cy = finestra.Y + Math.Max(1, finestra.A) / 2;
            foreach (var s in schermi)
                if (cx >= s.X && cx < s.Destra && cy >= s.Y && cy < s.Fondo) { meglio = s; break; }
        }
        return Blocca(finestra, meglio);
    }

    public static bool Tocca(Riquadro a, Riquadro b) =>
        a.L > 0 && a.A > 0 && a.X < b.Destra && a.Destra > b.X && a.Y < b.Fondo && a.Fondo > b.Y;

    public static string Chiave(Riquadro r) =>
        string.Create(CultureInfo.InvariantCulture, $"{r.X},{r.Y},{r.L},{r.A}");

    public static bool ProvaChiave(string? testo, out Riquadro r)
    {
        r = default;
        if (string.IsNullOrWhiteSpace(testo)) return false;
        var p = testo.Split(',');
        if (p.Length != 4) return false;
        if (!int.TryParse(p[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x)) return false;
        if (!int.TryParse(p[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y)) return false;
        if (!int.TryParse(p[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var l)) return false;
        if (!int.TryParse(p[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var a)) return false;
        if (l < 2 || a < 2) return false;
        r = new Riquadro(x, y, l, a);
        return true;
    }

    static int Centra(int inizio, int misuraAncora, int misura) => inizio + misuraAncora / 2 - misura / 2;

    static int Sovrapposizione(Riquadro a, Riquadro b)
    {
        var x = Math.Max(0, Math.Min(a.Destra, b.Destra) - Math.Max(a.X, b.X));
        var y = Math.Max(0, Math.Min(a.Fondo, b.Fondo) - Math.Max(a.Y, b.Y));
        return x * y;
    }
}
