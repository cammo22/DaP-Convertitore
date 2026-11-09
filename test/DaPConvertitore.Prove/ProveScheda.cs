using DaP.Convertitore.Lettore;

namespace DaP.Convertitore.Prove;

/// <summary>Copertine disegnate e nomi accanto: non serve FFmpeg.</summary>
public class ProveCopertine
{
    [Fact]
    public void IlNomeAccantoNonSovrascrive()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dap-copertina-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllBytes(Path.Combine(dir, "brano (copertina).jpg"), [1]);
            var primo = Copertine.NomeLibero(dir, "brano", ".jpg");
            Assert.Equal(Path.Combine(dir, "brano (copertina 2).jpg"), primo);
            File.WriteAllBytes(primo, [2]);
            var secondo = Copertine.NomeLibero(dir, "brano", ".jpg");
            Assert.EndsWith("(copertina 3).jpg", secondo);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void LeCopertineDisegnateSonoPngUgualiPerLoStessoTitolo()
    {
        var a = Copertine.Genera("Poco dopo lo zero", 0, null);
        var b = Copertine.Genera("Poco dopo lo zero", 0, null);
        var c = Copertine.Genera("Poco dopo lo zero", 1, null);
        var d = Copertine.Genera("Altro titolo", 3, null);
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
        Assert.NotEqual(a, d);
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, a[..8]);
        Assert.True(a.Length > 1000);
        // IHDR: larghezza subito dopo la firma, la lunghezza e il tipo
        Assert.Equal(Copertine.Lato, (a[16] << 24) | (a[17] << 16) | (a[18] << 8) | a[19]);
        Assert.Equal(Copertine.Lato, (a[20] << 24) | (a[21] << 16) | (a[22] << 8) | a[23]);
    }

    [Fact]
    public void IlVideoTieneLaCopertinaDentroIlFile()
    {
        Assert.True(Copertine.VideoNelFile("film.mp4"));
        Assert.True(Copertine.VideoNelFile("film.MKV"));
        Assert.True(Copertine.DentroIlFile("film.mov"));
        Assert.False(Copertine.VideoNelFile("film.webm"));
        Assert.False(Copertine.AudioNelFile("film.mp4"));
    }

    [Fact]
    public void LaCartellaDeiLavoriStaFraLeImmagini()
    {
        var p = CartellaLavori.DiSistema();
        Assert.EndsWith(CartellaLavori.Nome, p);
        Assert.Contains("DaPConvertitore", p);
    }

    [Fact]
    public void ICampiSonoQuelliCheIlFormatoTiene()
    {
        Assert.Contains("copertina", Metadati.Campi(".mp3"));
        Assert.Contains("artista", Metadati.Campi(".FLAC"));
        Assert.DoesNotContain("copertina", Metadati.Campi(".wav"));
        Assert.DoesNotContain("copertina", Metadati.Campi(".ogg"));
        Assert.Equal(new[] { "titolo", "commento" }, Metadati.Campi(".mkv"));
        Assert.Equal(new[] { "data", "descrizione", "autore" }, Metadati.Campi(".jpg"));
        Assert.Empty(Metadati.Campi(".png"));
        Assert.Equal(new DateTime(2024, 3, 12), Metadati.DataDi("2024-03-12")!.Value.Date);
        Assert.Equal(2026, Metadati.DataDi("2026")!.Value.Year);
        Assert.Equal(12, Metadati.DataDi("12/03/2024")!.Value.Day);
        Assert.Null(Metadati.DataDi("ieri"));
    }
}

/// <summary>I tag e la copertina scritti davvero, nel convertito o nel file, con la bozza che non tocca l'originale se fallisce.</summary>
[Collection("banco")]
public class ProveScheda(Banco b)
{
    [Fact]
    public async Task CopertinaNelFileEOriginaleIntattoSeMancaLImmagine()
    {
        var mp3 = b.Ffmpeg("dentro.mp3", "-f", "lavfi", "-i", "sine=f=440:d=1", "-c:a", "libmp3lame", "-q:a", "7", "-metadata", "title=Ciao");
        var prima = await File.ReadAllBytesAsync(mp3);
        await Assert.ThrowsAsync<ErroreConversione>(() => Copertine.ScriviNelFile(b.Strumenti, mp3, Path.Combine(b.Cartella, "non-c-e.png")));
        Assert.Equal(prima, await File.ReadAllBytesAsync(mp3));

        var png = Path.Combine(b.Cartella, "cover-gen.png");
        await File.WriteAllBytesAsync(png, Copertine.Genera("Ciao", 2, null));
        await Copertine.ScriviNelFile(b.Strumenti, mp3, png);
        var info = await Sonda.Leggi(b.Strumenti, mp3);
        Assert.True(info.Copertina);
        var scheda = await Media.Leggi(b.Strumenti, mp3);
        Assert.Equal("Ciao", scheda.Tag["title"]);
        var jpg = await Media.Copertina(b.Strumenti, mp3, true);
        Assert.NotNull(jpg);
        Assert.True(jpg!.Length > 100);

        var salvata = await Copertine.SalvaAccanto(b.Strumenti, mp3);
        Assert.EndsWith("dentro (copertina).jpg", salvata.Replace('\\', '/'));
        Assert.True(new FileInfo(salvata).Length > 100);
        var seconda = await Copertine.SalvaAccanto(b.Strumenti, mp3);
        Assert.EndsWith("(copertina 2).jpg", seconda);
        Assert.True(File.Exists(salvata));
    }

    [Fact]
    public async Task ITagELaCopertinaFinisconoNelConvertito()
    {
        var wav = b.Ffmpeg("scheda.wav", "-f", "lavfi", "-i", "sine=f=330:d=1", "-metadata", "title=Vecchio");
        var o = new Opzioni
        {
            Audio = { Kbps = 128 },
            Scheda = new OpzioniScheda
            {
                Scrivi = true,
                Titolo = "Poco dopo lo zero",
                Artista = "DaProd",
                Album = "Notte",
                Anno = "2026",
                Traccia = "4",
                Copertina = "generata:0",
            },
        };
        var l = await b.Converti(wav, "audio.mp3", o);
        var s = await Media.Leggi(b.Strumenti, l.Uscita!);
        Assert.Equal("Poco dopo lo zero", s.Tag["title"]);
        Assert.Equal("DaProd", s.Tag["artist"]);
        Assert.Equal("Notte", s.Tag["album"]);
        Assert.Contains("2026", s.Tag["date"]);
        Assert.StartsWith("4", s.Tag["track"]);
        Assert.True(s.Info.Copertina);
        // l'originale non viene riscritto
        var orig = await Media.Leggi(b.Strumenti, wav);
        Assert.False(orig.Info.Copertina);
        Assert.True(new FileInfo(wav).Length > 0);
    }

    [Fact]
    public async Task LaCopertinaDelVideoEUnFotogrammaAllegato()
    {
        var mp4 = b.Ffmpeg("film-cover.mp4", "-f", "lavfi", "-i", "testsrc2=s=160x90:r=15:d=1", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-preset", "ultrafast");
        var png = Path.Combine(b.Cartella, "cover-video.png");
        await File.WriteAllBytesAsync(png, Copertine.Genera("Film", 1, null));
        var prima = await File.ReadAllBytesAsync(mp4);
        await Copertine.ScriviNelFile(b.Strumenti, mp4, png);
        Assert.NotEqual(prima, await File.ReadAllBytesAsync(mp4));
        var info = await Sonda.Leggi(b.Strumenti, mp4);
        Assert.True(info.Copertina);
        Assert.True(info.IndiceCopertina >= 0);
        Assert.Equal(1, info.VideoVeri);
        var jpg = await Copertine.EstraiAllegata(b.Strumenti, mp4, info.IndiceCopertina, 160, true);
        Assert.NotNull(jpg);
        Assert.True(jpg!.Length > 80);
        Assert.InRange(info.Durata, 0.4, 2.5);
    }

    [Fact]
    public async Task IlVideoConvertitoTieneTitoloECommento()
    {
        var mp4 = b.Ffmpeg("film-scheda.mp4", "-f", "lavfi", "-i", "testsrc2=s=160x90:r=15:d=1", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-preset", "ultrafast");
        var o = new Opzioni
        {
            Video = { Modo = "copia" },
            Scheda = new OpzioniScheda { Scrivi = true, Titolo = "Episodio", Commento = "Una nota" },
        };
        var l = await b.Converti(mp4, "video.mkv", o);
        var s = await Media.Leggi(b.Strumenti, l.Uscita!);
        Assert.Equal("Episodio", s.Tag["title"]);
        var commento = s.Tag.TryGetValue("comment", out var c) ? c : s.Tag["description"];
        Assert.Equal("Una nota", commento);
    }

    [Fact]
    public async Task LaFotoConvertitaTieneAutoreEData()
    {
        var png = b.Ffmpeg("scheda-foto.png", "-f", "lavfi", "-i", "testsrc2=s=320x200", "-frames:v", "1");
        var o = new Opzioni
        {
            Immagine = { Qualita = 90, Modo = "qualita" },
            Scheda = new OpzioniScheda { Scrivi = true, Autore = "Cammo", Descrizione = "Il mare", DataScatto = "2024-03-12" },
        };
        var l = await b.Converti(png, "img.jpg", o);
        var letti = await Immagini.LeggiTesti(l.Uscita!);
        Assert.Equal("Cammo", letti.Autore);
        Assert.Contains("mare", letti.Descrizione);
        Assert.Equal(2024, letti.Data!.Value.Year);
        Assert.Equal(3, letti.Data.Value.Month);
        Assert.Equal(12, letti.Data.Value.Day);
    }
}
