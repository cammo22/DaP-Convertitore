using System.Globalization;

namespace DaP.Convertitore;

/// <summary>
/// Il comando di FFmpeg della registrazione, e cosa deve fare «Registra» dal vassoio, dall'avatar o dal ponte.
/// Niente finestre: le prove lo chiamano così com'è.
/// Lo schermo è gdigrab: un monitor, una zona, oppure una finestra per titolo. Il microfono è DirectShow (<c>audio=nome</c>),
/// l'audio del PC è WASAPI in loopback. I tempi sono quelli dell'orologio, così video e audio restano
/// allineati anche quando una sorgente tace un attimo: è la stessa idea della registrazione di DaProd
/// Editor Video, che riempie di silenzio i buchi del loopback. I nomi dei dispositivi sono un argomento
/// solo, senza virgolette aggiunte: con ArgumentList non c'è una shell, e il parser di dshow (spezza su
/// «=» e «:», e non toglie le virgolette) le terrebbe come parte del nome.
/// </summary>
public static class ComandiRegistrazione
{
    public const string ApriMenu = "menu";
    public const string PortaBarra = "barra";

    /// <summary>Da dove arriva «Registra»: il vassoio, il tasto destro dell'avatar, il comando del ponte. Stessa risposta.</summary>
    public static readonly string[] Origini = ["vassoio", "avatar", "ponte"];

    /// <summary>Il monitor da catturare, in pixel veri (come li vuole gdigrab). X e Y possono essere negativi.</summary>
    public sealed record Schermo(int X, int Y, int Larghezza, int Altezza);

    /// <summary>Menu se non si sta registrando, barra se la registrazione è già partita.</summary>
    public static string CosaFa(string origine, bool inCorso)
    {
        _ = origine;
        return inCorso ? PortaBarra : ApriMenu;
    }

    /// <summary>Solo audio senza ingressi, una zona non scelta o una finestra non scelta: non si parte.</summary>
    public static string? MotivoRifiuto(string modo, string? mic, string? audioPc, bool zonaSenzaRettangolo = false, bool finestraSenzaTitolo = false)
    {
        if (modo == "audio" && string.IsNullOrWhiteSpace(mic) && string.IsNullOrWhiteSpace(audioPc))
            return "Per registrare solo l'audio serve il microfono o l'audio del PC.";
        if (modo == "zona" && zonaSenzaRettangolo) return "Scegli la zona da registrare.";
        if (modo == "finestra" && finestraSenzaTitolo) return "Scegli la finestra da registrare.";
        return null;
    }

    /// <summary>30 oppure 60. Un numero in mezzo va a 30, uno alto a 60.</summary>
    public static int FpsDi(int fps) => fps >= 45 ? 60 : 30;

    /// <summary>Alta, buona, leggera: il CRF di libx264. Il resto è «buona».</summary>
    public static int CrfDi(string? qualita) => qualita switch
    {
        "alta" => 18,
        "leggera" => 28,
        _ => 23,
    };

    /// <summary>Un argomento solo: <c>audio=Microphone (Realtek Audio)</c>, senza virgolette intorno.</summary>
    public static string ArgomentoDshow(string nome) => "audio=" + Pulisci(nome);

    /// <summary>Il nome che WASAPI elenca, tale e quale, un argomento solo.</summary>
    public static string ArgomentoWasapi(string nome) => Pulisci(nome);

    public static IReadOnlyList<string> Argomenti(string modo, string? mic, string? audioPc, string uscita, Schermo? schermo, int fps = 30, int crf = 23, bool cursore = true, string? titoloFinestra = null)
    {
        var soloAudio = modo == "audio";
        var finestra = modo == "finestra" && !string.IsNullOrWhiteSpace(titoloFinestra);
        var fotogrammi = FpsDi(fps);
        var qualita = Math.Clamp(crf, 16, 32);
        var a = new List<string> { "-hide_banner", "-y" };
        if (!soloAudio)
        {
            Ingresso(a);
            a.Add("-f"); a.Add("gdigrab");
            a.Add("-framerate"); a.Add(fotogrammi.ToString(CultureInfo.InvariantCulture));
            a.Add("-draw_mouse"); a.Add(cursore ? "1" : "0");
            // una finestra si prende per titolo: offset e misura non c'entrano (gdigrab segue quella finestra)
            if (!finestra && schermo is { Larghezza: > 1, Altezza: > 1 })
            {
                a.Add("-offset_x"); a.Add(schermo.X.ToString(CultureInfo.InvariantCulture));
                a.Add("-offset_y"); a.Add(schermo.Y.ToString(CultureInfo.InvariantCulture));
                a.Add("-video_size"); a.Add($"{Pari(schermo.Larghezza)}x{Pari(schermo.Altezza)}");
            }
            a.Add("-i"); a.Add(finestra ? "title=" + Pulisci(titoloFinestra!) : "desktop");
        }
        var micOk = !string.IsNullOrWhiteSpace(mic);
        var pcOk = !string.IsNullOrWhiteSpace(audioPc);
        if (micOk)
        {
            Ingresso(a);
            a.Add("-f"); a.Add("dshow");
            a.Add("-i"); a.Add(ArgomentoDshow(mic!));
        }
        if (pcOk)
        {
            Ingresso(a);
            a.Add("-f"); a.Add("wasapi");
            a.Add("-loopback"); a.Add("1");
            a.Add("-i"); a.Add(ArgomentoWasapi(audioPc!));
        }
        var nAudio = (micOk ? 1 : 0) + (pcOk ? 1 : 0);
        if (!soloAudio)
        {
            // -vf e -filter_complex insieme FFmpeg li rifiuta: con due audio la scala sta nel filtro
            if (nAudio == 2)
            {
                a.Add("-filter_complex");
                a.Add("[0:v]scale=trunc(iw/2)*2:trunc(ih/2)*2[v];[1:a][2:a]amix=inputs=2:duration=longest:dropout_transition=0[a]");
                a.Add("-map"); a.Add("[v]");
                a.Add("-map"); a.Add("[a]");
            }
            else
            {
                a.Add("-vf"); a.Add("scale=trunc(iw/2)*2:trunc(ih/2)*2");
                if (nAudio == 1)
                {
                    a.Add("-map"); a.Add("0:v");
                    a.Add("-map"); a.Add("1:a");
                }
            }
            a.Add("-c:v"); a.Add("libx264");
            a.Add("-preset"); a.Add("veryfast");
            a.Add("-crf"); a.Add(qualita.ToString(CultureInfo.InvariantCulture));
            a.Add("-pix_fmt"); a.Add("yuv420p");
            if (nAudio > 0) { a.Add("-c:a"); a.Add("aac"); a.Add("-b:a"); a.Add("160k"); }
        }
        else
        {
            if (nAudio == 2)
            {
                a.Add("-filter_complex");
                a.Add("[0:a][1:a]amix=inputs=2:duration=longest:dropout_transition=0[a]");
                a.Add("-map"); a.Add("[a]");
            }
            a.Add("-c:a"); a.Add("aac");
            a.Add("-b:a"); a.Add("192k");
        }
        a.Add(uscita);
        return a;
    }

    /// <summary>La riga che finisce nel registro. Gli argomenti con spazi prendono le virgolette solo qui, per leggerli.</summary>
    public static string Riga(IEnumerable<string> argomenti) =>
        string.Join(' ', argomenti.Select(x => x.Any(char.IsWhiteSpace) ? "\"" + x.Replace("\"", "\\\"") + "\"" : x));

    /// <summary>I microfoni dall'elenco di <c>ffmpeg -f dshow -list_devices true</c> (esce su stderr).</summary>
    public static IReadOnlyList<string> MicrofoniDaElenco(string testo)
    {
        var audio = false;
        var nomi = new List<string>();
        foreach (var riga in testo.Split('\n'))
        {
            if (riga.Contains("DirectShow audio", StringComparison.OrdinalIgnoreCase)) { audio = true; continue; }
            if (riga.Contains("DirectShow video", StringComparison.OrdinalIgnoreCase)) { audio = false; continue; }
            if (!audio || riga.Contains("Alternative name", StringComparison.OrdinalIgnoreCase)) continue;
            if (NomeFraVirgolette(riga) is { } n && !nomi.Contains(n)) nomi.Add(n);
        }
        return nomi;
    }

    /// <summary>Le uscite del PC (playback) dall'elenco WASAPI. I microfoni, segnati capture, non ci vanno.</summary>
    public static IReadOnlyList<string> UsciteDaElenco(string testo)
    {
        var tutti = new List<string>();
        var playback = new List<string>();
        foreach (var riga in testo.Split('\n'))
        {
            if (riga.Contains("(capture)", StringComparison.OrdinalIgnoreCase)) continue;
            if (NomeFraVirgolette(riga) is not { } n) continue;
            if (!tutti.Contains(n)) tutti.Add(n);
            if (riga.Contains("playback", StringComparison.OrdinalIgnoreCase) && !playback.Contains(n)) playback.Add(n);
        }
        return playback.Count > 0 ? playback : tutti;
    }

    static void Ingresso(List<string> a)
    {
        a.Add("-thread_queue_size"); a.Add("1024");
        a.Add("-use_wallclock_as_timestamps"); a.Add("1");
    }

    /// <summary>libx264 con yuv420p vuole larghezza e altezza pari. 1921 diventa 1920.</summary>
    public static int Pari(int n) => Math.Max(2, n - (n & 1));

    /// <summary>I titoli da proporre per «una finestra». Si saltano quelli vuoti e le finestre nostre di registrazione.</summary>
    public static IReadOnlyList<string> TitoliUtili(IEnumerable<string> titoli)
    {
        var visti = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lista = new List<string>();
        foreach (var t in titoli)
        {
            var n = t.Trim();
            if (n.Length < 2) continue;
            if (n.Equals("Registra", StringComparison.OrdinalIgnoreCase) || n.Equals("Registrazione", StringComparison.OrdinalIgnoreCase)) continue;
            if (n.Equals("Program Manager", StringComparison.OrdinalIgnoreCase)) continue;
            if (visti.Add(n)) lista.Add(n);
        }
        return lista;
    }

    /// <summary>Un fotogramma piccolo, per la miniatura nei recenti. Esce su stdout in PNG.</summary>
    public static IReadOnlyList<string> ArgomentiFotogramma(string percorso) =>
    [
        "-hide_banner", "-loglevel", "error", "-ss", "0.4", "-i", percorso,
        "-frames:v", "1", "-vf", "scale=160:-2", "-f", "image2pipe", "-vcodec", "png", "-",
    ];

    static string Pulisci(string nome)
    {
        var n = nome.Trim();
        if (n.Length >= 2 && n[0] == '"' && n[^1] == '"') n = n[1..^1].Trim();
        return n.Replace("\"", "");
    }

    static string? NomeFraVirgolette(string riga)
    {
        var a = riga.IndexOf('"');
        var b = a >= 0 ? riga.IndexOf('"', a + 1) : -1;
        if (a < 0 || b <= a) return null;
        var n = riga[(a + 1)..b].Trim();
        return n.Length == 0 ? null : n;
    }
}
