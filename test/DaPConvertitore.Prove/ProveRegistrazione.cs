namespace DaP.Convertitore.Prove;

/// <summary>
/// Il comando di FFmpeg e la strada «Registra» (vassoio, avatar, ponte) senza finestre e senza FFmpeg.
/// </summary>
public class ProveRegistrazione
{
    const string Mic = "Microphone (Realtek(R) Audio)";
    const string Casse = "Speakers (Realtek(R) Audio)";

    [Fact]
    public void IlMenuIlVassoioEIlPonteFannoLaStessaCosa()
    {
        foreach (var da in ComandiRegistrazione.Origini)
        {
            Assert.Equal(ComandiRegistrazione.ApriMenu, ComandiRegistrazione.CosaFa(da, false));
            Assert.Equal(ComandiRegistrazione.PortaBarra, ComandiRegistrazione.CosaFa(da, true));
        }
        Assert.Equal(3, ComandiRegistrazione.Origini.Length);
        Assert.Contains("vassoio", ComandiRegistrazione.Origini);
        Assert.Contains("avatar", ComandiRegistrazione.Origini);
        Assert.Contains("ponte", ComandiRegistrazione.Origini);
    }

    [Fact]
    public void SoloAudioSenzaDispositiviNonParte()
    {
        Assert.Null(ComandiRegistrazione.MotivoRifiuto("schermo", null, null));
        Assert.Null(ComandiRegistrazione.MotivoRifiuto("audio", Mic, null));
        Assert.Null(ComandiRegistrazione.MotivoRifiuto("audio", null, Casse));
        Assert.NotNull(ComandiRegistrazione.MotivoRifiuto("audio", null, null));
        Assert.NotNull(ComandiRegistrazione.MotivoRifiuto("audio", "  ", ""));
    }

    [Fact]
    public void LoSchermoPrendeIlMonitorPrincipaleEIlMicrofonoEUnArgomentoSolo()
    {
        var a = ComandiRegistrazione.Argomenti("schermo", Mic, null, @"C:\out\reg.mp4", new ComandiRegistrazione.Schermo(-1920, 0, 1921, 1080)).ToList();
        var testo = string.Join("\n", a);
        Assert.Contains("-f", a);
        Assert.Contains("gdigrab", a);
        Assert.Contains("desktop", a);
        Assert.Equal("-1920", a[a.IndexOf("-offset_x") + 1]);
        Assert.Equal("1920x1080", a[a.IndexOf("-video_size") + 1]);
        var ingresso = a[a.IndexOf("-i", a.IndexOf("dshow")) + 1];
        Assert.Equal("audio=" + Mic, ingresso);
        Assert.DoesNotContain("\"", ingresso);
        Assert.Contains("-c:v", a);
        Assert.Contains("libx264", a);
        Assert.Contains("-map", a);
        Assert.Equal("0:v", a[a.IndexOf("-map") + 1]);
        Assert.Equal("1:a", a[a.LastIndexOf("-map") + 1]);
        Assert.DoesNotContain("-filter_complex", a);
        Assert.Equal(@"C:\out\reg.mp4", a[^1]);
        Assert.Contains("-use_wallclock_as_timestamps", a);
        Assert.DoesNotContain("wasapi", testo.Split('\n'));
    }

    [Fact]
    public void DueAudioSiMescolanoSenzaUsareVfInsiemeAlFiltro()
    {
        var a = ComandiRegistrazione.Argomenti("schermo", Mic, Casse, "out.mp4", new ComandiRegistrazione.Schermo(0, 0, 1280, 720)).ToList();
        Assert.Contains("-filter_complex", a);
        Assert.DoesNotContain("-vf", a);
        var filtro = a[a.IndexOf("-filter_complex") + 1];
        Assert.Contains("amix=inputs=2", filtro);
        Assert.Contains("scale=trunc(iw/2)*2:trunc(ih/2)*2", filtro);
        Assert.Equal("[v]", a[a.IndexOf("-map") + 1]);
        Assert.Equal("[a]", a[a.LastIndexOf("-map") + 1]);
        Assert.Equal("audio=" + Mic, a[a.IndexOf("-i", a.IndexOf("dshow")) + 1]);
        var wasapi = a.IndexOf("wasapi");
        Assert.Equal("1", a[a.IndexOf("-loopback") + 1]);
        Assert.Equal(Casse, a[a.IndexOf("-i", wasapi) + 1]);
        Assert.DoesNotContain("\"", a[a.IndexOf("-i", wasapi) + 1]);
    }

    [Fact]
    public void SoloAudioEUnM4aSenzaVideo()
    {
        var a = ComandiRegistrazione.Argomenti("audio", null, Casse, "out.m4a", null).ToList();
        Assert.DoesNotContain("gdigrab", a);
        Assert.DoesNotContain("libx264", a);
        Assert.Contains("wasapi", a);
        Assert.Contains("aac", a);
        Assert.Equal("192k", a[a.IndexOf("-b:a") + 1]);
        Assert.Equal("out.m4a", a[^1]);
        Assert.DoesNotContain("dshow", a);
    }

    [Fact]
    public void SoloAudioConMicrofonoECasseHaIlFiltroSuiDueIngressi()
    {
        var a = ComandiRegistrazione.Argomenti("audio", Mic, Casse, "out.m4a", null).ToList();
        var filtro = a[a.IndexOf("-filter_complex") + 1];
        Assert.Contains("[0:a][1:a]amix=inputs=2", filtro);
        Assert.DoesNotContain("scale=", filtro);
        Assert.Equal("[a]", a[a.IndexOf("-map") + 1]);
    }

    [Fact]
    public void LoSchermoSenzaAudioNonChiedeUnaTracciaAudio()
    {
        var a = ComandiRegistrazione.Argomenti("schermo", null, " ", "film.mp4", null).ToList();
        Assert.Contains("gdigrab", a);
        Assert.Contains("desktop", a);
        Assert.DoesNotContain("-offset_x", a);
        Assert.DoesNotContain("dshow", a);
        Assert.DoesNotContain("wasapi", a);
        Assert.DoesNotContain("-c:a", a);
        Assert.DoesNotContain("-map", a);
        Assert.Equal("film.mp4", a[^1]);
    }

    [Fact]
    public void LeVirgoletteDelNomeNonEntranoNelDispositivo()
    {
        Assert.Equal("audio=Cuffie", ComandiRegistrazione.ArgomentoDshow("\"Cuffie\""));
        Assert.Equal("Cuffie", ComandiRegistrazione.ArgomentoWasapi("  \"Cuffie\"  "));
        var riga = ComandiRegistrazione.Riga(ComandiRegistrazione.Argomenti("audio", Mic, null, "a.m4a", null));
        Assert.Contains("\"audio=" + Mic + "\"", riga);
    }

    [Fact]
    public void LElencoDshowTieneIMicrofoniENonLaWebcam()
    {
        var testo = """
            [dshow @ 1] DirectShow video devices (some may be both video and audio devices)
            [dshow @ 1]  "Integrated Camera"
            [dshow @ 1]     Alternative name "@device_pnp_\\?\usb#vid"
            [dshow @ 1] DirectShow audio devices
            [dshow @ 1]  "Microphone (Realtek(R) Audio)"
            [dshow @ 1]     Alternative name "@device_cm_{33D9A762}\\wave"
            [dshow @ 1]  "Stereo Mix (Realtek(R) Audio)"
            """;
        var nomi = ComandiRegistrazione.MicrofoniDaElenco(testo);
        Assert.Equal(new[] { "Microphone (Realtek(R) Audio)", "Stereo Mix (Realtek(R) Audio)" }, nomi);
    }

    [Fact]
    public void LElencoWasapiTieneLeUsciteENonIlMicrofono()
    {
        var testo = """
            [wasapi @ 1] "Speakers (Realtek(R) Audio)" (playback) (default)
            [wasapi @ 1] "Headphones" (playback)
            [wasapi @ 1] "Microphone (Realtek(R) Audio)" (capture)
            """;
        var nomi = ComandiRegistrazione.UsciteDaElenco(testo);
        Assert.Equal(new[] { "Speakers (Realtek(R) Audio)", "Headphones" }, nomi);
    }

    [Fact]
    public void SenzaLaParolaPlaybackRestanoTuttiTranneIlCapture()
    {
        var testo = """
            [wasapi @ 1] "Casse"
            [wasapi @ 1] "Mic" (capture)
            """;
        Assert.Equal(new[] { "Casse" }, ComandiRegistrazione.UsciteDaElenco(testo));
    }
}
