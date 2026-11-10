using System.Buffers.Binary;

namespace DaP.Convertitore.Prove;

/// <summary>Il mescolatore e il WAV, senza WASAPI e senza FFmpeg. Si apre e si chiude tante volte.</summary>
public class ProveMixAudio
{
    [Fact]
    public void IlBucoCortoSiAspettaEQuelloLungoESilenzio()
    {
        Assert.Equal(100, MixAudio.FrameDaScrivere(100, 0, 100, 0, true, false, 50));
        Assert.Equal(0, MixAudio.FrameDaScrivere(120, 100, 0, 0, true, false, 50));
        Assert.Equal(100, MixAudio.FrameDaScrivere(200, 100, 0, 0, true, false, 50));
        Assert.Equal(100, MixAudio.FrameDaScrivere(100, 0, 40, 100, true, true, 50));
        Assert.Equal(0, MixAudio.FrameDaScrivere(10, 0, 0, 0, false, false, 50));
        Assert.Equal(MixAudio.Frequenza / 10, MixAudio.FrameDovuti(TimeSpan.FromMilliseconds(100)));
    }

    [Fact]
    public void IDueLatiSiSommanoEIlVuotoNonFermaLAltro()
    {
        var o = new OrologioAudio(true, true);
        o.AccodaMic(new float[] { 0.5f, 0.25f, 0.5f, 0.25f });
        o.AccodaPc(new float[] { 0.5f, 0.25f, 0.1f, 0.1f });
        var prima = o.Verso(2);
        Assert.Equal(4, prima.Length);
        Assert.Equal(1f, prima[0], 3);
        Assert.Equal(0.5f, prima[1], 3);
        var dopo = o.Verso(2 + MixAudio.TolleranzaFrame + 5);
        Assert.True(dopo.Length > 4);
        Assert.Equal(0f, dopo[^1]);
    }

    [Fact]
    public void PcmERicampionamento()
    {
        var pcm = new byte[] { 0, 0x40, 0, 0xC0 };
        var f = MixAudio.AFloat(pcm, 2, 16, false);
        Assert.Equal(2, f.Length);
        Assert.True(f[0] > 0.4f && f[0] < 0.6f);
        Assert.True(f[1] < -0.4f);
        var mono = MixAudio.AFloat(new byte[] { 0, 0x40 }, 1, 16, false);
        Assert.Equal(mono[0], mono[1]);
        var ieee = new byte[4];
        BinaryPrimitives.WriteSingleLittleEndian(ieee, 0.5f);
        var fl = MixAudio.AFloat(ieee, 1, 32, true);
        Assert.Equal(0.5f, fl[0], 3);
        var stesso = MixAudio.Ricampiona(new float[] { 0.2f, -0.2f, 0.4f, -0.4f }, 48000, 48000);
        Assert.Equal(4, stesso.Length);
        var su = MixAudio.Ricampiona(new float[441 * 2], 44100, 48000);
        Assert.InRange(su.Length / 2, 470, 490);
        var norm = MixAudio.Normalizza(pcm, 2, 16, 48000, false);
        Assert.Equal(2, norm.Length);
    }

    [Fact]
    public void IlWavHaLaTestataESiChiude()
    {
        Assert.Equal(0, WavPcm.Aperti);
        using (var mem = new MemoryStream())
        {
            using (var wav = new WavPcm(mem, chiudiFlusso: false))
            {
                Assert.Equal(1, WavPcm.Aperti);
                wav.Scrivi(new float[] { 1f, -1f, 0f, 0.5f });
            }
            Assert.Equal(0, WavPcm.Aperti);
            var b = mem.ToArray();
            Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(b, 0, 4));
            Assert.Equal("WAVE", System.Text.Encoding.ASCII.GetString(b, 8, 4));
            Assert.Equal(48000, BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(24)));
            Assert.Equal(2, BinaryPrimitives.ReadInt16LittleEndian(b.AsSpan(22)));
            Assert.Equal(16, BinaryPrimitives.ReadInt16LittleEndian(b.AsSpan(34)));
            Assert.Equal(8, BinaryPrimitives.ReadInt32LittleEndian(b.AsSpan(40)));
            Assert.Equal(short.MaxValue, BinaryPrimitives.ReadInt16LittleEndian(b.AsSpan(44)));
        }
    }

    [Fact]
    public void AprireEChiudereTanteVolteNonLasciaWavAperti()
    {
        Assert.Equal(0, WavPcm.Aperti);
        for (var i = 0; i < 30; i++)
        {
            using var mem = new MemoryStream();
            using var seg = new SegmentoAudio(mem, microfono: true, audioPc: true);
            seg.Mic(new float[] { 0.2f, 0.2f, 0.2f, 0.2f });
            seg.Pc(new float[] { 0.2f, 0.2f, 0.2f, 0.2f });
            seg.Pompa(TimeSpan.FromMilliseconds(20));
            Assert.True(seg.FrameScritti > 0);
            Assert.Equal(1, WavPcm.Aperti);
        }
        Assert.Equal(0, WavPcm.Aperti);
    }

    [Fact]
    public void IFigliSiSegnanoESiTolgono()
    {
        Figli.Azzera();
        for (var i = 0; i < 20; i++)
        {
            Figli.Segna(1000 + i);
            Assert.Equal(1, Figli.Quanti());
            Figli.Tolto(1000 + i);
            Assert.Equal(0, Figli.Quanti());
        }
        Figli.Segna(7);
        Figli.Segna(8);
        Figli.Tolto(7);
        Assert.Equal(1, Figli.Quanti());
        Figli.Azzera();
        Assert.Equal(0, Figli.Quanti());
    }

    [Fact]
    public void IContatoriDiconoSeCrescono()
    {
        var prima = new Contatori(80, 40, 500, 200L * 1024 * 1024, 18);
        Assert.Contains("gdi 80", prima.Riga());
        Assert.Contains("ram 200 MB", prima.Riga());
        var ferma = new Contatori(90, 42, 520, 210L * 1024 * 1024, 18);
        Assert.False(PerditaRisorse.Sospetta(prima, ferma));
        Assert.DoesNotContain("in crescita", PerditaRisorse.Riga(prima, ferma));
        var persa = new Contatori(900, 40, 500, 200L * 1024 * 1024, 18);
        Assert.True(PerditaRisorse.Sospetta(prima, persa));
        Assert.Contains("in crescita", PerditaRisorse.Riga(prima, persa));
    }

}
