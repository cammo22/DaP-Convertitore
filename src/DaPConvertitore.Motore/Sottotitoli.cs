using System.Text;
using System.Text.RegularExpressions;

namespace DaP.Convertitore;

/// <summary>SRT, VTT e ASS fra loro con FFmpeg; "solo le battute" senza tempi; e i sottotitoli tirati fuori da un video.</summary>
public static partial class Sottotitoli
{
    public static async Task Converti(Lavoro l, Contesto ctx)
    {
        var est = l.Formato.Estensione;
        if (l.Formato.Categoria == Categoria.Video)
        {
            ctx.Fase("Cerco i sottotitoli nel video");
            var info = await Sonda.Leggi(ctx.Strumenti, l.Sorgente, ctx.Ct);
            var traccia = info.Sottotitoli.FirstOrDefault(s => s.Testo)
                ?? throw new ErroreConversione(info.Sottotitoli.Count == 0
                    ? "In questo video non ci sono sottotitoli."
                    : "I sottotitoli di questo video sono immagini (DVD/Blu-ray): non diventano testo.");
            await Ffmpeg.Esegui(ctx, ["-i", l.Sorgente, "-map", $"0:{traccia.Indice}", "-c:s", "srt", "-f", "srt", l.Bozza], info.Durata, fase: "Estraggo i sottotitoli");
            return;
        }

        if (est == ".txt")
        {
            var testo = Testo.Leggi(l.Sorgente);
            var battute = Path.GetExtension(l.Sorgente).ToLowerInvariant() is ".ass" or ".ssa" ? DaAss(testo) : DaSrt(testo);
            await File.WriteAllTextAsync(l.Bozza, string.Join(Environment.NewLine, battute) + Environment.NewLine, new UTF8Encoding(true), ctx.Ct);
            return;
        }
        var codec = est switch { ".vtt" => "webvtt", ".ass" => "ass", _ => "srt" };
        await Ffmpeg.Esegui(ctx, ["-i", l.Sorgente, "-c:s", codec, "-f", Ffmpeg.Muxer(est), l.Bozza], 0, fase: "Converto i sottotitoli");
    }

    /// <summary>SRT e VTT: via numeri, tempi, intestazioni e tag; restano le battute.</summary>
    public static IEnumerable<string> DaSrt(string testo)
    {
        foreach (var riga in testo.Replace("\r\n", "\n").Split('\n'))
        {
            var r = riga.Trim();
            if (r.Length == 0 || r.All(char.IsDigit) || r.Contains("-->") || r.StartsWith("WEBVTT") || r.StartsWith("NOTE") || r.StartsWith("STYLE")) continue;
            var pulita = Tag().Replace(r, "").Trim();
            if (pulita.Length > 0) yield return pulita;
        }
    }

    /// <summary>ASS: il testo è dopo la nona virgola delle righe Dialogue; {\tag} via, \N è un a-capo.</summary>
    public static IEnumerable<string> DaAss(string testo)
    {
        foreach (var riga in testo.Replace("\r\n", "\n").Split('\n'))
        {
            if (!riga.StartsWith("Dialogue:", StringComparison.OrdinalIgnoreCase)) continue;
            var parti = riga.Split(',', 10);
            if (parti.Length < 10) continue;
            var t = Graffe().Replace(parti[9], "").Replace("\\N", " ").Replace("\\n", " ").Trim();
            if (t.Length > 0) yield return t;
        }
    }

    [GeneratedRegex("<[^>]+>")] private static partial Regex Tag();
    [GeneratedRegex(@"\{[^}]*\}")] private static partial Regex Graffe();
}
