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

    [Fact]
    public void FpsQualitaCursoreEFinestraEntranoNelComando()
    {
        Assert.Equal(30, ComandiRegistrazione.FpsDi(24));
        Assert.Equal(60, ComandiRegistrazione.FpsDi(60));
        Assert.Equal(18, ComandiRegistrazione.CrfDi("alta"));
        Assert.Equal(28, ComandiRegistrazione.CrfDi("leggera"));
        Assert.Equal(23, ComandiRegistrazione.CrfDi(null));
        var a = ComandiRegistrazione.Argomenti("finestra", null, null, "out.mp4", new ComandiRegistrazione.Schermo(0, 0, 800, 600), 60, 18, false, "Blocco note").ToList();
        Assert.Equal("60", a[a.IndexOf("-framerate") + 1]);
        Assert.Equal("0", a[a.IndexOf("-draw_mouse") + 1]);
        Assert.Equal("18", a[a.IndexOf("-crf") + 1]);
        Assert.Contains("title=Blocco note", a);
        Assert.DoesNotContain("desktop", a);
        Assert.DoesNotContain("-offset_x", a);
        Assert.Equal("Scegli la zona da registrare.", ComandiRegistrazione.MotivoRifiuto("zona", null, null, zonaSenzaRettangolo: true));
        Assert.Equal("Scegli la finestra da registrare.", ComandiRegistrazione.MotivoRifiuto("finestra", null, null, finestraSenzaTitolo: true));
        var zona = ComandiRegistrazione.Argomenti("zona", null, null, "z.mp4", new ComandiRegistrazione.Schermo(100, 80, 640, 480), 30, 28, true, null).ToList();
        Assert.Contains("desktop", zona);
        Assert.Equal("640x480", zona[zona.IndexOf("-video_size") + 1]);
        Assert.Equal("28", zona[zona.IndexOf("-crf") + 1]);
        Assert.Equal(new[] { "Blocco note", "Chrome" }, ComandiRegistrazione.TitoliUtili(["", "Registra", "Blocco note", "Blocco note", "Program Manager", "Chrome"]));
        Assert.Contains("-vcodec", ComandiRegistrazione.ArgomentiFotogramma("film.mp4").ToList());
    }

    [Fact]
    public void LaFinestraRestaNelMonitorDellAvatarAncheSeENegativo()
    {
        var area = new Ancoraggio.Riquadro(-1920, 0, 1920, 1040);
        var avatar = new Ancoraggio.Riquadro(-180, 800, 140, 140);
        var dove = Ancoraggio.Accanto(avatar, 400, 500, area);
        Assert.True(Ancoraggio.Dentro(dove, area));
        Assert.True(dove.X < 0, "deve restare sul monitor a sinistra, non sul principale");
        // l'avatar è contro il bordo destro: a destra non ci sta, quindi va a sinistra
        Assert.True(dove.Destra <= avatar.X);
    }

    [Fact]
    public void UnAvatarFuoriDalloSchermoNonPortaLaFinestraFuori()
    {
        var area = new Ancoraggio.Riquadro(0, 0, 1920, 1080);
        var dove = Ancoraggio.Accanto(new Ancoraggio.Riquadro(5000, 4000, 120, 120), 400, 300, area);
        Assert.True(Ancoraggio.Dentro(dove, area));
        var centro = Ancoraggio.Centro(400, 300, area);
        Assert.Equal(760, centro.X);
        Assert.Equal(390, centro.Y);
        Assert.True(Ancoraggio.Dentro(centro, area));
        var stretto = Ancoraggio.Accanto(new Ancoraggio.Riquadro(10, 10, 80, 80), 400, 200, area);
        Assert.True(Ancoraggio.Dentro(stretto, area));
        Assert.True(stretto.X >= 10 + 80);
    }

    [Fact]
    public void LaChiaveDelMonitorSiLeggeESiRiscrive()
    {
        var r = new Ancoraggio.Riquadro(-1920, 12, 1920, 1080);
        Assert.True(Ancoraggio.ProvaChiave(Ancoraggio.Chiave(r), out var diNuovo));
        Assert.Equal(r, diNuovo);
        Assert.False(Ancoraggio.ProvaChiave("no", out _));
        Assert.True(Ancoraggio.Tocca(r, new Ancoraggio.Riquadro(-10, 0, 20, 20)));
        Assert.False(Ancoraggio.Tocca(r, new Ancoraggio.Riquadro(100, 0, 20, 20)));
    }

    [Fact]
    public void ScreenshotERegistrazioneEntranoNeiRecentiSenzaContareComeConversione()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dap-storico-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var prima = Environment.GetEnvironmentVariable("DAP_DATI");
        Environment.SetEnvironmentVariable("DAP_DATI", dir);
        try
        {
            var file = Path.Combine(dir, "schermo.png");
            File.WriteAllBytes(file, [1, 2, 3, 4]);
            Storico.AggiungiFile(file, "Screenshot");
            var (recenti, totali) = Storico.Recenti(6);
            Assert.Single(recenti);
            Assert.Equal(file, recenti[0].Uscita);
            Assert.Equal("Screenshot", recenti[0].Formato);
            Assert.Equal("immagine", recenti[0].Categoria);
            Assert.Equal(4, recenti[0].PesoDopo);
            Assert.Equal(0, totali.Conversioni);
            File.Delete(file);
            Assert.Empty(Storico.Recenti(6).Recenti);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DAP_DATI", prima);
            try { Directory.Delete(dir, true); } catch { /* il temp la toglie dopo */ }
        }
    }
}
