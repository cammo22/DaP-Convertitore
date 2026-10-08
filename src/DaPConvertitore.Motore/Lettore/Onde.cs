using System.Diagnostics;

namespace DaP.Convertitore.Lettore;

/// <summary>
/// La forma d'onda vera per la miniatura della musica, come i messaggi vocali di WhatsApp: tante barrette, alte dove
/// la musica è forte e basse dove è piano. FFmpeg decodifica tutto il brano a 8000 campioni al secondo e se ne
/// ricava il volume ogni 50 ms; poi si raggruppa in tante barre quante ne servono.
/// </summary>
public static class Onde
{
    const int Campionamento = 8000;
    const int CampioniPerFinestra = 400; // 50 ms

    /// <summary>
    /// Le barre, ognuna fra 0 e 1. Se il brano è lunghissimo e FFmpeg non finisce entro <paramref name="massimo"/>,
    /// ci si accontenta di quello che ha già decodificato (l'onda è in scala su quella parte). Vuoto se non c'è audio.
    /// </summary>
    public static async Task<float[]> Barre(Strumenti s, string percorso, int barre, TimeSpan massimo, CancellationToken ct = default)
    {
        var finestre = await Volumi(s, percorso, massimo, ct);
        return Raggruppa(finestre, barre);
    }

    /// <summary>Il volume (RMS) di ogni finestra da 50 ms.</summary>
    public static async Task<List<float>> Volumi(Strumenti s, string percorso, TimeSpan massimo, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo(s.Ffmpeg)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            WorkingDirectory = Path.GetDirectoryName(s.Ffmpeg) ?? Environment.CurrentDirectory,
        };
        foreach (var a in new[] { "-hide_banner", "-loglevel", "error", "-nostdin", "-i", percorso, "-map", "0:a:0", "-vn", "-sn", "-dn", "-ac", "1", "-ar", $"{Campionamento}", "-f", "s16le", "-" })
            psi.ArgumentList.Add(a);

        var volumi = new List<float>();
        using var p = new Process { StartInfo = psi };
        try { p.Start(); }
        catch { return volumi; }
        try { p.PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }
        p.StandardInput.Close();
        _ = p.StandardError.ReadToEndAsync(); // se no FFmpeg si pianta quando la pipe degli errori si riempie

        using var tempo = CancellationTokenSource.CreateLinkedTokenSource(ct);
        tempo.CancelAfter(massimo);
        var buf = new byte[CampioniPerFinestra * 2 * 40];
        var avanzo = 0;
        try
        {
            var flusso = p.StandardOutput.BaseStream;
            while (true)
            {
                var letti = await flusso.ReadAsync(buf.AsMemory(avanzo), tempo.Token);
                if (letti == 0) break;
                var totale = avanzo + letti;
                var intere = totale / (CampioniPerFinestra * 2);
                for (var f = 0; f < intere; f++)
                {
                    double somma = 0;
                    var o = f * CampioniPerFinestra * 2;
                    for (var i = 0; i < CampioniPerFinestra; i++)
                    {
                        double v = BitConverter.ToInt16(buf, o + i * 2) / 32768.0;
                        somma += v * v;
                    }
                    volumi.Add((float)Math.Sqrt(somma / CampioniPerFinestra));
                }
                avanzo = totale - intere * CampioniPerFinestra * 2;
                if (avanzo > 0) Buffer.BlockCopy(buf, intere * CampioniPerFinestra * 2, buf, 0, avanzo);
            }
        }
        catch (OperationCanceledException) { /* tempo scaduto o annullato: si tiene quello che c'è */ }
        catch (IOException) { }
        finally
        {
            try { if (!p.HasExited) p.Kill(entireProcessTree: true); } catch { }
        }
        ct.ThrowIfCancellationRequested();
        return volumi;
    }

    /// <summary>Tante finestre in <paramref name="barre"/> barre (media del volume), portate in scala: la più forte è quasi 1.</summary>
    public static float[] Raggruppa(IReadOnlyList<float> volumi, int barre)
    {
        if (volumi.Count == 0 || barre <= 0) return [];
        var out_ = new float[barre];
        for (var i = 0; i < barre; i++)
        {
            int da = (int)((long)i * volumi.Count / barre), a = Math.Max(da + 1, (int)((long)(i + 1) * volumi.Count / barre));
            double somma = 0;
            var n = 0;
            for (var j = da; j < a && j < volumi.Count; j++) { somma += volumi[j] * volumi[j]; n++; }
            out_[i] = n > 0 ? (float)Math.Sqrt(somma / n) : 0;
        }
        // in scala sul 97° percentile: un colpo solo, in un brano piano, non schiaccia tutto il resto
        var ordine = out_.OrderBy(x => x).ToArray();
        var top = Math.Max(0.02f, ordine[Math.Min(ordine.Length - 1, (int)(ordine.Length * 0.97))]);
        for (var i = 0; i < barre; i++) out_[i] = MathF.Pow(Math.Clamp(out_[i] / top, 0f, 1f), 0.8f);
        return out_;
    }
}
