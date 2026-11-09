using System.Globalization;
using DaP.Convertitore.Lettore;

namespace DaP.Convertitore;

/// <summary>
/// Quello che l'utente ha corretto nella sezione «Info del file». Si scrive nel file convertito.
/// I campi vuoti si scrivono vuoti (così si può togliere un titolo). Quello che il formato non tiene si ignora.
/// </summary>
public sealed class OpzioniScheda
{
    public bool Scrivi { get; set; }
    public string? Titolo { get; set; }
    public string? Artista { get; set; }
    public string? Album { get; set; }
    public string? Anno { get; set; }
    public string? Traccia { get; set; }
    public string? Commento { get; set; }
    public string? Autore { get; set; }
    public string? Descrizione { get; set; }
    public string? DataScatto { get; set; }
    /// <summary>Null = tieni quella che c'è. «nessuna» = toglila. «generata:2» = un disegno. Altrimenti il percorso di un'immagine.</summary>
    public string? Copertina { get; set; }
}

/// <summary>Quello che la sezione mostra, letto dal file, e quali campi il formato di arrivo sa tenere.</summary>
public sealed class DatiScheda
{
    public string Titolo { get; set; } = "";
    public string Artista { get; set; } = "";
    public string Album { get; set; } = "";
    public string Anno { get; set; } = "";
    public string Traccia { get; set; } = "";
    public string Commento { get; set; } = "";
    public string Autore { get; set; } = "";
    public string Descrizione { get; set; } = "";
    public string DataScatto { get; set; } = "";
    public string? Copertina { get; set; }
    public string[] Campi { get; set; } = [];
}

/// <summary>
/// I campi che un formato sa conservare, e come si passano a FFmpeg. Niente campi inventati:
/// la musica tiene titolo, artista, album, anno, traccia (e la copertina su MP3, M4A, FLAC);
/// il video MP4, MOV e MKV tiene titolo e commento; JPG e TIFF tengono data, descrizione e autore.
/// </summary>
public static class Metadati
{
    public static bool FotoTiene(string estensione) =>
        estensione.ToLowerInvariant() is ".jpg" or ".jpeg" or ".tif" or ".tiff";

    /// <summary>I nomi dei campi, come li aspetta l'interfaccia. Vuoto = il formato non ne tiene.</summary>
    public static string[] Campi(string estensione) => estensione.ToLowerInvariant() switch
    {
        ".mp3" or ".m4a" or ".flac" => ["titolo", "artista", "album", "anno", "traccia", "copertina"],
        ".ogg" or ".opus" or ".wma" => ["titolo", "artista", "album", "anno", "traccia"],
        ".mp4" or ".mov" or ".mkv" => ["titolo", "commento"],
        ".jpg" or ".jpeg" or ".tif" or ".tiff" => ["data", "descrizione", "autore"],
        _ => [],
    };

    /// <summary>Aggiunge i -metadata al comando, solo per i campi che questo formato tiene.</summary>
    public static void SulComando(List<string> a, OpzioniScheda? s, string estensione)
    {
        if (s is not { Scrivi: true }) return;
        var est = estensione.ToLowerInvariant();
        if (est is ".mp3" or ".m4a" or ".flac" or ".ogg" or ".opus" or ".wma")
        {
            Meta(a, "title", s.Titolo);
            Meta(a, "artist", s.Artista);
            Meta(a, "album", s.Album);
            Meta(a, "date", s.Anno);
            Meta(a, "track", s.Traccia);
        }
        else if (est is ".mp4" or ".mov" or ".mkv")
        {
            Meta(a, "title", s.Titolo);
            Meta(a, "comment", s.Commento);
        }
    }

    static void Meta(List<string> a, string chiave, string? valore)
    {
        if (valore is null) return;
        var pulito = valore.Replace('\r', ' ').Replace('\n', ' ').Trim();
        a.Add("-metadata");
        a.Add($"{chiave}={pulito}");
    }

    /// <summary>Una data scritta a mano: un anno, 2024-03-12, oppure 12/03/2024. Mezzogiorno, così il fuso non cambia giorno.</summary>
    public static DateTimeOffset? DataDi(string? testo)
    {
        if (string.IsNullOrWhiteSpace(testo)) return null;
        var t = testo.Trim();
        if (t.Length == 4 && int.TryParse(t, NumberStyles.None, CultureInfo.InvariantCulture, out var anno) && anno is >= 1000 and <= 9999)
            return new DateTimeOffset(anno, 1, 1, 12, 0, 0, TimeSpan.Zero);
        string[] formati = ["yyyy-MM-dd", "yyyy/MM/dd", "dd/MM/yyyy", "d/M/yyyy", "yyyy:MM:dd HH:mm:ss", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm:ss"];
        if (DateTime.TryParseExact(t, formati, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var d)
            || DateTime.TryParse(t, CultureInfo.GetCultureInfo("it-IT"), DateTimeStyles.AllowWhiteSpaces, out d))
            return new DateTimeOffset(d.Year, d.Month, d.Day, 12, 0, 0, TimeSpan.Zero);
        return null;
    }

    public static async Task<DatiScheda> Leggi(Strumenti s, string percorso, string estensioneUscita)
    {
        var dati = new DatiScheda { Campi = Campi(estensioneUscita) };
        try
        {
            var m = await Media.Leggi(s, percorso);
            string T(params string[] chiavi)
            {
                foreach (var k in chiavi)
                    if (m.Tag.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v)) return v.Trim();
                return "";
            }
            dati.Titolo = T("title");
            dati.Artista = T("artist", "album_artist");
            dati.Album = T("album");
            dati.Anno = Anno(T("date", "year"));
            dati.Traccia = Traccia(T("track"));
            dati.Commento = T("comment", "description");
            dati.Autore = T("artist", "author", "composer");
            dati.Descrizione = T("comment", "description", "title");
            dati.DataScatto = Giorno(T("creation_time", "date", "DateTimeOriginal"));
        }
        catch (ErroreConversione) { /* una foto che FFprobe non racconta: restano i campi della foto */ }

        if (FotoTiene(Path.GetExtension(percorso)) || FotoTiene(estensioneUscita))
        {
            try
            {
                var foto = await Immagini.LeggiTesti(percorso);
                if (!string.IsNullOrWhiteSpace(foto.Autore)) dati.Autore = foto.Autore;
                if (!string.IsNullOrWhiteSpace(foto.Descrizione)) dati.Descrizione = foto.Descrizione;
                if (foto.Data is { } d) dati.DataScatto = d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }
            catch { /* senza i testi della foto si tengono quelli di FFprobe, se c'erano */ }
        }

        if (dati.Campi.Contains("copertina"))
        {
            try
            {
                var b = await Media.Copertina(s, percorso, true, silenzioso: true, lato: 280);
                if (b is not null) dati.Copertina = new EsitoCopertina(b, null, true, null).ComeDataUrl();
            }
            catch { /* niente copertina da mostrare */ }
        }
        return dati;
    }

    static string Anno(string data)
    {
        for (var i = 0; i + 4 <= data.Length; i++)
        {
            if (data.Skip(i).Take(4).All(char.IsDigit) && (i == 0 || !char.IsDigit(data[i - 1])))
                return data.Substring(i, 4);
        }
        return "";
    }

    static string Traccia(string t)
    {
        if (t.Length == 0) return "";
        var p = t.Split('/')[0].Trim();
        return p.Length <= 4 ? p : "";
    }

    static string Giorno(string data)
    {
        var d = DataDi(data);
        return d is null ? "" : d.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

}
