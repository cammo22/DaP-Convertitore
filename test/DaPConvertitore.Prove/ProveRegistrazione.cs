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
    public void LoSchermoESoloVideoENonChiedeWasapiNeDshow()
    {
        var a = ComandiRegistrazione.ArgomentiVideo("schermo", @"C:\out\reg.mp4", new ComandiRegistrazione.Schermo(-1920, 0, 1921, 1080)).ToList();
        Assert.Contains("gdigrab", a);
        Assert.Contains("desktop", a);
        Assert.Equal("-1920", a[a.IndexOf("-offset_x") + 1]);
        Assert.Equal("1920x1080", a[a.IndexOf("-video_size") + 1]);
        Assert.Contains("libx264", a);
        Assert.Contains("-vf", a);
        Assert.DoesNotContain("dshow", a);
        Assert.DoesNotContain("wasapi", a);
        Assert.DoesNotContain("-c:a", a);
        Assert.Equal(@"C:\out\reg.mp4", a[^1]);
        Assert.Contains("-use_wallclock_as_timestamps", a);
        Assert.Equal(0, ComandiRegistrazione.RopAnteprima & ComandiRegistrazione.RopCaptureBlt);
    }

    [Fact]
    public void IlMuxCopiaIlVideoECodificaIlWav()
    {
        var a = ComandiRegistrazione.ArgomentiMux("v.mp4", "a.wav", "out.mp4").ToList();
        Assert.Contains("apad", a[a.IndexOf("-filter_complex") + 1]);
        Assert.Equal("copy", a[a.IndexOf("-c:v") + 1]);
        Assert.Equal("aac", a[a.IndexOf("-c:a") + 1]);
        Assert.Equal("160k", a[a.IndexOf("-b:a") + 1]);
        Assert.Contains("-shortest", a);
        Assert.DoesNotContain("wasapi", a);
        Assert.DoesNotContain("dshow", a);
        Assert.DoesNotContain("gdigrab", a);
        Assert.Equal("out.mp4", a[^1]);
    }

    [Fact]
    public void SoloAudioEUnM4aDalWav()
    {
        var a = ComandiRegistrazione.ArgomentiDaWav("preso.wav", "out.m4a").ToList();
        Assert.DoesNotContain("gdigrab", a);
        Assert.DoesNotContain("libx264", a);
        Assert.DoesNotContain("wasapi", a);
        Assert.DoesNotContain("dshow", a);
        Assert.Contains("aac", a);
        Assert.Equal("192k", a[a.IndexOf("-b:a") + 1]);
        Assert.Equal("preso.wav", a[a.IndexOf("-i") + 1]);
        Assert.Equal("out.m4a", a[^1]);
    }

    [Fact]
    public void LoSchermoSenzaAudioNonChiedeUnaTracciaAudio()
    {
        var a = ComandiRegistrazione.ArgomentiVideo("schermo", "film.mp4", null).ToList();
        Assert.Contains("gdigrab", a);
        Assert.Contains("desktop", a);
        Assert.DoesNotContain("-offset_x", a);
        Assert.DoesNotContain("dshow", a);
        Assert.DoesNotContain("wasapi", a);
        Assert.DoesNotContain("-c:a", a);
        Assert.Equal("film.mp4", a[^1]);
        var riga = ComandiRegistrazione.Riga(a);
        Assert.Contains("film.mp4", riga);
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
    public void IlFormatoNuovoDiFfmpegElencaIMicrofoniSenzaIntestazione()
    {
        // proprio l'uscita di ffmpeg n9.0.2 (build 20261109): niente «DirectShow audio devices»
        var testo = """
            [in#0 @ 000001dc56289c80] "usb-webcam" (video)
            [in#0 @ 000001dc56289c80]   Alternative name "@device_pnp_\\?\usb#vid_1234"
            [in#0 @ 000001dc56289c80] "Lavaliello (USB Microphone)" (audio)
            [in#0 @ 000001dc56289c80]   Alternative name "@device_cm_{33D9A762-90C8-11D0-BD43-00A0C911CE86}\wave_{A73B5C10-1111-2222-3333-444455556666}"
            [in#0 @ 000001dc56289c80] "Headset Microphone (Oculus Virtual Audio Device)" (audio)
            [in#0 @ 000001dc56289c80]   Alternative name "@device_cm_{33D9A762-90C8-11D0-BD43-00A0C911CE86}\wave_{A73B0002}"
            [in#0 @ 000001dc56289c80] "CABLE Output (VB-Audio Virtual Cable)" (audio)
            [in#0 @ 000001dc56289c80]   Alternative name "@device_cm_{33D9A762-90C8-11D0-BD43-00A0C911CE86}\wave_{A73B0003}"
            """;
        var nomi = ComandiRegistrazione.MicrofoniDaElenco(testo);
        Assert.Equal(
            new[]
            {
                "Lavaliello (USB Microphone)",
                "Headset Microphone (Oculus Virtual Audio Device)",
                "CABLE Output (VB-Audio Virtual Cable)",
            },
            nomi);
        Assert.DoesNotContain("usb-webcam", nomi);
        Assert.DoesNotContain(nomi, n => n.Contains("Alternative", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(nomi, n => n.StartsWith("@device", StringComparison.Ordinal));
    }

    [Fact]
    public void IlPrefissoVecchioEGliAccentiRestanoNelNome()
    {
        var testo = """
            [dshow @ 000001dc] DirectShow audio devices
            [dshow @ 000001dc]  "Cuffie àé (Realtek)"
            [dshow @ 000001dc]     Alternative name "@device_cm_{GUID}"
            """;
        Assert.Equal(new[] { "Cuffie àé (Realtek)" }, ComandiRegistrazione.MicrofoniDaElenco(testo));
        var utf8 = System.Text.Encoding.UTF8.GetBytes("[dshow @ 1] \"Cuffie àé\" (audio)\n");
        var letto = ComandiRegistrazione.DecodificaElenco(utf8);
        Assert.Contains("àé", letto);
        Assert.Equal(new[] { "Cuffie àé" }, ComandiRegistrazione.MicrofoniDaElenco(letto));
        var bom = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(System.Text.Encoding.UTF8.GetBytes("\"Microfono\" (audio)\n")).ToArray();
        Assert.Equal(new[] { "Microfono" }, ComandiRegistrazione.MicrofoniDaElenco(ComandiRegistrazione.DecodificaElenco(bom)));
        Assert.Contains("à", ComandiRegistrazione.DecodificaElenco(new byte[] { 0x22, 0xE0, 0x22, 0x20, 0x28, 0x61, 0x75, 0x64, 0x69, 0x6F, 0x29 }));
    }

    [Fact]
    public void FfmpegDevicesDiQuestaBuildNonHaWasapi()
    {
        var testo = """
             D. = Demuxing supported
             .E = Muxing supported
             --
             D  dshow           DirectShow capture
             D  gdigrab         GDI API Windows frame grabber
             D  lavfi           Libavfilter virtual input device
              E openal
             D  vfwcap          VfW video capture
            """;
        Assert.True(ComandiRegistrazione.HaIngresso(testo, "dshow"));
        Assert.True(ComandiRegistrazione.HaIngresso(testo, "gdigrab"));
        Assert.False(ComandiRegistrazione.HaIngresso(testo, "wasapi"));
        Assert.False(ComandiRegistrazione.HaIngresso(testo, "DirectShow"));
    }

    [Fact]
    public void LaFinestraRestaNelMonitorCheToccaDiPiu()
    {
        var sinistra = new Ancoraggio.Riquadro(-1920, 0, 1920, 1080);
        var principale = new Ancoraggio.Riquadro(0, 0, 1920, 1080);
        var schermi = new[] { sinistra, principale };
        var appesa = Ancoraggio.DentroSchermi(new Ancoraggio.Riquadro(-380, 40, 400, 300), schermi);
        Assert.True(Ancoraggio.Dentro(appesa, sinistra));
        var aCavallo = Ancoraggio.DentroSchermi(new Ancoraggio.Riquadro(-50, 10, 400, 200), schermi);
        Assert.True(Ancoraggio.Dentro(aCavallo, principale));
        var nelVuoto = Ancoraggio.DentroSchermi(new Ancoraggio.Riquadro(8000, 8000, 300, 200), schermi);
        Assert.True(Ancoraggio.Dentro(nelVuoto, principale) || Ancoraggio.Dentro(nelVuoto, sinistra));
        var grossa = Ancoraggio.DentroSchermi(new Ancoraggio.Riquadro(-2000, -10, 4000, 2000), schermi);
        Assert.True(grossa.L <= 1920 && grossa.A <= 1080);
        Assert.True(Ancoraggio.Dentro(grossa, sinistra) || Ancoraggio.Dentro(grossa, principale));
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
        var a = ComandiRegistrazione.ArgomentiVideo("finestra", "out.mp4", new ComandiRegistrazione.Schermo(0, 0, 800, 600), 60, 18, false, "Blocco note").ToList();
        Assert.Equal("60", a[a.IndexOf("-framerate") + 1]);
        Assert.Equal("0", a[a.IndexOf("-draw_mouse") + 1]);
        Assert.Equal("18", a[a.IndexOf("-crf") + 1]);
        Assert.Contains("title=Blocco note", a);
        Assert.DoesNotContain("desktop", a);
        Assert.DoesNotContain("-offset_x", a);
        Assert.Equal("Scegli la zona da registrare.", ComandiRegistrazione.MotivoRifiuto("zona", null, null, zonaSenzaRettangolo: true));
        Assert.Equal("Scegli la finestra da registrare.", ComandiRegistrazione.MotivoRifiuto("finestra", null, null, finestraSenzaTitolo: true));
        var zona = ComandiRegistrazione.ArgomentiVideo("zona", "z.mp4", new ComandiRegistrazione.Schermo(100, 80, 640, 480), 30, 28, true, null).ToList();
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
            Storico.Togli(file);
            Assert.Empty(Storico.Recenti(6).Recenti);
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
