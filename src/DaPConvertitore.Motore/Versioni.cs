namespace DaP.Convertitore;

/// <summary>Confronta due numeri di versione (2.3.0 e v2.3.1). Lo usa Linux per l'avviso di aggiornamento, senza API.</summary>
public static class Versioni
{
    public static int[]? Parti(string? testo)
    {
        if (string.IsNullOrWhiteSpace(testo)) return null;
        var v = testo.Trim().TrimStart('v', 'V');
        var i = v.IndexOfAny(['/', '?', '#', ' ']);
        if (i >= 0) v = v[..i];
        var pezzi = v.Split('.');
        if (pezzi.Length < 3) return null;
        if (!int.TryParse(pezzi[0], out var a) || !int.TryParse(pezzi[1], out var b) || !int.TryParse(pezzi[2], out var c)) return null;
        return [a, b, c];
    }

    /// <summary>Vero se <paramref name="trovata"/> è più nuova di <paramref name="attuale"/>.</summary>
    public static bool PiùNuova(string attuale, string trovata)
    {
        var a = Parti(attuale);
        var b = Parti(trovata);
        if (a is null || b is null) return false;
        for (var i = 0; i < 3; i++)
            if (b[i] != a[i]) return b[i] > a[i];
        return false;
    }

    /// <summary>Dal link finale di /releases/latest (…/releases/tag/v2.3.0) tira fuori la versione.</summary>
    public static string? DalLink(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        const string segno = "/tag/";
        var i = url.LastIndexOf(segno, StringComparison.OrdinalIgnoreCase);
        if (i < 0) return null;
        var tag = url[(i + segno.Length)..].Trim().Trim('/');
        return Parti(tag) is null ? null : tag.TrimStart('v', 'V');
    }
}
