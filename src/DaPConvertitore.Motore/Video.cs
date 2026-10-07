using System.Globalization;

namespace DaP.Convertitore;

/// <summary>
/// I video: MP4, MKV, WEBM, MOV e GIF. Il piano lo fa <see cref="PianoVideo"/>; qui si traduce in FFmpeg.
/// Se la scheda video si rifiuta si riprova con meno pretese e poi con la CPU; se col peso si sfora,
/// si rifà più stretto da solo.
/// </summary>
public static class Video
{
    static string N(double x) => x.ToString("0.###", CultureInfo.InvariantCulture);

    public static async Task Converti(Lavoro l, Contesto ctx)
    {
        var est = l.Formato.Estensione;
        ctx.Fase("Leggo il video");
        var info = await Sonda.Leggi(ctx.Strumenti, l.Sorgente, ctx.Ct);
        if (info.Video is null) throw new ErroreConversione("In questo file non c'è un video.");

        if (est == ".gif") { await Gif(l, ctx, info); return; }

        var o = l.Opzioni.Video;
        var piano = PianoVideo.Calcola(info, l.PesoPrima, Path.GetExtension(l.Sorgente), est, o, ctx.Hardware);
        Registro.Scrivi($"Piano: {piano.Modo} {piano.Encoder} {piano.Larghezza}x{piano.Altezza} {piano.BitrateVideo / 1000} kbps crf {piano.Crf} audio {piano.AudioKbps} → {piano.Giudizio}");

        if (piano.Modo == "copia")
        {
            await Esegui(l, ctx, info, piano, piano.Encoder, avanzate: false, hwDecodifica: false, passata: 0);
            return;
        }

        // i tentativi, dal migliore al più prudente
        var tentativi = new List<(string enc, bool avanzate, bool hwdec)>();
        if (piano.Hardware)
        {
            tentativi.Add((piano.Encoder, true, true));
            tentativi.Add((piano.Encoder, false, false));
        }
        var cpu = PianoVideo.ScegliEncoder(piano.Codec, "cpu", ctx.Hardware) ?? "libx264";
        tentativi.Add((cpu, true, false));

        var obiettivo = piano.Modo == "peso" ? o.PesoByte : 0;
        // NVENC & co. in VBR sforano di un 1-2%: si parte un filo più bassi, così di solito basta un giro solo
        // (rifare un film di due ore perché ha sforato di 3 MB non va bene). Il secondo giro resta la rete.
        var bitrate = piano.Hardware && piano.Modo == "peso" ? (long)(piano.BitrateVideo * 0.97) : piano.BitrateVideo;
        for (var giro = 0; ; giro++)
        {
            Exception? ultimo = null;
            foreach (var (enc, avanzate, hwdec) in tentativi)
            {
                try
                {
                    var p = piano with { BitrateVideo = bitrate, Encoder = enc, Hardware = PianoVideo.EHardware(enc), Crf = piano.Modo == "qualita" ? PianoVideo.CrfPer(enc, o.Qualita) : piano.Crf };
                    var dueP = piano.Modo == "peso" && enc is "libx264" or "libx265" or "libvpx-vp9";
                    if (dueP)
                    {
                        await Esegui(l, ctx, info, p, enc, avanzate, hwdec, passata: 1);
                        await Esegui(l, ctx, info, p, enc, avanzate, hwdec, passata: 2);
                    }
                    else await Esegui(l, ctx, info, p, enc, avanzate, hwdec, passata: 0);
                    ultimo = null;
                    if (enc != piano.Encoder) Registro.Scrivi($"Fatto con {enc} invece di {piano.Encoder}");
                    break;
                }
                catch (OperationCanceledException) { throw; }
                catch (ErroreConversione e)
                {
                    ultimo = e;
                    Registro.Scrivi($"Tentativo {enc} (avanzate {avanzate}, hwdec {hwdec}) fallito: {e.Message}");
                    ctx.Fase("Riprovo in un altro modo");
                }
            }
            if (ultimo is not null) throw ultimo;

            if (obiettivo <= 0 || giro >= 2) return;
            var peso = new FileInfo(l.Bozza).Length;
            if (peso <= obiettivo) return;
            // sforato: si stringe il bitrate in proporzione, con un po' di margine in più
            var fattore = obiettivo / (double)peso * 0.96;
            bitrate = (long)(bitrate * fattore);
            ctx.Fase($"Sforava del {(peso / (double)obiettivo - 1) * 100:0.#}%: rifaccio più stretto");
            Registro.Scrivi($"Sforato {peso} > {obiettivo}, nuovo bitrate {bitrate}");
        }
    }

    static async Task Esegui(Lavoro l, Contesto ctx, InfoMedia info, PianoVideo p, string encoder, bool avanzate, bool hwDecodifica, int passata)
    {
        var est = l.Formato.Estensione;
        var a = new List<string>();
        if (hwDecodifica) a.AddRange(["-hwaccel", "auto"]);
        a.AddRange(["-i", l.Sorgente]);

        a.AddRange(["-map", "0:V:0"]);
        var audioDentro = !p.SenzaAudio && passata != 1;
        if (audioDentro) a.AddRange(["-map", p.SoloPrimaTraccia ? "0:a:0" : "0:a?"]);
        if (est == ".mkv" && p.Modo != "peso" && passata != 1) a.AddRange(["-map", "0:s?", "-c:s", "copy"]);
        else a.Add("-sn");
        a.AddRange(["-map_metadata", "0", "-map_chapters", "0"]);

        if (p.Modo == "copia")
        {
            a.AddRange(["-c:v", "copy"]);
            if (p.Codec == "hevc" && est is ".mp4" or ".mov") a.AddRange(["-tag:v", "hvc1"]);
        }
        else
        {
            var filtri = new List<string>();
            var v = info.Video!;
            if (p.HdrInSdr)
                filtri.Add("zscale=t=linear:npl=100,format=gbrpf32le,zscale=p=bt709,tonemap=tonemap=hable:desat=0,zscale=t=bt709:m=bt709:r=tv");
            if (p.Larghezza != v.Larghezza || p.Altezza != v.Altezza)
                filtri.Add($"scale={p.Larghezza}:{p.Altezza}:flags=lanczos");
            if (Math.Abs(p.Fps - v.Fps) > 0.01 && v.Fps > 0)
                filtri.Add($"fps={N(p.Fps)}");
            var dieciBit = p.HdrTenuto;
            filtri.Add($"format={(dieciBit ? encoder.EndsWith("_nvenc") || encoder.EndsWith("_qsv") ? "p010le" : "yuv420p10le" : encoder == "prores_ks" ? "yuv422p10le" : "yuv420p")}");
            a.AddRange(["-vf", string.Join(',', filtri)]);
            a.AddRange(ArgomentiEncoder(p, encoder, avanzate, dieciBit, l, passata));
            if (p.HdrTenuto && info.Video!.Trasferimento is { } trc)
                a.AddRange(["-color_primaries", "bt2020", "-color_trc", trc, "-colorspace", "bt2020nc"]);
        }

        if (!audioDentro) a.Add("-an");
        else if (p.AudioCopia) a.AddRange(["-c:a", "copy"]);
        else
        {
            var kbps = p.AudioKbps > 0 ? p.AudioKbps : 192;
            if (est == ".webm") a.AddRange(["-c:a", "libopus", "-b:a", $"{kbps}k", "-ac", "2"]);
            else
            {
                a.AddRange(["-c:a", "aac", "-b:a", $"{kbps}k"]);
                if (p.Modo == "peso" || kbps < 256) a.AddRange(["-ac", "2"]);
            }
        }

        if (est is ".mp4" or ".mov") a.AddRange(["-movflags", "+faststart"]);
        if (passata == 1)
        {
            a.AddRange(["-f", "null", "NUL"]);
            await Ffmpeg.Esegui(ctx, a, info.Durata, 0, 0.35, "Prima passata: studio il video");
        }
        else
        {
            a.AddRange(["-f", Ffmpeg.Muxer(est), l.Bozza]);
            double da = passata == 2 ? 0.35 : 0, amp = passata == 2 ? 0.65 : 1;
            var fase = p.Modo == "copia" ? "Cambio la scatola" : p.Hardware ? $"Converto con la scheda video ({encoder.Split('_')[1].ToUpperInvariant()})" : "Converto con la CPU";
            await Ffmpeg.Esegui(ctx, a, info.Durata, da, amp, fase);
        }
    }

    static IEnumerable<string> ArgomentiEncoder(PianoVideo p, string enc, bool avanzate, bool dieciBit, Lavoro l, int passata)
    {
        var a = new List<string> { "-c:v", enc };
        var peso = p.Modo == "peso";
        var b = p.BitrateVideo;
        var passlog = Path.Combine(Strumenti.CartellaTemporanea, "passata-" + l.Id);

        if (enc.EndsWith("_nvenc"))
        {
            a.AddRange(["-preset", "p5", "-tune", "hq", "-rc", "vbr"]);
            if (avanzate)
            {
                a.AddRange(["-multipass", "fullres", "-spatial-aq", "1", "-rc-lookahead", "32"]);
                if (!enc.StartsWith("h264")) a.AddRange(["-temporal-aq", "1"]);
                a.AddRange(["-b_ref_mode", "middle"]);
            }
            if (peso) a.AddRange(["-b:v", $"{b}", "-maxrate", $"{(long)(b * 1.5)}", "-bufsize", $"{b * 2}"]);
            else a.AddRange(["-cq", $"{p.Crf}", "-b:v", "0"]);
            if (enc.StartsWith("h264")) a.AddRange(["-profile:v", "high"]);
            if (enc.StartsWith("hevc") && dieciBit) a.AddRange(["-profile:v", "main10"]);
        }
        else if (enc.EndsWith("_amf"))
        {
            a.AddRange(["-quality", "quality"]);
            if (peso) a.AddRange(["-rc", "vbr_peak", "-b:v", $"{b}", "-maxrate", $"{(long)(b * 1.5)}"]);
            else a.AddRange(["-rc", "cqp", "-qp_i", $"{p.Crf}", "-qp_p", $"{p.Crf + 2}", "-qp_b", $"{p.Crf + 4}"]);
        }
        else if (enc.EndsWith("_qsv"))
        {
            a.AddRange(["-preset", "slower"]);
            if (peso) a.AddRange(["-b:v", $"{b}", "-maxrate", $"{(long)(b * 1.5)}"]);
            else a.AddRange(["-global_quality", $"{p.Crf}"]);
        }
        else switch (enc)
        {
            case "libx264":
                a.AddRange(["-preset", "medium", "-profile:v", "high"]);
                if (peso) a.AddRange(["-b:v", $"{b}", "-pass", $"{passata}", "-passlogfile", passlog]);
                else a.AddRange(["-crf", $"{p.Crf}"]);
                break;
            case "libx265":
                a.AddRange(["-preset", "medium"]);
                if (peso) a.AddRange(["-b:v", $"{b}", "-x265-params", $"log-level=error:pass={passata}:stats={passlog.Replace('\\', '/').Replace(":", "\\:")}.log"]);
                else a.AddRange(["-crf", $"{p.Crf}", "-x265-params", "log-level=error"]);
                break;
            case "libsvtav1":
                a.AddRange(["-preset", "7"]);
                if (peso) a.AddRange(["-b:v", $"{b}"]);
                else a.AddRange(["-crf", $"{p.Crf}"]);
                break;
            case "libvpx-vp9":
                a.AddRange(["-deadline", "good", "-cpu-used", passata == 1 ? "4" : "2", "-row-mt", "1", "-tile-columns", "2"]);
                if (peso) a.AddRange(["-b:v", $"{b}", "-pass", $"{passata}", "-passlogfile", passlog]);
                else a.AddRange(["-crf", $"{p.Crf}", "-b:v", "0"]);
                break;
            case "prores_ks":
                a.AddRange(["-profile:v", $"{p.Crf}", "-vendor", "apl0"]);
                break;
        }
        if (enc.StartsWith("hevc") || enc == "libx265")
            if (l.Formato.Estensione is ".mp4" or ".mov") a.AddRange(["-tag:v", "hvc1"]);
        return a;
    }

    static async Task Gif(Lavoro l, Contesto ctx, InfoMedia info)
    {
        var o = l.Opzioni.Video;
        var v = info.Video!;
        var larghezza = Math.Min(o.GifLarghezza > 0 ? o.GifLarghezza : 480, v.Larghezza);
        var fps = Math.Clamp(o.GifFps > 0 ? o.GifFps : 15, 1, 50);
        var filtro = $"fps={fps},scale={larghezza}:-1:flags=lanczos,split[a][b];[a]palettegen=max_colors=256:stats_mode=diff[p];[b][p]paletteuse=dither=sierra2_4a:diff_mode=rectangle";
        await Ffmpeg.Esegui(ctx, ["-i", l.Sorgente, "-filter_complex", filtro, "-loop", "0", "-f", "gif", l.Bozza],
            info.Durata, fase: "Disegno la GIF");
    }

    /// <summary>Una GIF (o APNG) che diventa video: MP4 o WEBM, dimensioni pari, si ripete come nell'originale no.</summary>
    public static async Task DaAnimazione(Lavoro l, Contesto ctx)
    {
        var info = await Sonda.Leggi(ctx.Strumenti, l.Sorgente, ctx.Ct);
        var webm = l.Formato.Estensione == ".webm";
        var enc = webm ? PianoVideo.ScegliEncoder("vp9", "auto", ctx.Hardware)! : PianoVideo.ScegliEncoder("h264", "auto", ctx.Hardware)!;
        var a = new List<string> { "-i", l.Sorgente, "-vf", "scale=trunc(iw/2)*2:trunc(ih/2)*2,format=yuv420p", "-c:v", enc };
        if (enc.EndsWith("_nvenc")) a.AddRange(["-preset", "p5", "-rc", "vbr", "-cq", "23", "-b:v", "0"]);
        else if (enc == "libx264") a.AddRange(["-crf", "20", "-preset", "medium"]);
        else if (enc == "libvpx-vp9") a.AddRange(["-crf", "30", "-b:v", "0", "-row-mt", "1"]);
        if (!webm) a.AddRange(["-movflags", "+faststart"]);
        a.AddRange(["-an", "-f", webm ? "webm" : "mp4", l.Bozza]);
        await Ffmpeg.Esegui(ctx, a, info.Durata, fase: "Trasformo l'animazione in video");
    }
}
