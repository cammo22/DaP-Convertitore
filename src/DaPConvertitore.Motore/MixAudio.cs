using System.Buffers.Binary;

namespace DaP.Convertitore;

/// <summary>
/// Mescola microfono e audio del PC in un WAV stereo a 48 kHz, allineato all'orologio.
/// WASAPI sta nell'app (su Windows). Qui c'è il conto, così le prove girano anche senza scheda audio:
/// i buchi si riempiono di silenzio, come fa la registrazione di DaProd Editor Video, e un ritardo
/// corto si aspetta invece di spostare l'audio di un fotogramma a ogni giro.
/// </summary>
public static class MixAudio
{
    public const int Frequenza = 48000;
    public const int Canali = 2;
    /// <summary>Sotto questa attesa non si inventa silenzio: il dispositivo sta solo arrivando in ritardo.</summary>
    public const int TolleranzaFrame = 48000 * 80 / 1000;

    public static int FrameDovuti(TimeSpan trascorso)
    {
        var n = (long)(trascorso.Ticks * (long)Frequenza / TimeSpan.TicksPerSecond);
        if (n < 0) return 0;
        return n > int.MaxValue ? int.MaxValue : (int)n;
    }

    /// <summary>
    /// Quanti frame scrivere adesso. Se manca un lato (o mancano entrambi) e il buco è più lungo della
    /// tolleranza, si scrive fino all'orologio e il lato vuoto è silenzio. Un buco corto si aspetta.
    /// </summary>
    public static int FrameDaScrivere(int dovuti, int scritti, int frameMic, int framePc, bool usaMic, bool usaPc, int tolleranzaFrame)
    {
        var manca = dovuti - scritti;
        if (manca <= 0 || (!usaMic && !usaPc)) return 0;
        var disp = int.MaxValue;
        if (usaMic) disp = Math.Min(disp, Math.Max(0, frameMic));
        if (usaPc) disp = Math.Min(disp, Math.Max(0, framePc));
        if (disp == int.MaxValue) disp = 0;
        if (disp >= manca) return manca;
        if (manca - disp <= tolleranzaFrame) return disp;
        return manca;
    }

    public static float Limita(float x) => float.IsFinite(x) ? Math.Clamp(x, -1f, 1f) : 0f;

    /// <summary>PCM del dispositivo → float stereo alla frequenza di origine. Mono diventa due canali uguali.</summary>
    public static float[] AFloat(ReadOnlySpan<byte> dati, int canali, int bit, bool ieee)
    {
        if (canali < 1) canali = 1;
        var bytesPer = Math.Max(1, bit / 8);
        var frame = dati.Length / (bytesPer * canali);
        var dest = new float[frame * 2];
        var o = 0;
        for (var i = 0; i < frame; i++)
        {
            float l = 0, r = 0;
            for (var c = 0; c < canali; c++)
            {
                var v = Campione(dati, o, bit, ieee);
                o += bytesPer;
                if (c == 0) l = v;
                if (c == 1 || canali == 1) r = canali == 1 ? v : v;
            }
            dest[i * 2] = l;
            dest[i * 2 + 1] = r;
        }
        return dest;
    }

    /// <summary>Ricampiona stereo interleaved con interpolazione lineare. 48 kHz resta com'è.</summary>
    public static float[] Ricampiona(float[] stereo, int freqDa, int freqA)
    {
        if (stereo.Length < 2 || freqDa <= 0 || freqA <= 0 || freqDa == freqA) return stereo;
        var frames = stereo.Length / 2;
        var nuovi = Math.Max(1, (int)Math.Round(frames * (double)freqA / freqDa));
        var dest = new float[nuovi * 2];
        for (var i = 0; i < nuovi; i++)
        {
            var pos = i * (double)freqDa / freqA;
            var i0 = (int)pos;
            if (i0 >= frames) i0 = frames - 1;
            var i1 = Math.Min(i0 + 1, frames - 1);
            var f = (float)(pos - i0);
            dest[i * 2] = stereo[i0 * 2] * (1 - f) + stereo[i1 * 2] * f;
            dest[i * 2 + 1] = stereo[i0 * 2 + 1] * (1 - f) + stereo[i1 * 2 + 1] * f;
        }
        return dest;
    }

    /// <summary>Dal buffer di WASAPI al formato del WAV, in un colpo solo.</summary>
    public static float[] Normalizza(ReadOnlySpan<byte> dati, int canali, int bit, int frequenza, bool ieee) =>
        Ricampiona(AFloat(dati, canali, bit, ieee), frequenza, Frequenza);

    static float Campione(ReadOnlySpan<byte> dati, int o, int bit, bool ieee)
    {
        if (ieee && bit == 32 && o + 4 <= dati.Length) return Limita(BitConverter.ToSingle(dati[o..]));
        if (bit == 16 && o + 2 <= dati.Length) return BitConverter.ToInt16(dati[o..]) / 32768f;
        if (bit == 32 && o + 4 <= dati.Length) return BitConverter.ToInt32(dati[o..]) / 2147483648f;
        if (bit == 8 && o < dati.Length) return (dati[o] - 128) / 128f;
        if (bit == 24 && o + 3 <= dati.Length)
        {
            var v = dati[o] | (dati[o + 1] << 8) | (dati[o + 2] << 16);
            if ((v & 0x800000) != 0) v |= unchecked((int)0xFF000000);
            return v / 8388608f;
        }
        return 0;
    }
}

/// <summary>
/// Le code del microfono e dell'audio del PC. <see cref="Verso"/> tira fuori i frame dovuti dall'orologio:
/// un lato vuoto, passato la tolleranza, è silenzio e non ferma l'altro.
/// </summary>
public sealed class OrologioAudio
{
    readonly object lucchetto = new();
    readonly Queue<float> mic = new();
    readonly Queue<float> pc = new();
    readonly bool usaMic, usaPc;
    const int Tetto = MixAudio.Frequenza * 2;

    public OrologioAudio(bool usaMic, bool usaPc)
    {
        this.usaMic = usaMic;
        this.usaPc = usaPc;
    }

    public int FrameScritti { get; private set; }
    public int FrameMic { get { lock (lucchetto) return mic.Count / 2; } }
    public int FramePc { get { lock (lucchetto) return pc.Count / 2; } }

    public void AccodaMic(ReadOnlySpan<float> stereo) { lock (lucchetto) Accoda(mic, stereo); }
    public void AccodaPc(ReadOnlySpan<float> stereo) { lock (lucchetto) Accoda(pc, stereo); }

    /// <summary>I prossimi frame, stereo interleaved. Vuoto se non è ancora il momento (ritardo corto).</summary>
    public float[] Verso(int frameDovuti)
    {
        lock (lucchetto)
        {
            var n = MixAudio.FrameDaScrivere(frameDovuti, FrameScritti, mic.Count / 2, pc.Count / 2, usaMic, usaPc, MixAudio.TolleranzaFrame);
            if (n <= 0) return [];
            var dest = new float[n * 2];
            for (var i = 0; i < n; i++)
            {
                float ml = 0, mr = 0, pl = 0, pr = 0;
                if (usaMic && mic.Count >= 2) { ml = mic.Dequeue(); mr = mic.Dequeue(); }
                if (usaPc && pc.Count >= 2) { pl = pc.Dequeue(); pr = pc.Dequeue(); }
                dest[i * 2] = MixAudio.Limita(ml + pl);
                dest[i * 2 + 1] = MixAudio.Limita(mr + pr);
            }
            FrameScritti += n;
            return dest;
        }
    }

    static void Accoda(Queue<float> q, ReadOnlySpan<float> dati)
    {
        var n = dati.Length - (dati.Length & 1);
        for (var i = 0; i < n; i++) q.Enqueue(dati[i]);
        // a coppie: togliere un campione solo scambierebbe sinistra e destra
        while (q.Count > Tetto)
        {
            q.Dequeue();
            if (q.Count > 0) q.Dequeue();
        }
    }
}

/// <summary>Un WAV PCM 16 bit stereo 48 kHz. La testata si completa in <see cref="Dispose"/>.</summary>
public sealed class WavPcm : IDisposable
{
    public static int Aperti { get; private set; }

    readonly Stream flusso;
    readonly bool chiudiFlusso;
    long campioni;
    bool chiuso;

    public WavPcm(Stream flusso, bool chiudiFlusso = true)
    {
        this.flusso = flusso;
        this.chiudiFlusso = chiudiFlusso;
        Aperti++;
        Span<byte> testa = stackalloc byte[44];
        flusso.Write(testa);
    }

    public long Campioni => campioni;

    public void Scrivi(ReadOnlySpan<float> stereo)
    {
        if (chiuso || stereo.Length == 0) return;
        var n = stereo.Length - (stereo.Length & 1);
        var byteN = n * 2;
        var buf = new byte[byteN];
        for (var i = 0; i < n; i++)
        {
            var v = (int)Math.Round(MixAudio.Limita(stereo[i]) * 32767f);
            BinaryPrimitives.WriteInt16LittleEndian(buf.AsSpan(i * 2), (short)v);
        }
        flusso.Write(buf);
        campioni += n;
    }

    public void Dispose()
    {
        if (chiuso) return;
        chiuso = true;
        try
        {
            var dati = campioni * 2;
            flusso.Position = 0;
            Span<byte> testa = stackalloc byte[44];
            "RIFF"u8.CopyTo(testa);
            BinaryPrimitives.WriteInt32LittleEndian(testa[4..], (int)(dati + 36));
            "WAVE"u8.CopyTo(testa[8..]);
            "fmt "u8.CopyTo(testa[12..]);
            BinaryPrimitives.WriteInt32LittleEndian(testa[16..], 16);
            BinaryPrimitives.WriteInt16LittleEndian(testa[20..], 1);
            BinaryPrimitives.WriteInt16LittleEndian(testa[22..], MixAudio.Canali);
            BinaryPrimitives.WriteInt32LittleEndian(testa[24..], MixAudio.Frequenza);
            BinaryPrimitives.WriteInt32LittleEndian(testa[28..], MixAudio.Frequenza * MixAudio.Canali * 2);
            BinaryPrimitives.WriteInt16LittleEndian(testa[32..], MixAudio.Canali * 2);
            BinaryPrimitives.WriteInt16LittleEndian(testa[34..], 16);
            "data"u8.CopyTo(testa[36..]);
            BinaryPrimitives.WriteInt32LittleEndian(testa[40..], (int)dati);
            flusso.Write(testa);
            flusso.Flush();
        }
        finally
        {
            if (chiudiFlusso) flusso.Dispose();
            Aperti--;
        }
    }
}

/// <summary>Un pezzo di audio: l'orologio e il WAV. Si apre e si chiude, e a chiuso non tiene niente.</summary>
public sealed class SegmentoAudio : IDisposable
{
    readonly OrologioAudio orologio;
    readonly WavPcm wav;
    bool chiuso;

    public SegmentoAudio(Stream destinazione, bool microfono, bool audioPc)
    {
        orologio = new OrologioAudio(microfono, audioPc);
        wav = new WavPcm(destinazione);
    }

    public int FrameScritti => orologio.FrameScritti;

    public void Mic(ReadOnlySpan<float> stereo) => orologio.AccodaMic(stereo);
    public void Pc(ReadOnlySpan<float> stereo) => orologio.AccodaPc(stereo);

    public void Pompa(TimeSpan trascorso)
    {
        if (chiuso) return;
        var pezzo = orologio.Verso(MixAudio.FrameDovuti(trascorso));
        if (pezzo.Length > 0) wav.Scrivi(pezzo);
    }

    public void Dispose()
    {
        if (chiuso) return;
        chiuso = true;
        wav.Dispose();
    }
}
