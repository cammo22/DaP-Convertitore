using System.Globalization;
using System.Text.Json;

namespace DaP.Convertitore;

public sealed record InfoVideo(string Codec, int Larghezza, int Altezza, double Fps, long Bitrate, string PixFmt, bool Hdr, string? Trasferimento, int Bit)
{
    /// <summary>Il lato corto: "1080p" vuol dire 1080 anche per un video in verticale.</summary>
    public int LatoCorto => Math.Min(Larghezza, Altezza);
    public bool Verticale => Altezza > Larghezza;
}

public sealed record InfoAudio(int Indice, string Codec, int Canali, int Campionamento, long Bitrate, string? Lingua);

public sealed record InfoSottotitolo(int Indice, string Codec, string? Lingua)
{
    /// <summary>I sottotitoli a immagini (Blu-ray, DVD) non diventano SRT senza OCR.</summary>
    public bool Testo => Codec is "subrip" or "srt" or "ass" or "ssa" or "webvtt" or "mov_text" or "text";
}

/// <summary>Quello che FFprobe dice di un file audio o video.</summary>
public sealed record InfoMedia(
    double Durata,
    long Bitrate,
    string Contenitore,
    InfoVideo? Video,
    IReadOnlyList<InfoAudio> Audio,
    IReadOnlyList<InfoSottotitolo> Sottotitoli,
    bool Copertina);

public static class Sonda
{
    /// <param name="silenzioso">Non scrive nel registro: lo usano le miniature, che partono a migliaia.</param>
    public static async Task<InfoMedia> Leggi(Strumenti s, string percorso, CancellationToken ct = default, bool silenzioso = false)
    {
        var r = await Processi.Esegui(s.Ffprobe,
            ["-v", "error", "-print_format", "json", "-show_format", "-show_streams", "-i", percorso], ct, silenzioso: silenzioso);
        if (r.Codice != 0 || string.IsNullOrWhiteSpace(r.Uscita))
            throw new ErroreConversione("Questo file non si riesce a leggere: forse è rovinato o non è un file multimediale.", r.Errori);
        return Interpreta(r.Uscita);
    }

    public static InfoMedia Interpreta(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var radice = doc.RootElement;
        var formato = radice.TryGetProperty("format", out var f) ? f : default;
        var durata = Numero(formato, "duration");
        var bitrate = (long)Numero(formato, "bit_rate");
        var contenitore = Testo(formato, "format_name") ?? "";

        InfoVideo? video = null;
        var audio = new List<InfoAudio>();
        var sub = new List<InfoSottotitolo>();
        var copertina = false;
        long bitrateAudio = 0;

        if (radice.TryGetProperty("streams", out var flussi))
        {
            foreach (var st in flussi.EnumerateArray())
            {
                var tipo = Testo(st, "codec_type");
                var codec = Testo(st, "codec_name") ?? "";
                var indice = (int)Numero(st, "index");
                var lingua = st.TryGetProperty("tags", out var tag) ? Testo(tag, "language") : null;
                if (tipo == "video")
                {
                    var allegata = st.TryGetProperty("disposition", out var disp) && Numero(disp, "attached_pic") == 1;
                    if (allegata) { copertina = true; continue; }
                    if (video is not null) continue;
                    int w = (int)Numero(st, "width"), h = (int)Numero(st, "height");
                    if (Ruotato(st)) (w, h) = (h, w);
                    var trc = Testo(st, "color_transfer");
                    var pix = Testo(st, "pix_fmt") ?? "";
                    var bit = (int)Numero(st, "bits_per_raw_sample");
                    if (bit == 0) bit = pix.Contains("10") ? 10 : pix.Contains("12") ? 12 : 8;
                    video = new InfoVideo(codec, w, h, Fps(Testo(st, "avg_frame_rate")) is > 0 and var fps ? fps : Fps(Testo(st, "r_frame_rate")),
                        (long)Numero(st, "bit_rate"), pix, trc is "smpte2084" or "arib-std-b67", trc, bit);
                    if (durata <= 0) durata = Numero(st, "duration");
                }
                else if (tipo == "audio")
                {
                    var b = (long)Numero(st, "bit_rate");
                    bitrateAudio += b;
                    audio.Add(new InfoAudio(indice, codec, (int)Numero(st, "channels"), (int)Numero(st, "sample_rate"), b, lingua));
                    if (durata <= 0) durata = Numero(st, "duration");
                }
                else if (tipo == "subtitle")
                {
                    sub.Add(new InfoSottotitolo(indice, codec, lingua));
                }
            }
        }

        // MKV e WEBM spesso non dicono il bitrate del video: lo si ricava dal totale
        if (video is { Bitrate: 0 } && bitrate > 0)
            video = video with { Bitrate = Math.Max(0, bitrate - (bitrateAudio > 0 ? bitrateAudio : audio.Count * 160_000L)) };
        return new InfoMedia(durata, bitrate, contenitore, video, audio, sub, copertina);
    }

    static bool Ruotato(JsonElement st)
    {
        if (st.TryGetProperty("side_data_list", out var sd))
            foreach (var d in sd.EnumerateArray())
                if (d.TryGetProperty("rotation", out var r) && r.TryGetDouble(out var gradi) && Math.Abs(Math.Abs(gradi) % 180 - 90) < 1)
                    return true;
        if (st.TryGetProperty("tags", out var tag) && Testo(tag, "rotate") is { } rot && double.TryParse(rot, CultureInfo.InvariantCulture, out var g2))
            return Math.Abs(Math.Abs(g2) % 180 - 90) < 1;
        return false;
    }

    static double Fps(string? frazione)
    {
        if (string.IsNullOrEmpty(frazione)) return 0;
        var p = frazione.Split('/');
        if (p.Length == 2 && double.TryParse(p[0], CultureInfo.InvariantCulture, out var a) && double.TryParse(p[1], CultureInfo.InvariantCulture, out var b) && b > 0)
            return a / b;
        return double.TryParse(frazione, CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    static string? Testo(JsonElement e, string nome) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(nome, out var v) ? v.ToString() : null;

    static double Numero(JsonElement e, string nome)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(nome, out var v)) return 0;
        if (v.ValueKind == JsonValueKind.Number) return v.GetDouble();
        return double.TryParse(v.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0;
    }
}
