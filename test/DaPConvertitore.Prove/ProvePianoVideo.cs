namespace DaP.Convertitore.Prove;

public class ProvePianoVideo
{
    static readonly InfoHardware conNvidia = new("CPU", 16, [new Gpu("NVIDIA GeForce RTX 4060", "nvidia", 8L << 30, "1")],
        ["h264_nvenc", "hevc_nvenc", "av1_nvenc", "libx264", "libx265", "libsvtav1", "libvpx-vp9", "prores_ks"]);
    static readonly InfoHardware soloCpu = new("CPU", 8, [], ["libx264", "libx265", "libsvtav1", "libvpx-vp9", "prores_ks"]);

    static InfoMedia DieciMinuti1080(string codec = "h264", string audio = "aac") => new(600, 8_200_000, "mov,mp4,m4a,3gp,3g2,mj2",
        new InfoVideo(codec, 1920, 1080, 30, 8_000_000, "yuv420p", false, null, 8),
        [new InfoAudio(1, audio, 2, 48000, 192_000, "ita")], [], false);

    const long pesoSorgente = 615_000_000;

    [Fact]
    public void Col_peso_giusto_resta_in_1080_e_ci_sta()
    {
        var p = PianoVideo.Calcola(DieciMinuti1080(), pesoSorgente, ".mov", ".mp4", new OpzioniVideo { Modo = "peso", PesoByte = 300_000_000 }, conNvidia);
        Assert.Equal("peso", p.Modo);
        Assert.Equal("h264_nvenc", p.Encoder);
        Assert.Equal(1920, p.Larghezza);
        Assert.True(p.PesoStimato <= 300_000_000);
        var totale = (p.BitrateVideo + p.AudioKbps * 1000L) * 600 / 8;
        Assert.InRange(totale, 280_000_000, 300_000_000);
    }

    [Fact]
    public void Col_peso_piccolo_scende_di_risoluzione_e_lo_dice()
    {
        var p = PianoVideo.Calcola(DieciMinuti1080(), pesoSorgente, ".mov", ".mp4", new OpzioniVideo { Modo = "peso", PesoByte = 30_000_000 }, conNvidia);
        Assert.True(p.Altezza < 1080);
        Assert.Equal(0, p.Altezza % 2);
        Assert.Contains(p.Note, n => n.Contains($"{p.Altezza}p"));
        Assert.True(p.Punteggio < 0.6);
    }

    [Fact]
    public void Il_verticale_tiene_il_lato_corto()
    {
        var info = DieciMinuti1080() with { Video = new InfoVideo("h264", 1080, 1920, 30, 8_000_000, "yuv420p", false, null, 8) };
        var p = PianoVideo.Calcola(info, pesoSorgente, ".mp4", ".mp4", new OpzioniVideo { Modo = "qualita", Lato = 720 }, conNvidia);
        Assert.Equal(720, p.Larghezza);
        Assert.Equal(1280, p.Altezza);
    }

    [Fact]
    public void Da_mkv_a_mp4_con_codec_buoni_cambia_solo_la_scatola()
    {
        var p = PianoVideo.Calcola(DieciMinuti1080(), pesoSorgente, ".mkv", ".mp4", new OpzioniVideo(), conNvidia);
        Assert.Equal("copia", p.Modo);
        Assert.True(p.AudioCopia);
    }

    [Fact]
    public void Da_mp4_a_mp4_ricodifica()
    {
        var p = PianoVideo.Calcola(DieciMinuti1080(), pesoSorgente, ".mp4", ".mp4", new OpzioniVideo(), conNvidia);
        Assert.Equal("qualita", p.Modo);
    }

    [Fact]
    public void Senza_scheda_video_lavora_la_cpu()
    {
        var p = PianoVideo.Calcola(DieciMinuti1080(), pesoSorgente, ".mov", ".mp4", new OpzioniVideo { Modo = "qualita", Codec = "hevc", Motore = "nvidia" }, soloCpu);
        Assert.Equal("libx265", p.Encoder);
        Assert.False(p.Hardware);
    }

    [Fact]
    public void Il_webm_usa_av1_se_la_scheda_lo_fa()
    {
        Assert.Equal("av1", PianoVideo.Calcola(DieciMinuti1080(), pesoSorgente, ".mov", ".webm", new OpzioniVideo { Modo = "qualita" }, conNvidia).Codec);
        Assert.Equal("vp9", PianoVideo.Calcola(DieciMinuti1080(), pesoSorgente, ".mov", ".webm", new OpzioniVideo { Modo = "qualita" }, soloCpu).Codec);
    }

    [Fact]
    public void Lo_slider_ha_un_minimo_sensato_e_il_massimo_e_l_originale()
    {
        var p = PianoVideo.Calcola(DieciMinuti1080(), pesoSorgente, ".mov", ".mp4", new OpzioniVideo { Modo = "peso", PesoByte = 1 }, conNvidia);
        Assert.Equal(pesoSorgente, p.PesoMax);
        Assert.InRange(p.PesoMin, 5_000_000, 20_000_000);
        Assert.True(p.PesoStimato >= p.PesoMin * PianoVideo.Margine - 1);
    }

    [Fact]
    public void Hdr_in_h264_diventa_sdr_e_in_hevc_resta()
    {
        var hdr = DieciMinuti1080() with { Video = new InfoVideo("hevc", 3840, 2160, 30, 40_000_000, "yuv420p10le", true, "smpte2084", 10) };
        Assert.True(PianoVideo.Calcola(hdr, pesoSorgente, ".mov", ".mp4", new OpzioniVideo { Modo = "qualita", Codec = "h264" }, conNvidia).HdrInSdr);
        Assert.True(PianoVideo.Calcola(hdr, pesoSorgente, ".mov", ".mp4", new OpzioniVideo { Modo = "qualita", Codec = "hevc" }, conNvidia).HdrTenuto);
    }
}
