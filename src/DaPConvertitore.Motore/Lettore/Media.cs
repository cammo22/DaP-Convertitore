using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DaP.Convertitore.Lettore;

/// <summary>Quello che serve al lettore per un video o una canzone: tracce, durata, titolo, artista, copertina.</summary>
public sealed record SchedaMedia(
    InfoMedia Info,
    IReadOnlyDictionary<string, string> Tag,
    IReadOnlyList<(int Indice, string? Lingua, string? Titolo, string Codec)> Audio,
    IReadOnlyList<(int Indice, string? Lingua, string? Titolo, string Codec, bool Testo)> Sottotitoli,
    IReadOnlyList<(string Inizio, string Titolo)> Capitoli);

/// <summary>
/// Video e musica nel lettore. Quello che WebView2 suona da sé (MP4, WEBM, MP3, FLAC…) passa tale e quale; il resto
/// (AVI, WMV, MKV con dentro cose strane, WMA, AC3…) lo traduce FFmpeg al volo: il video in un flusso MP4 a pezzi
/// che la pagina mette in fila (Media Source), la musica in un FLAC in cache che poi si scorre come un file vero.
/// </summary>
public static class Media
{
    static readonly CultureInfo inv = CultureInfo.InvariantCulture;

    public static async Task<SchedaMedia> Leggi(Strumenti s, string percorso)
    {
        var r = await Processi.Esegui(s.Ffprobe,
            ["-v", "error", "-print_format", "json", "-show_format", "-show_streams", "-show_chapters", "-i", percorso], CancellationToken.None);
        if (r.Codice != 0 || string.IsNullOrWhiteSpace(r.Uscita))
            throw new ErroreConversione("Questo file non si riesce a leggere: forse è rovinato o non è un file multimediale.", r.Errori);
        var info = Sonda.Interpreta(r.Uscita);
        using var doc = JsonDocument.Parse(r.Uscita);
        var radice = doc.RootElement;
        var tag = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (radice.TryGetProperty("format", out var f) && f.TryGetProperty("tags", out var t))
            foreach (var p in t.EnumerateObject()) tag[p.Name] = p.Value.ToString();
        var audio = new List<(int, string?, string?, string)>();
        var sub = new List<(int, string?, string?, string, bool)>();
        if (radice.TryGetProperty("streams", out var flussi))
        {
            int na = 0, ns = 0;
            foreach (var st in flussi.EnumerateArray())
            {
                var tipo = st.TryGetProperty("codec_type", out var ct) ? ct.GetString() : null;
                var codec = st.TryGetProperty("codec_name", out var cn) ? cn.GetString() ?? "" : "";
                string? lingua = null, titolo = null;
                if (st.TryGetProperty("tags", out var stt))
                    foreach (var p in stt.EnumerateObject())
                    {
                        // anche i tag dei flussi (OGG e OPUS mettono lì titolo e artista)
                        if (p.NameEquals("language")) lingua = p.Value.GetString();
                        else if (p.NameEquals("title")) titolo = p.Value.GetString();
                        else if (tipo == "audio" && !tag.ContainsKey(p.Name)) tag[p.Name] = p.Value.ToString();
                    }
                if (tipo == "audio") audio.Add((na++, lingua, titolo, codec));
                else if (tipo == "subtitle") sub.Add((ns++, lingua, titolo, codec, new InfoSottotitolo(0, codec, null).Testo));
            }
        }
        var capitoli = new List<(string, string)>();
        if (radice.TryGetProperty("chapters", out var cap))
            foreach (var c in cap.EnumerateArray())
            {
                var inizio = c.TryGetProperty("start_time", out var st) ? st.GetString() ?? "0" : "0";
                var titolo = c.TryGetProperty("tags", out var ctg) && ctg.TryGetProperty("title", out var tt) ? tt.GetString() ?? "" : "";
                capitoli.Add((inizio, titolo));
            }
        return new SchedaMedia(info, tag, audio, sub, capitoli);
    }

    /// <summary>La copertina: quella dentro il file, o cover.jpg / folder.jpg nella cartella. JPEG piccolo.</summary>
    public static async Task<byte[]?> Copertina(Strumenti s, string percorso, bool dentro)
    {
        if (dentro)
        {
            var jpg = Path.Combine(Strumenti.CartellaTemporanea, $"copertina-{Guid.NewGuid():N}.jpg");
            try
            {
                var r = await Processi.Esegui(s.Ffmpeg, ["-hide_banner", "-loglevel", "error", "-y", "-i", percorso, "-map", "0:v:0", "-frames:v", "1",
                    "-vf", "scale='min(800,iw)':-2", "-q:v", "3", "-update", "1", jpg], CancellationToken.None, tieniUscita: false);
                if (r.Codice == 0 && File.Exists(jpg)) return await File.ReadAllBytesAsync(jpg);
            }
            finally { try { File.Delete(jpg); } catch { } }
        }
        var cartella = Path.GetDirectoryName(percorso)!;
        foreach (var nome in new[] { "cover.jpg", "folder.jpg", "cover.png", "front.jpg", "album.jpg", "Cover.jpg", "Folder.jpg" })
        {
            var p = Path.Combine(cartella, nome);
            if (File.Exists(p) && new FileInfo(p).Length < 20_000_000) return await File.ReadAllBytesAsync(p);
        }
        return null;
    }

    /// <summary>
    /// La forma d'onda: FFmpeg dà l'audio mono a 4000 campioni al secondo, si tiene il picco di ogni fetta.
    /// Valori fra 0 e 1, già in scala "come la sente l'orecchio".
    /// </summary>
    public static async Task<float[]> Onda(Strumenti s, string percorso, int fette = 1600, int traccia = 0)
    {
        var raw = Path.Combine(Strumenti.CartellaTemporanea, $"onda-{Guid.NewGuid():N}.raw");
        try
        {
            var r = await Processi.Esegui(s.Ffmpeg, ["-hide_banner", "-loglevel", "error", "-y", "-i", percorso, "-map", $"0:a:{traccia}", "-vn", "-sn",
                "-ac", "1", "-ar", "4000", "-f", "s16le", raw], CancellationToken.None, tieniUscita: false);
            if (r.Codice != 0 || !File.Exists(raw)) return [];
            var b = await File.ReadAllBytesAsync(raw);
            var campioni = b.Length / 2;
            if (campioni == 0) return [];
            var picchi = new float[fette];
            for (var i = 0; i < fette; i++)
            {
                long da = (long)i * campioni / fette, a = Math.Max(da + 1, (long)(i + 1) * campioni / fette);
                var max = 0;
                for (var j = da; j < a && j < campioni; j++)
                {
                    var v = Math.Abs((int)BitConverter.ToInt16(b, (int)(j * 2)));
                    if (v > max) max = v;
                }
                picchi[i] = max / 32768f;
            }
            var top = Math.Max(0.05f, picchi.Max());
            for (var i = 0; i < fette; i++) picchi[i] = MathF.Pow(picchi[i] / top, 0.7f);
            return picchi;
        }
        finally { try { File.Delete(raw); } catch { } }
    }

    /// <summary>I sottotitoli in WebVTT, quello che capisce il tag &lt;track&gt;: da un file accanto o da dentro il video.</summary>
    public static async Task<string> Vtt(Strumenti s, string percorso, int? traccia)
    {
        var args = new List<string> { "-hide_banner", "-loglevel", "error", "-i", percorso };
        if (traccia is { } t) args.AddRange(["-map", $"0:s:{t}"]);
        args.AddRange(["-f", "webvtt", "-"]);
        var r = await Processi.Esegui(s.Ffmpeg, args, CancellationToken.None);
        if (r.Codice != 0 || r.Uscita.Length == 0) throw new ErroreConversione("Questi sottotitoli non si leggono.", r.Errori);
        return r.Uscita;
    }

    /// <summary>I sottotitoli accanto al video: "Film.srt", "Film.it.srt", "Film.eng.vtt"…</summary>
    public static IReadOnlyList<string> SottotitoliAccanto(string video)
    {
        try
        {
            var radice = Path.GetFileNameWithoutExtension(video);
            return Directory.EnumerateFiles(Path.GetDirectoryName(video)!)
                .Where(f => Path.GetExtension(f).ToLowerInvariant() is ".srt" or ".vtt" or ".ass" or ".ssa")
                .Where(f => Path.GetFileName(f).StartsWith(radice, StringComparison.OrdinalIgnoreCase))
                .Order().ToList();
        }
        catch { return []; }
    }

    // ——— la cache del lettore: la musica tradotta in FLAC, i documenti impaginati in PDF ———

    public static string CartellaCache
    {
        get
        {
            var c = Path.Combine(Strumenti.CartellaDati, "cache", "lettore");
            Directory.CreateDirectory(c);
            return c;
        }
    }

    /// <summary>Il nome in cache dipende da percorso, peso e data: se il file cambia, si rifà.</summary>
    public static string InCache(string percorso, string estensione)
    {
        var fi = new FileInfo(percorso);
        var chiave = $"{fi.FullName.ToLowerInvariant()}|{fi.Length}|{fi.LastWriteTimeUtc.Ticks}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(chiave)))[..24].ToLowerInvariant();
        return Path.Combine(CartellaCache, hash + estensione);
    }

    /// <summary>Via quello che in cache non si tocca da una settimana: la cache non cresce per sempre.</summary>
    public static void PulisciCache()
    {
        try
        {
            foreach (var f in Directory.EnumerateFiles(CartellaCache))
                if (File.GetLastAccessTimeUtc(f) < DateTime.UtcNow.AddDays(-7) && File.GetLastWriteTimeUtc(f) < DateTime.UtcNow.AddDays(-7))
                    try { File.Delete(f); } catch { }
        }
        catch { }
    }

    /// <summary>La musica che WebView2 non suona (WMA, AC3, APE, ALAC…) diventa FLAC in cache, senza perdere niente.</summary>
    public static async Task<string> AudioInCache(Strumenti s, string percorso, int traccia = 0)
    {
        var uscita = InCache(percorso, $"-a{traccia}.flac");
        if (File.Exists(uscita)) { File.SetLastAccessTimeUtc(uscita, DateTime.UtcNow); return uscita; }
        var bozza = uscita + ".tmp";
        var r = await Processi.Esegui(s.Ffmpeg, ["-hide_banner", "-loglevel", "error", "-y", "-i", percorso, "-map", $"0:a:{traccia}", "-vn", "-sn",
            "-c:a", "flac", "-compression_level", "0", "-f", "flac", bozza], CancellationToken.None, tieniUscita: false);
        if (r.Codice != 0 || !File.Exists(bozza)) throw new ErroreConversione("Questo audio non si riesce a suonare.", r.Errori);
        File.Move(bozza, uscita, true);
        return uscita;
    }
}

/// <summary>
/// Un video tradotto al volo per la pagina: FFmpeg parte da un punto (lo scorrimento oltre quello già pronto fa
/// ripartire tutto da lì) e scrive un MP4 a frammenti; la pagina chiede i pezzi uno dopo l'altro e li mette in fila.
/// FFmpeg non corre troppo avanti: se la pagina ha già 48 MB da mangiare, aspetta.
/// </summary>
public sealed class Flusso : IDisposable
{
    readonly Process p;
    readonly List<byte[]?> pezzi = [];
    readonly SemaphoreSlim nuovo = new(0);
    readonly CancellationTokenSource fine = new();
    long prodotti, consumati;
    bool finito;

    public string Id { get; } = Guid.NewGuid().ToString("N")[..12];
    public double Da { get; }
    public string Mime { get; }
    public string Errori { get; private set; } = "";

    Flusso(Process p, double da, string mime)
    {
        this.p = p;
        Da = da;
        Mime = mime;
    }

    /// <summary>
    /// Copia il video quando la pagina lo sa già leggere (H.264 di solito: lo dice lei con <paramref name="copiaVideo"/>),
    /// se no lo rifà in H.264: con la scheda video se c'è, se no con la CPU in fretta. L'audio esce sempre AAC stereo.
    /// </summary>
    public static Flusso Avvia(Strumenti s, InfoHardware hw, string percorso, InfoMedia info, double da, int tracciaAudio, bool copiaVideo)
    {
        var a = new List<string> { "-hide_banner", "-loglevel", "error", "-nostdin" };
        if (da > 0) a.AddRange(["-ss", da.ToString("0.###", CultureInfo.InvariantCulture)]);
        a.AddRange(["-i", percorso]);
        var conVideo = info.Video is not null;
        if (conVideo) a.AddRange(["-map", "0:V:0"]);
        if (info.Audio.Count > 0) a.AddRange(["-map", $"0:a:{Math.Clamp(tracciaAudio, 0, info.Audio.Count - 1)}"]);
        a.Add("-sn");
        if (conVideo)
        {
            var v = info.Video!;
            if (copiaVideo) a.AddRange(["-c:v", "copy"]);
            else
            {
                var filtri = new List<string>();
                var lungo = Math.Max(v.Larghezza, v.Altezza);
                if (v.Hdr)
                {
                    // l'HDR diventa SDR coi colori giusti; prima si rimpicciolisce, se no la CPU non sta al passo
                    if (lungo > 1920) filtri.Add(v.Larghezza >= v.Altezza ? "scale=1920:-2" : "scale=-2:1920");
                    filtri.Add("zscale=t=linear:npl=100,format=gbrpf32le,zscale=p=bt709,tonemap=tonemap=hable:desat=0,zscale=t=bt709:m=bt709:r=tv");
                }
                else if (lungo > 3840) filtri.Add(v.Larghezza >= v.Altezza ? "scale=3840:-2" : "scale=-2:3840");
                filtri.Add("format=yuv420p");
                a.AddRange(["-vf", string.Join(',', filtri)]);
                if (hw.Ha("h264_nvenc")) a.AddRange(["-c:v", "h264_nvenc", "-preset", "p2", "-tune", "ll", "-rc", "vbr", "-cq", "21", "-b:v", "0", "-g", "60"]);
                else if (hw.Ha("h264_qsv")) a.AddRange(["-c:v", "h264_qsv", "-preset", "veryfast", "-global_quality", "22", "-g", "60"]);
                else if (hw.Ha("h264_amf")) a.AddRange(["-c:v", "h264_amf", "-quality", "speed", "-rc", "cqp", "-qp_i", "21", "-qp_p", "23", "-g", "60"]);
                else a.AddRange(["-c:v", "libx264", "-preset", "veryfast", "-tune", "zerolatency", "-crf", "21", "-g", "60"]);
                a.AddRange(["-profile:v", "high"]);
            }
        }
        if (info.Audio.Count > 0) a.AddRange(["-c:a", "aac", "-b:a", "192k", "-ac", "2"]);
        a.AddRange(["-f", "mp4", "-movflags", "frag_keyframe+empty_moov+default_base_moof", "-frag_duration", "500000", "pipe:1"]);

        var psi = new ProcessStartInfo(s.Ffmpeg)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var x in a) psi.ArgumentList.Add(x);
        Registro.Scrivi($"> lettore ffmpeg {string.Join(' ', a)}");
        var p = Process.Start(psi) ?? throw new ErroreConversione("FFmpeg non parte.");
        var codecVideo = conVideo ? "avc1.640028" : null;
        var mime = conVideo
            ? $"video/mp4; codecs=\"{codecVideo}{(info.Audio.Count > 0 ? ",mp4a.40.2" : "")}\""
            : "audio/mp4; codecs=\"mp4a.40.2\"";
        var f = new Flusso(p, da, mime);
        _ = f.Leggi();
        _ = Task.Run(async () =>
        {
            try { f.Errori = await p.StandardError.ReadToEndAsync(); } catch { }
        });
        return f;
    }

    async Task Leggi()
    {
        var uscita = p.StandardOutput.BaseStream;
        var buf = new byte[256 * 1024];
        using var raccolta = new MemoryStream();
        var ultimo = Stopwatch.StartNew();
        try
        {
            while (!fine.IsCancellationRequested)
            {
                // la pagina è indietro: FFmpeg aspetta (il tubo si riempie e lui si ferma da solo)
                while (Interlocked.Read(ref prodotti) - Interlocked.Read(ref consumati) > 48L * 1024 * 1024 && !fine.IsCancellationRequested)
                    await Task.Delay(100, fine.Token);
                var n = await uscita.ReadAsync(buf, fine.Token);
                if (n == 0) break;
                raccolta.Write(buf, 0, n);
                if (raccolta.Length >= 512 * 1024 || ultimo.ElapsedMilliseconds > 250)
                {
                    Metti(raccolta.ToArray());
                    raccolta.SetLength(0);
                    ultimo.Restart();
                }
            }
            if (raccolta.Length > 0) Metti(raccolta.ToArray());
        }
        catch (OperationCanceledException) { }
        catch (Exception e) { Registro.Scrivi($"lettore: flusso interrotto ({e.Message})"); }
        finally
        {
            finito = true;
            nuovo.Release(1000);
        }
    }

    void Metti(byte[] b)
    {
        lock (pezzi) pezzi.Add(b);
        Interlocked.Add(ref prodotti, b.Length);
        nuovo.Release();
    }

    /// <summary>Il pezzo n (dal primo, 0). null quando il video è finito o il flusso è chiuso.</summary>
    public async Task<byte[]?> Pezzo(int n, CancellationToken ct = default)
    {
        while (true)
        {
            lock (pezzi)
            {
                if (n < pezzi.Count)
                {
                    var b = pezzi[n];
                    if (b is null) return [];
                    pezzi[n] = null; // la pagina l'ha preso: la memoria si libera
                    Interlocked.Add(ref consumati, b.Length);
                    return b;
                }
                if (finito) return null;
            }
            await nuovo.WaitAsync(TimeSpan.FromSeconds(1), ct);
        }
    }

    public void Dispose()
    {
        fine.Cancel();
        try { if (!p.HasExited) p.Kill(true); } catch { }
        p.Dispose();
    }
}
