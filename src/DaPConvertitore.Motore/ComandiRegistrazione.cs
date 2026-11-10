using System.Globalization;
using System.Text;

namespace DaP.Convertitore;

/// <summary>
/// Il comando di FFmpeg della registrazione, e cosa deve fare «Registra» dal vassoio, dall'avatar o dal ponte.
/// Niente finestre: le prove lo chiamano così com'è.
/// Lo schermo è gdigrab (monitor, zona o finestra per titolo). L'audio non passa da FFmpeg: questa build
/// ha dshow, gdigrab, lavfi, openal e vfwcap, e non ha WASAPI. Microfono e audio del PC li prende il
/// programma con WASAPI, li mescola e alla fine FFmpeg unisce il WAV al video. I nomi dei dispositivi
/// non finiscono nel comando.
/// </summary>
public static class ComandiRegistrazione
{
    public const string ApriMenu = "menu";
    public const string PortaBarra = "barra";

    /// <summary>BitBlt dell'anteprima: solo SRCCOPY. CAPTUREBLT (0x40000000) fa sfarfallare il cursore.</summary>
    public const int RopAnteprima = 0x00CC0020;
    public const int RopCaptureBlt = 0x40000000;

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

    /// <summary>Solo il video. Niente dshow e niente wasapi: l'audio arriva dopo, da un WAV.</summary>
    public static IReadOnlyList<string> ArgomentiVideo(string modo, string uscita, Schermo? schermo, int fps = 30, int crf = 23, bool cursore = true, string? titoloFinestra = null)
    {
        var finestra = modo == "finestra" && !string.IsNullOrWhiteSpace(titoloFinestra);
        var fotogrammi = FpsDi(fps);
        var qualita = Math.Clamp(crf, 16, 32);
        var a = new List<string> { "-hide_banner", "-y" };
        Ingresso(a);
        a.Add("-f"); a.Add("gdigrab");
        a.Add("-framerate"); a.Add(fotogrammi.ToString(CultureInfo.InvariantCulture));
        a.Add("-draw_mouse"); a.Add(cursore ? "1" : "0");
        if (!finestra && schermo is { Larghezza: > 1, Altezza: > 1 })
        {
            a.Add("-offset_x"); a.Add(schermo.X.ToString(CultureInfo.InvariantCulture));
            a.Add("-offset_y"); a.Add(schermo.Y.ToString(CultureInfo.InvariantCulture));
            a.Add("-video_size"); a.Add($"{Pari(schermo.Larghezza)}x{Pari(schermo.Altezza)}");
        }
        a.Add("-i"); a.Add(finestra ? "title=" + Pulisci(titoloFinestra!) : "desktop");
        a.Add("-vf"); a.Add("scale=trunc(iw/2)*2:trunc(ih/2)*2");
        a.Add("-c:v"); a.Add("libx264");
        a.Add("-preset"); a.Add("veryfast");
        a.Add("-crf"); a.Add(qualita.ToString(CultureInfo.InvariantCulture));
        a.Add("-pix_fmt"); a.Add("yuv420p");
        a.Add(uscita);
        return a;
    }

    /// <summary>Unisce il video già pronto e il WAV mescolato dal programma. L'audio si allunga di silenzio se è più corto, e si taglia alla fine del video.</summary>
    public static IReadOnlyList<string> ArgomentiMux(string video, string wav, string uscita) =>
    [
        "-hide_banner", "-y",
        "-i", video,
        "-i", wav,
        "-filter_complex", "[1:a]apad[a]",
        "-map", "0:v",
        "-map", "[a]",
        "-c:v", "copy",
        "-c:a", "aac",
        "-b:a", "160k",
        "-shortest",
        uscita,
    ];

    /// <summary>Solo audio: il WAV diventa M4A.</summary>
    public static IReadOnlyList<string> ArgomentiDaWav(string wav, string uscita) =>
    [
        "-hide_banner", "-y", "-i", wav, "-c:a", "aac", "-b:a", "192k", uscita,
    ];

    /// <summary>La riga che finisce nel registro. Gli argomenti con spazi prendono le virgolette solo qui, per leggerli.</summary>
    public static string Riga(IEnumerable<string> argomenti) =>
        string.Join(' ', argomenti.Select(x => x.Any(char.IsWhiteSpace) ? "\"" + x.Replace("\"", "\\\"") + "\"" : x));

    /// <summary>
    /// I microfoni dall'elenco di <c>ffmpeg -f dshow -list_devices true</c> (esce su stderr).
    /// Vale il formato vecchio (intestazione «DirectShow audio devices») e quello nuovo di FFmpeg 8/9,
    /// che non ha intestazioni e scrive <c>[in#0 @ …] "nome" (audio)</c>. I video restano fuori.
    /// Le righe «Alternative name» si saltano. Il nome è quello fra la prima e l'ultima virgoletta,
    /// così le parentesi dentro il nome (Realtek(R), Oculus) non lo spezzano.
    /// </summary>
    public static IReadOnlyList<string> MicrofoniDaElenco(string testo)
    {
        var sezione = 0; // 0 non si sa, 1 video, 2 audio (solo il formato vecchio)
        var nomi = new List<string>();
        foreach (var grezza in testo.Split('\n'))
        {
            var riga = grezza.TrimEnd('\r').Trim();
            if (riga.Length == 0) continue;
            if (riga.Contains("Alternative name", StringComparison.OrdinalIgnoreCase)) continue;
            if (riga.Contains("DirectShow video", StringComparison.OrdinalIgnoreCase)) { sezione = 1; continue; }
            if (riga.Contains("DirectShow audio", StringComparison.OrdinalIgnoreCase)) { sezione = 2; continue; }
            if (NomeFraVirgolette(riga) is not { } n) continue;
            var audio = HaParola(riga, "audio");
            var video = HaParola(riga, "video");
            if (video && !audio) continue;
            if (audio || sezione == 2)
            {
                if (!nomi.Contains(n)) nomi.Add(n);
            }
        }
        return nomi;
    }

    /// <summary>Le uscite del PC (playback) dall'elenco WASAPI, se un FFmpeg ce l'ha. I microfoni, segnati capture, non ci vanno. Questa build non ha wasapi: l'app non lo chiama.</summary>
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

    /// <summary>
    /// L'uscita di <c>ffmpeg -devices</c>. Dice se quell'ingresso c'è (per esempio wasapi).
    /// Si guarda la colonna del nome, non una parola capitata nel mezzo di una riga.
    /// </summary>
    public static bool HaIngresso(string testoDevices, string nome)
    {
        foreach (var grezza in testoDevices.Split('\n'))
        {
            var riga = grezza.TrimEnd('\r').Trim();
            if (riga.Length < 3 || riga[1] != ' ') continue;
            var pezzi = riga.Split((char[])[' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            if (pezzi.Length >= 2 && pezzi[1].Equals(nome, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    /// <summary>
    /// I byte di stderr di FFmpeg. Prima UTF-8 (anche col BOM): è così che scrive la build BtbN.
    /// Se i byte non sono UTF-8 validi si usa Latin-1, così un accento non diventa un punto di domanda.
    /// </summary>
    public static string DecodificaElenco(ReadOnlySpan<byte> dati)
    {
        if (dati.Length >= 3 && dati[0] == 0xEF && dati[1] == 0xBB && dati[2] == 0xBF) dati = dati[3..];
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        try { return utf8.GetString(dati); }
        catch (DecoderFallbackException) { return Encoding.Latin1.GetString(dati); }
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

    /// <summary>Un fotogramma piccolo, per la miniatura nei recenti e nel riepilogo. Esce su stdout in PNG.</summary>
    public static IReadOnlyList<string> ArgomentiFotogramma(string percorso) =>
    [
        "-hide_banner", "-loglevel", "error", "-ss", "0.4", "-i", percorso,
        "-frames:v", "1", "-vf", "scale=320:-2", "-f", "image2pipe", "-vcodec", "png", "-",
    ];

    static string Pulisci(string nome)
    {
        var n = nome.Trim();
        if (n.Length >= 2 && n[0] == '"' && n[^1] == '"') n = n[1..^1].Trim();
        return n.Replace("\"", "");
    }

    /// <summary>Dalla prima virgoletta all'ultima: il nome può contenere parentesi, non una seconda coppia di virgolette.</summary>
    static string? NomeFraVirgolette(string riga)
    {
        var a = riga.IndexOf('"');
        var b = a >= 0 ? riga.LastIndexOf('"') : -1;
        if (a < 0 || b <= a) return null;
        var n = riga[(a + 1)..b].Trim();
        return n.Length == 0 ? null : n;
    }

    /// <summary><c>(audio)</c> o <c>(video)</c> come parola del formato nuovo, non un pezzo del nome fra virgolette.</summary>
    static bool HaParola(string riga, string parola)
    {
        var fine = riga.LastIndexOf('"');
        var dopo = fine >= 0 ? riga[(fine + 1)..] : riga;
        return dopo.Contains("(" + parola + ")", StringComparison.OrdinalIgnoreCase);
    }
}
