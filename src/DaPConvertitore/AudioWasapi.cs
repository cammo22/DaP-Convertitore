using System.Diagnostics;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace DaP.Convertitore.App;

/// <summary>
/// Microfono e audio del PC con WASAPI, le stesse API di Windows che usano le tacche.
/// L'FFmpeg dell'app non ha l'ingresso <c>wasapi</c> (solo dshow, gdigrab, lavfi, openal, vfwcap):
/// il loopback del PC da FFmpeg non può esistere. Si cattura qui, in modo condiviso, si mescola
/// su un WAV allineato all'orologio e alla fine FFmpeg lo unisce al video. Una pipe verso FFmpeg
/// mentre gdigrab gira si intasa e perde i campioni; il file, chiuso a ogni pausa, no.
/// </summary>
public static class AudioWasapi
{
    public static DispositiviAudio Elenca()
    {
        var mic = new List<string>();
        var uscite = new List<string>();
        try
        {
            using var e = new MMDeviceEnumerator();
            Nomi(e, DataFlow.Capture, mic);
            Nomi(e, DataFlow.Render, uscite);
        }
        catch (Exception ex)
        {
            Registro.Scrivi($"registrazione: WASAPI non elenca i dispositivi ({ex.Message})");
        }
        return new DispositiviAudio(mic, uscite);
    }

    static void Nomi(MMDeviceEnumerator e, DataFlow flusso, List<string> nomi)
    {
        var col = e.EnumerateAudioEndPoints(flusso, DeviceState.Active);
        foreach (var d in col)
        {
            try
            {
                var n = d.FriendlyName?.Trim();
                if (!string.IsNullOrEmpty(n) && !nomi.Contains(n)) nomi.Add(n);
            }
            catch { /* un dispositivo senza nome non si propone */ }
            finally { d.Dispose(); }
        }
    }

    internal static MMDevice? Trova(string nome, DataFlow flusso)
    {
        using var e = new MMDeviceEnumerator();
        var col = e.EnumerateAudioEndPoints(flusso, DeviceState.Active);
        MMDevice? esatto = null, vago = null;
        foreach (var d in col)
        {
            var n = "";
            try { n = d.FriendlyName ?? ""; } catch { /* si scarta */ }
            var preciso = n.Equals(nome, StringComparison.OrdinalIgnoreCase);
            var simile = !preciso && n.Length > 0 && (n.Contains(nome, StringComparison.OrdinalIgnoreCase) || nome.Contains(n, StringComparison.OrdinalIgnoreCase));
            if (preciso && esatto is null) esatto = d;
            else if (simile && vago is null) vago = d;
            else d.Dispose();
        }
        if (esatto is not null) { vago?.Dispose(); return esatto; }
        return vago;
    }

    /// <summary>Le tacche. Non apre una cattura: è lo stesso misuratore di Windows, e si lascia quando il pannello si chiude.</summary>
    public sealed class Metro : IDisposable
    {
        MMDevice? mic, pc;
        string? nomeMic, nomePc;
        bool detto;

        public float Microfono(string? nome) => Picco(ref mic, ref nomeMic, nome, DataFlow.Capture);
        public float Uscita(string? nome) => Picco(ref pc, ref nomePc, nome, DataFlow.Render);

        float Picco(ref MMDevice? dev, ref string? tenuto, string? nome, DataFlow flusso)
        {
            if (string.IsNullOrWhiteSpace(nome)) { Lascia(ref dev); tenuto = null; return 0; }
            try
            {
                if (!string.Equals(tenuto, nome, StringComparison.Ordinal) || dev is null)
                {
                    Lascia(ref dev);
                    dev = Trova(nome, flusso);
                    tenuto = nome;
                }
                if (dev is null) return 0;
                var v = dev.AudioMeterInformation.MasterPeakValue;
                return float.IsFinite(v) ? Math.Clamp(v, 0f, 1f) : 0;
            }
            catch (Exception e)
            {
                if (!detto) { detto = true; Registro.Scrivi($"registrazione: livello audio non letto ({e.Message})"); }
                return 0;
            }
        }

        public void Dispose()
        {
            Lascia(ref mic);
            Lascia(ref pc);
        }

        static void Lascia(ref MMDevice? dev)
        {
            var d = dev;
            dev = null;
            try { d?.Dispose(); } catch { /* già lasciato */ }
        }
    }
}

/// <summary>Scrive il WAV del pezzo. Il filo della pompa è l'unico che tocca il file; i callback si limitano ad accodare.</summary>
public sealed class CatturaAudio : IDisposable
{
    readonly SegmentoAudio segmento;
    readonly CancellationTokenSource ferma = new();
    readonly List<IDisposable> aperti = [];
    readonly Stopwatch orologio = new();
    Thread? pompa;
    bool chiusa;

    public bool Partita { get; }

    public CatturaAudio(string wav, string? nomeMic, string? nomePc)
    {
        WasapiCapture? capMic = null;
        WasapiLoopbackCapture? capPc = null;
        MMDevice? devMic = null, devPc = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(nomeMic)) devMic = AudioWasapi.Trova(nomeMic, DataFlow.Capture);
            if (!string.IsNullOrWhiteSpace(nomePc)) devPc = AudioWasapi.Trova(nomePc, DataFlow.Render);
        }
        catch (Exception e) { Registro.Scrivi($"registrazione: dispositivo audio ({e.Message})"); }

        var file = new FileStream(wav, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
        segmento = new SegmentoAudio(file, devMic is not null, devPc is not null);
        capMic = Avvia(devMic, microfono: true);
        capPc = AvviaLoop(devPc);
        Partita = capMic is not null || capPc is not null;
        if (Partita)
        {
            orologio.Start();
            pompa = new Thread(Gira) { IsBackground = true, Name = "dap-audio" };
            pompa.Start();
        }
        Registro.Scrivi(Partita
            ? $"registrazione: audio WASAPI (mic {(capMic is null ? "no" : "sì")}, pc {(capPc is null ? "no" : "sì")})"
            : "registrazione: senza traccia audio, i dispositivi scelti non si sono aperti");
    }

    WasapiCapture? Avvia(MMDevice? dev, bool microfono)
    {
        if (dev is null) return null;
        aperti.Add(dev);
        try
        {
            var cap = new WasapiCapture(dev);
            aperti.Add(cap);
            cap.DataAvailable += (_, e) => Campioni(cap, e, microfono);
            cap.StartRecording();
            return cap;
        }
        catch (Exception e)
        {
            Registro.Scrivi($"registrazione: microfono non aperto ({e.Message})");
            return null;
        }
    }

    WasapiLoopbackCapture? AvviaLoop(MMDevice? dev)
    {
        if (dev is null) return null;
        aperti.Add(dev);
        try
        {
            var cap = new WasapiLoopbackCapture(dev);
            aperti.Add(cap);
            cap.DataAvailable += (_, e) => Campioni(cap, e, microfono: false);
            cap.StartRecording();
            return cap;
        }
        catch (Exception e)
        {
            Registro.Scrivi($"registrazione: audio del PC non aperto ({e.Message})");
            return null;
        }
    }

    void Campioni(WasapiCapture cap, WaveInEventArgs e, bool microfono)
    {
        if (chiusa || e.BytesRecorded <= 0) return;
        try
        {
            var fmt = cap.WaveFormat;
            var pcm = MixAudio.Normalizza(e.Buffer.AsSpan(0, e.BytesRecorded), fmt.Channels, fmt.BitsPerSample, fmt.SampleRate, fmt.Encoding == WaveFormatEncoding.IeeeFloat);
            if (microfono) segmento.Mic(pcm);
            else segmento.Pc(pcm);
        }
        catch { /* un buffer storto non ferma la registrazione */ }
    }

    void Gira()
    {
        while (!ferma.IsCancellationRequested)
        {
            try { segmento.Pompa(orologio.Elapsed); } catch { /* si riprova al giro dopo */ }
            Thread.Sleep(20);
        }
    }

    public void Ferma()
    {
        if (chiusa) return;
        chiusa = true;
        ferma.Cancel();
        try { pompa?.Join(800); } catch { /* il filo è già uscito */ }
        foreach (var a in aperti)
        {
            try
            {
                if (a is WasapiCapture c) c.StopRecording();
            }
            catch { /* già fermo */ }
        }
        try { segmento.Pompa(orologio.Elapsed); } catch { /* l'ultimo pezzo, se ci sta */ }
        try { segmento.Dispose(); } catch { /* il WAV è già chiuso */ }
        foreach (var a in aperti)
        {
            try { a.Dispose(); } catch { /* già lasciato */ }
        }
        aperti.Clear();
    }

    public void Dispose() => Ferma();
}
